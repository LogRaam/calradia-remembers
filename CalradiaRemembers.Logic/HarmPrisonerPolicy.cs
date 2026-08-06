// Code written by Gabriel Mailhot, 06/08/2026.
// v1.34.2 resilience hardening (prisoner/gold audit, finding 1): harm_prisoner deducts REAL player health, yet
// the executor used to apply it with only a per-scene cap and a HP floor, never re-checking that the player is
// genuinely a captor's prisoner inside an ACTIVE captive scene, and never enforcing the Hardcore floor the
// feature is advertised behind. A stray or premature emission could then wound the player during an ordinary
// conversation. This pure gate mirrors PlayerExecutionPolicy for execute_player ("the prompt is advice, the
// bridge is law"): every guard must hold before a single hit point is lost.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure gate deciding whether a captor may inflict REAL physical harm on the captive PLAYER. Consulted by
    ///   BannerlordGameStateBridge right before it deducts health, so a stray or premature harm_prisoner emission
    ///   can never wound the player outside an actual captive scene.
    /// </summary>
    public static class HarmPrisonerPolicy
    {
        /// <summary>
        ///   True only when EVERY guard holds: the player is genuinely this captor's prisoner right now
        ///   (<paramref name="playerIsCaptiveOfCaptor" />), a captive scene is actually running
        ///   (<paramref name="captiveSceneActive" />), and the scene sits at the same Hardcore/Adult content
        ///   floor every other captive-scene act already requires (<paramref name="hardcore" />). Any single
        ///   false input refuses the harm and costs no health.
        /// </summary>
        public static bool CanHarm(bool playerIsCaptiveOfCaptor, bool captiveSceneActive, bool hardcore)
            => playerIsCaptiveOfCaptor && captiveSceneActive && hardcore;
    }
}
