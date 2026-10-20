// Code written by Gabriel Mailhot, 20/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the "Trauma:" header line to the hero page. UIExtenderEx applies this patch BEFORE
    ///   <see cref="HeroPageTraumaPatchA" />, so the label appears above the lines.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageTraumaPatchB : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroTraumaLabel";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
