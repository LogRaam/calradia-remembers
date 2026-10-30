// Code written by Gabriel Mailhot, 07/08/2026.
// Living World pillar, Increment 2: lords START schemes on their OWN initiative. Increment 1 built the BACK of
// the chain (a scheme that resolves sours the real relation and lets vanilla escalate to war on its own); this
// policy is the FRONT of it. It decides, purely, WHO may autonomously plot against WHOM and HOW OFTEN: only
// genuine rivals (never kin, never a friend, never the player or the player's own clan, never a faction leader),
// under a global cap on concurrent autonomous plots and a per-instigator cooldown, so the world feels alive
// rather than a plot-storm (the same "budgeted, not a flood" philosophy as LetterMailboxPolicy). The mod owns
// the weekly tick, the RNG roll, and the actual launch (SchemeStore.ForcePlot); this class only reasons over
// plain facts so the tuning stays protected by tests. No war is ever decreed here or anywhere in this feature.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Pure eligibility, budget, and cooldown gates for the autonomous lord-scheme generator. Every constant
   ///   here is a tuning knob: changing one is a one-line, test-covered decision, never a hunt through engine
   ///   code. The generator is deliberately curated and capped, not a simulation.
   /// </summary>
   public static class AutonomousSchemePolicy
   {
      /// <summary>
      ///   Tuning: the hard cap on how many AUTONOMOUS schemes may be in motion at once (counted distinctly from
      ///   player-abetted plots, which are player-driven and out of this budget). Kept low so the world's
      ///   intrigue stays ambient, mirroring the letter mailbox's "alive, not a flood" budgeting.
      /// </summary>
      public const int MaxActiveAutonomousSchemes = 4;

      /// <summary>
      ///   Tuning: in-game days one lord must wait between launching autonomous plots, so a single schemer can
      ///   never spam the world with feuds. Roughly a third of a Calradian year (84-day year).
      /// </summary>
      public const int InstigatorCooldownDays = 30;

      /// <summary>
      ///   Tuning: relation at or below which one lord's dislike of another counts as genuine, standing enmity
      ///   worth plotting over. Matches the gossip feeder's own "bitter rivals" line and ContextualNameResolver's
      ///   "a bitter enemy (resentment)" band, so a plot only ever springs from a rivalry the player could
      ///   already read elsewhere in the game.
      /// </summary>
      public const int RivalryThreshold = -30;

      /// <summary>
      ///   Tuning: the low per-eligible-pair probability (percent) that a real rivalry actually boils over into a
      ///   fresh plot on any given weekly pass. Low on purpose: combined with the cap and the cooldown, it keeps
      ///   even a realm full of enemies from all plotting at once. Consumed by the mod's RNG roll, not applied
      ///   here, so this policy stays pure and deterministic given its inputs.
      /// </summary>
      public const int PerPairLaunchChancePercent = 15;

      /// <summary>
      ///   True when there is room under the global cap for one more autonomous scheme. The caller counts only
      ///   schemes it launched autonomously (never player-abetted ones), so a busy player plotting on their own
      ///   never starves the world of its independent intrigue, nor the reverse.
      /// </summary>
      public static bool BudgetAllowsLaunch(int activeAutonomousSchemeCount)
         => activeAutonomousSchemeCount < MaxActiveAutonomousSchemes;

      /// <summary>
      ///   True when enough in-game days have passed since this lord last launched an autonomous plot
      ///   (<paramref name="lastLaunchDay" /> null meaning "never") for the per-instigator cooldown to have
      ///   elapsed. Mirrors <see cref="CourtActionPolicy.CooldownElapsed" />'s own shape.
      /// </summary>
      public static bool InstigatorCooldownElapsed(int? lastLaunchDay, int today)
         => lastLaunchDay == null || today - lastLaunchDay.Value >= InstigatorCooldownDays;

      /// <summary>
      ///   What is known about a lord who might start a plot of their own. FIVE booleans in a row, two
      ///   requirements then three exclusions, and the exclusions are what keep the player and the
      ///   player's own house out of the autonomous path entirely.
      ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
      ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
      ///   that names only what it is about reads as the rule it protects.
      /// </summary>
      public sealed class InstigatorFacts
      {
         /// <summary>They are alive.</summary>
         public bool IsAlive { get; set; }

         /// <summary>They are a lord, which is who has the standing to plot.</summary>
         public bool IsLord { get; set; }

         /// <summary>They are held prisoner, and a captive schemes nothing.</summary>
         public bool IsPrisoner { get; set; }

         /// <summary>They ARE the player, who only ever plots through the player-driven path.</summary>
         public bool IsPlayer { get; set; }

         /// <summary>They are of the player's own house, which likewise never acts on its own here.</summary>
         public bool IsPlayerClan { get; set; }
      }

      /// <summary>
      ///   True when a hero has the standing to autonomously plot: a living, free lord who is NEITHER the player
      ///   NOR a member of the player's own clan. The player only ever plots through the existing player-driven
      ///   path (pledge_against / scheme_assist); they are never an autonomous instigator, and neither is their
      ///   house acting on its own.
      /// </summary>
      public static bool IsEligibleInstigator(InstigatorFacts facts)
      {
         if (facts == null) return false;

         bool isAlive = facts.IsAlive;
         bool isLord = facts.IsLord;
         bool isPrisoner = facts.IsPrisoner;
         bool isPlayer = facts.IsPlayer;
         bool isPlayerClan = facts.IsPlayerClan;

         return isAlive && isLord && !isPrisoner && !isPlayer && !isPlayerClan;
      }

      /// <summary>
      ///   True when a hero is a grounded target for an autonomous plot: the instigator holds genuine enmity
      ///   toward them (relation at or below <see cref="RivalryThreshold" />, which by construction excludes any
      ///   friend), and they are NEVER the instigator's close kin, NEVER the player or the player's own clan, and
      ///   NEVER a faction leader (moving against a ruler is a war-scale act reserved for the leader-only
      ///   war-decision system, never a lord's private feud (the same exclusion pledge_against applies).
      ///   <para>
      ///     Takes the candidate rather than four loose facts (2026-09-10). It invents no type to do it:
      ///     <see cref="SchemeTargetCandidate" /> below already carries exactly this relation and these three
      ///     exclusions, and <see cref="SelectAutonomousTarget" /> was already unpacking one into the four
      ///     arguments at the only call site in the mod. Asking for the type that exists removes both the
      ///     unpacking and the chance of unpacking it wrong.
      ///   </para>
      /// </summary>
      public static bool IsGroundedTarget(SchemeTargetCandidate target)
         => !target.IsKin && !target.IsPlayerSide && !target.IsFactionLeader
            && target.Relation <= RivalryThreshold;

      /// <summary>
      ///   A candidate target for an autonomous plot: their id, the instigator's current relation to them, and
      ///   the exclusion facts. The engine-coupled reads (kin, clan, faction) live in the mod; this struct only
      ///   carries the resolved facts so the picking rule stays pure and testable. Mirrors
      ///   <see cref="CourtActionPolicy.SwayCandidate" />'s own shape.
      /// </summary>
      public readonly struct SchemeTargetCandidate
      {
         public string Id { get; init; }
         public int Relation { get; init; }
         public bool IsKin { get; init; }
         public bool IsPlayerSide { get; init; }
         public bool IsFactionLeader { get; init; }

         /// <summary>
         ///   The candidate heads their house (30/10/2026): a house grown too strong is struck through its head, the one
         ///   whose standing carries it.
         /// </summary>
         public bool IsHouseHead { get; init; }

         /// <summary>The candidate is of the instigator's own house: a house never plots against itself.</summary>
         public bool SameHouse { get; init; }

         /// <summary>The candidate's house's military strength (the game's own measure).</summary>
         public float HouseStrength { get; init; }

         /// <summary>The instigator's own house's military strength.</summary>
         public float OwnHouseStrength { get; init; }

         /// <summary>
         ///   The candidate's house serves the instigator's own realm, or a realm at war with it (30/10/2026): a rival is a
         ///   house he contends with, never a stranger at the far end of Calradia.
         /// </summary>
         public bool RivalRealm { get; init; }

         /// <summary>An autonomous plot already strikes this candidate: one house grown too strong draws one plot at a time.</summary>
         public bool AlreadyPlottedAgainst { get; init; }
      }

      /// <summary>
      ///   Tuning (30/10/2026): a house overshadows another once it is this many times stronger. Gabriel (29/10/2026) named
      ///   plotting "contre un rival ou une faction qui devient trop puissante" as a lord's ambition.
      /// </summary>
      public const float OvershadowRatio = 2f;

      /// <summary>
      ///   The most a strategist may like the head of a house and still plot to cut it down: anyone he regards above
      ///   neutral is a friend here, never a target, whatever his house's might.
      /// </summary>
      public const int OvershadowMaxRelation = NpcMemoryService.Core.Standing.VanillaRelation.NeutralTier;

      /// <summary>
      ///   True when a hero is a grounded target for a plot born of THREAT rather than hatred (30/10/2026): the head of
      ///   another house of his own realm or of a realm at war with it, at least <see cref="OvershadowRatio" /> times as
      ///   strong as the instigator's, not already the target of such a plot, whom the instigator does not count a friend,
      ///   never kin and never a faction leader (a ruler is the war-decision system's to move
      ///   against). Unlike a hatred plot, the player may be the target: Gabriel (30/10/2026) agreed the lords may plot
      ///   against the player's house once it dominates.
      /// </summary>
      public static bool IsOvershadowingTarget(SchemeTargetCandidate target)
         => target.IsHouseHead && target.RivalRealm && !target.AlreadyPlottedAgainst && !target.SameHouse && !target.IsKin && !target.IsFactionLeader
            && target.Relation <= OvershadowMaxRelation
            && target.HouseStrength > 0f
            && target.HouseStrength >= OvershadowRatio * System.Math.Max(target.OwnHouseStrength, 1f);

      /// <summary>
      ///   The head of the mightiest house that overshadows the instigator's (<see cref="IsOvershadowingTarget" />), or null.
      ///   Asked only when no hatred plot applies: an old enmity comes first.
      /// </summary>
      public static string SelectOvershadowingTarget(IEnumerable<SchemeTargetCandidate> candidates)
         => candidates?.Where(IsOvershadowingTarget).OrderByDescending(c => c.HouseStrength).Select(c => c.Id).FirstOrDefault();

      /// <summary>
      ///   Picks the most plausible autonomous-plot target from a pool of living lords: only among those the
      ///   instigator holds genuine, standing enmity toward and who are not excluded (<see cref="IsGroundedTarget" />),
      ///   preferring the DEEPEST enmity when several qualify (a plot springs from the bitterest rivalry first).
      ///   Mirrors <see cref="CourtActionPolicy.SelectPledgeTarget" />'s own kin-safe, already-disliked selection.
      ///   Null when no eligible rival exists (or the pool is empty).
      /// </summary>
      public static string SelectAutonomousTarget(IEnumerable<SchemeTargetCandidate> candidates)
      {
         if (candidates == null) return null;

         List<SchemeTargetCandidate> eligible = candidates
                                                .Where(IsGroundedTarget)
                                                .ToList();

         if (eligible.Count == 0) return null;

         return eligible.OrderBy(c => c.Relation).First().Id;
      }
   }
}
