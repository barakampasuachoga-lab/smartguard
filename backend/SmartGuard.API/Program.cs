using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using SmartGuard.API.Services;
using SmartGuard.Application.Auth;
using SmartGuard.Application.Services;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartGuard.API",
        Version = "v1",
        Description = "Security monitoring and alert dashboard API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? "SmartGuard-Local-Development-Key-123456789";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SmartGuard";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SmartGuardUsers";

var postgresConnection = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrWhiteSpace(postgresConnection))
{
    postgresConnection = NormalizePostgresConnectionString(postgresConnection);
}
var usePostgres = string.Equals(builder.Configuration["Database:Provider"], "Postgres", StringComparison.OrdinalIgnoreCase)
    || !string.IsNullOrWhiteSpace(postgresConnection);
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (usePostgres)
    {
        options.UseNpgsql(postgresConnection ?? builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Set ConnectionStrings:Postgres when Database:Provider is Postgres."));
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=smartguard.db");
    }
});

builder.Services.AddScoped<ISecurityDashboardDataSource, AppDbContextSecurityDashboardDataSource>();
builder.Services.AddScoped<SecurityDashboardService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddHttpClient(nameof(DarajaStkPushClient), client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<DarajaStkPushClient>();
builder.Services.AddHostedService<SubscriptionExpiryService>();
builder.Services.AddHostedService<SmartGuard.API.Services.AlertEscalationService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5174", "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();
Directory.CreateDirectory(app.Environment.WebRootPath);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (db.Database.IsSqlite())
    {
    EnsureSqliteColumn(db, "Properties", "SecurityScheduleJson", "TEXT NOT NULL DEFAULT ''");
    EnsureSqliteColumn(db, "Alerts", "AlertType", "TEXT NOT NULL DEFAULT 'Security anomaly'");
    EnsureSqliteColumn(db, "Alerts", "AcknowledgedByUserId", "INTEGER NULL");
    EnsureSqliteColumn(db, "Alerts", "AcknowledgedBy", "TEXT NULL");
    EnsureSqliteColumn(db, "Alerts", "AcknowledgedAt", "TEXT NULL");
    EnsureSqliteColumn(db, "Alerts", "AcknowledgementComment", "TEXT NULL");
    EnsureSqliteColumn(db, "Alerts", "ResolvedByUserId", "INTEGER NULL");
    EnsureSqliteColumn(db, "Alerts", "ResolvedBy", "TEXT NULL");
    EnsureSqliteColumn(db, "Alerts", "ResolvedAt", "TEXT NULL");
    EnsureSqliteColumn(db, "Alerts", "ResolutionNote", "TEXT NULL");
    EnsureSqliteColumn(db, "Alerts", "EscalatedAt", "TEXT NULL");
    EnsureSqliteColumn(db, "Alerts", "EscalationDetails", "TEXT NULL");
    db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS TrustedContacts (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, PropertyId TEXT NOT NULL, Name TEXT NOT NULL, Relationship TEXT NOT NULL, Email TEXT NULL, Days TEXT NOT NULL, StartTime TEXT NOT NULL, EndTime TEXT NOT NULL, IsActive INTEGER NOT NULL, CreatedAt TEXT NOT NULL, CONSTRAINT FK_TrustedContacts_Properties_PropertyId FOREIGN KEY (PropertyId) REFERENCES Properties (Id) ON DELETE CASCADE)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_TrustedContacts_PropertyId ON TrustedContacts (PropertyId)");
    db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS SecurityCheckIns (Id TEXT NOT NULL PRIMARY KEY, PropertyId TEXT NOT NULL, UserId INTEGER NOT NULL, UserName TEXT NOT NULL, Note TEXT NULL, CreatedAt TEXT NOT NULL, CONSTRAINT FK_SecurityCheckIns_Properties_PropertyId FOREIGN KEY (PropertyId) REFERENCES Properties (Id) ON DELETE CASCADE, CONSTRAINT FK_SecurityCheckIns_UserAccounts_UserId FOREIGN KEY (UserId) REFERENCES UserAccounts (Id) ON DELETE CASCADE)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_SecurityCheckIns_PropertyId_CreatedAt ON SecurityCheckIns (PropertyId, CreatedAt)");
    if (db.Database.IsSqlite())
    {
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS PasswordResetTokens (TokenHash TEXT NOT NULL PRIMARY KEY, UserAccountId INTEGER NOT NULL, CreatedAt TEXT NOT NULL, ExpiresAt TEXT NOT NULL, UsedAt TEXT NULL, CONSTRAINT FK_PasswordResetTokens_UserAccounts_UserAccountId FOREIGN KEY (UserAccountId) REFERENCES UserAccounts (Id) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_PasswordResetTokens_UserAccountId_ExpiresAt ON PasswordResetTokens (UserAccountId, ExpiresAt)");
    }
    db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS SystemSettings (Name TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL, UpdatedAt TEXT NOT NULL)");
    db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS UserReports (Id INTEGER NOT NULL CONSTRAINT PK_UserReports PRIMARY KEY AUTOINCREMENT, RecipientUserId INTEGER NOT NULL, SenderUserId INTEGER NOT NULL, Title TEXT NOT NULL, Body TEXT NOT NULL, CreatedAt TEXT NOT NULL, IsRead INTEGER NOT NULL, ReadAt TEXT NULL, CONSTRAINT FK_UserReports_Recipient FOREIGN KEY (RecipientUserId) REFERENCES UserAccounts (Id) ON DELETE CASCADE, CONSTRAINT FK_UserReports_Sender FOREIGN KEY (SenderUserId) REFERENCES UserAccounts (Id) ON DELETE RESTRICT)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_UserReports_RecipientUserId_CreatedAt ON UserReports (RecipientUserId, CreatedAt)");
    db.Database.OpenConnection();
    var hasOwnerUserId = false;
    using (var command = db.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "PRAGMA table_info('Properties')";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == "OwnerUserId")
            {
                hasOwnerUserId = true;
                break;
            }
        }
    }
    db.Database.CloseConnection();
    if (!hasOwnerUserId)
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Properties ADD COLUMN OwnerUserId INTEGER NULL");
    }
    db.Database.OpenConnection();
    var hasPropertyPhotoUrl = false;
    using (var command = db.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "PRAGMA table_info('Properties')";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == "PhotoUrl")
            {
                hasPropertyPhotoUrl = true;
                break;
            }
        }
    }
    db.Database.CloseConnection();
    if (!hasPropertyPhotoUrl)
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Properties ADD COLUMN PhotoUrl TEXT NULL");
    }
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Properties_OwnerUserId ON Properties (OwnerUserId)");
    db.Database.OpenConnection();
    var hasProfilePhotoUrl = false;
    using (var command = db.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "PRAGMA table_info('UserAccounts')";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == "ProfilePhotoUrl")
            {
                hasProfilePhotoUrl = true;
                break;
            }
        }
    }
    db.Database.CloseConnection();
    if (!hasProfilePhotoUrl)
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE UserAccounts ADD COLUMN ProfilePhotoUrl TEXT NULL");
    }
    }
    EnsureBillingSchema(db);

    var adminUser = db.UserAccounts.FirstOrDefault(x => x.Email == "admin@smartguard.com" || x.Email == "barakampasuachoga@gmail.com" || x.Role == "Administrator");

    if (adminUser is null)
    {
        db.UserAccounts.Add(new UserAccount
        {
            FullName = "Daniel Brooks",
            Email = "admin@smartguard.com",
            Role = "Administrator",
            PasswordHash = PasswordHasher.Hash("admin123"),
            IsBlocked = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
    else
    {
        adminUser.FullName = "Daniel Brooks";
        adminUser.Email = "admin@smartguard.com";
        adminUser.Role = "Administrator";
        adminUser.PasswordHash = PasswordHasher.Hash("admin123");
        adminUser.IsBlocked = false;
        adminUser.CreatedAt = adminUser.CreatedAt == default ? DateTimeOffset.UtcNow : adminUser.CreatedAt;
    }

    if (!db.UserAccounts.Any(x => x.Email == "user@smartguard.com"))
    {
        db.UserAccounts.Add(new UserAccount
        {
            FullName = "Aisha Morgan",
            Email = "user@smartguard.com",
            Role = "Resident User",
            PasswordHash = PasswordHasher.Hash("smartguard123"),
            IsBlocked = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    if (!db.UserAccounts.Any(x => x.Email == "marcus@smartguard.com"))
    {
        db.UserAccounts.Add(new UserAccount
        {
            FullName = "Marcus Reid",
            Email = "marcus@smartguard.com",
            Role = "Resident User",
            PasswordHash = PasswordHasher.Hash("password123"),
            IsBlocked = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    db.SaveChanges();

    foreach (var plan in SubscriptionService.Plans)
    {
        if (!db.SubscriptionPlans.Any(x => x.Code == plan.Code))
        {
            db.SubscriptionPlans.Add(plan);
        }
    }
    db.SaveChanges();

    if (app.Environment.IsDevelopment())
    {
        var demoPropertySeeds = new[]
        {
            (Email: "user@smartguard.com", Id: "prop-001", LegacyOwner: "Emma Johnson", Name: "Oakridge Residence", Address: "145 Cedar Lane, Austin, TX", Status: PropertyStatus.Away),
            (Email: "user@smartguard.com", Id: "demo-aisha-02", LegacyOwner: "", Name: "Willow Creek Home", Address: "18 Willow Creek Road, Austin, TX", Status: PropertyStatus.Home),
            (Email: "user@smartguard.com", Id: "demo-aisha-03", LegacyOwner: "", Name: "Cedar Grove Villa", Address: "42 Cedar Grove, Austin, TX", Status: PropertyStatus.Away),
            (Email: "user@smartguard.com", Id: "demo-aisha-04", LegacyOwner: "", Name: "Riverside Townhouse", Address: "7 Riverside Walk, Austin, TX", Status: PropertyStatus.Vacation),
            (Email: "user@smartguard.com", Id: "demo-aisha-05", LegacyOwner: "", Name: "Maple Leaf Residence", Address: "91 Maple Avenue, Austin, TX", Status: PropertyStatus.Home),
            (Email: "user@smartguard.com", Id: "demo-aisha-06", LegacyOwner: "", Name: "Hilltop House", Address: "6 Hilltop Drive, Austin, TX", Status: PropertyStatus.Maintenance),
            (Email: "user@smartguard.com", Id: "demo-aisha-07", LegacyOwner: "", Name: "Garden Court Apartment", Address: "220 Garden Court, Austin, TX", Status: PropertyStatus.Away),
            (Email: "marcus@smartguard.com", Id: "prop-002", LegacyOwner: "Michael Chen", Name: "Harbor View Condo", Address: "84 Bayfront Ave, Seattle, WA", Status: PropertyStatus.Home),
            (Email: "marcus@smartguard.com", Id: "demo-marcus-02", LegacyOwner: "", Name: "Pinecrest Home", Address: "12 Pinecrest Lane, Seattle, WA", Status: PropertyStatus.Home),
            (Email: "marcus@smartguard.com", Id: "demo-marcus-03", LegacyOwner: "", Name: "Westlake Residence", Address: "340 Westlake Avenue, Seattle, WA", Status: PropertyStatus.Away),
            (Email: "marcus@smartguard.com", Id: "demo-marcus-04", LegacyOwner: "", Name: "Ashford Townhouse", Address: "55 Ashford Street, Seattle, WA", Status: PropertyStatus.Vacation),
            (Email: "marcus@smartguard.com", Id: "demo-marcus-05", LegacyOwner: "", Name: "Meadowview Villa", Address: "8 Meadowview Court, Seattle, WA", Status: PropertyStatus.Home),
            (Email: "marcus@smartguard.com", Id: "demo-marcus-06", LegacyOwner: "", Name: "Summit Court Home", Address: "102 Summit Court, Seattle, WA", Status: PropertyStatus.Maintenance),
            (Email: "marcus@smartguard.com", Id: "demo-marcus-07", LegacyOwner: "", Name: "Brookfield Apartment", Address: "19 Brookfield Way, Seattle, WA", Status: PropertyStatus.Away)
        };

        var eventTypes = new[]
        {
            SecurityEventType.MotionDetected,
            SecurityEventType.DoorOpened,
            SecurityEventType.WindowOpened,
            SecurityEventType.DeviceOffline,
            SecurityEventType.PotentiallyUnusualActivity,
            SecurityEventType.DoorClosed,
            SecurityEventType.WindowClosed
        };
        var sensorTypes = new[] { "Motion", "Door", "Window", "Hub", "Activity", "Door", "Window" };
        var locations = new[] { "Front entry", "Garage access", "East window", "Network panel", "Perimeter", "Side entrance", "Bedroom window" };
        var priorities = new[] { AlertPriority.Medium, AlertPriority.High, AlertPriority.Critical, AlertPriority.High, AlertPriority.Medium, AlertPriority.Low, AlertPriority.High };
        var statuses = new[] { "Reviewing", "Triggered", "New", "Investigating", "Reviewing", "Resolved", "Triggered" };
        var descriptions = new[]
        {
            "Motion activity detected near the property entry.",
            "Access event recorded outside the usual schedule.",
            "Window sensor reported an unexpected state change.",
            "Device heartbeat was interrupted and requires a check.",
            "Activity pattern flagged for owner review.",
            "Door state changed and was recorded by the access sensor.",
            "Window contact sensor reported a recent event."
        };

        for (var propertyIndex = 0; propertyIndex < demoPropertySeeds.Length; propertyIndex++)
        {
            var seed = demoPropertySeeds[propertyIndex];
            var owner = db.UserAccounts.FirstOrDefault(x => x.Email == seed.Email);
            if (owner is null)
            {
                continue;
            }

            var property = db.Properties.FirstOrDefault(x => x.Id == seed.Id);
            if (property is null)
            {
                property = new Property
                {
                    Id = seed.Id,
                    Name = seed.Name,
                    Address = seed.Address,
                    Owner = owner.FullName,
                    OwnerUserId = owner.Id,
                    Status = seed.Status,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.Properties.Add(property);
            }
            else if (property.OwnerUserId is null && property.Owner == seed.LegacyOwner)
            {
                property.OwnerUserId = owner.Id;
                property.Owner = owner.FullName;
                property.UpdatedAt = DateTimeOffset.UtcNow;
            }

            if (property.OwnerUserId != owner.Id)
            {
                continue;
            }

            var existingEvents = db.SecurityEvents.Count(x => x.PropertyId == property.Id);
            for (var eventIndex = existingEvents; eventIndex < 3; eventIndex++)
            {
                var templateIndex = (propertyIndex + eventIndex) % eventTypes.Length;
                var timestamp = DateTimeOffset.UtcNow.AddHours(-((propertyIndex * 11) + (eventIndex * 7) + 1));
                db.SecurityEvents.Add(new SecurityEvent
                {
                    Id = Guid.NewGuid(),
                    PropertyId = property.Id,
                    DeviceId = $"device-{propertyIndex + 1:00}-{templateIndex + 1:00}",
                    SensorType = sensorTypes[templateIndex],
                    EventType = eventTypes[templateIndex],
                    Location = locations[templateIndex],
                    Description = descriptions[templateIndex],
                    Priority = priorities[templateIndex],
                    Status = statuses[templateIndex],
                    Timestamp = timestamp,
                    CreatedAt = timestamp.AddSeconds(5)
                });
            }
        }
    }

    db.SaveChanges();
}

if (app.Environment.IsDevelopment())
{
    // Emit the Swagger 2.0 format for compatibility with consumers that do not
    // accept the generated OpenAPI 3.x document.
    app.UseSwagger(options => options.SerializeAsV2 = true);
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartGuard.API v1");
    });
}

app.UseCors("FrontendPolicy");
app.UseDefaultFiles();
app.UseStaticFiles();
// app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

static string NormalizePostgresConnectionString(string connectionString)
{
    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var databaseUri)
        || databaseUri.Scheme is not ("postgres" or "postgresql"))
    {
        return connectionString;
    }

    var credentials = databaseUri.UserInfo.Split(':', 2);
    if (credentials.Length != 2)
    {
        throw new InvalidOperationException("The PostgreSQL URL must contain both a username and password.");
    }

    var npgsql = new NpgsqlConnectionStringBuilder
    {
        Host = databaseUri.Host,
        Port = databaseUri.Port > 0 ? databaseUri.Port : 5432,
        Database = Uri.UnescapeDataString(databaseUri.AbsolutePath.TrimStart('/')),
        Username = Uri.UnescapeDataString(credentials[0]),
        Password = Uri.UnescapeDataString(credentials[1]),
        SslMode = SslMode.Require
    };
    return npgsql.ConnectionString;
}

static void EnsureSqliteColumn(AppDbContext db, string table, string column, string definition)
{
    db.Database.OpenConnection();
    var hasColumn = false;
    try
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info('{table}')";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == column) { hasColumn = true; break; }
        }
    }
    finally { db.Database.CloseConnection(); }

    if (!hasColumn)
    {
        db.Database.OpenConnection();
        try
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
            command.ExecuteNonQuery();
        }
        finally { db.Database.CloseConnection(); }
    }
}

static void EnsureBillingSchema(AppDbContext db)
{
    if (db.Database.IsSqlite())
    {
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS PasswordResetTokens (TokenHash TEXT NOT NULL PRIMARY KEY, UserAccountId INTEGER NOT NULL, CreatedAt TEXT NOT NULL, ExpiresAt TEXT NOT NULL, UsedAt TEXT NULL, CONSTRAINT FK_PasswordResetTokens_UserAccounts_UserAccountId FOREIGN KEY (UserAccountId) REFERENCES UserAccounts (Id) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_PasswordResetTokens_UserAccountId_ExpiresAt ON PasswordResetTokens (UserAccountId, ExpiresAt)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS SubscriptionPlans (Code TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, PriceKes INTEGER NOT NULL, MaxProperties INTEGER NOT NULL, MaxDevices INTEGER NOT NULL, AnomalyDetection INTEGER NOT NULL, Analytics INTEGER NOT NULL, IncidentManagement INTEGER NOT NULL, SecurityIntelligence INTEGER NOT NULL, MultipleStaffAccounts INTEGER NOT NULL, AdvancedReports INTEGER NOT NULL, PrioritySupport INTEGER NOT NULL)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS Subscriptions (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL, PlanCode TEXT NOT NULL, Status TEXT NOT NULL, TrialStart TEXT NULL, TrialEnd TEXT NULL, CurrentPeriodStart TEXT NULL, CurrentPeriodEnd TEXT NULL, CancelAtPeriodEnd INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CONSTRAINT FK_Subscriptions_UserAccounts_UserId FOREIGN KEY (UserId) REFERENCES UserAccounts (Id) ON DELETE CASCADE, CONSTRAINT FK_Subscriptions_SubscriptionPlans_PlanCode FOREIGN KEY (PlanCode) REFERENCES SubscriptionPlans (Code) ON DELETE RESTRICT)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Subscriptions_UserId_Status ON Subscriptions (UserId, Status)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS PaymentTransactions (Id TEXT NOT NULL PRIMARY KEY, UserId INTEGER NOT NULL, SubscriptionId INTEGER NOT NULL, PlanCode TEXT NOT NULL, AmountKes INTEGER NOT NULL, PhoneNumber TEXT NOT NULL, Status TEXT NOT NULL, MerchantRequestId TEXT NULL, CheckoutRequestId TEXT NULL, ResponseDescription TEXT NULL, ResultCode INTEGER NULL, MpesaReceiptNumber TEXT NULL, InitiatedAt TEXT NOT NULL, CompletedAt TEXT NULL, CONSTRAINT FK_PaymentTransactions_UserAccounts_UserId FOREIGN KEY (UserId) REFERENCES UserAccounts (Id) ON DELETE CASCADE, CONSTRAINT FK_PaymentTransactions_Subscriptions_SubscriptionId FOREIGN KEY (SubscriptionId) REFERENCES Subscriptions (Id) ON DELETE RESTRICT, CONSTRAINT FK_PaymentTransactions_SubscriptionPlans_PlanCode FOREIGN KEY (PlanCode) REFERENCES SubscriptionPlans (Code) ON DELETE RESTRICT)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_PaymentTransactions_CheckoutRequestId ON PaymentTransactions (CheckoutRequestId)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_PaymentTransactions_MpesaReceiptNumber ON PaymentTransactions (MpesaReceiptNumber)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS Payments (Id TEXT NOT NULL PRIMARY KEY, PaymentTransactionId TEXT NOT NULL, UserId INTEGER NOT NULL, SubscriptionId INTEGER NOT NULL, AmountKes INTEGER NOT NULL, MpesaReceiptNumber TEXT NOT NULL, PaidAt TEXT NOT NULL, CONSTRAINT FK_Payments_PaymentTransactions_PaymentTransactionId FOREIGN KEY (PaymentTransactionId) REFERENCES PaymentTransactions (Id) ON DELETE RESTRICT, CONSTRAINT FK_Payments_UserAccounts_UserId FOREIGN KEY (UserId) REFERENCES UserAccounts (Id) ON DELETE CASCADE, CONSTRAINT FK_Payments_Subscriptions_SubscriptionId FOREIGN KEY (SubscriptionId) REFERENCES Subscriptions (Id) ON DELETE RESTRICT)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Payments_PaymentTransactionId ON Payments (PaymentTransactionId)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Payments_MpesaReceiptNumber ON Payments (MpesaReceiptNumber)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS Invoices (Id TEXT NOT NULL PRIMARY KEY, InvoiceNumber TEXT NOT NULL, PaymentId TEXT NOT NULL, UserId INTEGER NOT NULL, SubscriptionId INTEGER NOT NULL, AmountKes INTEGER NOT NULL, Currency TEXT NOT NULL, IssuedAt TEXT NOT NULL, CONSTRAINT FK_Invoices_Payments_PaymentId FOREIGN KEY (PaymentId) REFERENCES Payments (Id) ON DELETE RESTRICT, CONSTRAINT FK_Invoices_UserAccounts_UserId FOREIGN KEY (UserId) REFERENCES UserAccounts (Id) ON DELETE CASCADE, CONSTRAINT FK_Invoices_Subscriptions_SubscriptionId FOREIGN KEY (SubscriptionId) REFERENCES Subscriptions (Id) ON DELETE RESTRICT)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Invoices_InvoiceNumber ON Invoices (InvoiceNumber)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Invoices_PaymentId ON Invoices (PaymentId)");
        return;
    }

    if (db.Database.IsNpgsql())
    {
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"PasswordResetTokens\" (\"TokenHash\" character varying(64) PRIMARY KEY, \"UserAccountId\" integer NOT NULL REFERENCES \"UserAccounts\" (\"Id\") ON DELETE CASCADE, \"CreatedAt\" timestamp with time zone NOT NULL, \"ExpiresAt\" timestamp with time zone NOT NULL, \"UsedAt\" timestamp with time zone NULL)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_PasswordResetTokens_UserAccountId_ExpiresAt\" ON \"PasswordResetTokens\" (\"UserAccountId\", \"ExpiresAt\")");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"SubscriptionPlans\" (\"Code\" character varying(24) PRIMARY KEY, \"Name\" character varying(48) NOT NULL, \"PriceKes\" integer NOT NULL, \"MaxProperties\" integer NOT NULL, \"MaxDevices\" integer NOT NULL, \"AnomalyDetection\" boolean NOT NULL, \"Analytics\" boolean NOT NULL, \"IncidentManagement\" boolean NOT NULL, \"SecurityIntelligence\" boolean NOT NULL, \"MultipleStaffAccounts\" boolean NOT NULL, \"AdvancedReports\" boolean NOT NULL, \"PrioritySupport\" boolean NOT NULL)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"Subscriptions\" (\"Id\" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY, \"UserId\" integer NOT NULL REFERENCES \"UserAccounts\" (\"Id\") ON DELETE CASCADE, \"PlanCode\" character varying(24) NOT NULL REFERENCES \"SubscriptionPlans\" (\"Code\") ON DELETE RESTRICT, \"Status\" character varying(24) NOT NULL, \"TrialStart\" timestamp with time zone NULL, \"TrialEnd\" timestamp with time zone NULL, \"CurrentPeriodStart\" timestamp with time zone NULL, \"CurrentPeriodEnd\" timestamp with time zone NULL, \"CancelAtPeriodEnd\" boolean NOT NULL, \"CreatedAt\" timestamp with time zone NOT NULL, \"UpdatedAt\" timestamp with time zone NOT NULL)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_Subscriptions_UserId_Status\" ON \"Subscriptions\" (\"UserId\", \"Status\")");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"PaymentTransactions\" (\"Id\" uuid PRIMARY KEY, \"UserId\" integer NOT NULL REFERENCES \"UserAccounts\" (\"Id\") ON DELETE CASCADE, \"SubscriptionId\" bigint NOT NULL REFERENCES \"Subscriptions\" (\"Id\") ON DELETE RESTRICT, \"PlanCode\" character varying(24) NOT NULL REFERENCES \"SubscriptionPlans\" (\"Code\") ON DELETE RESTRICT, \"AmountKes\" integer NOT NULL, \"PhoneNumber\" character varying(16) NOT NULL, \"Status\" character varying(24) NOT NULL, \"MerchantRequestId\" text NULL, \"CheckoutRequestId\" character varying(128) NULL, \"ResponseDescription\" text NULL, \"ResultCode\" integer NULL, \"MpesaReceiptNumber\" character varying(40) NULL, \"InitiatedAt\" timestamp with time zone NOT NULL, \"CompletedAt\" timestamp with time zone NULL)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PaymentTransactions_CheckoutRequestId\" ON \"PaymentTransactions\" (\"CheckoutRequestId\")");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PaymentTransactions_MpesaReceiptNumber\" ON \"PaymentTransactions\" (\"MpesaReceiptNumber\")");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"Payments\" (\"Id\" uuid PRIMARY KEY, \"PaymentTransactionId\" uuid NOT NULL UNIQUE REFERENCES \"PaymentTransactions\" (\"Id\") ON DELETE RESTRICT, \"UserId\" integer NOT NULL REFERENCES \"UserAccounts\" (\"Id\") ON DELETE CASCADE, \"SubscriptionId\" bigint NOT NULL REFERENCES \"Subscriptions\" (\"Id\") ON DELETE RESTRICT, \"AmountKes\" integer NOT NULL, \"MpesaReceiptNumber\" character varying(40) NOT NULL UNIQUE, \"PaidAt\" timestamp with time zone NOT NULL)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"Invoices\" (\"Id\" uuid PRIMARY KEY, \"InvoiceNumber\" character varying(40) NOT NULL UNIQUE, \"PaymentId\" uuid NOT NULL UNIQUE REFERENCES \"Payments\" (\"Id\") ON DELETE RESTRICT, \"UserId\" integer NOT NULL REFERENCES \"UserAccounts\" (\"Id\") ON DELETE CASCADE, \"SubscriptionId\" bigint NOT NULL REFERENCES \"Subscriptions\" (\"Id\") ON DELETE RESTRICT, \"AmountKes\" integer NOT NULL, \"Currency\" character varying(3) NOT NULL, \"IssuedAt\" timestamp with time zone NOT NULL)");
        return;
    }

    throw new InvalidOperationException("SmartGuard billing schema supports SQLite and PostgreSQL only.");
}
