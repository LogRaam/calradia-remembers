// Code written by Gabriel Mailhot, 24/07/2026.
// Pins the door rules for a memory injected through the public CrNpcMemory API (Extension Surface). The facade
// resolves the hero and writes to the store; THIS is the pure normalisation a third-party mod's input passes
// through first, so it is where the contract's guarantees live.
//
// WHY IT MATTERS: an external mod (the addon author who asked for this API) hands raw strings. If the kind were
// taken at face value, a caller could inject a fake FirstMeeting or Captivity and corrupt the mod's own
// have-we-met / compression logic. If the day math were wrong, an injected memory could land in the future or
// before day 0. If an empty summary were stored, the NPC would carry a blank recollection. Each test guards one
// of those.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NpcMemoryInjectionTests
    {
        // The happy path: a valid injection becomes a storable event dated the right day with the caller's kind.
        // This is the whole point of the API, so if it did not round-trip the caller's intent the feature is inert.
        [Test]
        public void GIVEN_a_valid_injection_WHEN_built_THEN_it_carries_the_bounded_summary_kind_and_dated_day()
        {
            NotableEvent e = NpcMemoryInjection.BuildInjectedEvent("We agreed to meet at the ford at dawn.", "Agreement", 3, 100);

            e.Should().NotBeNull();
            e.summary.Should().Be("We agreed to meet at the ford at dawn.");
            e.type.Should().Be(NotableEventType.Agreement);
            e.gameDay.Should().Be(97);
        }

        // An empty or whitespace summary must be REJECTED (null), not stored: a blank memory is a slot wasted in
        // every future prompt and teaches the NPC it "remembers" nothing of substance.
        [Test]
        public void GIVEN_a_blank_summary_WHEN_built_THEN_it_is_rejected()
        {
            NpcMemoryInjection.BuildInjectedEvent("   ", "Other", 0, 100).Should().BeNull();
            NpcMemoryInjection.BuildInjectedEvent(null, "Other", 0, 100).Should().BeNull();
        }

        // The summary is the save-blob guard: an over-long injected line must be capped to the same bound a fed
        // world event gets, or a caller could bloat the persisted memory.
        [Test]
        public void GIVEN_an_over_long_summary_WHEN_built_THEN_it_is_capped_to_the_blob_safe_bound()
        {
            string huge = new string('x', ExternalEventRules.MaxSummaryChars + 200);

            NpcMemoryInjection.BuildInjectedEvent(huge, "Other", 0, 100)
                .summary.Length.Should().Be(ExternalEventRules.MaxSummaryChars);
        }

        // A recognised, caller-safe kind is honoured; anything unrecognised falls back to Other so a typo never
        // throws or silently drops the memory.
        [Test]
        public void GIVEN_a_known_or_unknown_kind_WHEN_normalised_THEN_it_maps_or_falls_back_to_Other()
        {
            NpcMemoryInjection.NormalizeKind("Flirt").Should().Be(NotableEventType.Flirt);
            NpcMemoryInjection.NormalizeKind("flirt").Should().Be(NotableEventType.Flirt); // case-insensitive
            NpcMemoryInjection.NormalizeKind("not-a-kind").Should().Be(NotableEventType.Other);
            NpcMemoryInjection.NormalizeKind(null).Should().Be(NotableEventType.Other);
        }

        // The security-shaped rule: the three engine-owned kinds must NEVER be settable from outside, or an
        // external mod could forge a FirstMeeting (breaking have-we-met), a Captivity (dodging compression), or a
        // Farewell (locking the player's input). All three are coerced to Other.
        [Test]
        public void GIVEN_an_engine_owned_kind_WHEN_normalised_THEN_it_is_coerced_to_Other()
        {
            NpcMemoryInjection.NormalizeKind("FirstMeeting").Should().Be(NotableEventType.Other);
            NpcMemoryInjection.NormalizeKind("Captivity").Should().Be(NotableEventType.Other);
            NpcMemoryInjection.NormalizeKind("Farewell").Should().Be(NotableEventType.Other);
        }

        // The day math: a memory is dated in the past, floored at day 0 (it cannot predate the world) and never
        // in the future (a negative days-ago is treated as today), or an injected memory would sort wrong in the
        // timeline the modder explicitly wants for context.
        [Test]
        public void GIVEN_various_days_ago_WHEN_resolving_the_day_THEN_it_stays_in_bounds()
        {
            NpcMemoryInjection.ResolveDay(5, 100).Should().Be(95);
            NpcMemoryInjection.ResolveDay(0, 100).Should().Be(100);
            NpcMemoryInjection.ResolveDay(-4, 100).Should().Be(100); // negative treated as today
            NpcMemoryInjection.ResolveDay(250, 100).Should().Be(0);  // cannot predate day 0
        }

        // tashmetu (01/10/2026): a lord given an embarrassing memory by another mod (Vivid World) brushed it off in
        // front of his Khan, which was right, but the injecting mod had no way to say "this was said in confidence".
        // A memory injected as private carries CR's own audience rule (NotableEvent.IsPrivate): kept to private talk.
        [Test]
        public void GIVEN_a_memory_injected_as_private_WHEN_built_THEN_it_is_kept_to_private_talk()
        {
            NpcMemoryInjection.BuildInjectedEvent("I wept at the feast.", "Other", 0, 100, isPrivate: true).IsPrivate.Should().BeTrue();
        }

        // Unmarked, an injected memory stays public as before: no existing mod changes behaviour.
        [Test]
        public void GIVEN_a_memory_injected_without_the_flag_WHEN_built_THEN_it_is_public_as_before()
        {
            NpcMemoryInjection.BuildInjectedEvent("We met at the ford.", "Agreement", 0, 100).IsPrivate.Should().BeFalse();
        }
    }
}
