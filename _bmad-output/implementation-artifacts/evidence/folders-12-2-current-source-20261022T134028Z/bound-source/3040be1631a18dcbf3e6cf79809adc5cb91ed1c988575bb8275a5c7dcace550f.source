@namespace Hexalith.EventStore.Admin.UI.Components.Shared
@using Hexalith.EventStore.Admin.Abstractions.Models.Common
@using Hexalith.EventStore.Admin.UI.Services
@using Microsoft.AspNetCore.Components.Authorization
@inject AdminUserContext UserContext
@inject AuthenticationStateProvider AuthenticationStateProvider
@implements IDisposable

@if (_isAuthorized)
{
    @ChildContent
}

@code {
    [Parameter]
    public AdminRole MinimumRole { get; set; } = AdminRole.ReadOnly;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private bool _isAuthorized;

    protected override async Task OnInitializedAsync()
    {
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        await RefreshAuthorizationAsync().ConfigureAwait(false);
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        _ = InvokeAsync(async () =>
        {
            await RefreshAuthorizationAsync().ConfigureAwait(false);
            StateHasChanged();
        });
    }

    private async Task RefreshAuthorizationAsync()
    {
        _isAuthorized = await UserContext.HasMinimumRoleAsync(MinimumRole).ConfigureAwait(false);
    }

    public void Dispose()
    {
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
