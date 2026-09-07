using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace VetManagement.Shared.Components.Modals.Base
{
    public abstract class ModalBase<TModel> : ComponentBase
    {
        // Features
        [CascadingParameter] public IMudDialogInstance MudDialog { get; set; } = default!;
        [Inject] public ISnackbar Snackbar { get; set; } = default!;

        // Header
        [Parameter] public virtual string Title { get; set; } = string.Empty;
        [Parameter] public virtual string? TitleIcon { get; set; } = null;
        [Parameter] public virtual Color TitleColor { get; set; } = Color.Primary;

        protected TModel Model { get; set; } = default!;
        protected bool isLoading = false;
        protected bool isValid = false;

        protected virtual Task OnFormValidChanged(bool valid)
        {
            isValid = valid;
            StateHasChanged();
            return Task.CompletedTask;
        }
        protected virtual void OnCancel()
        {
            MudDialog.Cancel();
        }
    }
}
