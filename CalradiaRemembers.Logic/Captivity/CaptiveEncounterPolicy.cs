// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>
   ///   The pure decisions behind a captive encounter: the per-tick probability a captor acts
   ///   (<see cref="EncounterProbability" />) and the intent the scene takes
   ///   (<see cref="SelectIntent" />). The host owns the roll, the witnesses, and every engine guard.
   ///   Extended 01/07/2026 with the bandit-captor rolls (<see cref="SelectBanditIntent" />,
   ///   <see cref="BanditCollectiveChance" />, <see cref="ShouldBanditSceneBeCollective" />,
   ///   <see cref="BanditWitnessCount" />), moved here from
   ///   <c>CalradiaRemembers.Letters.CaptiveEncounterService</c> next to the lord-captor rolls they mirror.
   ///   Also 01/07/2026: <see cref="SelectIntent" /> turned from a deterministic priority ladder into a
   ///   weighted draw, so the same captor no longer runs the same scene every time (replayability).
   /// </summary>
   public static class CaptiveEncounterPolicy
   {
      private const float BaseProbabilityPerDay = 0.15f;
      private const float KinkBonusDominance = 0.05f;
      private const float KinkBonusFreeUse = 0.10f;
      private const float KinkBonusMasterSlave = 0.05f;

      private const float KinkBonusPowerImbalance = 0.10f;
      private const float KinkBonusSadism = 0.05f;
      private const float MaxBaseProbability = 0.60f;
      private const float MaxTotalProbability = 0.70f;

      // ── SelectIntent weighted draw (01/07/2026) ─────────────────────────
      private const int IntentFloorWeight = 10; // Interrogation/PersonalDesire/Domination/Torture: never zero.
      private const int TortureKinkBoost = 15; // per matching cruelty kink (Sadism/ImpactPlay/FearPlay)
      private const int DominationKinkBoost = 12; // per matching ownership kink
      private const int DesireKinkBoost = 12; // per matching desire/use kink
      private const int TrainingWeight = 30; // only when the Training kink is present, otherwise 0
      private const int RewardBaseWeight = 15; // only once relation clears RewardRelationThreshold
      private const int RewardPraiseBoost = 20;
      private const int RewardRelationThreshold = 15;
      private const int ContemptThreshold = -15;
      private const int ContemptTortureBoost = 40;
      private const int WarmthThreshold = 20;
      private const int WarmthRewardBoost = 15;
      private const int WarmthPersonalDesireBoost = 15;
      private const int WarmthTortureReduction = 15;

      /// <summary>
      ///   Chance, this tick, that the captor initiates an encounter: a base that climbs with days
      ///   in captivity (capped), plus bonuses for the kinks that make a captor more predatory, the
      ///   whole capped again. Pure over the captor's kink set.
      /// </summary>
      public static float EncounterProbability(int daysInCaptivity, IReadOnlyList<Kink> kinks)
      {
         float baseProb = Math.Min(daysInCaptivity * BaseProbabilityPerDay, MaxBaseProbability);

         var bonus = 0f;

         if (kinks == null) return Math.Min(baseProb + bonus, MaxTotalProbability);
         if (kinks.Contains(Kink.PowerImbalance)) bonus += KinkBonusPowerImbalance;
         if (kinks.Contains(Kink.Dominance)) bonus += KinkBonusDominance;
         if (kinks.Contains(Kink.Sadism)) bonus += KinkBonusSadism;
         if (kinks.Contains(Kink.FreeUse)) bonus += KinkBonusFreeUse;
         if (kinks.Contains(Kink.MasterSlave)) bonus += KinkBonusMasterSlave;

         return Math.Min(baseProb + bonus, MaxTotalProbability);
      }

      /// <summary>
      ///   Picks the captor's intent from their kinks and their regard for the prisoner as a WEIGHTED
      ///   draw over the six lord/sexual intents, not a deterministic ladder: the same captor can run a
      ///   different scene from one encounter to the next. Kinks and relation are LEANINGS (they boost
      ///   an intent's weight) rather than hard gates, except for two true gates that keep the fiction
      ///   coherent: Reward needs at least a lukewarm relation to be reachable at all (a captor who
      ///   despises the prisoner does not reward them), and Training needs the Training kink (nothing
      ///   to condition otherwise). Interrogation/PersonalDesire/Domination/Torture always keep a floor
      ///   weight above zero, so no valid intent is ever impossible for a given captor.
      ///   <paramref name="rollPercent" /> is called once with the total weight and expected to return a
      ///   0..(total-1) draw (mirrors <see cref="SelectBanditIntent" />'s dice).
      ///   <para>
      ///     <paramref name="playerIsCompatible" /> (ratified 2026-07-23): orientation compatibility used to
      ///     be a WALL at the top of the profiled-captor path, before this method was even reached, so a male
      ///     player held by a straight male lord (the majority captor demographic) got NO scenes at all, and
      ///     an asexual captor could not even interrogate. It is now a FILTER here, the same shape the bandit
      ///     path always had: incompatible, the sexual/CNC intents (PersonalDesire, Domination, Torture,
      ///     Training, Reward) drop to zero weight and Interrogation carries the scene; compatible, nothing
      ///     changes. Torture sits on the gated side by design: at Hardcore its framing is sexualised, so
      ///     prudence keeps it behind the gate (Gabriel, 2026-07-23).
      ///   </para>
      /// </summary>
      public static CaptiveSceneIntent SelectIntent(IReadOnlyList<Kink> kinks, int relation, Func<int, int> rollPercent, bool playerIsCompatible = true)
      {
         bool Has(Kink k)
         {
            return kinks != null && kinks.Contains(k);
         }

         if (!playerIsCompatible) return CaptiveSceneIntent.Interrogation;

         int interrogation = IntentFloorWeight;
         int personalDesire = IntentFloorWeight;
         int domination = IntentFloorWeight;
         int torture = IntentFloorWeight;
         int training = Has(Kink.Training) ? TrainingWeight : 0;
         int reward = relation >= RewardRelationThreshold ? RewardBaseWeight : 0;

         // Cruelty / pain kinks lean the captor toward torture, without ruling anything else out.
         if (Has(Kink.Sadism)) torture += TortureKinkBoost;
         if (Has(Kink.ImpactPlay)) torture += TortureKinkBoost;
         if (Has(Kink.FearPlay)) torture += TortureKinkBoost;

         // Ownership / power kinks lean toward domination.
         if (Has(Kink.PowerImbalance)) domination += DominationKinkBoost;
         if (Has(Kink.Dominance)) domination += DominationKinkBoost;
         if (Has(Kink.MasterSlave)) domination += DominationKinkBoost;
         if (Has(Kink.Objectification)) domination += DominationKinkBoost;
         if (Has(Kink.Degradation)) domination += DominationKinkBoost;

         // Desire / use kinks lean toward personal desire.
         if (Has(Kink.Dominance)) personalDesire += DesireKinkBoost;
         if (Has(Kink.FreeUse)) personalDesire += DesireKinkBoost;
         if (Has(Kink.Breeding)) personalDesire += DesireKinkBoost;
         if (Has(Kink.OrgasmControl)) personalDesire += DesireKinkBoost;
         if (Has(Kink.PetPlay)) personalDesire += DesireKinkBoost;

         // A praise kink sharpens reward, but only once relation already opened the door to it.
         if (reward > 0 && Has(Kink.Praise)) reward += RewardPraiseBoost;

         // Relation modulates the whole spread: real contempt makes reward unthinkable and leans
         // heavily toward torture (never certain); real warmth opens up reward/personal desire and
         // cools torture, but never below the floor (the floor invariant holds regardless of regard).
         if (relation <= ContemptThreshold)
         {
            torture += ContemptTortureBoost;
            reward = 0;
         }
         else if (relation >= WarmthThreshold)
         {
            reward += WarmthRewardBoost;
            personalDesire += WarmthPersonalDesireBoost;
            torture = Math.Max(IntentFloorWeight, torture - WarmthTortureReduction);
         }

         int[] weights = {interrogation, personalDesire, domination, torture, training, reward};
         CaptiveSceneIntent[] intents = {
            CaptiveSceneIntent.Interrogation, CaptiveSceneIntent.PersonalDesire, CaptiveSceneIntent.Domination,
            CaptiveSceneIntent.Torture, CaptiveSceneIntent.Training, CaptiveSceneIntent.Reward
         };

         var total = 0;
         for (var i = 0; i < weights.Length; i++) total += weights[i];

         int roll = rollPercent(total);
         var cumulative = 0;
         for (var i = 0; i < weights.Length; i++)
         {
            cumulative += weights[i];
            if (roll < cumulative) return intents[i];
         }

         return CaptiveSceneIntent.Interrogation; // unreachable: roll is always < total by contract
      }

      // ── Bandit captor rolls ──────────────────────────────────────────────

      /// <summary>
      ///   Picks a faceless bandit captor's intent. Spoil-of-war (the sexual CNC intent) is
      ///   GENDER-WEIGHTED: near-nil for a male prisoner, common for a female, and only when the
      ///   captor's orientation is compatible; otherwise a non-sexual menace intent (extortion /
      ///   intimidation / revenge). <paramref name="rollPercent" /> is called with 100 and returns a
      ///   0..99 draw (mirrors the host's dice); called once for the spoil check and, only if that
      ///   branch is not taken, once more for the menace ladder.
      /// </summary>
      public static CaptiveSceneIntent SelectBanditIntent(bool playerIsFemale, RomanticProfile captorRomantic, Func<int, int> rollPercent)
      {
         int spoilChance = playerIsFemale
            ? 50
            : 3;

         if (rollPercent(100) < spoilChance && captorRomantic != null && captorRomantic.IsCompatibleWith(playerIsFemale))
            return CaptiveSceneIntent.PersonalDesire;

         int roll = rollPercent(100);

         if (roll < 45) return CaptiveSceneIntent.Extortion;
         if (roll < 75) return CaptiveSceneIntent.Intimidation;

         return CaptiveSceneIntent.Revenge;
      }

      /// <summary>
      ///   The chance (0..99 threshold) that the chief's men join a bandit scene: far more likely
      ///   for a spoil-of-war (PersonalDesire) scene than for a shakedown.
      /// </summary>
      public static int BanditCollectiveChance(CaptiveSceneIntent intent)
         => intent == CaptiveSceneIntent.PersonalDesire
            ? 55
            : 25;

      /// <summary>
      ///   How many extra bandits (beyond the chief) could plausibly join a collective scene: never
      ///   more than actually survived the capture, since the chief is one of the band's fighters.
      /// </summary>
      public static int BanditCollectiveParticipantCap(int fightersInBand)
         => Math.Max(0, fightersInBand - 1);

      /// <summary>
      ///   Whether the scene is collective: needs at least one extra bandit AND a roll under
      ///   <see cref="BanditCollectiveChance" />. The roll is only drawn when an extra bandit exists
      ///   (short-circuit, mirroring the host's original single-draw behaviour).
      /// </summary>
      public static bool ShouldBanditSceneBeCollective(CaptiveSceneIntent intent, int extraBandits, Func<int, int> rollPercent)
         => extraBandits >= 1 && rollPercent(100) < BanditCollectiveChance(intent);

      /// <summary>How many of the chief's men actually appear onscreen: capped at 2, never more than exist.</summary>
      public static int BanditWitnessCount(int extraBandits) => Math.Min(2, extraBandits);
   }
}