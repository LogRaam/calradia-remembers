// Code written by Gabriel Mailhot, 11/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SpeakerProfileSelectorTests
    {
        private const string HeroOwn = "hero-own";
        private const string Audience = "audience";
        private const string Captive = "captive";
        private const string Captor = "captor";
        private const string LastConversation = "last-conversation";

        [Test]
        public void Given_the_speaker_hero_has_their_own_profile_When_selecting_Then_it_always_wins_over_every_cache()
        {
            // Even in a captive encounter with a stale last-conversation profile present, the captor's own profile is used.
            SpeakerProfileSelector.Select(
                HeroOwn,
                true, Audience,
                true, Captive,
                true, Captor,
                LastConversation).Should().Be(HeroOwn);
        }

        [Test]
        public void Given_a_heroless_captive_encounter_When_selecting_Then_the_captive_profile_wins_not_the_last_conversation()
        {
            // The reported bug: a captor scene was fed the last companion chatted with. A heroless (bandit) captor
            // must use the captive-encounter profile, never the stale last-conversation profile.
            SpeakerProfileSelector.Select<string>(
                null,
                false, Audience,
                true, Captive,
                false, Captor,
                LastConversation).Should().Be(Captive);
        }

        [Test]
        public void Given_a_heroless_captor_scene_When_selecting_Then_the_captor_scene_profile_is_used()
        {
            SpeakerProfileSelector.Select<string>(
                null,
                false, Audience,
                false, Captive,
                true, Captor,
                LastConversation).Should().Be(Captor);
        }

        [Test]
        public void Given_an_audience_When_selecting_Then_the_audience_profile_outranks_the_other_modes()
        {
            SpeakerProfileSelector.Select<string>(
                null,
                true, Audience,
                true, Captive,
                true, Captor,
                LastConversation).Should().Be(Audience);
        }

        [Test]
        public void Given_an_ordinary_conversation_with_no_hero_profile_yet_When_selecting_Then_the_last_conversation_profile_is_the_final_fallback()
        {
            SpeakerProfileSelector.Select<string>(
                null,
                false, Audience,
                false, Captive,
                false, Captor,
                LastConversation).Should().Be(LastConversation);
        }
    }
}
