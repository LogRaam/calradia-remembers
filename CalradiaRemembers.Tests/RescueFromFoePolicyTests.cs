// Code written by Gabriel Mailhot, 10/10/2026.
// zhenja1 (Nexus, 10/10/2026): "I liberated two from Garios, and both are saying they were never prisoner." A lord the
// player takes from an enemy who held them, and lets go, was rescued (Gabriel), and remembers who held them. If these
// rules are wrong, the freed lords deny their captivity, a prisoner of the player's own is thanked for a pardon, or the
// memory blames a realm and never names the lord who held them.

#region

using CalradiaRemembers.Logic.Rescue;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RescueFromFoePolicyTests
   {
      // zhenja1's case: taken from Garios in battle, let go a few days later.
      [Test]
      public void GIVEN_a_lord_taken_from_a_beaten_foe_WHEN_the_player_lets_them_go_THEN_it_is_a_rescue()
      {
         RescueFromFoePolicy.IsRescue(new CustodyReleaseFacts {ReleasedByChoice = true, TakenFromFoeOnDay = 100, Today = 104}).Should().BeTrue();
      }

      // A lord the player captured themselves and later lets go is pardoned, not rescued: nothing to thank them for.
      [Test]
      public void GIVEN_a_prisoner_the_player_captured_themselves_WHEN_let_go_THEN_it_is_no_rescue()
      {
         RescueFromFoePolicy.IsRescue(new CustodyReleaseFacts {ReleasedByChoice = true, TakenFromFoeOnDay = null, Today = 104}).Should().BeFalse();
      }

      // Kept a season and more in the player's own dungeon, the release reads as the player's own pardon again.
      [Test]
      public void GIVEN_a_lord_kept_past_the_window_WHEN_let_go_THEN_it_is_no_longer_a_rescue()
      {
         RescueFromFoePolicy.IsRescue(new CustodyReleaseFacts {ReleasedByChoice = true, TakenFromFoeOnDay = 100, Today = 100 + RescueFromFoePolicy.WindowDays + 1}).Should().BeFalse();
         RescueFromFoePolicy.IsRescue(new CustodyReleaseFacts {ReleasedByChoice = false, TakenFromFoeOnDay = 100, Today = 101}).Should().BeFalse("ransomed or escaped is no rescue by the player");
      }

      // The memory names who held them: "the hands of the Western Empire" said nothing of Garios.
      [Test]
      public void GIVEN_the_lord_who_held_them_WHEN_the_rescue_is_remembered_THEN_that_lord_is_named()
      {
         RescueFromFoePolicy.CaptorPhrase(new RescueCaptorFacts {CaptorLabel = "Garios of the Western Empire", FactionName = "Western Empire"})
                            .Should().Be("Garios of the Western Empire, who held me prisoner");
         RescueFromFoePolicy.CaptorPhrase(new RescueCaptorFacts {CaptorLabel = "Garios", SettlementName = "Lycaron"})
                            .Should().Be("the dungeons of Lycaron, where Garios held me");
      }

      // Without a lord to name, the place, then the realm, then plain captivity, as before.
      [Test]
      public void GIVEN_no_lord_to_name_WHEN_the_rescue_is_remembered_THEN_the_place_or_realm_stands_in()
      {
         RescueFromFoePolicy.CaptorPhrase(new RescueCaptorFacts {SettlementName = "Lycaron", FactionName = "Western Empire"}).Should().Be("the dungeons of Lycaron");
         RescueFromFoePolicy.CaptorPhrase(new RescueCaptorFacts {FactionName = "Western Empire"}).Should().Be("the hands of Western Empire");
         RescueFromFoePolicy.CaptorPhrase(null).Should().Be("captivity");
      }
   }
}
