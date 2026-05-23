using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Diagnostics;
using TaleWorlds.Library;

namespace CalradiaRemembers
{
    /// <summary>
    /// Exposes diagnostic commands to the Bannerlord cheat console.
    /// Open the console with Alt + ~ (must be enabled in launcher config),
    /// then run any of:
    ///   campaign.test_llm     — run a synchronous LLM diagnostic
    ///   campaign.open_chat    — open the Calradia Remembers chat window (Sprint 7.6.A)
    /// </summary>
    public static class ModCheats
    {
        [CommandLineFunctionality.CommandLineArgumentFunction("test_llm", "campaign")]
        public static string TestLlm(List<string> args)
        {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior == null)
                return "Calradia Remembers behavior not active. Start a campaign first.";

            if (behavior.LlmServices == null || !behavior.LlmServices.IsAvailable)
                return "LLM services not available. Check OPENROUTER_API_KEY environment variable.";

            _ = LlmDiagnostic.Run(behavior.LlmServices, behavior.Store, behavior.GameStateBridge)
                .ContinueWith(t =>
                {
                    if (t.Exception != null)
                        ModLog.Error($"LLM diagnostic faulted: {t.Exception.GetBaseException().Message}");
                }, TaskScheduler.Default);

            return "LLM diagnostic started. Watch the combat log and the log file for results.";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("open_chat", "campaign")]
        public static string OpenChat(List<string> args)
        {
            try
            {
                ChatManager.Open();
                return "Chat window opened.";
            }
            catch (Exception ex)
            {
                ModLog.Error($"open_chat failed: {ex.GetType().Name}: {ex.Message}");
                return $"Failed to open chat: {ex.Message}";
            }
        }
    }
}


