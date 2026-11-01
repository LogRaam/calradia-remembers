// Code written by Gabriel Mailhot, 01/11/2026.
// RaidRestraintPolicy decides how likely a lord at war with the player is to spare the player's villages, and the ledger keeps
// what he drew. If it is wrong, DepaHN's complaint stands ("a close friend was the first to burn my villages"), a stranger holds
// back for no reason, or a friend draws every day until the villages are safe, and the chance Gabriel asked for is no chance.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Raids;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class RaidRestraintPolicyTests
   {
      private static int Chance(int regard, bool kin = false, int grudge = 0)
         => RaidRestraintPolicy.ChanceToSpare(new RaidRestraintFacts {Regard = regard, IsKin = kin, HeaviestGrudge = grudge});

      [Test]
      public void GIVEN_a_stronger_friendship_WHEN_the_war_starts_THEN_the_chance_of_being_spared_grows()
      {
         // STAKE: Gabriel: "Plus l'amitie est grande et moins il y a de chances qu'il attaque les villages."
         Chance(RaidRestraintPolicy.FriendFloor).Should().Be(RaidRestraintPolicy.ChanceOfANewFriend);
         Chance(50).Should().BeGreaterThan(Chance(30));
         Chance(100).Should().BeGreaterThan(Chance(50));
      }

      [Test]
      public void GIVEN_the_closest_friend_WHEN_the_war_starts_THEN_he_still_may_raid()
      {
         // STAKE: Gabriel: "un ami PEUT ne pas toucher le village": a certainty would be the lock he ruled out.
         Chance(100).Should().Be(RaidRestraintPolicy.ChanceOfTheClosestFriend);
         Chance(100).Should().BeLessThan(100);
      }

      [Test]
      public void GIVEN_a_lord_who_is_no_friend_WHEN_the_war_starts_THEN_nothing_holds_him_back()
      {
         // STAKE: only friends and kin hold back; every other lord fights this war as the base game plays it.
         Chance(RaidRestraintPolicy.FriendFloor - 1).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_relative_on_ordinary_terms_WHEN_the_war_starts_THEN_kinship_alone_gives_a_chance()
      {
         // STAKE: DepaHN: "I've had a son and a son-in-law fight me despite very strong relations."
         Chance(0, true).Should().Be(RaidRestraintPolicy.ChanceOfKin);
      }

      [Test]
      public void GIVEN_a_relative_who_is_also_a_close_friend_WHEN_the_war_starts_THEN_the_better_chance_counts()
      {
         // STAKE: being family must not make a close friend less likely to hold back.
         Chance(100, true).Should().Be(RaidRestraintPolicy.ChanceOfTheClosestFriend);
      }

      [Test]
      public void GIVEN_a_relative_who_has_turned_cold_WHEN_the_war_starts_THEN_blood_is_no_shield()
      {
         // STAKE: DepaHN: they act against him "when there is open hostility"; the same bar the calls for help keep for kin.
         Chance(RaidRestraintPolicy.KinFloor - 1, true).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_vendetta_WHEN_the_war_starts_THEN_neither_friendship_nor_blood_holds_him_back()
      {
         // STAKE: DepaHN's "clear reason": a lord with a blood grudge has one, whatever his old regard says.
         Chance(100, true, CaptorReleasePolicy.VendettaGrudgeFloor).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_restraint_WHEN_a_vendetta_comes_THEN_it_breaks_and_a_lesser_grudge_does_not()
      {
         // STAKE: Gabriel's go: the restraint gives way only to a named reason, not to any passing quarrel.
         RaidRestraintPolicy.GrudgeBreaksRestraint(CaptorReleasePolicy.VendettaGrudgeFloor).Should().BeTrue();
         RaidRestraintPolicy.GrudgeBreaksRestraint(CaptorReleasePolicy.VendettaGrudgeFloor - 1).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_chance_WHEN_the_draw_is_made_THEN_it_lands_inside_or_out()
      {
         // STAKE: an edge off by one would make a 95% friend certain, or a 0% lord able to hold back.
         RaidRestraintPolicy.Spares(25, 24).Should().BeTrue();
         RaidRestraintPolicy.Spares(25, 25).Should().BeFalse();
         RaidRestraintPolicy.Spares(0, 0).Should().BeFalse();
         RaidRestraintPolicy.Spares(95, 99).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_failed_draw_WHEN_the_next_day_comes_THEN_the_lord_has_drawn_and_does_not_draw_again()
      {
         // STAKE: DepaHN: "decide each lord's stance once"; daily draws would turn every friend into a sure thing within a week.
         var ledger = new RaidRestraintLedger();
         ledger.Add("lord", "realm", false, 10);

         ledger.HasDrawn("lord", "realm").Should().BeTrue();
         ledger.Draws[0].IsSparing.Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_lord_who_changes_realm_WHEN_he_fights_on_THEN_it_is_a_new_draw()
      {
         // STAKE: the draw belongs to one war; a lord who defects to another enemy realm stands anew.
         var ledger = new RaidRestraintLedger();
         ledger.Add("lord", "realm", true, 10);

         ledger.HasDrawn("lord", "other realm").Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_restraint_WHEN_it_is_broken_THEN_it_no_longer_spares_and_says_why()
      {
         // STAKE: a broken restraint must end at once, and stay in the ledger so he does not draw again in this war.
         var ledger = new RaidRestraintLedger();
         RaidRestraint draw = ledger.Add("lord", "realm", true, 10);
         draw.IsSparing.Should().BeTrue();

         draw.BrokenBecause = "the player raided his lands";

         draw.IsSparing.Should().BeFalse();
         ledger.HasDrawn("lord", "realm").Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_ledger_WHEN_saved_and_read_THEN_what_each_lord_drew_stands()
      {
         // STAKE: a reload must not give every friend a new draw, or the player could reload until their villages are safe.
         var ledger = new RaidRestraintLedger();
         ledger.Add("lord", "realm", true, 10);
         ledger.Add("other", "realm", false, 10).BrokenBecause = null;
         ledger.Add("third", "realm", true, 10).BrokenBecause = "a vendetta";

         RaidRestraintLedger read = RaidRestraintLedger.Deserialize(ledger.Serialize());

         read.Draws.Should().HaveCount(3);
         read.Draws[0].IsSparing.Should().BeTrue();
         read.Draws[1].IsSparing.Should().BeFalse();
         read.Draws[2].IsSparing.Should().BeFalse();
         read.Draws[2].BrokenBecause.Should().Be("a vendetta");
      }

      [TestCase(null)]
      [TestCase("")]
      [TestCase("{broken")]
      public void GIVEN_an_old_or_broken_save_WHEN_read_THEN_nobody_has_drawn(string json)
      {
         // STAKE: the save-migration rule: a save from before this feature never bricks a campaign.
         RaidRestraintLedger.Deserialize(json).Draws.Should().BeEmpty();
      }
   }
}
