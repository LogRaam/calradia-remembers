// Code written by Gabriel Mailhot, 28/10/2026.
// Whose turn it is to come to the player (see WorldInitiativeModel for Gabriel's words). The order of the checks is the
// rule: drop what waited too long; never while the player is busy or has only just settled; the world's impositions pass
// (Gabriel, 28/10/2026: the arrest, the war duty, the ambush, an envoy with a short deadline); then the player's own
// limits (how many at once, the quiet after their own conversation, the gap between two turns); then a family that has
// waited a month has its turn, past the budget ("Il ne faut pas que les priorites empechent les autres"); then the budget;
// then the best rank, drawn by weight among its requests: the family's weight, the request's age, the family's recent
// turn (variety), and the same character not twice in a row.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Initiative
{
   /// <summary>The director's pure rule.</summary>
   public static class WorldInitiativePolicy
   {
      /// <summary>TUNE: a request gains this much weight per day it waits, up to <see cref="AgingCapDays" />.</summary>
      public const double AgingPerDay = 0.15;

      /// <summary>TUNE: days of waiting past which a request gains no more weight.</summary>
      public const int AgingCapDays = 20;

      /// <summary>TUNE: hours during which the character just served is all but passed over.</summary>
      public const double SameCharacterHours = 72;

      /// <summary>TUNE: the weight left to the character just served, so they come back only when nothing else waits.</summary>
      public const double SameCharacterFactor = 0.05;

      /// <summary>The settings a pace fills (the MCM dial); fine settings override them in Custom.</summary>
      public static InitiativeSettings Preset(InitiativePace pace)
      {
         switch (pace)
         {
            case InitiativePace.Rarely:
               return new InitiativeSettings {PerWindow = 2, WindowDays = 10, MinGapHours = 36, SettlingDelayHours = 4, QuietAfterOwnTalkHours = 48};
            case InitiativePace.Often:
               return new InitiativeSettings {PerWindow = 5, WindowDays = 7, MinGapHours = 6, SettlingDelayHours = 1, QuietAfterOwnTalkHours = 12, AtOnce = 2};
            default:
               return new InitiativeSettings();
         }
      }

      /// <summary>What a turn costs from the budget.</summary>
      public static double CostOf(InitiativeRequest r, InitiativeSettings s)
      {
         if (r == null || !r.IsLetter) return 1;

         return (s ?? new InitiativeSettings()).LettersCountAs switch {
            LetterCost.Full => 1,
            LetterCost.None => 0,
            _ => 0.5
         };
      }

      /// <summary>
      ///   Decides whether a request is given its turn now, and which. <paramref name="roll" /> is a value in [0, 1) the
      ///   caller draws, so the rule stays pure.
      /// </summary>
      public static InitiativeDecision Decide(IReadOnlyList<InitiativeRequest> requests, IReadOnlyList<InitiativeGrant> grants,
         InitiativeSettings settings, InitiativeMoment moment, double roll)
      {
         settings ??= new InitiativeSettings();
         moment ??= new InitiativeMoment();
         grants ??= new List<InitiativeGrant>();
         double now = moment.NowHour;

         List<InitiativeRequest> all = (requests ?? new List<InitiativeRequest>()).Where(r => r != null).ToList();
         List<InitiativeRequest> expired = all.Where(r => r.Rank != InitiativeRank.Imposed && now - r.FiledAtHour > settings.ExpireDays * 24.0).ToList();
         // A family weighed at 0 is off; the world's impositions are never switched off here (each has its own option).
         List<InitiativeRequest> live = all.Except(expired).Where(r => r.Rank == InitiativeRank.Imposed || WeightOf(r.Family, settings) > 0).ToList();

         InitiativeDecision None(InitiativeVerdict v) => new() {Verdict = v, Expired = expired};

         if (live.Count == 0) return None(InitiativeVerdict.NothingWaiting);
         if (moment.Busy) return None(InitiativeVerdict.Busy);
         if (moment.HoursSinceSettled < settings.SettlingDelayHours) return None(InitiativeVerdict.Settling);

         if (!settings.UrgentMattersWait)
         {
            InitiativeRequest imposed = live.Where(r => r.Rank == InitiativeRank.Imposed).OrderBy(r => r.FiledAtHour).ThenBy(r => r.Key, StringComparer.Ordinal).FirstOrDefault();
            if (imposed != null) return Grant(imposed, InitiativeGround.Imposed);
         }

         if (moment.ActiveCount >= Math.Max(1, settings.AtOnce)) return None(InitiativeVerdict.AtOnceReached);
         if (moment.HoursSinceOwnTalk < settings.QuietAfterOwnTalkHours) return None(InitiativeVerdict.QuietAfterOwnTalk);

         double? lastTurn = grants.Count == 0 ? null : grants.Max(g => g.GrantedAtHour);
         if (lastTurn.HasValue && now - lastTurn.Value < settings.MinGapHours) return None(InitiativeVerdict.TooSoon);

         // Gabriel's month: a family in the queue that has had no turn for FairTurnDays has its turn now, the longest
         // starved first, past the budget and past any rank.
         InitiativeRequest fair = live
                                  .Select(r => new {r, waited = now - (LastTurnOf(r.Family, grants) ?? moment.KeptSinceHour)})
                                  .Where(x => x.waited >= settings.FairTurnDays * 24.0)
                                  .OrderByDescending(x => x.waited).ThenBy(x => x.r.FiledAtHour).ThenBy(x => x.r.Key, StringComparer.Ordinal)
                                  .Select(x => x.r).FirstOrDefault();
         if (fair != null) return Grant(fair, InitiativeGround.FairTurn);

         double spent = grants.Where(g => now - g.GrantedAtHour < settings.WindowDays * 24.0).Sum(g => g.Cost);
         if (spent >= settings.PerWindow) return None(InitiativeVerdict.BudgetSpent);

         InitiativeRank best = live.Min(r => r.Rank);
         List<InitiativeRequest> pool = live.Where(r => r.Rank == best).OrderBy(r => r.FiledAtHour).ThenBy(r => r.Key, StringComparer.Ordinal).ToList();

         return Grant(Draw(pool, grants, settings, now, roll), InitiativeGround.Rank);

         InitiativeDecision Grant(InitiativeRequest r, InitiativeGround ground)
            => new() {Verdict = InitiativeVerdict.Granted, Granted = r, Ground = ground, Expired = expired, Cost = CostOf(r, settings)};
      }

      /// <summary>The weight a request carries in its rank's draw, as the rule computes it (shown by cr.knocks).</summary>
      public static double DrawWeight(InitiativeRequest r, IReadOnlyList<InitiativeGrant> grants, InitiativeSettings settings, double now)
      {
         if (r == null) return 0;
         settings ??= new InitiativeSettings();
         grants ??= new List<InitiativeGrant>();

         double days = Math.Max(0, (now - r.FiledAtHour) / 24.0);
         double weight = WeightOf(r.Family, settings) / 100.0 * (1 + AgingPerDay * Math.Min(days, AgingCapDays));

         double? familyTurn = LastTurnOf(r.Family, grants);
         if (familyTurn.HasValue && now - familyTurn.Value < settings.WindowDays * 24.0) weight *= VarietyFactor(settings.Variety);

         bool sameCharacter = !string.IsNullOrEmpty(r.CharacterId)
                              && grants.Any(g => g.CharacterId == r.CharacterId && now - g.GrantedAtHour < SameCharacterHours);
         if (sameCharacter) weight *= SameCharacterFactor;

         return weight;
      }

      #region private

      private static InitiativeRequest Draw(List<InitiativeRequest> pool, IReadOnlyList<InitiativeGrant> grants, InitiativeSettings settings, double now, double roll)
      {
         List<double> weights = pool.Select(r => DrawWeight(r, grants, settings, now)).ToList();
         double total = weights.Sum();
         if (total <= 0) return pool[0];

         double target = Math.Min(Math.Max(roll, 0), 0.999999) * total;
         for (var i = 0; i < pool.Count; i++)
         {
            target -= weights[i];
            if (target < 0) return pool[i];
         }

         return pool[pool.Count - 1];
      }

      private static int WeightOf(InitiativeFamily family, InitiativeSettings settings)
         => settings.FamilyWeights != null && settings.FamilyWeights.TryGetValue(family, out int w) ? Math.Max(0, w) : 100;

      private static double? LastTurnOf(InitiativeFamily family, IReadOnlyList<InitiativeGrant> grants)
      {
         List<InitiativeGrant> mine = grants.Where(g => g.Family == family).ToList();

         return mine.Count == 0 ? null : mine.Max(g => g.GrantedAtHour);
      }

      private static double VarietyFactor(InitiativeVariety variety)
         => variety switch {
            InitiativeVariety.Low => 0.75,
            InitiativeVariety.Strong => 0.25,
            _ => 0.5
         };

      #endregion
   }
}
