// Code written by Gabriel Mailhot, 07/07/2026.
// Renamed from CaptiveSceneStager on 26/08/2026: it grew from the captive chat to host every CR conversation
// (captive, captor, plain forced meeting, companion audience, post-duel), so the name now reflects that.
// Open(ChatSceneRequest) is the SINGLE DOOR every map-originating CR chat enters, and it upholds the invariant
// that such a chat always opens over a live Mission, never as a bare overlay on the campaign map (which leaks
// VRAM and leaves a background that never repaints). The leaf opens on ChatManager (OpenForCaptorScene, ...)
// remain callable directly, but ONLY from a caller that is already inside a live conversation scene (a
// consequence run from within the running conversation, the captive-escape resume, the console harness); those
// self-certify the invariant. Everything that may run on the bare map goes through Open.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Duels;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Opens every CR chat over a 3D conversation scene rather than over the world map. Call
   ///   <see cref="Open" /> (or one of the Stage* builders that wrap it) instead of a ChatManager leaf open; it
   ///   starts a conversation with the partner and, once the scene is loaded (polled by <see cref="Tick" /> from
   ///   the submodule's per-frame hook), opens the chat over it. Falls back to the direct open only when a scene
   ///   is already live (clean), when the player is at sea (no scene can load), or when there is no partner.
   /// </summary>
   public static class ChatSceneStager
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

      // Frames to let the conversation scene render after our intercept line fires, so the chat opens over a
      // fully set-up mission screen rather than mid-transition.
      private const int SettleFrames = 3;

      // Close rings tried around the player to seat the companion beside them (a conversational distance, not a duel's).
      private static readonly float[] AudienceRingRadii = {2.5f, 3.5f, 4.5f, 6f};

      private static int _armedFrames;
      private static int _fallbackFrames;
      private static bool _lineReached;

      private static ChatSceneRequest _pending;
      private static int _settleFrames;

      /// <summary>StringId of a companion whose staged audience scene should bring them beside the player once its agents exist; null when none is pending.</summary>
      internal static string PendingAudienceCompanionId;

      /// <summary>True while a conversation scene is loading and the chat is waiting to open over it.</summary>
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
      ///   Registers the intercept dialogue line: while a scene is staging, this line wins the conversation's
      ///   "start" token (priority above the vanilla first-meeting intro, so "Who are you?" never shows), and
      ///   its consequence hands the conversation over to the chat. Routes to a nonexistent output token so the
      ///   vanilla conversation idles silently behind the chat until ChatManager.Close ends it (the same proven
      ///   pattern as the normal "A word with you" flow).
      /// </summary>
      public static void Register(CampaignGameStarter starter)
      {
         starter.AddDialogLine(
            "cr_captive_stage_intercept",
            "start", "cr_captive_stage_hold",
            "So. Here you are.",
            () => IsStaging
               && (_pending?.Kind == ChatSceneKind.VictimEncounter || _pending?.Kind == ChatSceneKind.CaptorScene),
            OnInterceptLineReached,
            200);

         // The same auto-open mechanism, for a plain (non-captive) forced meeting, e.g. a lord who rode out to
         // intercept the player. A neutral throwaway line wins "start" (covered by the chat within a few frames)
         // so the vanilla menu the player could dodge never shows.
         starter.AddDialogLine(
            "cr_intercept_stage_intercept",
            "start", "cr_captive_stage_hold",
            "You. A word, before you go any further.",
            () => IsStaging && _pending?.Kind == ChatSceneKind.PlainConversation,
            OnInterceptLineReached,
            200);

         // A companion the player leads, asking for the audience: a courteous opener in their own voice. Gender-
         // neutral (it flashes for a few frames before the chat covers it, but never assumes the player's gender).
         starter.AddDialogLine(
            "cr_audience_stage_intercept",
            "start", "cr_captive_stage_hold",
            "A moment of your time, if I may.",
            () => IsStaging && _pending?.Kind == ChatSceneKind.CompanionAudience,
            OnInterceptLineReached,
            200);

         // The meeting a briefing was for. The player asked for a moment and is coming back with his people;
         // the target has been waiting, so the opener is hers, not a fresh greeting.
         starter.AddDialogLine(
            "cr_briefed_return_intercept",
            "start", "cr_captive_stage_hold",
            "You have had your moment. Now, then.",
            () => IsStaging && _pending?.Kind == ChatSceneKind.BriefedReturn,
            OnInterceptLineReached,
            200);

         // The conversation that resumes right after a duel: a neutral opener wins "start" (covered by the chat
         // within a few frames), then the post-duel chat takes over with the victor's or the loser's own words.
         starter.AddDialogLine(
            "cr_postduel_stage_intercept",
            "start", "cr_captive_stage_hold",
            "The matter is settled. Now hear me.",
            () => IsStaging && _pending?.Kind == ChatSceneKind.PostDuel,
            OnInterceptLineReached,
            200);
      }

      /// <summary>
      ///   THE SINGLE DOOR. Opens the chat described by <paramref name="request" /> over a live Mission. When a
      ///   scene is already live, the chat opens directly over it (clean: it repaints each frame). Otherwise a
      ///   conversation scene is staged first, so the chat never opens over the bare map. Every entry point that
      ///   may run on the map routes here rather than calling a ChatManager leaf open directly.
      /// </summary>
      public static void Open(ChatSceneRequest request)
      {
         if (request == null) return;

         // A scene is already live: the chat opens cleanly over it, so there is nothing to stage. This is the
         // one generalization the door makes over the old per-path code (which only the companion audience did).
         if (Mission.Current != null)
         {
            OpenNow(request);

            return;
         }

         // THE SECOND HOST (13/09/2026). A Mission is not the only thing the chat can open over: a MAP
         // conversation is a live conversation with no Mission behind it, and the chat sits on it perfectly
         // well - it is what the companion briefing itself opened over, backdrop "MapConversation" on the map
         // screen. Reading only Mission.Current, this door concluded "nothing live" about a meeting the player
         // was standing in and STAGED one, which opens an interior mission from the campaign map: the native
         // crash of 13/09/2026, and before it the loading screen that went nowhere.
         //
         // Deliberately narrow: only when the live conversation is with THE VERY PERSON this request is for.
         // A live conversation with somebody else is not a host for this chat, it is a collision, and staging
         // remains the right answer there. That is why this asks about the partner rather than reusing the
         // stager's own looser "conversationActive" fallback (which is answering a different question: whether
         // a conversation IT started has registered yet).
         if (request.Partner != null && Hero.OneToOneConversationHero == request.Partner)
         {
            ModLog.Info($"[STAGE] {request.Kind}: a map conversation with {request.Partner.Name} is already "
                      + "live; opening over it rather than staging a scene.");
            OpenNow(request);

            return;
         }

         // Nothing live behind us (the world map or a settlement menu): stage a scene so the chat opens over a
         // rendered mission, never the bare map (the VRAM-leak / ghost-pixel class).
         BeginStagedConversation(request);
      }

      /// <summary>
      ///   Builds a VICTIM captive encounter request and stages it. <paramref name="captor" /> is the real captor
      ///   hero (nemesis / lord); <paramref name="portraitCharacter" /> is the fallback troop face for a faceless
      ///   bandit captor. One of them must be non-null to load a scene; otherwise the door opens directly.
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
         Open(new ChatSceneRequest {
            Kind = ChatSceneKind.VictimEncounter,
            Partner = captor,
            Profile = captorProfile,
            PortraitCharacter = portraitCharacter,
            // A captor with a field party attaches the scene to it; a captor with none (or a bandit) attaches to
            // whichever party is actually holding the player prisoner.
            AttachParty = captor?.PartyBelongedTo?.Party ?? Hero.MainHero?.PartyBelongedToAsPrisoner?.MobileParty?.Party,
            Captive = new CaptiveScenePayload {
               Witnesses = witnesses,
               IsCollective = isCollective,
               Intent = intent,
               VictimCompanion = victimCompanionHero,
               PlayerCoerced = playerCoerced
            }
         });
      }

      /// <summary>
      ///   Builds a PLAYER-AS-CAPTOR request (the inversion: <paramref name="prisonerHero" /> is the player's own
      ///   prisoner) and stages it over the same VRAM-safe scene as <see cref="StageCaptiveEncounter" />. Prefer
      ///   this over <see cref="ChatManager.OpenForCaptorScene" /> whenever a real prisoner is involved; the leaf
      ///   open remains fine for a caller already inside a live conversation scene and for the console harness.
      /// </summary>
      public static void StageCaptorScene(
         Hero prisonerHero,
         NpcProfile prisonerProfile,
         CaptiveSceneIntent intent = CaptiveSceneIntent.Interrogation)
      {
         Open(new ChatSceneRequest {
            Kind = ChatSceneKind.CaptorScene,
            Partner = prisonerHero,
            Profile = prisonerProfile,
            // The prisoner has no party of their own (they are held IN the player's), so the scene attaches to
            // whichever PartyBase is actually holding them captive.
            AttachParty = prisonerHero?.PartyBelongedToAsPrisoner ?? PartyBase.MainParty,
            Captive = new CaptiveScenePayload {Intent = intent}
         });
      }

      /// <summary>
      ///   Builds a PLAIN (non-captive) forced meeting with <paramref name="lord" /> (a lord who rode out to
      ///   intercept the player, a fief audience) and stages it. Opens the ordinary CR conversation chat
      ///   AUTOMATICALLY over a VRAM-safe scene, so the player cannot dodge the meeting by backing out of a
      ///   vanilla menu. The interception opener is consumed when the chat's context is built.
      /// </summary>
      public static void StagePlainConversation(Hero lord)
      {
         Open(new ChatSceneRequest {
            Kind = ChatSceneKind.PlainConversation,
            Partner = lord,
            // A road pursuer rides a field party; a fief audience target (a notable, a lady, a garrisoned lord
            // with no field party) has none, so attach the scene to the settlement they stand in.
            AttachParty = lord?.PartyBelongedTo?.Party ?? lord?.CurrentSettlement?.Party
         });
      }

      /// <summary>
      ///   Builds a COMPANION AUDIENCE request (a companion the player already leads asks for a word) and stages
      ///   it over the same VRAM-safe scene as the captive paths. Prefer this over
      ///   <see cref="ChatManager.OpenForCompanionAudience" />: opening the audience over the suspended map left
      ///   hidden sub-panels (Memories/Compose) painting ghost pixels, because nothing repaints behind them. Over
      ///   a staged scene the overlay redraws each frame, and the companion stands present in the shot.
      /// </summary>
      public static void StageCompanionAudience(
         Hero companion, NpcProfile profile, CompanionAudienceReason reason, bool landed, string topicDetail = null)
      {
         Open(new ChatSceneRequest {
            Kind = ChatSceneKind.CompanionAudience,
            Partner = companion,
            Profile = profile,
            // A companion rides in the player's own party, so the scene attaches to the main party.
            AttachParty = companion?.PartyBelongedTo?.Party,
            Audience = new CompanionAudiencePayload {Reason = reason, Landed = landed, TopicDetail = topicDetail}
         });
      }

      /// <summary>
      ///   Builds the BRIEFED-RETURN request: the meeting the player asked leave for, reopened with the
      ///   companions he drew aside already at his side.
      ///   <para>
      ///     Staged rather than opened directly, for the reason every other map-side entry is staged: the
      ///     briefing chat is closed and the conversation the player left was ENDED by the engine's own
      ///     close_window token, so there is nothing left to ride on. Staging also picks the map path from the
      ///     settlement path, which is not a nicety - staging a map conversation from inside a settlement
      ///     crashed natively (see StageOverSettlement).
      ///   </para>
      /// </summary>
      public static void StageBriefedReturn(Hero target, List<Hero> briefed)
         => Open(BuildBriefedReturn(target, briefed));

      /// <summary>
      ///   The request a briefed return stages, built apart from staging it. Separated so the live sweep can
      ///   assert WHAT the return asks for without a mission actually loading: the scene is the part only play
      ///   can prove, the request is the part that can be wrong quietly.
      /// </summary>
      public static ChatSceneRequest BuildBriefedReturn(Hero target, List<Hero> briefed)
         => new() {
            Kind = ChatSceneKind.BriefedReturn,
            Partner = target,
            AttachParty = target?.PartyBelongedTo?.Party ?? target?.CurrentSettlement?.Party,
            BriefedCompanions = briefed
         };

      /// <summary>
      ///   Builds a POST-DUEL request (the conversation that resumes right after a fought duel) and stages it.
      ///   <paramref name="opponent" /> is the surviving foe (duels are non-lethal); <paramref name="playerWon" />
      ///   colours the scene (the NPC concedes or prevails). <paramref name="priorConversation" /> is the exchange
      ///   that led to the duel, seeded into the new chat so it resumes seamlessly (null from a console-forced duel).
      /// </summary>
      public static void StagePostDuel(
         Hero opponent, NpcProfile profile, bool playerWon, DuelReason reason, Hero courtedHero, ConversationTranscript priorConversation)
      {
         Open(new ChatSceneRequest {
            Kind = ChatSceneKind.PostDuel,
            Partner = opponent,
            Profile = profile,
            // The opponent stands where they fought: attach to their field party, or to the settlement if they have none.
            AttachParty = opponent?.PartyBelongedTo?.Party ?? opponent?.CurrentSettlement?.Party,
            SeedTranscript = priorConversation,
            PostDuel = new PostDuelPayload {PlayerWon = playerWon, Reason = reason, CourtedHero = courtedHero}
         });
      }

      /// <summary>
      ///   Pumped every frame from the submodule. Once the conversation scene is loaded (a Mission is live) and
      ///   the chat is not already open, opens the chat over the scene. Fail-soft: any error opens directly so
      ///   the encounter is never lost.
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

         // Fallback: the conversation is live (scene loading/loaded) but our line never fired (another mod's
         // start line may have won the token). Open anyway after a grace period so the encounter is never lost.
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

      /// <summary>
      ///   Run from the Mission.AfterStart postfix (agents now exist): a staged COMPANION audience opens the
      ///   interior mission with the companion as the talk-to character, but the intercept-line hijack skips the
      ///   vanilla walk-up, leaving them at their spawn point out of view. Teleport them to a reachable spot beside
      ///   the player so the audience reads as a face-to-face. No-op unless one is pending; fail-soft (on any
      ///   trouble the companion simply stays where the engine put them). One-shot: the pending id is consumed here.
      /// </summary>
      public static void PlaceAudienceCompanionIfPending(Mission mission)
      {
         string id = PendingAudienceCompanionId;
         PendingAudienceCompanionId = null; // one-shot, whatever happens below

         if (string.IsNullOrEmpty(id) || mission?.MainAgent == null) return;

         try
         {
            Agent companion = null;
            foreach (Agent a in mission.Agents)
               if (a?.Character is CharacterObject co && co.HeroObject?.StringId == id)
               {
                  companion = a;

                  break;
               }

            if (companion == null) return; // the engine did not place them; nothing to reposition

            if (TryFindPointNearPlayer(mission.Scene, mission.MainAgent.GetWorldPosition(), out Vec3 dest))
            {
               companion.TeleportToPosition(dest);
               ModLog.Info($"[AUDIENCE STAGE] brought {companion.Name} beside the player for the audience.");
            }
         }
         catch (Exception ex) { ModLog.Info($"[AUDIENCE STAGE] could not place the companion ({ex.GetType().Name}); left where they stand."); }
      }

      #region private

      /// <summary>
      ///   Staging core: opens a map conversation with the request's partner (their <see cref="ChatSceneRequest.AttachParty" />
      ///   for scene attachment) and arms the pending state <see cref="Tick" /> polls; falls back to the direct
      ///   (unstaged) open when there is no partner to converse with, no <see cref="Campaign.ConversationManager" />,
      ///   or the conversation fails to start. Inside a settlement it hands off to the interior-mission twin.
      /// </summary>
      private static void BeginStagedConversation(ChatSceneRequest request)
      {
         CharacterObject partner = request.Partner?.CharacterObject ?? request.PortraitCharacter;

         if (partner == null || Campaign.Current?.ConversationManager == null)
         {
            ModLog.Info($"[CHAT STAGE {request.Kind}] no conversation partner available, opening directly on the map.");
            OpenNow(request);

            return;
         }

         // Never ask the engine for a conversation scene while the player is at sea. A map conversation is
         // placed on the terrain under the party, and there is no scene for open water: the load never
         // completes and the game sits on a loading screen forever (two player reports on v1.30.16). The
         // direct open needs no scene at all, and stays VRAM-safe because ChatManager suspends the map
         // scene on every open, staged or not. This is the choke point, so it covers every staged path.
         if (MobileParty.MainParty?.IsCurrentlyAtSea == true)
         {
            ModLog.Info($"[CHAT STAGE {request.Kind}] the player is at sea, where no conversation scene can load, opening directly on the map.");
            OpenNow(request);

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
            BeginSettlementMissionConversation(request, partner);

            return;
         }

         try
         {
            var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
            var partnerData = new ConversationCharacterData(partner, request.AttachParty);

            _pending = request;
            _lineReached = false;
            _settleFrames = 0;
            _fallbackFrames = 0;
            _armedFrames = 0;
            StagedMissionNeedsEnd = false;
            Campaign.Current.ConversationManager.OpenMapConversation(playerData, partnerData);
            ModLog.Info($"[CHAT STAGE {request.Kind}] conversation started with {partner.StringId}; awaiting the scene to load.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CHAT STAGE {request.Kind}] OpenMapConversation failed: {ex.GetType().Name}: {ex.Message}, opening directly on the map.");
            _pending = null;
            OpenNow(request);
         }
      }

      /// <summary>
      ///   The settlement twin of <see cref="BeginStagedConversation" /> (player reports CarolusIV/Windmolen,
      ///   2026-08-25/26: staging a MAP conversation from inside a settlement crashes natively when the chat
      ///   closes; vanilla never does that). Opens the settlement's OWN interior mission with the partner as the
      ///   talk-to character, the same engine path vanilla uses from settlement menus (the retirement retreat,
      ///   the arena, the alley quests): the conversation starts in the scene, our intercept line fires there
      ///   exactly like over the map, and ending it returns the player to the settlement menu, never ejecting
      ///   them from town. Falls back to the direct open only when no usable location/encounter exists or the
      ///   open throws.
      /// </summary>
      private static void BeginSettlementMissionConversation(ChatSceneRequest request, CharacterObject partner)
      {
         // A companion asking for a private word belongs in the TAVERN, not the lord's hall (their audience is a
         // personal matter, not court business); everyone else keeps the hall/centre.
         bool preferTavern = request.Kind == ChatSceneKind.CompanionAudience;
         Location location = ResolveAudienceLocation(preferTavern);

         if (location == null || PlayerEncounter.LocationEncounter == null)
         {
            ModLog.Info($"[CHAT STAGE {request.Kind}] no interior location available in this settlement, opening directly.");
            OpenNow(request);

            return;
         }

         try
         {
            _pending = request;
            _lineReached = false;
            _settleFrames = 0;
            _fallbackFrames = 0;
            _armedFrames = 0;
            StagedMissionNeedsEnd = false;
            // For a companion audience, remember whom to bring beside the player once the scene's agents exist
            // (the intercept-line hijack skips the vanilla walk-up, leaving the talk-to companion at their spawn
            // point out of view). PlaceAudienceCompanionIfPending, run from the Mission.AfterStart postfix,
            // teleports them over. Cleared for every non-companion stage so a stale id never fires.
            PendingAudienceCompanionId = request.Kind == ChatSceneKind.CompanionAudience ? partner?.HeroObject?.StringId : null;
            PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(location, null, partner, null);
            ModLog.Info($"[CHAT STAGE {request.Kind}] interior mission opening at {location.StringId} with {partner.StringId}; awaiting the scene to load.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CHAT STAGE {request.Kind}] interior mission open failed: {ex.GetType().Name}: {ex.Message}, opening directly.");
            _pending = null;
            OpenNow(request);
         }
      }

      /// <summary>
      ///   The interior location an in-settlement staged scene opens at: the lord's hall of a town or castle,
      ///   the village center of a village, the tavern as a last resort (every town has one). Null when the
      ///   settlement exposes no usable interior (a hideout camp, an exotic custom settlement), which the caller
      ///   turns into the direct-open fallback. Internal so the council stager reuses the exact same pick.
      /// </summary>
      internal static Location ResolveAudienceLocation(bool preferTavern = false)
      {
         LocationComplex complex = LocationComplex.Current;

         if (complex == null) return null;

         if (preferTavern)
            return complex.GetLocationWithId("tavern")
                   ?? complex.GetLocationWithId("lordshall")
                   ?? complex.GetLocationWithId("village_center");

         return complex.GetLocationWithId("lordshall")
                ?? complex.GetLocationWithId("village_center")
                ?? complex.GetLocationWithId("tavern");
      }

      /// <summary>A reachable navmesh point a conversational distance from the player, rung around them; false when none is standable.</summary>
      private static bool TryFindPointNearPlayer(Scene scene, WorldPosition centre, out Vec3 dest)
      {
         dest = default;
         if (scene == null) return false;

         Vec2 origin = centre.AsVec2;

         for (var j = 0; j < 8; j++)
         {
            double angle = 2.0 * Math.PI * j / 8.0;

            foreach (float r in AudienceRingRadii)
            {
               var target = new Vec2(origin.x + r * (float) Math.Cos(angle), origin.y + r * (float) Math.Sin(angle));

               WorldPosition candidate = centre;
               candidate.SetVec2(target);

               if (candidate.GetNavMesh() == UIntPtr.Zero) continue; // a wall or a building, not standable

               dest = candidate.GetGroundVec3();

               return true;
            }
         }

         return false;
      }

      /// <summary>Consequence of the intercept line: the conversation is ready; the tick opens the chat next frame.</summary>
      private static void OnInterceptLineReached()
      {
         _lineReached = true;
         ModLog.Info("[CHAT STAGE] intercept line reached, the conversation is ready for the chat.");
      }

      /// <summary>
      ///   The leaf dispatch: opens the actual chat for <paramref name="r" /> over whatever scene is now live
      ///   (staged or already-live) by routing to the matching ChatManager open. This is the only place the
      ///   leaf opens are reached from the door; direct callers of those leaves are the ones that already sit
      ///   inside a live conversation scene.
      /// </summary>
      private static void OpenNow(ChatSceneRequest r)
      {
         switch (r.Kind)
         {
            case ChatSceneKind.PlainConversation:
               // Open the ordinary CR conversation for the lord, exactly as clicking "A word with you." would, but
               // programmatically so the meeting cannot be dodged. OneToOneConversationHero is the lord here.
               NpcMemoryCampaignBehavior.Instance?.DialogFlow?.OpenChatForInterceptingHero(r.Partner);

               return;

            case ChatSceneKind.CaptorScene:
               ChatManager.OpenForCaptorScene(r.Partner, r.Profile, r.Captive?.Intent ?? CaptiveSceneIntent.Interrogation);

               return;

            case ChatSceneKind.CompanionAudience:
               // The DIRECT open: the choke that routed us here (ChatManager.OpenForCompanionAudience) stages, so
               // going back through it would re-arm the stager instead of opening the chat.
               ChatManager.OpenForCompanionAudienceDirect(r.Partner, r.Profile, r.Audience.Reason, r.Audience.Landed, r.Audience.TopicDetail);

               return;

            case ChatSceneKind.VictimEncounter:
               ChatManager.OpenForCaptiveEncounter(
                  r.Partner, r.Profile, r.Captive?.Witnesses, r.Captive?.IsCollective ?? false,
                  r.Captive?.Intent ?? CaptiveSceneIntent.Interrogation,
                  r.PortraitCharacter, r.Captive?.VictimCompanion, r.Captive?.PlayerCoerced ?? false);

               return;

            case ChatSceneKind.BriefedReturn:
               NpcMemoryCampaignBehavior.Instance?.DialogFlow?.OpenChatForBriefedReturn(
                  r.Partner, r.BriefedCompanions);

               return;

            case ChatSceneKind.PostDuel:
               ChatManager.OpenForPostDuel(
                  r.Partner, r.Profile, r.PostDuel?.PlayerWon ?? false,
                  r.PostDuel?.Reason ?? DuelReason.HonourAffront, r.PostDuel?.CourtedHero, r.SeedTranscript);

               return;

            default:
               // PostDuel and any future kind not yet wired: log rather than silently drop, so a missing branch
               // is visible instead of a scene that opens onto nothing.
               ModLog.Error($"[CHAT STAGE] no open wired for kind {r.Kind}; nothing opened.");

               return;
         }
      }

      private static void OpenPending(string via)
      {
         ChatSceneRequest request = _pending;
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
            ModLog.Info($"[CHAT STAGE] opening the chat over the scene via {via} " +
                        $"(Mission.Current={Mission.Current != null}, " +
                        $"convoHero={Hero.OneToOneConversationHero?.Name?.ToString() ?? "none"}).");
            OpenNow(request);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CHAT STAGE] opening over scene failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      #endregion
   }
}
