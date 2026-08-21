// Code written by Gabriel Mailhot, 20/08/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the nemesis dossier bullet list to the hero encyclopedia page.
    ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageNemesisPatchB" />,
    ///   so items appear below the "Nemesis:" label in InfoContainer's vertical stack.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageNemesisPatchA : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroNemesisItems";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
