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
        ///   The four independent guards on a captor killing the player, every one of which must hold. They
        ///   come from four different places, the MCM, the live campaign, the content floor and Bannerlord's
        ///   own death setting, and they read identically as bare booleans.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class ExecutionFacts
        {
           /// <summary>The player opted in through the MCM setting.</summary>
           public bool McmOn { get; set; }

           /// <summary>The player is genuinely THIS captor's captive right now.</summary>
           public bool PlayerIsCaptive { get; set; }

           /// <summary>
           ///   The scene sits at the Hardcore/Adult content floor every other captive-scene act requires.
           /// </summary>
           public bool Hardcore { get; set; }

           /// <summary>
           ///   Bannerlord's own campaign setting permits a hero to die, i.e. NOT IsLifeDeathCycleDisabled.
           /// </summary>
           public bool DeathAllowed { get; set; }
        }

        /// <summary>
        ///   True only when EVERY guard holds: the player opted in via MCM (<c>McmOn</c>), the
        ///   player is genuinely this captor's captive right now (<c>PlayerIsCaptive</c>), the
        ///   scene sits at the same Hardcore/Adult content floor every other captive-scene act already
        ///   requires (<c>Hardcore</c>), and Bannerlord's own campaign death setting permits a
        ///   hero to die (<c>DeathAllowed</c>, i.e. NOT IsLifeDeathCycleDisabled). Any single
        ///   false input refuses the whole thing.
        /// </summary>
        public static bool CanExecute(ExecutionFacts facts)
        {
           if (facts == null) return false;

           bool mcmOn = facts.McmOn;
           bool playerIsCaptive = facts.PlayerIsCaptive;
           bool hardcore = facts.Hardcore;
           bool deathAllowed = facts.DeathAllowed;

           return mcmOn && playerIsCaptive && hardcore && deathAllowed;
        }
    }
}
