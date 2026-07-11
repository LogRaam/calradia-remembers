// Code written by Gabriel Mailhot, 11/07/2026.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Chooses which stored profile represents the NPC currently speaking, so a memory never ends up on the
    ///   wrong person. The speaker's OWN profile (resolved live from the hero the player sees) always wins: a
    ///   captor must wear his own memories, not the last companion the player happened to chat with. The
    ///   mode-specific profiles are only a fallback for a speaker with no hero (a synthetic bandit captor) or
    ///   before the hero's profile has been persisted, and the last-conversation profile is the final fallback
    ///   for an ordinary talk. Pure so the precedence, the exact fix for the "captor wears a companion's
    ///   memories" bug, stays locked by a test.
    /// </summary>
    public static class SpeakerProfileSelector
    {
        /// <summary>
        ///   The profile for the current main speaker. <paramref name="heroOwnProfile" /> (the speaker hero's own
        ///   stored profile) takes precedence over everything; only when it is null does the mode fallback apply,
        ///   in the order audience, captive encounter, captor scene, then the last ordinary conversation.
        /// </summary>
        public static T Select<T>(
            T heroOwnProfile,
            bool hasAudience, T audienceProfile,
            bool isCaptiveEncounter, T captiveProfile,
            bool isCaptorScene, T captorSceneProfile,
            T lastConversationProfile) where T : class
        {
            if (heroOwnProfile != null) return heroOwnProfile;
            if (hasAudience) return audienceProfile;
            if (isCaptiveEncounter) return captiveProfile;
            if (isCaptorScene) return captorSceneProfile;

            return lastConversationProfile;
        }
    }
}
