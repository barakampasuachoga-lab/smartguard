using Microsoft.EntityFrameworkCore;
using SmartGuard.Domain.Models;

namespace SmartGuard.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Property> Properties { get; set; }
    public DbSet<SecurityEvent> SecurityEvents { get; set; }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<UserReport> UserReports { get; set; }
    public DbSet<TrustedContact> TrustedContacts { get; set; }
    public DbSet<SecurityCheckIn> SecurityCheckIns { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<UserSubscription> Subscriptions { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Invoice> Invoices { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Property>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Address).IsRequired().HasMaxLength(500);
            entity.Property(x => x.PhotoUrl).HasMaxLength(500);
            entity.Property(x => x.Owner).IsRequired().HasMaxLength(200);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SecurityEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PropertyId).IsRequired();
            entity.Property(x => x.DeviceId).IsRequired();
            entity.Property(x => x.Location).IsRequired();
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PropertyId).IsRequired();
            entity.Property(x => x.Message).IsRequired();
            entity.HasOne(x => x.SecurityEvent)
                .WithMany()
                .HasForeignKey(x => x.SecurityEventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrustedContact>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(160);
            entity.Property(x => x.Relationship).IsRequired().HasMaxLength(80);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SecurityCheckIn>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(x => x.TokenHash);
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.UserAccountId, x.ExpiresAt });
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(x => x.Code);
            entity.Property(x => x.Code).HasMaxLength(24);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(48);
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => x.PlanCode).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(24);
            entity.HasIndex(x => new { x.UserId, x.Status });
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => x.PlanCode).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(16);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(24);
            entity.Property(x => x.CheckoutRequestId).HasMaxLength(128);
            entity.Property(x => x.MpesaReceiptNumber).HasMaxLength(40);
            entity.HasIndex(x => x.CheckoutRequestId).IsUnique();
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.MpesaReceiptNumber).IsRequired().HasMaxLength(40);
            entity.HasIndex(x => x.PaymentTransactionId).IsUnique();
            entity.HasIndex(x => x.MpesaReceiptNumber).IsUnique();
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(40);
            entity.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            entity.HasIndex(x => x.InvoiceNumber).IsUnique();
            entity.HasIndex(x => x.PaymentId).IsUnique();
        });

        modelBuilder.Entity<Property>()
            .HasMany(x => x.SecurityEvents)
            .WithOne()
            .HasForeignKey(x => x.PropertyId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Property>()
            .HasMany(x => x.Alerts)
            .WithOne()
            .HasForeignKey(x => x.PropertyId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Property>().HasData(
            new Property
            {
                Id = "prop-001",
                Name = "Oakridge Residence",
                Address = "145 Cedar Lane, Austin, TX",
                Owner = "Emma Johnson",
                Status = PropertyStatus.Away,
                CreatedAt = new DateTimeOffset(2024, 1, 12, 8, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2024, 9, 20, 7, 15, 0, TimeSpan.Zero)
            },
            new Property
            {
                Id = "prop-002",
                Name = "Harbor View Condo",
                Address = "84 Bayfront Ave, Seattle, WA",
                Owner = "Michael Chen",
                Status = PropertyStatus.Home,
                CreatedAt = new DateTimeOffset(2024, 2, 25, 9, 30, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2024, 9, 22, 11, 0, 0, TimeSpan.Zero)
            }
        );

        modelBuilder.Entity<SecurityEvent>().HasData(
            new SecurityEvent
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                PropertyId = "prop-001",
                DeviceId = "cam-101",
                SensorType = "Camera",
                EventType = SecurityEventType.DoorOpened,
                Location = "Front Gate",
                Description = "Door opened while property was marked away.",
                Priority = AlertPriority.High,
                Status = "Triggered",
                Timestamp = new DateTimeOffset(2026, 9, 26, 4, 12, 0, TimeSpan.Zero),
                CreatedAt = new DateTimeOffset(2026, 9, 26, 4, 12, 5, TimeSpan.Zero)
            },
            new SecurityEvent
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                PropertyId = "prop-001",
                DeviceId = "cam-102",
                SensorType = "Motion",
                EventType = SecurityEventType.MotionDetected,
                Location = "Living Room",
                Description = "Repeated motion at 02:00 AM.",
                Priority = AlertPriority.Medium,
                Status = "Reviewing",
                Timestamp = new DateTimeOffset(2026, 9, 26, 2, 0, 0, TimeSpan.Zero),
                CreatedAt = new DateTimeOffset(2026, 9, 26, 2, 0, 5, TimeSpan.Zero)
            },
            new SecurityEvent
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                PropertyId = "prop-002",
                DeviceId = "cam-201",
                SensorType = "Window",
                EventType = SecurityEventType.WindowOpened,
                Location = "Bedroom Window",
                Description = "Window opened during normal occupancy.",
                Priority = AlertPriority.Low,
                Status = "Resolved",
                Timestamp = new DateTimeOffset(2026, 9, 25, 21, 35, 0, TimeSpan.Zero),
                CreatedAt = new DateTimeOffset(2026, 9, 25, 21, 35, 10, TimeSpan.Zero)
            }
        );

        modelBuilder.Entity<Alert>().HasData(
            new Alert
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                SecurityEventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                PropertyId = "prop-001",
                Priority = AlertPriority.High,
                Message = "Door opened while property was marked away.",
                Status = AlertStatus.Unread,
                CreatedAt = new DateTimeOffset(2026, 9, 26, 4, 12, 10, TimeSpan.Zero)
            },
            new Alert
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                SecurityEventId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                PropertyId = "prop-001",
                Priority = AlertPriority.Medium,
                Message = "Repeated motion activity detected over the last 15 minutes.",
                Status = AlertStatus.Acknowledged,
                CreatedAt = new DateTimeOffset(2026, 9, 26, 2, 5, 10, TimeSpan.Zero)
            }
        );

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(200);
            entity.Property(x => x.ProfilePhotoUrl).HasMaxLength(500);
            entity.Property(x => x.Role).IsRequired().HasMaxLength(50);
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(x => x.Name);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Value).IsRequired().HasMaxLength(500);
        });

        modelBuilder.Entity<UserReport>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(160);
            entity.Property(x => x.Body).IsRequired().HasMaxLength(5000);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.RecipientUserId, x.CreatedAt });
        });

    }
}
