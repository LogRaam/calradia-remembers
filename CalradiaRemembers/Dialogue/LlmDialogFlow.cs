// Code written by Gabriel Mailhot, 13/06/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Storage;
using CalradiaRemembers.Witnesses;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

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
   ///   Sprint 7.6.F final — close_window + PlayerEncounter.Finish:
   ///   - close_window: clean exit from the vanilla dialogue state machine
   ///   (avoids the "auto-trigger first option" bug we hit when looping
   ///   back to hero_main_options).
   ///   - PlayerEncounter.Finish: dismisses the Attack/Leave overlay so the
   ///   chat window is the only active UI.
   ///   - ChatManager pauses campaign time while the chat is open.
   ///   Sprint 8.3 polish — auto-upgrade on existing profile load:
   ///   - Existing profiles from earlier sprints (pre-8.2 RomanticProfile,
   ///   pre-8.1 archetype catalog) are silently brought up to date when
   ///   loaded. Idempotent — no-op for already up-to-date profiles.
   /// </summary>
   public sealed class LlmDialogFlow
   {
      private const string GreetColdText = "You. State your business, and be brief about it.";

      // Dialogue token for the relation-gated greeting that sets the NPC's
      // facial expression before the chat window opens (Sprint 10.5).
      private const string GreetingToken = "calradia_remembers_greeting";
      private const string GreetNeutralText = "Yes? What is it you wished to discuss?";

      // Fixed greeting texts (no [if:...] tag). The native dialogue line shows text+tag
      // for the facial animation — it sits hidden behind the chat overlay. The chat window
      // itself (and the story log) gets a varied line from PickGreeting; these constants
      // are the native default and the fail-soft fallback for each mood.
      private const string GreetWarmText = "Ah, it is good to see you. Come — what weighs on your mind?";

      // The vanilla menu line the player clicks to open a CR conversation. Deliberately NEUTRAL:
      // a loaded opener like "We need to talk..." was seeded into the LLM session as the player's
      // first line, so the NPC would insist the player was hiding something (or that a mundane
      // question "seemed important") even when they only asked about the weather. This line is now
      // also SKIPPED when seeding the chat session (see SeedCapturedVanillaLinesIntoChat), so it
      // never reaches the model as a substantive statement, only opens the floor.
      private const string OpenChatOptionText = "A word with you.";

      // Set by the chosen greeting line just before the chat opens, consumed when
      // the chat window is seeded.
      private string _pendingGreeting;

      private enum GreetingMood
      {
         Warm,
         Neutral,
         Cold
      }

      public EncounterContext Context { get; private set; }
      public NpcProfile Profile { get; private set; }
      public ChatSession Session { get; private set; }

      // The vanilla conversation hub tokens where the CR entry option must appear. `hero_main_options`
      // is the ordinary hero-conversation hub; `alley_player_owned_start` is the hub of the player-owned
      // alley manager conversation (a clan member you assigned to an alley), which branches straight from
      // "start" and never passes through hero_main_options — so without this a player reported the
      // "A word with you." option was missing when speaking with the clan member running an alley
      // (SandBox AlleyCampaignBehavior; the alley management options share this input token).
      private static readonly string[] EntryHubTokens =
         {"hero_main_options", "alley_player_owned_start"};

      public void Register(CampaignGameStarter starter)
      {
         for (int i = 0; i < EntryHubTokens.Length; i++)
         {
            string hub = EntryHubTokens[i];

            // 0. Letter delivery: only shows when the player carries an outstanding
            //    message addressed to THIS hero. Selecting it marks the errand delivered
            //    and opens the chat so the recipient can react.
            starter.AddPlayerLine(
               $"calradia_remembers_deliver_letter_{i}", // ID unique per hub
               hub,
               "calradia_remembers_dummy_token",
               "I bear a message for you, carried on another's behalf.",
               CanDeliverLetter,
               DeliverLetterAndOpenChat,
               115); // above the open-chat option

            // 1. Player opens the interaction → routes to the greeting token so the
            //    NPC can react with an expression matching the current relation.
            starter.AddPlayerLine(
               $"calradia_remembers_open_chat_{i}", // ID unique per hub
               hub, // input (où apparaît l'option)
               GreetingToken,
               OpenChatOptionText,
               IsHeroEligible,
               null,
               110); // priorité haute pour apparaître en haut
         }

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
            () => OpenChatWithGreeting(PickGreeting(GreetingMood.Warm)));
         starter.AddDialogLine(
            "calradia_remembers_greet_neutral",
            GreetingToken, "calradia_remembers_dummy_token",
            GreetNeutralText + "[if:convo_normal]",
            () => MoodForConversationHero() == GreetingMood.Neutral,
            () => OpenChatWithGreeting(PickGreeting(GreetingMood.Neutral)));
         starter.AddDialogLine(
            "calradia_remembers_greet_cold",
            GreetingToken, "calradia_remembers_dummy_token",
            GreetColdText + "[if:convo_stern]",
            () => MoodForConversationHero() == GreetingMood.Cold,
            () => OpenChatWithGreeting(PickGreeting(GreetingMood.Cold)));
      }

      #region private

      /// <summary>
      ///   Adds greeting variants flavored by the NPC's pronounced traits, in a register
      ///   that fits the mood. This is the "varies by traits" axis on top of the relation
      ///   axis — a calculating lord and a generous one open differently even at the same
      ///   regard. Silent when the hero or their traits cannot be read.
      /// </summary>
      private static void AddTraitGreetings(List<string> candidates, GreetingMood mood, Hero hero)
      {
         if (hero == null) return;

         int calculating = SafeTrait(hero, DefaultTraits.Calculating);
         int honor = SafeTrait(hero, DefaultTraits.Honor);
         int mercy = SafeTrait(hero, DefaultTraits.Mercy);
         int valor = SafeTrait(hero, DefaultTraits.Valor);
         int generosity = SafeTrait(hero, DefaultTraits.Generosity);

         if (mood == GreetingMood.Cold)
         {
            // Harsher register — only traits that read plausibly cold.
            if (calculating > 0) candidates.Add("You want something — everyone does. Name it, and we shall see.");
            if (calculating > 0) candidates.Add("Whatever you want, it will cost you. Speak.");
            if (mercy < 0) candidates.Add("What is it? Make it worth the interruption.");
            if (mercy < 0) candidates.Add("Quickly — my patience is a short road, and you are testing it.");
            if (valor > 0) candidates.Add("Speak up. I have no use for hesitation.");
            if (honor < 0) candidates.Add("Hah. And what scheme brings you to me this time?");

            return;
         }

         // Warm and Neutral share a calmer register.
         if (calculating > 0) candidates.Add("Yes? I trust you have not come to waste either of our time. What is it?");
         if (calculating > 0) candidates.Add("Let us be efficient about this — what is it you want?");
         if (honor > 0) candidates.Add("Speak plainly with me, and you will have a plain answer.");
         if (honor > 0) candidates.Add("I will deal with you straight, as I always have. What is it?");
         if (generosity > 0) candidates.Add("Come now — what is it you need? I will hear you out.");
         if (generosity > 0) candidates.Add("Whatever I can spare is yours to ask for. Go on.");
         if (valor > 0) candidates.Add("Out with it, then — I respect one who comes straight to the point.");
         if (mercy > 0) candidates.Add("You look as though something weighs on you. Tell me of it.");
         if (calculating < 0) candidates.Add("Ha, no need for cleverness here — just tell me what you need.");
      }

      /// <summary>The relation-keyed base pool — the variety that does not depend on traits.</summary>
      private static string[] BaseGreetings(GreetingMood mood) => mood switch {
         GreetingMood.Warm => new[] {
            GreetWarmText,
            "There you are. I had hoped you would pass this way — speak freely.",
            "Well met, my friend. Tell me what brings you.",
            "You are a welcome sight. What would you have of me?",
            "Good, it is you. I always have a moment for you. What is it?",
            "It gladdens me to see your face. Sit — what shall we speak of?",
            "You honor me with your company. Say what is on your mind.",
            "Ha! The very person I would have wished to see. Go on, then.",
            "Come, come — no need to stand on ceremony with me. What is it?",
            "Always a pleasure. Tell me, what brings you to my side today?",
            "I am glad you came. Whatever you need, you have but to ask.",
            "Friend! Set your worries down a moment and tell me how I can help.",
            "Ah, a familiar face in a hard season. What can I do for you?",
            "Welcome, welcome. My door is open to you — what is it you seek?"
         },
         GreetingMood.Cold => new[] {
            GreetColdText,
            "What do you want? I have little patience to spare.",
            "Make it quick — I did not ask for your company.",
            "Speak, and do not waste my time.",
            "What now? Say it and be done.",
            "I did not expect you, and I cannot say I am pleased. What is it?",
            "You again. Say what you came to say.",
            "Be brief. I have better things to do than entertain you.",
            "What could you possibly want from me? Out with it.",
            "I will hear you, but spare me the pleasantries.",
            "Whatever it is, say it plainly and then leave me be.",
            "You have some nerve, coming to me. Speak, then.",
            "I have neither time nor goodwill for you. Make this matter.",
            "So. You dare show your face. What is it you want?"
         },
         _ => new[] {
            GreetNeutralText,
            "You have my ear. Speak.",
            "What brings you to me?",
            "Say your piece, then.",
            "I am listening. What is it?",
            "Well? What would you have of me?",
            "Go on — I have a moment. What is it?",
            "Speak your mind. I will hear it.",
            "You wished to see me? Here I am.",
            "What can I do for you?",
            "Out with it, then. What do you need?",
            "I am at your disposal, for the moment. What is it?",
            "State your business and I will give you my answer.",
            "You have my attention. Use it well."
         }
      };

      private static string DefaultGreeting(GreetingMood mood) => mood switch {
         GreetingMood.Warm => GreetWarmText,
         GreetingMood.Cold => GreetColdText,
         _ => GreetNeutralText
      };

      /// <summary>
      ///   Maps the NPC's PERSONAL regard (our per-hero score) to a coarse greeting
      ///   mood — so a noble can greet the player warmly despite their clan's enmity
      ///   (secret allies/lovers), or coldly despite the clan being friendly. A later
      ///   sprint can refine this into finer bands (−1..−25 vs −26..−50) with distinct
      ///   expressions. New NPCs (no profile yet) default to Neutral.
      /// </summary>
      private static GreetingMood MoodForConversationHero()
      {
         var hero = Hero.OneToOneConversationHero;

         if (hero == null || NpcMemoryCampaignBehavior.Instance == null) return GreetingMood.Neutral;

         // M-C1 (regard audit): a dominant live VENDETTA against the player forces a cold greeting, whatever the
         // regard number reads. Otherwise a hidden vendetta beside a mildly positive regard produced a WARM
         // opening line, flatly contradicting the very prompt that says whatever warmth is shown is a mask.
         var today = (int) CampaignTime.Now.ToDays;
         IReadOnlyList<Grudge> grudges = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);
         if (grudges != null)
            foreach (Grudge g in grudges)
               if (g != null && g.Rigidity >= GrudgePolicy.VendettaRigidity && g.TargetId == Hero.MainHero?.StringId)
                  return GreetingMood.Cold;

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance.Store.Get(hero.StringId);
         int personal = profile?.ReputationWithPlayer ?? 0;

         // M-C1: align the coarse greeting to RegardBands. Genuine warmth begins at "genuine regard" (>= 15); a
         // merely "cordial" +5..14 and neutral read as a Neutral greeting, not the warm one the old +10 band gave.
         if (personal >= 15) return GreetingMood.Warm;
         if (personal <= -20) return GreetingMood.Cold;

         return GreetingMood.Neutral;
      }

      private static int SafeTrait(Hero hero, TraitObject trait)
      {
         try { return hero.GetTraitLevel(trait); }
         catch { return 0; }
      }

      /// <summary>
      ///   True when the player is carrying an undelivered message addressed to the
      ///   current conversation hero.
      /// </summary>
      private bool CanDeliverLetter()
      {
         if (!IsHeroEligible()) return false;
         if (NpcMemoryCampaignBehavior.Instance == null) return false;

         var recipient = Hero.OneToOneConversationHero;

         return QuestService.FindOutstandingLetterFor(
                   recipient, NpcMemoryCampaignBehavior.Instance.Store) !=
                null;
      }

      /// <summary>
      ///   Marks the letter delivered the instant the player hands it over, then opens
      ///   the chat so the recipient can respond. The reward stays with the giver — the
      ///   player collects it by reporting back to them later.
      /// </summary>
      private void DeliverLetterAndOpenChat()
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;
         var recipient = Hero.OneToOneConversationHero;
         if (behavior == null || recipient == null)
         {
            OnPlayerSelectedChat();

            return;
         }

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

      private NpcProfile GetOrCreateProfile(Hero hero, BannerlordNpcMemoryStore store)
      {
         NpcProfile existing = store.Get(hero.StringId);
         if (existing != null)
         {
            ModLog.Info($"Loaded existing profile for {hero.Name} ({existing.Events.Count} events).");

            // Retrofit Romantic profile / stale Trait for profiles created
            // before Sprint 8.1 / 8.2 / 8.3. Idempotent — silent for current profiles.
            HeroProfileMapper.UpgradeIfNeeded(existing, hero);

            // Relationships reflect live game state (deaths, new feuds) — rebuild at
            // conversation open so a parent who died mid-session reads as dead now,
            // not at the next session launch.
            existing.Relationships = RelationshipsBuilder.Build(hero);

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
         var hero = Hero.OneToOneConversationHero;

         if (hero == null) return false;
         if (!hero.IsAlive) return false;
         if (hero == Hero.MainHero) return false;

         return true;
      }

      /// <summary>
      ///   Opens the ordinary CR conversation chat for <paramref name="hero" /> PROGRAMMATICALLY (no player menu
      ///   click), so a forced meeting (a lord who rode out to intercept the player) cannot be dodged by backing
      ///   out of the vanilla menu. Called from the staged-conversation opener while the map conversation with the
      ///   lord is live, so <see cref="Hero.OneToOneConversationHero" /> is that lord. Picks a relation-appropriate
      ///   greeting and drops any line the staging captured (there is no real vanilla exchange to carry over).
      /// </summary>
      public void OpenChatForInterceptingHero(Hero hero)
      {
         try
         {
            Hero target = hero ?? Hero.OneToOneConversationHero;
            if (target == null)
            {
               ModLog.Error("[INTERCEPT] no hero to open the meeting chat with.");

               return;
            }

            // Nothing real was said before this staged meeting; drop whatever the recorder caught from the
            // staging line so it is not seeded into the chat as a spoken line.
            NpcMemoryCampaignBehavior.Instance?.DialogueRecorder?.Clear();

            // No canned greeting: the lord opens with an LLM-generated line via the NPC-led opener below, so he
            // does not both greet blandly AND then raise why he rode out.
            _pendingGreeting = null;
            OnPlayerSelectedChat();
            ChatManager.ActiveViewModel?.ActivateInterceptionOpener();
         }
         catch (Exception ex) { ModLog.Error($"[INTERCEPT] OpenChatForInterceptingHero failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnPlayerSelectedChat()
      {
         ModLog.Info("=== OnPlayerSelectedChat STARTED ===");
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            ModLog.Info($"NpcMemoryCampaignBehavior.Instance found: {behavior != null}");
            if (behavior == null)
            {
               ModLog.Error("Behavior is null - cannot open chat");

               return;
            }

            var hero = Hero.OneToOneConversationHero;
            ModLog.Info($"Hero.OneToOneConversationHero = {hero?.Name}");
            if (hero == null)
            {
               ModLog.Error("No OneToOneConversationHero");

               return;
            }

            Profile = GetOrCreateProfile(hero, behavior.Store);
            // resolveInterception:false, since this open-time context is never fed to the model (each LLM turn rebuilds
            // its own), so it must not consume the one-shot interception directive before the first turn does.
            Context = EncounterContextBuilder.Build(hero, Profile, behavior.GameStateBridge, resolveInterception: false);
            Session = new ChatSession();

            string npcName = hero.Name?.ToString() ?? "the lord";
            ModLog.Info($"Attempting to open chat for NPC: {npcName}");

            ChatManager.Open(npcName);
            ModLog.Info("ChatManager.Open() has been called");

            ChatViewModel vm = ChatManager.ActiveViewModel;
            ModLog.Info($"ChatManager.ActiveViewModel = {(vm != null ? "OK" : "NULL")}");

            if (vm != null)
            {
               SeedCapturedVanillaLinesIntoChat(vm, behavior);
               // Show both scores in the header from the first frame.
               vm.SetRelationSummary(
                  Profile.ReputationWithPlayer,
                  behavior.GameStateBridge.GetPlayerRelation(hero));

               // ── Witness resolution ────────────────────────────────────
               // Auto-detect heroes plausibly present (sync, safe — returns
               // empty on failure; same-location + social filters applied).
               List<WitnessEntry> autoWitnesses =
                  WitnessResolver.ResolveAutoWitnesses(hero, behavior.Store);
               if (autoWitnesses.Count > 0)
                  vm.SetWitnesses(autoWitnesses);

               // Companions: physical presence rules. If companions followed the
               // player into the loaded scene (Visit), they join automatically —
               // no picker, no dissonance. Otherwise (Talk, or the player came
               // alone) the picker offers to summon up to two, or go alone.
               // The player can later dismiss a companion in conversation — the
               // witness_leaves action handles it.
               List<WitnessEntry> sceneCompanions = WitnessResolver.ResolveSceneCompanions(hero);
               if (sceneCompanions.Count > 0)
                  foreach (WitnessEntry companion in sceneCompanions)
                     vm.AddWitness(companion);
               else
                  WitnessResolver.ShowCompanionPickerIfNeeded(hero, vm.AddWitness);
               // ─────────────────────────────────────────────────────────
            }

            ModLog.Info("=== OnPlayerSelectedChat COMPLETED SUCCESSFULLY ===");
         }
         catch (Exception ex)
         {
            ModLog.Error($"OnPlayerSelectedChat CRASHED: {ex.GetType().Name} - {ex.Message}");
            ModLog.Error($"StackTrace: {ex.StackTrace}");
         }
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

      /// <summary>
      ///   Picks the greeting shown in the chat window (and recorded to the story log) for
      ///   the given mood. The native dialogue overlay keeps its fixed line for the facial
      ///   animation tag, but it sits hidden behind the chat overlay — so varying only this
      ///   chat-window line removes the "always the same opener" repetition without an LLM
      ///   call or touching the engine's synchronous greeting. The candidate set is the
      ///   mood's base pool plus a few lines flavored by the NPC's dominant traits; one is
      ///   chosen at random. Fully fail-soft: any error falls back to the fixed line.
      /// </summary>
      private string PickGreeting(GreetingMood mood)
      {
         try
         {
            var candidates = new List<string>(BaseGreetings(mood));
            AddTraitGreetings(candidates, mood, Hero.OneToOneConversationHero);

            return candidates.Count == 0
               ? DefaultGreeting(mood)
               : candidates[MBRandom.RandomInt(candidates.Count)];
         }
         catch
         {
            return DefaultGreeting(mood);
         }
      }

      private void SeedCapturedVanillaLinesIntoChat(ChatViewModel vm, NpcMemoryCampaignBehavior behavior)
      {
         IReadOnlyList<VanillaDialogueRecorder.CapturedLine> captured
            = behavior.DialogueRecorder.GetCapturedLines();

         ModLog.Info($"Seeding {captured.Count} vanilla lines into chat");

         foreach (VanillaDialogueRecorder.CapturedLine line in captured)
         {
            // Skip the CR-opener menu line: it is a UI trigger (the player chose to start a
            // conversation), not a statement. Feeding it to the model made the NPC treat every
            // chat as if the player were withholding a weighty secret.
            if (line.IsPlayer && string.Equals(line.Text?.Trim(), OpenChatOptionText, StringComparison.Ordinal))
               continue;

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
         }

         behavior.DialogueRecorder.Clear();

         // Append the relation-gated greeting as the most recent NPC line. The
         // recorder captures it too late to be in 'captured', so we add it here.
         if (string.IsNullOrEmpty(_pendingGreeting)) return;
         vm.AppendNpcMessage(_pendingGreeting);
         Session.AddNpcMessage(_pendingGreeting);
         _pendingGreeting = null;
      }

      #endregion
   }
}