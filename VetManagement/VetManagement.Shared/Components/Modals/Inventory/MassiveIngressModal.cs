using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using VetManagement.Shared.Components.Modals.Common;
using VetManagement.Shared.Models.Core;
using VetManagement.Shared.Models.DTOs;

namespace VetManagement.Shared.Components.Modals.Inventory;

public partial class MassiveIngressModal : MassiveBaseModal<InventoryMassUpdateDTO>
{
    private readonly Dictionary<int, int> _stockByItemId = [];

    protected override void OnInitialized()
    {
        Title = "Massive ingress";
        TitleIcon = Icons.Material.Filled.Upload;
        TitleColor = Color.Success;
        base.OnInitialized();
    }

    protected override async Task<object?> GetItemByBarcode(string barcode)
    {
        var item = await ItemsService.GetItemByBarcodeAsync(barcode);
        if (item is Item i)
            _stockByItemId[i.Id] = i.Stock;
        return item;
    }
    protected override InventoryMassUpdateDTO CreateNewItem(object item)
    {
        var i = item as Item;
        if (i != null)
        {
            _stockByItemId[i.Id] = i.Stock;
        }
        return new InventoryMassUpdateDTO { ItemId = i?.Id ?? 0, Barcode = i?.Barcode ?? string.Empty, Name = i?.Name ?? string.Empty, Quantity = 1 };
    }
    protected override string GetName(InventoryMassUpdateDTO item) => item.Name;
    protected override string GetBarcode(InventoryMassUpdateDTO item) => item.Barcode;
    protected override int GetQuantity(InventoryMassUpdateDTO item) => item.Quantity;
    protected override void SetQuantity(InventoryMassUpdateDTO item, int value) => item.Quantity = value;
    protected override int? GetStock(InventoryMassUpdateDTO item)
    {
        if (_stockByItemId.TryGetValue(item.ItemId, out var stock))
            return stock;
        return null;
    }
    protected override int GetItemId(InventoryMassUpdateDTO item) => item.ItemId;

    protected override async Task OnBarcodeKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(currentBarcode))
        {
            var item = await ItemsService.GetItemByBarcodeAsync(currentBarcode);

            if (item != null)
            {
                _stockByItemId[item.Id] = item.Stock;

                var existing = items.FirstOrDefault(x => x.ItemId == item.Id);
                if (existing != null)
                    existing.Quantity++;
                else
                    items.Add(new InventoryMassUpdateDTO { ItemId = item.Id, Barcode = item.Barcode ?? string.Empty, Name = item.Name, Quantity = 1 });
                shouldFocusBarcode = true;
            }
            else
            {
                await BlurBarcodeInputAsync();
                shouldFocusBarcode = false;

                var confirmParams = new DialogParameters
                {
                    ["Title"] = "Create new product",
                    ["Message"] = $"The code {currentBarcode} doesn't exist. Do you want to create a new product?"
                };
                var confirmModal = await DialogService.ShowAsync<ConfirmModal>("Confirm", confirmParams);
                var confirmResult = await confirmModal.Result;

                if (confirmResult is not null && !confirmResult.Canceled)
                {
                    var createParams = new DialogParameters
                    {
                        [nameof(CreateItemModal.BarcodeReadonly)] = true
                    };
                    var createModal = await DialogService.ShowAsync<CreateItemModal>("New product", createParams);
                    var createResult = await createModal.Result;
                    if (createResult is not null && !createResult.Canceled && createResult.Data is Item created)
                    {
                        created.Barcode = currentBarcode;
                        _stockByItemId[created.Id] = created.Stock;
                        items.Add(new InventoryMassUpdateDTO { ItemId = created.Id, Barcode = created.Barcode ?? string.Empty, Name = created.Name, Quantity = 1 });
                    }
                }
            }

            currentBarcode = string.Empty;
            shouldFocusBarcode = true;
            StateHasChanged();
        }
    }
}
