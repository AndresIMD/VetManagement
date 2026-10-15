using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Moq;
using VetManagement.Api.Authorization;
using VetManagement.Api.Services;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Audit;
using Xunit;

namespace VetManagement.Tests;

public class UserManagementServiceTests
{
    [Fact]
    public async Task AssignRole_ShouldSyncClaims_ForLaboratory()
    {
        // Mocks for Identity
        var store = new Mock<IUserStore<IdentityUser>>();
        var userManager = new Mock<UserManager<IdentityUser>>(store.Object, null, null, null, null, null, null, null, null);
        var roleStore = new Mock<IRoleStore<IdentityRole>>();
        var roleManager = new Mock<RoleManager<IdentityRole>>(roleStore.Object, null, null, null, null);

        // Mock for AuditService dependencies
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockAuditRepo = new Mock<IAuditLogRepository>();
        mockAuditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>())).Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(uow => uow.AuditLogs).Returns(mockAuditRepo.Object);
        mockUnitOfWork.Setup(uow => uow.SaveChangesAsync()).ReturnsAsync(1);

        // Create AuditService with mocked UnitOfWork
        var auditService = new AuditService(mockUnitOfWork.Object);

        var user = new IdentityUser { Id = "u1", UserName = "john" };
        userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        roleManager.Setup(r => r.RoleExistsAsync(Permissions.ROLES.LABORATORY)).ReturnsAsync(true);
        userManager.Setup(m => m.AddToRoleAsync(user, Permissions.ROLES.LABORATORY)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { Permissions.ROLES.LABORATORY });
        userManager.Setup(m => m.GetClaimsAsync(user)).ReturnsAsync(new List<Claim>());
        userManager.Setup(m => m.AddClaimAsync(user, It.IsAny<Claim>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.RemoveClaimAsync(user, It.IsAny<Claim>())).ReturnsAsync(IdentityResult.Success);

        var svc = new UserManagementService(userManager.Object, roleManager.Object, auditService);
        var ok = await svc.AssignRoleAsync("u1", Permissions.ROLES.LABORATORY);

        Assert.True(ok);
        userManager.Verify(m => m.AddClaimAsync(user, It.Is<Claim>(c => c.Type == Permissions.CLAIM_TYPE && c.Value == Permissions.EXAMS.READ)), Times.AtLeastOnce);
    }
}
