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
using CalradiaRemembers.Letters;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Witnesses;
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

        // ── Help ─────────────────────────────────────────────────────────────

        /// <summary>cr.help — list all cr.* commands with a short description.</summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("help", "cr")]
        public static string Help(List<string> args)
        {
            return
                "Calradia Remembers console commands (group: cr)\n" +
                "\n" +
                "RELATION\n" +
                "  cr.relation <delta> [name]           Simulate change_relation (event Other) — cap +1, cooldown 8h\n" +
                "  cr.relation_intimate <delta> [name]  Same with Intimacy event — cap +3, bypasses cooldown\n" +
                "  cr.relation_reset [name]             Clear the relation-gain cooldown\n" +
                "  cr.rep_set <value> [name]            Force-set ReputationWithPlayer (bypasses gate — for testing)\n" +
                "  cr.info [name]                       Show relation, captive status, cooldown remaining\n" +
                "\n" +
                "GOLD\n" +
                "  cr.give_gold <amount> [name]         NPC gives the player gold\n" +
                "  cr.take_gold <amount> [name]         Player gives the NPC gold\n" +
                "\n" +
                "LETTERS\n" +
                "  cr.letter [name]                     Queue a RomanticCorrespondence test letter from the NPC (immediate delivery)\n" +
                "  cr.compose [name]                    Open the letter compose window to write to the NPC (Sprint 12d test path)\n" +
                "  cr.courier [name]                    Dispatch a test letter + spawn the visible courier party (Sprint 12c)\n" +
                "  cr.courier_slow [name]               Spawn a SLOW engageable courier to observe bandit interception\n" +
                "\n" +
                "MULTI-NPC WITNESSES\n" +
                "  cr.witnesses [name]                  Diagnose: log settlement/party presence data for an NPC (Talk-vs-Visit check)\n" +
                "  cr.witness [witnessName]             Add a witness to the open chat session (manual seed for testing)\n" +
                "\n" +
                "PLAYER STANDING\n" +
                "  cr.standing                          Show player clan tier and arena rank/wins\n" +
                "\n" +
                "DISCOVERY\n" +
                "  cr.discover [name]                   Seed 3 test DiscoveredTraits (encyclopedia test)\n" +
                "\n" +
                "QUESTS\n" +
                "  cr.quest [name]                      Seed a bandit_clear quest on the NPC (journal opens)\n" +
                "  cr.quest_scout [name]                Seed a scout_army quest on the NPC (uses giver's first enemy)\n" +
                "  cr.quest_done [name]                 Force-stamp the outstanding quest as satisfied (journal updates)\n" +
                "  cr.quest_complete [name]             Pay reward and close the journal entry with success\n" +
                "  cr.quest_abandon [name]              Force-abandon the outstanding quest (-3 regard, journal closes)\n" +
                "  cr.quests [name]                     List the NPC's quests with their current state\n" +
                "\n" +
                "[name] is an optional name fragment (case-insensitive). If omitted, the current conversation NPC is used.";
        }

        // ── Multi-NPC witnesses ───────────────────────────────────────────────

        /// <summary>
        ///   cr.witnesses [name]
        ///   Runs WitnessResolver.DiagnoseWitnesses for the given NPC and prints a
        ///   detailed report: CurrentSettlement, StayingInSettlement, HeroesWithoutParty
        ///   names with profile flags, party leaders inside the settlement, and
        ///   available player companions. Use this to verify which collection holds
        ///   present lords during a Talk conversation (vs Visit).
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("witnesses", "cr")]
        public static string DiagnoseWitnesses(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            return WitnessResolver.DiagnoseWitnesses(hero, behavior.Store);
        }

        /// <summary>
        ///   cr.witness [witnessName]
        ///   Adds a named witness to the currently open chat session. Primary test path
        ///   for the multi-NPC feature — validates prompt injection, header UI, and the
        ///   request_privacy round-trip without needing a real settlement context.
        ///   The chat window must be open first (e.g. via cr.chat or a normal conversation).
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("witness", "cr")]
        public static string AddWitness(List<string> args)
        {
            if (ChatManager.ActiveViewModel == null)
                return "No chat window is open. Open a conversation first.";

            string nameFragment = args.Count > 0 ? string.Join(" ", args).Trim() : null;
            if (string.IsNullOrWhiteSpace(nameFragment))
                return "Usage: cr.witness [name]";

            Hero witnessHero = Hero.AllAliveHeroes.FirstOrDefault(h =>
                h != Hero.MainHero && h.IsAlive
                && h.Name?.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0);

            string displayName = witnessHero?.Name?.ToString() ?? nameFragment;
            string relation = "a lord present";
            if (witnessHero != null)
            {
                int rel = witnessHero.GetRelation(Hero.MainHero);
                relation = rel >= 20 ? "a lord you regard with trust"
                         : rel <= -20 ? "a rival lord"
                         : "a lord you know";
            }

            var entry = new WitnessEntry { Name = displayName, RelationToNpc = relation };
            ChatManager.ActiveViewModel.AddWitness(entry);
            return $"Witness '{displayName}' ({relation}) added to chat session.";
        }

        // ── Sprint 12d: player letter compose ─────────────────────────────────

        /// <summary>
        ///   cr.compose [name]
        ///   Opens the letter compose window for the given NPC. Primary test path for
        ///   Sprint 12d — validates the compose UI and delivery pipeline without
        ///   requiring a tavernkeeper conversation.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("compose", "cr")]
        public static string ComposeLetter(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            ChatManager.OpenForCompose(profile);
            return $"Compose window opened for {hero.Name}.";
        }

        // ── Sprint 12c: visible courier testing ───────────────────────────────

        /// <summary>
        ///   cr.courier [name]
        ///   Dispatches a test letter to the given NPC immediately, spawning the visible
        ///   courier party. Use to watch the courier travel the map, be intercepted, or
        ///   deliver — without going through the compose window.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("courier", "cr")]
        public static string DispatchCourier(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            int today = behavior.GameStateBridge.GetCurrentGameDay();
            int days = PlayerLetterService.CreateAndQueue(
                profile, "A test message carried by courier.", today, behavior.Store);

            PlayerLetter letter = profile.ReceivedPlayerLetters.LastOrDefault();
            string courier = letter?.CourierPartyId ?? "FAILED (date fallback)";
            return $"Courier dispatched to {hero.Name} (~{days}d). Party: {courier}.";
        }

        /// <summary>
        ///   cr.courier_slow [name]
        ///   Like cr.courier but spawns a SLOW, engageable courier (speed 2, does not avoid
        ///   hostiles) so bandit interception and the [COURIER] destroyed path can actually
        ///   be observed — the normal fast courier almost always outruns threats.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("courier_slow", "cr")]
        public static string DispatchSlowCourier(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            int today = behavior.GameStateBridge.GetCurrentGameDay();

            var letter = new PlayerLetter {
                RecipientId   = profile.Id,
                RecipientName = profile.Name,
                Content       = "A slow test courier — easy prey for bandits.",
                SentOnDay     = today,
                DeliveryDay   = today + 8
            };
            profile.ReceivedPlayerLetters.Add(letter);

            Hero recipient = Hero.FindFirst(h => h.StringId == profile.Id);
            bool spawned = CourierService.TrySpawnCourier(
                letter, recipient, speedOverride: 2.0f, avoidHostile: false);
            behavior.Store.Set(profile);

            return spawned
                ? $"SLOW courier dispatched to {hero.Name} (party {letter.CourierPartyId}). Watch for interception."
                : $"Slow courier spawn FAILED for {hero.Name} (date fallback active).";
        }

        // ── Sprint 12b: letter testing ────────────────────────────────────────

        /// <summary>
        ///   cr.letter [name]
        ///   Queues a test letter from the NPC with DeliveryDay = today so it shows
        ///   up on the next daily tick (or immediately via cr.letter_deliver).
        ///   Uses RomanticCorrespondence as the reason so personality is exercised.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("letter", "cr")]
        public static string SeedLetter(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            int today = behavior.GameStateBridge.GetCurrentGameDay();

            LetterGenerationService.QueueInitialLetter(
                profile, hero,
                LetterReason.RomanticCorrespondence,
                $"Test letter. Romantic status: {profile.Romantic?.Status.ToString() ?? "unknown"}.",
                behavior.LlmServices, behavior.Store, today);

            // Force immediate delivery by pre-dating it.
            // The LLM call is async; the letter appears on next daily tick once generated.
            return $"{hero.Name}: letter generation queued. It will appear at the next daily tick.";
        }

        // ── Sprint 12a: player standing diagnostic ────────────────────────────

        /// <summary>
        ///   cr.standing
        ///   Shows the player's clan tier and arena rank/wins — the two standing metrics
        ///   that drive NPC social-attraction instructions in the prompt.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("standing", "cr")]
        public static string ShowStanding(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;

            Hero player = Hero.MainHero;
            if (player == null) return "Player hero not available.";

            int clanTier = player.Clan?.Tier ?? 0;
            string clanName = player.Clan?.Name?.ToString() ?? "no clan";

            int arenaRank = 0;
            int arenaWins = 0;
            try
            {
                var tm = Campaign.Current?.TournamentManager;
                if (tm != null)
                {
                    arenaRank = tm.GetLeaderBoardRank(player);
                    var lb = tm.GetLeaderboard();
                    foreach (var kvp in lb)
                        if (kvp.Key == player) { arenaWins = kvp.Value; break; }
                }
            }
            catch { /* TournamentManager not yet initialised — leave at 0 */ }

            string rankStr = arenaRank > 0 ? $"#{arenaRank} ({arenaWins} win(s))" : "unranked (0 tournament wins)";
            string attractionNote = clanTier >= 3 ? " [marriage interest threshold MET]" : " [marriage interest threshold NOT met (need tier 3+)]";
            string arenaNote = arenaRank > 0 && arenaRank <= 10 ? " [affair interest threshold MET]" : " [affair interest threshold NOT met (need rank 1–10)]";

            return $"Player standing:\n" +
                   $"  Clan: {clanName} — tier {clanTier}{attractionNote}\n" +
                   $"  Arena rank: {rankStr}{arenaNote}";
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
        ///   cr.quest_scout [name]
        ///   Seeds a scout_army quest on the NPC, targeting their first enemy kingdom.
        ///   Pair with cr.quest_done to test the completion reward, or let proximity
        ///   detection fire naturally (OnHourlyTick) for a real-path test.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("quest_scout", "cr")]
        public static string SeedScoutQuest(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            IFaction enemy = hero.MapFaction != null
                ? Kingdom.All?.FirstOrDefault(k =>
                    !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(hero.MapFaction, k))
                : null;

            if (enemy == null)
                return $"{hero.Name} is not at war with any kingdom — cannot seed a scout_army quest.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            int day = behavior.GameStateBridge.GetCurrentGameDay();

            var proposal = new QuestProposal {
                Type          = QuestType.ScoutArmy,
                Description   = $"Find where the armies of {enemy.Name} are moving and report back.",
                TargetFaction = enemy.Name?.ToString(),
                DeadlineDays  = 15,
                RewardGold    = 400,
                RewardRelation = 3
            };
            var fake = new ParsedResponse { Dialogue = "", QuestGiven = proposal };

            QuestService.ApplyResponse(profile, hero, fake, day,
                behavior.GameStateBridge, behavior.Store,
                (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

            int outstanding = profile.ActiveQuests.Count(q => q.IsOutstanding);
            return $"{hero.Name}: seeded scout_army quest targeting {enemy.Name} ({outstanding} outstanding quest(s)).";
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
            quest.Evidence = "(debug) forced complete on day " + day;
            behavior.Store.Set(profile);

            // Update the journal mirror so "Done" appears without a real battle.
            QuestJournalBridge.NotifyDeedDone(quest, hero);

            return $"{hero.Name}: quest '{quest.Type}' marked satisfied — journal updated. " +
                   $"Use cr.quest_complete to pay {quest.RewardGold}g/+{quest.RewardRelation} and close the journal.";
        }

        /// <summary>
        ///   cr.quest_complete [name]
        ///   Simulates the NPC acknowledging the completed quest: closes the journal
        ///   entry with success and pays the promised reward. Requires cr.quest_done
        ///   first (the quest must already be in the awaiting-reward state).
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("quest_complete", "cr")]
        public static string ForceQuestComplete(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            Hero hero = ResolveHero(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            if (!profile.ActiveQuests.Any(q => q.IsAwaitingReward))
                return $"{hero.Name} has no quest awaiting reward. Use cr.quest_done first.";

            int day = behavior.GameStateBridge.GetCurrentGameDay();
            var fake = new ParsedResponse { Dialogue = "", QuestCompleted = new QuestCompletionClaim() };

            QuestService.ApplyResponse(profile, hero, fake, day,
                behavior.GameStateBridge, behavior.Store,
                (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

            return $"{hero.Name}: quest completed — journal closed, reward paid.";
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

        /// <summary>
        ///   cr.journal_check
        ///   Diagnostic: prints how many CalradiaRemembers quests are registered in
        ///   Campaign.Current.QuestManager and lists their StringIds. Use to verify
        ///   that cr.quest actually created a QuestBase mirror in the journal.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("journal_check", "cr")]
        public static string JournalCheck(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;

            var qm = Campaign.Current?.QuestManager;
            if (qm == null) return "QuestManager not available.";

            var crQuests = qm.Quests.OfType<CalradiaRemembersQuest>().ToList();
            if (crQuests.Count == 0)
                return "No CalradiaRemembers quests in QuestManager. (OpenMirror may have failed — check the log or try cr.quest again.)";

            var lines = crQuests.Select(q =>
                $"  {q.StringId}  giver={q.QuestGiver?.Name}  isSpecialType={q.IsSpecialQuest}");
            return $"{crQuests.Count} CR quest(s) in QuestManager:\n" + string.Join("\n", lines);
        }

        /// <summary>
        ///   cr.rep_set &lt;value&gt; [name]
        ///   Directly sets ReputationWithPlayer to the given value, bypassing RelationGate.
        ///   Use for testing scenarios that require a specific trust level (e.g. +100 to
        ///   verify that a trusted NPC accepts a private audience request).
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("rep_set", "cr")]
        public static string SetReputation(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
                return error;

            if (args.Count == 0 || !int.TryParse(args[0], out int value))
                return "Usage: cr.rep_set <value> [name]  (e.g. cr.rep_set 100 Alympia)";

            var nameArgs = args.Count > 1 ? args.GetRange(1, args.Count - 1) : new List<string>();
            Hero hero = ResolveHero(nameArgs);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            int previous = profile.ReputationWithPlayer;
            profile.ReputationWithPlayer = value;
            behavior.Store.Set(profile);

            ModLog.Info($"[cr.rep_set] {hero.Name}: rep {previous} → {value} (gate bypassed).");
            return $"{hero.Name}: ReputationWithPlayer set to {value} (was {previous}).";
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
