// <copyright file="TestToastService.cs" company="Itaneo">
// Copyright (c) Itaneo. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;
/// <summary>
/// Hand-rolled <see cref="INotificationService"/> fake that inherits <see cref="FluentServiceBase{TComponent}"/>.
/// Replaces <c>Substitute.For&lt;INotificationService&gt;()</c> because Castle.DynamicProxy/DispatchProxy
/// cannot proxy the internal members declared by <see cref="IFluentServiceBase{TComponent}"/>.
/// </summary>
internal sealed class TestToastService : FluentServiceBase<INotificationInstance>, INotificationService {
    private readonly List<ToastOptions> _capturedOptions = [];
    private Func<Action<ToastOptions>, Task<ToastResult>>? _onShowToast;

    /// <summary>Gets the list of ToastOptions captured by <see cref="ShowToastAsync(Action{ToastOptions})"/>.</summary>
    public IReadOnlyList<ToastOptions> CapturedOptions => _capturedOptions;

    /// <summary>Gets the options from the most recent <see cref="ShowToastAsync(Action{ToastOptions})"/> call, or <see langword="null"/> if none.</summary>
    public ToastOptions? LastOptions => _capturedOptions.Count == 0 ? null : _capturedOptions[^1];

    /// <summary>Configures the behavior of <see cref="ShowToastAsync(Action{ToastOptions})"/> — by default it captures and returns default.</summary>
    public void SetupShowToast(Func<Action<ToastOptions>, Task<ToastResult>> handler)
        => _onShowToast = handler;

    public Task<ToastResult> ShowToastAsync(Action<ToastOptions> options) {
        ArgumentNullException.ThrowIfNull(options);

        ToastOptions captured = new();
        options(captured);
        _capturedOptions.Add(captured);

        return _onShowToast is null
            ? Task.FromResult<ToastResult>(null!)
            : _onShowToast(options);
    }

    public Task<ToastResult> ShowToastAsync(ToastOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        _capturedOptions.Add(options);

        return Task.FromResult<ToastResult>(null!);
    }

    public Task<ToastResult> ShowToastAsync<TToast>(ToastOptions options)
        where TToast : Microsoft.AspNetCore.Components.ComponentBase
        => throw new NotSupportedException("TestToastService does not render custom toast components in unit tests.");

    public Task<ToastResult> ShowToastAsync<TToast>(Action<ToastOptions> options)
        where TToast : Microsoft.AspNetCore.Components.ComponentBase
        => throw new NotSupportedException("TestToastService does not render custom toast components in unit tests.");

    public IToastInstance? GetToastInstance(string id) => null;

    public Task CloseAsync(IToastInstance toast, object? data = null) => Task.CompletedTask;

    public Task<int> CloseAllToastsAsync() => Task.FromResult(0);

    public Task<ToastResult> ShowSuccessToastAsync(string title, string? message = null, int? lifetime = 7, string? dismissLabel = null, Func<ToastEventArgs, Task>? dismissOnClickAsync = null)
        => ShowSimpleToastAsync(ToastIntent.Success, title, message);

    public Task<ToastResult> ShowWarningToastAsync(string title, string? message = null, int? lifetime = 7, string? dismissLabel = null, Func<ToastEventArgs, Task>? dismissOnClickAsync = null)
        => ShowSimpleToastAsync(ToastIntent.Warning, title, message);

    public Task<ToastResult> ShowErrorToastAsync(string title, string? message = null, int? lifetime = 7, string? dismissLabel = null, Func<ToastEventArgs, Task>? dismissOnClickAsync = null)
        => ShowSimpleToastAsync(ToastIntent.Error, title, message);

    public Task<ToastResult> ShowInfoToastAsync(string title, string? message = null, int? lifetime = 7, string? dismissLabel = null, Func<ToastEventArgs, Task>? dismissOnClickAsync = null)
        => ShowSimpleToastAsync(ToastIntent.Info, title, message);

    public Task<ToastResult> ShowProgressToastAsync(string title, string? message = null, int? lifetime = 7, string? dismissLabel = null, Func<ToastEventArgs, Task>? dismissOnClickAsync = null)
        => ShowSimpleToastAsync(ToastIntent.Progress, title, message);

    public Task<MessageBarResult> ShowSuccessBarAsync(string section, string? title = null, string? message = null)
        => Task.FromResult<MessageBarResult>(null!);

    public Task<MessageBarResult> ShowWarningBarAsync(string section, string? title = null, string? message = null)
        => Task.FromResult<MessageBarResult>(null!);

    public Task<MessageBarResult> ShowErrorBarAsync(string section, string? title = null, string? message = null)
        => Task.FromResult<MessageBarResult>(null!);

    public Task<MessageBarResult> ShowInfoBarAsync(string section, string? title = null, string? message = null)
        => Task.FromResult<MessageBarResult>(null!);

    public Task<MessageBarResult> ShowMessageBarAsync(MessageBarOptions options)
        => Task.FromResult<MessageBarResult>(null!);

    public Task<MessageBarResult> ShowMessageBarAsync(Action<MessageBarOptions> options)
        => Task.FromResult<MessageBarResult>(null!);

    public Task<MessageBarResult> ShowMessageBarAsync<TMessageBar>(MessageBarOptions options)
        where TMessageBar : Microsoft.AspNetCore.Components.ComponentBase
        => Task.FromResult<MessageBarResult>(null!);

    public Task<MessageBarResult> ShowMessageBarAsync<TMessageBar>(Action<MessageBarOptions> options)
        where TMessageBar : Microsoft.AspNetCore.Components.ComponentBase
        => Task.FromResult<MessageBarResult>(null!);

    public Task CloseAsync(IMessageBarInstance messageBar, object? data = null) => Task.CompletedTask;

    public Task<bool> CloseAsync(string id, object? data = null) => Task.FromResult(true);

    public Task<int> CloseAllMessageBarsAsync() => Task.FromResult(0);

    private Task<ToastResult> ShowSimpleToastAsync(ToastIntent intent, string title, string? message)
        => ShowToastAsync(options => {
            options.Intent = intent;
            options.Title = title;
            options.Message = message;
        });
}
