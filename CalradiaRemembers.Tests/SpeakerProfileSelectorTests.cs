// Code written by Gabriel Mailhot, 11/07/2026.
//
// WHY IT MATTERS: this is the exact fix for a reported bug ("my captor thinks he's my companion Fercread"
// / "Garios thinks he's Fercread", CHANGELOG + ChatViewModel.MainSpeaker) where a captor's dialogue and
// memories bled from the LAST companion the player had chatted with, because the old code consulted the
// session-lifetime DialogFlow.Profile FIRST. Select() feeds ChatViewModel.MainSpeaker() every turn, so
// whichever profile it returns is the one the LLM prompt is built from AND the one any [EVENT]/relation
// change gets written back onto. Get the precedence wrong and a scene's events land on the wrong person's
// memory, permanently.

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

        // The top-priority rule from the production header: a resolved hero profile must win over every
        // cache, no exception. This is the fix itself for a hero WITH an already-persisted profile: without
        // it, a stale mode/last-conversation cache could still shadow the live one.
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

        // The reported-bug case itself, for a captor with NO persisted profile yet (a synthetic bandit):
        // must use the captive-encounter profile, never fall through to the stale last-conversation
        // profile the pre-fix code returned.
        [Test]
        public void Given_a_heroless_captive_encounter_When_selecting_Then_the_captive_profile_wins_not_the_last_conversation()
        {
            // The reported bug: a captor scene was fed the last companion chatted with. A heroless (bandit) captor
            // must use the captive-encounter profile, never the stale last-conversation profile.
            SpeakerProfileSelector.Select<string>(
                null!,
                false, Audience,
                true, Captive,
                false, Captor,
                LastConversation).Should().Be(Captive);
        }

        // Pins the second fallback rung: a heroless captor SCENE (distinct from a captive ENCOUNTER, per
        // the documented order audience > captive > captor > last-conversation) resolves to its own
        // profile, not the captive-encounter or last-conversation ones.
        [Test]
        public void Given_a_heroless_captor_scene_When_selecting_Then_the_captor_scene_profile_is_used()
        {
            SpeakerProfileSelector.Select<string>(
                null!,
                false, Audience,
                false, Captive,
                true, Captor,
                LastConversation).Should().Be(Captor);
        }

        // Audience is documented as the FIRST fallback rung, ahead of captive/captor. Pins that when
        // several mode flags are simultaneously true, the precedence order is honored rather than the
        // more scene-specific-looking captive/captor mode winning.
        [Test]
        public void Given_an_audience_When_selecting_Then_the_audience_profile_outranks_the_other_modes()
        {
            SpeakerProfileSelector.Select<string>(
                null!,
                true, Audience,
                true, Captive,
                true, Captor,
                LastConversation).Should().Be(Audience);
        }

        // The ordinary chat path: a hero with no persisted profile yet (first meeting) and no special mode
        // active must still fall through to the last-conversation profile rather than return null, keeping
        // an everyday conversation working exactly as it did before this fix.
        [Test]
        public void Given_an_ordinary_conversation_with_no_hero_profile_yet_When_selecting_Then_the_last_conversation_profile_is_the_final_fallback()
        {
            SpeakerProfileSelector.Select<string>(
                null!,
                false, Audience,
                false, Captive,
                false, Captor,
                LastConversation).Should().Be(LastConversation);
        }
    }
}
