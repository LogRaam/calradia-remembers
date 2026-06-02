// Code written by Gabriel Mailhot, 19/05/2026.
// Updated 28/05/2026: Call HeroProfileMapper.UpgradeIfNeeded on existing profile load.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
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

        // Dialogue token for the relation-gated greeting that sets the NPC's
        // facial expression before the chat window opens (Sprint 10.5).
        private const string GreetingToken = "calradia_remembers_greeting";

        // Clean greeting texts (no [if:...] tag). The dialogue line shows text+tag
        // for the facial animation; this clean copy is appended to the chat window,
        // because the recorder captures the line too late to seed it.
        private const string GreetWarmText = "Ah, it is good to see you. Come — what weighs on your mind?";
        private const string GreetNeutralText = "Yes? What is it you wished to discuss?";
        private const string GreetColdText = "You. State your business, and be brief about it.";

        // Set by the chosen greeting line just before the chat opens, consumed when
        // the chat window is seeded.
        private string _pendingGreeting;

        public void Register(CampaignGameStarter starter)
        {
            // 0. Letter delivery: only shows when the player carries an outstanding
            //    message addressed to THIS hero. Selecting it marks the errand delivered
            //    and opens the chat so the recipient can react.
            starter.AddPlayerLine(
                "calradia_remembers_deliver_letter",
                "hero_main_options",
                "calradia_remembers_dummy_token",
                "I bear a message for you, carried on another's behalf.",
                CanDeliverLetter,
                DeliverLetterAndOpenChat,
                115);                                // above "We need to talk..."

            // 1. Player opens the interaction → routes to the greeting token so the
            //    NPC can react with an expression matching the current relation.
            starter.AddPlayerLine(
                "calradia_remembers_open_chat",      // ID unique
                "hero_main_options",                 // input (où apparaît l'option)
                GreetingToken,
                "We need to talk...",
                IsHeroEligible,
                null,
                110);                                // priorité haute pour apparaître en haut

            // 2. NPC greeting, gated by relation. The trailing [if:convo_*] tag is a
            //    native facial-animation directive — the engine applies it to the
            //    conversation agent (same mechanism vanilla uses to show anger when
            //    threatened) and it persists on the background NPC while the chat is
            //    open. The greeting's consequence opens the chat directly, so no
            //    second click is needed; output routes to a non-existent token
            //    (proven pattern — avoids the auto-trigger-first-option bug).
            starter.AddDialogLine(
                "calradia_remembers_greet_warm",
                GreetingToken, "calradia_remembers_dummy_token",
                GreetWarmText + "[if:convo_calm_friendly]",
                () => MoodForConversationHero() == GreetingMood.Warm,
                () => OpenChatWithGreeting(GreetWarmText));
            starter.AddDialogLine(
                "calradia_remembers_greet_neutral",
                GreetingToken, "calradia_remembers_dummy_token",
                GreetNeutralText + "[if:convo_normal]",
                () => MoodForConversationHero() == GreetingMood.Neutral,
                () => OpenChatWithGreeting(GreetNeutralText));
            starter.AddDialogLine(
                "calradia_remembers_greet_cold",
                GreetingToken, "calradia_remembers_dummy_token",
                GreetColdText + "[if:convo_stern]",
                () => MoodForConversationHero() == GreetingMood.Cold,
                () => OpenChatWithGreeting(GreetColdText));
        }

        #region private

        private enum GreetingMood
        {
            Warm,
            Neutral,
            Cold
        }

        /// <summary>
        ///   Maps the NPC's PERSONAL regard (our per-hero score) to a coarse greeting
        ///   mood — so a noble can greet the player warmly despite their clan's enmity
        ///   (secret allies/lovers), or coldly despite the clan being friendly. A later
        ///   sprint can refine this into finer bands (−1..−25 vs −26..−50) with distinct
        ///   expressions. New NPCs (no profile yet) default to Neutral.
        /// </summary>
        private static GreetingMood MoodForConversationHero()
        {
            Hero hero = Hero.OneToOneConversationHero;
            if (hero == null || NpcMemoryCampaignBehavior.Instance == null) return GreetingMood.Neutral;

            NpcProfile profile = NpcMemoryCampaignBehavior.Instance.Store.Get(hero.StringId);
            int personal = profile?.ReputationWithPlayer ?? 0;
            if (personal >= 10) return GreetingMood.Warm;
            if (personal <= -10) return GreetingMood.Cold;
            return GreetingMood.Neutral;
        }

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
            fresh.Relationships = RelationshipsBuilder.Build(hero);
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

        /// <summary>
        ///   True when the player is carrying an undelivered message addressed to the
        ///   current conversation hero.
        /// </summary>
        private bool CanDeliverLetter()
        {
            if (!IsHeroEligible()) return false;
            if (NpcMemoryCampaignBehavior.Instance == null) return false;

            Hero recipient = Hero.OneToOneConversationHero;
            return QuestService.FindOutstandingLetterFor(
                recipient, NpcMemoryCampaignBehavior.Instance.Store) != null;
        }

        /// <summary>
        ///   Marks the letter delivered the instant the player hands it over, then opens
        ///   the chat so the recipient can respond. The reward stays with the giver — the
        ///   player collects it by reporting back to them later.
        /// </summary>
        private void DeliverLetterAndOpenChat()
        {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            Hero recipient = Hero.OneToOneConversationHero;
            if (behavior == null || recipient == null) { OnPlayerSelectedChat(); return; }

            LetterErrand errand = QuestService.FindOutstandingLetterFor(recipient, behavior.Store);
            if (errand != null)
            {
                int day = behavior.GameStateBridge.GetCurrentGameDay();
                QuestService.MarkLetterDelivered(errand, recipient, day, behavior.Store);
                _pendingGreeting = "*You hand over the sealed letter.* So — word reaches me at last. " +
                                   "Let us speak of what it carries.";
            }

            OnPlayerSelectedChat();
        }

        /// <summary>
        ///   Stashes the chosen greeting (so it can be appended to the chat after the
        ///   captured vanilla lines) and opens the chat window.
        /// </summary>
        private void OpenChatWithGreeting(string greeting)
        {
            _pendingGreeting = greeting;
            OnPlayerSelectedChat();
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

                if (vm != null)
                {
                    SeedCapturedVanillaLinesIntoChat(vm);
                    // Show both scores in the header from the first frame.
                    vm.SetRelationSummary(
                        Profile.ReputationWithPlayer,
                        behavior.GameStateBridge.GetPlayerRelation(hero));
                }

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

            // Append the relation-gated greeting as the most recent NPC line. The
            // recorder captures it too late to be in 'captured', so we add it here.
            if (!string.IsNullOrEmpty(_pendingGreeting))
            {
                vm.AppendNpcMessage(_pendingGreeting);
                Session.AddNpcMessage(_pendingGreeting);
                _pendingGreeting = null;
            }
        }

        #endregion
    }
}
