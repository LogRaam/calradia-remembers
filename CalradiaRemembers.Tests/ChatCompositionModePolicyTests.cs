// Code written by Gabriel Mailhot, 16/08/2026.
// Documents how the "Chat Composition" MCM dropdown string maps to the ChatCompositionMode enum (Phase 2): the
// cheap single-call STANDARD, or the full ENHANCED pipeline (PROSE + INTERPRET + NSFW fallback), which is the
// default.
//
// WHY IT MATTERS: ENHANCED is the richest experience and the shipped default, so anything blank or unrecognised
// must degrade to it, not to the cheaper Standard. The legacy labels "Integrated" and "Prose + Interpreter" must
// still map to Standard and Enhanced so a config upgraded from the old two-mode era keeps its intent.

#region

using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Logic.Providers;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ChatCompositionModePolicyTests
    {
        // The cheap single-call mode is opt-in; its exact label must resolve to Standard.
        [Test]
        public void GIVEN_the_standard_label_WHEN_parsing_THEN_standard_is_returned()
        {
            ChatCompositionModePolicy.Parse(ChatCompositionModePolicy.StandardLabel)
                .Should().Be(ChatCompositionMode.Standard);
        }

        // The full pipeline label resolves to Enhanced.
        [Test]
        public void GIVEN_the_enhanced_label_WHEN_parsing_THEN_enhanced_is_returned()
        {
            ChatCompositionModePolicy.Parse(ChatCompositionModePolicy.EnhancedLabel)
                .Should().Be(ChatCompositionMode.Enhanced);
        }

        // A padded/recased Standard value must survive rather than silently falling to the default Enhanced.
        [Test]
        public void GIVEN_a_padded_or_recased_standard_value_WHEN_parsing_THEN_standard_is_still_returned()
        {
            ChatCompositionModePolicy.Parse("  standard  ").Should().Be(ChatCompositionMode.Standard);
        }

        // Migration: a config from the old two-mode dropdown must keep its intent - "Integrated" was the single
        // call (Standard) and "Prose + Interpreter" was the full pipeline (Enhanced).
        [Test]
        public void GIVEN_a_legacy_label_WHEN_parsing_THEN_it_maps_to_the_matching_new_mode()
        {
            ChatCompositionModePolicy.Parse("Integrated").Should().Be(ChatCompositionMode.Standard);
            ChatCompositionModePolicy.Parse("Prose + Interpreter").Should().Be(ChatCompositionMode.Enhanced);
        }

        // Enhanced is the shipped default: a blank, null, or unrecognised value must land on it (the richest
        // experience), never on the cheaper Standard behind the player's back.
        [Test]
        public void GIVEN_a_blank_null_or_unknown_value_WHEN_parsing_THEN_it_degrades_to_enhanced()
        {
            ChatCompositionModePolicy.Parse(null).Should().Be(ChatCompositionMode.Enhanced);
            ChatCompositionModePolicy.Parse("").Should().Be(ChatCompositionMode.Enhanced);
            ChatCompositionModePolicy.Parse("   ").Should().Be(ChatCompositionMode.Enhanced);
            ChatCompositionModePolicy.Parse("something else").Should().Be(ChatCompositionMode.Enhanced);
        }

        // STAKE: Gabriel (28/11/2026): Dramaturge is "DRAMA+PROSE+INTERPRET+NSFW", Enhanced plus. Read as anything but
        // Enhanced, every test of Enhanced in the chat (the interpreter, the NSFW fallback) would silently switch off.
        [Test]
        public void GIVEN_the_dramaturge_label_WHEN_parsing_THEN_it_composes_as_enhanced()
        {
            ChatCompositionModePolicy.Parse(ChatCompositionModePolicy.DramaturgeLabel).Should().Be(ChatCompositionMode.Enhanced);
            ChatCompositionModePolicy.Parse(" dramaturge ").Should().Be(ChatCompositionMode.Enhanced);
        }

        // STAKE: only the Dramaturge choice may run the per-turn machinery (two or three paid calls a turn, not
        // A/B-validated); an old Standard or Enhanced setting, or a blank one, must never start it.
        [Test]
        public void GIVEN_each_choice_WHEN_asking_whether_it_is_dramaturge_THEN_only_the_dramaturge_label_is()
        {
            ChatCompositionModePolicy.IsDramaturge("Dramaturge").Should().BeTrue();
            ChatCompositionModePolicy.IsDramaturge("  DRAMATURGE ").Should().BeTrue();
            ChatCompositionModePolicy.IsDramaturge("Enhanced").Should().BeFalse();
            ChatCompositionModePolicy.IsDramaturge("Standard").Should().BeFalse();
            ChatCompositionModePolicy.IsDramaturge(null).Should().BeFalse();
            ChatCompositionModePolicy.IsDramaturge("").Should().BeFalse();
        }
    }
}
