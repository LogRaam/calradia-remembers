// Code written by Gabriel Mailhot, 30/10/2026.
// The letter box (Gabriel, 30/10/2026): every letter waits in a box ordered by arrival; urgent ones wear a mark; a thrown
// letter means "the player no longer wants to hear of this subject from this sender". This decides a letter's subject,
// which letters are urgent, what a throw silences and the box's order. If it is wrong, a throw silences too much (a
// lover's whole correspondence for one tournament note) or too little (the sequel of a thrown letter still arrives), a
// silenced war summons hides a deadline the game still enforces, or the box shuffles letters out of the order they came.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterBoxPolicyTests
    {
        // STAKE: Gabriel's own case. The jealous husband's letter thrown, his next jealous letter never comes; the throw
        // does not silence anything else he might write.
        [Test]
        public void GIVEN_a_thrown_jealous_letter_WHEN_the_same_sender_writes_again_THEN_only_that_subject_is_silenced()
        {
            List<string> silenced = LetterBoxPolicy.AfterThrow(null, LetterReason.JealousThreat);

            LetterBoxPolicy.IsUrgent(LetterReason.JealousThreat).Should().BeTrue();
            LetterBoxPolicy.IsSilenced(silenced, LetterReason.JealousThreat).Should().BeTrue();
            LetterBoxPolicy.IsSilenced(silenced, LetterReason.RealmTidings).Should().BeFalse();
        }

        // STAKE: one conversation is one subject. A thrown divorce demand cannot leave its escalation to arrive, nor a
        // thrown love letter its "you did not answer me".
        [Test]
        public void GIVEN_letters_of_one_conversation_WHEN_their_subjects_are_read_THEN_they_share_one()
        {
            LetterBoxPolicy.SubjectOf(LetterReason.RomanticCorrespondence).Should().Be(LetterBoxPolicy.SubjectOf(LetterReason.AwaitingReply));
            LetterBoxPolicy.SubjectOf(LetterReason.SpouseDivorceDemand).Should().Be(LetterBoxPolicy.SubjectOf(LetterReason.SpouseDivorceEscalation));
            LetterBoxPolicy.SubjectOf(LetterReason.TournamentVictory).Should().NotBe(LetterBoxPolicy.SubjectOf(LetterReason.RomanticCorrespondence));
        }

        // STAKE: a war summons may be thrown (Gabriel: "oui"), but its subject is not silenced: the next summons still
        // carries a deadline the game holds the player to. Claude's default, put to Gabriel.
        [Test]
        public void GIVEN_a_war_summons_or_a_divorce_thrown_WHEN_the_next_one_comes_THEN_it_is_not_silenced()
        {
            LetterBoxPolicy.IsUrgent(LetterReason.WarSummons).Should().BeTrue();
            List<string> silenced = LetterBoxPolicy.AfterThrow(null, LetterReason.WarSummons);

            silenced.Should().BeEmpty();
            LetterBoxPolicy.IsSilenced(new[] {LetterBoxPolicy.SubjectOf(LetterReason.WarSummons)}, LetterReason.WarSummons).Should().BeFalse();
            LetterBoxPolicy.AfterThrow(null, LetterReason.SpouseDivorceDemand).Should().BeEmpty();
        }

        // STAKE: a reply to the player's own letter was asked for, and another mod's letter is that mod's to pace: neither
        // is ever silenced by a throw.
        [Test]
        public void GIVEN_a_reply_or_another_mods_letter_WHEN_thrown_THEN_nothing_is_silenced()
        {
            LetterBoxPolicy.AfterThrow(null, LetterReason.PlayerLetterReply).Should().BeEmpty();
            LetterBoxPolicy.AfterThrow(null, LetterReason.FromAnotherMod).Should().BeEmpty();
        }

        // STAKE: every reason has a subject or is deliberately never silenced; a reason added later and forgotten here
        // falls to its own name rather than silencing nothing or everything.
        [Test]
        public void GIVEN_every_reason_WHEN_its_subject_is_read_THEN_only_the_two_unsilenceable_ones_have_none()
        {
            var none = System.Enum.GetValues(typeof(LetterReason)).Cast<LetterReason>().Where(r => LetterBoxPolicy.SubjectOf(r) == null).ToList();

            none.Should().BeEquivalentTo(new[] {LetterReason.PlayerLetterReply, LetterReason.FromAnotherMod});
        }

        // STAKE: throwing the same subject twice keeps one entry, so "accept their letters again" undoes it in one click.
        [Test]
        public void GIVEN_a_subject_already_silenced_WHEN_thrown_again_THEN_it_is_listed_once()
        {
            List<string> once = LetterBoxPolicy.AfterThrow(null, LetterReason.RealmTidings);

            LetterBoxPolicy.AfterThrow(once, LetterReason.LeakedCorrespondence).Should().HaveCount(1);
        }

        // STAKE: Gabriel, "classees par ordre de reception": the oldest arrival first, and two letters of one day in the
        // order they came.
        [Test]
        public void GIVEN_letters_arrived_on_several_days_WHEN_the_box_is_ordered_THEN_it_reads_oldest_first()
        {
            var entries = new[] {
                new LetterBoxEntry {SenderId = "b", ArrivedOnDay = 12, Sequence = 0},
                new LetterBoxEntry {SenderId = "a", ArrivedOnDay = 10, Sequence = 4},
                new LetterBoxEntry {SenderId = "c", ArrivedOnDay = 10, Sequence = 1}
            };

            LetterBoxPolicy.Ordered(entries).Select(e => e.SenderId).Should().Equal("c", "a", "b");
        }

        // STAKE: the badge vanishes at zero and never overflows the small map bar badge.
        [Test]
        public void GIVEN_counts_WHEN_the_badge_is_written_THEN_empty_at_zero_and_two_digits_at_most()
        {
            LetterBoxPolicy.BadgeText(0).Should().BeEmpty();
            LetterBoxPolicy.BadgeText(7).Should().Be("7");
            LetterBoxPolicy.BadgeText(250).Should().Be("99");
        }
    }
}
