// Code written by Gabriel Mailhot, 07/08/2026.
// The letter archive is a player-requested feature: read your whole correspondence with a contact as a
// dated, threaded conversation. This assembler is that thread's only source of truth (the in-game viewer
// just binds it), so these tests pin what actually reaches the player: entries oldest-first, the exact
// header wording per direction ("Letter from X" vs "Your letter/reply to X") dated in the Calradian
// calendar, a player's inline reply surfaced as its own entry, in-flight/lost/blank letters kept out, and
// the newest-only display cap. Get any of these wrong and the archive misdates, misattributes, reorders,
// or leaks a letter that never actually arrived.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterThreadAssemblerTests
    {
        private static PendingLetter Incoming(int sentOnDay, string body, bool delivered = true)
            => new PendingLetter
            {
                SenderName = "Derthert",
                Content = body,
                SentOnDay = sentOnDay,
                DeliveryDay = sentOnDay + 2,
                IsDelivered = delivered
            };

        private static PlayerLetter Outgoing(int sentOnDay, string body, bool delivered = true)
            => new PlayerLetter
            {
                RecipientName = "Derthert",
                Content = body,
                SentOnDay = sentOnDay,
                DeliveryDay = sentOnDay + 1,
                IsDelivered = delivered
            };

        // Day 210 is chosen to exercise every part of the calendar clause at once (3rd year, autumn, day 43),
        // so a regression in season, year, ordinal, or within-year math shows up as a single readable failure.
        // This is the header the player literally sees at the top of a received letter in the archive.
        [Test]
        public void GIVEN_an_incoming_letter_WHEN_building_its_header_THEN_it_reads_letter_from_contact_with_the_date()
        {
            LetterThreadAssembler.Header(LetterDirection.Incoming, "Derthert", 210)
                .Should().Be("Letter from Derthert, autumn of the 3rd year, day 43");
        }

        // A letter the player sent must be attributed to the player, not the contact, or the thread would read
        // as if the NPC wrote both halves of the conversation.
        [Test]
        public void GIVEN_an_outgoing_letter_WHEN_building_its_header_THEN_it_reads_your_letter_to_contact()
        {
            LetterThreadAssembler.Header(LetterDirection.Outgoing, "Derthert", 210)
                .Should().Be("Your letter to Derthert, autumn of the 3rd year, day 43");
        }

        // A reply that rode inline on a received letter is still the player's writing but is a response, not a
        // fresh dispatch; the distinct "reply" wording is what lets the player follow who answered whom.
        [Test]
        public void GIVEN_an_outgoing_reply_WHEN_building_its_header_THEN_it_reads_your_reply_to_contact()
        {
            LetterThreadAssembler.Header(LetterDirection.Outgoing, "Derthert", 210, isReply: true)
                .Should().Be("Your reply to Derthert, autumn of the 3rd year, day 43");
        }

        // A save written before dates were stored carries day 0; the header must drop the date clause rather
        // than print a confidently wrong "spring of the 1st year, day 1" for a letter of unknown vintage.
        [Test]
        public void GIVEN_an_unknown_day_WHEN_building_a_header_THEN_the_date_clause_is_omitted()
        {
            LetterThreadAssembler.Header(LetterDirection.Incoming, "Derthert", 0)
                .Should().Be("Letter from Derthert");
        }

        // The whole point of the feature: a two-way correspondence must read in the order it happened,
        // interleaving both sides by day, not grouped by sender.
        [Test]
        public void GIVEN_letters_in_both_directions_WHEN_assembling_the_thread_THEN_it_is_ordered_oldest_first()
        {
            var incoming = new List<PendingLetter> { Incoming(30, "third"), Incoming(10, "first") };
            var outgoing = new List<PlayerLetter> { Outgoing(20, "second") };

            IReadOnlyList<LetterThreadEntry> thread =
                LetterThreadAssembler.Build("Derthert", incoming, outgoing, LetterArchivePolicy.PerContactCap);

            thread.Should().HaveCount(3);
            thread[0].Body.Should().Be("first");
            thread[1].Body.Should().Be("second");
            thread[2].Body.Should().Be("third");
            thread[1].Direction.Should().Be(LetterDirection.Outgoing);
        }

        // The player asked to see "the full exchange": a reply typed into a received letter must appear as its
        // own outgoing line, dated to the day the letter was received, so the back-and-forth is complete.
        [Test]
        public void GIVEN_a_received_letter_with_an_inline_reply_WHEN_assembling_THEN_the_reply_is_its_own_outgoing_entry()
        {
            PendingLetter letter = Incoming(10, "I miss you");
            letter.DeliveredOnDay = 13;
            letter.PlayerReply = "and I you";

            IReadOnlyList<LetterThreadEntry> thread =
                LetterThreadAssembler.Build("Derthert", new List<PendingLetter> { letter }, null, LetterArchivePolicy.PerContactCap);

            thread.Should().HaveCount(2);
            thread[0].IsIncoming.Should().BeTrue();
            thread[1].Direction.Should().Be(LetterDirection.Outgoing);
            thread[1].Body.Should().Be("and I you");
            thread[1].Day.Should().Be(13); // dated to the arrival day, not the letter's own send day
        }

        // Only letters that truly arrived are correspondence. An undelivered (in transit) or blank letter must
        // never show in the archive, or the player would "read" a letter they were never actually sent.
        [Test]
        public void GIVEN_undelivered_or_blank_letters_WHEN_assembling_THEN_they_are_excluded()
        {
            var incoming = new List<PendingLetter>
            {
                Incoming(10, "delivered"),
                Incoming(11, "in transit", delivered: false),
                Incoming(12, "   ") // blank body
            };

            IReadOnlyList<LetterThreadEntry> thread =
                LetterThreadAssembler.Build("Derthert", incoming, null, LetterArchivePolicy.PerContactCap);

            thread.Should().HaveCount(1);
            thread[0].Body.Should().Be("delivered");
        }

        // A decades-long correspondence must not flood the view; the cap keeps the NEWEST letters (the ones the
        // player most likely wants), dropping the oldest, and the count matches the cap exactly.
        [Test]
        public void GIVEN_more_letters_than_the_cap_WHEN_assembling_THEN_only_the_newest_are_kept()
        {
            var incoming = new List<PendingLetter>
            {
                Incoming(1, "oldest"),
                Incoming(2, "old"),
                Incoming(3, "recent"),
                Incoming(4, "newest")
            };

            IReadOnlyList<LetterThreadEntry> thread =
                LetterThreadAssembler.Build("Derthert", incoming, null, cap: 2);

            thread.Should().HaveCount(2);
            thread[0].Body.Should().Be("recent");
            thread[1].Body.Should().Be("newest");
        }
    }
}
