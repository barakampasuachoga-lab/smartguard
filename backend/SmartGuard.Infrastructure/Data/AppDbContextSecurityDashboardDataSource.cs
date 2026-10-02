using Microsoft.EntityFrameworkCore;
using SmartGuard.Application.Services;
using SmartGuard.Domain.Models;

namespace SmartGuard.Infrastructure.Data;

public class AppDbContextSecurityDashboardDataSource : ISecurityDashboardDataSource
{
    private readonly AppDbContext _context;

    public AppDbContextSecurityDashboardDataSource(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetTotalPropertiesAsync() => await _context.Properties.CountAsync();

    public async Task<int> GetTotalEventsAsync() => await _context.SecurityEvents.CountAsync();

    public async Task<int> GetOpenAlertsAsync() => await _context.Alerts.CountAsync(x => x.Status != AlertStatus.Resolved);

    public async Task<int> GetTotalDevicesAsync() => await _context.SecurityEvents.Select(x => x.DeviceId).Distinct().CountAsync();

    public async Task<int> GetCriticalAlertCountAsync() => await _context.Alerts.CountAsync(x => x.Priority == AlertPriority.Critical || x.Priority == AlertPriority.High);

    public async Task<List<SecurityEvent>> GetRecentEventsAsync(int count)
    {
        return await Task.FromResult(
            _context.SecurityEvents
                .AsEnumerable()
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToList());
    }

    public async Task<List<Alert>> GetRecentAlertsAsync(int count)
    {
        return await Task.FromResult(
            _context.Alerts
                .AsEnumerable()
                .OrderByDescending(x => x.CreatedAt)
                .Take(count)
                .ToList());
    }
}
