// Code written by Gabriel Mailhot, 23/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the "Misdeeds:" header line to the hero page. UIExtenderEx applies this patch BEFORE
    ///   <see cref="HeroPageMisdeedsPatchA" />, so the label appears above the lines.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageMisdeedsPatchB : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroMisdeedsLabel";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
