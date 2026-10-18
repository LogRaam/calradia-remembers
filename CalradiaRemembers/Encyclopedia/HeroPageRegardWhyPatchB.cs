// Code written by Gabriel Mailhot, 17/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the "Why:" header line to the hero encyclopedia page.
    ///   UIExtenderEx applies this patch BEFORE <see cref="HeroPageRegardWhyPatchA" />,
    ///   so the label appears above the bullet list in InfoContainer's vertical stack.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageRegardWhyPatchB : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroRegardWhyLabel";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
