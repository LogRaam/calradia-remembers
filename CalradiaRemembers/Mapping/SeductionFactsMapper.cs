// Code written by Gabriel Mailhot, 26/08/2026.
// Stolen Love pillar: the single seam that turns live game state (traits, the NPC's relation to their own
// spouse, shared children, the mod's regard ledger) into the pure SeductionFacts the SeductionEligibilityPolicy
// judges. Shared by EncounterContextBuilder (the per-conversation stance) and NpcMarriageEstrangementBehavior
// (which needs the same verdict's Difficulty to pace the estrangement), so both read seducibility from ONE place.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Builds <see cref="SeductionFacts" /> for a married NPC from live game state. The caller ensures the hero has a living spouse.</summary>
   public static class SeductionFactsMapper
   {
      /// <summary>
      ///   The seduction facts for <paramref name="hero" /> (married to a third party): their relation to their own
      ///   spouse (marriage happiness), whether they share children with that spouse, their Honor/Calculating
      ///   traits, and the player's regard. Marriage duration is left at 0 (the engine does not expose it; a minor,
      ///   capped factor in the policy).
      /// </summary>
      public static SeductionFacts For(Hero hero, NpcProfile profile)
         => new SeductionFacts {
            NpcSpouseRelation = PairBondReader.Points(hero, hero.Spouse),
            HasChildrenTogether = hero.Children != null
               && hero.Children.Any(c => c != null && (c.Father == hero.Spouse || c.Mother == hero.Spouse)),
            MarriageDurationDays = 0,
            NpcHonor = hero.GetTraitLevel(DefaultTraits.Honor),
            NpcCalculating = hero.GetTraitLevel(DefaultTraits.Calculating),
            RegardWithPlayer = profile?.ReputationWithPlayer ?? 0
         };
   }
}
