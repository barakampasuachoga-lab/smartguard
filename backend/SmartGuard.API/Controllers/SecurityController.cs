using System.Text;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartGuard.Application.Auth;
using SmartGuard.Application.AnomalyDetection;
using SmartGuard.Application.Services;
using SmartGuard.API.Services;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecurityController : ControllerBase
{
    public class AlertActionRequest { public string? Comment { get; set; } public string? ResolutionNote { get; set; } }
    public class TrustedContactRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Days { get; set; } = "Everyday";
        public string StartTime { get; set; } = "06:00";
        public string EndTime { get; set; } = "22:00";
        public bool IsActive { get; set; } = true;
    }
    public class SecurityCheckInRequest { public string? Note { get; set; } }
    public class CreatePropertyRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public int? OwnerUserId { get; set; }
        public string Status { get; set; } = nameof(PropertyStatus.Home);
        public IFormFile? PropertyPhoto { get; set; }
    }
    private readonly AppDbContext _context;
    private readonly SecurityDashboardService _dashboardService;
    private readonly IWebHostEnvironment? _environment;
    private readonly SubscriptionService? _subscriptions;
    private readonly AnomalyRuleEngine _anomalyRuleEngine = new();

    public SecurityController(AppDbContext context, SecurityDashboardService dashboardService, IWebHostEnvironment? environment = null, SubscriptionService? subscriptions = null)
    {
        _context = context;
        _dashboardService = dashboardService;
        _environment = environment;
        _subscriptions = subscriptions;
    }

    [HttpGet("overview")]
    [Authorize]
    public async Task<ActionResult<DashboardOverview>> GetOverview()
    {
        if (IsAdministrator)
        {
            return Ok(await _dashboardService.GetOverviewAsync());
        }

        var properties = await GetOwnedPropertiesAsync();
        var propertyIds = properties.Select(x => x.Id).ToHashSet();
        var events = (await _context.SecurityEvents.AsNoTracking().ToListAsync())
            .Where(x => propertyIds.Contains(x.PropertyId))
            .OrderByDescending(x => x.Timestamp)
            .ToList();
        var alerts = (await _context.Alerts.AsNoTracking().ToListAsync())
            .Where(x => propertyIds.Contains(x.PropertyId))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        var openAlerts = alerts.Count(x => x.Status is AlertStatus.Unread or AlertStatus.Read);
        var criticalAlerts = alerts.Count(x => x.Priority == AlertPriority.Critical);
        var overview = new DashboardOverview
        {
            TotalProperties = properties.Count,
            TotalEvents = events.Count,
            OpenAlerts = openAlerts,
            TotalDevices = events.Select(x => x.DeviceId).Distinct().Count(),
            CriticalAlertCount = criticalAlerts,
            RiskScore = Math.Min(100, (openAlerts * 18) + (criticalAlerts * 12) + (events.Count * 2)),
            RecentEvents = events.Take(5).ToList(),
            RecentAlerts = alerts.Take(5).ToList()
        };

        return Ok(overview);
    }

    [HttpGet("properties")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Property>>> GetProperties()
    {
        var query = _context.Properties.AsNoTracking();
        if (!IsAdministrator)
        {
            query = CurrentUserId.HasValue
                ? query.Where(x => x.OwnerUserId == CurrentUserId.Value)
                : query.Where(x => false);
        }

        var properties = await query.OrderBy(x => x.Name).ToListAsync();

        return Ok(properties);
    }

    [HttpPost("properties")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Property>> CreateProperty([FromForm] CreatePropertyRequest request)
    {
        var ownerUserId = IsAdministrator ? request.OwnerUserId : CurrentUserId;
        var owner = IsAdministrator ? request.Owner : CurrentUserName ?? string.Empty;
        if (IsAdministrator && ownerUserId.HasValue)
        {
            var ownerAccount = await _context.UserAccounts.FindAsync(ownerUserId.Value);
            if (ownerAccount is null)
            {
                return BadRequest(new { message = "Select a valid property owner." });
            }

            owner = ownerAccount.FullName;
        }

        if (!IsAdministrator && !ownerUserId.HasValue)
        {
            return Forbid();
        }
        if (!IsAdministrator && ownerUserId is int ownerId && _subscriptions is not null)
        {
            var (subscription, plan) = await _subscriptions.EnsureSubscriptionAsync(ownerId);
            if (!SubscriptionService.HasAccess(subscription, DateTimeOffset.UtcNow))
                return StatusCode(StatusCodes.Status402PaymentRequired, new { message = "Your trial or subscription has ended. Choose a plan to continue." });
            var propertyCount = await _context.Properties.CountAsync(x => x.OwnerUserId == ownerId);
            if (propertyCount >= plan.MaxProperties)
                return StatusCode(StatusCodes.Status402PaymentRequired, new { message = $"Your {plan.Name} plan allows up to {plan.MaxProperties} properties. Upgrade your plan to add more." });
        }
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(owner))
        {
            return BadRequest(new { message = "Property name, address, and owner are required." });
        }

        if (!Enum.TryParse<PropertyStatus>(request.Status, true, out var status))
        {
            status = PropertyStatus.Home;
        }

        var validation = PropertyRegistrationValidator.Validate(request.Name, request.Address, owner, status);
        if (!validation.IsValid)
        {
            return BadRequest(new { message = validation.ErrorMessage });
        }

        var (photoUrl, photoError) = await ProfilePhotoStorage.SaveAsync(
            request.PropertyPhoto,
            _environment ?? throw new InvalidOperationException("Image storage is unavailable."),
            "properties",
            "Property photo");
        if (photoError is not null)
        {
            return BadRequest(new { message = photoError });
        }

        var property = new Property
        {
            Id = $"prop-{Guid.NewGuid():N}",
            Name = validation.Name,
            Address = validation.Address,
            PhotoUrl = photoUrl,
            Owner = validation.Owner,
            OwnerUserId = ownerUserId,
            Status = validation.Status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Properties.Add(property);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            ProfilePhotoStorage.Delete(_environment!, photoUrl);
            throw;
        }

        return CreatedAtAction(nameof(GetProperties), new { id = property.Id }, property);
    }

    [HttpPut("properties/{id}")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Property>> UpdateProperty(string id, [FromForm] CreatePropertyRequest request)
    {
        var property = await _context.Properties.FindAsync(id);
        if (property is null)
        {
            return NotFound();
        }

        if (!IsAdministrator && (!CurrentUserId.HasValue || property.OwnerUserId != CurrentUserId.Value))
        {
            return Forbid();
        }

        var ownerUserId = IsAdministrator ? request.OwnerUserId : property.OwnerUserId;
        var owner = IsAdministrator ? request.Owner : property.Owner;
        if (IsAdministrator && ownerUserId.HasValue)
        {
            var ownerAccount = await _context.UserAccounts.FindAsync(ownerUserId.Value);
            if (ownerAccount is null)
            {
                return BadRequest(new { message = "Select a valid property owner." });
            }

            owner = ownerAccount.FullName;
        }
        if (!Enum.TryParse<PropertyStatus>(request.Status, true, out var status))
        {
            return BadRequest(new { message = "Choose a valid property status." });
        }

        var validation = PropertyRegistrationValidator.Validate(request.Name, request.Address, owner, status);
        if (!validation.IsValid)
        {
            return BadRequest(new { message = validation.ErrorMessage });
        }

        string? newPhotoUrl = null;
        if (request.PropertyPhoto is { Length: > 0 })
        {
            var (photoUrl, photoError) = await ProfilePhotoStorage.SaveAsync(
                request.PropertyPhoto,
                _environment ?? throw new InvalidOperationException("Image storage is unavailable."),
                "properties",
                "Property photo");
            if (photoError is not null)
            {
                return BadRequest(new { message = photoError });
            }

            newPhotoUrl = photoUrl;
        }

        var previousPhotoUrl = property.PhotoUrl;
        property.Name = validation.Name;
        property.Address = validation.Address;
        property.PhotoUrl = newPhotoUrl ?? property.PhotoUrl;
        property.Owner = validation.Owner;
        property.OwnerUserId = ownerUserId;
        property.Status = validation.Status;
        property.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            if (newPhotoUrl is not null)
            {
                ProfilePhotoStorage.Delete(_environment!, newPhotoUrl);
            }

            throw;
        }

        if (newPhotoUrl is not null)
        {
            ProfilePhotoStorage.Delete(_environment!, previousPhotoUrl);
        }

        return Ok(property);
    }

    [HttpGet("properties/{id}/schedule")]
    [Authorize]
    public async Task<ActionResult<SecuritySchedule>> GetSecuritySchedule(string id)
    {
        var property = await GetAccessiblePropertyAsync(id);
        if (property is null) return NotFound();
        return Ok(ReadSchedule(property));
    }

    [HttpPut("properties/{id}/schedule")]
    [Authorize]
    public async Task<ActionResult<SecuritySchedule>> UpdateSecuritySchedule(string id, [FromBody] SecuritySchedule schedule)
    {
        var property = await GetAccessiblePropertyAsync(id);
        if (property is null) return NotFound();
        if (schedule.Periods is null || schedule.Routines is null || schedule.Periods.Count != 4 || schedule.Periods.Any(period => string.IsNullOrWhiteSpace(period.Name)
            || !TimeOnly.TryParse(period.Start, out _) || !TimeOnly.TryParse(period.End, out _)))
        {
            return BadRequest(new { message = "Provide four named activity periods with valid start and end times." });
        }
        if (schedule.Routines.Any(routine => !TimeOnly.TryParse(routine.Time, out _)
            || !Enum.TryParse<SecurityEventType>(routine.EventType, true, out _)
            || string.IsNullOrWhiteSpace(routine.Days)))
        {
            return BadRequest(new { message = "Each routine needs valid days, a time, and an event type." });
        }

        property.SecurityScheduleJson = JsonSerializer.Serialize(schedule);
        property.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(schedule);
    }

    [HttpDelete("properties/{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteProperty(string id)
    {
        var property = await _context.Properties.FindAsync(id);
        if (property is null)
        {
            return NotFound();
        }

        _context.Properties.Remove(property);
        await _context.SaveChangesAsync();
        ProfilePhotoStorage.Delete(_environment!, property.PhotoUrl);
        return NoContent();
    }

    [HttpGet("properties/{id}/trusted-contacts")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<TrustedContact>>> GetTrustedContacts(string id)
    {
        if (!await CanAccessPropertyAsync(id)) return NotFound();
        return Ok(await _context.TrustedContacts.AsNoTracking().Where(contact => contact.PropertyId == id).OrderBy(contact => contact.Name).ToListAsync());
    }

    [HttpPost("properties/{id}/trusted-contacts")]
    [Authorize]
    public async Task<ActionResult<TrustedContact>> AddTrustedContact(string id, [FromBody] TrustedContactRequest request)
    {
        if (!await CanAccessPropertyAsync(id)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Relationship)
            || string.IsNullOrWhiteSpace(request.Days)
            || !TimeOnly.TryParse(request.StartTime, out _) || !TimeOnly.TryParse(request.EndTime, out _)
            || (request.Email is not null && (request.Email.Length > 200 || !new EmailAddressAttribute().IsValid(request.Email))))
            return BadRequest(new { message = "Enter a name, relationship, valid access hours, and a valid email address." });

        var contact = new TrustedContact
        {
            PropertyId = id, Name = request.Name.Trim(), Relationship = request.Relationship.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Days = request.Days.Trim(), StartTime = request.StartTime, EndTime = request.EndTime, IsActive = request.IsActive
        };
        _context.TrustedContacts.Add(contact);
        await _context.SaveChangesAsync();
        return Ok(contact);
    }

    [HttpDelete("properties/{id}/trusted-contacts/{contactId:int}")]
    [Authorize]
    public async Task<IActionResult> RemoveTrustedContact(string id, int contactId)
    {
        if (!await CanAccessPropertyAsync(id)) return NotFound();
        var contact = await _context.TrustedContacts.FirstOrDefaultAsync(item => item.Id == contactId && item.PropertyId == id);
        if (contact is null) return NotFound();
        _context.TrustedContacts.Remove(contact);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("properties/{id}/check-ins")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<SecurityCheckIn>>> GetSecurityCheckIns(string id)
    {
        if (!await CanAccessPropertyAsync(id)) return NotFound();
        return Ok(await _context.SecurityCheckIns.AsNoTracking().Where(checkIn => checkIn.PropertyId == id)
            .OrderByDescending(checkIn => checkIn.CreatedAt).Take(30).ToListAsync());
    }

    [HttpPost("properties/{id}/check-ins")]
    [Authorize]
    public async Task<ActionResult<SecurityCheckIn>> CreateSecurityCheckIn(string id, [FromBody] SecurityCheckInRequest request)
    {
        if (!await CanAccessPropertyAsync(id)) return NotFound();
        if (CurrentUserId is not int userId) return Forbid();
        if (request.Note?.Length > 1000) return BadRequest(new { message = "Check-in note must be 1,000 characters or fewer." });
        var checkIn = new SecurityCheckIn { PropertyId = id, UserId = userId, UserName = CurrentUserName ?? "Authorized user", Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim() };
        _context.SecurityCheckIns.Add(checkIn);
        await _context.SaveChangesAsync();
        return Ok(checkIn);
    }

    [HttpGet("events")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<SecurityEvent>>> GetEvents()
    {
        var events = await _context.SecurityEvents.AsNoTracking().ToListAsync();
        if (!IsAdministrator)
        {
            var propertyIds = (await GetOwnedPropertiesAsync()).Select(x => x.Id).ToHashSet();
            events = events.Where(x => propertyIds.Contains(x.PropertyId)).ToList();
        }

        events = events.OrderByDescending(x => x.Timestamp).Take(50).ToList();

        return Ok(events);
    }

    [HttpGet("devices")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<object>>> GetDevices()
    {
        var events = await _context.SecurityEvents.AsNoTracking().ToListAsync();
        if (!IsAdministrator)
        {
            var propertyIds = (await GetOwnedPropertiesAsync()).Select(x => x.Id).ToHashSet();
            events = events.Where(x => propertyIds.Contains(x.PropertyId)).ToList();
        }

        var devices = events
            .GroupBy(x => x.DeviceId)
            .Select(group =>
            {
                var latestEvent = group.OrderByDescending(x => x.Timestamp).First();
                return new
                {
                    deviceId = group.Key,
                    propertyId = latestEvent.PropertyId,
                    sensorType = latestEvent.SensorType,
                    lastSeen = latestEvent.Timestamp,
                    lastEventType = latestEvent.EventType.ToString(),
                    status = latestEvent.Status
                };
            })
            .OrderBy(x => x.deviceId)
            .ToList();

        return Ok(devices);
    }

    [HttpGet("alerts")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<object>>> GetAlerts()
    {
        var alerts = await _context.Alerts.AsNoTracking().Include(x => x.SecurityEvent).ToListAsync();
        if (!IsAdministrator)
        {
            var propertyIds = (await GetOwnedPropertiesAsync()).Select(x => x.Id).ToHashSet();
            alerts = alerts.Where(x => propertyIds.Contains(x.PropertyId)).ToList();
        }

        alerts = alerts.OrderByDescending(x => x.CreatedAt).ToList();

        var propertyNames = await _context.Properties.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);
        return Ok(alerts.Select(alert => ToAlertResponse(alert, propertyNames.GetValueOrDefault(alert.PropertyId, alert.PropertyId))));
    }

    [HttpGet("alerts/{id:guid}")]
    [Authorize]
    public async Task<ActionResult<object>> GetAlert(Guid id)
    {
        var alert = await _context.Alerts.Include(x => x.SecurityEvent).FirstOrDefaultAsync(x => x.Id == id);
        if (alert is null) return NotFound();
        if (!await CanAccessPropertyAsync(alert.PropertyId)) return Forbid();
        if (alert.Status == AlertStatus.Unread)
        {
            alert.Status = AlertStatus.Read;
            alert.ReadAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();
        }
        var propertyName = await _context.Properties.Where(x => x.Id == alert.PropertyId).Select(x => x.Name).FirstOrDefaultAsync() ?? alert.PropertyId;
        return Ok(ToAlertResponse(alert, propertyName));
    }

    [HttpGet("reports")]
    [Authorize]
    public async Task<IActionResult> DownloadReport()
    {
        if (!IsAdministrator && !await HasAnalyticsAsync())
            return StatusCode(StatusCodes.Status402PaymentRequired, new { message = "Reports require the Standard plan or above." });

        var events = await _context.SecurityEvents
            .AsNoTracking()
            .ToListAsync();
        if (!IsAdministrator)
        {
            var propertyIds = (await GetOwnedPropertiesAsync()).Select(x => x.Id).ToHashSet();
            events = events.Where(x => propertyIds.Contains(x.PropertyId)).ToList();
        }

        var orderedEvents = events
            .OrderByDescending(x => x.Timestamp)
            .ToList();

        var csv = SecurityReportExporter.BuildCsv(orderedEvents);
        var bytes = Encoding.UTF8.GetBytes(csv);

        return File(bytes, "text/csv", "smartguard-security-report.csv");
    }

    [HttpPost("alerts/{id}/acknowledge")]
    [Authorize]
    public async Task<IActionResult> AcknowledgeAlert(Guid id, [FromBody] AlertActionRequest? request)
    {
        var alert = await _context.Alerts.FindAsync(id);
        if (alert is null)
        {
            return NotFound();
        }

        if (!await CanAccessPropertyAsync(alert.PropertyId))
        {
            return Forbid();
        }
        if (!await HasIncidentManagementAsync())
            return StatusCode(StatusCodes.Status402PaymentRequired, new { message = "Incident management requires the Standard plan or above." });
        if (request?.Comment?.Length > 2000) return BadRequest(new { message = "Acknowledgement comments must be 2,000 characters or fewer." });

        alert.Status = AlertStatus.Acknowledged;
        alert.ReadAt = DateTimeOffset.UtcNow;
        alert.AcknowledgedAt = DateTimeOffset.UtcNow;
        alert.AcknowledgedByUserId = CurrentUserId;
        alert.AcknowledgedBy = CurrentUserName;
        alert.AcknowledgementComment = string.IsNullOrWhiteSpace(request?.Comment) ? null : request.Comment.Trim();
        await _context.SaveChangesAsync();

        return Ok(alert);
    }

    [HttpPost("alerts/{id:guid}/resolve")]
    [Authorize]
    public async Task<IActionResult> ResolveAlert(Guid id, [FromBody] AlertActionRequest? request)
    {
        var alert = await _context.Alerts.FindAsync(id);
        if (alert is null)
        {
            return NotFound();
        }

        if (!await CanAccessPropertyAsync(alert.PropertyId)) return Forbid();
        if (!await HasIncidentManagementAsync())
            return StatusCode(StatusCodes.Status402PaymentRequired, new { message = "Incident management requires the Standard plan or above." });
        if (alert.Status != AlertStatus.Acknowledged)
            return Conflict(new { message = "Acknowledge the alert before resolving it." });
        if (string.IsNullOrWhiteSpace(request?.ResolutionNote))
            return BadRequest(new { message = "Add an investigation note before resolving this alert." });
        if (request.ResolutionNote.Length > 2000)
            return BadRequest(new { message = "Resolution notes must be 2,000 characters or fewer." });

        alert.Status = AlertStatus.Resolved;
        alert.ReadAt = DateTimeOffset.UtcNow;
        alert.ResolvedAt = DateTimeOffset.UtcNow;
        alert.ResolvedByUserId = CurrentUserId;
        alert.ResolvedBy = CurrentUserName;
        alert.ResolutionNote = string.IsNullOrWhiteSpace(request?.ResolutionNote) ? null : request.ResolutionNote.Trim();
        await _context.SaveChangesAsync();
        return Ok(alert);
    }

    [HttpPost("events/{id:guid}/review")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> ReviewEvent(Guid id)
    {
        var securityEvent = await _context.SecurityEvents.FindAsync(id);
        if (securityEvent is null)
        {
            return NotFound();
        }

        securityEvent.Status = "Reviewing";
        await _context.SaveChangesAsync();
        return Ok(securityEvent);
    }

    [HttpPost("events")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SecurityEvent>> CreateEvent([FromBody] SecurityEvent securityEvent)
    {
        securityEvent.Id = Guid.NewGuid();
        securityEvent.CreatedAt = DateTimeOffset.UtcNow;
        securityEvent.Timestamp = securityEvent.Timestamp == default ? DateTimeOffset.UtcNow : securityEvent.Timestamp;

        var property = await _context.Properties.FirstOrDefaultAsync(x => x.Id == securityEvent.PropertyId);
        if (property is null) return BadRequest(new { message = "Choose an existing property for this event." });

        var anomalyDetectionEnabled = true;
        if (property.OwnerUserId is int propertyOwnerId && _subscriptions is not null)
        {
            var (subscription, plan) = await _subscriptions.EnsureSubscriptionAsync(propertyOwnerId);
            if (!SubscriptionService.HasAccess(subscription, DateTimeOffset.UtcNow))
                return StatusCode(StatusCodes.Status402PaymentRequired, new { message = "This property's trial or subscription has ended." });
            anomalyDetectionEnabled = plan.AnomalyDetection;
            var propertyIds = await _context.Properties.Where(x => x.OwnerUserId == propertyOwnerId).Select(x => x.Id).ToListAsync();
            var knownDevices = await _context.SecurityEvents.Where(x => propertyIds.Contains(x.PropertyId)).Select(x => x.DeviceId).Distinct().CountAsync();
            var deviceAlreadyRegistered = await _context.SecurityEvents.AnyAsync(x => propertyIds.Contains(x.PropertyId) && x.DeviceId == securityEvent.DeviceId);
            if (!deviceAlreadyRegistered && knownDevices >= plan.MaxDevices)
                return StatusCode(StatusCodes.Status402PaymentRequired, new { message = $"This plan allows up to {plan.MaxDevices} devices. Upgrade your plan to connect more." });
        }

        var recentEvents = await _context.SecurityEvents.AsNoTracking()
            .Where(item => item.PropertyId == securityEvent.PropertyId && item.Timestamp >= securityEvent.Timestamp.AddMinutes(-15))
            .ToListAsync();
        var trustedContacts = await _context.TrustedContacts.AsNoTracking()
            .Where(contact => contact.PropertyId == securityEvent.PropertyId && contact.IsActive)
            .ToListAsync();
        var recentCheckIns = await _context.SecurityCheckIns.AsNoTracking()
            .Where(checkIn => checkIn.PropertyId == securityEvent.PropertyId
                && checkIn.CreatedAt >= securityEvent.Timestamp.AddMinutes(-60)
                && checkIn.CreatedAt <= securityEvent.Timestamp)
            .ToListAsync();

        _context.SecurityEvents.Add(securityEvent);
        var alert = anomalyDetectionEnabled ? _anomalyRuleEngine.Evaluate(property, securityEvent, recentEvents, ReadSchedule(property), trustedContacts, recentCheckIns) : null;
        if (alert is not null) _context.Alerts.Add(alert);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEvents), new { id = securityEvent.Id }, securityEvent);
    }

    private static SecuritySchedule ReadSchedule(Property property)
    {
        if (!string.IsNullOrWhiteSpace(property.SecurityScheduleJson))
        {
            try { return JsonSerializer.Deserialize<SecuritySchedule>(property.SecurityScheduleJson) ?? new SecuritySchedule(); }
            catch (JsonException) { }
        }
        return new SecuritySchedule();
    }

    private async Task<Property?> GetAccessiblePropertyAsync(string id)
    {
        var property = await _context.Properties.FirstOrDefaultAsync(x => x.Id == id);
        return property is not null && await CanAccessPropertyAsync(property.Id) ? property : null;
    }

    private Task<bool> CanAccessPropertyAsync(string propertyId) => IsAdministrator
        ? Task.FromResult(true)
        : CurrentUserId is int userId
            ? _context.Properties.AnyAsync(x => x.Id == propertyId && x.OwnerUserId == userId)
            : Task.FromResult(false);

    private static object ToAlertResponse(Alert alert, string propertyName) => new
    {
        alert.Id,
        alert.SecurityEventId,
        alert.PropertyId,
        PropertyName = propertyName,
        alert.Priority,
        alert.AlertType,
        alert.Message,
        alert.Status,
        alert.CreatedAt,
        alert.ReadAt,
        alert.AcknowledgedByUserId,
        alert.AcknowledgedBy,
        alert.AcknowledgedAt,
        alert.AcknowledgementComment,
        alert.ResolvedByUserId,
        alert.ResolvedBy,
        alert.ResolvedAt,
        alert.ResolutionNote,
        alert.EscalatedAt,
        alert.EscalationDetails,
        SecurityEvent = alert.SecurityEvent
    };

    private bool IsAdministrator => User.IsInRole("Administrator");
    private string? CurrentUserName => User.Identity?.Name;
    private int? CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private Task<List<Property>> GetOwnedPropertiesAsync() => CurrentUserId is int userId
        ? _context.Properties.AsNoTracking().Where(x => x.OwnerUserId == userId).ToListAsync()
        : Task.FromResult(new List<Property>());

    private async Task<bool> HasAnalyticsAsync()
    {
        if (IsAdministrator) return true;
        if (CurrentUserId is not int userId || _subscriptions is null) return false;
        var (subscription, plan) = await _subscriptions.EnsureSubscriptionAsync(userId);
        return SubscriptionService.HasAccess(subscription, DateTimeOffset.UtcNow) && plan.Analytics;
    }

    private async Task<bool> HasIncidentManagementAsync()
    {
        if (IsAdministrator) return true;
        if (CurrentUserId is not int userId || _subscriptions is null) return false;
        var (subscription, plan) = await _subscriptions.EnsureSubscriptionAsync(userId);
        return SubscriptionService.HasAccess(subscription, DateTimeOffset.UtcNow) && plan.IncidentManagement;
    }
}
