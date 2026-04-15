// <copyright file="ToastServiceExtensionsTests.cs" company="Itaneo">
// Copyright (c) Itaneo. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

using System;
using System.Threading.Tasks;

using Microsoft.FluentUI.AspNetCore.Components;

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
        TestToastService fake = new();
        fake.SetupShowToast(_ => Task.FromException<ToastCloseReason>(new InvalidOperationException("provider not registered")));

        await Should.ThrowAsync<InvalidOperationException>(() => fake.ShowSuccessAsync("x"));
    }

    private static async Task<ToastOptions?> CaptureOptionsAsync(
        Func<IToastService, string?, Task> act,
        string? message)
    {
        TestToastService fake = new();

        await act(fake, message).ConfigureAwait(false);

        fake.CapturedOptions.Count.ShouldBe(1);
        return fake.LastOptions;
    }
}
