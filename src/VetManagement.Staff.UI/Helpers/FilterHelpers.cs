using VetManagement.Staff.UI.Components.Filters;
using VetManagement.Domain.Enums;
using MudBlazor;
using VetManagement.Staff.UI.Resources;

namespace VetManagement.Staff.UI.Helpers;

public static class FilterHelpers
{
    public static List<FilterChipModel> BuildCommonFilterChips(
        string? searchText = null,
        string? responsibleUser = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Enum? enumFilter = null,
        string? enumLabel = null,
        Action? onClearSearch = null,
        Action? onClearResponsible = null,
        Action? onClearFromDate = null,
        Action? onClearToDate = null,
        Action? onClearEnum = null)
    {
        var chips = new List<FilterChipModel>();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            chips.Add(new FilterChipModel
            {
                Label = Strings.T("Search: {0}", searchText),
                Color = Color.Primary,
                OnClear = onClearSearch ?? (() => { })
            });
        }

        if (!string.IsNullOrWhiteSpace(responsibleUser))
        {
            chips.Add(new FilterChipModel
            {
                Label = Strings.T("User: {0}", responsibleUser),
                Color = Color.Secondary,
                OnClear = onClearResponsible ?? (() => { })
            });
        }

        if (fromDate.HasValue)
        {
            chips.Add(new FilterChipModel
            {
                Label = Strings.T("From: {0}", fromDate.Value.ToString("d")),
                Color = Color.Tertiary,
                OnClear = onClearFromDate ?? (() => { })
            });
        }

        if (toDate.HasValue)
        {
            chips.Add(new FilterChipModel
            {
                Label = Strings.T("To: {0}", toDate.Value.ToString("d")),
                Color = Color.Tertiary,
                OnClear = onClearToDate ?? (() => { })
            });
        }

        if (enumFilter != null && !IsNoneValue(enumFilter) && !string.IsNullOrWhiteSpace(enumLabel))
        {
            chips.Add(new FilterChipModel
            {
                Label = $"{Strings.T(enumLabel)}: {enumFilter.ToText()}",
                Color = Color.Info,
                OnClear = onClearEnum ?? (() => { })
            });
        }

        return chips;
    }

    private static bool IsNoneValue(Enum enumValue)
    {
        return enumValue.ToString() == "None" || Convert.ToInt32(enumValue) == 0;
    }

    public static (DateTime? Start, DateTime? End) GetDateRange(DateTime? fromDate, DateTime? toDate)
    {
        DateTime? startDate = fromDate?.Date;
        DateTime? endDate = toDate?.Date.AddDays(1).AddTicks(-1);
        return (startDate, endDate);
    }
}
