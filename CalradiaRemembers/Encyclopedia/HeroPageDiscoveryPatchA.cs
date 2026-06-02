// Code written by Gabriel Mailhot, 01/06/2026. Sprint 10.5 — Discovery system.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Appends the discovery bullet list to the hero encyclopedia page.
    ///   UIExtenderEx applies this patch AFTER <see cref="HeroPageDiscoveryPatchB"/>,
    ///   so items appear below the "Discoveries:" label in InfoContainer's vertical stack.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageDiscoveryPatchA : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;
        public override int Index => 0;

        [PrefabExtensionInsertPatch.PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroDiscoveryItems";
    }
}
