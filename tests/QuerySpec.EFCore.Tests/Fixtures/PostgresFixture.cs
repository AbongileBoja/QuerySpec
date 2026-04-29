using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace QuerySpec.EFCore.Tests.Fixtures;

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> { }

/// <summary>
/// Starts a PostgreSQL 16 container once per test assembly run and exposes a
/// <see cref="DbContextOptions{TContext}"/> factory for the Widget entity.  The container
/// is not restarted between tests; each test creates its own <see cref="DbContext"/> instance
/// keeping container start overhead to one pay-once cost.
/// </summary>
[RequiresUnreferencedCode("Fixture creates EF Core DbContext which requires reflection metadata.")]
[RequiresDynamicCode("Fixture creates EF Core DbContext which compiles expression trees at runtime.")]
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public DbContextOptions<PostgresWidgetContext> Options { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        Options = new DbContextOptionsBuilder<PostgresWidgetContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        using var ctx = new PostgresWidgetContext(Options);
        await ctx.Database.EnsureCreatedAsync();
        ctx.Widgets.AddRange(
            new PostgresWidget { Id = 1, Name = "Alpha",    Quantity = 10, Price = 99.50m, CreatedAt = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
            new PostgresWidget { Id = 2, Name = "Beta",     Quantity = 5,  Price = null,   CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)  },
            new PostgresWidget { Id = 3, Name = "alphabet", Quantity = 25, Price = 49.99m, CreatedAt = new DateTime(2024, 9, 20, 0, 0, 0, DateTimeKind.Utc) },
            new PostgresWidget { Id = 4, Name = "Gamma",    Quantity = 0,  Price = 0m,     CreatedAt = new DateTime(2023, 12, 31, 0, 0, 0, DateTimeKind.Utc) });
        await ctx.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[RequiresUnreferencedCode("EF Core DbContext is not fully compatible with trimming.")]
[RequiresDynamicCode("EF Core DbContext is not fully compatible with NativeAOT.")]
public sealed class PostgresWidgetContext : DbContext
{
    public Microsoft.EntityFrameworkCore.DbSet<PostgresWidget> Widgets => Set<PostgresWidget>();

    public PostgresWidgetContext(DbContextOptions<PostgresWidgetContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PostgresWidget>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Price).HasPrecision(18, 4);
        });
    }
}

public sealed class PostgresWidget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal? Price { get; set; }
    public DateTime CreatedAt { get; set; }
}
