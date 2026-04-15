using System.Globalization;

using Bunit;

using Hexalith.EventStore.Admin.UI.Components.Shared;

using Microsoft.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components.Shared;

public class NumericInputTests : AdminUITestContext
{
    [Fact]
    public void NumericInput_RendersWithInitialValue()
    {
        IRenderedComponent<NumericInput<long>> cut = Render<NumericInput<long>>(
            parameters => parameters
                .Add(p => p.Value, 42L)
                .Add(p => p.Min, 0L)
                .Add(p => p.Placeholder, "e.g. 0"));

        string markup = cut.Markup;
        markup.ShouldContain("fluent-text-input");
        markup.ShouldContain("type=\"number\"");
        markup.ShouldContain("min=\"0\"");
        markup.ShouldContain("value=\"42\"");
    }

    [Fact]
    public void NumericInput_RejectsNonNumericInputAndShowsError()
    {
        IRenderedComponent<NumericInput<long>> cut = Render<NumericInput<long>>(
            parameters => parameters
                .Add(p => p.Value, 1L));

        cut.Find("fluent-text-input").Change("abc");

        cut.Markup.ShouldContain("numeric-input-error");
        cut.Markup.ShouldContain("Invalid number");
    }

    [Fact]
    public void NumericInput_FiresValueChangedCallback()
    {
        long? received = null;
        IRenderedComponent<NumericInput<long>> cut = Render<NumericInput<long>>(
            parameters => parameters
                .Add(p => p.Value, 0L)
                .Add(p => p.ValueChanged, EventCallback.Factory.Create<long?>(this, v => received = v)));

        cut.Find("fluent-text-input").Change("7");

        received.ShouldBe(7L);
    }

    [Fact]
    public void NumericInput_PreservesInvariantCultureParsing()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            // Switch to a culture that uses ',' as decimal separator so "1000.5"
            // would NOT parse under CurrentCulture.
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            decimal? received = null;
            IRenderedComponent<NumericInput<decimal>> cut = Render<NumericInput<decimal>>(
                parameters => parameters
                    .Add(p => p.Value, 0m)
                    .Add(p => p.ValueChanged, EventCallback.Factory.Create<decimal?>(this, v => received = v)));

            cut.Find("fluent-text-input").Change("1000.5");
            received.ShouldBe(1000.5m);

            cut.Find("fluent-text-input").Change("1000");
            received.ShouldBe(1000m);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void NumericInput_RoundTripsValueThroughChange()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            long? received = null;
            IRenderedComponent<NumericInput<long>> cut = Render<NumericInput<long>>(
                parameters => parameters
                    .Add(p => p.Value, 42L)
                    .Add(p => p.ValueChanged, EventCallback.Factory.Create<long?>(this, v => received = v)));

            cut.Find("fluent-text-input").Change("100");
            received.ShouldBe(100L);

            // Re-render with the new value to confirm format side is invariant-culture.
            cut.Render(parameters => parameters.Add(p => p.Value, 100L));
            cut.Markup.ShouldContain("value=\"100\"");
            cut.Markup.ShouldNotContain("100,00");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
