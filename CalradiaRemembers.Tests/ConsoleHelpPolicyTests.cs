// Code written by Gabriel Mailhot, 10/09/2026.
// What this pins: that cr.help can never again omit a command, or name one that does not exist.
//
// Why it exists. Gabriel asked, on 2026-09-10, whether cr.help was still complete. It described 70 of the 259
// commands the assembly actually registered, so 189 were invisible, among them cr.testall and cr.test, which the
// two of us had run a dozen times that same evening. And one of the 70 was dead: cr.compose had been renamed
// cr.letter_compose, so the one name a reader was most likely to copy verbatim was the one that would fail.
//
// The cause is the shape, not carelessness. A hand-written list of every command in a project that gains commands
// weekly can only drift, and it drifts in silence because nothing ever compares it with anything. So the list is
// no longer authored: the REGISTRY is the source of truth and the prose is an overlay on it. These tests hold the
// two halves apart: what may be CLAIMED (never a dead command) and what must be REACHABLE (all of them).
//
// If these tests fail, the console help is about to start lying again, or to start hiding things again.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ConsoleHelpPolicyTests
    {
        private const string Curated = "MEMORY\n  cr.memory [name]   List events\n  cr.forget <i>   Delete one\n";

        private static readonly string[] Registered = {"memory", "forget", "testall", "test", "letter_compose"};

        // The omission, stated as the rule that ends it: a command nobody wrote a sentence about must still be
        // findable. cr.testall was invisible for months precisely because nobody had written its sentence.
        [Test]
        public void GIVEN_a_command_nobody_described_WHEN_listing_everything_THEN_it_is_still_there()
        {
            string all = ConsoleHelpPolicy.RenderAll(Registered, Curated);

            foreach (string command in Registered) all.Should().Contain("cr." + command);
        }

        // The described ones are marked, so a reader can tell at a glance which have prose waiting for them and
        // which are bare dev probes.
        [Test]
        public void GIVEN_a_mixed_list_WHEN_listing_everything_THEN_the_described_ones_are_marked()
        {
            string all = ConsoleHelpPolicy.RenderAll(Registered, Curated);

            all.Should().Contain(". cr.memory").And.Contain("  cr.testall");
        }

        // The cr.compose error. This is the one that actively misleads, and the only one treated as a failure
        // rather than as a mere absence.
        [Test]
        public void GIVEN_the_help_describes_a_command_that_was_renamed_away_WHEN_checked_THEN_it_is_reported_as_dead()
        {
            ConsoleHelpPolicy.Dead(Registered, "  cr.compose   Open the compose window").Should().Contain("compose");
            ConsoleHelpPolicy.Dead(Registered, Curated).Should().BeEmpty();
        }

        // Being undescribed is not an error and must never be reported as one: most of these are dev probes that
        // would only bury the commands a player actually wants.
        [Test]
        public void GIVEN_commands_with_no_prose_WHEN_checked_THEN_they_are_listed_as_undescribed_not_as_broken()
        {
            ConsoleHelpPolicy.Undescribed(Registered, Curated)
                             .Should().BeEquivalentTo(new[] {"letter_compose", "test", "testall"});
            ConsoleHelpPolicy.Dead(Registered, Curated).Should().BeEmpty();
        }

        // A reader has no way to know that what they just read is a fraction of what exists unless it says so.
        [Test]
        public void GIVEN_the_curated_page_WHEN_closed_THEN_it_says_how_much_it_covers_and_where_the_rest_is()
        {
            string footer = ConsoleHelpPolicy.Footer(Registered, Curated);

            footer.Should().Contain("2 of 5").And.Contain("cr.help all").And.Contain("cr.help <text>");
        }

        // Search is how anyone actually looks for a command they half remember, and it must be case-insensitive
        // because nobody types a console command with the shift key.
        [Test]
        public void GIVEN_a_fragment_WHEN_searching_THEN_it_finds_every_command_containing_it_whatever_the_case()
        {
            ConsoleHelpPolicy.Search(Registered, Curated, "test").Should().Contain("cr.testall").And.Contain("cr.test");
            ConsoleHelpPolicy.Search(Registered, Curated, "TEST").Should().Contain("cr.testall");
            ConsoleHelpPolicy.Search(Registered, Curated, "mem").Should().Contain("cr.memory").And.NotContain("cr.forget");
        }

        // A search that finds nothing must point somewhere rather than answering with silence, which reads as a
        // broken command.
        [Test]
        public void GIVEN_a_fragment_matching_nothing_WHEN_searching_THEN_it_points_at_the_full_list()
        {
            ConsoleHelpPolicy.Search(Registered, Curated, "zzz").Should().Contain("No cr command").And.Contain("cr.help all");
        }

        // An empty search is not a search: it is someone asking for everything, and answering "no match" would be
        // the least useful reading of it.
        [Test]
        public void GIVEN_an_empty_fragment_WHEN_searching_THEN_everything_is_listed()
        {
            foreach (string fragment in new[] {null, "", "   "})
                ConsoleHelpPolicy.Search(Registered, Curated, fragment).Should().Contain("cr.testall");
        }

        // Duplicates, blanks and stray whitespace come from reflection over a real assembly, and none of them may
        // reach a player's console as a line of their own.
        [Test]
        public void GIVEN_a_ragged_registry_WHEN_listing_THEN_it_is_tidied_rather_than_shown_as_it_is()
        {
            var ragged = new[] {"memory", " memory ", "MEMORY", "", null, "   ", "forget"};

            string all = ConsoleHelpPolicy.RenderAll(ragged, Curated);

            all.Split('\n').Count(l => l.Contains("cr.memory")).Should().Be(1);
            all.Should().NotContain("cr. ").And.Contain("cr.forget");
        }

        // Order must be stable, so two runs of cr.help all read the same way and a player can scan the list the
        // second time the way they learned it the first.
        [Test]
        public void GIVEN_a_registry_in_any_order_WHEN_listing_THEN_it_comes_out_alphabetical()
        {
            string all = ConsoleHelpPolicy.RenderAll(new[] {"zeta", "alpha", "mu"}, "");

            all.IndexOf("cr.alpha").Should().BeLessThan(all.IndexOf("cr.mu"));
            all.IndexOf("cr.mu").Should().BeLessThan(all.IndexOf("cr.zeta"));
        }

        // Nothing registered is impossible in practice, so it must read as the anomaly it is rather than as an
        // empty and apparently working list.
        [Test]
        public void GIVEN_an_empty_registry_WHEN_listing_THEN_it_says_so_instead_of_showing_nothing()
        {
            ConsoleHelpPolicy.RenderAll(null, Curated).Should().Contain("should be impossible");
            ConsoleHelpPolicy.RenderAll(new string[0], Curated).Should().Contain("should be impossible");
        }
    }
}
