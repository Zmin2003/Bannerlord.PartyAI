using Bannerlord.PartyAI.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.UI.Components;

/// <summary>
/// One row in a settings list. A single view model covers every row kind so pages can be plain
/// lists; the prefab shows the control matching <see cref="Kind"/>. Values apply immediately.
/// </summary>
public sealed class SettingRowVM : ViewModel
{
    public enum RowKind
    {
        Header,
        Toggle,
        Number,
        Selector,
        Hotkey,
        Action,
        Info
    }

    private Func<bool>? _getBool;
    private Action<bool>? _setBool;
    private Func<int>? _getInt;
    private Action<int>? _setInt;
    private Func<int, string>? _formatInt;
    private Func<Hotkey>? _getHotkey;
    private Action<Hotkey>? _setHotkey;
    private Action? _action;
    private Func<string>? _getInfo;
    private Func<bool>? _isEnabled;
    private Func<int>? _getSelectedIndex;
    private bool _syncingSelector;

    private bool _isSelected;
    private int _value;
    private string _valueText = string.Empty;
    private string _modifierText = string.Empty;
    private string _keyText = string.Empty;
    private bool _isEnabledValue = true;
    private string _infoText = string.Empty;

    private SettingRowVM(RowKind kind, TextObject label, TextObject? hint)
    {
        Kind = kind;
        Label = label.ToString();
        Hint = new HintViewModel(hint ?? TextObject.GetEmpty());
    }

    // ---- Factories -------------------------------------------------------------------------------

    public static SettingRowVM Header(string idAndText)
        => new SettingRowVM(RowKind.Header, L.T(idAndText), null);

    public static SettingRowVM Toggle(string label, string? hint, Func<bool> get, Action<bool> set, Func<bool>? isEnabled = null)
        => new SettingRowVM(RowKind.Toggle, L.T(label), hint is null ? null : L.T(hint))
        {
            _getBool = get,
            _setBool = set,
            _isEnabled = isEnabled
        }.Refreshed();

    /// <summary>Integer setting shown as a slider. <paramref name="format"/> turns the raw value into display text.</summary>
    public static SettingRowVM Number(
        string label,
        string? hint,
        int min,
        int max,
        Func<int> get,
        Action<int> set,
        Func<int, string>? format = null,
        Func<bool>? isEnabled = null)
        => new SettingRowVM(RowKind.Number, L.T(label), hint is null ? null : L.T(hint))
        {
            Min = min,
            Max = max,
            _getInt = get,
            _setInt = set,
            _formatInt = format ?? (value => value.ToString()),
            _isEnabled = isEnabled
        }.Refreshed();

    /// <summary>Percentage setting stored as a 0..1 float.</summary>
    public static SettingRowVM Percent(string label, string? hint, int min, int max, Func<float> get, Action<float> set, Func<bool>? isEnabled = null)
        => Number(label, hint, min, max,
            () => (int)Math.Round(get() * 100f),
            value => set(value / 100f),
            value => value + "%",
            isEnabled);

    public static SettingRowVM Choice<T>(string label, string? hint, IEnumerable<(T Value, string Text)> options, Func<T> get, Action<T> set, Func<bool>? isEnabled = null)
        where T : notnull
    {
        var row = new SettingRowVM(RowKind.Selector, L.T(label), hint is null ? null : L.T(hint)) { _isEnabled = isEnabled };
        var values = options.Select(option => option.Value).ToList();
        row._getSelectedIndex = () => Math.Max(0, values.FindIndex(value => EqualityComparer<T>.Default.Equals(value, get())));

        // The selector only notifies on a change of index, so the initial index is applied from -1 with
        // the setter suppressed: the model must not be written just because the row was built.
        row.Selector = new SelectorVM<SelectorItemVM>(0, selector =>
        {
            if (!row._syncingSelector && selector.SelectedIndex >= 0 && selector.SelectedIndex < values.Count)
            {
                set(values[selector.SelectedIndex]);
                row.Changed?.Invoke();
            }
        });

        foreach ((T _, string text) in options)
        {
            row.Selector.AddItem(new SelectorItemVM(new TextObject(text)));
        }

        row._syncingSelector = true;
        row.Selector.SelectedIndex = -1;
        row.Selector.SelectedIndex = row._getSelectedIndex();
        row._syncingSelector = false;
        return row.Refreshed();
    }

    /// <summary>Enum-backed dropdown. <paramref name="text"/> labels each enum value.</summary>
    public static SettingRowVM Enum<T>(string label, string? hint, Func<T, string> text, Func<T> get, Action<T> set, Func<bool>? isEnabled = null)
        where T : struct, System.Enum
        => Choice(label, hint, System.Enum.GetValues(typeof(T)).Cast<T>().Select(value => (value, text(value))), get, set, isEnabled);

    /// <summary>Numeric range 0..max shown as a dropdown where 0 reads as "Max" / "Unlimited".</summary>
    public static SettingRowVM Limit(string label, string? hint, int max, string zeroText, Func<int> get, Action<int> set)
    {
        var options = new List<(int, string)> { (0, zeroText) };
        options.AddRange(Enumerable.Range(1, Math.Max(1, max)).Select(value => (value, value.ToString())));
        return Choice(label, hint, options, get, set);
    }

    public static SettingRowVM HotkeyRow(string label, string? hint, Func<Hotkey> get, Action<Hotkey> set, bool hasModifier = true)
        => new SettingRowVM(RowKind.Hotkey, L.T(label), hint is null ? null : L.T(hint))
        {
            _getHotkey = get,
            _setHotkey = set,
            HasModifier = hasModifier
        }.Refreshed();

    public static SettingRowVM Action(string label, string? hint, string buttonText, Action action, Func<bool>? isEnabled = null)
        => new SettingRowVM(RowKind.Action, L.T(label), hint is null ? null : L.T(hint))
        {
            ButtonText = L.S(buttonText),
            _action = action,
            _isEnabled = isEnabled
        }.Refreshed();

    /// <summary>Read-only line of text, refreshed with the page.</summary>
    public static SettingRowVM Info(string label, Func<string> text, string? hint = null)
        => new SettingRowVM(RowKind.Info, L.T(label), hint is null ? null : L.T(hint)) { _getInfo = text }.Refreshed();

    private SettingRowVM Refreshed()
    {
        RefreshValues();
        return this;
    }

    // ---- Bound state -----------------------------------------------------------------------------

    public RowKind Kind { get; }

    [DataSourceProperty] public string Label { get; }
    [DataSourceProperty] public HintViewModel Hint { get; }

    [DataSourceProperty] public bool IsHeader => Kind == RowKind.Header;
    [DataSourceProperty] public bool IsControl => Kind != RowKind.Header;
    [DataSourceProperty] public bool IsToggle => Kind == RowKind.Toggle;
    [DataSourceProperty] public bool IsNumber => Kind == RowKind.Number;
    [DataSourceProperty] public bool IsSelector => Kind == RowKind.Selector;
    [DataSourceProperty] public bool IsHotkey => Kind == RowKind.Hotkey;
    [DataSourceProperty] public bool IsAction => Kind == RowKind.Action;
    [DataSourceProperty] public bool IsInfo => Kind == RowKind.Info;

    [DataSourceProperty]
    public bool IsEnabled
    {
        get => _isEnabledValue;
        private set
        {
            if (value != _isEnabledValue)
            {
                _isEnabledValue = value;
                OnPropertyChangedWithValue(value, nameof(IsEnabled));
            }
        }
    }

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value != _isSelected)
            {
                _isSelected = value;
                _setBool?.Invoke(value);
                OnPropertyChangedWithValue(value, nameof(IsSelected));
                Changed?.Invoke();
            }
        }
    }

    [DataSourceProperty] public int Min { get; private set; }
    [DataSourceProperty] public int Max { get; private set; }

    [DataSourceProperty]
    public int Value
    {
        get => _value;
        set
        {
            value = Math.Max(Min, Math.Min(Max, value));
            if (value != _value)
            {
                _value = value;
                _setInt?.Invoke(value);
                OnPropertyChangedWithValue(value, nameof(Value));
                ValueText = _formatInt?.Invoke(value) ?? value.ToString();
                Changed?.Invoke();
            }
        }
    }

    [DataSourceProperty]
    public string ValueText
    {
        get => _valueText;
        private set
        {
            if (value != _valueText)
            {
                _valueText = value;
                OnPropertyChangedWithValue(value, nameof(ValueText));
            }
        }
    }

    [DataSourceProperty] public SelectorVM<SelectorItemVM>? Selector { get; private set; }

    [DataSourceProperty] public bool HasModifier { get; private set; }

    [DataSourceProperty]
    public string ModifierText
    {
        get => _modifierText;
        private set
        {
            if (value != _modifierText)
            {
                _modifierText = value;
                OnPropertyChangedWithValue(value, nameof(ModifierText));
            }
        }
    }

    [DataSourceProperty]
    public string KeyText
    {
        get => _keyText;
        private set
        {
            if (value != _keyText)
            {
                _keyText = value;
                OnPropertyChangedWithValue(value, nameof(KeyText));
            }
        }
    }

    [DataSourceProperty] public string ButtonText { get; private set; } = string.Empty;

    [DataSourceProperty]
    public string InfoText
    {
        get => _infoText;
        private set
        {
            if (value != _infoText)
            {
                _infoText = value;
                OnPropertyChangedWithValue(value, nameof(InfoText));
            }
        }
    }

    /// <summary>Raised after the user changed the value; lets a page refresh dependent rows.</summary>
    public event Action? Changed;

    // ---- Commands --------------------------------------------------------------------------------

    public void ExecuteAction() => _action?.Invoke();

    /// <summary>Type an exact number instead of dragging the slider.</summary>
    public void ExecuteEditValue()
    {
        if (Kind != RowKind.Number)
        {
            return;
        }

        InformationManager.ShowTextInquiry(new TextInquiryData(
            Label,
            L.T("{=PAI_ENTER_VALUE_RANGE}Enter a value between {MIN} and {MAX}.").SetTextVariable("MIN", Min).SetTextVariable("MAX", Max).ToString(),
            true,
            true,
            L.Game("str_done"),
            L.Game("str_cancel"),
            text =>
            {
                if (int.TryParse(text, out int parsed))
                {
                    Value = parsed;
                }
            },
            null,
            false,
            text => int.TryParse(text, out int parsed) && parsed >= Min && parsed <= Max
                ? new Tuple<bool, string>(true, string.Empty)
                : new Tuple<bool, string>(false, L.S("{=PAI5AWANWod}You must enter a number")),
            null,
            Value.ToString()));
    }

    public void ExecuteEditModifier()
    {
        InputKey[] modifiers =
        [
            InputKey.LeftControl, InputKey.RightControl,
            InputKey.LeftAlt, InputKey.RightAlt,
            InputKey.LeftShift, InputKey.RightShift,
            InputKey.Invalid
        ];
        PickKey(modifiers, key => Apply(_getHotkey!().WithModifier(key)));
    }

    public void ExecuteEditKey()
    {
        var keys = System.Enum.GetValues(typeof(InputKey)).Cast<InputKey>()
            .Where(key => ((int)key >= 12 && (int)key <= 88) || (int)key >= 227)
            .Concat([InputKey.RightAlt, InputKey.RightShift, InputKey.RightControl])
            .Distinct()
            .ToArray();
        PickKey(keys, key => Apply(_getHotkey!().WithKey(key)));
    }

    private void Apply(Hotkey hotkey)
    {
        _setHotkey?.Invoke(hotkey);
        RefreshValues();
        Changed?.Invoke();
    }

    private static void PickKey(IEnumerable<InputKey> keys, Action<InputKey> onPicked)
    {
        List<InquiryElement> elements = keys
            .OrderBy(key => key.ToString())
            .Select(key => new InquiryElement(key, key == InputKey.Invalid ? L.S("{=koX9okuG}None") : key.ToString(), null))
            .ToList();

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            L.S("{=PAIekKoDXkq}Select a Key"),
            string.Empty,
            elements,
            true,
            1,
            1,
            L.Game("str_ok"),
            L.Game("str_cancel"),
            results =>
            {
                if (results.FirstOrDefault()?.Identifier is InputKey key)
                {
                    onPicked(key);
                }
            },
            null,
            isSeachAvailable: true));
    }

    // ---- Refresh ---------------------------------------------------------------------------------

    public override void RefreshValues()
    {
        base.RefreshValues();

        IsEnabled = _isEnabled?.Invoke() ?? true;

        if (_getBool is not null)
        {
            _isSelected = _getBool();
            OnPropertyChangedWithValue(_isSelected, nameof(IsSelected));
        }

        if (_getInt is not null)
        {
            _value = Math.Max(Min, Math.Min(Max, _getInt()));
            OnPropertyChangedWithValue(_value, nameof(Value));
            ValueText = _formatInt?.Invoke(_value) ?? _value.ToString();
        }

        if (Selector is not null && _getSelectedIndex is not null)
        {
            _syncingSelector = true;
            Selector.SelectedIndex = _getSelectedIndex();
            _syncingSelector = false;
        }

        if (_getHotkey is not null)
        {
            Hotkey hotkey = _getHotkey();
            ModifierText = hotkey.Modifier == InputKey.Invalid ? L.S("{=koX9okuG}None") : hotkey.Modifier.ToString();
            KeyText = hotkey.Key.ToString();
        }

        if (_getInfo is not null)
        {
            InfoText = _getInfo();
        }
    }
}
