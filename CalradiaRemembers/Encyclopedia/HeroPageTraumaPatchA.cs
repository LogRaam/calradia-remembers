// Code written by Gabriel Mailhot, 20/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the Trauma lines (the aggressions a character lived through, Hardcore only) to the hero page.
    ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageTraumaPatchB" />, so the lines appear below the label.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageTraumaPatchA : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroTraumaItems";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
