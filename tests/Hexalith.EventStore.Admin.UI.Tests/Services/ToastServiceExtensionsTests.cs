// <copyright file="ToastServiceExtensionsTests.cs" company="Itaneo">
// Copyright (c) Itaneo. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

using System;
using System.Threading.Tasks;

using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;

public class ToastServiceExtensionsTests
{
    [Fact]
    public async Task ShowSuccessAsync_CallsShowToastAsync_WithSuccessIntent()
    {
        ToastOptions? captured = await CaptureOptionsAsync((svc, msg) => svc.ShowSuccessAsync(msg), "hello");

        captured.ShouldNotBeNull();
        captured.Intent.ShouldBe(ToastIntent.Success);
        captured.Body.ShouldBe("hello");
        captured.Title.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ShowErrorAsync_CallsShowToastAsync_WithErrorIntent()
    {
        ToastOptions? captured = await CaptureOptionsAsync((svc, msg) => svc.ShowErrorAsync(msg), "boom");

        captured.ShouldNotBeNull();
        captured.Intent.ShouldBe(ToastIntent.Error);
        captured.Body.ShouldBe("boom");
        captured.Title.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ShowWarningAsync_CallsShowToastAsync_WithWarningIntent()
    {
        ToastOptions? captured = await CaptureOptionsAsync((svc, msg) => svc.ShowWarningAsync(msg), "careful");

        captured.ShouldNotBeNull();
        captured.Intent.ShouldBe(ToastIntent.Warning);
        captured.Body.ShouldBe("careful");
        captured.Title.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ShowInfoAsync_CallsShowToastAsync_WithInfoIntent()
    {
        ToastOptions? captured = await CaptureOptionsAsync((svc, msg) => svc.ShowInfoAsync(msg), "fyi");

        captured.ShouldNotBeNull();
        captured.Intent.ShouldBe(ToastIntent.Info);
        captured.Body.ShouldBe("fyi");
        captured.Title.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ShowSuccessAsync_NullMessage_PassesNullBodyWithoutThrowing()
    {
        ToastOptions? captured = await CaptureOptionsAsync((svc, msg) => svc.ShowSuccessAsync(msg), null);

        captured.ShouldNotBeNull();
        captured.Intent.ShouldBe(ToastIntent.Success);
        captured.Body.ShouldBeNull();
    }

    [Fact]
    public async Task ShowSuccessAsync_WhenShowToastAsyncThrows_PropagatesException()
    {
        IToastService mockToast = Substitute.For<IToastService>();
        mockToast
            .ShowToastAsync(Arg.Any<Action<ToastOptions>>())
            .Returns(Task.FromException<ToastCloseReason>(new InvalidOperationException("provider not registered")));

        await Should.ThrowAsync<InvalidOperationException>(() => mockToast.ShowSuccessAsync("x"));
    }

    private static async Task<ToastOptions?> CaptureOptionsAsync(
        Func<IToastService, string?, Task> act,
        string? message)
    {
        IToastService mockToast = Substitute.For<IToastService>();
        ToastOptions? capturedOptions = null;

        mockToast
            .ShowToastAsync(Arg.Do<Action<ToastOptions>>(configure =>
            {
                capturedOptions = new ToastOptions();
                configure(capturedOptions);
            }))
            .Returns(Task.FromResult<ToastCloseReason>(default!));

        await act(mockToast, message).ConfigureAwait(false);

        await mockToast.Received(1).ShowToastAsync(Arg.Any<Action<ToastOptions>>()).ConfigureAwait(false);

        return capturedOptions;
    }
}
