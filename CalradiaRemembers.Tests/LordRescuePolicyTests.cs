// Code written by Gabriel Mailhot, 19/07/2026.
// Rescue pillar, increment 0 (passive). Decides whether a lord rides to break the captivity the player is
// sitting in, and on what grounds.
//
// Where it came from: a lord fell upon the bandits holding Gabriel's character and she slipped away in the
// confusion. It was an accident of vanilla AI, and the mod had no part in it. His call was to make the
// thing deliberate: CR triggers a liberation explicitly, and NOT every time, so that being a prisoner keeps
// its weight and the captivity event actually gets to happen.
//
// The eligibility rule is NOT "who likes the player enough". The transcript corrected that: the lord framed
// it as duty, not affection ("a lord of the Empire does not hear that a woman on the road has been dragged
// off by bandits and simply ride on. That is duty, not... whatever you are hoping it is"). So an honourable
// stranger is a legitimate rescuer, and duty stands beside blood, love, oath and hatred as a motive.
//
// EVERY THRESHOLD BELOW IS A TUNING CONSTANT AWAITING GABRIEL'S RATIFICATION. The tests pin the SHAPE of
// the rule (which motives exist, which gates are hard, what beats what), not the numbers as gospel.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LordRescuePolicyTests
   {
      // ── The hard gates ────────────────────────────────────────────────────

      // THE GATE THAT DEFINES THE PILLAR (Gabriel's call): the rescue may not fire until the captivity has
      // been LIVED. A liberation on the first day would cancel the captivity rather than end it, and being
      // taken prisoner would stop costing the player anything at all.
      [Test]
      public void GIVEN_the_captivity_has_not_been_lived_yet_WHEN_evaluating_THEN_nobody_rides()
      {
         LordRescuePolicy.Rides(Facts(captivityWasExperienced: false, sharesPlayerClan: true))
                         .Should().BeFalse();
      }

      // Rationing, not realism, and it mirrors what the companion rescue already does: one attempt per
      // capture. Without it a well-connected player would be sprung by a queue of suitors.
      [Test]
      public void GIVEN_a_rescue_was_already_attempted_WHEN_evaluating_THEN_nobody_rides_again()
      {
         LordRescuePolicy.Rides(Facts(alreadyAttemptedThisCaptivity: true, sharesPlayerClan: true))
                         .Should().BeFalse();
      }

      // Scope decision for increment 1: the engine forbids a party attacking one it is not hostile to, so a
      // LORD captor could only be assaulted where a war already exists. That is a different rule with
      // different consequences (a diplomatic incident), so it is excluded rather than half-handled.
      [Test]
      public void GIVEN_the_captor_is_a_lord_rather_than_a_bandit_WHEN_evaluating_THEN_nobody_rides()
      {
         LordRescuePolicy.Rides(Facts(captorIsBandit: false, sharesPlayerClan: true))
                         .Should().BeFalse();
      }

      // A lord who is dead, imprisoned himself, or has no party cannot ride however much he might want to.
      [Test]
      public void GIVEN_the_lord_cannot_ride_WHEN_evaluating_THEN_nobody_rides()
      {
         LordRescuePolicy.Rides(Facts(lordIsFreeToRide: false, sharesPlayerClan: true)).Should().BeFalse();
      }

      // Distance is the difference between a rescue and a miracle: a lord across Calradia does not hear of
      // it, and would not arrive in any case.
      [Test]
      public void GIVEN_the_lord_is_far_away_WHEN_evaluating_THEN_nobody_rides()
      {
         LordRescuePolicy.Rides(Facts(withinRange: false, sharesPlayerClan: true)).Should().BeFalse();
      }

      // ── The motives ───────────────────────────────────────────────────────

      // Kin needs no justification, and it outranks the rest: a lord of the player's OWN clan does not
      // weigh his honour before going after family.
      [Test]
      public void GIVEN_the_lord_is_of_the_players_own_clan_WHEN_evaluating_THEN_he_rides_as_kin()
      {
         LordRescuePolicy.Evaluate(Facts(sharesPlayerClan: true))
                         .Should().Be(LordRescueMotive.Kinship);
      }

      // A spouse or lover rides. This is the motive a player will most expect to exist, and its absence
      // would read as the romance system not being connected to anything.
      [Test]
      public void GIVEN_a_romantic_bond_WHEN_evaluating_THEN_he_rides_out_of_devotion()
      {
         LordRescuePolicy.Evaluate(Facts(hasRomanticBond: true))
                         .Should().Be(LordRescueMotive.Devotion);
      }

      // The motive that needs no goodwill toward the player at all: he wants those captors dead on his own
      // account. Freeing the player is the consequence, and the honest word for it is not gratitude.
      [Test]
      public void GIVEN_he_holds_a_grudge_against_the_captors_WHEN_evaluating_THEN_he_rides_for_his_own_reasons()
      {
         LordRescuePolicy.Evaluate(Facts(holdsGrudgeAgainstCaptor: true))
                         .Should().Be(LordRescueMotive.Vendetta);
      }

      // An oath in either direction: a liege does not leave a sworn vassal in a ditch, and a vassal does not
      // leave his liege there either.
      [Test]
      public void GIVEN_an_oath_binds_them_WHEN_evaluating_THEN_he_rides_on_fealty()
      {
         LordRescuePolicy.Evaluate(Facts(boundByOath: true)).Should().Be(LordRescueMotive.Fealty);
      }

      // THE MOTIVE THE TRANSCRIPT WROTE, and the reason the policy is not just a regard threshold: a lord
      // who owes the player nothing still rides, because that is what he believes a lord does. Without this
      // path the pillar would only ever reward players who had already made friends.
      [Test]
      public void GIVEN_an_honourable_stranger_WHEN_evaluating_THEN_he_rides_out_of_duty()
      {
         LordRescuePolicy.Evaluate(Facts(honor: LordRescuePolicy.DutyHonorThreshold))
                         .Should().Be(LordRescueMotive.Duty);
      }

      // Pity is kept as its own path rather than summed with honour, because they are different reasons and
      // a lord may have either. A merciful man with no principles about lordship still does not leave
      // someone in a bandit camp.
      [Test]
      public void GIVEN_a_merciful_lord_with_no_honour_WHEN_evaluating_THEN_he_still_rides()
      {
         LordRescuePolicy.Evaluate(Facts(honor: -2, mercy: LordRescuePolicy.DutyMercyThreshold))
                         .Should().Be(LordRescueMotive.Duty);
      }

      // Regard is a real motive on its own: he thinks well enough of the player to risk his own men.
      [Test]
      public void GIVEN_high_personal_regard_WHEN_evaluating_THEN_he_rides_for_the_player()
      {
         LordRescuePolicy.Evaluate(Facts(personalRegard: LordRescuePolicy.RegardRidesThreshold))
                         .Should().Be(LordRescueMotive.Regard);
      }

      // THE OTHER HALF OF GABRIEL'S RULE, and the one that keeps captivity meaningful: a lord who owes the
      // player nothing and believes in nothing does NOT come. Nothing happens, and the interception that
      // would otherwise have fired is stopped rather than quietly becoming something else.
      [Test]
      public void GIVEN_a_dishonourable_stranger_who_owes_the_player_nothing_WHEN_evaluating_THEN_he_does_not_come()
      {
         LordRescuePolicy.Evaluate(Facts(honor: -1, mercy: 0, personalRegard: 0))
                         .Should().Be(LordRescueMotive.None);
      }

      // Indifference is not enough either: a lord of no particular virtue and no particular warmth stays
      // where he is. Pins that the duty path is genuinely demanding rather than a formality.
      [Test]
      public void GIVEN_a_lord_of_no_virtue_and_no_warmth_WHEN_evaluating_THEN_he_does_not_come()
      {
         LordRescuePolicy.Rides(Facts(honor: 0, mercy: 0, personalRegard: 10)).Should().BeFalse();
      }

      // ── Boundaries and ordering ───────────────────────────────────────────

      // The reported motive is the TRUEST thing to say about why he came, which matters because the
      // chronicle may name it: when CR dispatches the rescuer the intent is finally known, unlike a vanilla
      // battle where it can never be. Kin outranks a grudge he also happens to hold.
      [Test]
      public void GIVEN_several_motives_at_once_WHEN_evaluating_THEN_the_closest_bond_is_the_one_reported()
      {
         LordRescuePolicy.Evaluate(Facts(sharesPlayerClan: true, holdsGrudgeAgainstCaptor: true, honor: 2))
                         .Should().Be(LordRescueMotive.Kinship);
      }

      // The regard threshold is a floor, not a window: one point below it, this motive does not apply. The
      // lord may still ride on some other ground, which is why the assertion names the motive.
      [Test]
      public void GIVEN_regard_just_below_the_bar_WHEN_evaluating_THEN_regard_is_not_the_reason()
      {
         LordRescuePolicy.Evaluate(Facts(personalRegard: LordRescuePolicy.RegardRidesThreshold - 1))
                         .Should().NotBe(LordRescueMotive.Regard);
      }

      // ── Calling it off ────────────────────────────────────────────────────

      // GABRIEL'S RULE (2026-07-19): the player got out on their own before the rescuer arrived, so he
      // stops. A ride that outlives its reason is worse than none, because the lord then bears down on a
      // player who is no longer in danger, and that reads as a hostile pursuit rather than a rescue.
      [Test]
      public void GIVEN_the_player_freed_themself_first_WHEN_the_rescue_is_under_way_THEN_it_is_called_off()
      {
         LordRescuePolicy.ShouldAbandonRescue(new LordRescuePolicy.RescueStandingFacts {PlayerStillCaptive = false, CaptorPartyStillExists = true, LordStillFreeToRide = true})
                         .Should().BeTrue();
      }

      // Somebody else destroyed the band first. There is nothing left to charge, and continuing would send
      // the lord at empty ground.
      [Test]
      public void GIVEN_the_captors_are_already_gone_WHEN_the_rescue_is_under_way_THEN_it_is_called_off()
      {
         LordRescuePolicy.ShouldAbandonRescue(new LordRescuePolicy.RescueStandingFacts {PlayerStillCaptive = true, CaptorPartyStillExists = false, LordStillFreeToRide = true})
                         .Should().BeTrue();
      }

      // The rescuer himself was killed or taken on the way. Pins that the check covers his own fate too,
      // rather than leaving a dead lord's order standing.
      [Test]
      public void GIVEN_the_rescuer_can_no_longer_ride_WHEN_the_rescue_is_under_way_THEN_it_is_called_off()
      {
         LordRescuePolicy.ShouldAbandonRescue(new LordRescuePolicy.RescueStandingFacts {PlayerStillCaptive = true, CaptorPartyStillExists = true, LordStillFreeToRide = false}).Should().BeTrue();
      }

      // The ordinary case, so the three guards above cannot be satisfied by abandoning everything: the
      // player is still held, the band still stands, the lord still rides. He keeps coming.
      [Test]
      public void GIVEN_nothing_has_changed_WHEN_the_rescue_is_under_way_THEN_he_keeps_coming()
      {
         LordRescuePolicy.ShouldAbandonRescue(new LordRescuePolicy.RescueStandingFacts {PlayerStillCaptive = true, CaptorPartyStillExists = true, LordStillFreeToRide = true}).Should().BeFalse();
      }

      // Fail safe: the host has nothing to offer (no player, no captor resolved). Silence, not a rescue.
      [Test]
      public void GIVEN_no_facts_at_all_WHEN_evaluating_THEN_nobody_rides()
      {
         LordRescuePolicy.Evaluate(null).Should().Be(LordRescueMotive.None);
         LordRescuePolicy.Rides(null).Should().BeFalse();
      }

      #region private

      /// <summary>
      ///   A lord who CAN ride and for whom the captivity gate is open, so each test supplies only the one
      ///   fact it is about. Defaults deliberately give him no bond and no virtue: every motive has to be
      ///   asked for explicitly, so none of them can pass by accident.
      /// </summary>
      private static LordRescueFacts Facts(bool captivityWasExperienced = true,
                                           bool lordIsFreeToRide = true,
                                           bool withinRange = true,
                                           bool alreadyAttemptedThisCaptivity = false,
                                           bool captorIsBandit = true,
                                           bool sharesPlayerClan = false,
                                           bool hasRomanticBond = false,
                                           bool boundByOath = false,
                                           bool holdsGrudgeAgainstCaptor = false,
                                           int personalRegard = 0,
                                           int honor = 0,
                                           int mercy = 0)
         => new(captivityWasExperienced, lordIsFreeToRide, withinRange, alreadyAttemptedThisCaptivity,
            captorIsBandit, sharesPlayerClan, hasRomanticBond, boundByOath, holdsGrudgeAgainstCaptor,
            personalRegard, honor, mercy);

      #endregion
   }
}
