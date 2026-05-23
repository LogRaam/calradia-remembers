// Code written by Gabriel Mailhot, 17/05/2026.

#region

using System;
using System.Linq;
using System.Threading.Tasks;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Diagnostics
{
    /// <summary>
    ///   Sends a test prompt to the configured LLM and logs the response.
    ///   In Sprint 7.3.A this used a synthetic NPC.
    ///   In Sprint 7.3.B, if the player is currently in a vanilla Bannerlord
    ///   dialogue with a Hero, this uses the REAL hero — mapping their traits,
    ///   building an EncounterContext from live game state, and persisting
    ///   the resulting NpcProfile to the store.
    /// </summary>
    public static class LlmDiagnostic
    {
        public static async Task Run(
            LlmServices services,
            BannerlordNpcMemoryStore store,
            IGameStateBridge bridge)
        {
            if (!services.IsAvailable)
            {
                ModLog.Error("LLM services are not initialized. Cannot run diagnostic.");

                return;
            }

            try
            {
                // ── Resolve the target NPC ────────────────────────────────────
                Hero conversationHero = Hero.OneToOneConversationHero;

                NpcProfile profile;
                EncounterContext context;
                string sourceLabel;

                if (conversationHero != null)
                {
                    // Real Hero in dialogue — full Sprint 7.3.B pipeline
                    profile = GetOrCreateProfile(conversationHero, store);
                    context = EncounterContextBuilder.Build(conversationHero, profile, bridge);
                    sourceLabel = $"real Hero ({conversationHero.Name})";
                }
                else
                {
                    // Fallback to synthetic NPC (no live game context)
                    profile = BuildSyntheticProfile();
                    context = EncounterContext.Empty;
                    sourceLabel = "synthetic Diagnostic Lord";
                }

                LogContext(profile, context, sourceLabel);

                // ── Call the LLM ───────────────────────────────────────────────
                WorldState world = new WorldState {
                    CurrentDay = bridge.GetCurrentGameDay(),
                    ActiveConflicts = "Calradia Remembers integration test.",
                    Rumors = ""
                };

                ChatSession session = new ChatSession();
                const string playerMessage = "Greetings. Tell me how you fare today.";

                ModLog.Info($"Player message: {playerMessage}");
                var startMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

                NpcChatResult result = await Task.Run(() =>
                    services.ChatService.ChatAsync(profile, world, session, playerMessage, context));

                var elapsedMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond - startMs;

                if (!result.IsSuccess)
                {
                    ModLog.Error($"LLM call failed: {result.ErrorMessage}");

                    return;
                }

                LogResult(result, elapsedMs);

                // ── Persist updated profile (apply event from LLM if any) ─────
                if (conversationHero != null && result.Response?.NewEventData != null)
                {
                    var currentDay = bridge.GetCurrentGameDay();
                    profile.Events.Add(new NotableEvent(
                        currentDay,
                        result.Response.NewEventData.Type,
                        result.Response.NewEventData.Summary));
                    store.Set(profile);
                    ModLog.Info($"Profile updated and stored ({profile.Events.Count} events total).");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"LLM diagnostic threw {ex.GetType().Name}: {ex.Message}");
                ModLog.Error($"Stack: {ex.StackTrace}");
            }
        }

        #region private

        private static NpcProfile BuildSyntheticProfile() => new() {
            Id = "diagnostic_npc",
            Name = "Diagnostic Lord",
            Faction = "Calradia",
            Clan = "TestClan",
            Personality = "Pragmatic, blunt, slightly skeptical of strangers."
        };

        // ── Profile resolution ────────────────────────────────────────────────

        private static NpcProfile GetOrCreateProfile(Hero hero, BannerlordNpcMemoryStore store)
        {
            NpcProfile existing = store.Get(hero.StringId);
            if (existing != null)
            {
                ModLog.Info($"Loaded existing profile for {hero.Name} ({existing.Events.Count} events).");

                return existing;
            }

            NpcProfile fresh = HeroProfileMapper.FromHero(hero);
            store.Set(fresh);
            ModLog.Info($"Created new profile for {hero.Name} from Hero traits.");

            return fresh;
        }

        // ── Logging ───────────────────────────────────────────────────────────

        private static void LogContext(NpcProfile profile, EncounterContext context, string sourceLabel)
        {
            ModLog.Info($"Starting diagnostic — source: {sourceLabel}");
            ModLog.Info($"Profile: {profile.Name} | {profile.Faction} | {profile.Clan}");
            ModLog.Info($"Personality: {Truncate(profile.Personality, 200)}");

            var contextDesc = context.ToPromptDescription();
            ModLog.Info(string.IsNullOrEmpty(contextDesc)
                ? "Encounter context: (none)"
                : $"Encounter context: {contextDesc}");
        }

        private static void LogResult(NpcChatResult result, long elapsedMs)
        {
            ParsedResponse response = result.Response;

            ModLog.Info($"LLM responded in {elapsedMs} ms.");
            ModLog.Info($"NPC dialogue: {response.Dialogue}");

            if (response.Memory != null)
                ModLog.Info($"[MEMORY] topic={response.Memory.Topic} | " +
                            $"sentiment={response.Memory.Sentiment} | " +
                            $"decision={response.Memory.Decision ?? "—"}");

            if (response.NewEventData != null)
                ModLog.Info($"[EVENT] {response.NewEventData.Type}: {response.NewEventData.Summary}");

            if (response.Reputation != null)
                ModLog.Info($"[REP] clan={response.Reputation.ClanDelta} faction={response.Reputation.FactionDelta}");

            if (response.Actions != null && response.Actions.Any())
                foreach (GameAction action in response.Actions)
                    ModLog.Info($"[ACTION] {action.Type} (context: {action.Context ?? "—"})");

            if (result.Usage != null)
                ModLog.Info($"[TOKENS] prompt={result.Usage.PromptTokens} " +
                            $"(cached:{result.Usage.CachedPromptTokens ?? 0}) | " +
                            $"completion={result.Usage.CompletionTokens}");
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max
                ? s
                : s.Substring(0, max) + "...";

        #endregion
    }
}