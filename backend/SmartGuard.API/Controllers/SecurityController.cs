using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartGuard.Application.Auth;
using SmartGuard.Application.Services;
using SmartGuard.API.Services;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecurityController : ControllerBase
{
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

    public SecurityController(AppDbContext context, SecurityDashboardService dashboardService, IWebHostEnvironment? environment = null)
    {
        _context = context;
        _dashboardService = dashboardService;
        _environment = environment;
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
    public async Task<ActionResult<IEnumerable<Alert>>> GetAlerts()
    {
        var alerts = await _context.Alerts.AsNoTracking().ToListAsync();
        if (!IsAdministrator)
        {
            var propertyIds = (await GetOwnedPropertiesAsync()).Select(x => x.Id).ToHashSet();
            alerts = alerts.Where(x => propertyIds.Contains(x.PropertyId)).ToList();
        }

        alerts = alerts.OrderByDescending(x => x.CreatedAt).ToList();

        return Ok(alerts);
    }

    [HttpGet("reports")]
    [Authorize]
    public async Task<IActionResult> DownloadReport()
    {
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
    public async Task<IActionResult> AcknowledgeAlert(Guid id)
    {
        var alert = await _context.Alerts.FindAsync(id);
        if (alert is null)
        {
            return NotFound();
        }

        if (!IsAdministrator && (!CurrentUserId.HasValue || !await _context.Properties.AnyAsync(x => x.Id == alert.PropertyId && x.OwnerUserId == CurrentUserId.Value)))
        {
            return Forbid();
        }

        alert.Status = AlertStatus.Acknowledged;
        alert.ReadAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(alert);
    }

    [HttpPost("alerts/{id:guid}/resolve")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> ResolveAlert(Guid id)
    {
        var alert = await _context.Alerts.FindAsync(id);
        if (alert is null)
        {
            return NotFound();
        }

        alert.Status = AlertStatus.Resolved;
        alert.ReadAt = DateTimeOffset.UtcNow;
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

        _context.SecurityEvents.Add(securityEvent);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEvents), new { id = securityEvent.Id }, securityEvent);
    }

    private bool IsAdministrator => User.IsInRole("Administrator");
    private string? CurrentUserName => User.Identity?.Name;
    private int? CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private Task<List<Property>> GetOwnedPropertiesAsync() => CurrentUserId is int userId
        ? _context.Properties.AsNoTracking().Where(x => x.OwnerUserId == userId).ToListAsync()
        : Task.FromResult(new List<Property>());
}
