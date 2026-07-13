// Code written by Gabriel Mailhot, 04/07/2026.
// Divorce, the tiered CONSEQUENCE ENGINE: given already-resolved facts (MutualConsent, sourced from
// PlayerDivorceConsentPolicy.SpouseConsents in Phase 2a, or DivorcePolicy.IsMutualConsent in Phase 2b;
// MarriageWasBlessed; the spouse's prior regard), this policy decides who reacts and how, across three
// stakes: the spouse, their immediate family, and their three best friends. DivorceProcessBehavior
// (ApplyReactionTo / ApplySpouseReaction) consumes the verdict almost blindly: PlantGrudge plants a
// real, persisted grudge at the returned magnitude; Relief/Gratitude only fires when RelationNudge is
// nonzero (the caller checks the NUDGE, not the Kind), so a Kind/field mismatch here silently drops an
// intended reaction on a live save.

#region

using System;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DivorceConsequencePolicyTests
   {
      // Elementary sign-check for the SpouseGrudges helper (a reading aid for call sites/tests, per its
      // own doc comment): mutual consent must map to "no grudge", the simplest case a stray boolean
      // flip would break.
      [Test]
      public void GIVEN_a_mutual_consent_spouse_WHEN_asking_whether_the_spouse_grudges_THEN_it_is_false()
      {
         DivorceFacts facts = RepudiatedBlessed();
         facts.MutualConsent = true;

         DivorceConsequencePolicy.SpouseGrudges(facts).Should().BeFalse();
      }

      // The blessed+mutual shape is what DivorceProcessBehavior actually builds today (MarriageWasBlessed
      // is hardcoded true until a forced/unblessed-marriage feature exists), so this is the common
      // real-world mutual divorce: the spouse gets relief, nobody else is touched. Getting it wrong
      // either grudges a split both parties wanted, or silently drops the spouse's relief gain
      // (ApplyReactionTo only applies a nudge when RelationNudge != 0, not when Kind == Relief).
      [Test]
      public void GIVEN_a_mutual_divorce_from_a_blessed_marriage_WHEN_resolving_THEN_the_spouse_is_relieved_and_family_and_friends_feel_nothing()
      {
         DivorceFacts facts = RepudiatedBlessed();
         facts.MutualConsent = true;

         DivorceConsequences result = DivorceConsequencePolicy.Resolve(facts);

         result.SpouseReaction.Kind.Should().Be(ReactionKind.Relief);
         result.DirectFamilyReaction.Kind.Should().Be(ReactionKind.None);
         result.FriendReaction.Kind.Should().Be(ReactionKind.None);
      }

      // Exercises the DORMANT unblessed-marriage branch (see DivorceFacts.MarriageWasBlessed's own doc:
      // no live path sets this false yet). This test is the only thing pinning the family's Gratitude
      // shape before the forced/unblessed-marriage feature that would wake this branch up gets built.
      [Test]
      public void GIVEN_a_mutual_divorce_from_an_unblessed_marriage_WHEN_resolving_THEN_the_family_is_grateful_and_nobody_grudges()
      {
         var facts = new DivorceFacts {MutualConsent = true, MarriageWasBlessed = false, SpouseRegardForPlayer = 0};

         DivorceConsequences result = DivorceConsequencePolicy.Resolve(facts);

         result.SpouseReaction.Kind.Should().Be(ReactionKind.Relief);
         result.DirectFamilyReaction.Kind.Should().Be(ReactionKind.Gratitude);
         result.FriendReaction.Kind.Should().Be(ReactionKind.None);

         // Minimal blame: nobody in this grid grudges the player.
         result.SpouseReaction.Kind.Should().NotBe(ReactionKind.PlantGrudge);
         result.DirectFamilyReaction.Kind.Should().NotBe(ReactionKind.PlantGrudge);
         result.FriendReaction.Kind.Should().NotBe(ReactionKind.PlantGrudge);
      }

      // Pins the invariant PartyReaction's own doc promises for ReactionKind.None: both fields zero.
      // ApplyReactionTo checks "RelationNudge != 0", not "Kind == Relief", so a stray nonzero value here
      // would apply an unearned relation nudge to a party meant to feel nothing at all.
      [Test]
      public void GIVEN_a_none_reaction_WHEN_inspecting_it_THEN_both_the_magnitude_and_the_relation_nudge_are_zero()
      {
         DivorceFacts facts = RepudiatedBlessed();
         facts.MutualConsent = true;

         PartyReaction none = DivorceConsequencePolicy.Resolve(facts).FriendReaction;

         none.Kind.Should().Be(ReactionKind.None);
         none.GrudgeMagnitude.Should().Be(0);
         none.RelationNudge.Should().Be(0);
      }

      // Same invariant, grudge side: ApplyReactionTo only reads GrudgeMagnitude on the PlantGrudge
      // branch, never RelationNudge, so a leftover nonzero nudge here would be silently ignored by
      // production code rather than caught, and a non-positive magnitude means the grudge exists in
      // name only (it would also fail GrudgePolicy's formation floor, see the grid test below).
      [Test]
      public void GIVEN_a_plant_grudge_reaction_WHEN_inspecting_it_THEN_the_relation_nudge_is_zero_and_the_magnitude_is_positive()
      {
         PartyReaction reaction = DivorceConsequencePolicy.Resolve(RepudiatedBlessed()).SpouseReaction;

         reaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         reaction.GrudgeMagnitude.Should().BeGreaterThan(0);
         reaction.RelationNudge.Should().Be(0);
      }

      // Mirrors the two invariant tests above for the positive branches: Relief and Gratitude both need
      // RelationNudge > 0 for ApplyReactionTo/ApplySpouseReaction to apply anything (they gate on the
      // NUDGE, not the Kind). A zero nudge here means a spouse or family member marked "relieved" or
      // "grateful" gets nothing in-game, a silent no-op the player would never see reported.
      [Test]
      public void GIVEN_a_relief_or_gratitude_reaction_WHEN_inspecting_it_THEN_the_relation_nudge_is_positive_and_the_grudge_magnitude_is_zero()
      {
         DivorceFacts facts = RepudiatedBlessed();
         facts.MutualConsent = true;

         PartyReaction relief = DivorceConsequencePolicy.Resolve(facts).SpouseReaction;
         relief.Kind.Should().Be(ReactionKind.Relief);
         relief.RelationNudge.Should().BeGreaterThan(0);
         relief.GrudgeMagnitude.Should().Be(0);

         facts.MarriageWasBlessed = false;
         PartyReaction gratitude = DivorceConsequencePolicy.Resolve(facts).DirectFamilyReaction;
         gratitude.Kind.Should().Be(ReactionKind.Gratitude);
         gratitude.RelationNudge.Should().BeGreaterThan(0);
         gratitude.GrudgeMagnitude.Should().Be(0);
      }

      // ── The full grid, one representative case each ─────────────────────

      // The worst-case cell of the matrix: a blessed marriage ended against the spouse's will costs the
      // player the FULL vendetta (spouse + family + friends). Since 07/04/2026 this is deliberately
      // NARROWED from the old clan-wide grudge (Gabriel's ruling: 30% of the old clan magnitude fell
      // below the formation floor anyway, so a clan-wide grudge was mostly noise) to just these three.
      [Test]
      public void GIVEN_a_repudiated_spouse_from_a_blessed_marriage_WHEN_resolving_THEN_the_spouse_the_family_and_the_friends_all_grudge()
      {
         DivorceConsequences result = DivorceConsequencePolicy.Resolve(RepudiatedBlessed());

         result.SpouseReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.DirectFamilyReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.FriendReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
      }

      // The one grid cell where MOTIVE (blessed vs not) splits a single party's reaction from the
      // others': an unblessed marriage's family never wanted the match, so even a repudiated spouse
      // leaves them relieved, not aggrieved, while the spouse and friends still react to the personal
      // betrayal. Currently unreachable in live play (MarriageWasBlessed is hardcoded true today), but
      // pins the intended shape for when a forced-marriage feature can set it false.
      [Test]
      public void GIVEN_a_repudiated_spouse_from_an_unblessed_marriage_WHEN_resolving_THEN_the_spouse_and_friends_grudge_but_the_family_is_grateful()
      {
         var facts = new DivorceFacts {MutualConsent = false, MarriageWasBlessed = false, SpouseRegardForPlayer = 0};

         DivorceConsequences result = DivorceConsequencePolicy.Resolve(facts);

         result.SpouseReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
         result.DirectFamilyReaction.Kind.Should().Be(ReactionKind.Gratitude);
         result.FriendReaction.Kind.Should().Be(ReactionKind.PlantGrudge);
      }

      // ── Helper predicates ────────────────────────────────────────────────

      // Companion case to the mutual-consent SpouseGrudges check above: a repudiated spouse must read as
      // "grudges". Same elementary reading-aid guard; no live production caller consumes this predicate
      // today.
      [Test]
      public void GIVEN_a_repudiated_spouse_WHEN_asking_whether_the_spouse_grudges_THEN_it_is_true()
      {
         DivorceConsequencePolicy.SpouseGrudges(RepudiatedBlessed()).Should().BeTrue();
      }

      // ── Shape guarantees per reaction kind ──────────────────────────────

      // KnownToPlayer gates whether this grudge surfaces in the encyclopedia's Grievances section
      // (GrudgeBehavior filters live grudges on it). The spouse's own grudge must stay public: they were
      // the one set aside, there is no in-fiction reason to hide it from the player who caused it.
      [Test]
      public void GIVEN_a_repudiated_spouse_WHEN_resolving_THEN_the_spouse_grudge_is_known_to_the_player()
      {
         DivorceConsequencePolicy.Resolve(RepudiatedBlessed()).SpouseReaction.KnownToPlayer.Should().BeTrue();
      }

      // Same KnownToPlayer gate, the other two stakes: the family voices its anger openly (shows in
      // Grievances), but the three best friends' resentment stays hidden, matching FriendReaction's own
      // doc ("a quiet resentment... not a grievance the friend makes a show of"). Flip these and the
      // player either never learns why in-laws turned cold, or sees a friend's grudge meant to stay a
      // private slight.
      [Test]
      public void GIVEN_a_repudiated_spouse_WHEN_resolving_THEN_the_family_grudge_is_known_but_the_friend_grudge_is_hidden()
      {
         DivorceConsequences result = DivorceConsequencePolicy.Resolve(RepudiatedBlessed());

         result.DirectFamilyReaction.KnownToPlayer.Should().BeTrue();
         result.FriendReaction.KnownToPlayer.Should().BeFalse();
      }

      // ── Magnitudes clear the grudge-formation floor ─────────────────────

      // Every magnitude here must clear GrudgePolicy.MagnitudeFloor (15): GrudgeBehavior.TryPlant calls
      // GrudgePolicy.ShouldForm before planting anything, and a candidate below the floor is silently
      // refused. Undershoot here and DivorceProcessBehavior would believe it planted three grudges while
      // the player's Grievances stayed empty.
      [Test]
      public void GIVEN_the_repudiated_grid_WHEN_reading_every_grudge_magnitude_THEN_all_clear_the_formation_floor()
      {
         DivorceConsequences result = DivorceConsequencePolicy.Resolve(RepudiatedBlessed());

         result.SpouseReaction.GrudgeMagnitude.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);
         result.DirectFamilyReaction.GrudgeMagnitude.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);
         result.FriendReaction.GrudgeMagnitude.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);
      }

      // ── Spouse regard scaling the grudge magnitude ──────────────────────

      // Pins LowRegardThreshold as an INCLUSIVE boundary (<=): a spouse sitting exactly at -10 still
      // gets the nursed-harder bonus, one point above does not. This policy only scales the proposed
      // MAGNITUDE (a spouse who already disliked the player takes the repudiation harder); the grudge's
      // actual RIGIDITY is a separate, downstream computation (GrudgePolicy.RigidityAt) from this same
      // regard, which this policy deliberately leaves alone.
      [Test]
      public void GIVEN_a_repudiated_spouse_who_already_disliked_the_player_WHEN_resolving_THEN_the_grudge_is_larger()
      {
         DivorceFacts lowRegard = RepudiatedBlessed();
         lowRegard.SpouseRegardForPlayer = DivorceConsequencePolicy.LowRegardThreshold;

         DivorceFacts highRegard = RepudiatedBlessed();
         highRegard.SpouseRegardForPlayer = DivorceConsequencePolicy.LowRegardThreshold + 1;

         int lowRegardMagnitude = DivorceConsequencePolicy.Resolve(lowRegard).SpouseReaction.GrudgeMagnitude;
         int highRegardMagnitude = DivorceConsequencePolicy.Resolve(highRegard).SpouseReaction.GrudgeMagnitude;

         lowRegardMagnitude.Should().Be(DivorceConsequencePolicy.SpouseGrudgeBaseMagnitude + DivorceConsequencePolicy.SpouseGrudgeLowRegardBonus);
         highRegardMagnitude.Should().Be(DivorceConsequencePolicy.SpouseGrudgeBaseMagnitude);
         lowRegardMagnitude.Should().BeGreaterThan(highRegardMagnitude);
      }

      // ── Null guard ───────────────────────────────────────────────────────

      // Fail-safe default for the SpouseGrudges helper: no facts reads as "no grudge". Contrast with
      // Resolve just below, which instead THROWS on null: the two disagree on purpose (see that test).
      [Test]
      public void GIVEN_null_facts_WHEN_asking_whether_the_spouse_grudges_THEN_it_is_false()
      {
         DivorceConsequencePolicy.SpouseGrudges(null).Should().BeFalse();
      }

      // Resolve throws on null instead of defaulting, unlike SpouseGrudges above: per its own doc, a
      // divorce that reaches this policy is already a confirmed, resolved event, so missing facts is a
      // CALLER BUG, not a legitimate "nothing to judge yet" case. Surfacing loudly here is safer than
      // silently resolving a wrong consequence matrix on a divorce nobody actually described.
      [Test]
      public void GIVEN_null_facts_WHEN_resolving_THEN_it_throws()
      {
         Assert.Throws<ArgumentNullException>(() => DivorceConsequencePolicy.Resolve(null));
      }

      #region private

      private static DivorceFacts RepudiatedBlessed()
         => new() {MutualConsent = false, MarriageWasBlessed = true, SpouseRegardForPlayer = 0};

      #endregion
   }
}
