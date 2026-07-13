// Code written by Gabriel Mailhot, 05/07/2026.
//
// WHY IT MATTERS: every chat line's portrait is a real GPU render-target (a character tableau), created
// per line with no engine-side pooling. Left unmanaged, a long scene accumulates dozens of live
// render-targets and exhausts VRAM (the "create_render_texture: Not enough memory resources" crash,
// CHANGELOG v1.23.0). This policy is what keeps live portraits bounded to the participant count instead
// of the transcript length: too little released and VRAM still climbs; one released too many leaves a
// speaker's line blank. Consumed by PortraitTextureGuard.EnforceOnePerParticipant / ReleaseAll, called on
// every new chat line and on window close.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PortraitCapPolicyTests
    {
        // The floor: with no duplicate speaker, nothing should ever be released. Guards against an
        // off-by-one that blanks an active speaker's own, only, portrait mid-scene.
        [Test]
        public void GIVEN_one_line_per_speaker_WHEN_capping_THEN_nothing_is_released()
        {
            List<IManagedPortrait> lines = new List<IManagedPortrait>
            {
                new FakePortrait("lord"), new FakePortrait("witness"), new FakePortrait("player")
            };

            PortraitCapPolicy.KeepOnePerParticipant(lines).Should().Be(0);
            lines.Count(l => l.HasLivePortrait).Should().Be(3);
        }

        // The core rule and the actual leak scenario: a speaker with several lines is exactly what
        // exhausted VRAM before this policy existed. Confirms the NEWEST line keeps its portrait, never
        // the oldest, so the line currently visible to the player is never the one blanked.
        [Test]
        public void GIVEN_a_speaker_who_spoke_three_times_WHEN_capping_THEN_only_their_newest_line_keeps_its_portrait()
        {
            FakePortrait oldest = new FakePortrait("wick");
            FakePortrait middle = new FakePortrait("wick");
            FakePortrait newest = new FakePortrait("wick");
            List<IManagedPortrait> lines = new List<IManagedPortrait> {oldest, middle, newest};

            PortraitCapPolicy.KeepOnePerParticipant(lines).Should().Be(2);

            oldest.HasLivePortrait.Should().BeFalse();
            middle.HasLivePortrait.Should().BeFalse();
            newest.HasLivePortrait.Should().BeTrue(); // the most recent line is the one kept
        }

        // The multi-participant shape of the real crash (a witness round-table, several speakers each
        // repeating): confirms one-per-key holds SIMULTANEOUSLY across distinct keys, not just for a
        // single speaker in isolation.
        [Test]
        public void GIVEN_several_participants_each_repeating_WHEN_capping_THEN_exactly_one_live_portrait_remains_per_participant()
        {
            List<IManagedPortrait> lines = new List<IManagedPortrait>
            {
                new FakePortrait("A"), new FakePortrait("B"), new FakePortrait("A"),
                new FakePortrait("B"), new FakePortrait("C")
            };

            PortraitCapPolicy.KeepOnePerParticipant(lines).Should().Be(2);

            lines.Where(l => l.HasLivePortrait).Select(l => l.PortraitKey)
                .Should().BeEquivalentTo("A", "B", "C"); // one live per distinct participant, no more
        }

        // A portraitless system line shares the same transcript list; it must never be miscounted as a
        // participant slot or have ReleasePortrait invoked on an already-inert line.
        [Test]
        public void GIVEN_portraitless_system_lines_interleaved_WHEN_capping_THEN_they_are_ignored()
        {
            FakePortrait systemLine = new FakePortrait("", live: false);
            FakePortrait olderWick = new FakePortrait("wick");
            FakePortrait newerWick = new FakePortrait("wick");
            List<IManagedPortrait> lines = new List<IManagedPortrait> {olderWick, systemLine, newerWick};

            PortraitCapPolicy.KeepOnePerParticipant(lines).Should().Be(1);

            systemLine.ReleaseCount.Should().Be(0); // a line with no live portrait is never touched
            olderWick.HasLivePortrait.Should().BeFalse();
            newerWick.HasLivePortrait.Should().BeTrue();
        }

        // PortraitTextureGuard calls KeepOnePerParticipant on every new line, so it must be safe to run
        // repeatedly on an already-capped list without over-releasing (double-freeing a texture) or erroring.
        [Test]
        public void GIVEN_a_capped_transcript_WHEN_capping_again_THEN_nothing_more_is_released()
        {
            List<IManagedPortrait> lines = new List<IManagedPortrait>
            {
                new FakePortrait("wick"), new FakePortrait("wick"), new FakePortrait("wick")
            };

            PortraitCapPolicy.KeepOnePerParticipant(lines).Should().Be(2);
            PortraitCapPolicy.KeepOnePerParticipant(lines).Should().Be(0); // idempotent
        }

        // Reproduces the actual crash shape (a long transcript, a small bounded cast) and checks the
        // invariant holds CONTINUOUSLY, after every single line, not just at one snapshot.
        [Test]
        public void GIVEN_a_long_scene_from_a_bounded_cast_WHEN_capping_after_each_line_THEN_live_portraits_never_exceed_the_participant_count()
        {
            string[] cast = {"lord", "witness1", "witness2", "companion1", "companion2", "player"};
            List<IManagedPortrait> lines = new List<IManagedPortrait>();

            // 200 lines round-robin from a 6-strong cast: the transcript is long, the live portraits are not.
            for (int turn = 0; turn < 200; turn++)
            {
                lines.Add(new FakePortrait(cast[turn % cast.Length]));
                PortraitCapPolicy.KeepOnePerParticipant(lines);
                lines.Count(l => l.HasLivePortrait).Should().BeLessThanOrEqualTo(cast.Length);
            }
        }

        // Window teardown must free every live render-target, or a closed chat leaks VRAM forward into the
        // next scene, compounding across sessions.
        [Test]
        public void GIVEN_a_window_closing_WHEN_releasing_all_THEN_every_live_portrait_is_freed()
        {
            List<IManagedPortrait> lines = new List<IManagedPortrait>
            {
                new FakePortrait("lord"), new FakePortrait("", live: false), new FakePortrait("player")
            };

            PortraitCapPolicy.ReleaseAll(lines).Should().Be(2); // the two live ones; the portraitless line is skipped
            lines.Any(l => l.HasLivePortrait).Should().BeFalse();
        }

        // Defensive fail-safe: a null list (e.g. a window's message list not yet initialized) must degrade
        // to a no-op rather than throw, since PortraitTextureGuard calls these unconditionally.
        [Test]
        public void GIVEN_a_null_list_WHEN_capping_or_releasing_THEN_it_is_a_no_op()
        {
            PortraitCapPolicy.KeepOnePerParticipant(null).Should().Be(0);
            PortraitCapPolicy.ReleaseAll(null).Should().Be(0);
        }

        private sealed class FakePortrait : IManagedPortrait
        {
            public FakePortrait(string key, bool live = true)
            {
                PortraitKey = key;
                HasLivePortrait = live;
            }

            public int ReleaseCount { get; private set; }
            public string PortraitKey { get; }
            public bool HasLivePortrait { get; private set; }

            public void ReleasePortrait()
            {
                ReleaseCount++;
                HasLivePortrait = false;
            }
        }
    }
}
