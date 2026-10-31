// Code written by Gabriel Mailhot, 30/10/2026.
// The letter box (Gabriel, 30/10/2026): every letter waits in a box ordered by arrival; urgent ones wear a mark; a thrown
// letter means "the player no longer wants to hear of this subject from this sender". This decides a letter's subject,
// which letters are urgent, what a throw silences and the box's order. If it is wrong, a throw silences too much (a
// lover's whole correspondence for one tournament note) or too little (the sequel of a thrown letter still arrives), a
// silenced war summons hides a deadline the game still enforces, or the box shuffles letters out of the order they came.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
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

        // STAKE: "Unasked Letters" off (kurushimi00, 31/10/2026) must never hide what the game then holds the player to: a
        // summons or levy with its deadline, a divorce, a pension, blackmail. A reply is what the player asked for, a farewell
        // is how a companion leaves, a patrol's report is where the player decides, a courtship trial answers by a proposal.
        [TestCase(LetterReason.WarSummons)]
        [TestCase(LetterReason.WarLevyDemand)]
        [TestCase(LetterReason.SpouseDivorceDemand)]
        [TestCase(LetterReason.SpouseDivorceEscalation)]
        [TestCase(LetterReason.ChildSupportRequest)]
        [TestCase(LetterReason.CompanionFarewell)]
        [TestCase(LetterReason.PlayerLetterReply)]
        [TestCase(LetterReason.PatrolReport)]
        [TestCase(LetterReason.FromAnotherMod)]
        [TestCase(LetterReason.Blackmail)]
        [TestCase(LetterReason.JealousThreat)]
        [TestCase(LetterReason.MarriageProposal)]
        public void GIVEN_unasked_letters_turned_off_WHEN_a_letter_that_matters_comes_THEN_it_still_arrives(LetterReason reason)
        {
            LetterBoxPolicy.IsUnasked(reason).Should().BeFalse();
        }

        // STAKE: the flood itself. Congratulations, love letters, news and offers are what the switch is for; one left out
        // of it keeps the riders coming for a player who said stop.
        [TestCase(LetterReason.TournamentVictory)]
        [TestCase(LetterReason.BattleVictory)]
        [TestCase(LetterReason.RomanticCorrespondence)]
        [TestCase(LetterReason.SpouseCorrespondence)]
        [TestCase(LetterReason.RealmTidings)]
        [TestCase(LetterReason.StanceFavor)]
        [TestCase(LetterReason.PrisonerOffer)]
        [TestCase(LetterReason.ServiceOffer)]
        public void GIVEN_unasked_letters_turned_off_WHEN_a_character_would_write_of_their_own_accord_THEN_it_is_stopped(LetterReason reason)
        {
            LetterBoxPolicy.IsUnasked(reason).Should().BeTrue();
        }

        // STAKE: one door for the two ways a player says no. A subject thrown away stays silenced with the setting on, and the
        // setting off stops an unasked letter from a sender nothing was ever thrown from, but never a summons.
        [Test]
        public void GIVEN_the_players_two_ways_of_saying_no_WHEN_a_letter_comes_THEN_either_stops_it_and_neither_stops_a_duty()
        {
            LetterBoxPolicy.IsStopped(new[] {"news"}, LetterReason.RealmTidings, unaskedLettersOn: true).Should().BeTrue();
            LetterBoxPolicy.IsStopped(null, LetterReason.RealmTidings, unaskedLettersOn: true).Should().BeFalse();
            LetterBoxPolicy.IsStopped(null, LetterReason.RealmTidings, unaskedLettersOn: false).Should().BeTrue();
            LetterBoxPolicy.IsStopped(null, LetterReason.WarSummons, unaskedLettersOn: false).Should().BeFalse();
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
        // carries a deadline the game holds the player to; likewise a divorce (always told) and a patrol's report (on it the
        // player decides whether the men come home). Claude's default, put to Gabriel.
        [Test]
        public void GIVEN_a_war_summons_or_a_divorce_thrown_WHEN_the_next_one_comes_THEN_it_is_not_silenced()
        {
            LetterBoxPolicy.IsUrgent(LetterReason.WarSummons).Should().BeTrue();
            List<string> silenced = LetterBoxPolicy.AfterThrow(null, LetterReason.WarSummons);

            silenced.Should().BeEmpty();
            LetterBoxPolicy.IsSilenced(new[] {LetterBoxPolicy.SubjectOf(LetterReason.WarSummons)}, LetterReason.WarSummons).Should().BeFalse();
            LetterBoxPolicy.AfterThrow(null, LetterReason.SpouseDivorceDemand).Should().BeEmpty();
            LetterBoxPolicy.AfterThrow(null, LetterReason.PatrolReport).Should().BeEmpty();
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

        // STAKE: a silenced letter must not take the mailbox's daily place from a letter the player wants, nor spend a model
        // call only to arrive unseen. The other sender's news and this sender's other subjects stay in.
        [Test]
        public void GIVEN_a_silenced_subject_in_the_mailbox_pool_WHEN_the_pool_is_filtered_THEN_only_that_senders_subject_leaves()
        {
            var pool = new[] {
                new LetterCandidate {SenderId = "husband", Reason = LetterReason.JealousThreat},
                new LetterCandidate {SenderId = "husband", Reason = LetterReason.RealmTidings},
                new LetterCandidate {SenderId = "cousin", Reason = LetterReason.JealousThreat}
            };
            var silenced = new Dictionary<string, List<string>> {["husband"] = LetterBoxPolicy.AfterThrow(null, LetterReason.JealousThreat)};

            List<LetterCandidate> kept = LetterBoxPolicy.Unsilenced(pool, id => silenced.TryGetValue(id, out List<string> l) ? l : null);

            kept.Select(c => c.SenderId + ":" + c.Reason).Should().Equal("husband:RealmTidings", "cousin:JealousThreat");
        }
    }
}
