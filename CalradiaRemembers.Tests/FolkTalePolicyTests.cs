// Code written by Gabriel Mailhot, 03/11/2026.
// Documents how a folklore file is read and which tales of their people a character carries. Gabriel, 03/11/2026: "Les NPC
// sauraient que c'est des contes, mais pourraient parfois en faire reference, ou meme raconter le conte si on leur demande."
//
// WHY IT MATTERS: the favourite is the only tale a character can tell, because only it carries its parts. If the choice
// moved between sessions, the same lord would love a different tale every evening (and the prompt cache would break); if a
// tale without parts were chosen, the character would be asked to tell a story nobody wrote, and would invent it.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Folklore;
using FluentAssertions;
using NpcMemoryService.Core.Knowledge;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class FolkTalePolicyTests
    {
        private const string File =
            "# Battanian tales\n"
            + "[The Hound of the Grey Ford]\n"
            + "gist: a hound that guards a ford\n"
            + "  until its master returns.\n"
            + "beat: A chief leaves his hound at the ford.\n"
            + "beat: The hound waits three winters.\n"
            + "\n"
            + "[The Piper Under the Hill]\n"
            + "gist: a piper who comes back old.\n"
            + "\n"
            + "[A Tale With No Outline]\n"
            + "beat: Something happens.\n";

        private static List<FolkTale> Many(int count, bool withBeats = true) =>
            Enumerable.Range(1, count).Select(i => new FolkTale {
                Title = $"Tale {i}",
                Gist = $"gist {i}.",
                Beats = withBeats ? new List<string> {$"beat {i}"} : new List<string>()
            }).ToList();

        // STAKE: a modder or Gabriel writes tales as plain text; a gist broken over two lines must stay one outline.
        [Test]
        public void GIVEN_a_folklore_file_WHEN_parsed_THEN_each_tale_keeps_its_title_outline_and_parts_in_order()
        {
            IReadOnlyList<FolkTale> tales = FolkTalePolicy.Parse(File);

            tales.Select(t => t.Title).Should().Equal("The Hound of the Grey Ford", "The Piper Under the Hill");
            tales[0].Gist.Should().Be("a hound that guards a ford until its master returns.");
            tales[0].Beats.Should().Equal("A chief leaves his hound at the ford.", "The hound waits three winters.");
            tales[1].Beats.Should().BeEmpty();
        }

        // STAKE: a tale with no outline could not even be alluded to, so it is left out rather than named blank.
        [Test]
        public void GIVEN_a_tale_with_no_gist_WHEN_parsed_THEN_it_is_left_out()
        {
            FolkTalePolicy.Parse(File).Should().NotContain(t => t.Title == "A Tale With No Outline");
        }

        // STAKE: the same lord must love the same tale every evening, in every session.
        [Test]
        public void GIVEN_the_same_character_WHEN_composed_twice_THEN_the_favourite_is_the_same()
        {
            List<FolkTale> tales = Many(5);

            FolkTalePolicy.Compose("lord_1_7", tales).Tales[0].Title
                .Should().Be(FolkTalePolicy.Compose("lord_1_7", tales).Tales[0].Title);
            FolkTalePolicy.StableHash("lord_1_7").Should().Be(FolkTalePolicy.StableHash("lord_1_7"));
        }

        // STAKE: if every Battanian loved the same tale, the people would have one story; different ids spread them.
        [Test]
        public void GIVEN_many_characters_WHEN_composed_THEN_they_do_not_all_love_the_same_tale()
        {
            List<FolkTale> tales = Many(5);

            Enumerable.Range(0, 40).Select(i => FolkTalePolicy.Compose($"lord_{i}", tales).Tales[0].Title)
                .Distinct().Count().Should().BeGreaterThan(1);
        }

        // STAKE: a favourite without parts would be a story the character is told to tell and must invent.
        [Test]
        public void GIVEN_tales_with_and_without_parts_WHEN_composed_THEN_the_favourite_is_one_with_parts()
        {
            var tales = new List<FolkTale> {
                new() {Title = "Outline only", Gist = "g."},
                new() {Title = "Tellable", Gist = "g.", Beats = new List<string> {"b"}}
            };

            foreach (string id in new[] {"a", "b", "c", "d", "e"})
                FolkTalePolicy.Compose(id, tales).Tales[0].Title.Should().Be("Tellable");
        }

        // STAKE: only the favourite may be told; any other tale carrying parts would invite a second, unchecked telling.
        [Test]
        public void GIVEN_a_composed_set_WHEN_read_THEN_only_the_favourite_carries_its_parts()
        {
            FolkloreFacts facts = FolkTalePolicy.Compose("lord_x", Many(4));

            facts.Tales[0].Beats.Should().NotBeEmpty();
            facts.Tales.Skip(1).Should().OnlyContain(t => t.Beats.Count == 0);
        }

        // STAKE: every tale costs a line in every prompt of that people; a long file must not grow every prompt with it.
        [Test]
        public void GIVEN_a_people_with_many_tales_WHEN_composed_THEN_a_character_carries_at_most_the_cap()
        {
            FolkloreFacts facts = FolkTalePolicy.Compose("lord_x", Many(12));

            facts.Tales.Should().HaveCount(FolkTalePolicy.MaxTalesKnown);
            facts.Tales.Select(t => t.Title).Should().OnlyHaveUniqueItems();
        }

        // STAKE: a people with no file (a minor culture, another mod's) says nothing rather than something invented.
        [Test]
        public void GIVEN_no_tales_WHEN_composed_THEN_nothing_is_carried()
        {
            FolkTalePolicy.Compose("lord_x", new List<FolkTale>()).Should().BeNull();
            FolkTalePolicy.Compose("lord_x", null).Should().BeNull();
        }

        // STAKE: a file of outlines only still lets a character allude to a tale, but never offers one to tell.
        [Test]
        public void GIVEN_only_outlines_WHEN_composed_THEN_tales_are_named_and_none_is_tellable()
        {
            FolkTalePolicy.Compose("lord_x", Many(3, withBeats: false)).Tales
                .Should().HaveCount(3).And.OnlyContain(t => t.Beats.Count == 0);
        }

        // tashmetu (05/11/2026): "Faiths of Calradia has faiths that cross cultures, with their own saints and legends."
        // Another mod may give a character tales of its own beside their people's.

        private static List<FolkTale> Saints(int count) =>
            Enumerable.Range(1, count).Select(i => new FolkTale {Title = $"Saint {i}", Gist = $"saint {i}.", Beats = new List<string> {$"saint beat {i}"}}).ToList();

        // STAKE: every character already in a save keeps the very tales and favourite they had, so nothing shifts (and the
        // prompt cache holds) for a player with no mod adding tales.
        [Test]
        public void GIVEN_no_outside_tales_WHEN_composed_THEN_the_choice_is_exactly_the_people_only_one()
        {
            List<FolkTale> people = Many(9);

            foreach (string id in new[] {"lord_a", "lord_b", "lady_c"})
            {
                FolkTalePolicy.Compose(id, people, new List<FolkTale>()).Tales.Select(t => t.Title)
                    .Should().Equal(FolkTalePolicy.Compose(id, people).Tales.Select(t => t.Title));
                FolkTalePolicy.Compose(id, people, null).Tales.Select(t => t.Title)
                    .Should().Equal(FolkTalePolicy.Compose(id, people).Tales.Select(t => t.Title));
            }
        }

        // STAKE: a pious lord of a people with many tales still carries his faith's legends, and his people's are not
        // pushed out by them either.
        [Test]
        public void GIVEN_many_tales_of_both_kinds_WHEN_composed_THEN_both_are_carried_within_the_limit()
        {
            FolkloreFacts facts = FolkTalePolicy.Compose("lord_x", Many(9), Saints(4));

            facts.Tales.Should().HaveCount(FolkTalePolicy.MaxTalesKnown);
            facts.Tales.Count(t => t.Title.StartsWith("Saint")).Should().BeGreaterThanOrEqualTo(2);
            facts.Tales.Count(t => t.Title.StartsWith("Tale")).Should().BeGreaterThanOrEqualTo(2);
        }

        // STAKE: a character of a people with no file (a minor culture) can still carry the tales another mod gives him,
        // and love one best.
        [Test]
        public void GIVEN_only_outside_tales_WHEN_composed_THEN_they_are_carried_and_one_can_be_told()
        {
            FolkloreFacts facts = FolkTalePolicy.Compose("lord_x", new List<FolkTale>(), Saints(2));

            facts.Tales.Select(t => t.Title).Should().BeEquivalentTo("Saint 1", "Saint 2");
            facts.Tales[0].Beats.Should().NotBeEmpty();
        }

        // STAKE: a tale both a file and a mod give is told once, never named twice in the prompt.
        [Test]
        public void GIVEN_an_outside_tale_their_people_already_tells_WHEN_composed_THEN_it_is_carried_once()
        {
            var twin = new List<FolkTale> {new FolkTale {Title = "tale 1", Gist = "the same tale.", Beats = new List<string> {"b"}}};

            FolkTalePolicy.Compose("lord_x", Many(2), twin).Tales
                .Count(t => t.Title.Equals("Tale 1", System.StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        }
    }
}
