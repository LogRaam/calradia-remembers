// Code written by Gabriel Mailhot, 08/10/2026.
// How a lord taken captive by the player is framed to those who hear of it. Fakade (Nexus, 08/10/2026): "it always
// seems wary or outright offended that the player would dare to do this ... The same NPCs that seem upset that the
// player takes prisoners happily do so themselves ... I find it somewhat strange that I have to justify my actions
// regarding a standardized game rule." No rule of CR called a capture an offence: the event carried no stake and no
// framing, and the model filled the gap with outrage. Gabriel (08/10/2026): keep every consequence of taking a lord,
// but frame it. A capture is an ordinary act of war; kin and realm still feel it.
// Fakade (Nexus, 10/11/2026): a lady left in a garrison dungeon, and her realm said she was "kept like chattel rather
// than a lady of consequence"; the garrison's own lord wrote that the player had "abandoned her". The 08/10 line named
// how a captive is kept as the true offence and gave no fact of the keeping, so the model invented the mistreatment.
// The line now tells where, whose hold and how long, and says nothing tells of ill treatment (NPCs never lie).
// If this breaks, lords scold the player for what they do themselves, accuse them of abuse that never happened, or
// kin shrug at a daughter in chains.

#region

using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptureNormTests
   {
      // STAKE: Fakade's first case, a lord of another realm scolding him for an ordinary capture.
      [Test]
      public void GIVEN_a_lord_with_no_stake_in_the_captive_WHEN_told_THEN_it_is_an_ordinary_act_of_war()
      {
         string line = CaptureNorm.Line(new CaptiveKeepingFacts {Stake = CaptureStake.None, StillHeld = true});

         line.Should().Contain("ordinary act of war");
         line.Should().NotContain("your own kin");
      }

      // STAKE: Fakade 10/11, "kept like chattel": the line itself must never plant the keeping as the offence.
      [Test]
      public void GIVEN_any_capture_WHEN_told_THEN_the_line_does_not_name_the_keeping_as_the_offence_and_forbids_inventing_abuse()
      {
         string line = CaptureNorm.Line(new CaptiveKeepingFacts {Stake = CaptureStake.Realm, StillHeld = true, HeldAt = "Pravend", DaysHeld = 4});

         line.Should().NotContain("What would truly offend");
         line.Should().Contain("Nothing you know tells of ill treatment");
         line.Should().Contain("never claim or hint");
         line.Should().Contain("no slight");
      }

      // STAKE: a captive in a garrison dungeon is told as held there, so the model has the fact instead of a guess.
      [Test]
      public void GIVEN_a_captive_in_a_settlement_dungeon_WHEN_told_THEN_the_place_and_the_days_are_named()
      {
         string line = CaptureNorm.Line(new CaptiveKeepingFacts {Stake = CaptureStake.None, StillHeld = true, HeldAt = "Pravend", DaysHeld = 6});

         line.Should().Contain("held in the dungeon of Pravend, for 6 days now");
      }

      // STAKE: Fakade 10/11, the garrison's own lord wrote she was "abandoned": he holds her himself, and must know it.
      [Test]
      public void GIVEN_the_captive_held_in_the_hearers_own_hold_WHEN_told_THEN_it_is_in_their_own_keeping()
      {
         string line = CaptureNorm.Line(new CaptiveKeepingFacts {
            Stake = CaptureStake.None, CaptiveIsFemale = true, StillHeld = true, HeldAt = "Pravend", HeldInHearersHold = true, DaysHeld = 3
         });

         line.Should().Contain("She is held in your own house's dungeon at Pravend").And.Contain("in your own keeping");
      }

      // STAKE: a lady was spoken of as "he" by the 08/10 line.
      [Test]
      public void GIVEN_a_female_captive_WHEN_her_kin_are_told_THEN_she_is_spoken_of_as_she()
      {
         string line = CaptureNorm.Line(new CaptiveKeepingFacts {Stake = CaptureStake.Kin, CaptiveIsFemale = true, StillHeld = true});

         line.Should().Contain("she is your own kin").And.Contain("want her back");
         line.Should().NotContain(" him ").And.NotContain(" he ");
      }

      // STAKE: a captive already freed must not be mourned as still in chains.
      [Test]
      public void GIVEN_a_captive_since_freed_WHEN_told_THEN_no_longer_a_captive()
      {
         CaptureNorm.Line(new CaptiveKeepingFacts {Stake = CaptureStake.Realm, StillHeld = false}).Should().Contain("no longer a captive");
      }

      // STAKE: kin still feel it: their own blood is in chains, and they want him back.
      [Test]
      public void GIVEN_the_captives_kin_WHEN_told_THEN_they_feel_it_as_their_own()
      {
         CaptureNorm.Line(new CaptiveKeepingFacts {Stake = CaptureStake.Kin, StillHeld = true}).Should().Contain("he is your own kin");
      }

      // STAKE: the captive's realm takes it as a loss in war, not an insult.
      [Test]
      public void GIVEN_a_lord_of_the_captives_realm_WHEN_told_THEN_it_is_a_loss_in_war()
      {
         CaptureNorm.Line(new CaptiveKeepingFacts {Stake = CaptureStake.Realm, StillHeld = true}).Should().Contain("a loss for your side");
      }

      // STAKE: the strongest stake wins: kin over realm over none.
      [Test]
      public void GIVEN_several_stakes_WHEN_judged_THEN_the_closest_one_speaks()
      {
         CaptureNorm.StakeOf(kin: true, realm: true).Should().Be(CaptureStake.Kin);
         CaptureNorm.StakeOf(kin: false, realm: true).Should().Be(CaptureStake.Realm);
         CaptureNorm.StakeOf(kin: false, realm: false).Should().Be(CaptureStake.None);
      }
   }
}
