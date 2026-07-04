// Code written by Gabriel Mailhot, 03/07/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the grievances bullet list to the hero encyclopedia page.
    ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageGrievancesPatchB" />,
    ///   so items appear below the "Grievances:" label in InfoContainer's vertical stack.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageGrievancesPatchA : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroGrievancesItems";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
