using Microsoft.EntityFrameworkCore;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.BuildingBlocks.Infrastructure.Outbox;
using SmartBank.Customer.Domain;

namespace SmartBank.Customer.Infrastructure.Persistence;

public sealed class CustomerDbContext : DbContext
{
    public CustomerDbContext(DbContextOptions<CustomerDbContext> options) : base(options) { }

    public DbSet<Domain.Customer> Customers => Set<Domain.Customer>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Hold> Holds => Set<Hold>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Domain.Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LegalName).HasColumnName("legal_name").HasMaxLength(120);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(256);
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.OwnsOne(x => x.Address, a =>
            {
                a.Property(p => p.Line1).HasColumnName("addr_line1");
                a.Property(p => p.Line2).HasColumnName("addr_line2");
                a.Property(p => p.City).HasColumnName("addr_city");
                a.Property(p => p.PostalCode).HasColumnName("addr_postal");
                a.Property(p => p.CountryCode).HasColumnName("addr_country");
            });
            e.Ignore(x => x.DomainEvents);
        });
        b.Entity<BankAccount>(e =>
        {
            e.ToTable("bank_accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Alias).HasColumnName("alias").HasMaxLength(40);
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.OpenedAt).HasColumnName("opened_at");
            e.OwnsOne(x => x.Iban, ib =>
            {
                ib.Property(p => p.Value).HasColumnName("iban");
                ib.HasIndex(p => p.Value).IsUnique().HasDatabaseName("ux_bank_accounts_iban");
            });
            e.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3)
                .HasConversion<string>(v => v.Code, v => Currency.From(v));
            e.OwnsOne(x => x.AvailableBalance, m =>
            {
                m.Property(p => p.Amount).HasColumnName("available_balance").HasColumnType("numeric(18,2)");
                m.Property(p => p.Currency).HasColumnName("available_currency").HasMaxLength(3)
                    .HasConversion<string>(v => v.Code, v => Currency.From(v));
            });
            e.OwnsOne(x => x.PostedBalance, m =>
            {
                m.Property(p => p.Amount).HasColumnName("posted_balance").HasColumnType("numeric(18,2)");
                m.Property(p => p.Currency).HasColumnName("posted_currency").HasMaxLength(3)
                    .HasConversion<string>(v => v.Code, v => Currency.From(v));
            });
            e.OwnsOne(x => x.HoldTotal, m =>
            {
                m.Property(p => p.Amount).HasColumnName("hold_total").HasColumnType("numeric(18,2)");
                m.Property(p => p.Currency).HasColumnName("hold_currency").HasMaxLength(3)
                    .HasConversion<string>(v => v.Code, v => Currency.From(v));
            });
            e.OwnsOne(x => x.OverdraftLimit, m =>
            {
                m.Property(p => p.Amount).HasColumnName("overdraft_limit").HasColumnType("numeric(18,2)");
                m.Property(p => p.Currency).HasColumnName("overdraft_currency").HasMaxLength(3)
                    .HasConversion<string>(v => v.Code, v => Currency.From(v));
            });
            e.Ignore(x => x.DomainEvents);
            e.Ignore(x => x.RowVersion);
            e.HasIndex("CustomerId").HasDatabaseName("ix_bank_accounts_customer");
        });

        b.Entity<Hold>(e =>
        {
            e.ToTable("account_holds");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TransactionId).HasColumnName("transaction_id");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.State).HasColumnName("state");
            e.OwnsOne(x => x.Amount, m =>
            {
                m.Property(p => p.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
                m.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3)
                    .HasConversion<string>(v => v.Code, v => Currency.From(v));
            });
        });
        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.Id);
        });
        b.Entity<InboxMessage>(e =>
        {
            e.ToTable("inbox_messages");
            e.HasKey(x => x.Id);
        });
    }
}
