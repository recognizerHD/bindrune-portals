using Jotunn.Entities;
using Jotunn.Managers;

namespace Bindrune.Config
{
    /// <summary>
    /// The mod's own display strings, registered as localisation tokens rather than written inline.
    /// <para>
    /// English only for now. The point of routing even one string through here is that the next one
    /// has somewhere to go: a mod that hardcodes its text is one nobody can translate without
    /// patching it, and Valheim's audience is not mostly English-speaking.
    /// </para>
    /// <para>
    /// Console commands and the sync log are deliberately <em>not</em> localised. They are developer
    /// output, and a bug report is more useful in the language the person reading it wrote the code in.
    /// </para>
    /// </summary>
    internal static class Translations
    {
        internal static void Add()
        {
            // GetLocalization hands back this mod's own CustomLocalization, already registered.
            LocalizationManager.Instance.GetLocalization()
                .AddTranslation("English", "bindrune_hover_aim", "Aim portal");
        }
    }
}
