// Code written by Gabriel Mailhot, 14/11/2026.
// What DiplomacyCommitPolicy decides: what the registered authorities' answers mean for a war, a peace or a change of realm
// CR is about to make, and what the player reads when it did not happen. VonLemberg's team (Bellum Civile bridge RFC,
// 14/11/2026): "The central risk is a mismatch between what the LLM says happened and what the game actually committed."
// If this breaks, CR applies a peace Bellum refused, applies one Bellum already applied, or tells the player a treaty was
// signed while it waits on a vote.

#region

using CalradiaRemembers.Logic.Diplomacy;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DiplomacyCommitPolicyTests
   {
      // STAKE: with no authority registered, CR behaves exactly as before.
      [Test]
      public void GIVEN_no_authority_has_an_opinion_WHEN_decided_THEN_cr_applies_the_change_itself()
      {
         DiplomacyVerdict verdict = DiplomacyCommitPolicy.Decide(new DiplomacyVerdict[0]);

         verdict.Should().Be(DiplomacyVerdict.PassThrough);
         DiplomacyCommitPolicy.CrApplies(verdict).Should().BeTrue();
      }

      // THE RFC: a peace the authority refuses is not applied, and has not happened.
      [Test]
      public void GIVEN_an_authority_rejects_WHEN_decided_THEN_cr_applies_nothing_and_it_has_not_happened()
      {
         DiplomacyVerdict verdict = DiplomacyCommitPolicy.Decide(new[] {DiplomacyVerdict.PassThrough, DiplomacyVerdict.Rejected});

         DiplomacyCommitPolicy.CrApplies(verdict).Should().BeFalse();
         DiplomacyCommitPolicy.HasHappened(verdict).Should().BeFalse();
      }

      // THE RFC: a change the authority applied itself is not applied a second time, and has happened.
      [Test]
      public void GIVEN_an_authority_applied_it_WHEN_decided_THEN_cr_does_not_apply_it_again()
      {
         DiplomacyVerdict verdict = DiplomacyCommitPolicy.Decide(new[] {DiplomacyVerdict.Applied});

         DiplomacyCommitPolicy.CrApplies(verdict).Should().BeFalse();
         DiplomacyCommitPolicy.HasHappened(verdict).Should().BeTrue();
      }

      // THE RFC: "For Pending, CR must not tell the player or participating NPCs that the war has already ended".
      [Test]
      public void GIVEN_a_pending_peace_WHEN_the_player_is_told_THEN_it_says_it_has_not_happened_yet()
      {
         DiplomacyCommitPolicy.NotHappenedLine(DiplomacyVerdict.Pending, "Peace between Vlandia and Sturgia", null)
                              .Should().Be("(Peace between Vlandia and Sturgia is put to the realm's decision and has not happened yet.)");
      }

      // STAKE: the authority's own reason reaches the player.
      [Test]
      public void GIVEN_a_rejection_with_a_reason_WHEN_the_player_is_told_THEN_the_reason_is_named()
      {
         DiplomacyCommitPolicy.NotHappenedLine(DiplomacyVerdict.Rejected, "War on Sturgia", "the council has not voted it.")
                              .Should().Be("(War on Sturgia did not happen: the council has not voted it.)");
      }

      // STAKE: the first authority with an opinion decides, so two mods cannot both act on one change.
      [Test]
      public void GIVEN_two_authorities_with_opinions_WHEN_decided_THEN_the_first_in_order_decides()
      {
         DiplomacyCommitPolicy.Decide(new[] {DiplomacyVerdict.PassThrough, DiplomacyVerdict.Pending, DiplomacyVerdict.Applied}).Should().Be(DiplomacyVerdict.Pending);
      }
   }
}
