// Code written by Gabriel Mailhot, 01/08/2026.
// The mailbox's last step between "an NPC has a reason to write" and "a letter is sent". Until 31/10/2026 it released one
// letter a day under a budget; the letter box removed the budget (Gabriel, 30/10/2026: "plus besoin de reduire la
// frequence ou le nombre de lettres, c'est le joueur qui classe, elimine, abonne et desabonne"; 31/10: step 4, "sans
// reglage pour le moment"), since a letter now costs nothing until opened. What it still decides: every ready letter goes,
// one per sender, the weightiest of that sender's. Get it wrong and letters are held back that the player was promised
// (the old complaint's opposite), a sender writes twice in a day, or the awaited correspondent is not listed first.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterMailboxPolicyTests
    {
        private const int Today = 100;

        private static LetterCandidate Candidate(string senderId, int weight, int eligibleSinceDay, LetterReason reason = LetterReason.StanceFavor)
        {
            return new LetterCandidate
            {
                Reason = reason,
                SenderId = senderId,
                Weight = weight,
                EligibleSinceDay = eligibleSinceDay
            };
        }

        // STAKE: the letter box's promise. With no budget, every correspondent who has a ready letter today sends it: two
        // lords congratulating a tournament win both write, rather than one winning the day's single slot.
        [Test]
        public void GIVEN_letters_ready_from_several_senders_WHEN_the_day_is_released_THEN_each_sender_sends_one()
        {
            var pool = new List<LetterCandidate> { Candidate("lord.a", 30, Today), Candidate("lord.b", 90, Today), Candidate("lady.c", 55, Today) };

            LetterMailboxPolicy.ReleasedToday(pool, Today).Select(c => c.SenderId).Should().BeEquivalentTo("lord.a", "lord.b", "lady.c");
        }

        // STAKE: one sender, one letter a day: two candidates from the same lord (his realm's news and a tournament win)
        // never arrive as two letters at once; the weightier goes, and his own wait between letters holds the other.
        [Test]
        public void GIVEN_two_ready_letters_from_one_sender_WHEN_released_THEN_only_the_weightier_goes()
        {
            var pool = new List<LetterCandidate> { Candidate("lord.a", 30, Today, LetterReason.TournamentVictory), Candidate("lord.a", 40, Today, LetterReason.RealmTidings) };

            LetterMailboxPolicy.ReleasedToday(pool, Today).Should().ContainSingle().Which.Reason.Should().Be(LetterReason.RealmTidings);
        }

        // STAKE: a letter that has waited still counts its patience when one sender has two ready.
        [Test]
        public void GIVEN_a_patient_letter_and_a_fresher_weightier_one_from_one_sender_WHEN_released_THEN_aging_lets_patience_win()
        {
            var pool = new List<LetterCandidate> { Candidate("lord.a", 65, Today, LetterReason.QuestUpdate), Candidate("lord.a", 50, Today - 20, LetterReason.StanceFavor) };

            LetterMailboxPolicy.ReleasedToday(pool, Today).Single().Reason.Should().Be(LetterReason.StanceFavor);
        }

        // STAKE: nothing ready, nothing sent, and no error: the daily tick calls this unconditionally.
        [Test]
        public void GIVEN_no_ready_letter_WHEN_released_THEN_nothing_is_sent()
        {
            LetterMailboxPolicy.ReleasedToday(new List<LetterCandidate>(), Today).Should().BeEmpty();
            LetterMailboxPolicy.ReleasedToday(null, Today).Should().BeEmpty();
        }

        // STAKE: Gabriel's love letters in wartime: the correspondent marked with the ribbon is listed first.
        [Test]
        public void GIVEN_an_awaited_correspondent_among_louder_letters_WHEN_released_THEN_the_awaited_letter_comes_first()
        {
            LetterCandidate beloved = Candidate("lady.awaited", 30, Today);
            beloved.Awaited = true;

            LetterMailboxPolicy.ReleasedToday(new List<LetterCandidate> { Candidate("lord.war", 90, Today - 5), beloved }, Today).First().Should().BeSameAs(beloved);
        }

        // STAKE: the same day and the same letters always give the same result, so a reloaded day sends the same mail.
        [Test]
        public void GIVEN_identical_inputs_WHEN_released_twice_THEN_the_same_letters_go_in_the_same_order()
        {
            var pool = new List<LetterCandidate> { Candidate("lord.b", 50, Today), Candidate("lord.a", 50, Today), Candidate("lord.a", 50, Today - 1, LetterReason.QuestUpdate) };

            LetterMailboxPolicy.ReleasedToday(pool, Today).Should().Equal(LetterMailboxPolicy.ReleasedToday(pool, Today));
        }

        // These reasons are mechanical or solicited (a divorce deadline, a child's upkeep, a companion's final goodbye, a
        // reply to the player's own letter): they never cool a correspondent's interest when acknowledged or thrown
        // (LetterBox.Throw, the letter window's Acknowledge). A flavor reason like BattleVictory must NOT be in this set.
        [TestCase(LetterReason.SpouseDivorceDemand)]
        [TestCase(LetterReason.SpouseDivorceEscalation)]
        [TestCase(LetterReason.ChildSupportRequest)]
        [TestCase(LetterReason.CompanionFarewell)]
        [TestCase(LetterReason.PlayerLetterReply)]
        public void GIVEN_a_guaranteed_reason_WHEN_asked_THEN_it_bypasses_the_budget(LetterReason reason)
        {
            LetterMailboxPolicy.BypassesBudget(reason).Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_ordinary_flavor_reason_WHEN_asked_THEN_it_does_NOT_bypass_the_budget()
        {
            LetterMailboxPolicy.BypassesBudget(LetterReason.BattleVictory).Should().BeFalse();
        }
    }
}
