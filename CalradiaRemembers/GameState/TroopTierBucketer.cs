// Code written by Gabriel Mailhot, 04/08/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   The engine-coupled half of council lend_troops (COUNCIL_ACTIONS.md's Kimi review, "CouncilTroopLoanPolicy"):
   ///   reads a live <see cref="TroopRoster" /> into the tier-bucketed counts the pure
   ///   <see cref="CouncilTroopLoanPolicy" /> reasons over, and moves the computed loan for real. Kept separate from
   ///   <see cref="CourtActionResolver" />'s own <c>HealthyRegularTroopCount</c>/<c>MoveLowestTierTroops</c>
   ///   (the 1:1 lend_troops verb's own policy, lowest-tier-first, no mixing) because Gabriel's council
   ///   CONDITIONS are a DIFFERENT policy (25% cap, tier mix), never a re-tuning of the 1:1 numbers.
   /// </summary>
   public static class TroopTierBucketer
   {
      /// <summary>
      ///   Design call (no canonical "low/medium/high" split exists in vanilla): CharacterObject.Tier runs
      ///   roughly 1 (raw recruit) to 6+ (elite veteran/noble). Tier 1-2 = Low (recruit/basic), 3-4 = Medium
      ///   (trained regular), 5+ = High (veteran/elite) - a coarse but even three-way split of the usual troop
      ///   tree depth, good enough for "the loan reads as a genuine mixed detachment", not a precision instrument.
      /// </summary>
      public const int LowMaxTier = 2;

      public const int MediumMaxTier = 4;

      /// <summary>Sum of healthy (unwounded) rank-and-file troops in a roster, bucketed by tier. Heroes never count (mirrors CourtActionResolver.HealthyRegularTroopCount).</summary>
      public static TierTroopCounts BucketHealthyTroops(TroopRoster roster)
      {
         if (roster == null) return new TierTroopCounts(0, 0, 0);

         var low = 0;
         var medium = 0;
         var high = 0;

         foreach (TroopRosterElement el in roster.GetTroopRoster())
         {
            if (el.Character is not {IsHero: false}) continue;

            int healthy = Math.Max(0, el.Number - el.WoundedNumber);
            if (healthy <= 0) continue;

            switch (BucketOf(el.Character.Tier))
            {
               case TroopTierBucket.Low: low += healthy; break;
               case TroopTierBucket.Medium: medium += healthy; break;
               default: high += healthy; break;
            }
         }

         return new TierTroopCounts(low, medium, high);
      }

      /// <summary>Which bucket a raw CharacterObject.Tier falls into (see this class's own header for the cutoffs).</summary>
      public static TroopTierBucket BucketOf(int tier)
         => tier <= LowMaxTier
            ? TroopTierBucket.Low
            : tier <= MediumMaxTier
               ? TroopTierBucket.Medium
               : TroopTierBucket.High;

      /// <summary>
      ///   Moves up to <paramref name="wanted" /> healthy regular troops (never heroes) per tier bucket, lowest
      ///   tier within each bucket first (mirrors CourtActionResolver.MoveLowestTierTroops's own within-bucket
      ///   order), from <paramref name="from" />'s roster into <paramref name="to" />'s. Returns how many
      ///   actually moved PER BUCKET (may be less than asked if the roster shrank since the loan was computed -
      ///   the caller re-reads live state here, it does not trust the earlier count blindly, "the bridge is law").
      /// </summary>
      public static TierTroopCounts MoveLoan(MobileParty from, MobileParty to, TierTroopCounts wanted)
      {
         if (from?.MemberRoster == null || to?.MemberRoster == null) return new TierTroopCounts(0, 0, 0);

         int low = MoveBucket(from, to, TroopTierBucket.Low, wanted.Low);
         int medium = MoveBucket(from, to, TroopTierBucket.Medium, wanted.Medium);
         int high = MoveBucket(from, to, TroopTierBucket.High, wanted.High);

         return new TierTroopCounts(low, medium, high);
      }

      #region private

      private static int MoveBucket(MobileParty from, MobileParty to, TroopTierBucket bucket, int count)
      {
         if (count <= 0) return 0;

         var moved = 0;
         List<TroopRosterElement> candidates = from.MemberRoster.GetTroopRoster()
                                                    .Where(el => el.Character is {IsHero: false} && el.Number - el.WoundedNumber > 0
                                                                 && BucketOf(el.Character.Tier) == bucket)
                                                    .OrderBy(el => el.Character.Tier)
                                                    .ToList();

         foreach (TroopRosterElement el in candidates)
         {
            if (moved >= count) break;

            int healthy = el.Number - el.WoundedNumber;
            int take = Math.Min(healthy, count - moved);

            if (take <= 0) continue;

            from.MemberRoster.AddToCounts(el.Character, -take);
            to.MemberRoster.AddToCounts(el.Character, take);
            moved += take;
         }

         return moved;
      }

      #endregion
   }

   /// <summary>The three coarse troop-quality buckets council lend_troops reasons over (see TroopTierBucketer's own header).</summary>
   public enum TroopTierBucket
   {
      Low,
      Medium,
      High
   }
}
