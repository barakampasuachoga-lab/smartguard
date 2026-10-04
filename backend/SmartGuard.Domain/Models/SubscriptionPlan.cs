namespace SmartGuard.Domain.Models;

public class SubscriptionPlan
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PriceKes { get; set; }
    public int MaxProperties { get; set; }
    public int MaxDevices { get; set; }
    public bool AnomalyDetection { get; set; }
    public bool Analytics { get; set; }
    public bool IncidentManagement { get; set; }
    public bool SecurityIntelligence { get; set; }
    public bool MultipleStaffAccounts { get; set; }
    public bool AdvancedReports { get; set; }
    public bool PrioritySupport { get; set; }
}
