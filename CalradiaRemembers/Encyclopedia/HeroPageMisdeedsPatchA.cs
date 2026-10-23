// Code written by Gabriel Mailhot, 23/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the Misdeeds lines (what the player did of their own will, on the player's page, Hardcore only) to the hero page.
    ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageMisdeedsPatchB" />, so the lines appear below the label.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageMisdeedsPatchA : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroMisdeedsItems";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
