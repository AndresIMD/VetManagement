using MudBlazor;
using VetManagement.Staff.UI.Resources;

namespace VetManagement.Staff.UI.Components.Modals.Inventory;

/// <summary>Moves several items from one warehouse to another; like an egress, at most what the first one holds.</summary>
public class MassiveTransferModal : MassiveEgressModal
{
    protected override bool IsTransfer => true;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Title = Strings.T("Transfer between warehouses");
        TitleIcon = Icons.Material.Filled.SwapHoriz;
        TitleColor = Color.Primary;
    }
}
