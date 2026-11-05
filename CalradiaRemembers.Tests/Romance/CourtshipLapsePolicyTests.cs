// Code written by Gabriel Mailhot, 04/11/2026.
// CourtshipLapsePolicy decides what becomes of a character's bond with the player when they wed someone else.
// avalanche24 (Nexus, 04/11/2026): Rhagaea was wed by the game while courted by the player, her Heart page still said
// "courting" and she denied the courtship in chat. Gabriel (04/11/2026): it falls to Estranged. If this is wrong, a
// married queen is still listed as courting the player, or a secret lover is cut off by the marriage she hid them from.

#region

using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Romance
{
   [TestFixture]
   public sealed class CourtshipLapsePolicyTests
   {
      // STAKE: avalanche24's Rhagaea: courted by the player, wed to another, the courtship is over, not still running.
      [TestCase(RomanticStatus.Courting)]
      [TestCase(RomanticStatus.Intimate)]
      [TestCase(RomanticStatus.Committed)]
      public void GIVEN_a_courtship_or_deeper_WHEN_the_character_weds_another_THEN_the_bond_falls_to_estranged(RomanticStatus status)
      {
         CourtshipLapsePolicy.After(status, false).Should().Be(RomanticStatus.Estranged);
      }

      // STAKE: wedding the player is the courtship's end the player wanted; it must not estrange them.
      [Test]
      public void GIVEN_a_courtship_WHEN_the_character_weds_the_player_THEN_the_bond_is_untouched()
      {
         CourtshipLapsePolicy.After(RomanticStatus.Courting, true).Should().Be(RomanticStatus.Courting);
      }

      // STAKE: a secret lover's bond already lives beside a marriage; a mere glance (Curious) has no courtship to end.
      [TestCase(RomanticStatus.SecretLover)]
      [TestCase(RomanticStatus.Curious)]
      [TestCase(RomanticStatus.None)]
      public void GIVEN_a_secret_lover_or_no_courtship_WHEN_the_character_weds_another_THEN_nothing_changes(RomanticStatus status)
      {
         CourtshipLapsePolicy.After(status, false).Should().Be(status);
      }

      // STAKE: avalanche24's own save: Rhagaea was already wed when this came; the courtship still running beside it ends.
      [Test]
      public void GIVEN_a_courtship_and_a_later_wedding_to_another_WHEN_reconciled_THEN_it_ends()
      {
         CourtshipLapsePolicy.EndsForAPastWedding(true, 300, 120).Should().BeTrue();
      }

      // STAKE: a married lady the player began courting after her wedding is the player's affair to pursue, not a lapse;
      // and a wedding CR never saw proves nothing.
      [TestCase(true, 100, 200)]
      [TestCase(true, null, 200)]
      [TestCase(false, 300, 120)]
      public void GIVEN_a_courtship_after_the_wedding_or_an_unseen_wedding_WHEN_reconciled_THEN_it_stays(bool wedToAnother, int? weddedOn, int? since)
      {
         CourtshipLapsePolicy.EndsForAPastWedding(wedToAnother, weddedOn, since).Should().BeFalse();
      }

      // STAKE: Gabriel's rule that NPCs never lie: she remembers choosing another and never denies the courtship was real.
      [Test]
      public void GIVEN_a_lapsed_courtship_WHEN_remembered_THEN_she_names_her_spouse_and_owns_what_was()
      {
         string memory = CourtshipLapsePolicy.Memory("Hoskuld");

         memory.Should().Contain("Hoskuld");
         memory.Should().Contain("never deny");
      }
   }
}
