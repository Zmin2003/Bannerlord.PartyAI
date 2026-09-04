using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.Core;

/// <summary>
/// Localization shorthand. Every user-facing string in the mod goes through here so the
/// string files can be regenerated from the code.
/// </summary>
internal static class L
{
    /// <summary>Creates a TextObject from a "{=id}text" literal.</summary>
    public static TextObject T(string idAndText) => new(idAndText);

    /// <summary>Creates a TextObject and sets one variable.</summary>
    public static TextObject T(string idAndText, string variable, object value)
        => new TextObject(idAndText).SetTextVariable(variable, value?.ToString() ?? string.Empty);

    /// <summary>Resolves a "{=id}text" literal to its localized string.</summary>
    public static string S(string idAndText) => new TextObject(idAndText).ToString();

    /// <summary>Resolves a vanilla game text by its str_ key.</summary>
    public static string Game(string key) => GameTexts.FindText(key).ToString();

    public static TextObject Empty => TextObject.GetEmpty();
}
