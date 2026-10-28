// Code written by Gabriel Mailhot, 27/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the What You Know lines (what the player has learned of who the character is) to the hero page.
    ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageKnownPatchB" />, so the lines appear below the label.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageKnownPatchA : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroKnownItems";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
