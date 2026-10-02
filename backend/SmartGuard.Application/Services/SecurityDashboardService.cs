using SmartGuard.Domain.Models;

namespace SmartGuard.Application.Services;

public class SecurityDashboardService
{
    private readonly ISecurityDashboardDataSource _dataSource;

    public SecurityDashboardService(ISecurityDashboardDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<DashboardOverview> GetOverviewAsync()
    {
        var totalProperties = await _dataSource.GetTotalPropertiesAsync();
        var totalEvents = await _dataSource.GetTotalEventsAsync();
        var openAlerts = await _dataSource.GetOpenAlertsAsync();
        var totalDevices = await _dataSource.GetTotalDevicesAsync();
        var criticalAlerts = await _dataSource.GetCriticalAlertCountAsync();
        var recentEvents = await _dataSource.GetRecentEventsAsync(5);
        var recentAlerts = await _dataSource.GetRecentAlertsAsync(5);

        var riskScore = Math.Min(100, (openAlerts * 18) + (criticalAlerts * 12) + (totalEvents * 2));

        return new DashboardOverview
        {
            TotalProperties = totalProperties,
            TotalEvents = totalEvents,
            OpenAlerts = openAlerts,
            TotalDevices = totalDevices,
            RiskScore = riskScore,
            RecentEvents = recentEvents,
            RecentAlerts = recentAlerts,
            CriticalAlertCount = criticalAlerts
        };
    }
}

public class DashboardOverview
{
    public int TotalProperties { get; set; }
    public int TotalEvents { get; set; }
    public int OpenAlerts { get; set; }
    public int TotalDevices { get; set; }
    public int RiskScore { get; set; }
    public int CriticalAlertCount { get; set; }
    public List<SecurityEvent> RecentEvents { get; set; } = new();
    public List<Alert> RecentAlerts { get; set; } = new();
}
