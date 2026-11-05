using System.Globalization;

namespace S4.Installer.Probe;

/// <summary>
/// Design D4: the app language without package identity. The Windows App SDK's
/// Microsoft.Windows.Globalization override replaces Windows.Globalization's, which needs identity.
/// </summary>
public static class Language
{
    public static void Apply(string tag)
    {
        var culture = CultureInfo.GetCultureInfo(tag);
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = culture.Name;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
