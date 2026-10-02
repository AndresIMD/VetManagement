using FluentAssertions;
using Moq;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Application.Services;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Enums;
using VetManagement.Shared.Models.DTOs;
using DomainInventoryMovementType = VetManagement.Domain.Enums.InventoryMovementType;
using DomainItemType = VetManagement.Domain.Enums.ItemType;
using DomainItem = VetManagement.Domain.Inventory.Item;
using DomainInventoryMovement = VetManagement.Domain.Inventory.InventoryMovement;

namespace VetManagement.Tests.Application.Services;

public class InventoryMovementServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IRealtimeNotificationService> _mockNotificationService;
    private readonly Mock<IInventoryMovementRepository> _mockMovementRepo;
    private readonly Mock<IItemRepository> _mockItemRepo;
    private readonly Mock<IAuditLogRepository> _mockAuditRepo;
    private readonly InventoryMovementService _service;

    public InventoryMovementServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockNotificationService = new Mock<IRealtimeNotificationService>();
        _mockMovementRepo = new Mock<IInventoryMovementRepository>();
        _mockItemRepo = new Mock<IItemRepository>();
        _mockAuditRepo = new Mock<IAuditLogRepository>();

        _mockUnitOfWork.Setup(u => u.InventoryMovements).Returns(_mockMovementRepo.Object);
        _mockUnitOfWork.Setup(u => u.Items).Returns(_mockItemRepo.Object);
        _mockUnitOfWork.Setup(u => u.AuditLogs).Returns(_mockAuditRepo.Object);

        _service = new InventoryMovementService(_mockUnitOfWork.Object, _mockNotificationService.Object);
    }

    [Fact]
    public async Task GetAllMovementsAsync_ShouldReturnAllMovements()
    {
        // Arrange
        var movements = new List<DomainInventoryMovement>
        {
            new DomainInventoryMovement { Id = 1, ItemId = 1, Quantity = 10, Type = DomainInventoryMovementType.Ingress },
            new DomainInventoryMovement { Id = 2, ItemId = 1, Quantity = 5, Type = DomainInventoryMovementType.Egress }
        };

        _mockMovementRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(movements);

        // Act
        var result = await _service.GetAllMovementsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(movements);
    }

    [Fact]
    public async Task AddMovementAsync_ShouldAddMovement_LogAudit_AndNotify()
    {
        // Arrange
        var movement = new DomainInventoryMovement
        {
            ItemId = 1,
            Quantity = 10,
            Type = DomainInventoryMovementType.Ingress
        };
        string userName = "TestUser";

        // Act
        await _service.AddMovementAsync(movement, userName);

        // Assert
        _mockMovementRepo.Verify(r => r.AddAsync(movement), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Exactly(2)); // Once for movement, once for audit
        _mockAuditRepo.Verify(r => r.AddAsync(It.Is<Shared.Models.Audit.AuditLog>(l =>
            l.EntityName == nameof(VetManagement.Domain.Inventory.InventoryMovement) &&
            l.Action == AuditActionType.Ingress &&
            l.User == userName)), Times.Once);

        _mockNotificationService.Verify(n => n.NotifyEntityChangedAsync<DomainInventoryMovement>(movement.Id, "Add", It.IsAny<object>()), Times.Once);
        _mockNotificationService.Verify(n => n.NotifyCollectionChangedAsync<DomainInventoryMovement>("Add"), Times.Once);
    }

    [Fact]
    public async Task AdjustStockAsync_WithValidItem_ShouldUpdateStock_AddMovement_AndNotify()
    {
        // Arrange
        int itemId = 1;
        int amount = 5;
        string reason = "Correction";
        string userName = "TestUser";
        var item = new DomainItem("Test Item", DomainItemType.Material, "123", null, 10, 100, "Brand") { Id = itemId, Stock = 10 };

        _mockItemRepo.Setup(r => r.GetByIdAsync(itemId)).ReturnsAsync(item);

        // Act
        var result = await _service.AdjustStockAsync(itemId, amount, reason, userName);

        // Assert
        result.Should().BeTrue();
        item.Stock.Should().Be(15);

        _mockItemRepo.Verify(r => r.Update(item), Times.Once);
        _mockMovementRepo.Verify(r => r.AddAsync(It.Is<DomainInventoryMovement>(m =>
            m.ItemId == itemId &&
            m.Quantity == 5 &&
            m.Type == DomainInventoryMovementType.Ingress &&
            m.Reason == reason)), Times.Once);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));
        _mockNotificationService.Verify(n => n.NotifyPropertyChangedAsync<DomainItem>(itemId, nameof(DomainItem.Stock), 15), Times.Once);
    }

    [Fact]
    public async Task AdjustStockAsync_WithNegativeAmount_ShouldDecreaseStock()
    {
        // Arrange
        int itemId = 1;
        int amount = -3;
        var item = new DomainItem("Test Item", DomainItemType.Material, "123", null, 10, 100, "Brand") { Id = itemId, Stock = 10 };

        _mockItemRepo.Setup(r => r.GetByIdAsync(itemId)).ReturnsAsync(item);

        // Act
        await _service.AdjustStockAsync(itemId, amount, "Reason", "User");

        // Assert
        item.Stock.Should().Be(7);
        _mockMovementRepo.Verify(r => r.AddAsync(It.Is<DomainInventoryMovement>(m =>
            m.Type == DomainInventoryMovementType.Egress &&
            m.Quantity == 3)), Times.Once);
    }

    [Fact]
    public async Task AdjustStockAsync_WithInvalidItem_ShouldReturnFalse()
    {
        // Arrange
        _mockItemRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((DomainItem?)null);

        // Act
        var result = await _service.AdjustStockAsync(1, 5, "Reason", "User");

        // Assert
        result.Should().BeFalse();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task MassStockUpdateAsync_ShouldUpdateMultipleItems()
    {
        // Arrange
        var updates = new List<InventoryMassUpdateDTO>
        {
            new() { ItemId = 1, Quantity = 5 },
            new() { ItemId = 2, Quantity = 3 }
        };

        var item1 = new DomainItem("Item 1", DomainItemType.Material, "111", null, 10, 100, "Brand") { Id = 1, Stock = 10 };
        var item2 = new DomainItem("Item 2", DomainItemType.Material, "222", null, 10, 100, "Brand") { Id = 2, Stock = 10 };

        _mockItemRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(item1);
        _mockItemRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(item2);

        // Act
        var count = await _service.MassStockUpdateAsync(updates, isIngress: true, "User");

        // Assert
        count.Should().Be(2);
        item1.Stock.Should().Be(15);
        item2.Stock.Should().Be(13);

        _mockMovementRepo.Verify(r => r.AddAsync(It.IsAny<DomainInventoryMovement>()), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Exactly(3)); // Once per item (audit) + once at the end

        _mockNotificationService.Verify(n => n.NotifyCollectionChangedAsync<DomainInventoryMovement>("MassUpdate"), Times.Once);
    }
}
