// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>The outcome of one day's spousal-resistance roll against an active <see cref="DivorceProcess" />.</summary>
   public enum DivorceResistanceOutcome
   {
      /// <summary>No resistance surfaced today; the process proceeds toward its current end day.</summary>
      Continue,

      /// <summary>The spouse's resistance pushed the end day back.</summary>
      Delayed,

      /// <summary>The spouse's resistance won outright: the estrangement is off.</summary>
      Aborted
   }

   /// <summary>
   ///   Every already-resolved fact <see cref="DivorcePolicy.IsEligible" /> needs. "Leaving spouse" is
   ///   whichever of the two would have to move clans to formalize the split (the one who joined the
   ///   other's clan at marriage) — resolved live by <c>DivorceEligibilityResolver</c>, since it takes a
   ///   Hero comparison the pure layer cannot make. The three "leaving spouse" flags default false
   ///   (no relocation needed) when neither spouse moved clans.
   /// </summary>
   public sealed class DivorceEligibilityFacts
   {
      /// <summary>True when either the NPC or the spouse is currently pregnant.</summary>
      public bool EitherPregnant { get; set; }

      /// <summary>True when either the NPC or the spouse is currently held prisoner.</summary>
      public bool EitherPrisoner { get; set; }

      /// <summary>True when the spouse who would have to relocate clans currently governs a settlement.</summary>
      public bool LeavingSpouseGoverns { get; set; }

      /// <summary>True when the spouse who would have to relocate clans heads their own clan.</summary>
      public bool LeavingSpouseIsClanLeader { get; set; }

      /// <summary>True when the spouse who would have to relocate clans leads a party (or army).</summary>
      public bool LeavingSpouseLeadsParty { get; set; }

      /// <summary>True when the NPC currently has a living spouse.</summary>
      public bool NpcIsMarried { get; set; }

      /// <summary>
      ///   True when the NPC is romantically bound to the player (courting or a consort): they would
      ///   leave the marriage FOR the player, so the trust gate is considered met.
      /// </summary>
      public bool NpcIsRomanticallyBoundToPlayer { get; set; }

      /// <summary>The NPC's own personal relation toward the player.</summary>
      public int NpcRelationWithPlayer { get; set; }

      /// <summary>The NPC's own personal relation toward their spouse.</summary>
      public int NpcRelationWithSpouse { get; set; }

      /// <summary>True when this NPC already has a divorce process under way (one at a time).</summary>
      public bool ProcessAlreadyActive { get; set; }

      /// <summary>
      ///   True when the NPC's spouse IS the player: the player's own marriage is out of Phase 1's
      ///   scope (it will be its own feature, with the spouse able to DEMAND the divorce), and the romance
      ///   waiver would otherwise open it by accident.
      /// </summary>
      public bool SpouseIsPlayer { get; set; }
   }

   /// <summary>
   ///   Pure math and gates for the end_marriage estrangement process. Every constant here is a tuning
   ///   knob: changing one is a one-line, test-covered decision, never a hunt through engine code.
   /// </summary>
   public static class DivorcePolicy
   {
      /// <summary>Tuning: additional abort percent per positive Calculating trait level of the spouse.</summary>
      public const int AbortChancePerCalculatingLevel = 2;

      /// <summary>Tuning: additional abort percent per positive Honor trait level of the spouse.</summary>
      public const int AbortChancePerHonorLevel = 2;

      // ── Spousal resistance ───────────────────────────────────────────────

      /// <summary>Tuning: the base percent chance, each day, that the spouse's resistance ends the process outright.</summary>
      public const int BaseAbortChancePercent = 3;

      /// <summary>Tuning: the base percent chance, each day, that the spouse's resistance merely delays the process.</summary>
      public const int BaseDelayChancePercent = 10;

      /// <summary>Tuning: the NPC's relation with the player must be at or above this to trust them with this.</summary>
      public const int BondThreshold = 30;

      /// <summary>Tuning: the default severity multiplier for every other culture.</summary>
      public const float DefaultScandalMultiplier = 1.0f;

      /// <summary>Tuning: additional delay percent per positive Calculating trait level of the spouse.</summary>
      public const int DelayChancePerCalculatingLevel = 3;

      /// <summary>Tuning: additional delay percent per positive Honor trait level of the spouse.</summary>
      public const int DelayChancePerHonorLevel = 3;

      /// <summary>Tuning: days added to the end day by one Delayed resistance roll.</summary>
      public const int DelayDaysPerDelay = 3;

      /// <summary>Tuning: relation dealt to each of the ex-spouse's own clan members toward the player.</summary>
      public const int ExSpouseClanMemberPlayerRelationHit = -15;

      // ── Consequence magnitudes ───────────────────────────────────────────

      /// <summary>Tuning: relation dealt to the ex-spouse's own regard for the NPC who divorced them.</summary>
      public const int ExSpouseNpcRelationHit = -30;

      /// <summary>Tuning: relation dealt to the ex-spouse's regard for the player, who is blamed for orchestrating it.</summary>
      public const int ExSpousePlayerRelationHit = -40;

      /// <summary>Tuning: the hard ceiling on the daily abort chance, however proud or calculating the spouse.</summary>
      public const int MaxAbortChancePercent = 30;

      /// <summary>Tuning: the cumulative cap on days added by resistance, so a process cannot stall forever.</summary>
      public const int MaxCumulativeDelayDays = 15;

      /// <summary>Tuning: the hard ceiling on the daily delay chance.</summary>
      public const int MaxDelayChancePercent = 45;

      /// <summary>
      ///   Tuning: at/below this marital misery, no bond with the player is needed at all — a hero
      ///   this wretched seizes the door once someone opens it, whoever they are.
      /// </summary>
      public const int MiseryNeedingNoBond = -50;

      /// <summary>
      ///   Tuning: the softened hits when the divorce was by MUTUAL consent (the spouse was unhappy too):
      ///   relief, not repudiation, a mild sting toward the NPC, near-neutral toward the player.
      /// </summary>
      public const int MutualExSpouseNpcRelationHit = -10;

      /// <summary>Tuning: mutual-consent hit toward the player (they merely midwifed what both wanted).</summary>
      public const int MutualExSpousePlayerRelationHit = -5;

      /// <summary>Tuning: the severity multiplier for Nordic/Sturgian-like cultures, which treat dissolution more freely.</summary>
      public const float NordicScandalMultiplier = 0.5f;

      // ── Process duration ─────────────────────────────────────────────────

      /// <summary>Tuning: the estrangement's baseline length in in-game days.</summary>
      public const int ProcessDurationDays = 15;

      // ── Culture-scaled scandal ───────────────────────────────────────────

      /// <summary>Tuning: the scandal's base reach (0..100), before culture scaling.</summary>
      public const int ScandalBaseMagnitude = 50;

      /// <summary>
      ///   Tuning: the small relation dip dealt to the NPC's regard for the player when the process is Aborted or fails
      ///   at the end day.
      /// </summary>
      public const int TerminatedNpcPlayerRelationHit = -5;
      // ── Eligibility ──────────────────────────────────────────────────────

      /// <summary>Tuning: the NPC's relation with their own spouse must be at or below this to count as unhappy.</summary>
      public const int UnhappyThreshold = -5;

      /// <summary>
      ///   Culture StringIds (lowercase, e.g. "sturgia") whose folk treat marriage dissolution more
      ///   freely — a per-culture flavour, expandable without touching the call sites. Any culture not
      ///   listed here scales at <see cref="DefaultScandalMultiplier" />.
      /// </summary>
      private static readonly HashSet<string> NordicCultureIds = new(StringComparer.OrdinalIgnoreCase) {
         "sturgia"
      };

      /// <summary>The end day for a process begun on <paramref name="startDay" />.</summary>
      public static int ComputeEndDay(int startDay) => startDay + ProcessDurationDays;

      /// <summary>The scandal severity multiplier for a culture StringId (unknown/null cultures scale at the default).</summary>
      public static float CultureSeverityMultiplier(string cultureId)
         => !string.IsNullOrEmpty(cultureId) && NordicCultureIds.Contains(cultureId)
            ? NordicScandalMultiplier
            : DefaultScandalMultiplier;

      /// <summary>The ex-spouse's hit toward the NPC, signed by mutual consent vs repudiation.</summary>
      public static int ExSpouseNpcHit(bool mutualConsent)
         => mutualConsent
            ? MutualExSpouseNpcRelationHit
            : ExSpouseNpcRelationHit;

      /// <summary>The ex-spouse's hit toward the player, signed by mutual consent vs repudiation.</summary>
      public static int ExSpousePlayerHit(bool mutualConsent)
         => mutualConsent
            ? MutualExSpousePlayerRelationHit
            : ExSpousePlayerRelationHit;

      /// <summary>The pushed-back end day after one Delayed resistance roll.</summary>
      public static int ExtendEndDay(int currentEndDay) => currentEndDay + DelayDaysPerDelay;

      // ── Helpers ──────────────────────────────────────────────────────────

      /// <summary>
      ///   True when a hero currently sits in a different clan than the one they were born/created
      ///   into — i.e. they moved into a spouse's clan at marriage. Pure string comparison; the caller
      ///   resolves the two StringIds live.
      /// </summary>
      public static bool HasMovedClans(string currentClanId, string originClanId)
         => !string.IsNullOrEmpty(currentClanId) &&
            !string.IsNullOrEmpty(originClanId) &&
            !string.Equals(currentClanId, originClanId, StringComparison.Ordinal);

      /// <summary>
      ///   True when every gate holds: the NPC is married, the marriage is genuinely unhappy, the bond
      ///   with the player runs deep enough, neither party is pregnant or held captive, the spouse who
      ///   would have to relocate is free to (no clan/party/settlement tied to them), and no process is
      ///   already under way. <paramref name="reason" /> names the first failing gate, else null.
      /// </summary>
      public static bool IsEligible(DivorceEligibilityFacts facts, out string reason)
      {
         if (facts == null)
         {
            reason = "no facts to evaluate";

            return false;
         }

         if (facts.ProcessAlreadyActive)
         {
            reason = "a divorce is already under way";

            return false;
         }

         if (!facts.NpcIsMarried)
         {
            reason = "not currently married";

            return false;
         }

         if (facts.SpouseIsPlayer)
         {
            reason = "their marriage is to YOU; ending your own marriage is another matter entirely";

            return false;
         }

         if (facts.EitherPregnant)
         {
            reason = "a pregnancy is in play";

            return false;
         }

         if (facts.EitherPrisoner)
         {
            reason = "one of the pair is held captive";

            return false;
         }

         if (facts.NpcRelationWithSpouse > UnhappyThreshold)
         {
            reason = "the marriage is not unhappy enough";

            return false;
         }

         // The bond gate SLIDES with marital misery, and love overrides it entirely: an NPC courting
         // the player leaves FOR them, and a deeply wretched one needs no urging from a friend at all.
         if (!facts.NpcIsRomanticallyBoundToPlayer && facts.NpcRelationWithPlayer < RequiredPlayerBond(facts.NpcRelationWithSpouse))
         {
            reason = "the bond with the player is not deep enough for so unhappy a step";

            return false;
         }

         if (facts.LeavingSpouseIsClanLeader)
         {
            reason = "the spouse who would have to relocate heads their own clan";

            return false;
         }

         if (facts.LeavingSpouseLeadsParty)
         {
            reason = "the spouse who would have to relocate leads a party";

            return false;
         }

         if (facts.LeavingSpouseGoverns)
         {
            reason = "the spouse who would have to relocate governs a settlement";

            return false;
         }

         reason = null;

         return true;
      }

      /// <summary>
      ///   A divorce is by MUTUAL consent when the spouse never RESISTED the estrangement (no delay days
      ///   accumulated): their acquiescence is the one honest signal we have, since engine relations are
      ///   SYMMETRIC per pair (both sides share one number, so "the spouse was unhappy too" cannot be read
      ///   from relation). A spouse who fought the process, however briefly, was repudiated, not willing.
      /// </summary>
      /// <summary>Regard at or above which a non-resisting spouse is read as genuinely acquiescing rather than merely not having rolled a resistance delay (romance audit minor: ~21% of hateful spouses falsely "consented"). Tuning.</summary>
      public const int MutualConsentMinRegard = 0;

      /// <summary>
      ///   True when the set-aside spouse genuinely acquiesced: they never once resisted the estrangement AND
      ///   they do not actively resent the player (romance audit minor). Resistance is probabilistic, so a
      ///   hateful spouse could roll zero delays by chance; without the regard gate they earned the amicable,
      ///   low-grudge "mutual consent" consequence they never deserved.
      /// </summary>
      public static bool IsMutualConsent(int cumulativeDelayDays, int spouseRegardForPlayer)
         => cumulativeDelayDays == 0 && spouseRegardForPlayer >= MutualConsentMinRegard;

      /// <summary>
      ///   The player-bond an NPC needs before agreeing to end their marriage, SLIDING with how miserable
      ///   the marriage is (Gabriel, 2026-07-03: a flat 30 was incoherent — the NPC divorces for THEMSELVES
      ///   first). Barely unhappy (at <see cref="UnhappyThreshold" />) needs the full
      ///   <see cref="BondThreshold" />; the requirement falls linearly to zero at
      ///   <see cref="MiseryNeedingNoBond" />.
      /// </summary>
      public static int RequiredPlayerBond(int relationWithSpouse)
      {
         if (relationWithSpouse >= UnhappyThreshold) return BondThreshold;
         if (relationWithSpouse <= MiseryNeedingNoBond) return 0;

         double span = UnhappyThreshold - MiseryNeedingNoBond;
         double misery = UnhappyThreshold - relationWithSpouse;

         return (int) Math.Round(BondThreshold * (1 - misery / span));
      }

      /// <summary>
      ///   One day's worth of a spouse's resistance to a dissolution. FOUR INTS in a row on four unrelated
      ///   scales: two trait levels, a day count, and the caller's own dice. Swap the traits and the wrong
      ///   one drives the abort chance; swap the delay days with the roll and a long-delayed process reads
      ///   as a lucky throw. Nothing about the types could tell any of it apart.
      ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
      ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
      ///   that names only what it is about reads as the rule it protects.
      /// </summary>
      public sealed class ResistanceRollFacts
      {
         /// <summary>The spouse's Honor trait. Only its positive side raises the abort and delay chances.</summary>
         public int SpouseHonor { get; set; }

         /// <summary>The spouse's Calculating trait, which raises the same two chances the same way.</summary>
         public int SpouseCalculating { get; set; }

         /// <summary>
         ///   Days of delay already accumulated. At the cap the process may still be Aborted but is never
         ///   offered a further Delayed roll, so a resistant spouse cannot stall forever.
         /// </summary>
         public int CumulativeDelayDays { get; set; }

         /// <summary>
         ///   The caller's own RNG draw, 0..99 inclusive. Injected so this stays a pure function of its inputs
         ///   and the harness can pin every boundary instead of chasing a flaky throw.
         /// </summary>
         public int Roll0To99 { get; set; }
      }

      /// <summary>
      ///   One day's resistance roll: a spouse of higher Honor or Calculating resists harder (both
      ///   raise the abort AND the delay chance, each capped). <c>Roll0To99</c> is the
      ///   caller's own RNG draw (0..99 inclusive) so this stays a pure, deterministic function of its
      ///   inputs. A process already at <see cref="MaxCumulativeDelayDays" /> of accumulated delay can
      ///   still be Aborted, but is never offered a further Delayed roll (Continue instead).
      /// </summary>
      public static DivorceResistanceOutcome RollResistance(ResistanceRollFacts facts)
      {
         if (facts == null) return DivorceResistanceOutcome.Continue;

         int spouseHonor = facts.SpouseHonor;
         int spouseCalculating = facts.SpouseCalculating;
         int cumulativeDelayDays = facts.CumulativeDelayDays;
         int roll0To99 = facts.Roll0To99;

         int abortChance = ClampPercent(
            BaseAbortChancePercent + PositivePart(spouseHonor) * AbortChancePerHonorLevel + PositivePart(spouseCalculating) * AbortChancePerCalculatingLevel,
            MaxAbortChancePercent);

         if (roll0To99 < abortChance) return DivorceResistanceOutcome.Aborted;

         if (cumulativeDelayDays >= MaxCumulativeDelayDays) return DivorceResistanceOutcome.Continue;

         int delayChance = ClampPercent(
            BaseDelayChancePercent + PositivePart(spouseHonor) * DelayChancePerHonorLevel + PositivePart(spouseCalculating) * DelayChancePerCalculatingLevel,
            MaxDelayChancePercent);

         return roll0To99 < abortChance + delayChance
            ? DivorceResistanceOutcome.Delayed
            : DivorceResistanceOutcome.Continue;
      }

      /// <summary>The scandal's world-event magnitude, culture-scaled, never rounding away to nothing.</summary>
      public static int ScaledScandalMagnitude(string cultureId)
         => Math.Max(1, (int) (ScandalBaseMagnitude * CultureSeverityMultiplier(cultureId)));

      #region private

      private static int ClampPercent(int value, int max)
      {
         if (value < 0) return 0;

         return value > max
            ? max
            : value;
      }

      private static int PositivePart(int value) => value > 0
         ? value
         : 0;

      #endregion
   }
}