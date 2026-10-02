using Moq;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Audit;
using Xunit;

namespace VetManagement.Tests;

public class AuditServiceTests
{
    [Fact]
    public async Task LogSecurityAsync_WritesSecurityEntry()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockAuditRepo = new Mock<IAuditLogRepository>();

        mockUnitOfWork.Setup(uow => uow.AuditLogs).Returns(mockAuditRepo.Object);
        mockUnitOfWork.Setup(uow => uow.SaveChangesAsync()).ReturnsAsync(1);

        var service = new AuditService(mockUnitOfWork.Object);

        // ACT
        await service.LogSecurityAsync(AuditActionType.UserLogin, "tester", "ok");

        // ASSERT
        mockAuditRepo.Verify(repo => repo.AddAsync(It.Is<AuditLog>(log =>
            log.EntityName == "Security" &&
            log.EntityId == 0 &&
            log.Action == AuditActionType.UserLogin &&
            log.User == "tester" &&
            log.Changes == "ok"
        )), Times.Once);

        mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(), Times.Once);
    }
}
