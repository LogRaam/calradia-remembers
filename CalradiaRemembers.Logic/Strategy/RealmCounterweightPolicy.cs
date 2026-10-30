// Code written by Gabriel Mailhot, 30/10/2026.
// Lords who play the realm's strategy. Gabriel (29/10/2026), on what a lord's ambition is: "tout ce qui renforcit son
// Clan ou sa faction militairement. Ce qui lui donne un avantage tactique comme par exemple manigancer contre un rival
// ou une faction qui devient trop puissante. Faire des arrangements avec d'autres factions plus faibles pour attaquer
// une faction plus puissante." On 30/10/2026 he agreed that the mod may have a house file the game's own kingdom
// decisions on its own (war, peace, alliance, a call to war: the clans vote and the ruler decides, as for any proposal
// the game's own houses make). The game already proposes all four, but at random; a strategist proposes them against
// the ONE realm that has grown too strong, in the order a counterweight is built. This policy decides which realm
// dominates, and which steps a realm facing it may take, best first; the mod asks the game whether each is allowed.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Strategy
{
   /// <summary>One realm's might, read from the game.</summary>
   public sealed class RealmPower
   {
      /// <summary>The realm's StringId.</summary>
      public string Id { get; init; }

      /// <summary>The realm's total military strength (the game's own sum over its houses).</summary>
      public float Strength { get; init; }
   }

   /// <summary>Another realm, as seen from the realm weighing its next step against the dominant one.</summary>
   public sealed class OtherRealm
   {
      /// <summary>The realm's StringId.</summary>
      public string Id { get; init; }

      /// <summary>Its total military strength.</summary>
      public float Strength { get; init; }

      /// <summary>It is at war with the realm weighing its step: no alliance with it, perhaps a peace.</summary>
      public bool AtWarWithUs { get; init; }

      /// <summary>It already fights the dominant realm: a natural ally.</summary>
      public bool AtWarWithDominant { get; init; }

      /// <summary>It is already allied with the realm weighing its step.</summary>
      public bool AlliedWithUs { get; init; }
   }

   /// <summary>What a realm facing the dominant one knows of the board.</summary>
   public sealed class CounterweightFacts
   {
      /// <summary>The realm's own strength.</summary>
      public float Strength { get; init; }

      /// <summary>It is already at war with the dominant realm.</summary>
      public bool AtWarWithDominant { get; init; }

      /// <summary>Every other realm still standing, neither this one nor the dominant one.</summary>
      public IReadOnlyList<OtherRealm> Others { get; init; } = new List<OtherRealm>();
   }

   /// <summary>A step toward a counterweight against the dominant realm, each one a decision the game already knows.</summary>
   public enum CounterweightMove
   {
      /// <summary>Call an ally that is not yet fighting the dominant realm into the war.</summary>
      CallAllyToWar,

      /// <summary>Offer an alliance to a realm already fighting the dominant one.</summary>
      AllianceAgainstDominant,

      /// <summary>Make peace with a weaker enemy, so the realm's armies are free for the stronger one.</summary>
      PeaceWithWeaker,

      /// <summary>Put war on the dominant realm to the vote.</summary>
      WarOnDominant
   }

   /// <summary>One step: the move, and the realm it is made with or against (the dominant realm itself for a war).</summary>
   public sealed class CounterweightStep
   {
      /// <summary>What is proposed.</summary>
      public CounterweightMove Move { get; init; }

      /// <summary>The other realm's StringId: the ally, the enemy to make peace with, or the dominant realm.</summary>
      public string RealmId { get; init; }
   }

   /// <summary>Who proposed a step, in which realm, toward whom: the names the news is told with.</summary>
   public sealed class CounterweightNewsFacts
   {
      /// <summary>What was put to the vote.</summary>
      public CounterweightMove Move { get; init; }

      /// <summary>The house head who proposed it, already labelled for the prompt (name and house).</summary>
      public string Proposer { get; init; }

      /// <summary>The proposer's realm.</summary>
      public string Realm { get; init; }

      /// <summary>The other realm: the ally, the enemy to make peace with, or the dominant realm for a war.</summary>
      public string Other { get; init; }

      /// <summary>The realm grown too strong.</summary>
      public string Dominant { get; init; }
   }

   /// <summary>Which realm dominates, and how the others may build a counterweight. Pure.</summary>
   public static class RealmCounterweightPolicy
   {
      /// <summary>TUNE: the strongest realm dominates only at this many times the strength of the next one.</summary>
      public const float DominanceRatio = 1.5f;

      /// <summary>TUNE: ... and with at least this share of all the realms' strength together.</summary>
      public const float DominanceShare = 0.3f;

      /// <summary>Fewer realms than this standing and there is no balance left to keep.</summary>
      public const int MinRealms = 3;

      /// <summary>TUNE: days a realm waits after one strategist proposal before the next (proposed to Gabriel 30/10/2026: a month).</summary>
      public const int RealmCooldownDays = 30;

      /// <summary>
      ///   The game's support a house must not fall below to propose a war: below its negative the house is against it
      ///   (<see cref="PressForWarPolicy.SupportThreshold" />, the same bar a lord pressed by the player answers to).
      /// </summary>
      public const float WarSupportFloor = -PressForWarPolicy.SupportThreshold;

      /// <summary>The realm that has grown too strong, or null when the balance holds.</summary>
      public static string Dominant(IReadOnlyList<RealmPower> realms)
      {
         if (realms == null || realms.Count < MinRealms) return null;

         List<RealmPower> ranked = realms.Where(r => r != null && r.Strength > 0f).OrderByDescending(r => r.Strength).ToList();
         if (ranked.Count < MinRealms) return null;

         float total = ranked.Sum(r => r.Strength);
         RealmPower first = ranked[0];

         return first.Strength >= DominanceRatio * ranked[1].Strength && first.Strength >= DominanceShare * total
            ? first.Id
            : null;
      }

      /// <summary>
      ///   The steps a realm facing the dominant one may take, best first. At war with it: bring an ally in, then free the
      ///   armies from a weaker enemy, then find a new ally among its other enemies. At peace with it: find allies first,
      ///   then free the armies, then put the war to the vote. The game decides whether each is allowed and how the houses
      ///   vote; this only orders them.
      /// </summary>
      public static IReadOnlyList<CounterweightStep> Steps(CounterweightFacts f, string dominantId)
      {
         var steps = new List<CounterweightStep>();
         if (f == null || string.IsNullOrEmpty(dominantId)) return steps;

         List<OtherRealm> others = (f.Others ?? new List<OtherRealm>()).Where(o => o != null && o.Id != dominantId).ToList();
         IEnumerable<CounterweightStep> calls = others.Where(o => o.AlliedWithUs && !o.AtWarWithDominant)
                                                      .OrderByDescending(o => o.Strength)
                                                      .Select(o => Step(CounterweightMove.CallAllyToWar, o.Id));
         IEnumerable<CounterweightStep> alliances = others.Where(o => o.AtWarWithDominant && !o.AtWarWithUs && !o.AlliedWithUs)
                                                          .OrderByDescending(o => o.Strength)
                                                          .Select(o => Step(CounterweightMove.AllianceAgainstDominant, o.Id));
         IEnumerable<CounterweightStep> peaces = others.Where(o => o.AtWarWithUs && o.Strength < f.Strength)
                                                       .OrderByDescending(o => o.Strength)
                                                       .Select(o => Step(CounterweightMove.PeaceWithWeaker, o.Id));

         if (f.AtWarWithDominant)
         {
            steps.AddRange(calls);
            steps.AddRange(peaces);
            steps.AddRange(alliances);
         }
         else
         {
            steps.AddRange(alliances);
            steps.AddRange(peaces);
            steps.Add(Step(CounterweightMove.WarOnDominant, dominantId));
         }

         return steps;
      }

      /// <summary>
      ///   True when a house backs the step enough to put it to the vote, by the game's own support: a war only when the
      ///   house is not against it, the other steps only when it is for them at all.
      /// </summary>
      public static bool HouseWouldPropose(CounterweightMove move, float support)
         => move == CounterweightMove.WarOnDominant ? support >= WarSupportFloor : support > 0f;

      /// <summary>True when the realm's last strategist proposal (null: never) is a month behind it.</summary>
      public static bool RealmMayAct(int? lastActDay, int today)
         => lastActDay == null || today - lastActDay.Value >= RealmCooldownDays;

      /// <summary>
      ///   The news of a proposal, as the realm's court hears it. It says who URGED what and why, never what was decided:
      ///   the houses vote and the ruler decides, and the game tells its own outcome.
      /// </summary>
      public static string News(CounterweightNewsFacts f)
      {
         if (f == null) return null;

         return f.Move switch {
            CounterweightMove.WarOnDominant => $"{f.Proposer} urged {f.Realm} to make war on {f.Dominant}, grown too strong.",
            CounterweightMove.PeaceWithWeaker => $"{f.Proposer} urged {f.Realm} to make peace with {f.Other}, to turn its swords on {f.Dominant}.",
            CounterweightMove.AllianceAgainstDominant => $"{f.Proposer} urged {f.Realm} to seek an alliance with {f.Other} against {f.Dominant}.",
            _ => $"{f.Proposer} urged {f.Realm} to call its ally {f.Other} to arms against {f.Dominant}."
         };
      }

      private static CounterweightStep Step(CounterweightMove move, string realmId) => new() {Move = move, RealmId = realmId};
   }
}
