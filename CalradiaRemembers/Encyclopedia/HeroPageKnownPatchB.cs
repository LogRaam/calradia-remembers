// Code written by Gabriel Mailhot, 27/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the "What You Know:" header line to the hero page. UIExtenderEx applies this patch BEFORE
    ///   <see cref="HeroPageKnownPatchA" />, so the label appears above the lines.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageKnownPatchB : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroKnownLabel";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
