// Code written by Gabriel Mailhot, 15/10/2026.
// What the vassal war duty decides: whether the player owes his liege service in a war (WarDutyPolicy), the reckoning
// that follows when he gave none (WarDutyLadder), and the levy in coin that stands for his absence (WarLevyPolicy).
// Maifailun (Nexus, 15/10/2026) asked that a vassal who does not fight the realm's enemy hear from his ruler; Gabriel
// (15/10/2026) laid out the ladder: a summons to answer in person, then a levy in coin, or troops, horses or arms
// instead, and a fief taken if in the end he neither fights nor provides anything. Wrong one way, a mercenary or a
// vassal who did fight is summoned and fined; wrong the other way, the duty is never felt or a fief is taken unfairly.

#region

using CalradiaRemembers.Logic.WarDuty;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WarDutyTests
   {
      private static WarDutyFacts Idle(int? daysSinceServed = null) => new WarDutyFacts {
         IsSwornVassal = true, DaysAtWar = 40, DaysAsVassal = 200, DaysSinceServed = daysSinceServed, WindowDays = 30
      };

      // STAKE: Maifailun's case. A sworn vassal who sat out a month of his realm's war is called to account.
      [Test]
      public void GIVEN_a_sworn_vassal_who_sat_out_a_month_of_war_WHEN_judged_THEN_he_owes_service()
      {
         WarDutyPolicy.Decide(Idle()).Should().Be(WarDutyStanding.Owes);
      }

      // STAKE: a mercenary is paid by the day and swore no fealty; summoning him to account would be a false grievance.
      [Test]
      public void GIVEN_a_mercenary_WHEN_judged_THEN_he_owes_no_fealty()
      {
         WarDutyPolicy.Decide(new WarDutyFacts {IsSwornVassal = false, DaysAtWar = 90, DaysAsVassal = 300})
            .Should().Be(WarDutyStanding.NotAVassal);
      }

      // STAKE: the ruler does not summon himself.
      [Test]
      public void GIVEN_the_player_rules_the_realm_WHEN_judged_THEN_no_one_calls_him_to_account()
      {
         WarDutyPolicy.Decide(new WarDutyFacts {IsSwornVassal = true, RulesTheRealm = true, DaysAtWar = 90, DaysAsVassal = 300})
            .Should().Be(WarDutyStanding.RulesTheRealm);
      }

      // STAKE: a captive cannot ride; blaming him for it would be unjust.
      [Test]
      public void GIVEN_a_captive_vassal_WHEN_judged_THEN_he_owes_nothing_while_held()
      {
         WarDutyPolicy.Decide(new WarDutyFacts {IsSwornVassal = true, IsCaptive = true, DaysAtWar = 90, DaysAsVassal = 300})
            .Should().Be(WarDutyStanding.Captive);
      }

      // STAKE: there is no war to serve in.
      [Test]
      public void GIVEN_a_realm_at_peace_WHEN_judged_THEN_no_duty_is_owed()
      {
         WarDutyPolicy.Decide(new WarDutyFacts {IsSwornVassal = true, DaysAtWar = null, DaysAsVassal = 300})
            .Should().Be(WarDutyStanding.AtPeace);
      }

      // STAKE: an absence is measured within THIS war. A war ten days old has not given anyone a month to ride,
      // whatever he did before it began.
      [Test]
      public void GIVEN_a_war_younger_than_the_window_WHEN_judged_THEN_no_one_is_called_yet()
      {
         WarDutyPolicy.Decide(new WarDutyFacts {IsSwornVassal = true, DaysAtWar = 10, DaysAsVassal = 300})
            .Should().Be(WarDutyStanding.WarTooYoung);
      }

      // STAKE: a lord who swore last week is not summoned for a month he did not owe.
      [Test]
      public void GIVEN_a_vassal_sworn_a_week_ago_WHEN_judged_THEN_he_is_not_held_to_account_yet()
      {
         WarDutyPolicy.Decide(new WarDutyFacts {IsSwornVassal = true, DaysAtWar = 90, DaysAsVassal = 7})
            .Should().Be(WarDutyStanding.NewVassal);
      }

      // STAKE: a vassal who fought within the window did his part; summoning him would read as the mod not seeing him.
      [Test]
      public void GIVEN_a_vassal_who_fought_within_the_window_WHEN_judged_THEN_he_has_served()
      {
         WarDutyPolicy.Decide(Idle(daysSinceServed: 12)).Should().Be(WarDutyStanding.Served);
      }

      // STAKE: one reckoning per war, so the same war does not summon him again after he settled.
      [Test]
      public void GIVEN_a_reckoning_already_opened_this_war_WHEN_judged_again_THEN_he_is_not_summoned_twice()
      {
         WarDutyPolicy.Decide(new WarDutyFacts {
            IsSwornVassal = true, DaysAtWar = 90, DaysAsVassal = 300, AlreadyCalledThisWar = true
         }).Should().Be(WarDutyStanding.AlreadyCalledThisWar);
      }

      // STAKE: a hand-edited MCM window of 0 must not summon every vassal on the first day of every war.
      [Test]
      public void GIVEN_a_window_set_absurdly_low_WHEN_judged_THEN_the_window_is_held_to_its_floor()
      {
         WarDutyPolicy.Window(0).Should().Be(WarDutyPolicy.MinWindowDays);
      }

      // STAKE: Gabriel's ladder begins with a summons to come and answer in person, with time to reach a king on campaign.
      [Test]
      public void GIVEN_a_reckoning_opened_WHEN_it_starts_THEN_the_player_is_summoned_with_time_to_come()
      {
         WarDutyCase c = WarDutyLadder.Open("empire", "lucon", 3000, 100);

         c.Stage.Should().Be(WarDutyStage.Summoned);
         c.DeadlineDay.Should().Be(100 + WarDutyLadder.StageDays);
      }

      // STAKE: Gabriel: answering in person is where the ruler asks for the levy, face to face, with a new deadline.
      [Test]
      public void GIVEN_a_summoned_vassal_WHEN_he_comes_before_the_ruler_THEN_the_levy_is_demanded_in_person()
      {
         WarDutyCase c = WarDutyLadder.Open("empire", "lucon", 3000, 100);

         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.Appeared, Today = 110})
            .Should().Be(WarDutyEffect.DemandInPerson);
         c.Stage.Should().Be(WarDutyStage.Owing);
         c.DeadlineDay.Should().Be(110 + WarDutyLadder.StageDays);
      }

      // STAKE: a vassal who ignores the summons is not let off: the ruler demands the levy by letter.
      [Test]
      public void GIVEN_a_summons_ignored_past_its_deadline_WHEN_the_day_passes_THEN_the_levy_is_demanded_by_letter()
      {
         WarDutyCase c = WarDutyLadder.Open("empire", "lucon", 3000, 100);

         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.DayPassed, Today = 121})
            .Should().Be(WarDutyEffect.SendLevyLetter);
         c.Stage.Should().Be(WarDutyStage.Owing);
      }

      // STAKE: the deadline day itself is still his; the letter must not leave a day early.
      [Test]
      public void GIVEN_the_deadline_day_itself_WHEN_the_day_passes_THEN_nothing_happens_yet()
      {
         WarDutyCase c = WarDutyLadder.Open("empire", "lucon", 3000, 100);

         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.DayPassed, Today = 120}).Should().Be(WarDutyEffect.None);
         c.Stage.Should().Be(WarDutyStage.Summoned);
      }

      // STAKE: the duty was service; a vassal who rides back to the war at any step owes nothing more.
      [Test]
      public void GIVEN_a_vassal_owing_the_levy_WHEN_he_serves_the_realm_THEN_the_reckoning_is_settled()
      {
         WarDutyCase c = WarDutyLadder.Open("empire", "lucon", 3000, 100);
         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.Appeared, Today = 105});

         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.Served, Today = 110}).Should().Be(WarDutyEffect.Settled);
         c.Stage.Should().Be(WarDutyStage.Settled);
      }

      // STAKE: Gabriel: coin, or troops, horses and arms to its value. Two hand-overs that together meet it settle it.
      [Test]
      public void GIVEN_the_levy_paid_partly_in_coin_and_partly_in_horses_WHEN_the_total_meets_it_THEN_it_is_settled()
      {
         WarDutyCase c = WarDutyLadder.Open("empire", "lucon", 3000, 100);
         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.Appeared, Today = 105});

         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.LevyPaid, Amount = 1800, Today = 106}).Should().Be(WarDutyEffect.None);
         c.LevyOutstanding.Should().Be(1200);
         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.LevyPaid, Amount = 1200, Today = 107}).Should().Be(WarDutyEffect.Settled);
      }

      // STAKE: Gabriel: "Si au final le joueur ne participe pas et ne fournit rien, il pourrait etre confisque d'un fief."
      [Test]
      public void GIVEN_nothing_served_or_paid_by_the_last_deadline_WHEN_the_player_holds_a_fief_THEN_a_fief_is_taken()
      {
         WarDutyCase c = WarDutyLadder.Open("empire", "lucon", 3000, 100);
         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.Appeared, Today = 105});

         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.DayPassed, Today = 126, HoldsATakeableFief = true})
            .Should().Be(WarDutyEffect.Confiscate);
         c.Stage.Should().Be(WarDutyStage.Confiscated);
      }

      // STAKE: a landless vassal has no fief to lose; the ruler remembers the default instead of the game inventing a loss.
      [Test]
      public void GIVEN_nothing_served_or_paid_WHEN_the_player_holds_no_fief_THEN_the_ruler_remembers_the_default()
      {
         WarDutyCase c = WarDutyLadder.Open("empire", "lucon", 3000, 100);
         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.Appeared, Today = 105});

         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.DayPassed, Today = 126, HoldsATakeableFief = false})
            .Should().Be(WarDutyEffect.RememberDefault);
      }

      // STAKE: a levy for a war that ended, or from a realm he has left, has nothing left to support.
      [Test]
      public void GIVEN_peace_before_the_levy_is_paid_WHEN_the_duty_ends_THEN_the_reckoning_is_dropped_without_a_loss()
      {
         WarDutyCase c = WarDutyLadder.Open("empire", "lucon", 3000, 100);

         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.DutyEnded, Today = 110}).Should().Be(WarDutyEffect.Drop);
         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.DayPassed, Today = 200, HoldsATakeableFief = true})
            .Should().Be(WarDutyEffect.None);
      }

      // STAKE: a settled reckoning stays settled; a later day must never take a fief for a debt already paid.
      [Test]
      public void GIVEN_a_settled_reckoning_WHEN_its_old_deadline_passes_THEN_nothing_is_taken()
      {
         WarDutyCase c = WarDutyLadder.Open("empire", "lucon", 3000, 100);
         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.Served, Today = 101});

         WarDutyLadder.Advance(c, new WarDutyInput {Event = WarDutyEvent.DayPassed, Today = 300, HoldsATakeableFief = true})
            .Should().Be(WarDutyEffect.None);
      }

      // STAKE: the levy grows with the house: a great house with fiefs owes more than a minor clan.
      [Test]
      public void GIVEN_a_greater_house_with_fiefs_WHEN_the_levy_is_set_THEN_it_asks_more_than_of_a_minor_clan()
      {
         int minor = WarLevyPolicy.Amount(new WarLevyFacts {ClanTier = 1, FiefCount = 0});
         int great = WarLevyPolicy.Amount(new WarLevyFacts {ClanTier = 4, FiefCount = 2});

         great.Should().BeGreaterThan(minor);
      }

      // STAKE: a levy above one take_gold transfer could never be settled in one hand-over (BastardBlackmailPolicy's lesson).
      [Test]
      public void GIVEN_a_vast_house_and_a_high_scale_WHEN_the_levy_is_set_THEN_it_never_exceeds_one_transfer()
      {
         WarLevyPolicy.Amount(new WarLevyFacts {ClanTier = 6, FiefCount = 12, Scale = 5})
            .Should().Be(WarLevyPolicy.MaxLevy);
      }

      // STAKE: gating before declaration. The summons names the war, the days to come, and only the next step the game
      // will really take if he does not come: the levy, with its sum.
      [Test]
      public void GIVEN_a_summons_WHEN_its_context_is_worded_THEN_it_names_the_war_the_days_and_the_levy_that_follows()
      {
         string context = WarDutyWording.SummonsContext(new SummonsFacts {
            EnemyNames = new[] {"Battania", "the Aserai"}, DaysAtWar = 34, DaysToCome = 20, LevyAmount = 3500
         });

         context.Should().Contain("at war with Battania and the Aserai for 34 days");
         context.Should().Contain("within 20 days");
         context.Should().Contain("a levy of 3500 denars");
      }

      // STAKE: Gabriel's last rung is a fief; a landless vassal has none, so the letter must not threaten one.
      [Test]
      public void GIVEN_a_landless_vassal_WHEN_the_levy_letter_is_worded_THEN_no_fief_is_threatened()
      {
         string context = WarDutyWording.LevyLetterContext(new LevyFacts {Outstanding = 2000, DaysLeft = 20, HoldsATakeableFief = false});

         context.Should().NotContain("fief");
         context.Should().Contain("you will remember it");
      }

      // STAKE: a vassal who holds a fief is told plainly what he stands to lose, and that goods or riding to war serve too.
      // Soldiers only on a condition: in person the game takes them only into the liege's own host with room (gating
      // before declaration), so the letter must not promise them outright.
      [Test]
      public void GIVEN_a_vassal_with_a_fief_WHEN_the_levy_letter_is_worded_THEN_the_fief_goods_and_service_are_named()
      {
         string context = WarDutyWording.LevyLetterContext(new LevyFacts {Outstanding = 2000, DaysLeft = 20, HoldsATakeableFief = true});

         context.Should().Contain("a levy of 2000 denars");
         context.Should().Contain("horses or weapons to that value");
         context.Should().Contain("soldiers too if your own host has room for them");
         context.Should().Contain("riding to war against your enemies before then settles it");
         context.Should().Contain("you will take one of their fiefs");
      }

      // STAKE: the punishment stings without stripping the house of its seat: a castle goes before a town.
      [Test]
      public void GIVEN_a_town_and_a_castle_WHEN_a_fief_is_confiscated_THEN_the_castle_is_taken()
      {
         WarDutyConfiscation.Choose(new[] {
            new FiefCandidate {Id = "town_seat", IsTown = true, Prosperity = 1000},
            new FiefCandidate {Id = "castle_hill", IsTown = false, Prosperity = 3000}
         }).Id.Should().Be("castle_hill");
      }

      // STAKE: of two castles, the poorer goes; the vassal keeps the better of what he holds.
      [Test]
      public void GIVEN_two_castles_WHEN_a_fief_is_confiscated_THEN_the_poorer_is_taken()
      {
         WarDutyConfiscation.Choose(new[] {
            new FiefCandidate {Id = "castle_rich", Prosperity = 900},
            new FiefCandidate {Id = "castle_poor", Prosperity = 200}
         }).Id.Should().Be("castle_poor");
      }

      // STAKE: a fief under siege is never taken from the vassal holding it (and the engine would not hand it over).
      [Test]
      public void GIVEN_his_only_fief_under_siege_WHEN_judged_THEN_nothing_can_be_taken()
      {
         WarDutyConfiscation.HasTakeable(new[] {new FiefCandidate {Id = "castle_besieged", UnderSiege = true}}).Should().BeFalse();
      }

      // STAKE: a broken or zero MCM scale must not make the levy free.
      [Test]
      public void GIVEN_a_scale_of_zero_WHEN_the_levy_is_set_THEN_the_levy_as_designed_is_asked()
      {
         WarLevyPolicy.Amount(new WarLevyFacts {ClanTier = 2, FiefCount = 1, Scale = 0})
            .Should().Be(WarLevyPolicy.Amount(new WarLevyFacts {ClanTier = 2, FiefCount = 1}));
      }
   }
}
