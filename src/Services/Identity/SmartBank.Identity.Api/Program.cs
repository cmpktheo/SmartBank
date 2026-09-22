using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Serilog;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Infrastructure.Clock;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Identity.Api.Endpoints;
using SmartBank.Identity.Application.Abstractions;
using SmartBank.Identity.Application.Auth;
using SmartBank.Identity.Infrastructure.Auth;
using SmartBank.Identity.Infrastructure.Persistence;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteToSmartBank(ctx.Configuration, "smartbank-identity"));

((IHostApplicationBuilder)builder).AddSmartBankOpenTelemetry("smartbank-identity");

builder.Services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(LoginCommand).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(LoginCommand).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddDbContext<IdentityDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Identity")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

var isDevOrTesting = builder.Environment.IsDevelopment()
    || builder.Environment.IsEnvironment("Testing")
    || Environment.GetEnvironmentVariable("E2E_OTP_SEAM") == "true";

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(o =>
{
    if (isDevOrTesting)
    {
        // Demo seed users use password "123456" (see IdentitySeed + RUNBOOK).
        // Keep strict policy for Production, lenient for local dev/testing.
        o.Password.RequiredLength = 6;
        o.Password.RequireDigit = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequiredUniqueChars = 1;
    }
    else
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = true;
        o.Password.RequireUppercase = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireNonAlphanumeric = true;
        o.Password.RequiredUniqueChars = 4;
    }
    o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    o.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<IdentityDbContext>()
.AddDefaultTokenProviders();

var signingKey = builder.Configuration["Jwt:SigningKey"] ?? "dev-signing-key-32-bytes-long!!!!!!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Jwt:Authority"];
        o.Audience = "smartbank-spa";
        o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        // Fallback to symmetric validation so Gateway + services work without remote JWKS in tests.
        o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        o.TokenValidationParameters.ValidateIssuer = false;
        o.TokenValidationParameters.ValidateAudience = false;
    });
builder.Services.AddAuthorization();

builder.Services.AddOpenIddict()
    .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())
    .AddServer(o =>
    {
        o.SetTokenEndpointUris("/connect/token");
        o.AllowPasswordFlow();
        o.AllowRefreshTokenFlow();
        o.RegisterScopes("smartbank.api", "offline_access");
        o.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        o.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
        o.DisableAccessTokenEncryption();
        o.AddDevelopmentEncryptionCertificate();
        o.AddDevelopmentSigningCertificate();
        o.UseAspNetCore().EnableTokenEndpointPassthrough();
    })
    .AddValidation(o =>
    {
        o.UseLocalServer();
        o.UseAspNetCore();
    });

builder.Services.AddScoped<ILoginUserLookup, EfLoginLookup>();
builder.Services.AddScoped<ILoginUserById>(sp => (ILoginUserById)sp.GetRequiredService<ILoginUserLookup>());
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<ITokenBlacklist, RedisTokenBlacklist>();
builder.Services.AddScoped<IMfaStore, RedisMfaStore>();
if (builder.Environment.IsEnvironment("Testing") || Environment.GetEnvironmentVariable("E2E_OTP_SEAM") == "true")
    builder.Services.AddSingleton<IOtpDelivery, InMemoryOtpSink>();
else
    builder.Services.AddScoped<IOtpDelivery, LogOtpDelivery>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Identity")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    await IdentitySeed.RunAsync(app.Services);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<JwtBlacklistMiddleware>();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapAuthEndpoints();

// JWKS endpoint (symmetric oct key for HS256 so Gateway can validate).
app.MapGet("/connect/jwks", (IConfiguration config) =>
{
    var key = config["Jwt:SigningKey"] ?? "dev-signing-key-32-bytes-long!!!!!!";
    var jwk = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(key));
    return Results.Ok(new
    {
        keys = new[] { new { kty = "oct", use = "sig", alg = "HS256", kid = "dev", k = jwk } }
    });
});

app.Run();

public partial class Program;
