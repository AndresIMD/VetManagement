using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace VetManagement.Api.Hubs;

/// <summary>
/// SignalR hub for real-time data updates.
/// </summary>
[Authorize]
public class DataHub : Hub
{
    private readonly ILogger<DataHub> _logger;

    public DataHub(ILogger<DataHub> logger)
    {
        _logger = logger;
    }

    public async Task NotifyEntityChangedAsync(string entityType, int entityId, string action)
    {
        await Clients.Others.SendAsync("EntityChanged", entityType, entityId, action);
    }

    public async Task NotifyExamChangedAsync(int examId, string action)
    {
        await Clients.Others.SendAsync("ExamChanged", examId, action);
    }

    public async Task NotifyClientChangedAsync(int clientId, string action)
    {
        await Clients.Others.SendAsync("ClientChanged", clientId, action);
    }

    public async Task NotifyMedicalVisitChangedAsync(int visitId, string action)
    {
        await Clients.Others.SendAsync("MedicalVisitChanged", visitId, action);
    }

    public override async Task OnConnectedAsync()
    {
        var userName = Context.User?.Identity?.Name ?? "Anonymous";
        var connectionId = Context.ConnectionId;

        _logger.LogInformation("SignalR DataHub: User '{UserName}' connected with ID '{ConnectionId}'", userName, connectionId);

        await Clients.Caller.SendAsync("Connected", $"Connected to DataHub: {userName}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userName = Context.User?.Identity?.Name ?? "Anonymous";
        var connectionId = Context.ConnectionId;

        if (exception != null)
            _logger.LogWarning(exception, "SignalR DataHub: User '{UserName}' disconnected with error (ID '{ConnectionId}')", userName, connectionId);
        else
            _logger.LogInformation("SignalR DataHub: User '{UserName}' disconnected (ID '{ConnectionId}')", userName, connectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
