using SmartGuard.Domain.Models;

namespace SmartGuard.Application.Services;

public interface ISecurityDashboardDataSource
{
    Task<int> GetTotalPropertiesAsync();
    Task<int> GetTotalEventsAsync();
    Task<int> GetOpenAlertsAsync();
    Task<int> GetTotalDevicesAsync();
    Task<int> GetCriticalAlertCountAsync();
    Task<List<SecurityEvent>> GetRecentEventsAsync(int count);
    Task<List<Alert>> GetRecentAlertsAsync(int count);
}
