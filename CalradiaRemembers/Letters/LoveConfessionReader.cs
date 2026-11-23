// Code written by Gabriel Mailhot, 23/11/2026.
// The love letter (Gabriel, 23/11/2026: "Une lettre d'aveux ou d'amour serait tres bien"; "Il faudrait qu'il y ait une
// forme d'affinite; culture, traits, etc."). Reads, off the campaign, what LoveConfessionLetterPolicy judges: the
// register the conversation prompt would resolve from the same facts (LlmServices gives the prompt the same bar), the
// game's own courtship attraction, culture, personality traits, marriage, and writes the Context the letter is written
// from. The collector (LetterTriggerEvaluator) and the delivery check (LetterIntentionResolver) both read it here.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Letters;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>The campaign side of the love letter: facts, affinity and the Context it is written from.</summary>
   internal static class LoveConfessionReader
   {
      /// <summary>
      ///   The five personality traits the game gives every hero, with the words a letter admires them in. Read on each call,
      ///   never held in a static field: DefaultTraits lives in the engine's registry, which a static initializer may touch
      ///   before the campaign exists (EngineRegistryInitializationGuardTests).
      /// </summary>
      private static (TraitObject Trait, string Admired)[] Personality => new[] {
         (DefaultTraits.Honor, "honour"),
         (DefaultTraits.Mercy, "mercy"),
         (DefaultTraits.Valor, "courage"),
         (DefaultTraits.Generosity, "generosity"),
         (DefaultTraits.Calculating, "a shrewd mind")
      };

      /// <summary>The register the conversation prompt would give this character now: the very same rule and the same bar.</summary>
      internal static RomanticRegister RegisterOf(Hero hero, NpcProfile profile)
      {
         Hero player = Hero.MainHero;
         if (hero == null || profile?.Romantic == null || player == null) return RomanticRegister.Platonic;

         return RomanticRegisterPolicy.Resolve(new RomanticRegisterFacts {
            AdultContentEnabled = ModSettings.AdultContentLevel != AdultContentLevel.Off,
            PlayerIsCompatible = profile.Romantic.IsCompatibleWith(player.IsFemale),
            RegardWithPlayer = profile.ReputationWithPlayer,
            AttractionToPlayer = profile.Romantic.AttractionToPlayer,
            Status = profile.Romantic.Status,
            NpcIsPlayerSpouse = hero.Spouse == player,
            DeclaredAffectionBar = CompanionSecretAffectionPolicy.MinRegardFor(ModSettings.RelationWarmth)
         });
      }

      /// <summary>What decides whether they may write at all.</summary>
      internal static LoveConfessionFacts FactsFor(Hero hero, NpcProfile profile) => new() {
         Register = RegisterOf(hero, profile),
         Status = profile?.Romantic?.Status ?? RomanticStatus.None,
         IsPlayerCompanion = hero is {IsPlayerCompanion: true},
         HasMet = hero is {HasMet: true},
         AlreadyConfessed = profile?.SentLetters?.Any(l => l != null && l.Reason == LetterReason.LoveConfession) == true
      };

      /// <summary>Whether the feeling that wrote their letter still stands (read again at delivery).</summary>
      internal static bool StillHeartfelt(Hero hero, NpcProfile profile)
         => LoveConfessionLetterPolicy.IsStillHeartfelt(RegisterOf(hero, profile), profile?.Romantic?.Status ?? RomanticStatus.None);

      /// <summary>What draws them to the player.</summary>
      internal static LoveAffinityFacts AffinityFor(Hero hero, NpcProfile profile)
      {
         Hero player = Hero.MainHero;
         if (hero == null || player == null) return null;

         int agreement = 0;
         foreach ((TraitObject trait, string _) in Personality)
         {
            int theirs = hero.GetTraitLevel(trait), mine = player.GetTraitLevel(trait);
            if (theirs == 0 || mine == 0) continue;
            agreement += theirs > 0 == mine > 0 ? 1 : -1;
         }

         return new LoveAffinityFacts {
            GameAttractionPercent = Campaign.Current?.Models?.RomanceModel?.GetAttractionValuePercentage(hero, player) ?? 50,
            SharesCulture = hero.Culture != null && hero.Culture == player.Culture,
            TraitAgreement = agreement,
            AttractionToPlayer = profile?.Romantic?.AttractionToPlayer ?? 0,
            IsMarried = hero.Spouse != null && hero.Spouse != player
         };
      }

      /// <summary>The Context the letter is written from: how they stand, what draws them, and who they are bound to.</summary>
      internal static string ContextFor(Hero hero, NpcProfile profile, LoveAffinityFacts affinity)
      {
         Hero player = Hero.MainHero;
         var lines = new List<string> {$"Your feeling toward the player right now: {ThirdPartyDescriptor.DescribeRelation(profile?.ReputationWithPlayer ?? 0)}."};

         if (affinity != null)
         {
            if (affinity.GameAttractionPercent > LoveConfessionLetterPolicy.DrawnToYouAbove) lines.Add("You were drawn to them from the first.");
            else if (affinity.GameAttractionPercent <= LoveConfessionLetterPolicy.NotMyTypeAtOrBelow) lines.Add("They were never the kind you thought you would love; it grew in spite of that.");
            if (affinity.SharesCulture && hero?.Culture != null) lines.Add($"You share their blood and ways: both of you are of the {hero.Culture.Name}.");
         }

         if (hero != null && player != null)
         {
            List<string> alike = Personality.Where(p => hero.GetTraitLevel(p.Trait) > 0 && player.GetTraitLevel(p.Trait) > 0).Select(p => p.Admired).ToList();
            if (alike.Count > 0) lines.Add($"You admire in them what you hold yourself: {string.Join(", ", alike)}.");

            if (hero.Spouse != null && hero.Spouse != player)
               lines.Add($"You are married to {hero.Spouse.Name}. This feeling is yours alone and hidden: write with discretion, knowing what it would cost if this letter were found.");
            if (player.Spouse != null && player.Spouse != hero)
               lines.Add($"The player is married to {player.Spouse.Name}, and you know it.");
         }

         return string.Join(" ", lines);
      }
   }
}
