// Code written by Gabriel Mailhot, 05/07/2026.

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
