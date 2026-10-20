// Code written by Gabriel Mailhot, 20/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the Heart lines (the bond told to the player, or the recap on the player's own page) to the hero page.
    ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageHeartPatchB" />, so the lines appear below the label.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageHeartPatchA : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroHeartItems";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
