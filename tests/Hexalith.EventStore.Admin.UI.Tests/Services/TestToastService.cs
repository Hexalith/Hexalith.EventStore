// <copyright file="TestToastService.cs" company="Itaneo">
// Copyright (c) Itaneo. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;
/// <summary>
/// Hand-rolled <see cref="IToastService"/> fake that inherits <see cref="FluentServiceBase{TComponent}"/>.
/// Replaces <c>Substitute.For&lt;IToastService&gt;()</c> — Castle.DynamicProxy/DispatchProxy both fail in v5
/// because <see cref="IFluentServiceBase{TComponent}"/> declares internal members (ProviderId setter,
/// Items getter, OnUpdatedAsync get/set) that cannot be proxied from outside the Microsoft.FluentUI
/// assembly. Inheriting the provided base class delegates those internals to Microsoft's implementation.
/// </summary>
internal sealed class TestToastService : FluentServiceBase<IToastInstance>, IToastService {
    private readonly List<ToastOptions> _capturedOptions = [];
    private Func<Action<ToastOptions>, Task<ToastCloseReason>>? _onShowToast;

    /// <summary>Gets the list of ToastOptions captured by <see cref="ShowToastAsync(Action{ToastOptions})"/>.</summary>
    public IReadOnlyList<ToastOptions> CapturedOptions => _capturedOptions;

    /// <summary>Gets the options from the most recent <see cref="ShowToastAsync(Action{ToastOptions})"/> call, or <see langword="null"/> if none.</summary>
    public ToastOptions? LastOptions => _capturedOptions.Count == 0 ? null : _capturedOptions[^1];

    /// <summary>Configures the behavior of <see cref="ShowToastAsync(Action{ToastOptions})"/> — by default it captures and returns default.</summary>
    public void SetupShowToast(Func<Action<ToastOptions>, Task<ToastCloseReason>> handler)
        => _onShowToast = handler;

    public Task<ToastCloseReason> ShowToastAsync(Action<ToastOptions> options) {
        ArgumentNullException.ThrowIfNull(options);

        ToastOptions captured = new();
        options(captured);
        _capturedOptions.Add(captured);

        return _onShowToast is null
            ? Task.FromResult<ToastCloseReason>(default)
            : _onShowToast(options);
    }

    public Task<ToastCloseReason> ShowToastAsync(ToastOptions? options = null) {
        if (options is not null) {
            _capturedOptions.Add(options);
        }

        return Task.FromResult<ToastCloseReason>(default);
    }

    public Task<IToastInstance> ShowToastInstanceAsync(ToastOptions? options = null)
        => throw new NotSupportedException("TestToastService does not provide IToastInstance creation in unit tests.");

    public Task<IToastInstance> ShowToastInstanceAsync(Action<ToastOptions> options)
        => throw new NotSupportedException("TestToastService does not provide IToastInstance creation in unit tests.");

    public Task UpdateToastAsync(IToastInstance toast, Action<ToastOptions> update) => Task.CompletedTask;

    public Task CloseAsync(IToastInstance Toast, ToastCloseReason reason) => Task.CompletedTask;

    public Task DismissAsync(IToastInstance Toast) => Task.CompletedTask;

    public Task<bool> DismissAsync(string toastId) => Task.FromResult(true);

    public Task<int> DismissAllAsync() => Task.FromResult(0);
}
