// Code written by Gabriel Mailhot, 22/06/2026.
// Guards player-authored character data: traits clamp to the engine band and unset ones are left alone,
// and resolution never hands one hero another's authored personality (id wins; an id-less homonym is
// refused, not guessed).
//
// WHY IT MATTERS: this is the moddability surface (character_overrides.json, hand-edited by a non-programmer)
// that CharacterOverrideLoader reads every turn to feed Hero.SetTraitLevel (traits, derived nature, the campaign
// AI) and the live prompt (backstory, orientation). A wrong Resolve() hands one lord's authored persona to
// another lord entirely; an unclamped or falsely-authored value leaks a value the player never actually set.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CharacterOverrideTests
    {
        // ── Trait clamping / selection ──────────────────────────────────────

        // Hero.SetTraitLevel clamps to −2..+2 too, but ClampedTraitLevels is what the mod's own derived-nature
        // and prompt logic actually reads. A hand-typed JSON value like "5" is easy for a non-programmer author
        // to write; if this clamp drifted, that raw out-of-band value would leak into logic the engine's own
        // clamp never touches.
        [Test]
        public void GIVEN_out_of_band_traits_WHEN_clamping_THEN_each_is_pinned_to_minus2_to_plus2()
        {
            var o = new CharacterOverride { Valor = 5, Honor = -9, Mercy = 1 };
            var levels = o.ClampedTraitLevels().ToDictionary(x => x.Trait, x => x.Level);

            levels[AuthoredTrait.Valor].Should().Be(2);
            levels[AuthoredTrait.Honor].Should().Be(-2);
            levels[AuthoredTrait.Mercy].Should().Be(1);
        }

        // ── Authored-content gate (cr.reload_lore skips only truly-empty entries) ──

        // THE player report: a character defined with ONLY a backstory (no traits, no "kinks" entry) was skipped by
        // cr.reload_lore because its apply loop counted only traits and kinks as "content". A backstory is authored
        // content; the loop must visit the character.
        [Test]
        public void GIVEN_only_a_backstory_WHEN_checking_content_THEN_it_counts_as_authored()
        {
            new CharacterOverride { Hero = "X", Backstory = "A quiet cooper's son." }
                .HasAnyAuthoredContent.Should().BeTrue();
        }

        // A conviction is authored content too (it reaches the prompt as a motive), so a conviction-only override is
        // likewise never treated as empty.
        [Test]
        public void GIVEN_only_a_conviction_WHEN_checking_content_THEN_it_counts_as_authored()
        {
            new CharacterOverride { Hero = "X", Conviction = "He believes he pulls the realm's strings." }
                .HasAnyAuthoredContent.Should().BeTrue();
        }

        // Traits-only and an explicit kink lock (even the empty-list lock) stay content, so the reload loop keeps
        // visiting them exactly as before this fix.
        [Test]
        public void GIVEN_only_traits_or_a_kink_lock_WHEN_checking_content_THEN_it_counts_as_authored()
        {
            new CharacterOverride { Hero = "X", Honor = 2 }.HasAnyAuthoredContent.Should().BeTrue();
            new CharacterOverride { Hero = "X", Kinks = new List<string>() }.HasAnyAuthoredContent.Should().BeTrue();
        }

        // A truly-empty entry (a name and nothing authored) is NOT content: the reload loop still skips it, so this
        // fix never turns a blank entry into a processed character.
        [Test]
        public void GIVEN_an_entry_with_no_authored_fields_WHEN_checking_content_THEN_it_is_not_authored()
        {
            new CharacterOverride { Hero = "X" }.HasAnyAuthoredContent.Should().BeFalse();
        }

        // The remaining hole behind the same player report: an entry whose ONLY field is a MISSPELLED orientation
        // parsed to nothing, so the entry counted as empty and was skipped in silence, which reads to the author
        // exactly like the original bug ("my character is ignored until I add kinks"). Orientation TEXT is authored
        // content whether or not the parser knows the word.
        [Test]
        public void GIVEN_only_a_misspelled_orientation_WHEN_checking_content_THEN_it_still_counts_as_authored()
        {
            new CharacterOverride { Hero = "X", Orientation = "lesbain" }.HasAnyAuthoredContent.Should().BeTrue();
        }

        // ── Authoring problems (never fail silently on a hand-edited file) ──

        // The misspelled orientation must be REPORTED, not just counted: the author needs to know the word was
        // ignored, or they will keep believing the file is broken rather than the spelling.
        [Test]
        public void GIVEN_a_misspelled_orientation_WHEN_listing_problems_THEN_the_offending_word_is_named()
        {
            var o = new CharacterOverride { Hero = "X", Orientation = "lesbain" };

            o.HasUnrecognizedOrientation.Should().BeTrue();
            string.Join(" ", o.AuthoringProblems()).Should().Contain("lesbain");
        }

        // A mistyped kink is silently dropped by the forgiving parser, which means an authored lock of nothing but
        // typos pins the NPC to ZERO kinks while looking deliberate. The typo must be named.
        [Test]
        public void GIVEN_a_mistyped_kink_WHEN_listing_problems_THEN_the_offending_name_is_reported()
        {
            var o = new CharacterOverride { Hero = "X", Kinks = new List<string> { "Bondge" } };

            o.UnrecognizedKinkNames.Should().ContainSingle().Which.Should().Be("Bondge");
            string.Join(" ", o.AuthoringProblems()).Should().Contain("Bondge");
        }

        // A clean entry reports nothing, so the diagnostic stays quiet for a correct file and a warning always
        // means something real.
        [Test]
        public void GIVEN_a_clean_entry_WHEN_listing_problems_THEN_there_are_none()
        {
            var o = new CharacterOverride { Hero = "X", Orientation = "bi", Backstory = "A cooper's son." };

            o.AuthoringProblems().Should().BeEmpty();
            o.HasUnrecognizedOrientation.Should().BeFalse();
        }

        // Only traits the author actually set may reach Hero.SetTraitLevel. If an unset (null) trait were
        // listed anyway, every authored NPC would have their untouched, vanilla-rolled traits silently forced
        // to a default, erasing personality diversity the player never asked to touch.
        [Test]
        public void GIVEN_unset_traits_WHEN_listing_THEN_only_the_authored_ones_appear()
        {
            var o = new CharacterOverride { Honor = 2 }; // the rest stay vanilla
            o.ClampedTraitLevels().Select(x => x.Trait).Should().Equal(AuthoredTrait.Honor);
            o.HasTraits.Should().BeTrue();
        }

        // An override authored for backstory alone (no traits) must not touch the NPC's personality at all.
        // HasTraits is the gate the mod checks before calling SetTraitLevel; a false positive here would force
        // trait values onto a hero the author never meant to touch.
        [Test]
        public void GIVEN_no_traits_WHEN_asked_THEN_HasTraits_is_false_and_the_list_is_empty()
        {
            var o = new CharacterOverride { Backstory = "A quiet exile." };
            o.HasTraits.Should().BeFalse();
            o.ClampedTraitLevels().Should().BeEmpty();
            o.HasBackstory.Should().BeTrue();
        }

        // CharacterOverrideLoader.BackstoryFor only forwards the string to the live prompt when HasBackstory is
        // true. A whitespace-only value (an author who left the JSON's example field blank) must read as
        // unauthored, or the prompt would inject an empty backstory section instead of skipping it.
        [Test]
        public void GIVEN_blank_backstory_WHEN_asked_THEN_HasBackstory_is_false()
        {
            new CharacterOverride { Backstory = "   " }.HasBackstory.Should().BeFalse();
        }

        // Same contract for the conviction, and the same trap: CharacterOverrideLoader.ConvictionFor gates on
        // HasConviction, and an empty section in the prompt is worse than no section (it spends tokens teaching
        // the model that the character holds nothing in particular to be true).
        [Test]
        public void GIVEN_blank_conviction_WHEN_asked_THEN_HasConviction_is_false()
        {
            new CharacterOverride { Conviction = "   " }.HasConviction.Should().BeFalse();
            new CharacterOverride().HasConviction.Should().BeFalse();
        }

        // Backstory and conviction are two DIFFERENT levers and must be independently authorable: the backstory
        // colours only HOW a character speaks (the prompt says so in as many words: not what they decide), while
        // the conviction moves what they WANT and pursue. A player may want one without the other, so neither
        // flag may be inferred from the other.
        [Test]
        public void GIVEN_a_conviction_and_no_backstory_WHEN_asked_THEN_only_HasConviction_is_true()
        {
            var o = new CharacterOverride { Conviction = "He believes the player's clan means to unseat him." };

            o.HasConviction.Should().BeTrue();
            o.HasBackstory.Should().BeFalse();
        }

        // ── Authored kinks (ParsedKinks) ─────────────────────────────────────

        // The whole point of the field: CharacterLoreBehavior and the per-turn chat sync both need to tell
        // "author left kinks out" (null → keep the rolled/frozen kinks) apart from "author locked to
        // exactly this list". If Kinks==null ever parsed to an empty list instead of null, a lock to "no
        // kinks at all" would become indistinguishable from no lock, and the reload path would silently
        // keep re-rolling from traits for an NPC the author meant to leave kink-less.
        [Test]
        public void GIVEN_kinks_omitted_WHEN_parsing_THEN_ParsedKinks_is_null()
        {
            new CharacterOverride { Hero = "Derthert" }.ParsedKinks.Should().BeNull();
        }

        // The other half of the same contract: a present-but-empty JSON array ("kinks": []) is a deliberate
        // lock to NO kinks, and must parse to an empty (non-null) list so the caller applies it (clears the
        // NPC's kinks) rather than treating it as "field not set" and leaving the rolled kinks alone.
        [Test]
        public void GIVEN_an_empty_kinks_list_WHEN_parsing_THEN_ParsedKinks_is_an_empty_non_null_list()
        {
            var o = new CharacterOverride { Hero = "Derthert", Kinks = new List<string>() };

            o.ParsedKinks.Should().NotBeNull();
            o.ParsedKinks.Should().BeEmpty();
        }

        // The names a player types are the enum's own member names (what cr.kinks lists as valid), authored
        // by hand in JSON, same contract as ParsedOrientation, routed through KinkParser so unknown/garbage
        // entries degrade quietly instead of corrupting the whole override.
        [Test]
        public void GIVEN_valid_kink_names_WHEN_parsing_THEN_ParsedKinks_maps_them_in_order()
        {
            var o = new CharacterOverride { Hero = "Derthert", Kinks = new List<string> { "Dominance", "Praise" } };

            o.ParsedKinks.Should().Equal(Kink.Dominance, Kink.Praise);
        }

        // ── Resolution ──────────────────────────────────────────────────────

        // The rule that "id wins" is the one thing standing between a typo and a wrong hero getting someone
        // else's authored persona. Two authored entries can plausibly share a display name; without id taking
        // priority, name matching alone could resolve to the wrong lord even when an id was actually supplied.
        [Test]
        public void GIVEN_an_id_match_WHEN_resolving_THEN_id_wins_even_if_the_name_differs()
        {
            var set = new CharacterOverrideSet(new[]
            {
                new CharacterOverride { Hero = "Someone Else", Id = "lord_1", Honor = 2 },
                new CharacterOverride { Hero = "Derthert", Valor = 1 }
            });

            set.Resolve(heroId: "lord_1", heroName: "Derthert").Hero.Should().Be("Someone Else");
        }

        // tashmetu (Nexus, 26/09/2026): a mercenary captain's id and name are rolled per campaign, but his template is
        // not. An entry keyed on the template applies in every game, even where the rolled name matches nothing.
        [Test]
        public void GIVEN_an_entry_keyed_on_the_template_WHEN_a_rolled_hero_is_resolved_THEN_it_applies()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Id = "spc_skolderbrotva_leader_0", Valor = 2 } });

            set.Resolve(heroId: "CharacterObject_2526", heroName: "Yahya of the Beni Zilal", templateId: "spc_skolderbrotva_leader_0")
               .Should().NotBeNull();
        }

        // The hero's own id is still the unambiguous key: it wins over an entry for the template he came from.
        [Test]
        public void GIVEN_entries_for_both_the_hero_and_his_template_WHEN_resolving_THEN_the_heros_own_wins()
        {
            var set = new CharacterOverrideSet(new[]
            {
                new CharacterOverride { Id = "spc_skolderbrotva_leader_0", Hero = "Template entry" },
                new CharacterOverride { Id = "CharacterObject_2526", Hero = "Hero entry" }
            });

            set.Resolve("CharacterObject_2526", "Yahya", "spc_skolderbrotva_leader_0").Hero.Should().Be("Hero entry");
        }

        // Most hand-authored entries will never carry an id (the file's basic example doesn't ask for one), so
        // plain name matching has to work on its own, and case-insensitively: an author typing "derthert" for
        // "Derthert" should not silently fail to apply the override they just wrote.
        [Test]
        public void GIVEN_no_id_match_WHEN_resolving_THEN_a_single_name_match_is_used()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Hero = "Derthert", Valor = 1 } });
            set.Resolve(heroId: "lord_99", heroName: "derthert").Should().NotBeNull(); // case-insensitive
        }

        // Most heroes in a campaign have no authored entry at all, which is the normal case, not an error. A
        // miss has to fall through to null (and vanilla behavior) cleanly, never throw or fabricate a default
        // override for a hero the author never wrote about.
        [Test]
        public void GIVEN_no_match_at_all_WHEN_resolving_THEN_it_returns_null()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Hero = "Derthert" } });
            set.Resolve("x", "Caladog").Should().BeNull();
        }

        // Two hand-authored characters can innocently share a display name. Guessing which one applies would
        // silently hand one lord's traits or backstory to the wrong lord; refusing (null) instead is the signal
        // that tells the author to add an id, per CharacterOverrideSet.Resolve's own contract.
        [Test]
        public void GIVEN_two_entries_with_the_same_name_and_no_id_WHEN_resolving_THEN_it_refuses_rather_than_guess()
        {
            var set = new CharacterOverrideSet(new[]
            {
                new CharacterOverride { Hero = "Aria", Honor = 2 },
                new CharacterOverride { Hero = "Aria", Honor = -2 }
            });

            set.Resolve(heroId: null, heroName: "Aria").Should().BeNull();
        }

        // Confirms the refuse-on-ambiguity rule above doesn't over-refuse: once the author actually adds ids to
        // tell two same-named characters apart, resolution must use them correctly, not keep returning null.
        [Test]
        public void GIVEN_homonyms_disambiguated_by_id_WHEN_resolving_THEN_the_right_one_wins()
        {
            var set = new CharacterOverrideSet(new[]
            {
                new CharacterOverride { Hero = "Aria", Id = "aria_north", Honor = 2 },
                new CharacterOverride { Hero = "Aria", Id = "aria_south", Honor = -2 }
            });

            set.Resolve(heroId: "aria_south", heroName: "Aria").Honor.Should().Be(-2);
        }

        // cr.lore_status (the diagnostic the player-feedback loop needs) enumerates every authored entry to
        // report which ones matched a live hero and which didn't. If All ever returned an empty or wrong
        // list, that diagnostic would tell a player their file loaded nothing when it actually loaded fine.
        [Test]
        public void GIVEN_a_set_with_entries_WHEN_reading_All_THEN_the_constructed_entries_are_returned()
        {
            var derthert = new CharacterOverride { Hero = "Derthert", Valor = 1 };
            var aria = new CharacterOverride { Hero = "Aria", Id = "aria_north", Honor = 2 };
            var set = new CharacterOverrideSet(new[] { derthert, aria });

            set.All.Should().BeEquivalentTo(new[] { derthert, aria });
        }

        // ── Runtime injection (With): the console backstory-test path ───────────

        // This is what cr.backstory_test relies on: injecting a strong named persona onto a hero at runtime,
        // without touching character_overrides.json, so the authored-backstory path can be checked end to end
        // in one command. If the injected entry never actually resolved, that console command would prove
        // nothing.
        [Test]
        public void GIVEN_an_injected_override_WHEN_resolving_by_id_THEN_the_injected_backstory_is_returned()
        {
            var set = new CharacterOverrideSet(null)
                .With(new CharacterOverride { Id = "lord_1", Hero = "Derthert", Backstory = "A rage-forged warrior." });

            set.Resolve("lord_1", "Derthert").Backstory.Should().Be("A rage-forged warrior.");
        }

        // A live console test has to override the file-backed entry for that one NPC, not sit alongside it. If
        // it merely added a second entry instead of replacing the first, the two would become an unresolvable
        // same-id ambiguity and the console command would break the very hero it targeted.
        [Test]
        public void GIVEN_an_injection_for_a_hero_that_already_has_a_file_entry_WHEN_resolving_THEN_the_injection_wins_by_id()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Id = "lord_1", Hero = "Derthert", Backstory = "old" } })
                .With(new CharacterOverride { Id = "lord_1", Hero = "Derthert", Backstory = "new" });

            set.Resolve("lord_1", "Derthert").Backstory.Should().Be("new");
            set.Count.Should().Be(1); // replaced, not duplicated
        }

        // Injecting a one-off runtime test override must not wipe out the rest of the campaign's authored file.
        // A console command meant to test one NPC's backstory should never be able to silently blank out every
        // other player-authored character for the rest of the session.
        [Test]
        public void GIVEN_an_injection_for_one_hero_WHEN_resolving_others_THEN_the_rest_of_the_set_is_preserved()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Id = "lord_other", Hero = "Caladog", Backstory = "kept" } })
                .With(new CharacterOverride { Id = "lord_1", Hero = "Derthert", Backstory = "injected" });

            set.Resolve("lord_other", "Caladog").Backstory.Should().Be("kept");
            set.Resolve("lord_1", "Derthert").Backstory.Should().Be("injected");
        }

        // Defensive null-safety for callers of With(): InjectRuntimeOverride already guards this at the call
        // site, but the set itself must not corrupt or crash on a null addition if that guard is ever bypassed.
        [Test]
        public void GIVEN_a_null_override_WHEN_added_THEN_the_set_is_unchanged()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Id = "lord_1", Hero = "Derthert" } });
            set.With(null).Count.Should().Be(1);
        }
    }
}
