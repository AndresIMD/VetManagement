using MudBlazor;
using VetManagement.Staff.UI.Models.Core;
using VetManagement.Staff.UI.Models.DTOs;
using VetManagement.Staff.UI.Resources;

namespace VetManagement.Staff.UI.Components.Modals.Inventory;

public class MassiveEgressModal : MassiveBaseModal<InventoryMassUpdateDTO>
{
    private readonly Dictionary<int, int> _stockByItemId = [];
    private string? _errorMessage;

    protected override void OnInitialized()
    {
        Title = Strings.T("Mass egress");
        TitleIcon = Icons.Material.Filled.Download;
        TitleColor = Color.Error;
        base.OnInitialized();
    }

    protected override async Task<object?> GetItemByBarcode(string barcode)
    {
        var item = await ItemsService.GetItemByBarcodeAsync(barcode);
        if (item is Item i)
        {
            _stockByItemId[i.Id] = i.Stock;
        }
        return item;
    }

    protected override InventoryMassUpdateDTO CreateNewItem(object item)
    {
        var i = item as Item;
        if (i != null)
        {
            _stockByItemId[i.Id] = i.Stock;
        }
        return new InventoryMassUpdateDTO
        {
            ItemId = i?.Id ?? 0,
            Barcode = i?.Barcode ?? string.Empty,
            Name = i?.Name ?? string.Empty,
            Quantity = 1
        };
    }

    protected override string GetName(InventoryMassUpdateDTO item) => item.Name;
    protected override string GetBarcode(InventoryMassUpdateDTO item) => item.Barcode;
    protected override int GetQuantity(InventoryMassUpdateDTO item) => item.Quantity;
    protected override void SetQuantity(InventoryMassUpdateDTO item, int value)
    {
        if (_stockByItemId.TryGetValue(item.ItemId, out var stock))
        {
            if (value > stock)
            {
                _errorMessage = Strings.T("You cannot withdraw more than {0} units of '{1}'.", stock, item.Name);
                item.Quantity = stock;
            }
            else if (stock <= 0)
            {
                _errorMessage = Strings.T("No stock available for '{0}'.", item.Name);
                item.Quantity = 0;
            }
            else
            {
                _errorMessage = null;
                item.Quantity = Math.Max(0, value);
            }
        }
        else
        {
            item.Quantity = Math.Max(0, value);
        }
    }

    protected override int? GetStock(InventoryMassUpdateDTO item)
    {
        if (_stockByItemId.TryGetValue(item.ItemId, out var stock))
            return stock;
        return null;
    }

    protected override int GetItemId(InventoryMassUpdateDTO item) => item.ItemId;

    protected override bool CanAddItem(object entity, out string? reason)
    {
        reason = null;
        if (entity is Item i)
        {
            if (i.Stock <= 0)
            {
                reason = Strings.T("No stock available for '{0}'.", i.Name);
                return false;
            }
        }
        return true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (!string.IsNullOrEmpty(_errorMessage))
        {
            var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = false };
            await DialogService.ShowAsync<Components.Modals.Common.ConfirmModal>(
                Strings.T("Stock error"),
                new DialogParameters<Components.Modals.Common.ConfirmModal>
                {
                    { x => x.Title, Strings.T("Stock error") },
                    { x => x.Message, _errorMessage },
                    { x => x.ButtonConfirmText, Strings.T("OK") },
                    { x => x.ButtonCancelText, string.Empty }
                },
                options
            );
            _errorMessage = null;
        }
    }
}
