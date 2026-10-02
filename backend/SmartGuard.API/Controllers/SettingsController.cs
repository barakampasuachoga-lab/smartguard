using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Roles = "Administrator")]
public class SettingsController : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        ["anomalyDetectionEnabled"] = "true",
        ["alertNotificationsEnabled"] = "true",
        ["maintenanceMode"] = "false",
        ["riskThreshold"] = "70"
    };

    private readonly AppDbContext _context;

    public SettingsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SystemSetting>>> GetSettings()
    {
        foreach (var setting in Defaults)
        {
            if (!await _context.SystemSettings.AnyAsync(x => x.Name == setting.Key))
            {
                _context.SystemSettings.Add(new SystemSetting { Name = setting.Key, Value = setting.Value });
            }
        }

        await _context.SaveChangesAsync();
        return Ok(await _context.SystemSettings.OrderBy(x => x.Name).ToListAsync());
    }

    [HttpPut("{name}")]
    public async Task<ActionResult<SystemSetting>> UpdateSetting(string name, [FromBody] UpdateSettingRequest request)
    {
        if (!Defaults.ContainsKey(name) || !IsValidValue(name, request.Value))
        {
            return BadRequest(new { message = "This setting or value is not supported." });
        }

        var setting = await _context.SystemSettings.FindAsync(name);
        if (setting is null)
        {
            setting = new SystemSetting { Name = name, Value = request.Value };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = request.Value;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(setting);
    }

    private static bool IsValidValue(string name, string value) => name == "riskThreshold"
        ? int.TryParse(value, out var threshold) && threshold is >= 0 and <= 100
        : bool.TryParse(value, out _);
}

public class UpdateSettingRequest
{
    public string Value { get; set; } = string.Empty;
}