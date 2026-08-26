// Code written by Gabriel Mailhot, 07/07/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Opens a captive encounter over a 3D conversation scene rather than over the world map. Call
   ///   <see cref="StageCaptiveEncounter" /> instead of <see cref="ChatManager.OpenForCaptiveEncounter" />; it
   ///   starts a conversation with the captor and, once the scene is loaded (polled by <see cref="Tick" /> from
   ///   the submodule's per-frame hook), opens the captive chat over it. Falls back to the direct map overlay if
   ///   there is no character to converse with or the conversation cannot start.
   /// </summary>
   public static class CaptiveSceneStager
   {
      // Fallback: if our intercept line never fires (another mod's start line won the token), open anyway once
      // the conversation has been live this long. ~1 second at 60 fps.
      private const int FallbackFrames = 60;

      /// <summary>
      ///   Grace given to <c>OpenMapConversation</c> to register its conversation before we conclude it did
      ///   nothing. This is NOT a timeout on the scene load: it is the shortest wait that lets a successful
      ///   call be observed, because a successful one sets <c>Hero.OneToOneConversationHero</c> at once and
      ///   only then loads a scene. A few frames, not seconds.
      ///   <para>
      ///     Player report (Nexus, 2026-07-19): "getting stuck in the loop of someone approaching me, but the
      ///     dialogue doesn't trigger, so it's an endless chain of them trying to talk to me." The fallback
      ///     below only runs once a conversation IS active, so a call that started none left the staging armed
      ///     forever, and because <see cref="IsStaging" /> gates InterceptionBehavior.PlayerIsFreeOnMap,
      ///     NOTHING could open again: not this meeting, not the next lord's, not a captive scene.
      ///   </para>
      /// </summary>
      private const int ConversationRegistrationFrames = 10;

      private static int _armedFrames;

      // Frames to let the conversation scene render after our intercept line fires, so the chat opens over a
      // fully set-up mission screen rather than mid-transition.
      private const int SettleFrames = 3;
      private static int _fallbackFrames;
      private static bool _lineReached;

      private static Pending _pending;
      private static int _settleFrames;

      /// <summary>Which direction a staged scene opens once its conversation scene is loaded.</summary>
      private enum StagedSceneKind
      {
         /// <summary>
         ///   The captor (NPC or bandit) summons the player, who is the captive.
         ///   <see cref="ChatManager.OpenForCaptiveEncounter" />.
         /// </summary>
         VictimEncounter,

         /// <summary>The player holds the NPC prisoner and drives the scene. <see cref="ChatManager.OpenForCaptorScene" />.</summary>
         CaptorScene,

         /// <summary>A plain forced meeting (a lord who intercepted the player): opens the ordinary CR conversation chat.</summary>
         PlainConversation,

         /// <summary>
         ///   A companion the player already leads asks for a word (grievance, retirement, a found-topic report).
         ///   <see cref="ChatManager.OpenForCompanionAudienceDirect" />. Staged like the rest so the audience opens
         ///   over a rendered scene with the companion present, not over the suspended map where a hidden sub-panel
         ///   (Memories/Compose) leaves ghost pixels behind.
         /// </summary>
         CompanionAudience
      }

      /// <summary>True while a conversation scene is loading and the captive chat is waiting to open over it.</summary>
      public static bool IsStaging => _pending != null;

      /// <summary>
      ///   True when the currently open chat sits over a mission this stager (or the council twin) opened but
      ///   whose conversation never started (the intercept line lost the "start" token to another mod): with no
      ///   conversation in progress, ChatManager.Close's EndConversation never runs, and it is the conversation
      ///   end that makes the engine tear a conversation mission down, so the player would be stranded in a
      ///   silent scene. Close consults and clears this to end that mission explicitly. Internal get/set: the
      ///   council stager arms it through its own OpenPending mirror.
      /// </summary>
      internal static bool StagedMissionNeedsEnd { get; set; }

      /// <summary>
      ///   Registers the intercept dialogue line: while a captive encounter is staging, this line wins the
      ///   conversation's "start" token (priority above the vanilla first-meeting intro, so "Who are you?"
      ///   never shows), and its consequence hands the conversation over to the captive chat. Routes to a
      ///   nonexistent output token so the vanilla conversation idles silently behind the chat until
      ///   ChatManager.Close ends it (the same proven pattern as the normal "A word with you" flow).
      /// </summary>
      public static void Register(CampaignGameStarter starter)
      {
         starter.AddDialogLine(
            "cr_captive_stage_intercept",
            "start", "cr_captive_stage_hold",
            "So. Here you are.",
            () => IsStaging
               && (_pending?.Kind == StagedSceneKind.VictimEncounter || _pending?.Kind == StagedSceneKind.CaptorScene),
            OnInterceptLineReached,
            200);

         // The same auto-open mechanism, for a plain (non-captive) forced meeting, e.g. a lord who rode out to
         // intercept the player. A neutral throwaway line wins "start" (covered by the chat within a few frames)
         // so the vanilla menu the player could dodge never shows.
         starter.AddDialogLine(
            "cr_intercept_stage_intercept",
            "start", "cr_captive_stage_hold",
            "You. A word, before you go any further.",
            () => IsStaging && _pending?.Kind == StagedSceneKind.PlainConversation,
            OnInterceptLineReached,
            200);

         // A companion the player leads, asking for the audience: a courteous opener in their own voice. Gender-
         // neutral (it flashes for a few frames before the chat covers it, but never assumes the player's gender).
         starter.AddDialogLine(
            "cr_audience_stage_intercept",
            "start", "cr_captive_stage_hold",
            "A moment of your time, if I may.",
            () => IsStaging && _pending?.Kind == StagedSceneKind.CompanionAudience,
            OnInterceptLineReached,
            200);
      }

      /// <summary>
      ///   Stages the captive encounter as a conversation scene. <paramref name="captor" /> is the real captor
      ///   hero (nemesis / lord); <paramref name="portraitCharacter" /> is the fallback troop face for a faceless
      ///   bandit captor. One of them must be non-null to load a scene; otherwise we open directly on the map.
      /// </summary>
      public static void StageCaptiveEncounter(
         Hero captor,
         NpcProfile captorProfile,
         List<WitnessEntry> witnesses,
         bool isCollective,
         CaptiveSceneIntent intent,
         CharacterObject portraitCharacter = null,
         Hero victimCompanionHero = null,
         bool playerCoerced = false)
      {
         var pending = new Pending {
            Kind = StagedSceneKind.VictimEncounter,
            Captor = captor,
            Profile = captorProfile,
            Witnesses = witnesses,
            IsCollective = isCollective,
            Intent = intent,
            PortraitCharacter = portraitCharacter,
            VictimCompanion = victimCompanionHero,
            PlayerCoerced = playerCoerced
         };

         CharacterObject partner = captor?.CharacterObject ?? portraitCharacter;
         PartyBase captorParty =
            captor?.PartyBelongedTo?.Party ?? Hero.MainHero?.PartyBelongedToAsPrisoner?.MobileParty?.Party;

         BeginStagedConversation(pending, partner, captorParty, "CAPTIVE STAGE");
      }

      /// <summary>
      ///   Stages a PLAYER-AS-CAPTOR scene (the inversion: <paramref name="prisonerHero" /> is the
      ///   player's own prisoner) over the same VRAM-safe conversation scene as
      ///   <see cref="StageCaptiveEncounter" />, instead of a bare overlay on the world map. Call this
      ///   rather than <see cref="ChatManager.OpenForCaptorScene" /> directly whenever a real prisoner is
      ///   involved (a nemesis, a captured lord); the direct overlay call remains fine for the console
      ///   dev harness, which forces the framing on an NPC who need not truly be held.
      /// </summary>
      public static void StageCaptorScene(
         Hero prisonerHero,
         NpcProfile prisonerProfile,
         CaptiveSceneIntent intent = CaptiveSceneIntent.Interrogation)
      {
         var pending = new Pending {
            Kind = StagedSceneKind.CaptorScene,
            Captor = prisonerHero,
            Profile = prisonerProfile,
            Intent = intent
         };

         CharacterObject partner = prisonerHero?.CharacterObject;
         // The prisoner has no party of their own (they are held IN the player's), so the party the
         // conversation attaches them to is whichever PartyBase is actually holding them captive.
         PartyBase prisonerParty = prisonerHero?.PartyBelongedToAsPrisoner ?? PartyBase.MainParty;

         BeginStagedConversation(pending, partner, prisonerParty, "CAPTOR STAGE");
      }

      /// <summary>
      ///   Stages a PLAIN (non-captive) forced meeting with <paramref name="lord" />: a lord who rode out to
      ///   intercept the player. Opens the ordinary CR conversation chat AUTOMATICALLY over a VRAM-safe
      ///   conversation scene, so the player cannot dodge the meeting by backing out of a vanilla menu. The
      ///   interception opener is consumed when the chat's context is built, colouring the lord's first words.
      /// </summary>
      public static void StagePlainConversation(Hero lord)
      {
         var pending = new Pending {
            Kind = StagedSceneKind.PlainConversation,
            Captor = lord
         };

         // A road pursuer rides a field party; a fief audience target (a notable, a lady, a garrisoned lord who
         // stands in the settlement with no field party) has none, so attach the scene to the settlement they
         // stand in. If neither yields a party, BeginStagedConversation opens directly rather than stage nothing.
         PartyBase attachParty = lord?.PartyBelongedTo?.Party ?? lord?.CurrentSettlement?.Party;

         BeginStagedConversation(pending, lord?.CharacterObject, attachParty, "INTERCEPT STAGE");
      }

      /// <summary>
      ///   Stages a COMPANION AUDIENCE (a companion the player already leads asks for a word) over the same
      ///   VRAM-safe conversation scene as the captive paths, instead of a bare overlay on the world map. Call
      ///   this rather than <see cref="ChatManager.OpenForCompanionAudience" /> whenever there is no scene already
      ///   live: opening the audience over the suspended map left hidden sub-panels (Memories/Compose) painting
      ///   ghost pixels, because nothing repaints behind them. Over a staged scene the overlay redraws each frame,
      ///   exactly like a normal "Talk", and the companion stands present in the shot. Falls back to the direct
      ///   overlay when there is no companion to converse with or the conversation cannot start.
      /// </summary>
      public static void StageCompanionAudience(
         Hero companion, NpcProfile profile, CompanionAudienceReason reason, bool landed, string topicDetail = null)
      {
         var pending = new Pending {
            Kind = StagedSceneKind.CompanionAudience,
            Captor = companion, // the hero we converse with to load the scene (named for the captive paths this stager grew from)
            Profile = profile,
            Reason = reason,
            Landed = landed,
            TopicDetail = topicDetail
         };

         // A companion rides in the player's own party, so the scene attaches to the main party.
         BeginStagedConversation(pending, companion?.CharacterObject, companion?.PartyBelongedTo?.Party, "AUDIENCE STAGE");
      }

      /// <summary>
      ///   Pumped every frame from the submodule. Once the conversation scene is loaded (a Mission is live) and
      ///   the chat is not already open, opens the captive chat over the scene. Fail-soft: any error opens
      ///   directly so the encounter is never lost.
      /// </summary>
      public static void Tick()
      {
         if (_pending == null) return;
         if (ChatManager.IsOpen)
         {
            _pending = null; // something else opened the chat; drop the staging

            return;
         }

         // Primary path: our intercept line fired, meaning the conversation is set up and displaying. Give the
         // screen a few frames to settle, then open the chat over the loaded scene.
         if (_lineReached)
         {
            if (++_settleFrames < SettleFrames) return;
            OpenPending("intercept line");

            return;
         }

         // Fallback: the conversation is live (scene loading/loaded) but our line never fired — another mod's
         // start line may have won the token. Open anyway after a grace period so the encounter is never lost.
         bool conversationActive = Hero.OneToOneConversationHero != null || Mission.Current != null;

         // The STATE that says the call failed, checked before the conversation-active guard below: a
         // successful OpenMapConversation registers its conversation immediately and only then loads a
         // scene, so "the call returned and no conversation exists" IS the failure, not a slow load. React
         // to that rather than to a stopwatch: it fires in a fraction of a second instead of ten, and it
         // cannot mistake a slow scene load on a tired machine for a failure, because a slow load has
         // already set the conversation by this point.
         if (!conversationActive && ++_armedFrames >= ConversationRegistrationFrames)
         {
            ModLog.Error("[STAGE] OpenMapConversation registered no conversation; opening directly so the "
                       + "staging cannot jam every later meeting.");
            OpenPending("no conversation was registered");

            return;
         }

         if (!conversationActive) return;
         if (++_fallbackFrames < FallbackFrames) return;
         OpenPending("fallback (intercept line never fired)");
      }

      private sealed class Pending
      {
         public Hero Captor;
         public CaptiveSceneIntent Intent;
         public bool IsCollective;
         public StagedSceneKind Kind;
         public bool PlayerCoerced;
         public CharacterObject PortraitCharacter;
         public NpcProfile Profile;
         public Hero VictimCompanion;
         public List<WitnessEntry> Witnesses;

         // CompanionAudience only.
         public bool Landed;
         public CompanionAudienceReason Reason;
         public string TopicDetail;
      }

      #region private

      /// <summary>
      ///   Shared staging core for both directions: opens a map conversation with
      ///   <paramref name="partner" /> (their <paramref name="partnerParty" /> for scene attachment) and
      ///   arms the pending state <see cref="Tick" /> polls; falls back to the direct (unstaged) open when
      ///   there is no partner to converse with, no <see cref="Campaign.ConversationManager" />, or the
      ///   conversation fails to start. <paramref name="logTag" /> distinguishes the two directions in the log.
      /// </summary>
      private static void BeginStagedConversation(Pending pending, CharacterObject partner, PartyBase partnerParty, string logTag)
      {
         if (partner == null || Campaign.Current?.ConversationManager == null)
         {
            ModLog.Info($"[{logTag}] no conversation partner available, opening directly on the map.");
            OpenDirect(pending);

            return;
         }

         // Never ask the engine for a conversation scene while the player is at sea. A map conversation is
         // placed on the terrain under the party, and there is no scene for open water: the load never
         // completes and the game sits on a loading screen forever (two player reports on v1.30.16). The
         // direct overlay needs no scene at all, and stays VRAM-safe because ChatManager suspends the map
         // scene on every open, staged or not. This is the choke point, so it covers every staged path.
         if (MobileParty.MainParty?.IsCurrentlyAtSea == true)
         {
            ModLog.Info($"[{logTag}] the player is at sea, where no conversation scene can load, opening directly on the map.");
            OpenDirect(pending);

            return;
         }

         // Inside a settlement, never a MAP conversation: vanilla never starts one there (in-settlement talks
         // ride the settlement's own scene), and tearing that untested engine state down a frame after
         // ChatManager.Close crashes NATIVELY (player reports CarolusIV/Windmolen, 2026-08-25/26: a hard crash
         // on LEAVING an NPC-requested private audience, nothing in the log, still present on v2.2.4). Stage the
         // settlement's own interior mission instead, so the chat still gets a live Mission behind it (never the
         // bare map/menu overlay, the ghost-pixels class) and the close becomes the proven keep-talk one.
         if (PlayerEncounter.InsideSettlement)
         {
            BeginSettlementMissionConversation(pending, partner, logTag);

            return;
         }

         try
         {
            var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
            var partnerData = new ConversationCharacterData(partner, partnerParty);

            _pending = pending;
            _lineReached = false;
            _settleFrames = 0;
            _fallbackFrames = 0;
            _armedFrames = 0;
            StagedMissionNeedsEnd = false;
            Campaign.Current.ConversationManager.OpenMapConversation(playerData, partnerData);
            ModLog.Info($"[{logTag}] conversation started with {partner.StringId}; awaiting the scene to load.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[{logTag}] OpenMapConversation failed: {ex.GetType().Name}: {ex.Message}, opening directly on the map.");
            _pending = null;
            OpenDirect(pending);
         }
      }

      /// <summary>
      ///   The settlement twin of <see cref="BeginStagedConversation" /> (player reports CarolusIV/Windmolen,
      ///   2026-08-25/26: staging a MAP conversation from inside a settlement crashes natively when the chat
      ///   closes; vanilla never does that). Opens the settlement's OWN interior mission with the partner as the
      ///   talk-to character, the same engine path vanilla uses from settlement menus (the retirement retreat,
      ///   the arena, the alley quests): the conversation starts in the scene, our intercept line fires there
      ///   exactly like over the map, and ending it returns the player to the settlement menu, never ejecting
      ///   them from town. Falls back to the direct overlay only when no usable location/encounter exists or the
      ///   open throws.
      /// </summary>
      private static void BeginSettlementMissionConversation(Pending pending, CharacterObject partner, string logTag)
      {
         Location location = ResolveAudienceLocation();

         if (location == null || PlayerEncounter.LocationEncounter == null)
         {
            ModLog.Info($"[{logTag}] no interior location available in this settlement, opening directly.");
            OpenDirect(pending);

            return;
         }

         try
         {
            _pending = pending;
            _lineReached = false;
            _settleFrames = 0;
            _fallbackFrames = 0;
            _armedFrames = 0;
            StagedMissionNeedsEnd = false;
            PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(location, null, partner, null);
            ModLog.Info($"[{logTag}] interior mission opening at {location.StringId} with {partner.StringId}; awaiting the scene to load.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[{logTag}] interior mission open failed: {ex.GetType().Name}: {ex.Message}, opening directly.");
            _pending = null;
            OpenDirect(pending);
         }
      }

      /// <summary>
      ///   The interior location an in-settlement staged scene opens at: the lord's hall of a town or castle,
      ///   the village center of a village, the tavern as a last resort (every town has one). Null when the
      ///   settlement exposes no usable interior (a hideout camp, an exotic custom settlement), which the caller
      ///   turns into the direct-overlay fallback. Internal so the council stager reuses the exact same pick.
      /// </summary>
      internal static Location ResolveAudienceLocation()
      {
         LocationComplex complex = LocationComplex.Current;

         if (complex == null) return null;

         return complex.GetLocationWithId("lordshall")
                ?? complex.GetLocationWithId("village_center")
                ?? complex.GetLocationWithId("tavern");
      }

      /// <summary>Consequence of the intercept line: the conversation is ready; the tick opens the chat next frame.</summary>
      private static void OnInterceptLineReached()
      {
         _lineReached = true;
         ModLog.Info("[CAPTIVE STAGE] intercept line reached — the conversation is ready for the chat.");
      }

      private static void OpenDirect(Pending p)
      {
         if (p.Kind == StagedSceneKind.PlainConversation)
         {
            // Open the ordinary CR conversation for the lord, exactly as clicking "A word with you." would, but
            // programmatically so the meeting cannot be dodged. OneToOneConversationHero is the lord here.
            NpcMemoryCampaignBehavior.Instance?.DialogFlow?.OpenChatForInterceptingHero(p.Captor);

            return;
         }

         if (p.Kind == StagedSceneKind.CaptorScene)
         {
            ChatManager.OpenForCaptorScene(p.Captor, p.Profile, p.Intent);

            return;
         }

         if (p.Kind == StagedSceneKind.CompanionAudience)
         {
            // The DIRECT open: the choke that routed us here (ChatManager.OpenForCompanionAudience) stages, so
            // going back through it would re-arm the stager instead of opening the chat.
            ChatManager.OpenForCompanionAudienceDirect(p.Captor, p.Profile, p.Reason, p.Landed, p.TopicDetail);

            return;
         }

         ChatManager.OpenForCaptiveEncounter(
            p.Captor, p.Profile, p.Witnesses, p.IsCollective, p.Intent,
            p.PortraitCharacter, p.VictimCompanion, p.PlayerCoerced);
      }

      private static void OpenPending(string via)
      {
         Pending p = _pending;
         _pending = null;
         _lineReached = false;
         _settleFrames = 0;
         _fallbackFrames = 0;
         _armedFrames = 0;

         // If the chat opens over a mission whose conversation never started (the fallback path: our intercept
         // line lost the "start" token to another mod), no conversation exists for ChatManager.Close to end, and
         // it is the conversation end that makes the engine tear a conversation mission down. Flag the mission
         // as OURS so Close ends it explicitly instead of stranding the player in a silent scene.
         StagedMissionNeedsEnd = Mission.Current != null && Hero.OneToOneConversationHero == null;

         try
         {
            ModLog.Info($"[CAPTIVE STAGE] opening the captive chat over the scene via {via} " +
                        $"(Mission.Current={Mission.Current != null}, " +
                        $"convoHero={Hero.OneToOneConversationHero?.Name?.ToString() ?? "none"}).");
            OpenDirect(p);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CAPTIVE STAGE] opening over scene failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      #endregion
   }
}