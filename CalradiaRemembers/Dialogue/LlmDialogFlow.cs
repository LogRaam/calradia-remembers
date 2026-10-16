// Code written by Gabriel Mailhot, 13/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Companions;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
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
using TaleWorlds.Localization;

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
      // Companion-briefing pillar (2026-09-07): the input token for the fixed NPC reply that follows the
      // player's request for a private word with his own people. Its own token, distinct from GreetingToken,
      // since its reply is a single fixed line (no relation-gated mood variants) and its output closes the
      // vanilla window outright (see Register) rather than opening the main chat.
      private const string BriefCompanionsToken = "calradia_remembers_brief_companions";

      /// <summary>
      ///   An output token nothing consumes, so the vanilla conversation with the LORD idles silently behind
      ///   the picker and the briefing chat instead of ending.
      ///   <para>
      ///     IT USED TO BE close_window, and that one word cost two days. Ending the conversation meant there
      ///     was nothing left to come back to, so the way back had to build a scene from nothing - and staging
      ///     an interior mission from the campaign map is the documented native-crash class in this codebase.
      ///     Gabriel's game died on it (2026-09-13): "[CHAT STAGE BriefedReturn] interior mission opening at
      ///     lordshall", then the crash reporter.
      ///   </para>
      ///   <para>
      ///     Idling is the SAME proven pattern the ordinary "A word with you" flow uses, and the one
      ///     ChatSceneStager uses for its own staged scenes: a token with no matching line leaves the engine's
      ///     conversation alive and quiet underneath. Then the way back is no journey at all - the meeting was
      ///     never left. The original reason for close_window was that no dialogue window should hang behind
      ///     the companion picker; an idling conversation does not hang, it waits, which is what every other
      ///     CR chat in the mod already does on top of one.
      ///   </para>
      /// </summary>
      private const string BriefCompanionsHoldToken = "calradia_remembers_brief_hold";

      /// <summary>
      ///   Set only for the length of a briefed return (see OpenChatForBriefedReturn): the companions to seat
      ///   instead of running the witness picker. Null on every ordinary open, which is unchanged.
      /// </summary>
      private List<Hero> _preSeatedBriefed;

      // Deliberately courteous and ordinary: asking to step aside with one's own people is socially normal, so
      // it breeds no suspicion (the design property the whole feature rests on, see LlmDialogFlow's own
      // header for the two rejected designs this replaces). The lord's reaction is the vanilla reply below,
      // never a bespoke NPC penalty (Gabriel's ruling #1).
      private const string BriefCompanionsOptionText = "Grant me a moment first, to speak with my own people.";
      private const string BriefCompanionsReplyText = "Of course. Take what time you need.";

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
      // Internal (not private): DialogueEntryTraceCollector reads this same array rather than keeping a
      // second copy, so there is exactly one place naming the hubs CR is registered on.
      internal static readonly string[] EntryHubTokens =
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

            // 0.5. Companion-briefing pillar: ask openly for a moment with one's own people, BEFORE "A word
            //      with you...", gated so it never appears as dead noise when the player has no companion to
            //      draw aside (ruling #4). Sits above the open-chat option so the player reads it first.
            starter.AddPlayerLine(
               $"calradia_remembers_brief_companions_{i}", // ID unique per hub
               hub,
               BriefCompanionsToken,
               BriefCompanionsOptionText,
               CanBriefCompanions,
               null,
               112);

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

         // 1.5. Companion-briefing pillar: the lord's fixed, courteous reply. Output is the real vanilla
         //      "close_window" token (not a dead-end placeholder): the conversation ends cleanly HERE, whatever
         //      the companion picker the consequence opens goes on to do (including the player picking nobody),
         //      so there is never a hung dialogue window waiting on a popup that may not open a chat at all.
         starter.AddDialogLine(
            "calradia_remembers_brief_companions_reply",
            BriefCompanionsToken, BriefCompanionsHoldToken,
            BriefCompanionsReplyText,
            null,
            OnPlayerRequestedCompanionBriefing);

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
         if (generosity > 0) candidates.Add(GreetingDeferencePolicy.WhateverICanSpare);
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
            GreetingDeferencePolicy.HonorMeWithYourCompany,
            "Ha! The very person I would have wished to see. Go on, then.",
            "Come, come — no need to stand on ceremony with me. What is it?",
            "Always a pleasure. Tell me, what brings you to my side today?",
            GreetingDeferencePolicy.WhateverYouNeed,
            GreetingDeferencePolicy.TellMeHowICanHelp,
            GreetingDeferencePolicy.FamiliarFaceWhatCanIDo,
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
            GreetingDeferencePolicy.WhatCanIDoForYou,
            "Out with it, then. What do you need?",
            GreetingDeferencePolicy.AtYourDisposal,
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
      ///   Ruling #4 gate: the option shows only when the player has a companion physically able to be drawn
      ///   aside for this word (the same pool the follow-up conversation's own witness picker offers). Does NOT
      ///   call <see cref="IsHeroEligible" />'s own entry-trace side effect: that diagnostic is scoped to the
      ///   MAIN "A word with you" option, and double-recording it here under a different meaning would muddy it.
      /// </summary>
      private bool CanBriefCompanions()
      {
         var hero = Hero.OneToOneConversationHero;

         if (hero == null || !hero.IsAlive || hero == Hero.MainHero) return false;

         return CompanionBriefingCoordinator.HasBriefableCompanions(hero);
      }

      /// <summary>
      ///   Consequence of the fixed courteous reply: the vanilla conversation has already closed cleanly
      ///   (BriefCompanionsToken's output is "close_window"), so this only needs to hand the still-live
      ///   <see cref="Hero.OneToOneConversationHero" /> to the picker. Captured here, by value, rather than
      ///   trusted to still read live once the picker's own async confirmation callback runs later.
      /// </summary>
      private void OnPlayerRequestedCompanionBriefing()
      {
         Hero target = Hero.OneToOneConversationHero;
         if (target == null) return;

         CompanionBriefingCoordinator.OfferPicker(target);
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

            // Age was never carried on an existing profile before this field existed, and a
            // fresh session-launch refresh could still be a stale in-game year old, so keep it
            // current from the next conversation on, same cadence as Relationships above.
            existing.Age = (int) hero.Age;

            // Realm/house identity, same live cadence: a hero who has since defected or joined a
            // runtime-created kingdom (e.g. a civil-war split from another mod) is introduced by
            // their CURRENT realm, not the one frozen when their profile was first built.
            HeroProfileMapper.RefreshIdentity(existing, hero);

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
         // This method's own EXECUTION proves the engine evaluated CR's player line while sitting at one
         // of EntryHubTokens (see the field's remarks): the "CR entry offered" diagnostic (player reports
         // of the option going missing or the overlay never opening) hooks in here, not on the return
         // value, since reaching this point is the fact worth recording either way.
         NpcMemoryCampaignBehavior.Instance?.EntryTraceCollector?.NoteEntryOffered();

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
      ///   lord is live, so <see cref="Hero.OneToOneConversationHero" /> is that lord, but also called directly
      ///   for a fief audience (FiefAudienceBehavior), which has no vanilla conversation at all: <paramref name="hero" />
      ///   is threaded through to <see cref="OnPlayerSelectedChat" /> explicitly rather than trusting the live
      ///   <see cref="Hero.OneToOneConversationHero" />, which is null in that path. Picks a relation-appropriate
      ///   greeting and drops any line the staging captured (there is no real vanilla exchange to carry over).
      /// </summary>
      /// <summary>
      ///   Reopens the meeting a companion briefing was for, with the briefed companions already seated as
      ///   FULL participants rather than onlookers.
      ///   <para>
      ///     Two things separate this from an ordinary open, and both are Gabriel's, from playing it on
      ///     2026-09-12. The witness picker must not run: these companions were already chosen when the
      ///     briefing was launched, and asking again made the player pick the same man twice. And they arrive
      ///     ACTIVE: he drew them aside and gave them an errand, so they are in this conversation on purpose,
      ///     not standing at the edge waiting to be named before they may speak.
      ///   </para>
      /// </summary>
      public void OpenChatForBriefedReturn(Hero target, List<Hero> briefed)
      {
         try
         {
            if (target == null)
            {
               ModLog.Error("[BRIEFING] no target to return to.");

               return;
            }

            NpcMemoryCampaignBehavior.Instance?.DialogueRecorder?.Clear();

            // No canned greeting: the target opens with an LLM line of their own (below), so they do not both
            // greet blandly AND then remark on the player's return. Same shape as the interception opener.
            _pendingGreeting = null;
            _preSeatedBriefed = briefed;

            try { OnPlayerSelectedChat(target); }
            finally { _preSeatedBriefed = null; }

            // The one who waited gets the first word. Without this the chat opened SILENT, which is what
            // Gabriel found: the hand-off landed, and then nobody said anything at all.
            string aside = NamesOf(briefed);
            ChatManager.ActiveViewModel?.ActivateBriefedReturnOpener(aside);

            ModLog.Info($"[BRIEFING] returned to {target.Name} with {briefed?.Count ?? 0} briefed companion(s).");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BRIEFING] OpenChatForBriefedReturn failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   The companions the player drew aside, named, for the return opener's cue. The target watched them
      ///   go, so naming them is not a leak - what was SAID is the secret, never who was in the room.
      /// </summary>
      private static string NamesOf(List<Hero> briefed)
      {
         if (briefed == null || briefed.Count == 0) return null;

         var names = new List<string>();
         foreach (Hero h in briefed)
         {
            string n = h?.Name?.ToString();
            if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
         }

         if (names.Count == 0) return null;
         if (names.Count == 1) return names[0];

         return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
      }

      /// <summary>
      ///   A written tale that began on the map (Gabriel, 10/10/2026): the player went to find the giver. The ordinary
      ///   conversation, opened as if the player had chosen to speak: no interception opener, no reason ridden out for.
      /// </summary>
      public void OpenChatForTaleMeeting(Hero hero)
      {
         try
         {
            if (hero == null)
            {
               ModLog.Error("[TALE] no giver to open the tale's conversation with.");

               return;
            }

            NpcMemoryCampaignBehavior.Instance?.DialogueRecorder?.Clear();
            _pendingGreeting = null;
            OnPlayerSelectedChat(hero);
         }
         catch (Exception ex) { ModLog.Error($"[TALE] OpenChatForTaleMeeting failed: {ex.GetType().Name}: {ex.Message}"); }
      }

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
            OnPlayerSelectedChat(target);
            ChatManager.ActiveViewModel?.ActivateInterceptionOpener();
         }
         catch (Exception ex) { ModLog.Error($"[INTERCEPT] OpenChatForInterceptingHero failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   <paramref name="heroOverride" /> lets a caller with no live vanilla conversation (a fief audience,
      ///   opened straight from a settlement-entry inquiry) name the target explicitly; every ordinary caller
      ///   passes null and keeps reading the live <see cref="Hero.OneToOneConversationHero" />, unchanged.
      /// </summary>
      private void OnPlayerSelectedChat(Hero heroOverride = null)
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

            var hero = heroOverride ?? Hero.OneToOneConversationHero;
            ModLog.Info($"Hero.OneToOneConversationHero = {hero?.Name}");
            if (hero == null)
            {
               ModLog.Error("No OneToOneConversationHero");

               return;
            }

            Profile = GetOrCreateProfile(hero, behavior.Store);
            // A new conversation owes nothing to the last one: whoever sought the player out then must not be
            // read as the seeker here (see InterceptionBehavior.SoughtThePlayerThisConversation).
            InterceptionBehavior.Instance?.ForgetConversationSeeker();
            // resolveInterception:false, since this open-time context is never fed to the model (each LLM turn rebuilds
            // its own), so it must not consume the one-shot interception directive before the first turn does.
            Context = EncounterContextBuilder.Build(hero, Profile, behavior.GameStateBridge, resolveInterception: false);
            Session = new ChatSession();

            string npcName = hero.Name?.ToString() ?? "the lord";
            ModLog.Info($"Attempting to open chat for NPC: {npcName}");

            // The one entry that is an ordinary conversation: the player walked up and started talking. It is
            // therefore the one that may pick up a conversation he stepped out of earlier today (fkasad,
            // 20/09/2026). Every other opener stages a scene of its own and takes the default.
            ChatManager.Open(npcName, opening: ChatOpening.OrdinaryConversation);
            ModLog.Info("ChatManager.Open() has been called");

            ChatViewModel vm = ChatManager.ActiveViewModel;
            ModLog.Info($"ChatManager.ActiveViewModel = {(vm != null ? "OK" : "NULL")}");

            if (vm != null)
            {
               // Make this path authoritative about WHO the NPC is. GenerateChatWindow only knows the live
               // vanilla Hero.OneToOneConversationHero, which is null for an opening we staged ourselves without a
               // vanilla conversation (a fief audience: the notable was simply standing there). Left null, the
               // main speaker carries a null Hero, so the portrait never renders (player report) AND the
               // per-turn ConsumeInterceptionReason(null) never fires, so the "you sought this meeting" directive
               // is lost (the NPC asks "what brings you" though THEY asked for the word) and the stashed
               // interception slot is never cleared (the daily pursuit-release later reports "lost track" of an
               // NPC the player already spoke to). Setting the resolved hero here fixes all three at once, and is
               // idempotent for an ordinary Talk where hero == the conversation hero already set.
               vm.SetNpcHero(hero);

               SeedCapturedVanillaLinesIntoChat(vm, behavior);
               // Gabriel (08/10/2026): a conversation that is part of a written tale opens on its stage's picture.
               if (Quests.AuthoredQuestBehavior.Instance?.ConversationImageFor(hero) is (string talePicture, string taleTitle))
                  vm.ShowSceneImage(talePicture, taleTitle);
               // Show both scores in the header from the first frame.
               Logic.GameRelationFacts relations = GameRelations.Of(hero);
               vm.SetRelationSummary(
                  Profile.ReputationWithPlayer,
                  Logic.GameRelationReading.ClanStanding(relations),
                  Logic.GameRelationReading.OwnRelationIfDistinct(relations));

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
               // A briefed return seats exactly the people the player drew aside, and never asks again: they
               // were chosen at the briefing. They also arrive ACTIVE - see OpenChatForBriefedReturn.
               if (_preSeatedBriefed is {Count: > 0})
               {
                  foreach (Hero companion in _preSeatedBriefed)
                  {
                     if (companion == null) continue;
                     vm.AddWitness(WitnessResolver.EntryForCompanion(companion));

                     if (vm.PromoteForDiagnostics(companion.StringId))
                        ModLog.Info($"[SPOTLIGHT] {companion.Name} returns from the briefing as a full participant.");
                  }
               }
               else
               {
                  List<WitnessEntry> sceneCompanions = WitnessResolver.ResolveSceneCompanions(hero);
                  if (sceneCompanions.Count > 0)
                     foreach (WitnessEntry companion in sceneCompanions)
                        vm.AddWitness(companion);
                  else
                     WitnessResolver.ShowCompanionPickerIfNeeded(hero, vm.AddWitness);
               }
               // ─────────────────────────────────────────────────────────
            }

            ModLog.Info("=== OnPlayerSelectedChat COMPLETED SUCCESSFULLY ===");
         }
         catch (Exception ex)
         {
            ModLog.Error($"OnPlayerSelectedChat CRASHED: {ex.GetType().Name} - {ex.Message}");
            ModLog.Error($"StackTrace: {ex.StackTrace}");
            // Never let a broken persisted record silently kill EVERY conversation (player report: after a
            // soft-locked council scene, "A word with you" only ever gave the vanilla continue). Tell the player
            // it was CR that failed and hand them the recovery command, instead of a mute dead-end with no clue.
            MBInformationManager.AddQuickInformation(new TextObject(
               "Calradia Remembers could not open this conversation. Type 'cr.reset' in the console, then try again. See CalradiaRemembers.log."));
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
      /// <summary>
      ///   The greetings <paramref name="hero" /> may open with in this mood: the relation pool and the trait lines,
      ///   less the lines that offer service when the speaker outranks the player (tashmetu, 16/10/2026).
      /// </summary>
      private static List<string> Candidates(GreetingMood mood, Hero hero)
      {
         var candidates = new List<string>(BaseGreetings(mood));
         AddTraitGreetings(candidates, mood, hero);
         if (hero == null) return candidates;

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId);

         return GreetingDeferencePolicy.Filter(candidates, GreetingDeferencePolicy.MayBeDeferential(Letters.CorrespondenceRank.Facts(hero, profile, null)));
      }

      /// <summary>The cr.testall seam: every greeting <paramref name="hero" /> may open with in each mood, and whether they may defer.</summary>
      internal static (List<string> Lines, bool MayDefer) DebugCandidates(Hero hero)
      {
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId);
         bool mayDefer = GreetingDeferencePolicy.MayBeDeferential(Letters.CorrespondenceRank.Facts(hero, profile, null));
         var lines = new List<string>();
         foreach (GreetingMood mood in new[] {GreetingMood.Warm, GreetingMood.Neutral, GreetingMood.Cold}) lines.AddRange(Candidates(mood, hero));

         return (lines, mayDefer);
      }

      private string PickGreeting(GreetingMood mood)
      {
         try
         {
            List<string> candidates = Candidates(mood, Hero.OneToOneConversationHero);

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

         // The two channels are deliberately NOT the same any more. The player still SEES the vanilla exchange
         // in the window, which is the visible bridge between the two conversations. The MODEL gets it as one
         // block of history instead of as turns (Session.AddPriorExchange), because seeding it as turns is what
         // made a wanderer restart the conversation at every reply: his nine-line vanilla backstory ritual opens
         // with the player saying "My name is X, tell me about yourself", so the exchange the model saw began
         // with an introduction and it spent the rest of the conversation answering it (player report and log,
         // 2026-09-08). Gabriel's ruling: the vanilla conversation is history, and its only value is to bridge.
         var history = new StringBuilder();
         string npcName = Hero.OneToOneConversationHero?.Name?.ToString() ?? "They";
         string playerName = Hero.MainHero?.Name?.ToString() ?? "You";

         foreach (VanillaDialogueRecorder.CapturedLine line in captured)
         {
            // Skip the CR-opener menu line: it is a UI trigger (the player chose to start a
            // conversation), not a statement. Feeding it to the model made the NPC treat every
            // chat as if the player were withholding a weighty secret.
            if (line.IsPlayer && string.Equals(line.Text?.Trim(), OpenChatOptionText, StringComparison.Ordinal))
               continue;

            if (line.IsPlayer) vm.AppendPlayerMessage(line.Text);
            else vm.AppendNpcMessage(line.Text);

            if (!string.IsNullOrWhiteSpace(line.Text))
               history.AppendLine($"{(line.IsPlayer ? playerName : npcName)}: {line.Text.Trim()}");
         }

         Session.AddPriorExchange(history.ToString());

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