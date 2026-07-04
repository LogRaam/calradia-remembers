// Code written by Gabriel Mailhot, 04/07/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the "Kindnesses:" header line to the hero encyclopedia page.
    ///   UIExtenderEx applies this patch BEFORE <see cref="HeroPageKindnessesPatchA" />,
    ///   so the label appears above the bullet list in InfoContainer's vertical stack.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageKindnessesPatchB : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroKindnessesLabel";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
