using System.Text;
using System.Text.Json;
using Prometheus;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Api.Middleware;
using CMS.Application;
using CMS.Application.Abstractions;
using CMS.Domain.Identity;
using CMS.Infrastructure;
using CMS.Infrastructure.Identity;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Default document storage root to ContentRoot/App_Data/documents unless overridden by config.
if (string.IsNullOrWhiteSpace(builder.Configuration["DocumentStorage:Root"]))
{
    var defaultRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "documents");
    builder.Configuration["DocumentStorage:Root"] = defaultRoot;
}

// Default dev email drop folder to ContentRoot/App_Data/mail-drop so FileSystemEmailSender works
// out of the box in Development when no SMTP host is set.
if (string.IsNullOrWhiteSpace(builder.Configuration["Mail:DevDropFolder"]))
{
    builder.Configuration["Mail:DevDropFolder"] =
        Path.Combine(builder.Environment.ContentRootPath, "App_Data", "mail-drop");
}

// --- Serilog ---
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services));

// --- Core services ---
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<CorrelationContextAccessor>();
builder.Services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContextAccessor>());
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddHttpContextAccessor();

// SignInManager isn't registered by AddIdentityCore; we need it for password checks.
builder.Services.AddScoped<SignInManager<ApplicationUser>>();
builder.Services.AddScoped<IUserConfirmation<ApplicationUser>, DefaultUserConfirmation<ApplicationUser>>();

// --- JWT bearer: single AddAuthentication so JwtBearer is the real default scheme ---
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

// Resolve TokenValidationParameters from IOptions<JwtOptions> AT options-build time,
// so test harnesses that override Jwt:* via ConfigureAppConfiguration take effect.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOpts) =>
    {
        var jwt = jwtOpts.Value;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = string.IsNullOrWhiteSpace(jwt.SigningKey)
                ? new SymmetricSecurityKey(new byte[64])
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });

// --- Authorization: dynamic permission policies ---
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

// --- API versioning ---
builder.Services
    .AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.AssumeDefaultVersionWhenUnspecified = true;
        o.ReportApiVersions = true;
    })
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// --- Rate limiting on /auth/* ---
// Fixed window: at most 10 requests / minute per IP. 429 on overflow — covers brute-force password
// guessing and reset-token enumeration. Integration tests opt out via the "RateLimiting:Disabled" flag.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", httpCtx =>
    {
        if (builder.Configuration.GetValue<bool>("RateLimiting:Disabled"))
            return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("disabled");
        var key = httpCtx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CMS API", Version = "v1", Description = "CMS — Phase 2 (Auth + RBAC)." });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT access token (without the 'Bearer ' prefix)."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }]
            = Array.Empty<string>()
    });
});

// --- CORS ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// --- Health checks ---
var connectionString = builder.Configuration.GetConnectionString("CmsDb");
var healthBuilder = builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(connectionString))
{
    healthBuilder.AddNpgSql(connectionString, name: "postgres", tags: new[] { "ready" });
}

var app = builder.Build();

// --- Pipeline ---
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("CorrelationId", http.TraceIdentifier);
        diag.Set("UserAgent", http.Request.Headers.UserAgent.ToString());
    };
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1"));
}

app.UseCors();
app.UseRouting();

// Security headers on every response. These are cheap and have no impact on API clients.
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["X-XSS-Protection"] = "0";
    if (ctx.Request.IsHttps) h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Prometheus request-duration + response-code histogram. Anonymous; scrape with a sidecar/node_exporter.
app.UseHttpMetrics();

app.MapControllers();
app.MapMetrics("/metrics"); // anonymous — restrict by firewall / scrape auth in production

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = reg => reg.Tags.Contains("ready"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(ctx.Response.Body, new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), error = e.Value.Exception?.Message })
        });
    }
});

// --- Development seeding (skippable so integration tests can seed themselves) ---
var skipHostSeed = app.Configuration.GetValue<bool>("Dev:SkipHostSeed");
if (app.Environment.IsDevelopment() && !skipHostSeed)
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    try
    {
        var db = sp.GetRequiredService<CmsDbContext>();
        if (db.Database.IsRelational() && db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            await db.Database.MigrateAsync();
        }
        var roles = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        await IdentitySeeder.SeedAsync(db, roles, users, sp.GetRequiredService<ILogger<Program>>());
        await CMS.Infrastructure.Clients.ServiceSchemaSeeder.ApplyAsync(db,
            sp.GetRequiredService<ILogger<Program>>());
        await CMS.Infrastructure.Compliance.ComplianceDefaultsSeeder.ApplyAsync(db,
            sp.GetRequiredService<ILogger<Program>>());
        await CMS.Infrastructure.Persistence.DevSampleDataSeeder.ApplyAsync(db,
            sp.GetRequiredService<ILogger<Program>>());
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Dev seed/migrate failed. Continuing startup.");
    }
}

try
{
    Log.Information("Starting CMS.Api");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CMS.Api terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
