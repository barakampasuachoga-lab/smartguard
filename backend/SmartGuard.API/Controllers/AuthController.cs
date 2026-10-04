using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SmartGuard.API.Models;
using SmartGuard.API.Services;
using SmartGuard.Application.Auth;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IWebHostEnvironment _environment;
    private readonly SmtpEmailSender _emailSender;
    private readonly SubscriptionService? _subscriptionService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, JwtTokenService jwtTokenService, IWebHostEnvironment environment, SmtpEmailSender? emailSender = null, ILogger<AuthController>? logger = null, SubscriptionService? subscriptionService = null)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _environment = environment;
        _emailSender = emailSender ?? new SmtpEmailSender(new ConfigurationBuilder().Build());
        _subscriptionService = subscriptionService;
        _logger = logger ?? NullLogger<AuthController>.Instance;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserLoginResponse>> GetProfile()
    {
        var user = await GetCurrentUserAsync();
        return user is null ? Unauthorized() : Ok(ToUserResponse(user));
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<object>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(fullName) || !new EmailAddressAttribute().IsValid(email))
        {
            return BadRequest(new { message = "Enter a name and valid email address." });
        }

        if (await _context.UserAccounts.AnyAsync(x => x.Id != user.Id && x.Email.ToLower() == email))
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { message = "Current password is incorrect." });
            }

            if (request.NewPassword.Length < 8)
            {
                return BadRequest(new { message = "New password must be at least 8 characters long." });
            }

            user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        }

        user.FullName = fullName;
        user.Email = email;

        if (await _context.Properties.AnyAsync(x => x.OwnerUserId == user.Id))
        {
            var ownedProperties = await _context.Properties.Where(x => x.OwnerUserId == user.Id).ToListAsync();
            foreach (var property in ownedProperties)
            {
                property.Owner = fullName;
                property.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
        var token = _jwtTokenService.GenerateToken(user.Id, user.Email, user.FullName, user.Role);
        return Ok(new { token, user = ToUserResponse(user) });
    }

    [HttpPost("me/photo")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UserLoginResponse>> UpdateProfilePhoto(IFormFile? profilePhoto)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var (photoUrl, error) = await ProfilePhotoStorage.SaveAsync(profilePhoto, _environment);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var previousPhotoUrl = user.ProfilePhotoUrl;
        user.ProfilePhotoUrl = photoUrl;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            ProfilePhotoStorage.Delete(_environment, photoUrl);
            throw;
        }

        ProfilePhotoStorage.Delete(_environment, previousPhotoUrl);
        return Ok(ToUserResponse(user));
    }

    [HttpPost("register")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> Register([FromForm] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Full name, email, and password are required." });
        }

        var validation = AccountRegistrationValidator.Validate(request.FullName, request.Email, request.Password);
        if (!validation.IsValid)
        {
            return BadRequest(new { message = validation.ErrorMessage });
        }

        var normalizedEmail = validation.Email;
        var existingUser = await _context.UserAccounts
            .AnyAsync(x => x.Email.ToLower() == normalizedEmail.ToLower());

        if (existingUser)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        var (photoUrl, photoError) = await ProfilePhotoStorage.SaveAsync(request.ProfilePhoto, _environment);
        if (photoError is not null)
        {
            return BadRequest(new { message = photoError });
        }

        var user = new UserAccount
        {
            FullName = validation.FullName,
            Email = normalizedEmail,
            ProfilePhotoUrl = photoUrl,
            Role = validation.Role,
            PasswordHash = PasswordHasher.Hash(request.Password),
            IsBlocked = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.UserAccounts.Add(user);
        try
        {
            await _context.SaveChangesAsync();
            if (_subscriptionService is not null)
            {
                await _subscriptionService.EnsureSubscriptionAsync(user.Id);
            }
        }
        catch
        {
            ProfilePhotoStorage.Delete(_environment, photoUrl);
            throw;
        }

        return Ok(new
        {
            message = "Account created successfully. Please sign in to continue.",
            user = new UserLoginResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                ProfilePhotoUrl = user.ProfilePhotoUrl,
                Role = user.Role,
                IsBlocked = user.IsBlocked,
                LastLoginAt = user.LastLoginAt,
            }
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (!new EmailAddressAttribute().IsValid(email))
        {
            return BadRequest(new { message = "Enter a valid email address." });
        }

        if (!_emailSender.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Password reset email is not configured yet. Please contact support."
            });
        }

        var genericResponse = new { message = "If an account exists for that email, a password reset link will be sent." };
        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower(), cancellationToken);
        if (user is null)
        {
            return Ok(genericResponse);
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var tokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        var now = DateTimeOffset.UtcNow;

        var previousTokens = await _context.PasswordResetTokens
            .Where(x => x.UserAccountId == user.Id)
            .ToListAsync(cancellationToken);
        _context.PasswordResetTokens.RemoveRange(previousTokens);
        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            TokenHash = tokenHash,
            UserAccountId = user.Id,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30)
        });
        await _context.SaveChangesAsync(cancellationToken);

        var baseUrl = (HttpContext.RequestServices.GetRequiredService<IConfiguration>()["App:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var resetLink = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(token)}";
        var emailBody = $"Hello {user.FullName},\r\n\r\nUse this link to reset your SmartGuard password. It expires in 30 minutes and can only be used once:\r\n\r\n{resetLink}\r\n\r\nIf you did not request a password reset, you can ignore this email.";

        try
        {
            await _emailSender.SendAsync(user.Email, "Reset your SmartGuard password", emailBody, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedToken = await _context.PasswordResetTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
            if (failedToken is not null)
            {
                _context.PasswordResetTokens.Remove(failedToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            _logger.LogError(exception, "Failed to send a password reset email for SmartGuard user {UserId}.", user.Id);
        }

        return Ok(genericResponse);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "The password reset link is invalid or has expired. Request a new link and try again." });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8 || request.NewPassword.Length > 128)
        {
            return BadRequest(new { message = "Your new password must be between 8 and 128 characters." });
        }

        var tokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Token)));
        var now = DateTimeOffset.UtcNow;
        var resetToken = await _context.PasswordResetTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (resetToken is null || resetToken.UsedAt is not null || resetToken.ExpiresAt <= now)
        {
            return BadRequest(new { message = "The password reset link is invalid or has expired. Request a new link and try again." });
        }

        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Id == resetToken.UserAccountId, cancellationToken);
        if (user is null)
        {
            return BadRequest(new { message = "The password reset link is invalid or has expired. Request a new link and try again." });
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        var outstandingTokens = await _context.PasswordResetTokens
            .Where(x => x.UserAccountId == user.Id)
            .ToListAsync(cancellationToken);
        foreach (var outstandingToken in outstandingTokens)
        {
            outstandingToken.UsedAt = now;
        }
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Your password has been reset. You can now sign in." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        var user = await _context.UserAccounts
            .FirstOrDefaultAsync(x => x.Email.ToLower() == request.Email.Trim().ToLower());

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        if (user.IsBlocked)
        {
            return StatusCode(403, new { message = "This account has been blocked by the administrator." });
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _context.SaveChangesAsync();

        var token = _jwtTokenService.GenerateToken(user.Id, user.Email, user.FullName, user.Role);

        return Ok(new
        {
            token,
            user = ToUserResponse(user)
        });
    }

    [HttpGet("users")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<IEnumerable<UserLoginResponse>>> GetUsers()
    {
        var users = await _context.UserAccounts
            .OrderBy(x => x.Role)
            .ThenBy(x => x.FullName)
            .Select(x => new UserLoginResponse
            {
                Id = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                ProfilePhotoUrl = x.ProfilePhotoUrl,
                Role = x.Role,
                IsBlocked = x.IsBlocked,
                LastLoginAt = x.LastLoginAt,
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("users/{id:int}/details")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<object>> GetUserDetails(int id)
    {
        var user = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        var properties = await _context.Properties.AsNoTracking()
            .Where(x => x.OwnerUserId == user.Id)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Address,
                x.PhotoUrl,
                x.Owner,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();
        var propertyIds = properties.Select(x => x.Id).ToArray();
        var events = (await _context.SecurityEvents.AsNoTracking()
            .Where(x => propertyIds.Contains(x.PropertyId))
            .ToListAsync())
            .OrderByDescending(x => x.Timestamp)
            .Take(50)
            .ToList();
        var alerts = (await _context.Alerts.AsNoTracking()
            .Where(x => propertyIds.Contains(x.PropertyId))
            .ToListAsync())
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        var reports = (await _context.UserReports.AsNoTracking()
            .Where(x => x.RecipientUserId == user.Id)
            .ToListAsync())
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        var subscription = (await _context.Subscriptions.AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .ToListAsync())
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var subscriptionPlan = subscription is null
            ? null
            : await _context.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Code == subscription.PlanCode);
        var paymentTransactions = (await _context.PaymentTransactions.AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .ToListAsync())
            .OrderByDescending(x => x.InitiatedAt)
            .Take(20)
            .ToList();

        return Ok(new
        {
            user = new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.ProfilePhotoUrl,
                user.Role,
                user.IsBlocked,
                user.CreatedAt,
                user.LastLoginAt,
                user.LastLoginIp
            },
            properties,
            events,
            alerts,
            reports,
            subscription,
            subscriptionPlan,
            paymentTransactions
        });
    }

    [HttpPost("users/{id:int}/reports")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<UserReport>> SendUserReport(int id, [FromBody] CreateUserReportRequest request)
    {
        var recipient = await _context.UserAccounts.FindAsync(id);
        if (recipient is null)
        {
            return NotFound();
        }

        var title = request.Title.Trim();
        var body = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 160 || string.IsNullOrWhiteSpace(body) || body.Length > 5000)
        {
            return BadRequest(new { message = "Enter a title (up to 160 characters) and report body (up to 5,000 characters)." });
        }

        var senderId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var report = new UserReport
        {
            RecipientUserId = recipient.Id,
            SenderUserId = senderId,
            Title = title,
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.UserReports.Add(report);
        await _context.SaveChangesAsync();
        return Ok(report);
    }

    [HttpPost("users/{id:int}/email")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> SendUserEmail(int id, [FromBody] SendUserEmailRequest request, CancellationToken cancellationToken)
    {
        var recipient = await _context.UserAccounts.FindAsync(id);
        if (recipient is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var subject = request.Subject.Trim();
        var body = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 160 || string.IsNullOrWhiteSpace(body) || body.Length > 5000)
        {
            return BadRequest(new { message = "Enter a subject (up to 160 characters) and message (up to 5,000 characters)." });
        }

        if (!_emailSender.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Email delivery is not configured. Set the Email__Smtp__Host and Email__Smtp__FromAddress settings, then restart the API."
            });
        }

        var emailBody = $"Hello {recipient.FullName},\r\n\r\n{body}\r\n\r\nSmartGuard Administration";
        try
        {
            await _emailSender.SendAsync(recipient.Email, subject, emailBody, cancellationToken);
            return Ok(new { message = $"Email sent to {recipient.Email}." });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send an email to SmartGuard user {UserId} at {EmailAddress}.", recipient.Id, recipient.Email);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "The email could not be sent. Check the SMTP settings and try again."
            });
        }
    }

    [HttpGet("me/reports")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<UserReport>>> GetMyReports()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var reports = (await _context.UserReports
            .Where(x => x.RecipientUserId == user.Id)
            .ToListAsync())
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        var unreadReports = reports.Where(x => !x.IsRead).ToList();
        foreach (var report in unreadReports)
        {
            report.IsRead = true;
            report.ReadAt = DateTimeOffset.UtcNow;
        }

        if (unreadReports.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        return Ok(reports);
    }

    [HttpPatch("users/{id}/block")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> ToggleUserBlock(int id, [FromBody] ToggleUserBlockRequest request)
    {
        var user = await _context.UserAccounts.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.IsBlocked = request.IsBlocked;
        await _context.SaveChangesAsync();

        return Ok(new { id = user.Id, isBlocked = user.IsBlocked, email = user.Email });
    }

    private async Task<UserAccount?> GetCurrentUserAsync()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var userId))
        {
            return null;
        }

        return await _context.UserAccounts.FindAsync(userId);
    }

    private static UserLoginResponse ToUserResponse(UserAccount user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        ProfilePhotoUrl = user.ProfilePhotoUrl,
        Role = user.Role,
        IsBlocked = user.IsBlocked,
        LastLoginAt = user.LastLoginAt
    };
}
