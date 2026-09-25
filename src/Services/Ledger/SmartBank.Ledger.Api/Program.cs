using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.EventBus;
using SmartBank.BuildingBlocks.Infrastructure.Clock;
using SmartBank.BuildingBlocks.Infrastructure.Idempotency;
using SmartBank.BuildingBlocks.Infrastructure.Messaging;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Contracts.Grpc;
using SmartBank.Ledger.Api.Endpoints;
using SmartBank.Ledger.Application.Abstractions;
using SmartBank.Ledger.Application.Transfers;
using SmartBank.Ledger.Infrastructure.Grpc;
using SmartBank.Ledger.Infrastructure.Idempotency;
using SmartBank.Ledger.Infrastructure.Persistence;
using SmartBank.Ledger.Infrastructure.Repositories;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteToSmartBank(ctx.Configuration, "smartbank-ledger"));

((IHostApplicationBuilder)builder).AddSmartBankOpenTelemetry("smartbank-ledger");

// Propagate X-Correlation-Id on all outgoing HttpClient/gRPC calls (Ledger -> Customer).
builder.Services.AddTransient<CorrelationIdForwardingHandler>();
builder.Services.ConfigureHttpClientDefaults(b => b.AddHttpMessageHandler<CorrelationIdForwardingHandler>());

builder.Services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(TransferFundsCommand).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(TransferFundsCommand).Assembly);
// Logging is registered outermost so validation rejections are logged too
// (MediatR executes behaviors in registration order).
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddDbContext<LedgerDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Ledger")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddScoped<IIdempotencyStore, RedisIdempotencyStore>();
builder.Services.AddScoped<IJournalRepository, EfJournalRepository>();
builder.Services.AddGrpcClient<AccountDirectory.AccountDirectoryClient>(o =>
    o.Address = new Uri(builder.Configuration["Grpc:Customer"] ?? "http://localhost:6102"));
builder.Services.AddScoped<IAccountDirectory, AccountDirectoryClientAdapter>();
// Async settlement: journal commit stages MoneyTransferredIntegrationEvent in
// outbox_messages; dispatcher publishes to RabbitMQ for background settlement
// (Customer capture/credit, notifications, fraud/AML checks).
builder.Services.AddRabbitMqEventPublisher(builder.Configuration, LedgerTopology.Exchange);
builder.Services.AddHostedService<SmartBank.Ledger.Infrastructure.Outbox.OutboxDispatcher>();

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
    .AddNpgSql(builder.Configuration.GetConnectionString("Ledger")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.MigrateAsync();
    await LedgerSeed.RunAsync(app.Services);
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
app.MapTransferEndpoints();
app.MapStatementEndpoints();
app.Run();

public partial class Program;
