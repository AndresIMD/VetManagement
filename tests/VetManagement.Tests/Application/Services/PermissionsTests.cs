using VetManagement.Api.Authorization;
using Xunit;

namespace VetManagement.Tests;

public class PermissionsTests
{
    [Fact]
    public void All_ShouldContain_SystemSendEmail()
    {
        var all = Permissions.ALL().ToList();
        Assert.Contains(Permissions.SYSTEM.SEND_EMAIL, all);
    }
}
