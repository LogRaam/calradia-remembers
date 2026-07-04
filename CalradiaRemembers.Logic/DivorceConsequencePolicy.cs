// Code written by Gabriel Mailhot, 04/07/2026.
// 07/04/2026: narrowed the repudiated-divorce blast radius per Gabriel's ruling — the wronged spouse's
// IMMEDIATE FAMILY and THREE BEST FRIENDS grudge the player, not the whole clan (30% of the old clan
// magnitude would fall below the formation floor anyway, so a clan-wide grudge was mostly noise).

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   The kind of reaction one party has to a divorce. A <see cref="PartyReaction" /> is a transient
   ///   policy verdict the engine consumes the same tick it is produced (mapped to
   ///   <c>GrudgeBehavior.TryPlant</c> for <see cref="PlantGrudge" />, or a one-off relation nudge for
   ///   <see cref="Relief" />/<see cref="Gratitude" />) — it is never itself written to a save, so unlike
   ///   <c>GrudgeSource</c> these ordinals are NOT persisted and reordering here is safe. Frozen anyway,
   ///   to match the codebase's habit for every other consequence enum.
   /// </summary>
   public enum ReactionKind
   {
      /// <summary>A signed grudge forms (against the player, or, for the spouse, against the NPC who set them aside).</summary>
      PlantGrudge = 0,

      /// <summary>No grudge; a small positive relation nudge (glad to see this particular knot cut).</summary>
      Relief = 1,

      /// <summary>No grudge; a larger positive relation nudge (the divorce is a boon this party actively wanted).</summary>
      Gratitude = 2,

      /// <summary>Indifferent: this divorce touches this party too lightly to register at all.</summary>
      None = 3
   }

   /// <summary>
   ///   One party's verdict on a divorce. For <see cref="ReactionKind.PlantGrudge" />,
   ///   <see cref="GrudgeMagnitude" /> is positive and <see cref="RelationNudge" /> is zero; for
   ///   <see cref="ReactionKind.Relief" /> or <see cref="ReactionKind.Gratitude" />,
   ///   <see cref="RelationNudge" /> is positive and <see cref="GrudgeMagnitude" /> is zero; for
   ///   <see cref="ReactionKind.None" /> both are zero. <see cref="KnownToPlayer" /> only ever matters for
   ///   <see cref="ReactionKind.PlantGrudge" /> (whether the grudge is public knowledge at formation).
   /// </summary>
   public sealed class PartyReaction
   {
      /// <summary>
      ///   The proposed grudge intensity, 0..100, before <c>GrudgePolicy.RigidityAt</c>/formation gating. Zero unless
      ///   <see cref="Kind" /> is <see cref="ReactionKind.PlantGrudge" />.
      /// </summary>
      public int GrudgeMagnitude { get; set; }

      /// <summary>Which reaction this party has.</summary>
      public ReactionKind Kind { get; set; }

      /// <summary>
      ///   True when the grudge is public/aimed at the player at formation (only meaningful for
      ///   <see cref="ReactionKind.PlantGrudge" />).
      /// </summary>
      public bool KnownToPlayer { get; set; }

      /// <summary>
      ///   The immediate positive relation nudge. Zero unless <see cref="Kind" /> is <see cref="ReactionKind.Relief" />
      ///   or <see cref="ReactionKind.Gratitude" />.
      /// </summary>
      public int RelationNudge { get; set; }
   }

   /// <summary>
   ///   Every already-resolved fact <see cref="DivorceConsequencePolicy.Resolve" /> needs about the shape of
   ///   one divorce. The engine gathers these live; see the morning report for exactly where each one comes
   ///   from (and which, today, has no existing source and must be newly derived or assumed).
   /// </summary>
   public sealed class DivorceFacts
   {
      /// <summary>
      ///   True when the marriage was an approved/arranged union the families valued as a political alliance, vs made
      ///   against their will.
      /// </summary>
      public bool MarriageWasBlessed { get; set; }

      /// <summary>True when the set-aside spouse acquiesced (never resisted) — see <see cref="DivorcePolicy.IsMutualConsent" />.</summary>
      public bool MutualConsent { get; set; }

      /// <summary>
      ///   The set-aside spouse's prior regard for the player; downstream, <c>GrudgePolicy.RigidityAt</c> turns this into
      ///   the grudge's actual rigidity. This policy only uses it to modestly scale the proposed MAGNITUDE (a spouse who already
      ///   disliked the player nurses the repudiation harder); rigidity itself stays the plant-time computation's job, not this
      ///   policy's.
      /// </summary>
      public int SpouseRegardForPlayer { get; set; }
   }

   /// <summary>The full per-party verdict for one divorce, one <see cref="PartyReaction" /> per stake.</summary>
   public sealed class DivorceConsequences
   {
      /// <summary>
      ///   The set-aside spouse's IMMEDIATE ADULT FAMILY's reaction (father, mother, siblings, children — resolved live by
      ///   the engine via the Hero family API, applied per qualifying member). Replaces the old clan-wide grudge: the
      ///   extended clan feels nothing today, by design.
      /// </summary>
      public PartyReaction DirectFamilyReaction { get; set; }

      /// <summary>
      ///   The set-aside spouse's THREE BEST FRIENDS' reaction (the top 3 living heroes by regard for the spouse, applied
      ///   per qualifying member). A quiet resentment taken up on their friend's behalf — narrower and less publicly voiced
      ///   than the family's.
      /// </summary>
      public PartyReaction FriendReaction { get; set; }

      /// <summary>The set-aside spouse's own reaction.</summary>
      public PartyReaction SpouseReaction { get; set; }
   }

   /// <summary>
   ///   Pure rules turning a divorce's facts into a signed per-party consequence matrix: who grudges, who
   ///   feels relief, who feels outright gratitude, and who is untouched. Replaces flat relation deltas
   ///   with a matrix keyed on WHO is affected and HOW they stood toward the union.
   /// </summary>
   public static class DivorceConsequencePolicy
   {
      // ── The spouse's immediate family ───────────────────────────────────

      /// <summary>
      ///   Tuning: the grudge magnitude for each qualifying immediate-family member (father, mother, siblings, children) of
      ///   the repudiated spouse. Smaller than the spouse's own — the family loses one of their own to a broken match, not
      ///   the betrayal itself — but voiced openly (see <see cref="KnownToPlayer" /> on the reaction), unlike the friends'.
      /// </summary>
      public const int DirectFamilyGrudgeMagnitude = 35;

      /// <summary>
      ///   Tuning: the gratitude nudge for the family that opposed an unblessed marriage from the start, now that it has
      ///   ended. Dormant today: <c>DivorceProcessBehavior</c> hardcodes every marriage as blessed until a forced/unblessed-
      ///   marriage feature exists, which is the only thing that would ever set <see cref="DivorceFacts.MarriageWasBlessed" />
      ///   false and wake this branch up.
      /// </summary>
      public const int UnblessedFamilyGratitudeRelationNudge = 15;

      // ── The spouse's three best friends ──────────────────────────────────

      /// <summary>
      ///   Tuning: the grudge magnitude for each of the repudiated spouse's three best (living) friends. Smaller still than
      ///   the family's, and hidden from the player (<see cref="ReactionKind.PlantGrudge" />'s <c>KnownToPlayer</c> is false)
      ///   — a quiet resentment nursed on a friend's behalf, not a grievance the friend makes a show of.
      /// </summary>
      public const int FriendGrudgeMagnitude = 30;

      // ── Spouse (the set-aside party) ────────────────────────────────────

      /// <summary>Tuning: the repudiated spouse's grudge magnitude before the low-regard bonus.</summary>
      public const int SpouseGrudgeBaseMagnitude = 55;

      /// <summary>
      ///   Tuning: the magnitude bump applied when <see cref="DivorceFacts.SpouseRegardForPlayer" /> is at/below
      ///   <see cref="LowRegardThreshold" />.
      /// </summary>
      public const int SpouseGrudgeLowRegardBonus = 15;

      /// <summary>
      ///   Tuning: at/below this prior regard for the player, the repudiated spouse already disliked them, so the grudge
      ///   is nursed HARDER (a modest magnitude bump; actual rigidity is still computed downstream by
      ///   <c>GrudgePolicy.RigidityAt</c> from this same regard).
      /// </summary>
      public const int LowRegardThreshold = -10;

      /// <summary>
      ///   Tuning: the small relief nudge for a spouse who acquiesced to the divorce (mutual consent softens even the one
      ///   party closest to it — they wanted this too).
      /// </summary>
      public const int SpouseMutualReliefRelationNudge = 5;

      // ── Resolution ───────────────────────────────────────────────────────

      /// <summary>
      ///   The full per-party verdict for one divorce. Throws on null <paramref name="facts" />: unlike the
      ///   Grudges pillar's own candidate DTOs (which tolerate null as "nothing proposed"), a divorce that
      ///   reaches this policy is already a confirmed, resolved event — the caller is expected to have real
      ///   facts in hand.
      /// </summary>
      public static DivorceConsequences Resolve(DivorceFacts facts)
      {
         if (facts == null) throw new ArgumentNullException(nameof(facts));

         return new DivorceConsequences {
            SpouseReaction = ResolveSpouseReaction(facts),
            DirectFamilyReaction = ResolveDirectFamilyReaction(facts),
            FriendReaction = ResolveFriendReaction(facts)
         };
      }

      // ── Helper predicates (clarify intent at call sites and in tests) ───

      /// <summary>True when the set-aside spouse's own reaction is a grudge (i.e. they were repudiated, not a willing party).</summary>
      public static bool SpouseGrudges(DivorceFacts facts) => facts != null && !facts.MutualConsent;

      #region private

      private static PartyReaction Gratitude(int relationNudge) => new() {Kind = ReactionKind.Gratitude, GrudgeMagnitude = 0, KnownToPlayer = false, RelationNudge = relationNudge};

      // ── Reaction factories ───────────────────────────────────────────────

      private static PartyReaction Grudge(int magnitude, bool knownToPlayer) => new() {Kind = ReactionKind.PlantGrudge, GrudgeMagnitude = magnitude, KnownToPlayer = knownToPlayer, RelationNudge = 0};

      private static PartyReaction NoneReaction() => new() {Kind = ReactionKind.None, GrudgeMagnitude = 0, KnownToPlayer = false, RelationNudge = 0};

      private static PartyReaction Relief(int relationNudge) => new() {Kind = ReactionKind.Relief, GrudgeMagnitude = 0, KnownToPlayer = false, RelationNudge = relationNudge};

      /// <summary>
      ///   The immediate family's verdict: a blessed, repudiated split makes them grudge the player openly
      ///   (<c>KnownToPlayer = true</c> — they voice their anger); mutual consent leaves them untouched. An
      ///   unblessed marriage (dormant today, see <see cref="UnblessedFamilyGratitudeRelationNudge" />) folds in as
      ///   gratitude instead, regardless of who initiated the split — this family never wanted the match, so its end
      ///   is a relief even when the spouse themselves was the one set aside.
      /// </summary>
      private static PartyReaction ResolveDirectFamilyReaction(DivorceFacts facts)
      {
         if (!facts.MarriageWasBlessed) return Gratitude(UnblessedFamilyGratitudeRelationNudge);

         return facts.MutualConsent
            ? NoneReaction()
            : Grudge(DirectFamilyGrudgeMagnitude, true);
      }

      /// <summary>
      ///   The three best friends' verdict: a repudiated split earns each a hidden grudge on their friend's behalf;
      ///   mutual consent leaves them untouched. Unlike the family, friends do not care whether the marriage was ever
      ///   a blessed political alliance — they are reacting to their friend's own heartbreak, not a lost match.
      /// </summary>
      private static PartyReaction ResolveFriendReaction(DivorceFacts facts)
         => facts.MutualConsent
            ? NoneReaction()
            : Grudge(FriendGrudgeMagnitude, false);

      private static PartyReaction ResolveSpouseReaction(DivorceFacts facts)
      {
         if (facts.MutualConsent) return Relief(SpouseMutualReliefRelationNudge);

         int magnitude = SpouseGrudgeBaseMagnitude +
                         (facts.SpouseRegardForPlayer <= LowRegardThreshold
                            ? SpouseGrudgeLowRegardBonus
                            : 0);

         return Grudge(magnitude, true);
      }

      #endregion
   }
}
