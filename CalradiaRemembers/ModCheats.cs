// Code written by Gabriel Mailhot, 18/05/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    /// <summary>
    ///   Exposes diagnostic commands to the Bannerlord cheat console.
    ///   Open the console with Alt + ~ (must be enabled in launcher config),
    ///   then run any of:
    ///   campaign.test_llm     — run a synchronous LLM diagnostic
    ///   campaign.open_chat    — open the Calradia Remembers chat window (Sprint 7.6.A)
    ///
    ///   Sprint 10 — action testing without recreating a scene (group "cr").
    ///   These exercise the REAL pipeline (RelationGate → bridge.ExecuteAction →
    ///   ApplyPlayerRelation/GiveGoldAction), not a reimplementation:
    ///   cr.relation &lt;delta&gt; [name]            — simulate a change_relation action
    ///   cr.relation_intimate &lt;delta&gt; [name]   — same, with an intimacy event (cap +3, bypasses cooldown)
    ///   cr.give_gold &lt;amount&gt; [name]          — NPC gives the player gold
    ///   cr.take_gold &lt;amount&gt; [name]          — player gives the NPC gold
    ///   cr.info [name]                          — relation, cooldown state, captive status
    ///   cr.relation_reset [name]                — clear the relation-gain cooldown stamp
    ///   [name] is an optional name fragment; if omitted, the current conversation NPC is used.
    /// </summary>
    public static class ModCheats
    {
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

        // ── Sprint 10: action testing (group "cr") ────────────────────────────

        [CommandLineFunctionality.CommandLineArgumentFunction("relation", "cr")]
        public static string SimulateRelation(List<string> args)
            => SimulateRelation(args, NotableEventType.Other, "cr.relation <delta> [name]");

        [CommandLineFunctionality.CommandLineArgumentFunction("relation_intimate", "cr")]
        public static string SimulateRelationIntimate(List<string> args)
            => SimulateRelation(args, NotableEventType.Intimacy, "cr.relation_intimate <delta> [name]");

        [CommandLineFunctionality.CommandLineArgumentFunction("give_gold", "cr")]
        public static string GiveGold(List<string> args)
            => SimulateGold("give_gold", args, "cr.give_gold <amount> [name]");

        [CommandLineFunctionality.CommandLineArgumentFunction("take_gold", "cr")]
        public static string TakeGold(List<string> args)
            => SimulateGold("take_gold", args, "cr.take_gold <amount> [name]");

        [CommandLineFunctionality.CommandLineArgumentFunction("info", "cr")]
        public static string Info(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            double now = CampaignTime.Now.ToHours;
            bool captive = behavior.GameStateBridge.GetPlayerStatus(hero) == PlayerStatusVsNpc.NpcIsCaptive;

            string lastGain = profile.LastRelationGainHour.HasValue
                ? $"{profile.LastRelationGainHour.Value:F1}h"
                : "never";
            string cooldown = profile.LastRelationGainHour.HasValue
                ? $"{Math.Max(0.0, RelationGate.CooldownHours - (now - profile.LastRelationGainHour.Value)):F1}h remaining"
                : "ready";

            return $"{hero.Name}: relation={behavior.GameStateBridge.GetPlayerRelation(hero)}, " +
                   $"captive={captive}, lastGain={lastGain}, cooldown={cooldown} (now {now:F1}h).";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("relation_reset", "cr")]
        public static string ResetRelationCooldown(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            profile.LastRelationGainHour = null;
            behavior.Store.Set(profile);

            return $"{hero.Name}: relation-gain cooldown cleared. Next positive gain will land immediately.";
        }

        #region private

        private static string SimulateRelation(List<string> args, NotableEventType emittedEvent, string usage)
        {
            if (args == null || args.Count == 0 || !int.TryParse(args[0], out int delta))
                return $"Usage: {usage}";

            GameAction action = new GameAction {
                Type = "change_relation",
                Parameters = new Dictionary<string, string> { { "delta", delta.ToString() } }
            };
            return Dispatch(action, args.Skip(1), emittedEvent, $"proposed {delta:+#;-#;0}");
        }

        private static string SimulateGold(string type, List<string> args, string usage)
        {
            if (args == null || args.Count == 0 || !int.TryParse(args[0], out int amount))
                return $"Usage: {usage}";

            GameAction action = new GameAction {
                Type = type,
                Parameters = new Dictionary<string, string> { { "amount", amount.ToString() } }
            };
            return Dispatch(action, args.Skip(1), NotableEventType.Other, $"{type} {amount}");
        }

        /// <summary>
        ///   Runs an action through the live bridge against the resolved hero,
        ///   persists any cooldown stamp, mirrors the outcome into an open chat
        ///   window, and reports the before/after relation.
        /// </summary>
        private static string Dispatch(GameAction action, IEnumerable<string> nameTokens,
            NotableEventType emittedEvent, string requested)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(nameTokens);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            try
            {
                NpcProfile profile = GetOrCreateProfile(behavior, hero);
                int before = behavior.GameStateBridge.GetPlayerRelation(hero);

                ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
                    action, hero, new ActionContext(profile, emittedEvent));

                behavior.Store.Set(profile); // persist LastRelationGainHour if stamped
                if (outcome != null)
                    ChatManager.ActiveViewModel?.AppendSystemMessage(outcome.Message, outcome.IsPositive);

                int after = behavior.GameStateBridge.GetPlayerRelation(hero);
                string result = outcome?.Message ?? "gated to 0 (cooldown / clamp / invalid)";
                return $"{hero.Name}: {requested} → {result} | relation {before}→{after}.";
            }
            catch (Exception ex)
            {
                ModLog.Error($"cr action dispatch failed: {ex.GetType().Name}: {ex.Message}");
                return $"Failed: {ex.Message}";
            }
        }

        private static bool TryContext(out NpcMemoryCampaignBehavior behavior, out string error)
        {
            behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior == null)
            {
                error = "Calradia Remembers behavior not active. Start a campaign first.";
                return false;
            }

            error = null;
            return true;
        }

        private static NpcProfile GetOrCreateProfile(NpcMemoryCampaignBehavior behavior, Hero hero)
        {
            NpcProfile profile = behavior.Store.Get(hero.StringId);
            if (profile != null) return profile;

            profile = HeroProfileMapper.FromHero(hero);
            behavior.Store.Set(profile);
            return profile;
        }

        /// <summary>
        ///   Resolves the target hero: a name fragment if provided (case-insensitive,
        ///   excluding the player), else the current conversation NPC.
        /// </summary>
        private static Hero ResolveHero(IEnumerable<string> nameTokens)
        {
            string query = nameTokens == null ? "" : string.Join(" ", nameTokens).Trim();

            if (!string.IsNullOrEmpty(query))
                return Hero.AllAliveHeroes.FirstOrDefault(h =>
                    h != Hero.MainHero
                    && h.Name != null
                    && h.Name.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

            return Hero.OneToOneConversationHero;
        }

        #endregion
    }
}
