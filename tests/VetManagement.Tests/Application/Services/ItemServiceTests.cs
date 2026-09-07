using FluentAssertions;
using Moq;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Application.Services;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Tests.Application.Services;

public class ItemServiceTests
{
    private Mock<IUnitOfWork> CreateMockUnitOfWork()
    {
        var mock = new Mock<IUnitOfWork>();

        // Mock items repository
        var itemsRepoMock = new Mock<IItemRepository>();
        itemsRepoMock.Setup(r => r.AddAsync(It.IsAny<Item>())).Returns(Task.CompletedTask);
        itemsRepoMock.Setup(r => r.Remove(It.IsAny<Item>()));
        itemsRepoMock.Setup(r => r.Update(It.IsAny<Item>()));
        mock.Setup(uow => uow.Items).Returns(itemsRepoMock.Object);

        // Mock AuditLogs repository
        var auditRepoMock = new Mock<IAuditLogRepository>();
        auditRepoMock.Setup(r => r.AddAsync(It.IsAny<Shared.Models.Audit.AuditLog>())).Returns(Task.CompletedTask);
        mock.Setup(uow => uow.AuditLogs).Returns(auditRepoMock.Object);

        // SaveChangesAsync should return Task<int> (number of changes saved)
        mock.Setup(uow => uow.SaveChangesAsync())
     .ReturnsAsync(1); // Returns 1 change saved

        return mock;
    }

    // TEST #1: Make sure GetAllAsync returns all items
    [Fact]
    public async Task GetAllAsync_ShouldReturnAllItems()
    {
        // ============ ARRANGE ============
        // Create test data
        var expectedItems = new List<Item>
        {
   new("Item 1", ItemType.Material, "001", null, 10, 100, "Brand A"),
       new("Item 2", ItemType.Drug, "002", null, 5, 200, "Brand B")
        };

        // Create mocks
        var mockUnitOfWork = CreateMockUnitOfWork();

        // Setup mock behavior
        // When Items.GetAllAsync() is called, return expectedItems
        mockUnitOfWork.Setup(uow => uow.Items.GetAllAsync())
            .ReturnsAsync(expectedItems);

        // Create the service with the mock
        var service = new ItemService(mockUnitOfWork.Object);

        // ============ ACT ============
        // Execute the method we want to test
        var result = await service.GetAllAsync();

        // ============ ASSERT ============
        // Verify the result is correct

        // FluentAssertions makes assertions more readable:
        result.Should().NotBeNull(); // The result should not be null
        result.Should().HaveCount(2); // It should have 2 elements
        result.Should().BeEquivalentTo(expectedItems); // It should be equivalent to the expected items

        // Verify that the correct method on the mock was called
        mockUnitOfWork.Verify(uow => uow.Items.GetAllAsync(), Times.Once);
    }

    // TEST #2: Verify that GetByIdAsync returns the correct item
    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnItem()
    {
        // ARRANGE
        var expectedItem = new Item("Test Item", ItemType.Material, "123", null, 10, 100, "Test Brand")
        {
            Id = 1
        };

        var mockUnitOfWork = CreateMockUnitOfWork();
        mockUnitOfWork.Setup(uow => uow.Items.GetByIdAsync(1))
          .ReturnsAsync(expectedItem);

        var service = new ItemService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetByIdAsync(1);

        // ASSERT
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedItem);
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Test Item");
    }

    // TEST #3:  Verify that GetByIdAsync returns null if not found
    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // ARRANGE
        var mockUnitOfWork = CreateMockUnitOfWork();
        mockUnitOfWork.Setup(uow => uow.Items.GetByIdAsync(999))
        .ReturnsAsync((Item?)null); // Return null

        var service = new ItemService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetByIdAsync(999);

        // ASSERT
        result.Should().BeNull();
    }

    // TEST #4: Verify that AddAsync saves correctly
    [Fact]
    public async Task AddAsync_WithValidItem_ShouldSaveAndReturnTrue()
    {
        // ARRANGE
        var newItem = new Item("New Item", ItemType.Material, "456", null, 20, 150, "Brand C");
        var userName = "TestUser";

        var mockUnitOfWork = CreateMockUnitOfWork();
        var service = new ItemService(mockUnitOfWork.Object);

        // ACT
        var result = await service.AddAsync(newItem, userName);

        // ASSERT
        result.Should().BeTrue();

        // Verify that the correct methods were called
        mockUnitOfWork.Verify(uow => uow.Items.AddAsync(newItem), Times.Once);
        mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(), Times.Exactly(2)); // Una vez para item, otra para audit
        mockUnitOfWork.Verify(uow => uow.AuditLogs.AddAsync(It.IsAny<Shared.Models.Audit.AuditLog>()), Times.Once);
    }

    // TEST #5: Verify that AddAsync rejects invalid items
    [Theory] // Theory allows testing multiple cases
    [InlineData(null, ItemType.Material)] // Item with null name
    [InlineData("", ItemType.Material)]   // Item with empty name
    [InlineData("Valid Name", ItemType.None)] // Item with None type
    public async Task AddAsync_WithInvalidItem_ShouldReturnFalse(string? itemName, ItemType itemType)
    {
        // ARRANGE
        Item invalidItem;
        if (itemName == null)
        {
            invalidItem = null!;
        }
        else
        {
            invalidItem = new Item(itemName, itemType, "789", null, 10, 100, "Brand");
        }

        var userName = "TestUser";

        var mockUnitOfWork = CreateMockUnitOfWork();
        var service = new ItemService(mockUnitOfWork.Object);

        // ACT
        var result = await service.AddAsync(invalidItem, userName);

        // ASSERT
        result.Should().BeFalse();

        // Verify that nothing was saved
        mockUnitOfWork.Verify(uow => uow.Items.AddAsync(It.IsAny<Item>()), Times.Never);
        mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(), Times.Never);
    }

    // TEST #6: Verify that DeleteAsync deletes correctly
    [Fact]
    public async Task DeleteAsync_WithExistingItem_ShouldDeleteAndReturnTrue()
    {
        // ARRANGE
        var existingItem = new Item("Item to Delete", ItemType.Material, "999", null, 5, 50, "Brand")
        {
            Id = 10
        };
        var userName = "TestUser";

        var mockUnitOfWork = CreateMockUnitOfWork();
        mockUnitOfWork.Setup(uow => uow.Items.GetByIdAsync(10))
       .ReturnsAsync(existingItem);

        var service = new ItemService(mockUnitOfWork.Object);

        // ACT
        var result = await service.DeleteAsync(10, userName);

        // ASSERT
        result.Should().BeTrue();

        // Verificar que se eliminó
        mockUnitOfWork.Verify(uow => uow.Items.Remove(existingItem), Times.Once);
        mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(), Times.Exactly(2));
    }

    // TEST #7: Verify that DeleteAsync returns false if the item does not exist
    [Fact]
    public async Task DeleteAsync_WithNonExistingItem_ShouldReturnFalse()
    {
        // ARRANGE
        var mockUnitOfWork = CreateMockUnitOfWork();
        mockUnitOfWork.Setup(uow => uow.Items.GetByIdAsync(999))
     .ReturnsAsync((Item?)null);

        var service = new ItemService(mockUnitOfWork.Object);

        // ACT
        var result = await service.DeleteAsync(999, "TestUser");

        // ASSERT
        result.Should().BeFalse();
        mockUnitOfWork.Verify(uow => uow.Items.Remove(It.IsAny<Item>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithChangedStock_ShouldPreservePersistedStock()
    {
        // An item edit updates catalog data only; stock is changed through an inventory movement.
        var persistedItem = new Item("Existing item", ItemType.Material, "123", null, 12, 100, "Brand") { Id = 7 };
        var editedItem = new Item("Renamed item", ItemType.Material, "123", null, 999, 100, "Brand") { Id = 7 };
        var mockUnitOfWork = CreateMockUnitOfWork();
        var service = new ItemService(mockUnitOfWork.Object);

        var result = await service.UpdateAsync(editedItem, persistedItem, "TestUser");

        result.Should().BeTrue();
        editedItem.Stock.Should().Be(12);
        mockUnitOfWork.Verify(uow => uow.Items.Update(editedItem), Times.Once);
    }
}
