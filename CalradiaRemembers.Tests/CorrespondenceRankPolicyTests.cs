// Code written by Gabriel Mailhot, 28/09/2026.
// Who deals with the player in person. Gabriel (28/09/2026), landless at the start of a campaign: the Empress Rhagaea
// wrote to him in her own hand, and had ridden out herself to find him. His ruling: a lord writes in person to equals and
// superiors, never down, except for love; a greater lord has the letter written under a title with no name; and the same
// rule keeps her from riding out in person. If this is wrong one way, the great of Calradia keep writing to a nobody; the
// other way, a lover's letter or a friend's arrives from a clerk.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CorrespondenceRankPolicyTests
   {
      private static CorrespondenceRankFacts Empress(int playerTier = 0, LetterReason? reason = LetterReason.QuestUpdate)
         => new CorrespondenceRankFacts {SenderIsRuler = true, SenderClanTier = 6, PlayerClanTier = playerTier, Reason = reason};

      // Gabriel's case: the Empress, and a landless newcomer.
      [Test]
      public void GIVEN_a_ruler_and_a_landless_newcomer_WHEN_she_writes_THEN_it_is_not_in_her_own_hand()
      {
         CorrespondenceRankPolicy.DealsInPerson(Empress()).Should().BeFalse();
      }

      // Rising in the world shows: a great vassal is written to by the ruler herself, and a fellow ruler always is.
      [Test]
      public void GIVEN_a_player_risen_to_a_great_house_or_a_throne_WHEN_a_ruler_writes_THEN_she_writes_herself()
      {
         CorrespondenceRankPolicy.DealsInPerson(Empress(CorrespondenceRankPolicy.TierARulerAddressesInPerson)).Should().BeTrue();
         CorrespondenceRankPolicy.DealsInPerson(new CorrespondenceRankFacts {SenderIsRuler = true, SenderClanTier = 6, PlayerIsRuler = true}).Should().BeTrue();
      }

      // Gabriel: "sauf pour les lettres d'amours". Love, marriage, family and what no clerk should read stay personal.
      [TestCase(LetterReason.RomanticCorrespondence)]
      [TestCase(LetterReason.SpouseCorrespondence)]
      [TestCase(LetterReason.MarriageProposal)]
      [TestCase(LetterReason.Blackmail)]
      [TestCase(LetterReason.CorruptionAttempt)]
      public void GIVEN_a_private_matter_WHEN_a_ruler_writes_to_a_newcomer_THEN_she_writes_it_herself(LetterReason reason)
      {
         CorrespondenceRankPolicy.DealsInPerson(Empress(reason: reason)).Should().BeTrue();
      }

      // A romance or the family is personal whatever the letter is about.
      [Test]
      public void GIVEN_a_personal_bond_WHEN_a_ruler_writes_on_any_matter_THEN_she_writes_herself()
      {
         CorrespondenceRankFacts f = Empress();
         f.PersonalBond = true;

         CorrespondenceRankPolicy.DealsInPerson(f).Should().BeTrue();
      }

      // Friendship outranks station: from "deep affection" a character deals in person whatever the rank.
      [Test]
      public void GIVEN_a_ruler_who_holds_the_player_dear_WHEN_she_writes_THEN_she_writes_herself()
      {
         CorrespondenceRankFacts f = Empress();
         f.Regard = Regard.ConfidingTier;

         CorrespondenceRankPolicy.DealsInPerson(f).Should().BeTrue();
         f.Regard = Regard.ConfidingTier - 1;
         CorrespondenceRankPolicy.DealsInPerson(f).Should().BeFalse();
      }

      // Among lords, a house two tiers above the player's writes through its steward; a nearer one writes itself.
      [TestCase(5, 3, false)]
      [TestCase(4, 3, true)]
      [TestCase(2, 4, true)]
      public void GIVEN_two_houses_WHEN_the_greater_writes_THEN_a_gap_of_two_tiers_brings_a_steward(int senderTier, int playerTier, bool inPerson)
      {
         CorrespondenceRankPolicy.DealsInPerson(new CorrespondenceRankFacts {SenderClanTier = senderTier, PlayerClanTier = playerTier}).Should().Be(inPerson);
      }

      // The same rule on the road: a meeting has no letter reason, and the Empress does not ride out after a newcomer.
      [Test]
      public void GIVEN_a_meeting_on_the_road_WHEN_a_ruler_would_ride_out_to_a_newcomer_THEN_she_does_not()
      {
         CorrespondenceRankPolicy.DealsInPerson(Empress(reason: null)).Should().BeFalse();
      }

      // Gabriel: a title with no name, never an invented clerk.
      [Test]
      public void GIVEN_a_letter_written_on_a_lords_behalf_WHEN_signed_THEN_it_bears_a_title_and_no_invented_name()
      {
         CorrespondenceRankPolicy.ScribeTitle("Rhagaea", senderIsRuler: true, senderIsFemale: true).Should().Be("the chancellery of Her Majesty Rhagaea");
         CorrespondenceRankPolicy.ScribeTitle("Lucon", senderIsRuler: false, senderIsFemale: false).Should().Be("the steward of Lord Lucon");
      }
   }
}
