using Microsoft.JSInterop;
using MudBlazor;
using VetManagement.Shared.Theme;

namespace VetManagement.Shared.Services.App;

/// <summary>
/// Manages application theme with dynamic CSS variable injection.
/// Eliminates static CSS files and provides seamless dark/light mode switching.
/// </summary>
public class ThemeService(IJSRuntime js)
{
    private const string THEME_KEY = "app.theme.mode";
    private const string LIGHT_MODE = "light";
    private const string DARK_MODE = "dark";

    public bool IsDarkMode { get; private set; }
    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        var storedMode = await js.InvokeAsync<string?>("localStorage.getItem", THEME_KEY);
        IsDarkMode = storedMode == DARK_MODE;
        await ApplyThemeAsync();
    }

    public async Task ToggleAsync()
    {
        IsDarkMode = !IsDarkMode;
        await js.InvokeVoidAsync("localStorage.setItem", THEME_KEY, IsDarkMode ? DARK_MODE : LIGHT_MODE);
        await ApplyThemeAsync();
        OnChange?.Invoke();
    }

    private async Task ApplyThemeAsync()
    {
        var palette = IsDarkMode ? AppTheme.LightDark : (Palette)AppTheme.Light;

        var cssVariables = new Dictionary<string, string>
        {
            ["--app-primary"] = palette.Primary.ToString(),
            ["--app-secondary"] = palette.Secondary.ToString(),
            ["--app-tertiary"] = palette.Tertiary.ToString(),
            ["--app-info"] = palette.Info.ToString(),
            ["--app-success"] = palette.Success.ToString(),
            ["--app-warning"] = palette.Warning.ToString(),
            ["--app-error"] = palette.Error.ToString(),
            ["--app-background"] = palette.Background.ToString(),
            ["--app-surface"] = palette.Surface.ToString(),
            ["--app-appbar-bg"] = palette.AppbarBackground.ToString(),
            ["--app-appbar-text"] = palette.AppbarText.ToString(),
            ["--app-text-primary"] = palette.TextPrimary.ToString(),
            ["--app-text-secondary"] = palette.TextSecondary.ToString(),
            ["--app-divider"] = palette.Divider.ToString(),
        };

        if (palette is PaletteDark dark)
        {
            cssVariables["--app-drawer-bg"] = dark.DrawerBackground.ToString();
            cssVariables["--app-drawer-text"] = dark.DrawerText.ToString();
            cssVariables["--app-lines"] = dark.LinesDefault.ToString();
        }

        await js.InvokeVoidAsync("appTheme.applyVariables", cssVariables);
    }
}
