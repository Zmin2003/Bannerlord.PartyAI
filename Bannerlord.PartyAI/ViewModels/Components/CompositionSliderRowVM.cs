using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Components;

public class CompositionSliderRowVM : ViewModel
{
    public event Action<CompositionSliderRowVM>? UserChangedValue;

    private bool _isProgrammatic;
    private int _value;
    private bool _isLocked;
    private bool _isSliderEnabled;
    private bool _isLockToggleable;
    private HintViewModel _lockHint;

    public CompositionSliderRowVM(int initialValue, bool enabled, string icon)
    {
        Value = initialValue;
        Icon = icon;
        IsLockToggleable = enabled;
        IsLocked = !IsLockToggleable;
        IsSliderEnabled = !IsLocked;

        LockHint = IsLockToggleable
            ? new HintViewModel()
            : new HintViewModel(
                new TextObject("{=PAI_locked_formation}This formation class is not used by this party's template."));
    }

    [DataSourceProperty]
    public string Icon { get; }

    [DataSourceProperty]
    public int Value
    {
        get => _value;
        set
        {
            if (value != _value)
            {
                _value = value;
                OnPropertyChangedWithValue(value, nameof(Value));
                OnPropertyChanged(nameof(Percentage));
                if (!_isProgrammatic)
                {
                    UserChangedValue?.Invoke(this);
                }
            }
        }
    }

    [DataSourceProperty]
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            IsSliderEnabled = !value;

            if (value != _isLocked)
            {
                _isLocked = value;
                OnPropertyChangedWithValue(value, nameof(IsLocked));
            }
        }
    }

    [DataSourceProperty]
    public bool IsSliderEnabled
    {
        get => _isSliderEnabled;
        set
        {
            if (value != _isSliderEnabled)
            {
                _isSliderEnabled = value;
                OnPropertyChangedWithValue(value, nameof(IsSliderEnabled));
            }
        }
    }

    [DataSourceProperty]
    public bool IsLockToggleable
    {
        get => _isLockToggleable;
        set
        {
            if (!value)
            {
                IsLocked = true;
            }

            if (value != _isLockToggleable)
            {
                _isLockToggleable = value;
                OnPropertyChangedWithValue(value, nameof(IsLockToggleable));
            }
        }
    }

    [DataSourceProperty]
    public string Percentage => $"{Value}%";

    [DataSourceProperty]
    public HintViewModel LockHint
    {
        get => _lockHint;
        set
        {
            if (value != _lockHint)
            {
                _lockHint = value;
                OnPropertyChangedWithValue(value, nameof(LockHint));
            }
        }
    }

    public void SetValueSilently(int value)
    {
        _isProgrammatic = true;
        Value = value;
        _isProgrammatic = false;
    }
}
