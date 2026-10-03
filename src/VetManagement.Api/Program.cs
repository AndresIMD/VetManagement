using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VetManagement.Api.Authorization;
using VetManagement.Api.Hubs;
using VetManagement.Api.Payments;
using VetManagement.Api.Seeding;
using VetManagement.Api.Services;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Infrastructure.Data;
using VetManagement.Infrastructure.Persistence;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Inventory;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

// User secrets are loaded by CreateBuilder in Development only. Loading them in every Debug build
// (as before) let a developer's secrets override test/production configuration.

static bool IsPlaceholder(string? value)
 => !string.IsNullOrWhiteSpace(value) && value.Trim().StartsWith("<") && value.Trim().EndsWith(">");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
 ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
if (IsPlaceholder(connectionString))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection contains a placeholder.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("VetManagement.Api");
            sqlOptions.CommandTimeout(30);
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        }
    ));

builder.Services.AddIdentityCore<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    // Harden password requirements
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredUniqueChars = 3;
    // Brute-force protection: lock the account after repeated failed logins (enforced in AccountController)
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
if (IsPlaceholder(jwtKey) || IsPlaceholder(jwtIssuer) || IsPlaceholder(jwtAudience))
    throw new InvalidOperationException("JWT settings contain placeholders.");

var requireHttpsMetadata = builder.Configuration.GetValue<bool>("Security:RequireHttpsMetadata", !builder.Environment.IsDevelopment());
var keyBytes = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "JwtBearer";
    options.DefaultChallengeScheme = "JwtBearer";
})
.AddJwtBearer("JwtBearer", options =>
{
    options.RequireHttpsMetadata = requireHttpsMetadata;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ClockSkew = TimeSpan.Zero,
        RequireExpirationTime = true
    };
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                context.Token = accessToken;
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("Inventory.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.INVENTORY.READ))
    .AddPolicy("Inventory.Create", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.INVENTORY.CREATE))
    .AddPolicy("Inventory.Update", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.INVENTORY.UPDATE))
    .AddPolicy("Inventory.Delete", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.INVENTORY.DELETE))
    .AddPolicy("Exams.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXAMS.READ))
    .AddPolicy("Exams.Create", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXAMS.CREATE))
    .AddPolicy("Exams.Update", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXAMS.UPDATE))
    .AddPolicy("Exams.Delete", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXAMS.DELETE))
    .AddPolicy("ExternalLabs.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXTERNAL_LABS.READ))
    .AddPolicy("ExternalLabs.Create", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXTERNAL_LABS.CREATE))
    .AddPolicy("ExternalLabs.Update", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXTERNAL_LABS.UPDATE))
    .AddPolicy("ExternalLabs.Delete", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXTERNAL_LABS.DELETE))
    .AddPolicy("ExamsPerformed.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXAMS_PERFORMED.READ))
    .AddPolicy("ExamsPerformed.Create", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXAMS_PERFORMED.CREATE))
    .AddPolicy("ExamsPerformed.Update", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXAMS_PERFORMED.UPDATE))
    .AddPolicy("ExamsPerformed.Delete", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.EXAMS_PERFORMED.DELETE))
    .AddPolicy("Medical.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.MEDICAL.READ))
    .AddPolicy("Medical.Create", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.MEDICAL.CREATE))
    .AddPolicy("Medical.Update", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.MEDICAL.UPDATE))
    .AddPolicy("Medical.Delete", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.MEDICAL.DELETE))
    .AddPolicy("ClientsPets.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.CLIENTS_PETS.READ))
    .AddPolicy("ClientsPets.Create", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.CLIENTS_PETS.CREATE))
    .AddPolicy("ClientsPets.Update", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.CLIENTS_PETS.UPDATE))
    .AddPolicy("ClientsPets.Delete", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.CLIENTS_PETS.DELETE))
    .AddPolicy("Audit.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.AUDIT.READ))
    .AddPolicy("Users.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.USERS.READ))
    .AddPolicy("Users.Create", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.USERS.CREATE))
    .AddPolicy("Users.ManageRoles", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.USERS.MANAGE_ROLES))
    .AddPolicy("System.SendEmail", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.SYSTEM.SEND_EMAIL))
    .AddPolicy("Scheduling.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.SCHEDULING.READ))
    .AddPolicy("Scheduling.Book", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.SCHEDULING.BOOK))
    .AddPolicy("Scheduling.Manage", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.SCHEDULING.MANAGE))
    .AddPolicy("Billing.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.BILLING.READ))
    .AddPolicy("Billing.Charge", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.BILLING.CHARGE))
    .AddPolicy("Billing.Manage", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.BILLING.MANAGE))
    .AddPolicy("Clinical.Manage", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.CLINICAL.MANAGE))
    .AddPolicy("Reports.Read", p => p.RequireClaim(Permissions.CLAIM_TYPE, Permissions.REPORTS.READ));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<VetManagement.Application.Services.ItemService>();
builder.Services.AddScoped<InventoryQueryService>();
builder.Services.AddScoped<VetManagement.Application.Services.InventoryMovementService>();
builder.Services.AddScoped<VetManagement.Application.Services.AuditService>();
builder.Services.AddScoped<VetManagement.Application.Services.ExamService>();
builder.Services.AddScoped<VetManagement.Application.Services.ExamPerformedService>();
builder.Services.AddScoped<VetManagement.Application.Services.ExternalLabService>();
builder.Services.AddScoped<VetManagement.Application.Services.ClientPetService>();
builder.Services.AddScoped<VetManagement.Application.Services.MedicalVisitService>();
builder.Services.AddSingleton(new VetManagement.Application.Scheduling.SchedulingDefaults(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Scheduling", "scheduling.defaults.json"))));
builder.Services.AddScoped<VetManagement.Application.Scheduling.SchedulingSettingsService>();
builder.Services.AddScoped<VetManagement.Application.Scheduling.AppointmentService>();
builder.Services.AddScoped<VetManagement.Application.Scheduling.AgendaRules>();
builder.Services.AddScoped<VetManagement.Application.Scheduling.OnlineBookingService>();
builder.Services.AddSingleton(builder.Configuration.GetSection("BookingPortal").Get<VetManagement.Api.Controllers.Public.BookingPortalOptions>() ?? new());
builder.Services.AddScoped<VetManagement.Application.Scheduling.AppointmentNotifier>();
builder.Services.AddScoped<VetManagement.Application.Billing.BillingSettingsService>();
builder.Services.AddScoped<VetManagement.Application.Billing.BillingService>();
builder.Services.AddScoped<VetManagement.Application.Clinical.ClinicalSettingsService>();
builder.Services.AddScoped<VetManagement.Application.Clinical.ClinicalService>();
builder.Services.AddScoped<VetManagement.Application.Reports.ReportsService>();
builder.Services.AddPaymentGateway(builder.Configuration, builder.Environment);
builder.Services.AddScoped<VetManagement.Application.Contracts.Services.IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024;
    options.CompactionPercentage = 0.25;
});
builder.Services.AddScoped<OptimizedCacheService>();
builder.Services.AddScoped<OptimizedQueryService>();

builder.Services.AddScoped<VetManagement.Application.Contracts.Services.IRealtimeNotificationService, RealtimeNotificationService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<InventoryAlertsHostedService>();
builder.Services.AddHostedService<AppointmentRemindersHostedService>();

builder.Services.AddScoped<IDataSeeder, DataSeeder>();

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
if (allowedOrigins == null || allowedOrigins.Length == 0)
    throw new InvalidOperationException("AllowedOrigins for CORS are not configured.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalOnly", policy => policy.WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials());
});

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 32 * 1024;
    options.StreamBufferCapacity = 10;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
        return new ValueTask();
    };

    // Public booking portal (anonymous), per client IP. Defaults 120 reads / 10 writes per minute; tune per clinic with RateLimits:*.
    options.AddPolicy("public-read", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:PublicReadPerMinute", 120), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("public-write", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:PublicWritePerMinute", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // Login attempts per client IP, on top of the global limit and the per-account lockout.
    options.AddPolicy("login", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Skip rate limiting for health checks, static files, and development tools
        var path = context.Request.Path.ToString();
        if (path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js", StringComparison.OrdinalIgnoreCase))
        {
            return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("bypass");
        }

        var userIdentifier = context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

        // More permissive limits in development
        var permitLimit = builder.Environment.IsDevelopment() ? 1000 : 100;
        var window = builder.Environment.IsDevelopment() ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(1);

        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            userIdentifier,
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0
            });
    });
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.DefaultBufferSize = 16 * 1024;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var securityConfig = builder.Configuration.GetSection("Security");
var enableHttpsRedirection = securityConfig.GetValue<bool>("EnableHttpsRedirection", true);
var enableHsts = securityConfig.GetValue<bool>("EnableHsts", true);

if (!app.Environment.IsDevelopment())
{
    if (enableHsts)
        app.UseHsts();
    if (enableHttpsRedirection)
        app.UseHttpsRedirection();
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });
}
else
{
    if (enableHttpsRedirection)
        app.UseHttpsRedirection();
}

// Outside Development: logs the full exception and returns an RFC 7807 ProblemDetails body without internals.
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler();

app.UseRateLimiter();
app.UseCors("LocalOnly");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<InventoryHub>("/hubs/inventory");
app.MapHub<DataHub>("/hubs/data");
app.MapHealthChecks("/healthz").AllowAnonymous();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

// Database Migration & Seeding
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

await app.RunAsync();
