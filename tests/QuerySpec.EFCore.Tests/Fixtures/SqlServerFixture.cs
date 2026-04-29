using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace QuerySpec.EFCore.Tests.Fixtures;

[CollectionDefinition("SqlServer")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }

/// <summary>
/// Starts a SQL Server 2022 container once per test assembly run and exposes a
/// <see cref="DbContextOptions{TContext}"/> factory for the Widget entity.  The container
/// is not restarted between tests; each test creates its own <see cref="DbContext"/> instance
/// against the same connection string, which provides hermetic reads while keeping container
/// start overhead to one pay-once cost.
/// </summary>
[RequiresUnreferencedCode("Fixture creates EF Core DbContext which requires reflection metadata.")]
[RequiresDynamicCode("Fixture creates EF Core DbContext which compiles expression trees at runtime.")]
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public DbContextOptions<SqlServerWidgetContext> Options { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        Options = new DbContextOptionsBuilder<SqlServerWidgetContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        using var ctx = new SqlServerWidgetContext(Options);
        await ctx.Database.EnsureCreatedAsync();
        ctx.Widgets.AddRange(
            new SqlServerWidget { Id = 1, Name = "Alpha",       Quantity = 10, Price = 99.50m, CreatedAt = new DateTime(2024, 1, 15) },
            new SqlServerWidget { Id = 2, Name = "Beta",        Quantity = 5,  Price = null,   CreatedAt = new DateTime(2024, 6, 1)  },
            new SqlServerWidget { Id = 3, Name = "alphabet",    Quantity = 25, Price = 49.99m, CreatedAt = new DateTime(2024, 9, 20) },
            new SqlServerWidget { Id = 4, Name = "Gamma",       Quantity = 0,  Price = 0m,     CreatedAt = new DateTime(2023, 12, 31) },
            new SqlServerWidget { Id = 5, Name = "50%_off",     Quantity = 3,  Price = 10.00m, CreatedAt = new DateTime(2024, 3, 1)  },
            new SqlServerWidget { Id = 6, Name = "[Special]",   Quantity = 1,  Price = 5.00m,  CreatedAt = new DateTime(2024, 4, 15) },
            new SqlServerWidget { Id = 7, Name = "Under_score", Quantity = 2,  Price = 8.00m,  CreatedAt = new DateTime(2024, 5, 10) });
        await ctx.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[RequiresUnreferencedCode("EF Core DbContext is not fully compatible with trimming.")]
[RequiresDynamicCode("EF Core DbContext is not fully compatible with NativeAOT.")]
public sealed class SqlServerWidgetContext : DbContext
{
    public Microsoft.EntityFrameworkCore.DbSet<SqlServerWidget> Widgets => Set<SqlServerWidget>();

    public SqlServerWidgetContext(DbContextOptions<SqlServerWidgetContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SqlServerWidget>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Price).HasPrecision(18, 4);
        });
    }
}

public sealed class SqlServerWidget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal? Price { get; set; }
    public DateTime CreatedAt { get; set; }
}
