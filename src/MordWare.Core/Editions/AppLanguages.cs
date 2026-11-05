using System.Globalization;

namespace MordWare.Core.Editions;

/// <summary>
/// The languages mordWare supports. The app's selected language, never the OS, drives every
/// displayed text, date, number and money value (CLAUDE.md guardrail 3). Until the foundation change
/// adds a language setting, the selected language is <see cref="Default"/>.
/// </summary>
public static class AppLanguages
{
    /// <summary>US English (always US spelling).</summary>
    public const string English = "en-US";

    /// <summary>Italian.</summary>
    public const string Italian = "it-IT";

    /// <summary>The language used until the user picks one.</summary>
    public const string Default = English;

    /// <summary>Every supported language tag.</summary>
    public static IReadOnlyList<string> Supported { get; } = [English, Italian];

    /// <summary>Returns the supported language tag matching <paramref name="tag"/>, or <see cref="Default"/>.</summary>
    public static string Normalize(string? tag) =>
        Supported.FirstOrDefault(s => string.Equals(s, tag, StringComparison.OrdinalIgnoreCase)) ?? Default;

    /// <summary>The culture used for formatting in the given language.</summary>
    public static CultureInfo Culture(string? tag) => CultureInfo.GetCultureInfo(Normalize(tag));
}
