using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Infrastructure.Clock;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Cards.Api.Endpoints;
using SmartBank.Cards.Application.Cards;
using SmartBank.Cards.Domain;
using SmartBank.Cards.Infrastructure.Persistence;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteToSmartBank(ctx.Configuration, "smartbank-cards"));

((IHostApplicationBuilder)builder).AddSmartBankOpenTelemetry("smartbank-cards");

builder.Services.AddTransient<CorrelationIdForwardingHandler>();
builder.Services.ConfigureHttpClientDefaults(b => b.AddHttpMessageHandler<CorrelationIdForwardingHandler>());

builder.Services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(FreezeCardCommand).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(FreezeCardCommand).Assembly);
// Logging is registered outermost so validation rejections are logged too
// (MediatR executes behaviors in registration order).
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddDbContext<CardsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Cards")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddScoped<ICardRepository, EfCardRepository>();
builder.Services.AddSingleton<IPanProtector, EnvPanProtector>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Jwt:Authority"];
        o.Audience = "smartbank-spa";
        o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        var key = builder.Configuration["Jwt:SigningKey"] ?? "dev-signing-key-32-bytes-long!!!!!!";
        o.TokenValidationParameters.IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        o.TokenValidationParameters.ValidateIssuer = false;
        o.TokenValidationParameters.ValidateAudience = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Cards")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<CardsDbContext>().Database.MigrateAsync();
    await CardsSeed.RunAsync(app.Services);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(o =>
{
    o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms (CorrelationId={CorrelationId})";
    // Health probes fire every few seconds — keep them out of Loki.
    o.GetLevel = (ctx, _, _) => ctx.Request.Path.StartsWithSegments("/health")
        ? LogEventLevel.Verbose
        : LogEventLevel.Information;
    o.EnrichDiagnosticContext = (dc, http) =>
    {
        dc.Set("CorrelationId", http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier);
        dc.Set("RequestPath", http.Request.Path.ToString());
        dc.Set("RequestMethod", http.Request.Method);
    };
});
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<JwtBlacklistMiddleware>();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapCardEndpoints();
app.Run();

public partial class Program;
