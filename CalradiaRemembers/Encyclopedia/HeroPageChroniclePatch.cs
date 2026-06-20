// Code written by Gabriel Mailhot, 19/06/2026. v1.2 Phase 2b — chronicle button injection.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Inserts the "Read the Chronicle" button into the hero encyclopedia page's info block.
    ///   Content lives in GUI/Prefabs/CR_HeroChronicleButton.xml and binds to <c>@IsPlayerPage</c> /
    ///   <c>@ExecuteOpenChronicle</c> from <see cref="EncyclopediaHeroPageChronicleMixin"/>.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPageChroniclePatch : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;
        public override int Index => 0;

        [PrefabExtensionInsertPatch.PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroChronicleButton";
    }
}
