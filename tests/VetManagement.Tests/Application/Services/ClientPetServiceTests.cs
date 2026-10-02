using FluentAssertions;
using Moq;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Clients;

namespace VetManagement.Tests.Application.Services;

public class ClientPetServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IClientRepository> _mockClientRepo;
    private readonly Mock<IPetRepository> _mockPetRepo;
    private readonly Mock<IAuditLogRepository> _mockAuditRepo;
    private readonly Mock<IRealtimeNotificationService> _mockNotificationService;
    private readonly ClientPetService _service;

    public ClientPetServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockClientRepo = new Mock<IClientRepository>();
        _mockPetRepo = new Mock<IPetRepository>();
        _mockAuditRepo = new Mock<IAuditLogRepository>();
        _mockNotificationService = new Mock<IRealtimeNotificationService>();

        _mockUnitOfWork.Setup(u => u.Clients).Returns(_mockClientRepo.Object);
        _mockUnitOfWork.Setup(u => u.Pets).Returns(_mockPetRepo.Object);
        _mockUnitOfWork.Setup(u => u.AuditLogs).Returns(_mockAuditRepo.Object);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Default setups
        _mockClientRepo.Setup(r => r.AddAsync(It.IsAny<Client>())).Returns(Task.CompletedTask);
        _mockPetRepo.Setup(r => r.AddAsync(It.IsAny<Pet>())).Returns(Task.CompletedTask);
        _mockAuditRepo.Setup(r => r.AddAsync(It.IsAny<Domain.Audit.AuditLog>())).Returns(Task.CompletedTask);

        _service = new ClientPetService(_mockUnitOfWork.Object, _mockNotificationService.Object);
    }

    #region Client Tests

    [Fact]
    public async Task AddClientAsync_ShouldAddClient_LogAudit_AndNotify()
    {
        // Arrange
        var client = new Client { Id = 1, Name = "John", LastName = "Doe", TaxId = "12345678-9" };
        string userName = "TestUser";

        // Act
        await _service.AddClientAsync(client, userName);

        // Assert
        _mockClientRepo.Verify(r => r.AddAsync(client), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Exactly(2)); // Client + Audit

        _mockAuditRepo.Verify(r => r.AddAsync(It.Is<Domain.Audit.AuditLog>(l =>
            l.EntityName == nameof(Client) &&
            l.Action == AuditActionType.Add &&
            l.User == userName)), Times.Once);

        _mockNotificationService.Verify(n => n.NotifyEntityChangedAsync<Client>(client.Id, "Add", It.IsAny<object>()), Times.Once);
        _mockNotificationService.Verify(n => n.NotifyCollectionChangedAsync<Client>("Add"), Times.Once);
    }

    [Fact]
    public async Task GetClientByTaxIdAsync_ShouldReturnClient()
    {
        // Arrange
        var client = new Client { Id = 1, TaxId = "123" };
        _mockClientRepo.Setup(r => r.GetByTaxIdAsync("123")).ReturnsAsync(client);

        // Act
        var result = await _service.GetClientByTaxIdAsync("123");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(client);
    }

    #endregion

    #region Pet Tests

    [Fact]
    public async Task AddPetAsync_ShouldAddPet_LogAudit_AndNotify()
    {
        // Arrange
        var pet = new Pet { Id = 1, Name = "Buddy", OwnerId = 1, Species = Species.Dog, Breed = "Labrador" };
        string userName = "TestUser";

        // Act
        await _service.AddPetAsync(pet, userName);

        // Assert
        _mockPetRepo.Verify(r => r.AddAsync(pet), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Exactly(2)); // Pet + Audit

        _mockAuditRepo.Verify(r => r.AddAsync(It.Is<Domain.Audit.AuditLog>(l =>
            l.EntityName == nameof(Pet) &&
            l.Action == AuditActionType.Add &&
            l.User == userName)), Times.Once);

        _mockNotificationService.Verify(n => n.NotifyEntityChangedAsync<Pet>(pet.Id, "Add", It.IsAny<object>()), Times.Once);
        _mockNotificationService.Verify(n => n.NotifyCollectionChangedAsync<Pet>("Add"), Times.Once);
    }

    [Fact]
    public async Task GetPetsByOwnerIdAsync_ShouldReturnPets()
    {
        // Arrange
        var pets = new List<Pet>
        {
            new() { Id = 1, Name = "Buddy", OwnerId = 1, Breed = "Labrador" },
            new() { Id = 2, Name = "Max", OwnerId = 1, Breed = "Poodle" }
        };
        _mockPetRepo.Setup(r => r.GetByOwnerIdAsync(1)).ReturnsAsync(pets);

        // Act
        var result = await _service.GetPetsByOwnerIdAsync(1);

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(pets);
    }

    #endregion
}
