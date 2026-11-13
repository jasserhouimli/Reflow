using System.Text;
using System.Threading.RateLimiting;
using Reflow.Infrastructure;
using Reflow.Infrastructure.Middleware;
using Reflow.Modules.NodeTypes;
using Reflow.Modules.PipelineExecution;
using Reflow.Modules.Triggers;
using Reflow.Modules.Identity;
using Reflow.Modules.Pipelines;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddReflowInfrastructure();

IdentityModule.Register(builder);
PipelinesModule.Register(builder);
NodeTypesModule.Register(builder);
PipelineExecutionModule.Register(builder);
TriggersModule.Register(builder);

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (builder.Environment.IsDevelopment())
    {
        jwtKey = "dev-secret-key-that-is-at-least-32-characters-long-12345";
    }
    else
    {
        throw new InvalidOperationException("Configuration 'Jwt:Key' is required and must not be empty.");
    }
}

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Configuration 'Jwt:Key' must be at least 256 bits (32 bytes) for secure HMAC-SHA256 signing.");
}

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
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Reflow",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Reflow",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Cookies["Reflow.Token"];
            if (string.IsNullOrEmpty(token)
                && context.Request.Path.StartsWithSegments("/hubs"))
            {
                token = context.Request.Query["access_token"];
            }
            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "Reflow.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    var authLimit = builder.Configuration.GetValue<int>("RateLimiting:AuthPermitLimit", 5);
    var generalLimit = builder.Configuration.GetValue<int>("RateLimiting:GeneralPermitLimit", 100);
    var webhookLimit = builder.Configuration.GetValue<int>("RateLimiting:WebhookPermitLimit", 30);

    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = authLimit;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("general", limiterOptions =>
    {
        limiterOptions.PermitLimit = generalLimit;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 10;
    });

    options.AddFixedWindowLimiter("webhook", limiterOptions =>
    {
        limiterOptions.PermitLimit = webhookLimit;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:5174"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var provider = scope.ServiceProvider;
    await provider.GetRequiredService<Reflow.Modules.Identity.Persistence.IdentityDbContext>()
        .Database.MigrateAsync();
    await provider.GetRequiredService<Reflow.Modules.Pipelines.Persistence.PipelinesDbContext>()
        .Database.MigrateAsync();
    await provider.GetRequiredService<Reflow.Modules.PipelineExecution.Persistence.PipelineExecutionDbContext>()
        .Database.MigrateAsync();
    await provider.GetRequiredService<Reflow.Modules.Triggers.Persistence.TriggersDbContext>()
        .Database.MigrateAsync();
}

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAntiforgery();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

IdentityModule.MapEndpoints(app);
PipelinesModule.MapEndpoints(app);
NodeTypesModule.MapEndpoints(app);
PipelineExecutionModule.MapEndpoints(app);
TriggersModule.MapEndpoints(app);

// Liveness + Postgres reachability. No auth, no rate limiting: supervisors,
// CI and dev scripts all poll this to know the backend is truly ready.
app.MapGet("/health", async (IConfiguration config, CancellationToken ct) =>
{
    var timestamp = DateTime.UtcNow;
    try
    {
        var connectionString = config.GetConnectionString("Reflow");
        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await conn.OpenAsync(timeout.Token);
        await using var cmd = new Npgsql.NpgsqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync(timeout.Token);
        return Results.Ok(new { status = "healthy", database = "reachable", timestamp });
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
    {
        return Results.Json(
            new { status = "degraded", database = "unreachable", timestamp },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

public partial class Program { }
