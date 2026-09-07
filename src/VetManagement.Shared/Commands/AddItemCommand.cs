using VetManagement.Shared.Models.Core;
using VetManagement.Shared.Services.Api;

namespace VetManagement.Shared.Commands;

public sealed class AddItemCommand(ItemsApiService itemsApi, Item item, string performedBy) : ICommand
{
    public string Description => $"Add Item: {item.Name} (Type: {item.Type})";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string? PerformedBy { get; } = performedBy;

    public async Task ExecuteAsync() => await itemsApi.AddItemAsync(item);

    public async Task UndoAsync()
    {
        if (item.Id > 0)
            await itemsApi.DeleteItemAsync(item.Id);
    }
}
