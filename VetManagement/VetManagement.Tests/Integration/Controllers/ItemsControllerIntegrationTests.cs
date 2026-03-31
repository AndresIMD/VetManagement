using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Infrastructure.Data;
using VetManagement.Infrastructure.Persistence;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Tests.Integration.Controllers;

public class ItemsControllerIntegrationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ItemService _service;

    public ItemsControllerIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
        _service = new ItemService(_unitOfWork);
    }

    [Fact]
    public async Task UpdateItem_WithGetByIdAsync_ShouldThrowTrackingConflict()
    {
        // Arrange - Create initial item
        var item = new Item("Original Item", ItemType.Material, "001", null, 10, 100, "Brand A")
        {
            Id = 1
        };
        await _context.Items.AddAsync(item);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear(); // Simulate new request

        // Act - Simulate PUT request pattern (WRONG WAY)
        var updatedItem = new Item("Updated Item", ItemType.Material, "001", null, 15, 120, "Brand A")
        {
            Id = 1
        };

        // This simulates what happens in controller with GetByIdAsync
        var oldData = await _service.GetByIdAsync(1); // ← EF tracks this

        // Assert - This SHOULD throw tracking exception
        var act = async () =>
        {
            _unitOfWork.Items.Update(updatedItem); // ← Conflict here
            await _unitOfWork.SaveChangesAsync();
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already being tracked*");
    }

    [Fact]
    public async Task UpdateItem_WithGetByIdAsNoTrackingAsync_ShouldSucceed()
    {
        // Arrange - Create initial item
        var item = new Item("Original Item", ItemType.Material, "002", null, 10, 100, "Brand B")
        {
            Id = 2
        };
        await _context.Items.AddAsync(item);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear(); // Simulate new request

        // Act - Simulate PUT request pattern (CORRECT WAY)
        var updatedItem = new Item("Updated Item", ItemType.Material, "002", null, 15, 120, "Brand B")
        {
            Id = 2
        };

        // This simulates correct controller pattern with AsNoTracking
        var oldData = await _service.GetByIdAsNoTrackingAsync(2); // ← No tracking

        // Assert - This should NOT throw
        var act = async () =>
        {
            await _service.UpdateAsync(updatedItem, oldData, "TestUser");
        };

        await act.Should().NotThrowAsync();

        // Verify update worked
        var updated = await _context.Items.FindAsync(2);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Item");
        updated.Stock.Should().Be(15);
    }

    [Fact]
    public async Task DeleteItem_WithGetByIdAsync_ShouldSucceed()
    {
        // Arrange - Create item to delete
        var item = new Item("Item to Delete", ItemType.Drug, "003", null, 5, 50, "Brand C")
        {
            Id = 3
        };
        await _context.Items.AddAsync(item);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act - Delete uses GetByIdAsync (with tracking) - this is OK
        var result = await _service.DeleteAsync(3, "TestUser");

        // Assert
        result.Should().BeTrue();
        var deleted = await _context.Items.FindAsync(3);
        deleted.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
