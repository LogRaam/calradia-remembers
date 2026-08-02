// Code written by Gabriel Mailhot, 02/08/2026.
// Coercion is not intimacy. A captive or captor scene is non-consensual by construction (a prisoner cannot
// consent), so an [EVENT] the LLM tags Intimacy or Flirt inside one must NEVER be stored as a consensual
// intimacy memory, must NOT grant the intimacy regard gain, and must NOT rouse jealousy as a courtship. The
// scene's own Captivity / grudge finalizers already hold the harm. Pure so this rule stays covered by tests.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Decides whether a romantic <c>[EVENT]</c> emitted mid-scene must be discarded because the scene is
    ///   coercive (the player is a captive, or holds the NPC captive). Keeps intimacy and coercion cleanly
    ///   separate in the memory model.
    /// </summary>
    public static class CoerciveSceneEventPolicy
    {
        /// <summary>
        ///   True when a romantic event (<see cref="NotableEventType.Intimacy" /> or
        ///   <see cref="NotableEventType.Flirt" />) must be DROPPED because it was emitted inside a coercive
        ///   scene. Any other event type, or a non-coercive scene, keeps the event (returns false).
        /// </summary>
        public static bool ShouldDropRomanticEvent(NotableEventType? type, bool coerciveScene)
        {
            if (!coerciveScene || type == null) return false;

            return type.Value == NotableEventType.Intimacy || type.Value == NotableEventType.Flirt;
        }
    }
}
