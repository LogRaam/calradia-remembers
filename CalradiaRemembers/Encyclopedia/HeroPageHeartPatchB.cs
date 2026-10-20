// Code written by Gabriel Mailhot, 20/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the "Heart:" header line to the hero page. UIExtenderEx applies this patch BEFORE
    ///   <see cref="HeroPageHeartPatchA" />, so the label appears above the lines.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageHeartPatchB : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroHeartLabel";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
