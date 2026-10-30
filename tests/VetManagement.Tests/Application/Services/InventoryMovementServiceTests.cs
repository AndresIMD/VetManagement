using FluentAssertions;
using Moq;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Application.Inventory;
using VetManagement.Application.Services;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Enums;
using VetManagement.Contracts.Inventory;
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
    private readonly Mock<IRepository<Warehouse>> _mockWarehouseRepo;
    private readonly Mock<IWarehouseStockRepository> _mockWarehouseStockRepo;
    private readonly InventoryMovementService _service;

    public InventoryMovementServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockNotificationService = new Mock<IRealtimeNotificationService>();
        _mockMovementRepo = new Mock<IInventoryMovementRepository>();
        _mockItemRepo = new Mock<IItemRepository>();
        _mockAuditRepo = new Mock<IAuditLogRepository>();
        _mockWarehouseRepo = new Mock<IRepository<Warehouse>>();
        _mockWarehouseStockRepo = new Mock<IWarehouseStockRepository>();

        _mockUnitOfWork.Setup(u => u.InventoryMovements).Returns(_mockMovementRepo.Object);
        _mockUnitOfWork.Setup(u => u.Items).Returns(_mockItemRepo.Object);
        _mockUnitOfWork.Setup(u => u.AuditLogs).Returns(_mockAuditRepo.Object);
        _mockUnitOfWork.Setup(u => u.Warehouses).Returns(_mockWarehouseRepo.Object);
        _mockUnitOfWork.Setup(u => u.WarehouseStocks).Returns(_mockWarehouseStockRepo.Object);
        // Only the daily-use warehouse, which holds every unit.
        _mockWarehouseRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Warehouse, bool>>>()))
            .ReturnsAsync([new Warehouse { Id = 1, Name = "Daily", IsDailyUse = true }]);
        _mockWarehouseStockRepo.Setup(r => r.ForItemsAsync(It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync([]);
        // Stock changes run under the shared stock lock; here the work simply runs.
        _mockUnitOfWork.Setup(u => u.ExecuteExclusiveAsync(It.IsAny<string>(), It.IsAny<Func<Task<StockMoveResult>>>()))
            .Returns((string _, Func<Task<StockMoveResult>> work) => work());

        _service = new InventoryMovementService(_mockUnitOfWork.Object, _mockNotificationService.Object);
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
        var result = await _service.AdjustStockAsync(itemId, amount, reason, userName, StockScope.All);

        // Assert
        result.Status.Should().Be(StockMoveStatus.Done);
        item.Stock.Should().Be(15);

        _mockItemRepo.Verify(r => r.Update(item), Times.Once);
        _mockMovementRepo.Verify(r => r.AddAsync(It.Is<DomainInventoryMovement>(m =>
            m.ItemId == itemId &&
            m.Quantity == 5 &&
            m.Type == DomainInventoryMovementType.Ingress &&
            m.WarehouseId == null &&
            m.Reason == reason)), Times.Once);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));
        // Every connected user gets the notice: it names the item, never the new count.
        _mockNotificationService.Verify(n => n.NotifyPropertyChangedAsync<DomainItem>(itemId, nameof(DomainItem.Stock), null), Times.Once);
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
        await _service.AdjustStockAsync(itemId, amount, "Reason", "User", StockScope.All);

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
        var result = await _service.AdjustStockAsync(1, 5, "Reason", "User", StockScope.All);

        // Assert
        result.Status.Should().Be(StockMoveStatus.NotFound);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AdjustStockAsync_BelowZero_IsRefused()
    {
        var item = new DomainItem("Test Item", DomainItemType.Material, "123", null, 10, 100, "Brand") { Id = 1, Stock = 2 };
        _mockItemRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(item);

        var result = await _service.AdjustStockAsync(1, -3, "Count", "User", StockScope.All);

        result.Status.Should().Be(StockMoveStatus.Invalid);
        item.Stock.Should().Be(2);
        _mockMovementRepo.Verify(r => r.AddAsync(It.IsAny<DomainInventoryMovement>()), Times.Never);
    }

    [Fact]
    public async Task MassStockUpdateAsync_ShouldUpdateMultipleItems()
    {
        // Arrange
        var updates = new List<MassStockUpdateRequest>
        {
            new() { ItemId = 1, Quantity = 5 },
            new() { ItemId = 2, Quantity = 3 }
        };

        var item1 = new DomainItem("Item 1", DomainItemType.Material, "111", null, 10, 100, "Brand") { Id = 1, Stock = 10 };
        var item2 = new DomainItem("Item 2", DomainItemType.Material, "222", null, 10, 100, "Brand") { Id = 2, Stock = 10 };

        _mockItemRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(item1);
        _mockItemRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(item2);

        // Act
        var result = await _service.MassStockUpdateAsync(updates, isIngress: true, "User", StockScope.All);

        // Assert
        result.Count.Should().Be(2);
        item1.Stock.Should().Be(15);
        item2.Stock.Should().Be(13);

        _mockMovementRepo.Verify(r => r.AddAsync(It.IsAny<DomainInventoryMovement>()), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Exactly(3)); // Once per item (audit) + once at the end

        _mockNotificationService.Verify(n => n.NotifyCollectionChangedAsync<DomainInventoryMovement>("Add"), Times.Once);
    }
}
