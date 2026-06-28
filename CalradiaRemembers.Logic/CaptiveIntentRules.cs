// Code written by Gabriel Mailhot, 24/06/2026.
// Pure semantics of a captive-scene intent (the verb for the memory summary, and which intents carry a
// forced-body toll or a public humiliation), extracted from ChatViewModel so the classification is testable
// and not buried in UI orchestration.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Classifies a <see cref="CaptiveSceneIntent" /> for the chat flow's scene handling.</summary>
    public static class CaptiveIntentRules
    {
        /// <summary>The past-tense verb describing what the captor did, for the scene memory summary.</summary>
        public static string Verb(CaptiveSceneIntent intent) => intent switch
        {
            CaptiveSceneIntent.Interrogation  => "interrogated",
            CaptiveSceneIntent.PersonalDesire => "took my pleasure from",
            CaptiveSceneIntent.Domination     => "dominated and subdued",
            CaptiveSceneIntent.Torture        => "tortured",
            CaptiveSceneIntent.Training       => "trained and conditioned",
            CaptiveSceneIntent.Reward         => "rewarded",
            CaptiveSceneIntent.Reckoning      => "confronted as my prisoner",
            _                                 => "kept as my prisoner"
        };

        /// <summary>
        ///   Intents whose scenes involve the player being taken by force — they leave a guaranteed body
        ///   toll. Reward is gentle, Interrogation is not inherently sexual, and the bandit menace intents
        ///   handle their own violence through harm_prisoner, so none of those carry an automatic toll.
        /// </summary>
        public static bool IsForcedSexual(CaptiveSceneIntent intent)
            => intent == CaptiveSceneIntent.PersonalDesire
            || intent == CaptiveSceneIntent.Domination
            || intent == CaptiveSceneIntent.Torture
            || intent == CaptiveSceneIntent.Training;

        /// <summary>
        ///   Intents that constitute a public abasement of the player before an audience. Interrogation is
        ///   official and Reward is favour — neither shames the player.
        /// </summary>
        public static bool IsHumiliating(CaptiveSceneIntent intent)
            => intent == CaptiveSceneIntent.Torture
            || intent == CaptiveSceneIntent.Domination
            || intent == CaptiveSceneIntent.PersonalDesire
            || intent == CaptiveSceneIntent.Training;
    }
}
