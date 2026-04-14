// <copyright file="ToastServiceExtensions.cs" company="Itaneo">
// Copyright (c) Itaneo. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.EventStore.Admin.UI.Services;

using System.Threading.Tasks;

using Microsoft.FluentUI.AspNetCore.Components;

/// <summary>
/// Provides legacy-compatible shortcut extensions for <see cref="IToastService"/> that target
/// the Fluent UI Blazor v5 <c>ShowToastAsync</c> pipeline. Mirrors the ergonomics of the v4
/// <c>Show*</c> helpers with body-only messages and the v5 default timeout.
/// </summary>
public static class ToastServiceExtensions
{
    /// <summary>Shows a success toast with the given body message.</summary>
    /// <param name="toastService">The toast service instance.</param>
    /// <param name="message">The message rendered in the toast body. Can be null.</param>
    /// <returns>A task that completes when the toast has been shown.</returns>
    public static Task ShowSuccessAsync(this IToastService toastService, string? message)
    {
        ArgumentNullException.ThrowIfNull(toastService);
        return toastService.ShowToastAsync(options =>
        {
            options.Intent = ToastIntent.Success;
            options.Title = string.Empty;
            options.Body = message;
        });
    }

    /// <summary>Shows an error toast with the given body message.</summary>
    /// <param name="toastService">The toast service instance.</param>
    /// <param name="message">The message rendered in the toast body. Can be null.</param>
    /// <returns>A task that completes when the toast has been shown.</returns>
    public static Task ShowErrorAsync(this IToastService toastService, string? message)
    {
        ArgumentNullException.ThrowIfNull(toastService);
        return toastService.ShowToastAsync(options =>
        {
            options.Intent = ToastIntent.Error;
            options.Title = string.Empty;
            options.Body = message;
        });
    }

    /// <summary>Shows a warning toast with the given body message.</summary>
    /// <param name="toastService">The toast service instance.</param>
    /// <param name="message">The message rendered in the toast body. Can be null.</param>
    /// <returns>A task that completes when the toast has been shown.</returns>
    public static Task ShowWarningAsync(this IToastService toastService, string? message)
    {
        ArgumentNullException.ThrowIfNull(toastService);
        return toastService.ShowToastAsync(options =>
        {
            options.Intent = ToastIntent.Warning;
            options.Title = string.Empty;
            options.Body = message;
        });
    }

    /// <summary>Shows an info toast with the given body message.</summary>
    /// <param name="toastService">The toast service instance.</param>
    /// <param name="message">The message rendered in the toast body. Can be null.</param>
    /// <returns>A task that completes when the toast has been shown.</returns>
    public static Task ShowInfoAsync(this IToastService toastService, string? message)
    {
        ArgumentNullException.ThrowIfNull(toastService);
        return toastService.ShowToastAsync(options =>
        {
            options.Intent = ToastIntent.Info;
            options.Title = string.Empty;
            options.Body = message;
        });
    }
}
