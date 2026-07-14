using InvoiceSoftware.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace InvoiceSoftware.Data;

public sealed class InvoiceDbContext(DbContextOptions<InvoiceDbContext> options) : DbContext(options)
{
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptPayment> ReceiptPayments => Set<ReceiptPayment>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<ApplicationSettings> ApplicationSettings => Set<ApplicationSettings>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BackupRecord> Backups => Set<BackupRecord>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entity.ClrType))
            {
                modelBuilder.Entity(entity.ClrType).HasQueryFilter(CreateSoftDeleteFilter(entity.ClrType));
            }
        }

        modelBuilder.Entity<Customer>().HasIndex(x => x.CustomerCode).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Barcode);
        modelBuilder.Entity<Product>().HasIndex(x => x.Name);
        modelBuilder.Entity<Product>().HasIndex(x => x.Category);
        modelBuilder.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Supplier>().HasIndex(x => x.CompanyName);
        modelBuilder.Entity<StockMovement>().HasIndex(x => new { x.ProductId, x.TransactionDate });
        modelBuilder.Entity<StockMovement>().HasIndex(x => x.RelatedInvoiceId);
        modelBuilder.Entity<Invoice>().HasIndex(x => x.ReferenceNumber).IsUnique();
        modelBuilder.Entity<Receipt>().HasIndex(x => x.ReferenceNumber).IsUnique();
        modelBuilder.Entity<DocumentSequence>().HasIndex(x => new { x.Kind, x.Prefix, x.Year }).IsUnique();
        modelBuilder.Entity<ApplicationSettings>().HasIndex(x => x.Key).IsUnique();
        modelBuilder.Entity<User>().HasIndex(x => x.UserName).IsUnique();

        modelBuilder.Entity<Invoice>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Invoice)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Product>()
            .HasOne(x => x.CategoryRecord)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Product>()
            .HasOne(x => x.Supplier)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InvoiceItem>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockMovement>()
            .HasOne(x => x.Product)
            .WithMany(x => x.StockMovements)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockMovement>()
            .HasOne(x => x.RelatedInvoice)
            .WithMany()
            .HasForeignKey(x => x.RelatedInvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Receipt>()
            .HasOne(x => x.Invoice)
            .WithMany(x => x.Receipts)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        ConfigureMoney(modelBuilder);
        Seed(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampEntities();
        return base.SaveChanges();
    }

    private void StampEntities()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
            if (entry.State == EntityState.Modified) entry.Entity.ModifiedAt = now;
        }
    }

    private static void ConfigureMoney(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(4);
        }
    }

    private static LambdaExpression CreateSoftDeleteFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
        var condition = Expression.Equal(property, Expression.Constant(false));
        return Expression.Lambda(condition, parameter);
    }

    private static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompanySettings>().HasData(new CompanySettings
        {
            Id = 1,
            CompanyName = "MicroBrain Embedded System Solutions",
            ContactPersonName = "Fadi Assi",
            LogoPath = @"C:\Users\Fady\Documents\Logo\Logo.png",
            Address = "Street Address, City",
            Telephone = "000-000-0000",
            Email = "info@example.com",
            Website = "www.example.com",
            Currency = "USD",
            CurrencySymbol = "$",
            DefaultTaxPercentage = 6.25m,
            InvoicePrefix = "INV",
            ReceiptPrefix = "REC",
            PaymentTerms = "Total payment due in 30 days.",
            InvoiceFooterNotes = "Thank you for your business."
        });

        modelBuilder.Entity<Customer>().HasData(
            new Customer { Id = 1, CustomerCode = "CUS-0001", Name = "Acme Manufacturing", AttentionName = "Accounts Payable", Address = "12 Market Street", Telephone = "555-0100", Email = "ap@acme.test", TaxNumber = "TX-100", OpeningBalance = 0 },
            new Customer { Id = 2, CustomerCode = "CUS-0002", Name = "Northwind Services", AttentionName = "Billing Department", Address = "88 Service Avenue", Telephone = "555-0199", Email = "billing@northwind.test", TaxNumber = "TX-200", OpeningBalance = 120 });

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Code = "SRV-001", Name = "Embedded software consulting", Description = "Embedded software consulting", Type = ProductType.Service, Unit = "hr", SellingPrice = 75m, TaxPercentage = 6.25m, TrackStock = false },
            new Product { Id = 2, Code = "PRT-001", Name = "Controller board", Description = "Controller board", Type = ProductType.Product, Unit = "ea", SellingPrice = 345m, CostPrice = 210m, TaxPercentage = 6.25m, CurrentQuantity = 25, MinimumQuantity = 5, TrackStock = true });

        modelBuilder.Entity<PaymentMethod>().HasData(
            new PaymentMethod { Id = 1, Name = "Cash", MethodType = PaymentMethodType.Cash },
            new PaymentMethod { Id = 2, Name = "Bank transfer", MethodType = PaymentMethodType.BankTransfer },
            new PaymentMethod { Id = 3, Name = "Credit card", MethodType = PaymentMethodType.CreditCard });

        modelBuilder.Entity<Category>().HasData(new Category { Id = 1, Name = "Services" }, new Category { Id = 2, Name = "Hardware" });
        modelBuilder.Entity<Unit>().HasData(new Unit { Id = 1, Name = "Each", Abbreviation = "ea" }, new Unit { Id = 2, Name = "Hour", Abbreviation = "hr" });
    }
}
