// Code written by Gabriel Mailhot, 24/08/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the standing-accords bullet list to the hero encyclopedia page. UIExtenderEx applies this patch
    ///   AFTER <see cref="HeroPageStandingAccordsPatchB" />, so the list renders below the "Standing accords:" label.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageStandingAccordsPatchA : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroStandingAccordsItems";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
