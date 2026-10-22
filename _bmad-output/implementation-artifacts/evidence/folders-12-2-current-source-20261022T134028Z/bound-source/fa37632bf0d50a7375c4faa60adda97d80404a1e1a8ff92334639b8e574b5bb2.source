@inject IJSRuntime JSRuntime
@inject NavigationManager NavigationManager

<FluentDialog @ref="_dialog" Modal="true" aria-label="Command palette"
              OnStateChange="OnDialogStateChangeAsync"
              Style="width: 560px; max-height: 480px;">
    <FluentDialogBody>
        <TitleTemplate>
            <FluentTextInput @bind-Value="_searchQuery" @bind-Value:after="OnSearchChanged" Class="command-palette-search"
                          Placeholder="Search commands, pages, actions..." Style="width: 100%;"
                          TextInputType="TextInputType.Text">
                <StartTemplate>
                    <FluentIcon Value="@(new Icons.Regular.Size16.Search())" />
                </StartTemplate>
                <EndTemplate>
                    <FluentButton Appearance="ButtonAppearance.Transparent"
                                  Disabled="@(string.IsNullOrEmpty(_searchQuery))"
                                  OnClick="@(() => { _searchQuery = string.Empty; OnSearchChanged(); })"
                                  aria-label="Clear search"
                                  Title="Clear search">
                        <FluentIcon Value="@(new Icons.Regular.Size16.Dismiss())" />
                    </FluentButton>
                </EndTemplate>
            </FluentTextInput>
        </TitleTemplate>
        <ChildContent>
            @if (_filteredResults.Count > 0)
            {
                string? currentCategory = null;
                @foreach (CommandPaletteItem item in _filteredResults)
                {
                    @if (item.Category != currentCategory)
                    {
                        currentCategory = item.Category;
                        <div
                            style="padding: 8px 0 4px; font-size: 11px; font-weight: 600; color: var(--colorNeutralForeground3); text-transform: uppercase; letter-spacing: 0.5px;">
                            @currentCategory
                        </div>
                    }
                    <FluentButton Appearance="ButtonAppearance.Subtle"
                                  Style="width: 100%; justify-content: flex-start; padding: 8px 12px;"
                                  OnClick="@(() => NavigateToAsync(item.Href))">
                        @item.Label
                    </FluentButton>
                }
            }
            else if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                <div style="padding: 24px; text-align: center; color: var(--colorNeutralForeground3);">
                    No results found for "@_searchQuery"
                </div>
            }
        </ChildContent>
    </FluentDialogBody>
</FluentDialog>

@code {
    private FluentDialog? _dialog;
    private bool _isOpen;
    private DialogState _dialogState = DialogState.Closed;
    private bool _isTransitioning;
    private bool _focusSearchRequested;
    private string? _searchQuery;
    private List<CommandPaletteItem> _filteredResults = [];

    public Task OpenAsync() => OpenAsync(force: false);

    public async Task OpenAsync(bool force)
    {
        if (_isTransitioning)
        {
            return;
        }

        // UX contract: no-op when the dialog is genuinely open.
        // Recovery path: if _isOpen is stale but state says Closing/Closed, allow re-open.
        if (!force && _isOpen && (_dialogState is DialogState.Open or DialogState.Opening))
        {
            return;
        }

        _isTransitioning = true;
        try
        {
            _searchQuery = null;
            _filteredResults = [.. CommandPaletteCatalog.AllItems];
            _focusSearchRequested = true;
            if (_dialog is not null)
            {
                _dialogState = DialogState.Opening;
                try
                {
                    await _dialog.ShowAsync();
                }
                catch (JSException)
                {
                    // FluentDialog v5 ShowAsync invoked on a stale-open dialog
                    // can throw via the underlying web component. The force path
                    // is the recovery — preserve state machine and continue.
                }
                _isOpen = true;
            }
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    public async Task CloseAsync()
    {
        if (_isTransitioning || !_isOpen)
        {
            return;
        }

        _isTransitioning = true;
        try
        {
            if (_dialog is not null)
            {
                await _dialog.HideAsync();
            }

            _isOpen = false;
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private void OnSearchChanged()
    {
        _filteredResults = [.. CommandPaletteCatalog.Filter(_searchQuery)];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusSearchRequested && _isOpen)
        {
            _focusSearchRequested = false;
            await JSRuntime.InvokeVoidAsync("hexalithAdmin.focusCommandPaletteSearch");
        }
    }

    private Task OnDialogStateChangeAsync(DialogEventArgs args)
    {
        _dialogState = args.State;

        if (args.State is DialogState.Open or DialogState.Opening)
        {
            _isOpen = true;
        }
        else if (args.State is DialogState.Closing or DialogState.Closed)
        {
            _isOpen = false;
            _isTransitioning = false;
            _focusSearchRequested = false;
        }

        return Task.CompletedTask;
    }

    private async Task NavigateToAsync(string href)
    {
        await CloseAsync();
        NavigationManager.NavigateTo(href);
    }
}
