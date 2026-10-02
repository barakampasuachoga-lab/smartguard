using System.Text;
using SmartGuard.Domain.Models;

namespace SmartGuard.Application.Services;

public static class SecurityReportExporter
{
    public static string BuildCsv(IEnumerable<SecurityEvent> events)
    {
        var rows = new List<string>();
        rows.Add("Id,PropertyId,DeviceId,SensorType,EventType,Location,Description,Priority,Status,Timestamp,CreatedAt");

        foreach (var securityEvent in events)
        {
            rows.Add(string.Join(",",
                Escape(securityEvent.Id.ToString()),
                Escape(securityEvent.PropertyId),
                Escape(securityEvent.DeviceId),
                Escape(securityEvent.SensorType),
                Escape(securityEvent.EventType.ToString()),
                Escape(securityEvent.Location),
                Escape(securityEvent.Description),
                Escape(securityEvent.Priority.ToString()),
                Escape(securityEvent.Status),
                Escape(securityEvent.Timestamp.ToString("O")),
                Escape(securityEvent.CreatedAt.ToString("O"))
            ));
        }

        return string.Join(Environment.NewLine, rows);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        if (escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r'))
        {
            return $"\"{escaped}\"";
        }

        return escaped;
    }
}
