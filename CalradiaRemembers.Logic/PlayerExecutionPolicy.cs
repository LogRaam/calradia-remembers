// Code written by Gabriel Mailhot, 04/08/2026.
// Captivity execution (player request via the "AI Executes" mod, COUNCIL_ACTIONS.md Partie 10.6): a captive under
// torture wanted the scene able to end their character for real. This is IRREVERSIBLE (game over, or heir
// succession when the clan has an heir), so every guard below must hold before the bridge ever calls
// KillCharacterAction: the MCM opt-in (AllowCaptorExecutions, default OFF), the player genuinely captive right
// now, the same Hardcore/Adult content gate the rest of the captive-scene vocabulary (harm_prisoner, the CNC
// framing) already requires, and Bannerlord's own campaign death setting (IsLifeDeathCycleDisabled). A single
// missing guard refuses the execution outright; there is no partial credit for a high-stakes, one-way action.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure gate deciding whether a captor may actually execute the captive PLAYER. Consulted twice: once by
    ///   the mod's EncounterContextBuilder (deciding whether the LLM is even TAUGHT the execute_player action
    ///   this turn) and again, always, by BannerlordGameStateBridge right before it calls KillCharacterAction
    ///   ("the prompt is advice, the bridge is law"), so a stale or premature emission can never end the run.
    /// </summary>
    public static class PlayerExecutionPolicy
    {
        /// <summary>
        ///   True only when EVERY guard holds: the player opted in via MCM (<paramref name="mcmOn" />), the
        ///   player is genuinely this captor's captive right now (<paramref name="playerIsCaptive" />), the
        ///   scene sits at the same Hardcore/Adult content floor every other captive-scene act already
        ///   requires (<paramref name="hardcore" />), and Bannerlord's own campaign death setting permits a
        ///   hero to die (<paramref name="deathAllowed" />, i.e. NOT IsLifeDeathCycleDisabled). Any single
        ///   false input refuses the whole thing.
        /// </summary>
        public static bool CanExecute(bool mcmOn, bool playerIsCaptive, bool hardcore, bool deathAllowed)
            => mcmOn && playerIsCaptive && hardcore && deathAllowed;
    }
}
