// Code written by Gabriel Mailhot, 04/07/2026.

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
   /// <summary>
   ///   Inserts a single line into the hero encyclopedia page, alongside the personal-relation
   ///   line, showing the NPC's Bearing (Trust/Respect/Fear, numberless) toward the player. The
   ///   content lives in GUI/Prefabs/CR_HeroBearing.xml and binds to <c>@CalradiaBearing</c> /
   ///   <c>@CalradiaBearingLabel</c> provided by <see cref="EncyclopediaHeroPageBearingMixin" />.
   /// </summary>
   [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
   internal sealed class HeroPageBearingPatch : PrefabExtensionInsertPatch
   {
      [PrefabExtensionFileName(true)]
      public string FileName => "CR_HeroBearing";

      public override int Index => 0;
      public override InsertType Type => InsertType.Append;
   }
}
