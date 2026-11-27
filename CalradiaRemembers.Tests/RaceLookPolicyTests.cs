// Code written by Gabriel Mailhot, 26/11/2026.
// RaceLookPolicy: races.json names the people a character belongs to, adds the race's own line, and silences the facts a
// face reads wrongly for that race. CatWithFourEyes (Discord, 26/11/2026) asked for non-human races of overhaul mods
// (LOTR, GoT, Forgotten Realms conversions); Gabriel (26/11/2026) chose a file per race. If these rules are wrong, an elf
// is still "a man", a green orc is "dark-skinned", or a stock game with no entry reads differently than before.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RaceLookPolicyTests
    {
        private static AppearanceFacts Woman() => new AppearanceFacts {
            IsFemale = true, AgeYears = 30, HeightNormalized = 0.9, BuildNormalized = 0.5, WeightNormalized = 0.5,
            HairColorDescriptor = "black", SkinToneDescriptor = "dark-skinned", EyeColorDescriptor = "green"
        };

        // STAKE: CatWithFourEyes' elf must be called an elf, not "a tall woman".
        [Test]
        public void GIVEN_a_race_with_a_noun_WHEN_composed_THEN_the_people_is_named_with_the_right_article()
        {
            RaceLook elf = new RaceLook {Noun = "elf"};

            AppearanceComposer.Compose(RaceLookPolicy.Apply(Woman(), elf), null)
                              .Should().StartWith("A tall, dark-skinned elf woman in her prime");
            var averageMan = new AppearanceFacts {IsFemale = false, HeightNormalized = 0.5, BuildNormalized = 0.5, WeightNormalized = 0.5};
            AppearanceComposer.Compose(RaceLookPolicy.Apply(averageMan, elf), null, AppearanceVoice.Self)
                              .Should().Be("You are an elf man.");
        }

        // STAKE: an author who wants another word ("she-orc") gets exactly that word.
        [Test]
        public void GIVEN_a_whole_noun_for_one_sex_WHEN_named_THEN_it_wins_over_the_people_noun()
        {
            var orc = new RaceLook {Noun = "orc", FemaleNoun = "she-orc"};

            RaceLookPolicy.KindNoun(orc, true).Should().Be("she-orc");
            RaceLookPolicy.KindNoun(orc, false).Should().Be("orc man");
        }

        // STAKE: the game reads an orc's green face as "dark-skinned"; an omitted fact is left unsaid.
        [Test]
        public void GIVEN_omitted_facts_WHEN_composed_THEN_they_are_left_unsaid_and_the_race_line_follows()
        {
            var orc = new RaceLook {Noun = "orc", Omit = new List<string> {"Skin", "eyes", "height"}, Line = "Tusks jut from the lower jaw."};

            string text = AppearanceComposer.Compose(RaceLookPolicy.Apply(Woman(), orc), "A scar crosses the brow.");

            text.Should().Be("An orc woman in her prime, with black hair. Tusks jut from the lower jaw. A scar crosses the brow.");
        }

        // STAKE: a stock game, or a race the file does not name, must read exactly as before.
        [Test]
        public void GIVEN_no_entry_for_the_race_WHEN_applied_THEN_the_description_is_unchanged()
        {
            var table = new Dictionary<string, RaceLook> {["Elf"] = new RaceLook {Noun = "elf"}};
            AppearanceFacts facts = Woman();

            RaceLookPolicy.Find(table, "human").Should().BeNull();
            RaceLookPolicy.Apply(facts, RaceLookPolicy.Find(table, "human")).Should().BeSameAs(facts);
            AppearanceComposer.Compose(facts, null).Should().StartWith("A tall, dark-skinned woman");
        }

        // STAKE: the game's race name and the file's key may differ in case; the elf is still found.
        [Test]
        public void GIVEN_a_key_in_another_case_WHEN_found_THEN_it_matches()
        {
            var table = new Dictionary<string, RaceLook> {["Elf"] = new RaceLook {Noun = "elf"}};

            RaceLookPolicy.Find(table, "elf").Should().NotBeNull();
        }
    }
}
