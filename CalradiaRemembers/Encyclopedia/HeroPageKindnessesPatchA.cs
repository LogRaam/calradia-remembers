// Code written by Gabriel Mailhot, 04/07/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the kindnesses bullet list to the hero encyclopedia page.
    ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageKindnessesPatchB" />,
    ///   so items appear below the "Kindnesses:" label in InfoContainer's vertical stack.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageKindnessesPatchA : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroKindnessesItems";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
