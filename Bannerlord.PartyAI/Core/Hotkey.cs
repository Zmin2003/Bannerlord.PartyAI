using TaleWorlds.InputSystem;

namespace Bannerlord.PartyAI.Core;

/// <summary>
/// A key combination consisting of an optional modifier and a main key.
/// </summary>
public readonly struct Hotkey
{
    public InputKey Modifier { get; }
    public InputKey Key { get; }

    public Hotkey(InputKey modifier, InputKey key)
    {
        Modifier = modifier;
        Key = key;
    }

    public Hotkey(InputKey key) : this(InputKey.Invalid, key)
    {
    }

    public bool HasModifier => Modifier != InputKey.Invalid;

    public Hotkey WithModifier(InputKey modifier) => new(modifier, Key);

    public Hotkey WithKey(InputKey key) => new(Modifier, key);

    /// <summary>True while the whole combination is held down.</summary>
    public bool IsDown()
        => Key != InputKey.Invalid
            && Input.IsKeyDown(Key)
            && (!HasModifier || Input.IsKeyDown(Modifier));

    /// <summary>True on the frame the main key is pressed while the modifier is held.</summary>
    public bool IsPressed()
        => Key != InputKey.Invalid
            && Input.IsKeyPressed(Key)
            && (!HasModifier || Input.IsKeyDown(Modifier));

    public override string ToString()
        => HasModifier ? $"{Modifier}+{Key}" : Key.ToString();
}
