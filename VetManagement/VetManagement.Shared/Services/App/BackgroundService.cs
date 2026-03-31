using Microsoft.JSInterop;

namespace VetManagement.Shared.Services.App;

/// <summary>
/// Manages application background with theme-aware styles.
/// Supports modes: none, soft, grid, and custom images.
/// </summary>
public class BackgroundService(IJSRuntime js)
{
    private const string MODE_KEY = "app.bg.mode";
    private const string IMAGE_KEY = "app.bg.image";

    public async Task InitializeAsync()
    {
        var imageUrl = await js.InvokeAsync<string?>("localStorage.getItem", IMAGE_KEY);
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            await SetImageAsync(imageUrl);
            return;
        }

        var mode = await js.InvokeAsync<string?>("localStorage.getItem", MODE_KEY);
        await SetModeAsync(mode ?? "none");
    }

    public async Task SetModeAsync(string mode)
    {
        await js.InvokeVoidAsync("appBg.setMode", mode);
        await js.InvokeVoidAsync("localStorage.setItem", MODE_KEY, mode);
        await js.InvokeVoidAsync("localStorage.removeItem", IMAGE_KEY);
    }

    public async Task SetImageAsync(string imageUrl)
    {
        await js.InvokeVoidAsync("appBg.setImage", imageUrl);
        await js.InvokeVoidAsync("localStorage.setItem", IMAGE_KEY, imageUrl);
        await js.InvokeVoidAsync("localStorage.removeItem", MODE_KEY);
    }
}
