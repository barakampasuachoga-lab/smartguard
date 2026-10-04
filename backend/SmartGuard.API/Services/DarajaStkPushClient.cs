using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartGuard.API.Services;

public sealed class DarajaStkPushClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Daraja:ConsumerKey"]) &&
        !string.IsNullOrWhiteSpace(configuration["Daraja:ConsumerSecret"]) &&
        !string.IsNullOrWhiteSpace(configuration["Daraja:ShortCode"]) &&
        !string.IsNullOrWhiteSpace(configuration["Daraja:Passkey"]) &&
        (!string.IsNullOrWhiteSpace(configuration["Daraja:CallbackUrl"]) || !string.IsNullOrWhiteSpace(configuration["RENDER_EXTERNAL_URL"])) &&
        !string.IsNullOrWhiteSpace(configuration["Daraja:CallbackToken"]);

    public async Task<StkPushResult> InitiateAsync(int amountKes, string phone, string reference, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Daraja is not configured. Set the Daraja credentials, public callback URL, and callback token in the backend environment.");
        }

        var baseUrl = (configuration["Daraja:BaseUrl"] ?? "https://sandbox.safaricom.co.ke").TrimEnd('/');
        var callbackUrl = configuration["Daraja:CallbackUrl"];
        if (string.IsNullOrWhiteSpace(callbackUrl) && Uri.TryCreate(configuration["RENDER_EXTERNAL_URL"], UriKind.Absolute, out var renderUrl))
        {
            callbackUrl = $"{renderUrl.ToString().TrimEnd('/')}/api/payments/mpesa/callback/{Uri.EscapeDataString(configuration["Daraja:CallbackToken"]!)}";
        }
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || baseUri.Host is not ("sandbox.safaricom.co.ke" or "api.safaricom.co.ke"))
        {
            throw new InvalidOperationException("Daraja BaseUrl must be the Safaricom sandbox or production endpoint.");
        }
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var callbackUri) || callbackUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Daraja CallbackUrl must be a public HTTPS URL.");
        }

        var client = httpClientFactory.CreateClient(nameof(DarajaStkPushClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/mpesa/stkpush/v1/processrequest");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(client, baseUrl, cancellationToken));

        var shortcode = configuration["Daraja:ShortCode"]!;
        var timestamp = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).ToString("yyyyMMddHHmmss");
        var password = Convert.ToBase64String(Encoding.UTF8.GetBytes(shortcode + configuration["Daraja:Passkey"] + timestamp));
        var payload = new StkPushRequest
        {
            BusinessShortCode = shortcode,
            Password = password,
            Timestamp = timestamp,
            TransactionType = configuration["Daraja:TransactionType"] ?? "CustomerPayBillOnline",
            Amount = amountKes,
            PartyA = phone,
            PartyB = shortcode,
            PhoneNumber = phone,
            CallBackURL = callbackUrl,
            AccountReference = reference,
            TransactionDesc = "SmartGuard subscription"
        };
        request.Content = new StringContent(JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = null }), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<StkPushResponse>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || result is null || result.ResponseCode != "0" || string.IsNullOrWhiteSpace(result.CheckoutRequestID))
        {
            throw new InvalidOperationException(result?.CustomerMessage ?? result?.ResponseDescription ?? $"Daraja rejected the STK Push request (HTTP {(int)response.StatusCode}).");
        }

        return new StkPushResult(result.MerchantRequestID, result.CheckoutRequestID, result.CustomerMessage ?? "STK Push sent. Check your phone and enter your M-PESA PIN.");
    }

    private async Task<string> GetAccessTokenAsync(HttpClient client, string baseUrl, CancellationToken cancellationToken)
    {
        if (_accessToken is not null && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) return _accessToken;
            var key = configuration["Daraja:ConsumerKey"]!;
            var secret = configuration["Daraja:ConsumerSecret"]!;
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/oauth/v1/generate?grant_type=client_credentials");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}")));
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Daraja returned an empty access token response.");
            _accessToken = token.AccessToken;
            var seconds = int.TryParse(token.ExpiresIn, out var expiry) ? expiry : 3600;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, seconds - 60));
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed class StkPushRequest
    {
        public string BusinessShortCode { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string Timestamp { get; init; } = string.Empty;
        public string TransactionType { get; init; } = string.Empty;
        public int Amount { get; init; }
        public string PartyA { get; init; } = string.Empty;
        public string PartyB { get; init; } = string.Empty;
        public string PhoneNumber { get; init; } = string.Empty;
        public string CallBackURL { get; init; } = string.Empty;
        public string AccountReference { get; init; } = string.Empty;
        public string TransactionDesc { get; init; } = string.Empty;
    }

    private sealed class StkPushResponse
    {
        [JsonPropertyName("MerchantRequestID")] public string? MerchantRequestID { get; init; }
        [JsonPropertyName("CheckoutRequestID")] public string? CheckoutRequestID { get; init; }
        [JsonPropertyName("ResponseCode")] public string? ResponseCode { get; init; }
        [JsonPropertyName("ResponseDescription")] public string? ResponseDescription { get; init; }
        [JsonPropertyName("CustomerMessage")] public string? CustomerMessage { get; init; }
    }

    private sealed class AccessTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; init; } = string.Empty;
        [JsonPropertyName("expires_in")] public string ExpiresIn { get; init; } = "3600";
    }
}

public sealed record StkPushResult(string? MerchantRequestId, string CheckoutRequestId, string Message);
