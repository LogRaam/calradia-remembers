// Code written by Gabriel Mailhot, 26/09/2026.
// Documents the rules of an arrest "in the King's name" (Gabriel's rulings, 26/09/2026). Who can be arrested, who
// sits in judgement, how long a conviction runs, and the case from warrant to release. The stakes: an acquittal that
// left a charge behind would let the same fault be tried twice; a flight that cost nothing would make surrender
// pointless; a faction leader arrested by his own vassals would break the one line Gabriel drew.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Violence;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ArrestPolicyTests
   {
      // ── Who can be arrested ────────────────────────────────────────────────

      // Gabriel: a vassal or a mercenary answers to a liege.
      [Test]
      public void GIVEN_a_vassal_or_a_mercenary_WHEN_asked_THEN_they_can_be_arrested()
      {
         ArrestPolicy.CanBeArrested(new ArrestStandingFacts {IsVassal = true}).Should().BeTrue();
         ArrestPolicy.CanBeArrested(new ArrestStandingFacts {IsMercenary = true}).Should().BeTrue();
      }

      // Gabriel: once the player leads a faction, it is politics and war, never an arrest.
      [Test]
      public void GIVEN_a_faction_leader_WHEN_asked_THEN_they_cannot_be_arrested()
      {
         ArrestPolicy.CanBeArrested(new ArrestStandingFacts {IsFactionLeader = true, IsVassal = true}).Should().BeFalse();
      }

      // A player who serves no realm has no liege to answer to.
      [Test]
      public void GIVEN_a_player_in_no_realm_WHEN_asked_THEN_they_cannot_be_arrested()
      {
         ArrestPolicy.CanBeArrested(new ArrestStandingFacts()).Should().BeFalse();
         ArrestPolicy.CanBeArrested(null).Should().BeFalse();
      }

      // ── What is grave ──────────────────────────────────────────────────────

      // Violence against a member of the player's own realm is a charge; a killing that failed is still an assault.
      [TestCase(ViolentAct.Kill, ViolenceVerdict.Succeeded, ArrestCharge.KilledRealmMember)]
      [TestCase(ViolentAct.Kill, ViolenceVerdict.Failed, ArrestCharge.AssaultedRealmMember)]
      [TestCase(ViolentAct.Assault, ViolenceVerdict.Succeeded, ArrestCharge.AssaultedRealmMember)]
      [TestCase(ViolentAct.Assault, ViolenceVerdict.Failed, ArrestCharge.AssaultedRealmMember)]
      [TestCase(ViolentAct.Rob, ViolenceVerdict.Succeeded, ArrestCharge.RobbedRealmMember)]
      public void GIVEN_an_act_of_violence_WHEN_charged_THEN_it_is_the_matching_charge(ViolentAct act, ViolenceVerdict verdict, ArrestCharge expected)
      {
         ArrestPolicy.ChargeFor(act, verdict).Should().Be(expected);
      }

      // An act the campaign never allowed (killing with the toggle off) was never attempted, so nothing is charged.
      [Test]
      public void GIVEN_an_act_never_attempted_WHEN_charged_THEN_there_is_no_charge()
      {
         ArrestPolicy.ChargeFor(ViolentAct.Kill, ViolenceVerdict.Forbidden).Should().BeNull();
      }

      // The warrant says what the player answers for, in words, so the arrest is never a mystery.
      [Test]
      public void GIVEN_charges_WHEN_the_warrant_is_read_THEN_it_names_them_in_plain_words()
      {
         ArrestPolicy.Describe(new[] {ArrestCharge.TreasonLetter}).Should().Be("treason");
         ArrestPolicy.Describe(new[] {ArrestCharge.TreasonLetter, ArrestCharge.RobbedRealmMember, ArrestCharge.FledArrest})
                     .Should().Be("treason, robbery of a member of the realm and flight from arrest");
         ArrestPolicy.Describe(null).Should().Be("crimes against the realm");
      }

      // ── The council ────────────────────────────────────────────────────────

      // Gabriel: the liege and the vassals who hate the player most.
      [Test]
      public void GIVEN_the_realms_vassals_WHEN_the_council_is_seated_THEN_the_liege_sits_with_those_who_hate_the_player_most()
      {
         var vassals = new List<CouncilCandidate> {
            new() {HeroId = "friend", RegardForPlayer = 40},
            new() {HeroId = "rival", RegardForPlayer = -30},
            new() {HeroId = "enemy", RegardForPlayer = -80},
            new() {HeroId = "cold", RegardForPlayer = -5},
            new() {HeroId = "neutral", RegardForPlayer = 0}
         };

         ArrestPolicy.CouncilSeats("king", vassals, "player").Should().Equal("king", "enemy", "rival", "cold");
      }

      // The liege never sits twice, and the accused never judges themselves.
      [Test]
      public void GIVEN_the_liege_or_the_player_among_the_vassals_WHEN_seated_THEN_neither_takes_a_vassal_seat()
      {
         var vassals = new List<CouncilCandidate> {
            new() {HeroId = "king", RegardForPlayer = -100},
            new() {HeroId = "player", RegardForPlayer = -100},
            new() {HeroId = "enemy", RegardForPlayer = -50}
         };

         ArrestPolicy.CouncilSeats("king", vassals, "player").Should().Equal("king", "enemy");
      }

      // ── The sentence ───────────────────────────────────────────────────────

      // Each charge weighs on the sentence; a graver fault means a longer stay.
      [Test]
      public void GIVEN_graver_charges_WHEN_sentenced_THEN_the_prison_runs_longer()
      {
         int robbery = ArrestPolicy.PrisonDays(new[] {ArrestCharge.RobbedRealmMember});
         int killing = ArrestPolicy.PrisonDays(new[] {ArrestCharge.KilledRealmMember});

         killing.Should().BeGreaterThan(robbery);
      }

      // Gabriel: the flight is held against the player at the trial.
      [Test]
      public void GIVEN_a_player_who_fled_WHEN_sentenced_THEN_the_flight_adds_to_the_prison()
      {
         ArrestPolicy.PrisonDays(new[] {ArrestCharge.TreasonLetter, ArrestCharge.FledArrest})
                     .Should().BeGreaterThan(ArrestPolicy.PrisonDays(new[] {ArrestCharge.TreasonLetter}));
      }

      // No sentence runs forever.
      [Test]
      public void GIVEN_every_charge_at_once_WHEN_sentenced_THEN_the_prison_is_capped()
      {
         ArrestPolicy.PrisonDays(new[] {
            ArrestCharge.KilledRealmMember, ArrestCharge.TreasonLetter, ArrestCharge.AssaultedRealmMember,
            ArrestCharge.RobbedRealmMember, ArrestCharge.SevereCrimeRating, ArrestCharge.FledArrest
         }).Should().Be(ArrestPolicy.MaxPrisonDays);
      }

      // ── The case, warrant to release ───────────────────────────────────────

      // A grave act brings a warrant in the liege's name.
      [Test]
      public void GIVEN_no_case_WHEN_a_grave_act_is_charged_THEN_a_warrant_goes_out_in_the_lieges_name()
      {
         var c = new ArrestCase();
         c.Charge(ArrestCharge.TreasonLetter, "king", canBeArrested: true);

         c.Stage.Should().Be(ArrestStage.WarrantOut);
         c.LiegeId.Should().Be("king");
         c.Charges.Should().Equal(ArrestCharge.TreasonLetter);
      }

      // A player who cannot be arrested (a faction leader) is not charged at all.
      [Test]
      public void GIVEN_a_player_who_cannot_be_arrested_WHEN_charged_THEN_nothing_happens()
      {
         var c = new ArrestCase();
         c.Charge(ArrestCharge.KilledRealmMember, "king", canBeArrested: false);

         c.Stage.Should().Be(ArrestStage.None);
         c.Charges.Should().BeEmpty();
      }

      // One case at a time: a second grave act while the warrant is out is one more charge on it.
      [Test]
      public void GIVEN_a_warrant_out_WHEN_another_grave_act_is_charged_THEN_it_joins_the_same_case()
      {
         var c = new ArrestCase();
         c.Charge(ArrestCharge.TreasonLetter, "king", true);
         c.Charge(ArrestCharge.AssaultedRealmMember, "other_king", true);

         c.Charges.Should().Equal(ArrestCharge.TreasonLetter, ArrestCharge.AssaultedRealmMember);
         c.LiegeId.Should().Be("king");
      }

      // Gabriel: fleeing makes the player an outlaw with a bounty on their head, and it is a charge of its own.
      [Test]
      public void GIVEN_a_warrant_out_WHEN_the_player_flees_THEN_they_are_an_outlaw_with_a_bounty_and_a_new_charge()
      {
         var c = new ArrestCase();
         c.Charge(ArrestCharge.TreasonLetter, "king", true);
         c.Flee();

         c.Stage.Should().Be(ArrestStage.Outlaw);
         c.BountyOnHead.Should().BeTrue();
         c.Charges.Should().Contain(ArrestCharge.FledArrest);
      }

      // Gabriel: captured, the bounty is removed (as if the captor collected it), and the player is tried all the same.
      [Test]
      public void GIVEN_an_outlaw_WHEN_captured_THEN_the_bounty_is_removed_and_they_go_to_trial()
      {
         var c = new ArrestCase();
         c.Charge(ArrestCharge.TreasonLetter, "king", true);
         c.Flee();
         c.Taken();

         c.Stage.Should().Be(ArrestStage.OnTrial);
         c.BountyOnHead.Should().BeFalse();
         c.Charges.Should().Contain(ArrestCharge.FledArrest);
      }

      // Gabriel: an acquittal wipes the reason entirely; only a new grave act brings another arrest.
      [Test]
      public void GIVEN_a_trial_WHEN_the_council_acquits_THEN_every_charge_is_wiped()
      {
         var c = new ArrestCase();
         c.Charge(ArrestCharge.KilledRealmMember, "king", true);
         c.Taken();
         c.Verdict(convicted: false, today: 100);

         c.Stage.Should().Be(ArrestStage.None);
         c.Charges.Should().BeEmpty();
         c.LiegeId.Should().BeNull();
      }

      // Gabriel: losing the trial means prison, for as long as the charges weigh.
      [Test]
      public void GIVEN_a_trial_WHEN_the_council_convicts_THEN_the_player_is_imprisoned_for_the_sentence()
      {
         var c = new ArrestCase();
         c.Charge(ArrestCharge.RobbedRealmMember, "king", true);
         c.Taken();
         c.Verdict(convicted: true, today: 100);

         c.Stage.Should().Be(ArrestStage.Imprisoned);
         c.ReleaseDay.Should().Be(100 + ArrestPolicy.PrisonDays(new[] {ArrestCharge.RobbedRealmMember}));
      }

      // The sentence served, the case is closed and the charges paid for.
      [Test]
      public void GIVEN_a_prisoner_WHEN_the_sentence_has_run_THEN_they_are_released_and_the_case_closed()
      {
         var c = new ArrestCase();
         c.Charge(ArrestCharge.RobbedRealmMember, "king", true);
         c.Taken();
         c.Verdict(true, 100);

         c.SentenceServed(c.ReleaseDay - 1).Should().BeFalse();
         c.SentenceServed(c.ReleaseDay).Should().BeTrue();

         c.Release();
         c.Stage.Should().Be(ArrestStage.None);
         c.Charges.Should().BeEmpty();
      }

      // A prisoner who breaks out, awaiting trial or serving the sentence, has fled: an outlaw with a bounty.
      [TestCase(false)]
      [TestCase(true)]
      public void GIVEN_a_held_player_WHEN_they_escape_THEN_they_are_an_outlaw_with_the_flight_charged(bool convictedFirst)
      {
         var c = new ArrestCase();
         c.Charge(ArrestCharge.RobbedRealmMember, "king", true);
         c.Taken();
         if (convictedFirst) c.Verdict(true, 100);

         c.Escape();

         c.Stage.Should().Be(ArrestStage.Outlaw);
         c.BountyOnHead.Should().BeTrue();
         c.Charges.Should().Contain(ArrestCharge.FledArrest);
      }

      // Steps out of order change nothing: no trial without a taking, no flight from a trial.
      [Test]
      public void GIVEN_a_step_out_of_order_WHEN_applied_THEN_the_case_is_unchanged()
      {
         var c = new ArrestCase();
         c.Verdict(true, 100);
         c.Flee();
         c.Taken();
         c.Stage.Should().Be(ArrestStage.None);

         c.Charge(ArrestCharge.TreasonLetter, "king", true);
         c.Taken();
         c.Flee();
         c.Stage.Should().Be(ArrestStage.OnTrial);
      }
   }
}
