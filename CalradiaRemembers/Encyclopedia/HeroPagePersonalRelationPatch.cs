// Code written by Gabriel Mailhot, 31/05/2026. Sprint 10.5 — encyclopedia integration.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
    /// <summary>
    ///   Inserts a single line into the hero encyclopedia page, just after the vanilla
    ///   Info block, showing the NPC's personal regard for the player. The content
    ///   lives in GUI/Prefabs/CR_HeroPersonalRelation.xml and binds to
    ///   <c>@CalradiaPersonalRelation</c> provided by
    ///   <see cref="EncyclopediaHeroPagePersonalRelationMixin"/>.
    /// </summary>
    [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
    internal sealed class HeroPagePersonalRelationPatch : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;
        public override int Index => 0;

        [PrefabExtensionInsertPatch.PrefabExtensionFileName(true)]
        public string FileName => "CR_HeroPersonalRelation";
    }
}
