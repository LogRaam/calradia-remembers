// Code written by Gabriel Mailhot, 13/08/2026.
// Documents how the "Chat Composition" MCM dropdown string maps to the ChatCompositionMode enum. This decides
// whether a reply is produced in one integrated call (today's behaviour) or the two-call prose+extract path.
//
// WHY IT MATTERS: the prose+extract orchestration is not built yet, so anything the parser does NOT firmly
// recognise as "Prose + Extract" must degrade to Integrated. A stale or hand-edited config (or a future rename)
// that resolved to the unfinished path would break every conversation. The dropdown itself is built from the
// same label constants this parser matches, so the two can never drift.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ChatCompositionModePolicyTests
    {
        // The shipped default is the single-call path; a config that never touched this setting must keep it.
        [Test]
        public void GIVEN_the_integrated_label_WHEN_parsing_THEN_integrated_is_returned()
        {
            ChatCompositionModePolicy.Parse(ChatCompositionModePolicy.IntegratedLabel)
                .Should().Be(ChatCompositionMode.Integrated);
        }

        // The only value that may unlock the two-call path is the exact opt-in label; if this stops matching, a
        // player who chose Prose + Extract would silently keep the integrated path.
        [Test]
        public void GIVEN_the_prose_extract_label_WHEN_parsing_THEN_prose_extract_is_returned()
        {
            ChatCompositionModePolicy.Parse(ChatCompositionModePolicy.ProseExtractLabel)
                .Should().Be(ChatCompositionMode.ProseExtract);
        }

        // An MCM value can arrive with stray casing/spacing across MCM versions; the opt-in must survive that
        // rather than silently reverting to Integrated behind the player's back.
        [Test]
        public void GIVEN_a_padded_or_recased_prose_extract_value_WHEN_parsing_THEN_prose_extract_is_still_returned()
        {
            ChatCompositionModePolicy.Parse("  prose + extract  ").Should().Be(ChatCompositionMode.ProseExtract);
        }

        // The prose+extract orchestration does not exist yet, so a blank, null, or unrecognised value must NEVER
        // land a player on the unfinished path; it degrades to Integrated (today's behaviour).
        [Test]
        public void GIVEN_a_blank_null_or_unknown_value_WHEN_parsing_THEN_it_degrades_to_integrated()
        {
            ChatCompositionModePolicy.Parse(null).Should().Be(ChatCompositionMode.Integrated);
            ChatCompositionModePolicy.Parse("").Should().Be(ChatCompositionMode.Integrated);
            ChatCompositionModePolicy.Parse("   ").Should().Be(ChatCompositionMode.Integrated);
            ChatCompositionModePolicy.Parse("something else").Should().Be(ChatCompositionMode.Integrated);
        }
    }
}
