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
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
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
    ///   cr.discover [name]                      — inject 3 test discoveries (Sprint 10.5 encyclopedia test)
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

        /// <summary>
        ///   cr.discover [name]
        ///   Injects 3 test discoveries into the NPC's profile so the encyclopedia
        ///   discovery section can be validated without playing through a conversation.
        ///   Safe to call repeatedly — deduplicated by key.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("discover", "cr")]
        public static string SimulateDiscover(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            int added = 0;
            int day = (int)CampaignTime.Now.ToDays;
            DiscoveredTrait[] seeds = {
                new DiscoveredTrait { Key = "orientation",          Description = "She seems drawn to men.",                           GameDay = day },
                new DiscoveredTrait { Key = "archetype",            Description = "She moves with the quiet confidence of someone who knows their worth.", GameDay = day },
                new DiscoveredTrait { Key = "preference_dominant",  Description = "She prefers to lead in her relationships.",         GameDay = day }
            };

            foreach (DiscoveredTrait seed in seeds)
            {
                bool exists = profile.DiscoveredTraits.Exists(t =>
                    string.Equals(t.Key, seed.Key, StringComparison.OrdinalIgnoreCase));
                if (!exists) { profile.DiscoveredTraits.Add(seed); added++; }
            }

            behavior.Store.Set(profile);
            return $"{hero.Name}: {added} discovery/ies added ({profile.DiscoveredTraits.Count} total). Open encyclopedia to verify.";
        }

        // ── Sprint 11: informal quest testing (group "cr") ────────────────────

        /// <summary>
        ///   cr.quest [name]
        ///   Seeds a bandit-clear quest (near a real village, 10-day deadline, 300g/+3)
        ///   on the NPC, through the real issue path. Tests prompt injection of the quest
        ///   and the giver acknowledging it. Pair with cr.quest_done to test the reward.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("quest", "cr")]
        public static string SeedQuest(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            string village = Settlement.All.FirstOrDefault(s => s.IsVillage)?.Name?.ToString();
            if (village == null) return "No village found to anchor the quest.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            int day = behavior.GameStateBridge.GetCurrentGameDay();

            var proposal = new QuestProposal {
                Type             = QuestType.BanditClear,
                Description      = $"Clear the bandits troubling the roads near {village}.",
                TargetSettlement = village,
                DeadlineDays     = 10,
                RewardGold       = 300,
                RewardRelation   = 3
            };
            var fake = new ParsedResponse { Dialogue = "", QuestGiven = proposal };

            QuestService.ApplyResponse(profile, hero, fake, day,
                behavior.GameStateBridge, behavior.Store,
                (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

            int outstanding = profile.ActiveQuests.Count(q => q.IsOutstanding);
            return $"{hero.Name}: seeded bandit_clear near {village} ({outstanding} outstanding quest(s)).";
        }

        /// <summary>
        ///   cr.quest_done [name]
        ///   Force-stamps the NPC's outstanding quest as satisfied (bypassing real events)
        ///   so the giver will offer to reward it on the next exchange. Tests the
        ///   completion + reward path without fighting a battle.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("quest_done", "cr")]
        public static string ForceQuestDone(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            InformalQuest quest = profile.ActiveQuests.FirstOrDefault(q => q.IsOutstanding);
            if (quest == null) return $"{hero.Name} has no outstanding quest. Seed one with cr.quest first.";

            int day = behavior.GameStateBridge.GetCurrentGameDay();
            quest.SatisfiedOnDay = day;
            quest.Evidence = "(debug) forced complete";
            behavior.Store.Set(profile);
            return $"{hero.Name}: quest '{quest.Type}' marked satisfied. Talk to them to collect {quest.RewardGold}g/+{quest.RewardRelation}.";
        }

        /// <summary>
        ///   cr.quest_abandon [name]
        ///   Force-abandons the NPC's outstanding quest (as if the player told them they
        ///   quit), applying the broken-word penalty. Tests the abandon path without the LLM.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("quest_abandon", "cr")]
        public static string ForceQuestAbandon(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            int before = profile.ReputationWithPlayer;
            int day = behavior.GameStateBridge.GetCurrentGameDay();

            var fake = new ParsedResponse { Dialogue = "", QuestAbandoned = new QuestAbandonClaim() };
            QuestService.ApplyResponse(profile, hero, fake, day,
                behavior.GameStateBridge, behavior.Store,
                (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

            return $"{hero.Name}: abandon applied (regard {before}→{profile.ReputationWithPlayer}).";
        }

        /// <summary>
        ///   cr.quests [name]
        ///   Lists the NPC's quests with their state.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("quests", "cr")]
        public static string ListQuests(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            if (profile.ActiveQuests.Count == 0) return $"{hero.Name} has no quests.";

            var lines = profile.ActiveQuests.Select(q =>
            {
                string state = q.IsOutstanding ? "outstanding"
                    : q.IsAwaitingReward ? "DONE (awaiting reward)"
                    : q.Status.ToString();
                string deadline = q.DeadlineDay.HasValue ? $", deadline day {q.DeadlineDay}" : "";
                return $"  [{q.Type}] {state}{deadline} — {q.Description}";
            });

            return $"{hero.Name} quests:\n" + string.Join("\n", lines);
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
