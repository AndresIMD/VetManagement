using MudBlazor;

namespace VetManagement.Staff.UI.Theme;

public static class AppTheme
{
    // Light palette
    public static readonly PaletteLight Light = new()
    {
        Primary = "#1976d2",
        Secondary = "#6fb1fc",
        Tertiary = "#00bcd4",
        Info = "#29b6f6",
        Success = "#2e7d32",
        Warning = "#f9a825",
        Error = "#d32f2f",
        Background = "#f5f7fa",
        Surface = "#ffffff",
        AppbarBackground = "#1976d2",
        AppbarText = "#ffffff",
    };

    // Dark palette
    public static readonly PaletteDark Dark = new()
    {
        Primary = "#90caf9",
        Secondary = "#80deea",
        Tertiary = "#4dd0e1",
        Info = "#4fc3f7",
        Success = "#81c784",
        Warning = "#ffb74d",
        Error = "#ef5350",
        Background = "#121212",
        Surface = "#1e1e1e",
        DrawerBackground = "#1e1e1e",
        AppbarBackground = "#1e1e1e",
        AppbarText = "#e0e0e0"
    };

    // LightDark palette
    public static readonly PaletteDark LightDark = new()
    {
        Primary = "#90caf9",
        Secondary = "#80deea",
        Tertiary = "#4dd0e1",
        Info = "#4fc3f7",
        Success = "#81c784",
        Warning = "#ffb74d",
        Error = "#ef5350",

        // Softened grays
        Background = "#181a1f",
        Surface = "#20242a",
        DrawerBackground = "#20242a",
        AppbarBackground = "#20242a",
        AppbarText = "#e6e6e6",

        // More subtle lines and dividers
        LinesDefault = "#2a2f36",
        TableLines = "#2a2f36",
        Divider = "#2a2f36",

        // Higher contrast text and icons
        TextPrimary = "#e6e6e6",
        TextSecondary = "#b9c2cc",
        DrawerText = "#dde3ea",
        DrawerIcon = "#c7d0d9",
    };

    public static MudTheme Theme => new()
    {
        PaletteLight = Light,
        PaletteDark = LightDark
    };
}
