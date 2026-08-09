// Code written by Gabriel Mailhot, 20/06/2026.
// The v1.5 competition loop (JealousyBehavior.EvaluateCompetition): once the player keeps several
// sharing-culture partners, this picks AT MOST ONE to feel neglected on a given pass, or none.
// The answer decides who cools toward the player, who writes a demanding letter, and (for a
// long-neglected high-hierarchy senior) who withdraws from the bond entirely (Estranged). Fire on
// the wrong partner, or on an evenly-attended household, and a player juggling several genuine
// loves gets punished for a slight that never happened; fail to fire on a real one and the
// competition mechanic goes silent.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompetitionSelectorTests
   {
      // The floor the whole selector stands on: neglect below NeglectThresholdDays is just normal
      // life, not a slight. Firing here would punish a player for the ordinary rhythm of attending
      // several partners in turn.
      [Test]
      public void GIVEN_a_household_all_recently_seen_WHEN_selecting_THEN_no_one_competes()
      {
         //Arrange — none past the absolute neglect floor.
         var partners = new List<PartnerStanding> {Standing("a", 2), Standing("b", 5)};

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull();
      }

      // ToleratesSharing false means this partner's culture reads a shared partner as BETRAYAL, not
      // competition (JealousyReaction.Severity handles that case separately, at a far harsher scale).
      // The selector must skip them, or a monogamous partner's grievance would be undersold as a
      // mere rank sting instead of the betrayal it actually is.
      [Test]
      public void GIVEN_a_neglected_but_monogamous_partner_WHEN_selecting_THEN_they_do_not_merely_compete()
      {
         //Arrange — a monogamous partner is betrayed, not in competition; the selector skips them.
         var partners = new List<PartnerStanding> {
            Standing("favoured", 0, false),
            Standing("exclusive", 25, false)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull();
      }

      // Pins InequityGapDays as the boundary just short of qualifying: neglect past the floor is not
      // enough on its own, it must ALSO be markedly worse than the most-attended partner. One day
      // short of the gap must still read as "roughly even", not a relative slight.
      [Test]
      public void GIVEN_a_partner_just_past_the_floor_but_within_the_inequity_gap_WHEN_selecting_THEN_they_do_not_react()
      {
         //Arrange — neglected (>= floor) but only InequityGapDays-1 more than the favoured partner.
         var partners = new List<PartnerStanding> {
            Standing("favoured", CompetitionSelector.NeglectThresholdDays - 1, false),
            Standing("close", CompetitionSelector.NeglectThresholdDays - 1 + CompetitionSelector.InequityGapDays - 1)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull();
      }

      // Per the class doc: "an even, if thin, distribution of attention is no slight". Without the
      // relative (inequity-gap) half of the gate, a player who is simply busy (away on campaign,
      // say) would get every sharing-culture partner competing against them at once.
      [Test]
      public void GIVEN_everyone_neglected_evenly_WHEN_selecting_THEN_no_one_feels_singled_out()
      {
         //Arrange — both past the floor, but the gap between them is below the inequity threshold.
         var partners = new List<PartnerStanding> {Standing("a", 20), Standing("b", 20)};

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull("an even, if thin, distribution of attention is no slight");
      }

      // Rivalry needs a rival: JealousyBehavior.EvaluateCompetition itself bails before calling the
      // selector when partners.Count < 2, but the selector pins the same guard so it stays safe if
      // ever called directly, e.g. from a future caller or the debug hook.
      [Test]
      public void GIVEN_fewer_than_two_partners_WHEN_selecting_THEN_no_one_competes()
      {
         //Arrange
         var partners = new List<PartnerStanding> {Standing("solo", 40)};

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull("rivalry needs a household of several");
      }

      // The core positive case: a real, relative slight (favoured partner riding along at 0 days,
      // the other left 25 days) must actually fire, or the whole competition mechanic never
      // triggers and the "polygamous households compete for attention" design goes unfelt.
      [Test]
      public void GIVEN_one_partner_neglected_far_more_than_the_favoured_one_WHEN_selecting_THEN_they_compete()
      {
         //Arrange — the player dotes on the anchor, leaving the other to wither.
         var partners = new List<PartnerStanding> {
            Standing("favoured", 0, false),
            Standing("slighted", 25)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().Be("slighted");
      }

      // The class doc promises AT MOST ONE reaction per pass. Without picking the single
      // most-neglected, several partners could all fire in the same pass, spamming the player with
      // more than one jealous letter/relation hit for what is really one uneven-attention problem.
      [Test]
      public void GIVEN_several_eligible_partners_WHEN_selecting_THEN_the_most_neglected_wins()
      {
         //Arrange
         var partners = new List<PartnerStanding> {
            Standing("favoured", 0, false),
            Standing("aggrieved", 16),
            Standing("most_aggrieved", 25)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().Be("most_aggrieved");
      }

      // OnCooldown mirrors JealousyBehavior's CompetitionCooldownDays (15 days between reactions per
      // partner). Without this guard, the same partner could fire again on the very next daily tick,
      // turning one grievance into a barrage of relation hits and letters.
      [Test]
      public void GIVEN_the_only_eligible_partner_is_on_cooldown_WHEN_selecting_THEN_they_are_skipped()
      {
         //Arrange
         var partners = new List<PartnerStanding> {
            Standing("favoured", 0, false),
            Standing("slighted", 25, onCooldown: true)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull();
      }

      // Open relationship (Inc 2), the core exclusion: a partner on reciprocal open terms must NEVER be the
      // aggrieved party, even when she is the single most-neglected partner in the house. This is the reported
      // no-jealousy pact honoured at the selection seam: without it, the open wife the player leaves longest
      // would still fire competition jealousy she explicitly renounced.
      [Test]
      public void GIVEN_the_most_neglected_partner_is_open_WHEN_selecting_THEN_a_different_non_open_partner_is_chosen()
      {
         //Arrange: the open partner is the most neglected of all, yet accepts sharing; the next-most-neglected,
         //non-open partner is the one who is really slighted.
         var partners = new List<PartnerStanding> {
            Standing("favoured", 0, false),
            Standing("slighted", 25),
            Standing("open_and_neglected", 40, open: true)
         };

         //Act/Assert: the open partner is skipped, and the non-open slighted one wins instead.
         CompetitionSelector.SelectAggrieved(partners).Should().Be("slighted");
      }

      // The open partner must still COUNT toward the most-attended baseline she helps set (she is part of the
      // household), only never be CHOSEN. Here she is the sole neglected partner; excluding her from selection
      // leaves no one aggrieved, which is exactly the drama-free result an all-open household should give.
      [Test]
      public void GIVEN_the_only_neglected_partner_is_open_WHEN_selecting_THEN_no_one_competes()
      {
         //Arrange: the only partner past the floor is on open terms; the rest are well attended.
         var partners = new List<PartnerStanding> {
            Standing("favoured", 0, false),
            Standing("open_and_neglected", 30, open: true)
         };

         //Act/Assert
         CompetitionSelector.SelectAggrieved(partners).Should().BeNull("an open partner never competes for attention");
      }

      #region private

      private static PartnerStanding Standing(string id, int neglect, bool tolerates = true, bool onCooldown = false, bool open = false)
         => new() {
            PartnerId = id,
            ToleratesSharing = tolerates,
            NeglectDays = neglect,
            OnCooldown = onCooldown,
            IsOpenArrangement = open
         };

      #endregion
   }
}