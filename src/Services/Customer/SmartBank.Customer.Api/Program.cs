using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.EventBus;
using SmartBank.BuildingBlocks.Infrastructure.Clock;
using SmartBank.BuildingBlocks.Infrastructure.Messaging;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Customer.Api.Endpoints;
using SmartBank.Customer.Api.Grpc;
using SmartBank.Customer.Api.Messaging;
using SmartBank.Customer.Application.Abstractions;
using SmartBank.Customer.Application.Accounts;
using SmartBank.Customer.Infrastructure.Persistence;
using SmartBank.Customer.Infrastructure.Repositories;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteToSmartBank(ctx.Configuration, "smartbank-customer"));

((IHostApplicationBuilder)builder).AddSmartBankOpenTelemetry("smartbank-customer");

builder.Services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(OpenAccountCommand).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(OpenAccountCommand).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddDbContext<CustomerDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Customer")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddScoped<IBankAccountRepository, EfBankAccountRepository>();
builder.Services.AddScoped<ICustomerRepository, EfCustomerRepository>();

// Async settlement: consume MoneyTransferredIntegrationEvent (booked by Ledger)
// to CaptureHold + CreditPosted; HoldExpiryWorker compensates Reserve-without-Book.
builder.Services.AddRabbitMqEventPublisher(builder.Configuration, LedgerTopology.Exchange);
builder.Services.AddHostedService<MoneyTransferredConsumer>();
builder.Services.AddHostedService<HoldExpiryWorker>();

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
builder.Services.AddGrpc();
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Customer")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
    await db.Database.MigrateAsync();
    await CustomerSeed.RunAsync(app.Services);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<JwtBlacklistMiddleware>();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapAccountEndpoints();
app.MapGrpcService<AccountDirectoryService>();
app.Run();

public partial class Program;
