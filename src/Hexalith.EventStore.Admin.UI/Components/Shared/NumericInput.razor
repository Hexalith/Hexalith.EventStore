@namespace Hexalith.EventStore.Admin.UI.Components.Shared
@typeparam TValue where TValue : struct, IComparable, IComparable<TValue>, IEquatable<TValue>
@using System.Globalization
@using Microsoft.FluentUI.AspNetCore.Components

<FluentTextInput Value="@_stringValue"
                 ValueChanged="@OnStringValueChangedAsync"
                 TextInputType="TextInputType.Number"
                 Label="@Label"
                 Placeholder="@Placeholder"
                 Required="@Required"
                 Autofocus="@Autofocus"
                 AriaLabel="@AriaLabel"
                 @attributes="@BuildAttributes()" />
@if (_parseError)
{
    <span class="numeric-input-error" role="alert">Invalid number</span>
}

@code {
    private string? _stringValue;
    private bool _parseError;
    private TValue? _lastParsedValue;

    /// <summary>
    /// Gets or sets the numeric value. Null indicates an empty/cleared input.
    /// <para>Keystroke semantics: <see cref="ValueChanged"/> fires on every keystroke
    /// per the underlying <see cref="FluentTextInput"/>; this wrapper does NOT
    /// debounce or defer to blur/commit. Callers with per-keystroke side effects
    /// (enable-disable buttons, validation toasts) should account for this.</para>
    /// </summary>
    [Parameter, EditorRequired] public TValue? Value { get; set; }

    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    [Parameter] public EventCallback ValueChangedAfter { get; set; }

    [Parameter] public TValue? Min { get; set; }

    [Parameter] public TValue? Max { get; set; }

    [Parameter] public string? Label { get; set; }

    [Parameter] public string? Placeholder { get; set; }

    [Parameter] public bool Required { get; set; }

    [Parameter] public bool Autofocus { get; set; }

    [Parameter] public string? AriaLabel { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? AdditionalAttributes { get; set; }

    protected override void OnParametersSet()
    {
        // Only resync _stringValue from Value when the incoming Value differs from
        // the last parsed value — otherwise we would erase the user's in-progress
        // typing mid-keystroke (per AC 5 resync policy).
        if (!Nullable.Equals(Value, _lastParsedValue))
        {
            _stringValue = FormatInvariant(Value);
            _lastParsedValue = Value;
            _parseError = false;
        }
    }

    private async Task OnStringValueChangedAsync(string next)
    {
        _stringValue = next;
        if (string.IsNullOrWhiteSpace(next))
        {
            _parseError = false;
            _lastParsedValue = null;
            await ValueChanged.InvokeAsync(null);
            await ValueChangedAfter.InvokeAsync();
            return;
        }

        if (TryParseInvariant(next, out TValue? parsed))
        {
            _parseError = false;
            _lastParsedValue = parsed;
            _stringValue = FormatInvariant(parsed);
            await ValueChanged.InvokeAsync(parsed);
            await ValueChangedAfter.InvokeAsync();
        }
        else
        {
            _parseError = true;
        }
    }

    private static bool TryParseInvariant(string s, out TValue? result)
    {
        Type t = typeof(TValue);
        CultureInfo ci = CultureInfo.InvariantCulture;
        if (t == typeof(long))
        {
            if (long.TryParse(s, NumberStyles.Integer, ci, out long v)) { result = (TValue)(object)v; return true; }
        }
        else if (t == typeof(int))
        {
            if (int.TryParse(s, NumberStyles.Integer, ci, out int v)) { result = (TValue)(object)v; return true; }
        }
        else if (t == typeof(decimal))
        {
            if (decimal.TryParse(s, NumberStyles.Number, ci, out decimal v)) { result = (TValue)(object)v; return true; }
        }
        else if (t == typeof(double))
        {
            if (double.TryParse(s, NumberStyles.Number, ci, out double v)) { result = (TValue)(object)v; return true; }
        }
        else
        {
            throw new NotSupportedException($"NumericInput<{t.Name}> not supported — add a TryParse branch.");
        }

        result = null;
        return false;
    }

    private static string? FormatInvariant(TValue? value)
        => value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value?.ToString();

    private Dictionary<string, object> BuildAttributes()
    {
        Dictionary<string, object> attrs = AdditionalAttributes is null
            ? []
            : new Dictionary<string, object>(AdditionalAttributes);
        if (Min is not null)
        {
            attrs["min"] = FormatInvariant(Min)!;
        }
        if (Max is not null)
        {
            attrs["max"] = FormatInvariant(Max)!;
        }
        return attrs;
    }
}
