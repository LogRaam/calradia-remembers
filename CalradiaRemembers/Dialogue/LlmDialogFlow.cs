// Code written by Gabriel Mailhot, 19/05/2026.
// Updated 28/05/2026: Call HeroProfileMapper.UpgradeIfNeeded on existing profile load.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Dialogue
{
    /// <summary>
    ///   Registers Calradia Remembers' player option in the Bannerlord
    ///   conversation flow. When selected, captures the conversation context
    ///   (NPC profile, EncounterContext, fresh ChatSession seeded with vanilla
    ///   dialogue), closes the vanilla dialogue, leaves the encounter, and
    ///   opens the custom chat window. Time is paused by ChatManager so the
    ///   player is safe while chatting.
    ///
    ///   Sprint 7.6.F final — close_window + PlayerEncounter.Finish:
    ///   - close_window: clean exit from the vanilla dialogue state machine
    ///     (avoids the "auto-trigger first option" bug we hit when looping
    ///     back to hero_main_options).
    ///   - PlayerEncounter.Finish: dismisses the Attack/Leave overlay so the
    ///     chat window is the only active UI.
    ///   - ChatManager pauses campaign time while the chat is open.
    ///
    ///   Sprint 8.3 polish — auto-upgrade on existing profile load:
    ///   - Existing profiles from earlier sprints (pre-8.2 RomanticProfile,
    ///     pre-8.1 archetype catalog) are silently brought up to date when
    ///     loaded. Idempotent — no-op for already up-to-date profiles.
    /// </summary>
    public sealed class LlmDialogFlow
    {
        public EncounterContext Context { get; private set; }
        public NpcProfile Profile { get; private set; }
        public ChatSession Session { get; private set; }

        public void Register(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "calradia_remembers_open_chat",      // ID unique
                "hero_main_options",                 // input (où apparaît l'option)
                "calradia_remembers_dummy_token",    // ← TOKEN CUSTOM QUI N'EXISTE PAS
                "We need to talk...",
                IsHeroEligible,
                OnPlayerSelectedChat,
                110);                                // priorité haute pour apparaître en haut
        }

        #region private

        private NpcProfile GetOrCreateProfile(Hero hero, BannerlordNpcMemoryStore store)
        {
            NpcProfile existing = store.Get(hero.StringId);
            if (existing != null)
            {
                ModLog.Info($"Loaded existing profile for {hero.Name} ({existing.Events.Count} events).");

                // Retrofit Romantic profile / stale Trait for profiles created
                // before Sprint 8.1 / 8.2 / 8.3. Idempotent — silent for current profiles.
                HeroProfileMapper.UpgradeIfNeeded(existing, hero);

                return existing;
            }

            NpcProfile fresh = HeroProfileMapper.FromHero(hero);
            store.Set(fresh);
            ModLog.Info($"Created new profile for {hero.Name} from Hero traits.");
            return fresh;
        }

        private bool IsHeroEligible()
        {
            Hero hero = Hero.OneToOneConversationHero;
            if (hero == null) return false;
            if (!hero.IsAlive) return false;
            if (hero == Hero.MainHero) return false;
            return true;
        }

        private void OnPlayerSelectedChat()
        {
            ModLog.Info("=== OnPlayerSelectedChat STARTED ===");
            try
            {
                NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
                ModLog.Info($"NpcMemoryCampaignBehavior.Instance found: {behavior != null}");
                if (behavior == null)
                {
                    ModLog.Error("Behavior is null - cannot open chat");
                    return;
                }

                Hero hero = Hero.OneToOneConversationHero;
                ModLog.Info($"Hero.OneToOneConversationHero = {hero?.Name}");
                if (hero == null)
                {
                    ModLog.Error("No OneToOneConversationHero");
                    return;
                }

                Profile = GetOrCreateProfile(hero, behavior.Store);
                Context = EncounterContextBuilder.Build(hero, Profile, behavior.GameStateBridge);
                Session = new ChatSession();

                var npcName = hero.Name?.ToString() ?? "the lord";
                ModLog.Info($"Attempting to open chat for NPC: {npcName}");

                ChatManager.Open(npcName);
                ModLog.Info("ChatManager.Open() has been called");

                ChatViewModel vm = ChatManager.ActiveViewModel;
                ModLog.Info($"ChatManager.ActiveViewModel = {(vm != null ? "OK" : "NULL")}");

                if (vm != null) SeedCapturedVanillaLinesIntoChat(vm);

                ModLog.Info("=== OnPlayerSelectedChat COMPLETED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                ModLog.Error($"OnPlayerSelectedChat CRASHED: {ex.GetType().Name} - {ex.Message}");
                ModLog.Error($"StackTrace: {ex.StackTrace}");
            }
        }

        private void SeedCapturedVanillaLinesIntoChat(ChatViewModel vm)
        {
            IReadOnlyList<VanillaDialogueRecorder.CapturedLine> captured
                = NpcMemoryCampaignBehavior.Instance.DialogueRecorder.GetCapturedLines();

            ModLog.Info($"Seeding {captured.Count} vanilla lines into chat");

            foreach (VanillaDialogueRecorder.CapturedLine line in captured)
                if (line.IsPlayer)
                {
                    vm.AppendPlayerMessage(line.Text);
                    Session.AddPlayerMessage(line.Text);
                }
                else
                {
                    vm.AppendNpcMessage(line.Text);
                    Session.AddNpcMessage(line.Text);
                }

            NpcMemoryCampaignBehavior.Instance.DialogueRecorder.Clear();
        }

        #endregion
    }
}
