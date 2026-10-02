using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=smartguard.db"));

builder.Services.AddScoped<ISecurityDashboardDataSource, AppDbContextSecurityDashboardDataSource>();
builder.Services.AddScoped<SecurityDashboardService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<SmtpEmailSender>();

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
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartGuard.API v1");
    });
}

app.UseCors("FrontendPolicy");
app.UseStaticFiles();
// app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
