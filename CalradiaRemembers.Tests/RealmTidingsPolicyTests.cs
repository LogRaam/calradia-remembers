// Code written by Gabriel Mailhot, 08/08/2026.
// Guards the subject-selection at the heart of the realm-tidings letter: a friend writes unbidden to RELAY a
// real world event, never to invent one. Three promises rest on this pure pick and all break in silence if it
// drifts: the per-NPC non-repeat (an NPC must never relay the same event twice, or the player gets the same news
// on loop from one friend), the no-fake-news floor (the policy may only ever hand back a real known event, never
// conjure one, so returning null when nothing qualifies is a feature not a gap), and the no-trivial-spam floor
// (gossip below a salience worth a friend's ink must be left unsaid). The tie-break must stay deterministic or
// the same inputs could relay different events run to run, which no test could then pin.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.RealmTidings;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RealmTidingsPolicyTests
    {
        private static RealmTidingsPolicy.RealmTidingsCandidate Candidate(string id, int salience)
        {
            return new RealmTidingsPolicy.RealmTidingsCandidate(id, salience);
        }

        // If the friend did not lead with their weightiest news, the whole feature reads as noise: the player
        // should hear the looming war before an idle rumour. The most salient worth-telling event must win.
        [Test]
        public void GIVEN_several_worth_telling_events_WHEN_selecting_THEN_the_most_salient_is_relayed()
        {
            var candidates = new List<RealmTidingsPolicy.RealmTidingsCandidate>
            {
                Candidate("rumour", 15),
                Candidate("war", 80),
                Candidate("hardship", 40)
            };

            RealmTidingsPolicy.SelectSubject(candidates, new string[0]).Should().Be("war");
        }

        // The per-NPC non-repeat is the feature's core courtesy: one friend must never relay the same event
        // twice. When the most salient event is already sent, the pick must fall through to the next fresh one
        // rather than repeating itself or falling silent while real news remains untold.
        [Test]
        public void GIVEN_the_most_salient_event_already_sent_WHEN_selecting_THEN_the_next_fresh_event_is_relayed()
        {
            var candidates = new List<RealmTidingsPolicy.RealmTidingsCandidate>
            {
                Candidate("war", 80),
                Candidate("hardship", 40)
            };

            RealmTidingsPolicy.SelectSubject(candidates, new[] { "war" }).Should().Be("hardship");
        }

        // The dedup floor in full: if this writer has already relayed everything they know, they have nothing
        // fresh to say, so they must stay silent rather than repeat old news. Null means "write no letter".
        [Test]
        public void GIVEN_every_candidate_already_sent_WHEN_selecting_THEN_null_is_returned()
        {
            var candidates = new List<RealmTidingsPolicy.RealmTidingsCandidate>
            {
                Candidate("war", 80),
                Candidate("hardship", 40)
            };

            RealmTidingsPolicy.SelectSubject(candidates, new[] { "war", "hardship" }).Should().BeNull();
        }

        // The no-trivial-spam floor: a realm full of minor rumours should not pester a friend into writing. If
        // nothing clears MinSalience, the honest answer is silence, not the least-trivial triviality.
        [Test]
        public void GIVEN_all_candidates_below_the_salience_floor_WHEN_selecting_THEN_null_is_returned()
        {
            var candidates = new List<RealmTidingsPolicy.RealmTidingsCandidate>
            {
                Candidate("murmur", RealmTidingsPolicy.MinSalience - 1),
                Candidate("whisper", 1)
            };

            RealmTidingsPolicy.SelectSubject(candidates, new string[0]).Should().BeNull();
        }

        // The floor is a threshold, not a strict cutoff: an event sitting exactly at MinSalience is worth a
        // letter. Drifting this edge by one would silently drop (or admit) a whole tier of borderline news.
        [Test]
        public void GIVEN_a_candidate_exactly_at_the_salience_floor_WHEN_selecting_THEN_it_qualifies()
        {
            var candidates = new List<RealmTidingsPolicy.RealmTidingsCandidate>
            {
                Candidate("edge", RealmTidingsPolicy.MinSalience)
            };

            RealmTidingsPolicy.SelectSubject(candidates, new string[0]).Should().Be("edge");
        }

        // Determinism is what lets any of these other tests pin a result at all: when two events tie on
        // salience, the ordinal-lowest EventId must win every time, so the same inputs never relay different
        // news between runs.
        [Test]
        public void GIVEN_two_candidates_tied_on_salience_WHEN_selecting_THEN_the_ordinal_lowest_event_id_wins()
        {
            var candidates = new List<RealmTidingsPolicy.RealmTidingsCandidate>
            {
                Candidate("zephyr", 50),
                Candidate("apple", 50)
            };

            RealmTidingsPolicy.SelectSubject(candidates, new string[0]).Should().Be("apple");
        }

        // The weekly tick runs on live, sometimes-empty data; a friend with no known events, or a first-ever
        // writer with no sent-ledger yet, must yield a quiet null, never a crash on the daily tick.
        [Test]
        public void GIVEN_null_or_empty_inputs_WHEN_selecting_THEN_null_is_returned_and_it_never_throws()
        {
            RealmTidingsPolicy.SelectSubject(null, new string[0]).Should().BeNull();
            RealmTidingsPolicy.SelectSubject(new List<RealmTidingsPolicy.RealmTidingsCandidate>(), new string[0]).Should().BeNull();
            RealmTidingsPolicy.SelectSubject(new List<RealmTidingsPolicy.RealmTidingsCandidate> { Candidate("war", 80) }, null)
                .Should().Be("war");
        }
    }
}
