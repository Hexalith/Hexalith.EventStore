using Bunit;
using Bunit.Rendering;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.UI.Tests;

/// <summary>
/// Guards the renderer-info contract of <see cref="AdminUITestContext"/>.
/// FluentUI v5 reads <see cref="ComponentBase.RendererInfo"/>, so every render entry point must
/// declare one; setting it eagerly in the constructor is not an option because it seals
/// <see cref="BunitContext.Services"/>. Without both properties the whole Admin.UI suite breaks.
/// </summary>
public class AdminUITestContextRendererInfoTests : AdminUITestContext {
    private static RenderFragment ProbeFragment => builder => {
        builder.OpenComponent<RendererInfoProbe>(0);
        builder.CloseComponent();
    };

    [Fact]
    public void Services_RemainOpenForRegistration_AfterConstruction() {
        // Regresses if EnsureRendererInfo is ever moved into the constructor: touching
        // Renderer builds the provider and every later Services.Add* throws.
        Should.NotThrow(() => Services.AddSingleton(new ProbeMarker()));
    }

    [Fact]
    public void Render_ParameterBuilderOverload_AppliesInteractiveServerRendererInfo() {
        IRenderedComponent<RendererInfoProbe> cut = Render<RendererInfoProbe>();

        cut.Markup.ShouldBe("Server|True");
    }

    [Fact]
    public void Render_FragmentOverload_AppliesInteractiveServerRendererInfo() {
        IRenderedComponent<RendererInfoProbe> cut = Render<RendererInfoProbe>(ProbeFragment);

        cut.Markup.ShouldBe("Server|True");
    }

    [Fact]
    public void Render_NonGenericFragmentOverload_AppliesInteractiveServerRendererInfo() {
        IRenderedComponent<ContainerFragment> cut = Render(ProbeFragment);

        cut.Markup.ShouldBe("Server|True");
    }

    [Fact]
    public void SetRendererInfo_ExplicitChoice_IsNotOverwrittenByRender() {
        SetRendererInfo(new RendererInfo("WebAssembly", false));

        IRenderedComponent<RendererInfoProbe> cut = Render<RendererInfoProbe>();

        cut.Markup.ShouldBe("WebAssembly|False");
    }

    private sealed class ProbeMarker;

    private sealed class RendererInfoProbe : ComponentBase {
        protected override void BuildRenderTree(RenderTreeBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);
            builder.AddContent(0, $"{RendererInfo.Name}|{RendererInfo.IsInteractive}");
        }
    }
}
