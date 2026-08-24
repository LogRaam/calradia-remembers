// Code written by Gabriel Mailhot, 24/08/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the "Standing accords:" header line to the hero encyclopedia page.
    ///   UIExtenderEx applies this patch BEFORE <see cref="HeroPageStandingAccordsPatchA" />,
    ///   so the label appears above the bullet list in InfoContainer's vertical stack.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageStandingAccordsPatchB : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroStandingAccordsLabel";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
