// Code written by Gabriel Mailhot, 02/09/2026.
// cr.reset: a player-facing recovery command. A player reported the whole mod stopped working after a war
// council opened a soft-locking scene in a village and they force-quit; afterwards every "A word with you" fell
// through to the vanilla "continue" because the chat open path threw on state left behind. This command stands
// down the stuck CR overlays (a chat window that never closed, an assembly still marked active) and clears the
// vanilla-dialogue capture buffer, then reports what it did. It NEVER wipes saved memories. The decision and the
// wording live in the tested ModRecoveryPolicy; this command only reads the engine flags and executes the plan.

#region

using System.Collections.Generic;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.reset: stands down any stuck Calradia Remembers overlay so conversations work again. Closes a chat
        ///   window that never closed, cancels an assembly still marked active, and clears the stale
        ///   vanilla-dialogue capture. Saved memories are never touched.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("reset", "cr")]
        public static string Reset(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

            bool chatOverlayOpen = ChatManager.IsOpen;
            bool assemblyActive = AssemblyBehavior.Instance?.HasActiveAssembly == true;

            IReadOnlyList<CrRecoveryAction> plan = ModRecoveryPolicy.Plan(chatOverlayOpen, assemblyActive);
            foreach (CrRecoveryAction action in plan)
                switch (action)
                {
                    case CrRecoveryAction.CloseChatOverlay:
                        ChatManager.Close();
                        break;
                    case CrRecoveryAction.CancelAssembly:
                        AssemblyBehavior.Instance.Cancel();
                        break;
                }

            // Always drop a stale vanilla-dialogue capture: a buffer left over from an aborted scene would
            // otherwise seed the next conversation with junk lines. Harmless when already empty.
            behavior.DialogueRecorder?.Clear();

            string message = ModRecoveryPolicy.Describe(plan);
            ModLog.Info($"[RESET] {message} (chatOverlayOpen={chatOverlayOpen}, assemblyActive={assemblyActive})");

            return message;
        }
    }
}
