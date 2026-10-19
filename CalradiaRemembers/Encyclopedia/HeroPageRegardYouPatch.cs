// Code written by Gabriel Mailhot, 31/05/2026.
// tashmetu (Regard and Standing, 18/10/2026, ask 6): "the player's own regard first", above the friends and foes and the
// houses. Every hero-page patch appends right after InfoContainer, and UIExtenderEx applies them in type order
// (Assembly.GetTypes), which the compiler lays down SOURCE FILE by source file, alphabetically: the LAST applied sits on
// TOP. As HeroPagePersonalRelationPatch.cs it landed under the houses; this FILE name sorts after HeroPageRegardWhyPatchB.cs
// and before HeroPageStandingAccordsPatchA.cs, so the row reads first in the regard block, under the standing accords.
// Renaming the file moves the row on the page (HeroPageRowOrderTests guards it).

#region

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
   /// <summary>
   ///   Inserts a single line into the hero encyclopedia page, at the head of the regard block (above "Why:" and the
   ///   houses), showing the NPC's personal regard for the player. Its FILE name places it: see the header. The content
   ///   lives in GUI/Prefabs/CR_HeroPersonalRelation.xml and binds to
   ///   <c>@CalradiaPersonalRelation</c> provided by
   ///   <see cref="EncyclopediaHeroPagePersonalRelationMixin" />.
   /// </summary>
   [PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
   internal sealed class HeroPageRegardYouPatch : PrefabExtensionInsertPatch
   {
      [PrefabExtensionFileName(true)]
      public string FileName => "CR_HeroPersonalRelation";

      public override int Index => 0;
      public override InsertType Type => InsertType.Append;
   }
}