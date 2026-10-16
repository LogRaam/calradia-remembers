// Code written by Gabriel Mailhot, 15/10/2026.
// Whether and how the realm's ruler calls in a vassal he holds to the war duty (Gabriel's ladder, 15/10/2026: the
// vassal is summoned to come and answer for sitting out the war). Until now the summons came only by letter, and the
// ruler sat in his hall: a vassal who walked into his town was never called in, and a ruler's field party kept him out
// of the fief audience altogether. Wrong one way, the summoned vassal stands beside his liege and nothing is said, or
// another lord's errand takes the meeting; wrong the other way, a ruler who already heard the vassal keeps sending for
// him, or a chase that missed writes a second letter beside the duty's own.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.WarDuty;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WarDutySummonsTests
   {
      // STAKE: the liege who summoned his vassal calls him in for that, not for a grudge or a piece of news; any other
      // errand would take the one meeting the duty needs.
      [Test]
      public void GIVEN_a_ruler_holding_the_reckoning_and_a_grudge_WHEN_the_reason_is_chosen_THEN_he_calls_the_vassal_in_over_the_war()
      {
         InterceptionPolicy.Choose(new InterceptionFacts {HoldsWarDutyReckoning = true, HoldsLiveGrudgeAgainstPlayer = true, HeardRecentPlayerDeed = true})
            .Should().Be(InterceptionTrigger.WarDutySummons);
      }

      // STAKE: the guards still hold; a ruler on cooldown, or while another lord is already riding, does not stack a
      // second approach on the player.
      [Test]
      public void GIVEN_a_ruler_holding_the_reckoning_on_cooldown_WHEN_the_reason_is_chosen_THEN_he_does_not_come()
      {
         InterceptionPolicy.Choose(new InterceptionFacts {HoldsWarDutyReckoning = true, OnCooldown = true}).Should().Be(InterceptionTrigger.None);
         InterceptionPolicy.Choose(new InterceptionFacts {HoldsWarDutyReckoning = true, AnotherPursuerActive = true}).Should().Be(InterceptionTrigger.None);
      }

      // STAKE: the king sits for the meeting and does not go out in person (Gabriel, 16/10/2026); a ruler chasing his
      // vassal across the map reads as a lord with nothing better to do, and every other errand still rides out.
      [Test]
      public void GIVEN_the_war_duty_summons_WHEN_asked_whether_it_sends_the_ruler_out_on_the_road_THEN_it_never_does()
      {
         InterceptionPolicy.IsRoadValid(InterceptionTrigger.WarDutySummons).Should().BeFalse();
         InterceptionPolicy.IsRoadValid(InterceptionTrigger.Grudge).Should().BeTrue();
         InterceptionPolicy.IsRoadValid(InterceptionTrigger.HeardOfDeed).Should().BeTrue();
      }

      // STAKE: a summoned vassal who walks into his liege's town is called to the hall; the summons reads as well from
      // a ruler who never moved as from one who rode out.
      [Test]
      public void GIVEN_the_war_duty_summons_WHEN_narrowed_to_a_fief_THEN_it_is_kept()
      {
         FiefAudiencePolicy.IsFiefValid(InterceptionTrigger.WarDutySummons).Should().BeTrue();
         FiefAudiencePolicy.ChooseFiefValid(new InterceptionFacts {HoldsWarDutyReckoning = true}).Should().Be(InterceptionTrigger.WarDutySummons);
      }

      // STAKE: in a busy town a notable whose id sorts first would otherwise take the one audience, and the summoned
      // vassal would walk past the liege who sent for him.
      [Test]
      public void GIVEN_a_notable_and_the_summoning_ruler_in_the_same_town_WHEN_one_is_chosen_THEN_the_ruler_calls_the_vassal_in()
      {
         FiefAudiencePolicy.SelectCandidate(new[] {
            new FiefAudienceCandidate("aaa_notable", InterceptionTrigger.HeardOfDeed),
            new FiefAudienceCandidate("zzz_ruler", InterceptionTrigger.WarDutySummons)
         }).HeroId.Should().Be("zzz_ruler");
      }

      // STAKE: a ruler in his own town still has a field party; without the exception he could never call the vassal
      // in where he stands, and every other lord with a party stays out of the fief audience as before.
      [Test]
      public void GIVEN_a_ruler_with_a_field_party_in_this_town_WHEN_asked_whether_he_may_seek_an_audience_THEN_only_the_summoning_ruler_may()
      {
         FiefAudiencePolicy.MaySeekAudience(new FiefAudienceSeekerFacts {StandsInThisSettlement = true, HasFieldParty = true, SummonsThePlayer = true}).Should().BeTrue();
         FiefAudiencePolicy.MaySeekAudience(new FiefAudienceSeekerFacts {StandsInThisSettlement = true, HasFieldParty = true}).Should().BeFalse();
         FiefAudiencePolicy.MaySeekAudience(new FiefAudienceSeekerFacts {StandsInThisSettlement = true, HasFieldParty = true, SummonsThePlayer = true, IsPrisoner = true}).Should().BeFalse();
      }

      // STAKE: the duty writes its own levy letter when the vassal never came; a missed-meeting letter beside it would
      // say the same thing twice, or contradict the terms.
      [Test]
      public void GIVEN_a_war_duty_chase_that_came_to_nothing_WHEN_the_letter_is_judged_THEN_no_missed_meeting_letter_is_written()
      {
         InterceptionPolicy.ShouldWriteMissedMeetingLetter(AbandonReason.GaveUp, true, InterceptionTrigger.WarDutySummons).Should().BeFalse();
         InterceptionPolicy.ShouldWriteMissedMeetingLetter(AbandonReason.GaveUp, true, InterceptionTrigger.Grudge).Should().BeTrue();
      }

      // STAKE: summoned, or owing by letter because he never came, the vassal is called in; once he has stood before
      // his liege the levy is between them, and the ruler sending for him again would be harassment.
      [Test]
      public void GIVEN_each_step_of_the_reckoning_WHEN_asked_whether_the_ruler_calls_the_vassal_in_THEN_only_before_he_has_come()
      {
         WarDutyLadder.CallsThePlayerIn(new WarDutyCase {Stage = WarDutyStage.Summoned}).Should().BeTrue();
         WarDutyLadder.CallsThePlayerIn(new WarDutyCase {Stage = WarDutyStage.Owing, Appeared = false}).Should().BeTrue();
         WarDutyLadder.CallsThePlayerIn(new WarDutyCase {Stage = WarDutyStage.Owing, Appeared = true}).Should().BeFalse();
         WarDutyLadder.CallsThePlayerIn(new WarDutyCase {Stage = WarDutyStage.Settled}).Should().BeFalse();
         WarDutyLadder.CallsThePlayerIn(null).Should().BeFalse();
      }
   }
}
