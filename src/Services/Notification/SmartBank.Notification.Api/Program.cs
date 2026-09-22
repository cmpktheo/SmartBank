using Microsoft.EntityFrameworkCore;
using Serilog;
using SmartBank.BuildingBlocks.EventBus;
using SmartBank.BuildingBlocks.Infrastructure.Messaging;
using SmartBank.BuildingBlocks.Web;
using SmartBank.Notification.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteToSmartBank(ctx.Configuration, "smartbank-notification"));

((IHostApplicationBuilder)builder).AddSmartBankOpenTelemetry("smartbank-notification");

builder.Services.AddDbContext<NotificationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Notification")));
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Notification")!);

// Async notifications: consume booked-transfer events published by Ledger outbox.
builder.Services.AddRabbitMqEventPublisher(builder.Configuration, LedgerTopology.Exchange);
builder.Services.AddHostedService<MoneyTransferredConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

app.MapGet("/api/notifications/log", async (int? take, NotificationDbContext db, HttpContext ctx) =>
{
    if (!app.Environment.IsDevelopment() && !ctx.User.IsInRole("Operations"))
        return Results.Forbid();
    var t = Math.Min(take.GetValueOrDefault(50), 100);
    var rows = await db.Log.OrderByDescending(x => x.CreatedAt).Take(t).ToListAsync(ctx.RequestAborted);
    return Results.Ok(rows);
});

app.Run();

public sealed class NotificationEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid EventId { get; set; }
    public string Channel { get; set; } = "Email";
    public string Template { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }
    public DbSet<NotificationEntry> Log => Set<NotificationEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<NotificationEntry>(e =>
        {
            e.ToTable("notification_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.Channel).HasColumnName("channel");
            e.Property(x => x.Template).HasColumnName("template");
            e.Property(x => x.Recipient).HasColumnName("recipient");
            e.Property(x => x.Subject).HasColumnName("subject");
            e.Property(x => x.Body).HasColumnName("body");
            e.Property(x => x.Payload).HasColumnName("payload");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.EventId).IsUnique();
        });
    }

    public static string AccountOpenedBody(string accountType, string iban, string currency) =>
        $"Welcome to SmartBank.\n\nYour {accountType} account is open.\nIBAN: {iban}\nCurrency: {currency}";

    public static string TransferBody(string reference, string amount, string currency, string from, string to, string when) =>
        $"A transfer was booked.\n\nReference: {reference}\nAmount: {amount} {currency}\nFrom: {from}\nTo: {to}\nWhen: {when}";

    public static string CardFrozenBody(string lastFour) => $"Your card •••• {lastFour} was frozen.";
}

public partial class Program;
