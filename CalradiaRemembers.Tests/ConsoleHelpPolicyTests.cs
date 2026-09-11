// Code written by Gabriel Mailhot, 10/09/2026.
// What this pins: that cr.help can never again omit a command, or name one that does not exist, and that reading
// 259 of them stays possible.
//
// Why it exists. Gabriel asked, on 2026-09-10, whether cr.help was still complete. It described 70 of the 259
// commands the assembly actually registers, so 189 were invisible, among them cr.testall and cr.test, which the
// two of us had run a dozen times that same evening. And one of the 70 was dead: cr.compose had been renamed
// cr.letter_compose, so the one name a reader was most likely to copy verbatim was the one that would fail. His
// answer on being told: "les joueurs utilisent beaucoup les commandes consoles. Ce serait bien qu'elles soient
// bien documentees."
//
// The cause is the shape, not carelessness. A hand-written list of every command in a project that gains commands
// weekly can only drift, and it drifts in silence because nothing ever compares it with anything. So there are
// three layers now and only one is written by hand: the registry says what EXISTS, a table says what each one
// DOES, and the curated sections stay the editorial layer. These tests hold the halves apart: what may be CLAIMED
// (never a dead command), what must be REACHABLE (all of them), and what must stay READABLE (not 259 paragraphs).
//
// If these tests fail, the console help is about to start lying again, hiding things again, or burying them.

#region

using System;
using System.Collections.Generic;
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

        private static readonly Dictionary<string, ConsoleCommandDoc> Docs = new(StringComparer.OrdinalIgnoreCase) {
            ["memory"] = new ConsoleCommandDoc("[name]", "Lists every recorded event with its index. A second sentence no listing needs."),
            ["forget"] = new ConsoleCommandDoc("<index> [name]", "Deletes one event by index."),
            ["testall"] = new ConsoleCommandDoc("", "Runs the mechanical self-tests and prints a summary."),
            ["test"] = new ConsoleCommandDoc("<name>", "Runs one named self-test."),
            ["letter_compose"] = new ConsoleCommandDoc("[name]", "Opens the letter compose window.")
        };

        // The omission, stated as the rule that ends it. cr.testall was invisible for months because nobody had
        // written its sentence into a hand-kept list; the list is no longer what decides.
        [Test]
        public void GIVEN_the_whole_registry_WHEN_listing_everything_THEN_not_one_command_is_left_out()
        {
            string all = ConsoleHelpPolicy.RenderAll(Registered, Docs);

            foreach (string command in Registered) all.Should().Contain("cr." + command);
        }

        // The shape of a command belongs in the list, because half of knowing a command is knowing what it takes.
        [Test]
        public void GIVEN_commands_that_take_arguments_WHEN_listing_THEN_the_arguments_are_shown_with_them()
        {
            string all = ConsoleHelpPolicy.RenderAll(Registered, Docs);

            all.Should().Contain("cr.forget <index> [name]").And.Contain("cr.memory [name]");
        }

        // Gabriel's actual request: every command documented, enforced rather than hoped for. This is the check
        // cr.testall runs, and the reason adding a command now means writing its line.
        [Test]
        public void GIVEN_a_command_nobody_wrote_a_line_for_WHEN_checked_THEN_it_is_reported_as_undocumented()
        {
            ConsoleHelpPolicy.Undocumented(Registered, Docs).Should().BeEmpty();

            var missing = new[] {"memory", "brand_new_command"};
            ConsoleHelpPolicy.Undocumented(missing, Docs).Should().BeEquivalentTo(new[] {"brand_new_command"});
        }

        // An entry with a name but no words is not documentation, and must be caught rather than counted.
        [Test]
        public void GIVEN_an_entry_with_no_words_in_it_WHEN_checked_THEN_it_counts_as_undocumented()
        {
            var hollow = new Dictionary<string, ConsoleCommandDoc>(StringComparer.OrdinalIgnoreCase) {
                ["memory"] = new ConsoleCommandDoc("[name]", "")
            };

            ConsoleHelpPolicy.Undocumented(new[] {"memory"}, hollow).Should().Contain("memory");
        }

        // The cr.compose error, in the table half. The only failure that actively misleads rather than omitting.
        [Test]
        public void GIVEN_a_documented_command_that_was_renamed_away_WHEN_checked_THEN_it_is_reported_as_dead()
        {
            var stale = new Dictionary<string, ConsoleCommandDoc>(StringComparer.OrdinalIgnoreCase) {
                ["compose"] = new ConsoleCommandDoc("[name]", "Opens the compose window.")
            };

            ConsoleHelpPolicy.Dead(Registered, stale).Should().Contain("compose");
            ConsoleHelpPolicy.Dead(Registered, Docs).Should().BeEmpty();
        }

        // And the same error in the hand-written half, which is exactly where it actually happened.
        [Test]
        public void GIVEN_the_curated_prose_names_a_command_that_is_gone_WHEN_checked_THEN_it_is_reported_too()
        {
            ConsoleHelpPolicy.DeadInCuratedText(Registered, "  cr.compose  Open the compose window")
                             .Should().Contain("compose");
            ConsoleHelpPolicy.DeadInCuratedText(Registered, Curated).Should().BeEmpty();
        }

        // A reader has no way to know that what they just read is a fraction of what exists unless it says so.
        [Test]
        public void GIVEN_the_curated_page_WHEN_closed_THEN_it_says_how_many_there_are_and_how_to_reach_them()
        {
            string footer = ConsoleHelpPolicy.Footer(Registered);

            footer.Should().Contain("5 commands").And.Contain("cr.help all")
                  .And.Contain("cr.help <text>").And.Contain("cr.help <command>");
        }

        // Typing a command's exact name is asking about that command, not searching for it, and it should answer
        // in full rather than with the one line a listing shows.
        [Test]
        public void GIVEN_an_exact_command_name_WHEN_asked_THEN_it_answers_in_full_rather_than_listing()
        {
            string answer = ConsoleHelpPolicy.Search(Registered, Docs, "memory");

            answer.Should().Contain("cr.memory [name]").And.Contain("A second sentence no listing needs.");
            answer.Should().NotContain("matching");
        }

        // The cost of stopping at the exact hit, found by cr.testall in a live game (2026-09-10). A player who
        // types the stem of a family is exactly the player who does not yet know the family exists: `cr.help duel`
        // explained cr.duel and never mentioned duel_challenge, duel_arm, duel_town, duel_courtship or duel_here,
        // so the one word most likely to be typed was the one that hid five commands. Gabriel asked for these to
        // be well documented because players use them heavily; a help that answers correctly and incompletely is
        // the same dead end as one that answers wrongly.
        [Test]
        public void GIVEN_a_name_that_is_also_the_stem_of_a_family_WHEN_asked_THEN_the_relatives_are_named_too()
        {
            string answer = ConsoleHelpPolicy.Search(Registered, Docs, "test");

            answer.Should().Contain("Runs one named self-test.");  // the exact command, in full
            answer.Should().Contain("cr.testall");                 // and the relative it used to hide
        }

        // The mirror, so the previous rule cannot be satisfied by simply listing everything at every hit: a name
        // with no relatives answers with itself alone, and says nothing about a family that does not exist.
        [Test]
        public void GIVEN_a_name_with_no_relatives_WHEN_asked_THEN_nothing_is_invented_to_list_beside_it()
        {
            string answer = ConsoleHelpPolicy.Search(Registered, Docs, "forget");

            answer.Should().Contain("Deletes one event by index.");
            answer.Should().NotContain("more command");
        }

        // Case must not matter: nobody types a console command with the shift key.
        [Test]
        public void GIVEN_a_name_in_any_case_WHEN_asked_THEN_it_is_still_recognised()
        {
            ConsoleHelpPolicy.Search(Registered, Docs, "MEMORY").Should().Contain("Lists every recorded event");
        }

        // A fragment is a search, and a short result is worth a line of description each: that is what makes a
        // family of commands legible at a glance.
        [Test]
        public void GIVEN_a_fragment_WHEN_searching_THEN_each_match_comes_with_what_it_does()
        {
            string answer = ConsoleHelpPolicy.Search(Registered, Docs, "et"); // memory? no: forget, letter_compose

            answer.Should().Contain("cr.forget").And.Contain("Deletes one event by index.");
            answer.Should().Contain("cr.letter_compose").And.Contain("Opens the letter compose window.");
        }

        // A long description is cut at a sentence, so a listing stays one line per command and never ends in half
        // a word. cr.memory's second sentence is there precisely to be dropped here.
        [Test]
        public void GIVEN_a_long_description_WHEN_listed_in_a_search_THEN_only_its_first_sentence_is_shown()
        {
            ConsoleHelpPolicy.Search(Registered, Docs, "mem")
                             .Should().Contain("Lists every recorded event with its index.")
                             .And.NotContain("A second sentence no listing needs.");
        }

        // Shorten is the piece that keeps the console readable, and its worst failure would be cutting mid-word.
        [Test]
        public void GIVEN_prose_with_no_sentence_end_WHEN_shortened_THEN_it_cuts_at_a_word_and_says_it_was_cut()
        {
            string runaway = string.Join(" ", Enumerable.Repeat("interminable", 40));
            string shortened = ConsoleHelpPolicy.Shorten(runaway);

            shortened.Should().EndWith("...");
            shortened.Length.Should().BeLessThan(runaway.Length);
            shortened.Should().NotContain("intermina..."); // never mid-word
            ConsoleHelpPolicy.Shorten("Short enough.").Should().Be("Short enough.");
            ConsoleHelpPolicy.Shorten(null).Should().BeEmpty();
        }

        // Many matches collapse to names alone, or a search for a common syllable prints a page of prose.
        [Test]
        public void GIVEN_more_matches_than_the_threshold_WHEN_searching_THEN_it_answers_with_names_alone()
        {
            var many = Enumerable.Range(0, ConsoleHelpPolicy.CompactSearchThreshold + 3)
                                 .Select(i => $"probe_{i}").ToArray();
            var docs = many.ToDictionary(n => n, n => new ConsoleCommandDoc("", "Some description worth a line."),
                                         StringComparer.OrdinalIgnoreCase);

            string answer = ConsoleHelpPolicy.Search(many, docs, "probe");

            answer.Should().NotContain("Some description worth a line.");
            answer.Should().Contain("Narrow the search");
        }

        // A search that finds nothing must point somewhere rather than answering with silence, which reads as a
        // broken command.
        [Test]
        public void GIVEN_a_fragment_matching_nothing_WHEN_searching_THEN_it_points_at_the_full_list()
        {
            ConsoleHelpPolicy.Search(Registered, Docs, "zzz").Should().Contain("No cr command").And.Contain("cr.help all");
        }

        // An empty search is not a search: it is someone asking for everything.
        [Test]
        public void GIVEN_an_empty_fragment_WHEN_searching_THEN_everything_is_listed()
        {
            foreach (string fragment in new[] {null, "", "   "})
                ConsoleHelpPolicy.Search(Registered, Docs, fragment).Should().Contain("cr.testall");
        }

        // Duplicates, blanks and stray whitespace come from reflection over a real assembly, and none of them may
        // reach a player's console as a line of their own.
        [Test]
        public void GIVEN_a_ragged_registry_WHEN_listing_THEN_it_is_tidied_rather_than_shown_as_it_is()
        {
            var ragged = new[] {"memory", " memory ", "MEMORY", "", null, "   ", "forget"};

            string all = ConsoleHelpPolicy.RenderAll(ragged, Docs);

            all.Split('\n').Count(l => l.Contains("cr.memory")).Should().Be(1);
            all.Should().NotContain("cr. ").And.Contain("cr.forget");
        }

        // Order must be stable, so two runs read the same way and a player can scan the list the second time the
        // way they learned it the first.
        [Test]
        public void GIVEN_a_registry_in_any_order_WHEN_listing_THEN_it_comes_out_alphabetical()
        {
            string all = ConsoleHelpPolicy.RenderAll(new[] {"zeta", "alpha", "mu"}, null);

            all.IndexOf("cr.alpha").Should().BeLessThan(all.IndexOf("cr.mu"));
            all.IndexOf("cr.mu").Should().BeLessThan(all.IndexOf("cr.zeta"));
        }

        // Nothing registered is impossible in practice, so it must read as the anomaly it is rather than as an
        // empty and apparently working list.
        [Test]
        public void GIVEN_an_empty_registry_WHEN_listing_THEN_it_says_so_instead_of_showing_nothing()
        {
            ConsoleHelpPolicy.RenderAll(null, Docs).Should().Contain("should be impossible");
            ConsoleHelpPolicy.RenderAll(new string[0], Docs).Should().Contain("should be impossible");
        }

        // A command with no documentation must still LIST, or the very failure being fixed comes back through the
        // door marked "we have no sentence for this one yet".
        [Test]
        public void GIVEN_a_command_with_no_entry_WHEN_listing_or_searching_THEN_it_is_still_shown()
        {
            var registered = new[] {"memory", "brand_new_command"};

            ConsoleHelpPolicy.RenderAll(registered, Docs).Should().Contain("cr.brand_new_command");
            ConsoleHelpPolicy.Search(registered, Docs, "brand").Should().Contain("cr.brand_new_command");
            ConsoleHelpPolicy.Explain("brand_new_command", Docs).Should().BeEmpty();
        }
    }
}
