// Code written by Gabriel Mailhot, 02/09/2026.
// The cr.reset recovery command: a player reported the whole mod stopped working after a war council opened a
// soft-locking scene in a village and they force-quit. Since then every "A word with you" fell straight through
// to the vanilla "continue" (OnPlayerSelectedChat throws on a persisted record left behind, so the chat never
// opens). This policy decides which stuck CR subsystems the recovery command should stand down given the live
// flags, and composes the player-facing report. Pure and engine-free so the decision and the wording are
// testable: the command only reads the engine flags and executes what this policy returns.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>A stuck CR subsystem the recovery command (cr.reset) can stand down.</summary>
    public enum CrRecoveryAction
    {
        /// <summary>A chat overlay left open with no live conversation behind it.</summary>
        CloseChatOverlay,

        /// <summary>A convened assembly / council still marked active.</summary>
        CancelAssembly
    }

    /// <summary>
    ///   Decides what cr.reset should clear, and how to report it. The recovery command is run deliberately by a
    ///   stuck player, so an "open" chat overlay or an "active" assembly at that moment is treated as stuck and
    ///   stood down; nothing here touches saved memories, which the command must never wipe.
    /// </summary>
    public static class ModRecoveryPolicy
    {
        /// <summary>
        ///   The subsystems to stand down, given the live flags. Order is stable (overlay before assembly) so the
        ///   report reads the same every time.
        /// </summary>
        public static IReadOnlyList<CrRecoveryAction> Plan(bool chatOverlayOpen, bool assemblyActive)
        {
            var actions = new List<CrRecoveryAction>();
            if (chatOverlayOpen) actions.Add(CrRecoveryAction.CloseChatOverlay);
            if (assemblyActive) actions.Add(CrRecoveryAction.CancelAssembly);

            return actions;
        }

        /// <summary>
        ///   The player-facing line for what the reset cleared. An empty plan is not a failure: it means nothing
        ///   was stuck, so the message says so and points at the log in case the fault is elsewhere.
        /// </summary>
        public static string Describe(IReadOnlyList<CrRecoveryAction> actions)
        {
            if (actions == null || actions.Count == 0)
                return "Calradia Remembers: nothing was stuck to reset. If conversations still fail, see CalradiaRemembers.log.";

            var parts = new List<string>();
            foreach (CrRecoveryAction action in actions)
                switch (action)
                {
                    case CrRecoveryAction.CloseChatOverlay:
                        parts.Add("closed a stuck chat window");
                        break;
                    case CrRecoveryAction.CancelAssembly:
                        parts.Add("stood down an active assembly");
                        break;
                }

            return "Calradia Remembers reset: " + string.Join(", ", parts) + ". Try speaking with someone again.";
        }
    }
}
