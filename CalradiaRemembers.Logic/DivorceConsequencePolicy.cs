// Code written by Gabriel Mailhot, 04/07/2026.

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
      /// <summary>Same as <see cref="SpouseClanValuedAlliance" />, for the clan of the NPC who initiated the divorce.</summary>
      public bool DivorcerClanValuedAlliance { get; set; }

      /// <summary>
      ///   True when the marriage was an approved/arranged union the families valued as a political alliance, vs made
      ///   against their will.
      /// </summary>
      public bool MarriageWasBlessed { get; set; }

      /// <summary>True when the set-aside spouse acquiesced (never resisted) — see <see cref="DivorcePolicy.IsMutualConsent" />.</summary>
      public bool MutualConsent { get; set; }

      /// <summary>
      ///   True when the ex-spouse's own clan saw this marriage as a valuable political alliance (typically paired with a
      ///   blessed union).
      /// </summary>
      public bool SpouseClanValuedAlliance { get; set; }

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
      /// <summary>The divorcer's own clan's reaction (applied per living clan member by the engine).</summary>
      public PartyReaction DivorcerClanReaction { get; set; }

      /// <summary>The ex-spouse's own clan's reaction (applied per living clan member by the engine).</summary>
      public PartyReaction SpouseClanReaction { get; set; }

      /// <summary>The set-aside spouse's own reaction.</summary>
      public PartyReaction SpouseReaction { get; set; }

      /// <summary>
      ///   The family that opposed an unblessed marriage and is glad to see it dissolved.
      ///   <see cref="ReactionKind.None" /> when the marriage was blessed.
      /// </summary>
      public PartyReaction UnblessedFamilyReaction { get; set; }
   }

   /// <summary>
   ///   Pure rules turning a divorce's facts into a signed per-party consequence matrix: who grudges, who
   ///   feels relief, who feels outright gratitude, and who is untouched. Replaces flat relation deltas
   ///   with a matrix keyed on WHO is affected and HOW they stood toward the union.
   /// </summary>
   public static class DivorceConsequencePolicy
   {
      /// <summary>
      ///   Tuning: the gratitude nudge for a clan whose unblessed marriage is finally over (they never wanted it, and now
      ///   do not have to keep enduring it).
      /// </summary>
      public const int ClanGratitudeRelationNudge = 12;

      // ── Clans (spouse's own, and the divorcer's own — symmetric rules) ──

      /// <summary>
      ///   Tuning: the per-clan-member grudge magnitude when a blessed, valued alliance is lost to the divorce (smaller
      ///   than the spouse's own — the spouse lived the betrayal directly, the clan only loses the match).
      /// </summary>
      public const int ClanGrudgeMagnitude = 35;

      /// <summary>
      ///   Tuning: the relief nudge for a clan that never particularly valued the alliance, even though the marriage had
      ///   been blessed.
      /// </summary>
      public const int ClanReliefRelationNudge = 8;

      /// <summary>
      ///   Tuning: at/below this prior regard for the player, the repudiated spouse already disliked them, so the grudge
      ///   is nursed HARDER (a modest magnitude bump; actual rigidity is still computed downstream by
      ///   <c>GrudgePolicy.RigidityAt</c> from this same regard).
      /// </summary>
      public const int LowRegardThreshold = -10;

      /// <summary>
      ///   Tuning: mutual consent SOFTENS what would otherwise be a clan grudge (a valued, blessed alliance lost) down to
      ///   a mild relief instead — both sides wanted it, so less blame lands on the player. Deliberately smaller than
      ///   <see cref="ClanReliefRelationNudge" />: this clan did lose something it valued, so its relief is more muted than a
      ///   clan that never cared either way.
      /// </summary>
      public const int MutualSoftenedClanReliefRelationNudge = 6;
      // ── Spouse (the set-aside party) ────────────────────────────────────

      /// <summary>Tuning: the repudiated spouse's grudge magnitude before the low-regard bonus.</summary>
      public const int SpouseGrudgeBaseMagnitude = 55;

      /// <summary>
      ///   Tuning: the magnitude bump applied when <see cref="DivorceFacts.SpouseRegardForPlayer" /> is at/below
      ///   <see cref="LowRegardThreshold" />.
      /// </summary>
      public const int SpouseGrudgeLowRegardBonus = 15;

      /// <summary>
      ///   Tuning: the small relief nudge for a spouse who acquiesced to the divorce (mutual consent softens even the one
      ///   party closest to it — they wanted this too).
      /// </summary>
      public const int SpouseMutualReliefRelationNudge = 5;

      // ── The unblessed-marriage family ───────────────────────────────────

      /// <summary>
      ///   Tuning: the gratitude nudge for the family that opposed an unblessed marriage from the start, now that it has
      ///   ended.
      /// </summary>
      public const int UnblessedFamilyGratitudeRelationNudge = 15;

      /// <summary>
      ///   True when a clan (spouse's own or the divorcer's own) reacts with a grudge: the marriage was
      ///   blessed, this clan valued it as an alliance, and the split was not mutual. Mutual consent always
      ///   softens this case to <see cref="ReactionKind.Relief" /> instead.
      /// </summary>
      public static bool ClanWouldGrudge(bool marriageWasBlessed, bool clanValuedAlliance, bool mutualConsent)
         => marriageWasBlessed && clanValuedAlliance && !mutualConsent;

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
            SpouseClanReaction = ResolveClanReaction(facts.MarriageWasBlessed, facts.SpouseClanValuedAlliance, facts.MutualConsent),
            DivorcerClanReaction = ResolveClanReaction(facts.MarriageWasBlessed, facts.DivorcerClanValuedAlliance, facts.MutualConsent),
            UnblessedFamilyReaction = ResolveUnblessedFamilyReaction(facts)
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
      ///   Shared logic for both <see cref="DivorceConsequences.SpouseClanReaction" /> and
      ///   <see cref="DivorceConsequences.DivorcerClanReaction" /> — the two are symmetric, differing only
      ///   in which "valued the alliance" fact feeds them. Precedence: a blessed, valued, non-mutual split
      ///   grudges; mutual consent softens that same case to relief; otherwise MarriageWasBlessed alone
      ///   decides between gratitude (unblessed: this clan never wanted the match) and mild relief (blessed,
      ///   but this clan simply did not prize the alliance).
      /// </summary>
      private static PartyReaction ResolveClanReaction(bool marriageWasBlessed, bool clanValuedAlliance, bool mutualConsent)
      {
         if (marriageWasBlessed && clanValuedAlliance)
            return mutualConsent
               ? Relief(MutualSoftenedClanReliefRelationNudge)
               : Grudge(ClanGrudgeMagnitude, false);

         return marriageWasBlessed
            ? Relief(ClanReliefRelationNudge)
            : Gratitude(ClanGratitudeRelationNudge);
      }

      private static PartyReaction ResolveSpouseReaction(DivorceFacts facts)
      {
         if (facts.MutualConsent) return Relief(SpouseMutualReliefRelationNudge);

         int magnitude = SpouseGrudgeBaseMagnitude +
                         (facts.SpouseRegardForPlayer <= LowRegardThreshold
                            ? SpouseGrudgeLowRegardBonus
                            : 0);

         return Grudge(magnitude, true);
      }

      private static PartyReaction ResolveUnblessedFamilyReaction(DivorceFacts facts)
         => facts.MarriageWasBlessed
            ? NoneReaction()
            : Gratitude(UnblessedFamilyGratitudeRelationNudge);

      #endregion
   }
}