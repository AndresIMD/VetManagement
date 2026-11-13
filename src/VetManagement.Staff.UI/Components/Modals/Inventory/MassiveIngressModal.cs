using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using VetManagement.Staff.UI.Components.Modals.Common;
using VetManagement.Staff.UI.Models.Core;
using VetManagement.Staff.UI.Models.DTOs;
using VetManagement.Staff.UI.Resources;

namespace VetManagement.Staff.UI.Components.Modals.Inventory;

public partial class MassiveIngressModal : MassiveBaseModal<InventoryMassUpdateDTO>
{
    protected override void OnInitialized()
    {
        Title = Strings.T("Mass ingress");
        TitleIcon = Icons.Material.Filled.Upload;
        TitleColor = Color.Success;
        base.OnInitialized();
    }

    protected override async Task<object?> GetItemByBarcode(string barcode)
    {
        var item = await ItemsService.GetItemByBarcodeAsync(barcode);
        Remember(item);
        return item;
    }
    protected override InventoryMassUpdateDTO CreateNewItem(object item)
    {
        var i = item as Item;
        Remember(i);
        return new InventoryMassUpdateDTO { ItemId = i?.Id ?? 0, Barcode = i?.Barcode ?? string.Empty, Name = i?.Name ?? string.Empty, Quantity = 1 };
    }
    protected override string GetName(InventoryMassUpdateDTO item) => item.Name;
    protected override string GetBarcode(InventoryMassUpdateDTO item) => item.Barcode;
    protected override int GetQuantity(InventoryMassUpdateDTO item) => item.Quantity;
    protected override void SetQuantity(InventoryMassUpdateDTO item, int value) => item.Quantity = value;
    protected override int? GetStock(InventoryMassUpdateDTO item) => UnitsInChosen(item.ItemId);
    protected override int GetItemId(InventoryMassUpdateDTO item) => item.ItemId;

    protected override async Task OnBarcodeKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(currentBarcode))
        {
            var item = await ItemsService.GetItemByBarcodeAsync(currentBarcode);

            if (item != null)
            {
                AddToList(item);
                shouldFocusBarcode = true;
            }
            else
            {
                await BlurBarcodeInputAsync();
                shouldFocusBarcode = false;

                var confirmParams = new DialogParameters
                {
                    ["Title"] = Strings.T("Create new product"),
                    ["Message"] = Strings.T("The code {0} doesn't exist. Do you want to create a new product?", currentBarcode)
                };
                var confirmModal = await DialogService.ShowAsync<ConfirmModal>(Strings.T("Confirm"), confirmParams);
                var confirmResult = await confirmModal.Result;

                if (confirmResult is not null && !confirmResult.Canceled)
                {
                    var createParams = new DialogParameters
                    {
                        [nameof(CreateItemModal.BarcodeReadonly)] = true,
                        [nameof(CreateItemModal.Barcode)] = currentBarcode,
                        // Its units are counted here, in the list, like every other product's.
                        [nameof(CreateItemModal.AsksForUnits)] = false,
                        [nameof(CreateItemModal.Warehouses)] = Warehouses
                    };
                    var createModal = await DialogService.ShowAsync<CreateItemModal>(Strings.T("New product"), createParams);
                    var createResult = await createModal.Result;
                    if (createResult is not null && !createResult.Canceled && createResult.Data is Item created)
                    {
                        Remember(created);
                        items.Add(new InventoryMassUpdateDTO { ItemId = created.Id, Barcode = created.Barcode ?? string.Empty, Name = created.Name, Quantity = 1 });
                    }
                    // The name typed was a product that already exists (under another code): that one is listed.
                    else if (createResult is { Canceled: false, Data: CreateItemModal.ExistingChosen chosen }
                             && await ItemsService.GetItemByIdAsync(chosen.ItemId) is { } known)
                    {
                        AddToList(known);
                    }
                }
            }

            currentBarcode = string.Empty;
            shouldFocusBarcode = true;
            StateHasChanged();
        }
    }

    /// <summary>One more unit of the product in the list, or its first.</summary>
    private void AddToList(Item item)
    {
        Remember(item);
        if (items.FirstOrDefault(x => x.ItemId == item.Id) is { } listed)
            listed.Quantity++;
        else
            items.Add(new InventoryMassUpdateDTO { ItemId = item.Id, Barcode = item.Barcode ?? string.Empty, Name = item.Name, Quantity = 1 });
    }
}
