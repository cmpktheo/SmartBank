using Microsoft.EntityFrameworkCore;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.BuildingBlocks.Infrastructure.Outbox;
using SmartBank.Ledger.Domain;

namespace SmartBank.Ledger.Infrastructure.Persistence;

public sealed class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }

    public DbSet<JournalTransaction> Journals => Set<JournalTransaction>();
    public DbSet<LedgerLine> Lines => Set<LedgerLine>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<JournalTransaction>(e =>
        {
            e.ToTable("journal_transactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Reference).HasColumnName("reference");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3);
            e.Property(x => x.BookedAt).HasColumnName("booked_at");
            e.Property(x => x.ReversesTransactionId).HasColumnName("reverses_transaction_id");
            e.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
            e.Property(x => x.SourceAccountId).HasColumnName("source_account_id");
            e.Property(x => x.DestinationAccountId).HasColumnName("destination_account_id");
            e.Property(x => x.CounterpartyIban).HasColumnName("counterparty_iban");
            e.Property(x => x.Narrative).HasColumnName("narrative");
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.TransactionId);
            e.HasIndex(x => x.Reference).IsUnique();
            e.Ignore(x => x.DomainEvents);
        });
        b.Entity<LedgerLine>(e =>
        {
            e.ToTable("ledger_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TransactionId).HasColumnName("transaction_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Direction).HasColumnName("direction");
            e.Property(x => x.BookedAt).HasColumnName("booked_at");
            e.OwnsOne(x => x.Amount, m =>
            {
                m.Property(p => p.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
                m.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3)
                    .HasConversion<string>(v => v.Code, v => Currency.From(v));
            });
            e.HasIndex(x => new { x.AccountId, x.BookedAt }).HasDatabaseName("ix_ledger_lines_account_booked");
        });
        b.Entity<OutboxMessage>(e => { e.ToTable("outbox_messages"); e.HasKey(x => x.Id); });
    }
}
