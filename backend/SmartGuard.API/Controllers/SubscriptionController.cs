using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartGuard.API.Services;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.API.Controllers;

[ApiController]
[Route("api/subscription")]
[Authorize]
public sealed class SubscriptionController(AppDbContext db, SubscriptionService subscriptions, DarajaStkPushClient daraja) : ControllerBase
{
    public sealed class CheckoutRequest
    {
        public string PlanCode { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<SubscriptionPlan>>> GetPlans(CancellationToken cancellationToken)
    {
        var plans = await db.SubscriptionPlans.AsNoTracking().OrderBy(x => x.PriceKes).ToListAsync(cancellationToken);
        return Ok(plans);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMySubscription(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var (subscription, plan) = await subscriptions.EnsureSubscriptionAsync(userId.Value, cancellationToken);
        var history = (await db.PaymentTransactions.AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.InitiatedAt)
            .Take(50)
            .ToList();
        var invoices = (await db.Invoices.AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.IssuedAt)
            .Select(x => new
            {
                x.Id,
                x.InvoiceNumber,
                x.PaymentId,
                x.UserId,
                x.SubscriptionId,
                x.AmountKes,
                x.Currency,
                x.IssuedAt,
                MpesaReceiptNumber = db.Payments.Where(payment => payment.Id == x.PaymentId).Select(payment => payment.MpesaReceiptNumber).FirstOrDefault()
            })
            .ToList();
        return Ok(new { subscription, plan, paymentTransactions = history, invoices });
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> StartCheckout([FromBody] CheckoutRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var planCode = request.PlanCode.Trim().ToUpperInvariant();
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Code == planCode, cancellationToken);
        if (plan is null) return BadRequest(new { message = "Choose one of the available SmartGuard plans." });
        if (!daraja.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "M-PESA payments are not configured yet. Set the Daraja sandbox credentials and public HTTPS callback URL on the backend."
            });
        }

        if (!TryNormalizeKenyanPhone(request.PhoneNumber, out var phone))
            return BadRequest(new { message = "Enter a Kenyan M-PESA mobile number, such as 0712345678." });

        var (subscription, _) = await subscriptions.EnsureSubscriptionAsync(userId.Value, cancellationToken);
        if (subscription.Status == SubscriptionStatuses.PendingApproval)
            return Conflict(new { message = "Your M-PESA payment has been confirmed and is waiting for administrator approval." });
        var pendingTransactions = await db.PaymentTransactions
            .Where(x => x.UserId == userId.Value && x.Status == PaymentStatuses.Pending)
            .ToListAsync(cancellationToken);
        var checkoutCutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
        foreach (var staleTransaction in pendingTransactions.Where(x => x.InitiatedAt <= checkoutCutoff))
        {
            staleTransaction.Status = PaymentStatuses.Failed;
            staleTransaction.ResponseDescription = "M-PESA payment prompt timed out.";
            staleTransaction.CompletedAt = DateTimeOffset.UtcNow;
        }
        if (pendingTransactions.Any(x => x.InitiatedAt > checkoutCutoff))
            return Conflict(new { message = "A payment request is already waiting for a response. Check your phone or wait a few minutes before retrying." });
        if (pendingTransactions.Count > 0) await db.SaveChangesAsync(cancellationToken);

        var paymentTransaction = new PaymentTransaction
        {
            UserId = userId.Value,
            SubscriptionId = subscription.Id,
            PlanCode = plan.Code,
            AmountKes = plan.PriceKes,
            PhoneNumber = phone,
            InitiatedAt = DateTimeOffset.UtcNow
        };
        db.PaymentTransactions.Add(paymentTransaction);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await daraja.InitiateAsync(plan.PriceKes, phone, $"SG-{paymentTransaction.Id.ToString("N")[..10]}", cancellationToken);
            paymentTransaction.MerchantRequestId = result.MerchantRequestId;
            paymentTransaction.CheckoutRequestId = result.CheckoutRequestId;
            paymentTransaction.ResponseDescription = result.Message;
            await db.SaveChangesAsync(cancellationToken);
            return Accepted(new { paymentTransaction.Id, paymentTransaction.Status, plan = plan.Name, amountKes = plan.PriceKes, phoneNumber = phone, message = result.Message });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            paymentTransaction.Status = PaymentStatuses.Failed;
            paymentTransaction.ResponseDescription = exception.Message;
            paymentTransaction.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "M-PESA could not start the payment prompt. Check the backend Daraja settings and try again." });
        }
    }

    [HttpGet("transactions/{id:guid}")]
    public async Task<IActionResult> GetTransaction(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var transaction = await db.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        return transaction is null ? NotFound() : Ok(transaction);
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized();
        var (subscription, _) = await subscriptions.EnsureSubscriptionAsync(userId.Value, cancellationToken);
        if (subscription.Status is SubscriptionStatuses.Active or SubscriptionStatuses.Trial)
        {
            subscription.CancelAtPeriodEnd = true;
            subscription.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return Ok(new { message = "Cancellation is scheduled for the end of the current access period.", subscription });
    }

    [HttpPost("admin/transactions/{transactionId:guid}/approve")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> ApprovePaidSubscription(Guid transactionId, CancellationToken cancellationToken)
    {
        var transaction = await db.PaymentTransactions.FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);
        if (transaction is null) return NotFound(new { message = "Payment transaction was not found." });
        if (transaction.Status != PaymentStatuses.Succeeded)
            return Conflict(new { message = "Only an M-PESA payment confirmed by the backend callback can be approved." });
        if (!await db.Payments.AnyAsync(x => x.PaymentTransactionId == transaction.Id, cancellationToken))
            return Conflict(new { message = "A confirmed payment record is required before subscription approval." });

        var subscription = await db.Subscriptions.FirstOrDefaultAsync(
            x => x.Id == transaction.SubscriptionId && x.UserId == transaction.UserId,
            cancellationToken);
        if (subscription is null) return NotFound(new { message = "The user's subscription was not found." });
        if (subscription.Status != SubscriptionStatuses.PendingApproval || subscription.PlanCode != transaction.PlanCode)
            return Conflict(new { message = "This payment is no longer waiting for subscription approval." });

        var approvedAt = DateTimeOffset.UtcNow;
        subscription.Status = SubscriptionStatuses.Active;
        subscription.CurrentPeriodStart = approvedAt;
        subscription.CurrentPeriodEnd = approvedAt.AddMonths(1);
        subscription.CancelAtPeriodEnd = false;
        subscription.UpdatedAt = approvedAt;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Payment approved and subscription activated.", subscription });
    }

    [HttpGet("admin/summary")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> GetAdminSummary(CancellationToken cancellationToken)
    {
        var subscriptionsList = await db.Subscriptions.AsNoTracking().ToListAsync(cancellationToken);
        var payments = await db.Payments.AsNoTracking().ToListAsync(cancellationToken);
        var users = await db.UserAccounts.AsNoTracking().ToDictionaryAsync(x => x.Id, x => new { x.FullName, x.Email }, cancellationToken);
        var plans = await db.SubscriptionPlans.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Name, cancellationToken);
        var paidThisMonth = payments.Where(x => x.PaidAt >= new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero)).Sum(x => x.AmountKes);
        var activeStatuses = subscriptionsList.Where(x => x.Status is SubscriptionStatuses.Active or SubscriptionStatuses.Trial)
            .Select(x => new { subscription = x, user = users.GetValueOrDefault(x.UserId), planName = plans.GetValueOrDefault(x.PlanCode) })
            .ToList();
        return Ok(new
        {
            revenueThisMonthKes = paidThisMonth,
            totalRevenueKes = payments.Sum(x => x.AmountKes),
            activeSubscriptions = subscriptionsList.Count(x => x.Status == SubscriptionStatuses.Active),
            trialSubscriptions = subscriptionsList.Count(x => x.Status == SubscriptionStatuses.Trial),
            pastDueSubscriptions = subscriptionsList.Count(x => x.Status == SubscriptionStatuses.PastDue),
            subscriptions = activeStatuses.Select(x => new
            {
                x.subscription.Id,
                x.subscription.UserId,
                userName = x.user?.FullName,
                email = x.user?.Email,
                plan = x.planName,
                x.subscription.Status,
                x.subscription.TrialEnd,
                x.subscription.CurrentPeriodEnd,
                x.subscription.CancelAtPeriodEnd
            }),
            latestPayments = payments.OrderByDescending(x => x.PaidAt).Take(50)
        });
    }

    [HttpGet("admin/payments")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> GetAdminPayments(CancellationToken cancellationToken)
    {
        var payments = (await db.Payments.AsNoTracking().ToListAsync(cancellationToken)).OrderByDescending(x => x.PaidAt).Take(200).ToList();
        var users = await db.UserAccounts.AsNoTracking().ToDictionaryAsync(x => x.Id, x => new { x.FullName, x.Email }, cancellationToken);
        return Ok(payments.Select(x => new { payment = x, user = users.GetValueOrDefault(x.UserId) }));
    }

    private int? CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static bool TryNormalizeKenyanPhone(string input, out string normalized)
    {
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0") && digits.Length == 10) digits = "254" + digits[1..];
        else if (digits.StartsWith("7") && digits.Length == 9 || digits.StartsWith("1") && digits.Length == 9) digits = "254" + digits;
        normalized = digits;
        return System.Text.RegularExpressions.Regex.IsMatch(digits, "^254[17][0-9]{8}$");
    }
}
