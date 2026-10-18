// Code written by Gabriel Mailhot, 17/10/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the houses at odds and of affinity to the hero encyclopedia page.
    ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageRegardHousesPatchB" />,
    ///   so items appear below the "Houses:" label in InfoContainer's vertical stack.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageRegardHousesPatchA : PrefabExtensionInsertPatch
    {
        [PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroRegardHousesItems";

        public override int Index => 0;
        public override InsertType Type => InsertType.Append;
    }
}
