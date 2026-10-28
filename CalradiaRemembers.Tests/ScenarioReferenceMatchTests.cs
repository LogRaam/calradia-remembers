// Code written by Gabriel Mailhot, 11/10/2026.
// Decides which hero or kingdom a scenario rule means when it names one ("empire_s", "lord_1_14", "Rhagaea").
// If it is wrong, a shipped rule acts on the wrong realm: cr.test scenario_death found "empire" (the Northern
// Empire's id) picking the Southern Empire, whose name contains the word, so the war the pack declares was skipped.
// And the game translates names, so in French or Turkish only an id still resolves; an id must beat any name.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Scenarios;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ScenarioReferenceMatchTests
    {
        private static readonly ScenarioCandidate South = new() {Id = "empire_s", Name = "Southern Empire"};
        private static readonly ScenarioCandidate North = new() {Id = "empire", Name = "Northern Empire"};
        private static readonly ScenarioCandidate West = new() {Id = "empire_w", Name = "Western Empire"};

        // STAKE: Rhagaea's death declares war "empire" against "empire_s". With the South listed first (her own realm
        // is in the event), "empire" picked the South and the war against herself was skipped.
        [Test]
        public void GIVEN_an_id_that_another_name_contains_WHEN_the_name_comes_first_THEN_the_exact_id_wins()
        {
            ScenarioReferenceMatch.BestIndex(new List<ScenarioCandidate> {South, North, West}, "empire").Should().Be(1);
        }

        // STAKE: a modder who writes the full English name keeps getting that realm, not a namesake listed earlier.
        [Test]
        public void GIVEN_an_exact_name_and_a_longer_name_containing_it_WHEN_matching_THEN_the_exact_name_wins()
        {
            var empire = new ScenarioCandidate {Id = "x1", Name = "Empire of the Sun"};
            var exact = new ScenarioCandidate {Id = "x2", Name = "Empire"};

            ScenarioReferenceMatch.BestIndex(new List<ScenarioCandidate> {empire, exact}, "Empire").Should().Be(1);
        }

        // STAKE: the packs written by hand say "Rhagaea", not her full name; a short name must still find her.
        [Test]
        public void GIVEN_a_short_name_WHEN_only_a_longer_name_contains_it_THEN_that_candidate_is_named()
        {
            var rhagaea = new ScenarioCandidate {Id = "lord_1_14", Name = "Rhagaea Pethros"};

            ScenarioReferenceMatch.BestIndex(new List<ScenarioCandidate> {South, rhagaea}, "rhagaea").Should().Be(1);
        }

        // STAKE: the event's own heroes are listed first; between two equally good matches, the one the event is
        // about must be chosen, not a stranger elsewhere in the world.
        [Test]
        public void GIVEN_two_equally_good_matches_WHEN_matching_THEN_the_first_listed_wins()
        {
            var first = new ScenarioCandidate {Id = "a", Name = "Ira"};
            var second = new ScenarioCandidate {Id = "b", Name = "Ira"};

            ScenarioReferenceMatch.BestIndex(new List<ScenarioCandidate> {first, second}, "Ira").Should().Be(0);
        }

        // STAKE: in a translated game the English name finds nothing; the rule must see "no such realm", never a
        // random one.
        [Test]
        public void GIVEN_a_reference_no_candidate_matches_WHEN_matching_THEN_none_is_named()
        {
            var nord = new ScenarioCandidate {Id = "empire", Name = "Empire du Nord"};

            ScenarioReferenceMatch.BestIndex(new List<ScenarioCandidate> {nord}, "Northern Empire").Should().Be(-1);
        }

        // STAKE: a blank reference in a hand-typed pack must name nobody, rather than match every name (an empty
        // string is contained in all of them).
        [Test]
        public void GIVEN_a_blank_reference_WHEN_matching_THEN_none_is_named()
        {
            ScenarioReferenceMatch.BestIndex(new List<ScenarioCandidate> {South, North}, "  ").Should().Be(-1);
            ScenarioReferenceMatch.Rank(South, null).Should().Be(ScenarioReferenceMatch.None);
        }

        // STAKE: ids are typed by hand; "Empire_S" must still be the Southern Empire, as names already ignore case.
        [Test]
        public void GIVEN_an_id_in_another_case_WHEN_ranking_THEN_it_is_an_exact_id()
        {
            ScenarioReferenceMatch.Rank(South, "Empire_S").Should().Be(ScenarioReferenceMatch.ExactId);
        }
    }
}
