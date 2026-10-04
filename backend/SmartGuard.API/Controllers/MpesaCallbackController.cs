using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.API.Controllers;

[ApiController]
[Route("api/payments/mpesa")]
public sealed class MpesaCallbackController(AppDbContext db, IConfiguration configuration, ILogger<MpesaCallbackController> logger) : ControllerBase
{
    [HttpPost("callback/{callbackToken}")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Callback(string callbackToken, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var configuredToken = configuration["Daraja:CallbackToken"];
        if (string.IsNullOrWhiteSpace(configuredToken) || !FixedTimeEquals(callbackToken, configuredToken))
            return NotFound();

        if (!TryGetProperty(payload, "Body", out var body) || !TryGetProperty(body, "stkCallback", out var callback))
            return BadRequest(new { message = "Invalid M-PESA callback payload." });

        var checkoutId = GetString(callback, "CheckoutRequestID");
        var merchantId = GetString(callback, "MerchantRequestID");
        var resultCode = GetInt(callback, "ResultCode");
        if (string.IsNullOrWhiteSpace(checkoutId) || resultCode is null)
            return BadRequest(new { message = "M-PESA callback is missing its checkout or result identifier." });

        var transaction = await db.PaymentTransactions.FirstOrDefaultAsync(x => x.CheckoutRequestId == checkoutId, cancellationToken);
        if (transaction is null)
        {
            logger.LogWarning("Received M-PESA callback for an unknown checkout request {CheckoutRequestId}.", checkoutId);
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }

        if (transaction.Status != PaymentStatuses.Pending)
            return Ok(new { ResultCode = 0, ResultDesc = "Already processed" });

        if (!string.IsNullOrWhiteSpace(transaction.MerchantRequestId) && !string.Equals(transaction.MerchantRequestId, merchantId, StringComparison.Ordinal))
        {
            transaction.Status = PaymentStatuses.Failed;
            transaction.ResponseDescription = "M-PESA merchant request ID did not match the payment request.";
            transaction.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Merchant request ID mismatch for payment transaction {PaymentTransactionId}.", transaction.Id);
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }

        transaction.ResultCode = resultCode;
        transaction.CompletedAt = DateTimeOffset.UtcNow;
        transaction.ResponseDescription = GetString(callback, "ResultDesc");

        if (resultCode != 0)
        {
            transaction.Status = PaymentStatuses.Failed;
            await db.SaveChangesAsync(cancellationToken);
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }

        var metadata = ReadMetadata(callback);
        if (metadata.Amount != transaction.AmountKes || string.IsNullOrWhiteSpace(metadata.Receipt))
        {
            transaction.Status = PaymentStatuses.Failed;
            transaction.ResponseDescription = "M-PESA payment amount or receipt did not match the subscription checkout.";
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("M-PESA callback amount or receipt mismatch for transaction {PaymentTransactionId}.", transaction.Id);
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }

        if (await db.Payments.AnyAsync(x => x.MpesaReceiptNumber == metadata.Receipt, cancellationToken))
        {
            transaction.Status = PaymentStatuses.Failed;
            transaction.ResponseDescription = "M-PESA receipt number has already been recorded.";
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Duplicate M-PESA receipt {MpesaReceiptNumber} was rejected.", metadata.Receipt);
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var subscription = await db.Subscriptions.FirstOrDefaultAsync(x => x.Id == transaction.SubscriptionId && x.UserId == transaction.UserId, cancellationToken);
        if (subscription is null)
        {
            transaction.Status = PaymentStatuses.Failed;
            transaction.ResponseDescription = "Subscription was not found for this payment.";
            await db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
            return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
        }

        var paidAt = DateTimeOffset.UtcNow;
        transaction.Status = PaymentStatuses.Succeeded;
        transaction.MpesaReceiptNumber = metadata.Receipt;
        subscription.PlanCode = transaction.PlanCode;
        subscription.Status = SubscriptionStatuses.PendingApproval;
        subscription.TrialStart = null;
        subscription.TrialEnd = null;
        subscription.CurrentPeriodStart = null;
        subscription.CurrentPeriodEnd = null;
        subscription.CancelAtPeriodEnd = false;
        subscription.UpdatedAt = paidAt;

        var payment = new Payment
        {
            PaymentTransactionId = transaction.Id,
            UserId = transaction.UserId,
            SubscriptionId = subscription.Id,
            AmountKes = transaction.AmountKes,
            MpesaReceiptNumber = metadata.Receipt,
            PaidAt = paidAt
        };
        db.Payments.Add(payment);
        db.Invoices.Add(new Invoice
        {
            InvoiceNumber = $"SG-{paidAt:yyyyMMdd}-{payment.Id.ToString("N")[..8].ToUpperInvariant()}",
            PaymentId = payment.Id,
            UserId = transaction.UserId,
            SubscriptionId = subscription.Id,
            AmountKes = transaction.AmountKes,
            IssuedAt = paidAt
        });

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
    }

    private static (int? Amount, string? Receipt) ReadMetadata(JsonElement callback)
    {
        if (!TryGetProperty(callback, "CallbackMetadata", out var metadata) || !TryGetProperty(metadata, "Item", out var items) || items.ValueKind != JsonValueKind.Array)
            return (null, null);
        int? amount = null;
        string? receipt = null;
        foreach (var item in items.EnumerateArray())
        {
            var name = GetString(item, "Name");
            if (string.Equals(name, "Amount", StringComparison.OrdinalIgnoreCase) && TryGetProperty(item, "Value", out var amountValue))
            {
                if (amountValue.ValueKind == JsonValueKind.Number && amountValue.TryGetDecimal(out var decimalAmount)) amount = (int)decimalAmount;
                else if (int.TryParse(amountValue.ToString(), out var parsedAmount)) amount = parsedAmount;
            }
            else if (string.Equals(name, "MpesaReceiptNumber", StringComparison.OrdinalIgnoreCase))
            {
                receipt = GetString(item, "Value");
            }
        }
        return (amount, receipt);
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in element.EnumerateObject())
            {
                if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate.Value;
                    return true;
                }
            }
        }
        property = default;
        return false;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var property) && property.ValueKind != JsonValueKind.Null ? property.ToString() : null;

    private static int? GetInt(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var property) && int.TryParse(property.ToString(), out var result) ? result : null;

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
