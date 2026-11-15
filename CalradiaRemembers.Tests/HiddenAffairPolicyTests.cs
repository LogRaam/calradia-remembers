// Code written by Gabriel Mailhot, 01/11/2026.
// HiddenAffairPolicy decides who may take a hidden lover, how many stand at once, when one ends and what a companion's inquiry
// finds (Gabriel's "go", 01/11/2026: rarely, a handful in the realm, found by a companion sent to look). If it is wrong, the
// player's own household gains lovers it never chose, every lord cheats and a secret is worth nothing, or a companion reports
// something untrue about a lord, which the game never does.

#region

using CalradiaRemembers.Logic.Companions;
using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class HiddenAffairPolicyTests
   {
      private static HiddenAffairCandidate Lord(int honor = 0, int feeling = 10, bool married = true, bool ownHouse = false)
         => new() {IsMarriedAdult = married, Honor = honor, FeelingForSpouse = feeling, IsOfPlayerHousehold = ownHouse};

      private static readonly HiddenAffairNames Names = new() {Subject = "Lucon", Lover = "Ira", Spouse = "Zerosica"};

      [Test]
      public void GIVEN_an_unmarried_lord_WHEN_asked_THEN_there_is_no_one_to_betray()
      {
         // STAKE: an affair needs a betrayed spouse: without one there is no secret for the player to sell or tell.
         HiddenAffairPolicy.MayTake(Lord(married: false)).Should().BeFalse();
      }

      [Test]
      public void GIVEN_the_players_own_people_WHEN_asked_THEN_none_of_them_is_touched()
      {
         // STAKE: the player's clan and spouse have their own paths: a lover they never chose would rewrite their own household behind them.
         HiddenAffairPolicy.MayTake(Lord(ownHouse: true)).Should().BeFalse();
         HiddenAffairPolicy.MayTake(new HiddenAffairCandidate {IsMarriedAdult = true, IsThePlayersOwn = true}).Should().BeFalse();
         HiddenAffairPolicy.MayTake(new HiddenAffairCandidate {IsMarriedAdult = true, IsPlayersLover = true}).Should().BeFalse();
      }

      [Test]
      public void GIVEN_someone_already_in_an_affair_WHEN_asked_THEN_they_take_no_second()
      {
         // STAKE: TakenLoverId holds one name: a second affair would silently overwrite the first and its ledger line.
         HiddenAffairPolicy.MayTake(new HiddenAffairCandidate {IsMarriedAdult = true, HasLover = true}).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_lord_of_high_honor_WHEN_the_chance_is_read_THEN_it_is_zero()
      {
         // STAKE: Gabriel's "selon son caractere": the most honourable lord being found out would read as the game not knowing them.
         HiddenAffairPolicy.ChancePercent(Lord(HiddenAffairPolicy.FaithfulHonor)).Should().Be(0);
      }

      [Test]
      public void GIVEN_nature_and_marriage_WHEN_the_chance_is_read_THEN_both_move_it()
      {
         // STAKE: Gabriel's "selon son caractere et son mariage": a cold marriage and a dishonourable nature must be where the secrets are.
         int steady = HiddenAffairPolicy.ChancePercent(Lord());
         HiddenAffairPolicy.ChancePercent(Lord(1)).Should().BeLessThan(steady);
         HiddenAffairPolicy.ChancePercent(Lord(-1)).Should().BeGreaterThan(steady);
         HiddenAffairPolicy.ChancePercent(Lord(feeling: -5)).Should().BeGreaterThan(steady);
      }

      [Test]
      public void GIVEN_the_handful_standing_WHEN_a_roll_lands_THEN_no_new_affair_begins()
      {
         // STAKE: Gabriel: a handful in the realm at once. Past it, a world where everyone cheats and a secret is worth nothing.
         HiddenAffairPolicy.Begins(new AffairDraw {Standing = HiddenAffairPolicy.MaxAtOnce, ChancePercent = 100, Roll = 0}).Should().BeFalse();
         HiddenAffairPolicy.Begins(new AffairDraw {Standing = HiddenAffairPolicy.MaxAtOnce - 1, ChancePercent = 100, Roll = 0}).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_roll_at_the_chance_WHEN_read_THEN_it_misses()
      {
         // STAKE: a chance of 0 must never begin an affair, so the honourable lord stays out of it on the highest and lowest roll alike.
         HiddenAffairPolicy.Begins(new AffairDraw {Standing = 0, ChancePercent = 0, Roll = 0}).Should().BeFalse();
         HiddenAffairPolicy.Begins(new AffairDraw {Standing = 0, ChancePercent = 6, Roll = 6}).Should().BeFalse();
         HiddenAffairPolicy.Begins(new AffairDraw {Standing = 0, ChancePercent = 6, Roll = 5}).Should().BeTrue();
      }

      [TestCase(false, true, true, 10)]
      [TestCase(true, false, true, 10)]
      [TestCase(true, true, false, 10)]
      [TestCase(true, true, true, HiddenAffairPolicy.LongestDays)]
      public void GIVEN_a_change_in_their_lives_WHEN_reviewed_THEN_the_affair_is_over(bool inWorld, bool married, bool free, int age)
      {
         // STAKE: an affair with the dead or a widow would go on selling a secret nobody can be hurt by, and the handful would never turn over.
         HiddenAffairPolicy.IsOver(new HiddenAffairSpan {BothInTheWorld = inWorld, StillMarried = married, LoverStillFree = free, AgeDays = age}).Should().BeTrue();
      }

      [Test]
      public void GIVEN_nothing_changed_WHEN_reviewed_THEN_it_stands()
      {
         // STAKE: an affair ending by itself would take away a hold the player paid a companion's risk to find.
         HiddenAffairPolicy.IsOver(new HiddenAffairSpan {BothInTheWorld = true, StillMarried = true, LoverStillFree = true, AgeDays = 10}).Should().BeFalse();
      }

      [Test]
      public void GIVEN_an_errand_that_went_well_WHEN_there_is_an_affair_THEN_it_is_found()
      {
         // STAKE: the one road to the secret Gabriel chose: if it never finds anything, the player has no lever on anyone.
         HiddenAffairPolicy.Inquire(MissionResult.Success, true).Should().Be(AffairInquiry.Found);
      }

      [TestCase(MissionResult.Partial)]
      [TestCase(MissionResult.Failure)]
      public void GIVEN_an_errand_that_went_badly_WHEN_there_is_an_affair_THEN_nothing_certain_is_said(MissionResult result)
      {
         // STAKE: NPCs never lie: a poor errand must not report "nothing hidden" about someone who is hiding something.
         HiddenAffairPolicy.Inquire(result, true).Should().Be(AffairInquiry.NothingCertain);
      }

      [Test]
      public void GIVEN_an_errand_that_went_well_WHEN_there_is_no_affair_THEN_it_says_so_and_invents_nothing()
      {
         // STAKE: a false accusation must stay impossible: a companion never brings back an affair the game does not hold.
         HiddenAffairPolicy.Inquire(MissionResult.Success, false).Should().Be(AffairInquiry.NothingToFind);
         HiddenAffairPolicy.ReportLine(AffairInquiry.NothingToFind, "Lucon", new HiddenAffairNames()).Should().Contain("Lucon").And.Contain("nothing");
      }

      [Test]
      public void GIVEN_an_affair_found_WHEN_the_report_line_is_read_THEN_it_names_all_three()
      {
         // STAKE: the line is what the companion voices: a report without the lover or the spouse tells the player nothing they can use.
         HiddenAffairPolicy.ReportLine(AffairInquiry.Found, "Lucon", Names).Should().Contain("Lucon").And.Contain("Ira").And.Contain("Zerosica");
      }

      [Test]
      public void GIVEN_one_of_the_lovers_WHEN_their_prompt_is_read_THEN_they_may_evade_and_may_not_lie()
      {
         // STAKE: Gabriel's rule: NPCs never lie. The character knows, keeps it, and refuses or turns aside instead of denying it.
         string block = HiddenAffairPolicy.PromptBlock("Ira", "Zerosica");
         block.Should().Contain("Ira").And.Contain("Zerosica").And.Contain("Never raise it").And.Contain("never deny it with a lie");
      }

      [Test]
      public void GIVEN_the_ledger_WHEN_asked_of_either_lover_THEN_it_finds_the_same_affair()
      {
         // STAKE: the lover has no field of their own: without this they would forget the affair, and could never be looked into.
         var ledger = new HiddenAffairLedger();
         ledger.Affairs.Add(new HiddenAffair {SubjectId = "lucon", LoverId = "ira", Day = 3});

         ledger.Of("lucon").Should().BeSameAs(ledger.Of("ira"));
         ledger.Of("nobody").Should().BeNull();
      }

      [Test]
      public void GIVEN_a_saved_ledger_WHEN_read_back_THEN_the_affairs_survive()
      {
         // STAKE: losing the ledger on load would leave a lord's TakenLoverId outside it, and the partner path would call it a betrayal of the player.
         var ledger = new HiddenAffairLedger();
         ledger.Affairs.Add(new HiddenAffair {SubjectId = "lucon", LoverId = "ira", Day = 3});

         HiddenAffairLedger.Deserialize(ledger.Serialize()).Affairs.Should().ContainSingle(a => a.LoverId == "ira" && a.Day == 3);
      }

      [TestCase(null)]
      [TestCase("")]
      [TestCase("not json")]
      public void GIVEN_an_old_or_broken_save_WHEN_read_THEN_the_ledger_is_empty(string json)
      {
         // STAKE: a save written before this feature has no key: it must load, not throw.
         HiddenAffairLedger.Deserialize(json).Affairs.Should().BeEmpty();
      }
   }
}
