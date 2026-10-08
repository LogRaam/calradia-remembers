// Code written by Gabriel Mailhot, 08/10/2026.
// How a lord taken captive by the player is framed to those who hear of it. Fakade (Nexus, 08/10/2026): "it always
// seems wary or outright offended that the player would dare to do this ... The same NPCs that seem upset that the
// player takes prisoners happily do so themselves ... I find it somewhat strange that I have to justify my actions
// regarding a standardized game rule." No rule of CR called a capture an offence: the event carried no stake and no
// framing, and the model filled the gap with outrage. Gabriel (08/10/2026): keep every consequence of taking a lord,
// but frame it. A capture is an ordinary act of war; what offends is how the captive is kept; kin and realm still feel it.
// If this breaks, every lord in Calradia scolds the player for what they do themselves, or kin shrug at a son in chains.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptureNormTests
   {
      // Fakade's case, a lord of another realm: an ordinary act of war, and the treatment is what matters.
      [Test]
      public void GIVEN_a_lord_with_no_stake_in_the_captive_WHEN_told_THEN_it_is_an_ordinary_act_of_war()
      {
         string line = CaptureNorm.Line(CaptureStake.None);

         line.Should().Contain("ordinary act of war").And.Contain("how a captive is kept");
         line.Should().NotContain("your own kin");
      }

      // Kin still feel it: their own blood is in chains, and they want him back.
      [Test]
      public void GIVEN_the_captives_kin_WHEN_told_THEN_they_feel_it_as_their_own()
      {
         CaptureNorm.Line(CaptureStake.Kin).Should().Contain("your own kin");
      }

      // The captive's realm takes it as a loss in war, not an insult.
      [Test]
      public void GIVEN_a_lord_of_the_captives_realm_WHEN_told_THEN_it_is_a_loss_in_war()
      {
         CaptureNorm.Line(CaptureStake.Realm).Should().Contain("a loss for your side");
      }

      // The strongest stake wins: kin over realm over none.
      [Test]
      public void GIVEN_several_stakes_WHEN_judged_THEN_the_closest_one_speaks()
      {
         CaptureNorm.StakeOf(kin: true, realm: true).Should().Be(CaptureStake.Kin);
         CaptureNorm.StakeOf(kin: false, realm: true).Should().Be(CaptureStake.Realm);
         CaptureNorm.StakeOf(kin: false, realm: false).Should().Be(CaptureStake.None);
      }
   }
}
