using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.BuildingBlocks.Infrastructure.Outbox;
using SmartBank.Cards.Application.Cards;
using SmartBank.Cards.Domain;

namespace SmartBank.Cards.Infrastructure.Persistence;

public sealed class CardsDbContext : DbContext
{
    public CardsDbContext(DbContextOptions<CardsDbContext> options) : base(options) { }
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Card>(e =>
        {
            e.ToTable("cards");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Type).HasColumnName("card_type");
            e.Property(x => x.Brand).HasColumnName("card_brand");
            e.Property(x => x.LastFour).HasColumnName("last_four");
            e.Property(x => x.PanToken).HasColumnName("pan_token");
            e.Property(x => x.ExpiryMonth).HasColumnName("expiry_month");
            e.Property(x => x.ExpiryYear).HasColumnName("expiry_year");
            e.Property(x => x.CvvEnc).HasColumnName("cvv_enc");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.IssuedAt).HasColumnName("issued_at");
            e.Property(x => x.Currency).HasColumnName("currency");
            e.OwnsOne(x => x.DailyEcommerceLimit, m => m.Property(p => p.Amount).HasColumnName("ecom_limit").HasColumnType("numeric(18,2)"));
            e.OwnsOne(x => x.DailyAtmLimit, m => m.Property(p => p.Amount).HasColumnName("atm_limit").HasColumnType("numeric(18,2)"));
            // One debit + one credit card per account.
            e.HasIndex(x => new { x.AccountId, x.Type }).IsUnique().HasDatabaseName("ux_cards_account_type");
            e.HasIndex(x => x.CustomerId).HasDatabaseName("ix_cards_customer");
            e.Ignore(x => x.DomainEvents);
        });
        b.Entity<OutboxMessage>(e => { e.ToTable("outbox_messages"); e.HasKey(x => x.Id); });
        b.Entity<InboxMessage>(e => { e.ToTable("inbox_messages"); e.HasKey(x => x.Id); });
    }
}

public sealed class EfCardRepository : ICardRepository
{
    private readonly CardsDbContext _db;
    public EfCardRepository(CardsDbContext db) => _db = db;
    public Task<Card?> GetByIdAsync(Guid id, CancellationToken ct) => _db.Cards.FirstOrDefaultAsync(c => c.Id == id, ct)!;
    public Task<List<Card>> ListByCustomerAsync(Guid customerId, CancellationToken ct) => _db.Cards.Where(c => c.CustomerId == customerId).ToListAsync(ct);
    public Task<Card?> GetByAccountAsync(Guid accountId, CancellationToken ct) => _db.Cards.FirstOrDefaultAsync(c => c.AccountId == accountId, ct)!;
    public Task AddAsync(Card card, CancellationToken ct) => _db.Cards.AddAsync(card, ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

public sealed class EnvPanProtector : IPanProtector
{
    private readonly byte[] _hmacKey;
    private readonly byte[] _aesKey;

    public EnvPanProtector(IConfiguration config)
    {
        var hmac = config["CARD_HMAC_KEY"] ?? "dev-card-hmac-key-32-bytes-long!";
        var aes = config["CARD_AES_KEY"] ?? "dev-card-aes-key-32-bytes-long!!";
        _hmacKey = Encoding.UTF8.GetBytes(hmac.PadRight(32)[..32]);
        _aesKey = Encoding.UTF8.GetBytes(aes.PadRight(32)[..32]);
    }

    public string Tokenize(string pan)
    {
        var bytes = HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(pan));
        return Convert.ToHexString(bytes);
    }

    public string EncryptCvv(string cvv)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(cvv);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_aesKey, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(cipher).Concat(tag).ToArray());
    }

    public string DecryptCvv(string cipher)
    {
        var bytes = Convert.FromBase64String(cipher);
        var nonce = bytes[..12];
        var tag = bytes[^16..];
        var ct = bytes[12..^16];
        var plain = new byte[ct.Length];
        using var aes = new AesGcm(_aesKey, 16);
        aes.Decrypt(nonce, ct, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
