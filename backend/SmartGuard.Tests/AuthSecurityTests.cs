using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SmartGuard.API.Controllers;
using SmartGuard.API.Services;
using SmartGuard.Application.Auth;
using SmartGuard.Application.Services;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.Tests;

public class AuthSecurityTests
{
    [Fact]
    public void PasswordHasher_ShouldCreateStableHashAndVerifyCorrectly()
    {
        var hash1 = PasswordHasher.Hash("admin123");
        var hash2 = PasswordHasher.Hash("admin123");

        Assert.Equal(hash1, hash2);
        Assert.NotEqual("admin123", hash1);
        Assert.True(PasswordHasher.Verify("admin123", hash1));
        Assert.False(PasswordHasher.Verify("wrong-password", hash1));
    }

    [Fact]
    public void AccountRegistrationValidator_ShouldValidateAResidentUserRegistration()
    {
        var result = AccountRegistrationValidator.Validate("Jane Doe", "jane@example.com", "Password123");

        Assert.True(result.IsValid);
        Assert.Equal("Resident User", result.Role);
        Assert.Equal("jane@example.com", result.Email);
        Assert.Equal("Jane Doe", result.FullName);
    }

    [Fact]
    public void PropertyRegistrationValidator_ShouldValidatePropertyDetails()
    {
        var result = PropertyRegistrationValidator.Validate(
            "Riverside Villa",
            "18 River Road, Nairobi",
            "Amina Hassan",
            PropertyStatus.Home);

        Assert.True(result.IsValid);
        Assert.Equal("Riverside Villa", result.Name);
        Assert.Equal("Amina Hassan", result.Owner);
        Assert.Equal(PropertyStatus.Home, result.Status);
    }

    [Fact]
    public void SecurityReportExporter_ShouldBuildCsvForSecurityEvents()
    {
        var csv = SecurityReportExporter.BuildCsv(new[]
        {
            new SecurityEvent
            {
                Id = Guid.NewGuid(),
                PropertyId = "prop-001",
                DeviceId = "cam-101",
                SensorType = "Camera",
                EventType = SecurityEventType.DoorOpened,
                Location = "Front Gate",
                Description = "Door open",
                Priority = AlertPriority.High,
                Status = "Triggered",
                Timestamp = new DateTimeOffset(2026, 9, 26, 4, 12, 0, TimeSpan.Zero),
                CreatedAt = new DateTimeOffset(2026, 9, 26, 4, 12, 5, TimeSpan.Zero)
            }
        });

        Assert.Contains("PropertyId", csv);
        Assert.Contains("Front Gate", csv);
        Assert.Contains("DoorOpened", csv);
    }

    [Fact]
    public async Task DownloadReport_ShouldGenerateCsvForSqliteSecurityEvents()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var context = new AppDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            context.SecurityEvents.Add(new SecurityEvent
            {
                Id = Guid.NewGuid(),
                PropertyId = "prop-001",
                DeviceId = "cam-101",
                SensorType = "Camera",
                EventType = SecurityEventType.DoorOpened,
                Location = "Front Gate",
                Description = "Door open",
                Priority = AlertPriority.High,
                Status = "Triggered",
                Timestamp = new DateTimeOffset(2026, 9, 26, 4, 12, 0, TimeSpan.Zero),
                CreatedAt = new DateTimeOffset(2026, 9, 26, 4, 12, 5, TimeSpan.Zero)
            });

            await context.SaveChangesAsync();

            var controller = new SecurityController(
                context,
                new SecurityDashboardService(new TestDashboardDataSource()));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Role, "Administrator") },
                        "test"))
                }
            };

            var result = await controller.DownloadReport();

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/csv", file.ContentType);
            Assert.Contains("Front Gate", Encoding.UTF8.GetString(file.FileContents));
        }

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task GetProperties_ShouldLimitResidentToOwnedProperties()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var context = new AppDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.UserAccounts.AddRange(
                new UserAccount { Id = 7, FullName = "Jane Doe", Email = "jane@example.com", PasswordHash = "test-hash" },
                new UserAccount { Id = 8, FullName = "Jane Doe", Email = "jane2@example.com", PasswordHash = "test-hash" });
            context.Properties.Add(new Property
            {
                Id = "prop-jane",
                Name = "Jane's Home",
                Address = "1 Main Street",
                Owner = "Jane Doe",
                OwnerUserId = 7,
                Status = PropertyStatus.Home
            });
            context.Properties.Add(new Property
            {
                Id = "prop-jane-2",
                Name = "Jane's Other Home",
                Address = "2 Main Street",
                Owner = "Jane Doe",
                OwnerUserId = 8,
                Status = PropertyStatus.Home
            });
            await context.SaveChangesAsync();

            var controller = new SecurityController(
                context,
                new SecurityDashboardService(new TestDashboardDataSource()));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Name, "Jane Doe"), new Claim(ClaimTypes.Role, "Resident User") },
                        "test"))
                }
            };

            var action = await controller.GetProperties();
            var ok = Assert.IsType<OkObjectResult>(action.Result);
            var properties = Assert.IsAssignableFrom<IEnumerable<Property>>(ok.Value);

            Assert.Single(properties);
            Assert.Equal("Jane's Home", properties.Single().Name);

            var adminController = new SecurityController(
                context,
                new SecurityDashboardService(new TestDashboardDataSource()));
            adminController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Name, "Admin User"), new Claim(ClaimTypes.Role, "Administrator") },
                        "test"))
                }
            };
            var adminAction = await adminController.GetProperties();
            var adminResult = Assert.IsType<OkObjectResult>(adminAction.Result);
            Assert.Equal(4, Assert.IsAssignableFrom<IEnumerable<Property>>(adminResult.Value).Count());

            var forbidden = await controller.UpdateProperty("prop-001", new SecurityController.CreatePropertyRequest
            {
                Name = "Changed by another owner",
                Address = "1 New Street",
                Owner = "Jane Doe",
                Status = nameof(PropertyStatus.Home)
            });
            Assert.IsType<ForbidResult>(forbidden.Result);
        }

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task CreateProperty_ShouldRequireAndPersistPropertyPhoto()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };

        await using (var context = new AppDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.UserAccounts.Add(new UserAccount { Id = 7, FullName = "Jane Doe", Email = "jane@example.com", PasswordHash = "test-hash" });
            await context.SaveChangesAsync();

            var controller = new SecurityController(
                context,
                new SecurityDashboardService(new TestDashboardDataSource()),
                environment);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Name, "Jane Doe"), new Claim(ClaimTypes.Role, "Resident User") },
                        "test"))
                }
            };

            var request = new SecurityController.CreatePropertyRequest
            {
                Name = "Photo Home",
                Address = "11 Image Lane",
                Status = nameof(PropertyStatus.Home)
            };
            var missingPhoto = await controller.CreateProperty(request);
            Assert.IsType<BadRequestObjectResult>(missingPhoto.Result);
            Assert.Equal(2, await context.Properties.CountAsync());

            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };
            request.PropertyPhoto = new FormFile(new MemoryStream(pngBytes), 0, pngBytes.Length, "propertyPhoto", "home.png");
            var created = await controller.CreateProperty(request);
            var result = Assert.IsType<CreatedAtActionResult>(created.Result);
            var property = Assert.IsType<Property>(result.Value);

            Assert.Equal(7, property.OwnerUserId);
            Assert.StartsWith("/uploads/properties/", property.PhotoUrl);
            Assert.Equal(3, await context.Properties.CountAsync());
            Assert.True(File.Exists(Path.Combine(environment.ContentRootPath, "wwwroot", property.PhotoUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));

            ProfilePhotoStorage.Delete(environment, property.PhotoUrl);
        }

        Directory.Delete(environment.ContentRootPath, recursive: true);
        await connection.DisposeAsync();
    }

    [Fact]
    public void AdministrativeEndpoints_ShouldRequireAdministratorRole()
    {
        var usersAuthorization = Assert.Single(typeof(AuthController)
            .GetMethod(nameof(AuthController.GetUsers))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>());
        var incidentAuthorization = Assert.Single(typeof(SecurityController)
            .GetMethod(nameof(SecurityController.ReviewEvent))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>());
        var userDetailAuthorization = Assert.Single(typeof(AuthController)
            .GetMethod(nameof(AuthController.GetUserDetails))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>());
        var sendReportAuthorization = Assert.Single(typeof(AuthController)
            .GetMethod(nameof(AuthController.SendUserReport))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>());
        var settingsAuthorization = Assert.Single(typeof(SettingsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal("Administrator", usersAuthorization.Roles);
        Assert.Equal("Administrator", incidentAuthorization.Roles);
        Assert.Equal("Administrator", userDetailAuthorization.Roles);
        Assert.Equal("Administrator", sendReportAuthorization.Roles);
        Assert.Equal("Administrator", settingsAuthorization.Roles);
    }

    [Fact]
    public async Task ProfilePhotoStorage_ShouldRejectMissingAndNonImageFiles()
    {
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };

        var missing = await ProfilePhotoStorage.SaveAsync(null, environment);
        var invalidFile = new FormFile(new MemoryStream(new byte[] { 1, 2, 3, 4 }), 0, 4, "profilePhoto", "profile.txt");
        var invalid = await ProfilePhotoStorage.SaveAsync(invalidFile, environment);

        Assert.Equal("A profile photo is required.", missing.Error);
        Assert.Equal("Upload a valid JPEG, PNG, or WebP profile photo.", invalid.Error);
        Assert.False(Directory.Exists(environment.ContentRootPath));
    }

    [Fact]
    public async Task ProfilePhotoStorage_ShouldSaveAndDeletePngFiles()
    {
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };
        var file = new FormFile(new MemoryStream(pngBytes), 0, pngBytes.Length, "profilePhoto", "profile.png");

        var (url, error) = await ProfilePhotoStorage.SaveAsync(file, environment);
        var path = Path.Combine(environment.ContentRootPath, "wwwroot", url!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        Assert.Null(error);
        Assert.StartsWith("/uploads/profiles/", url);
        Assert.True(File.Exists(path));

        ProfilePhotoStorage.Delete(environment, url);
        Assert.False(File.Exists(path));
        Directory.Delete(environment.ContentRootPath, recursive: true);
    }

    [Fact]
    public async Task Register_ShouldRequireAndPersistProfilePhoto()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };
        var jwt = new SmartGuard.API.Services.JwtTokenService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        await using (var context = new AppDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var controller = new AuthController(context, jwt, environment);
            var missingPhoto = await controller.Register(new SmartGuard.API.Models.RegisterRequest
            {
                FullName = "Photo Required",
                Email = "photo-required@example.com",
                Password = "Password123"
            });
            Assert.IsType<BadRequestObjectResult>(missingPhoto.Result);
            Assert.Empty(await context.UserAccounts.ToListAsync());

            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };
            var photo = new FormFile(new MemoryStream(pngBytes), 0, pngBytes.Length, "profilePhoto", "profile.png");
            var registered = await controller.Register(new SmartGuard.API.Models.RegisterRequest
            {
                FullName = "Photo Required",
                Email = "photo-required@example.com",
                Password = "Password123",
                ProfilePhoto = photo
            });

            Assert.IsType<OkObjectResult>(registered.Result);
            var account = await context.UserAccounts.SingleAsync();
            Assert.StartsWith("/uploads/profiles/", account.ProfilePhotoUrl);
        }

        Directory.Delete(environment.ContentRootPath, recursive: true);
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task AdminReport_ShouldBeDeliveredOnlyToTheIntendedUser()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var environment = new TestWebHostEnvironment { ContentRootPath = Path.GetTempPath() };
        var jwt = new SmartGuard.API.Services.JwtTokenService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        await using (var context = new AppDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.UserAccounts.AddRange(
                new UserAccount { Id = 21, FullName = "Admin User", Email = "admin@example.com", Role = "Administrator", PasswordHash = "hash" },
                new UserAccount { Id = 22, FullName = "Resident User", Email = "resident@example.com", Role = "Resident User", PasswordHash = "hash" },
                new UserAccount { Id = 23, FullName = "Other Resident", Email = "other@example.com", Role = "Resident User", PasswordHash = "hash" });
            context.Properties.Add(new Property
            {
                Id = "report-user-property",
                Name = "Resident Home",
                Address = "10 Report Road",
                Owner = "Resident User",
                OwnerUserId = 22,
                PhotoUrl = "/uploads/properties/resident-home.jpg"
            });
            await context.SaveChangesAsync();

            var admin = new AuthController(context, jwt, environment);
            admin.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "21"), new Claim(ClaimTypes.Role, "Administrator") },
                        "test"))
                }
            };

            var sent = await admin.SendUserReport(22, new SmartGuard.API.Models.CreateUserReportRequest
            {
                Title = "Property review",
                Body = "Please review the recent activity at your property."
            });
            Assert.IsType<OkObjectResult>(sent.Result);

            var detailAction = await admin.GetUserDetails(22);
            var detailResult = Assert.IsType<OkObjectResult>(detailAction.Result);
            using var details = JsonDocument.Parse(JsonSerializer.Serialize(detailResult.Value));
            Assert.Equal("Resident Home", details.RootElement.GetProperty("properties")[0].GetProperty("Name").GetString());
            Assert.Equal("/uploads/properties/resident-home.jpg", details.RootElement.GetProperty("properties")[0].GetProperty("PhotoUrl").GetString());
            Assert.Equal("Property review", details.RootElement.GetProperty("reports")[0].GetProperty("Title").GetString());

            var recipient = new AuthController(context, jwt, environment);
            recipient.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "22"), new Claim(ClaimTypes.Role, "Resident User") },
                        "test"))
                }
            };
            var inbox = await recipient.GetMyReports();
            var reports = Assert.IsAssignableFrom<IEnumerable<UserReport>>(Assert.IsType<OkObjectResult>(inbox.Result).Value);
            var delivered = Assert.Single(reports);
            Assert.Equal("Property review", delivered.Title);
            Assert.True(delivered.IsRead);

            var otherResident = new AuthController(context, jwt, environment);
            otherResident.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "23"), new Claim(ClaimTypes.Role, "Resident User") },
                        "test"))
                }
            };
            var otherInbox = await otherResident.GetMyReports();
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<UserReport>>(Assert.IsType<OkObjectResult>(otherInbox.Result).Value));
        }

        await connection.DisposeAsync();
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SmartGuard.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TestDashboardDataSource : ISecurityDashboardDataSource
    {
        public Task<int> GetTotalPropertiesAsync() => Task.FromResult(0);
        public Task<int> GetTotalEventsAsync() => Task.FromResult(0);
        public Task<int> GetOpenAlertsAsync() => Task.FromResult(0);
        public Task<int> GetTotalDevicesAsync() => Task.FromResult(0);
        public Task<int> GetCriticalAlertCountAsync() => Task.FromResult(0);
        public Task<List<SecurityEvent>> GetRecentEventsAsync(int count) => Task.FromResult(new List<SecurityEvent>());
        public Task<List<Alert>> GetRecentAlertsAsync(int count) => Task.FromResult(new List<Alert>());
    }
}
