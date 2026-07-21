using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PantryService.Data;
using PantryService.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace PantryService.Tests.Repositories;

// Requires a Docker daemon reachable from the test host; Testcontainers spins up
// a throwaway Postgres per run instead of relying on a shared/mocked database.
public class PantryItemRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private PantryDbContext _context = null!;
    private PantryItemRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<PantryDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new PantryDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new PantryItemRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task UpsertAsync_SecondCallForSameIngredient_UpdatesRatherThanDuplicates()
    {
        await _repository.UpsertAsync(userId: 1, ingredientId: 7, quantity: 100, unit: "g", expiryDate: null);
        await _repository.UpsertAsync(userId: 1, ingredientId: 7, quantity: 250, unit: "g", expiryDate: null);

        var items = await _repository.GetForUserAsync(1);

        items.Should().ContainSingle();
        items[0].Quantity.Should().Be(250);
    }

    [Fact]
    public async Task DeleteAsync_ForDifferentUser_ReturnsFalseAndLeavesItemIntact()
    {
        var created = await _repository.UpsertAsync(userId: 1, ingredientId: 7, quantity: 100, unit: "g", expiryDate: null);

        var deleted = await _repository.DeleteAsync(userId: 2, itemId: created.Id);

        deleted.Should().BeFalse();
        (await _repository.GetByIdAsync(created.Id)).Should().NotBeNull();
    }
}
