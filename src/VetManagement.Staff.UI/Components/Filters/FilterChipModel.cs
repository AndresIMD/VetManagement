using MudBlazor;

namespace VetManagement.Staff.UI.Components.Filters;

public class FilterChipModel
{
    public required string Label { get; init; }

    public Color Color { get; init; } = Color.Default;

    public Action? OnClear { get; init; }
}
