// Code written by Gabriel Mailhot, 03/07/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the "Grievances:" header line to the hero encyclopedia page.
    ///   UIExtenderEx applies this patch BEFORE <see cref="HeroPageGrievancesPatchA" />,
    ///   so the label appears above the bullet list in InfoContainer's vertical stack.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageGrievancesPatchB : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroGrievancesLabel";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
