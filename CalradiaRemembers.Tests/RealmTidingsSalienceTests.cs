// Code written by Gabriel Mailhot, 08/08/2026.
// Guards the salience score that ranks which known world event a friend relays first. It folds an event's
// magnitude (how far the news carries) with how surely the writer knows it (RumorConfidence) into one int that
// RealmTidingsPolicy.SelectSubject sorts on. Two promises rest on it: magnitude must still carry the news (a
// looming war outranks an idle rumour), and confidence must genuinely bite (a confirmed report is worth more ink
// than a doubtful whisper of the same size, and a faint whisper of a trivial thing must sink below the policy's
// no-trivial-spam floor so it is left unsaid). It also runs on live, sometimes-malformed data every weekly tick,
// so a zero or negative magnitude must degrade quietly, never throw.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.RealmTidings;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RealmTidingsSalienceTests
    {
        // Magnitude must still carry the news: at the same confidence, the event that travels farther is the one
        // a friend leads with. If this inverted, the player would hear the skirmish before the looming war.
        [Test]
        public void GIVEN_same_confidence_WHEN_magnitude_is_higher_THEN_salience_is_higher()
        {
            RealmTidingsSalience.Of(60, RumorConfidence.WellKnown)
                .Should().BeGreaterThan(RealmTidingsSalience.Of(30, RumorConfidence.WellKnown));
        }

        // Confidence must bite: a confirmed report of an event outranks a doubtful, second-hand or distant whisper
        // of the SAME event, so the writer's certainty (and thus their willingness to relay it) tracks what they
        // actually know. Monotonic across all three levels.
        [Test]
        public void GIVEN_same_magnitude_WHEN_confidence_rises_THEN_salience_rises()
        {
            int distant = RealmTidingsSalience.Of(60, RumorConfidence.Distant);
            int secondhand = RealmTidingsSalience.Of(60, RumorConfidence.Secondhand);
            int wellKnown = RealmTidingsSalience.Of(60, RumorConfidence.WellKnown);

            distant.Should().BeLessThan(secondhand);
            secondhand.Should().BeLessThan(wellKnown);
        }

        // A confirmed, near event is worth its FULL reach: WellKnown applies no damping, so magnitude passes
        // through untouched. This is the anchor the two dampened tiers are measured against; drifting it would
        // silently rescale every realm-tidings pick.
        [Test]
        public void GIVEN_well_known_confidence_WHEN_scoring_THEN_the_full_magnitude_counts()
        {
            RealmTidingsSalience.Of(60, RumorConfidence.WellKnown).Should().Be(60);
        }

        // The no-trivial-spam interplay: a faint, distant whisper of a MINOR event must fall under the policy's
        // MinSalience floor, so the friend stays silent about gossip they are not even sure of rather than spending
        // a letter on it. The damping exists precisely to produce this silence.
        [Test]
        public void GIVEN_a_faint_whisper_of_a_trivial_event_WHEN_scoring_THEN_it_falls_below_the_spam_floor()
        {
            RealmTidingsSalience.Of(15, RumorConfidence.Distant)
                .Should().BeLessThan(RealmTidingsPolicy.MinSalience);
        }

        // The complement: a confirmed, sizable event must clear the floor comfortably, or a war looming (the
        // flagship realm-tidings event) would never be worth relaying at all and the feature would go mute.
        [Test]
        public void GIVEN_a_confirmed_sizable_event_WHEN_scoring_THEN_it_clears_the_spam_floor()
        {
            RealmTidingsSalience.Of(30, RumorConfidence.WellKnown)
                .Should().BeGreaterThanOrEqualTo(RealmTidingsPolicy.MinSalience);
        }

        // Confidence can genuinely REORDER, not just break ties: a confirmed mid-size event should be able to beat
        // a faint whisper of a bigger one, because a friend relays what they are sure of over what they half-heard.
        // This pins that the confidence weights are strong enough to matter, not cosmetic.
        [Test]
        public void GIVEN_a_confirmed_mid_event_and_a_faint_bigger_one_WHEN_scoring_THEN_confidence_can_win()
        {
            RealmTidingsSalience.Of(30, RumorConfidence.WellKnown)
                .Should().BeGreaterThan(RealmTidingsSalience.Of(48, RumorConfidence.Distant));
        }

        // The weekly tick runs over live, occasionally malformed events; a zero or negative magnitude must degrade
        // to a quiet zero rather than throw or go negative and pollute the ranking. A crash here would take down the
        // whole weekly letter pass.
        [Test]
        public void GIVEN_zero_or_negative_magnitude_WHEN_scoring_THEN_it_returns_zero_and_never_throws()
        {
            RealmTidingsSalience.Of(0, RumorConfidence.WellKnown).Should().Be(0);
            RealmTidingsSalience.Of(-40, RumorConfidence.WellKnown).Should().Be(0);
        }
    }
}
