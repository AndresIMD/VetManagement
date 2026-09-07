using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace VetManagement.Api.Hubs;

[Authorize]
public class InventoryHub(ILogger<InventoryHub> logger) : Hub
{
    public async Task NotifyItemChangedAsync(int itemId, string action)
    {
        await Clients.Others.SendAsync("ItemChanged", itemId, action);
    }

    public async Task NotifyStockChangedAsync(int itemId, int newStock)
    {
        await Clients.Others.SendAsync("StockChanged", itemId, newStock);
    }

    public async Task NotifyMovementAddedAsync(int itemId)
    {
        await Clients.Others.SendAsync("MovementAdded", itemId);
    }

    public override async Task OnConnectedAsync()
    {
        var userName = Context.User?.Identity?.Name ?? "Anonymous";
        var connectionId = Context.ConnectionId;

        logger.LogInformation("SignalR InventoryHub: User '{UserName}' connected with ID '{ConnectionId}'", userName, connectionId);

        await Clients.Caller.SendAsync("Connected", $"Welcome {userName}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userName = Context.User?.Identity?.Name ?? "Anonymous";
        var connectionId = Context.ConnectionId;

        if (exception != null)
            logger.LogWarning(exception, "SignalR InventoryHub: User '{UserName}' disconnected with error (ID '{ConnectionId}')", userName, connectionId);
        else
            logger.LogInformation("SignalR InventoryHub: User '{UserName}' disconnected (ID '{ConnectionId}')", userName, connectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
