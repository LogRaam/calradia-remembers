// Code written by Gabriel Mailhot, 04/07/2026.

#region

using CalradiaRemembers.Logic.Appreciations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Appreciations
{
   /// <summary>Bridges a live <see cref="Hero" /> to the pure Appreciations DTOs.</summary>
   internal static class AppreciationBridge
   {
      /// <summary>
      ///   The full candidate-facts bundle a live <c>TryPlant</c> call reasons over. Every live planting
      ///   site names an unambiguous, attributable kindness by construction, so <c>IsAttributable</c> is
      ///   always true here — an unattributable candidate is simply never planted in the first place.
      /// </summary>
      public static AppreciationCandidateFacts BuildCandidateFacts(Hero holder, AppreciationSource source, int magnitude) => new() {
         Source = source,
         Magnitude = magnitude,
         IsAttributable = true,
         Traits = BuildTraitFacts(holder)
      };

      /// <summary>The hero's trait levels relevant to appreciation formation and decay.</summary>
      public static TraitFacts BuildTraitFacts(Hero hero) => new() {
         Generosity = TraitLevel(hero, DefaultTraits.Generosity),
         Mercy = TraitLevel(hero, DefaultTraits.Mercy),
         Calculating = TraitLevel(hero, DefaultTraits.Calculating),
         Honor = TraitLevel(hero, DefaultTraits.Honor),
         Valor = TraitLevel(hero, DefaultTraits.Valor)
      };

      #region private

      private static int TraitLevel(Hero hero, TraitObject trait)
      {
         try { return hero?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
      }

      #endregion
   }
}