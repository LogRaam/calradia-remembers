// Code written by Gabriel Mailhot, 02/08/2026.
// Council-in-mission spike: re-hosts cr.council over a staged conversation scene exactly like
// CaptiveSceneStager already does for the captive chat, instead of the old forced pushed screen. A pushed
// screen needed no scene to look right (a blank backdrop); the council prefab now shows a glimpse of the
// anchor's own conversation scene through its partial-alpha backdrop (CalradiaRemembersCouncil.xml), so it
// needs the SAME suspended-map / keyboard-focus scene the captive chat gets. Mirrors CaptiveSceneStager's
// structure closely on purpose: one proven pattern, two call sites.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Opens a council (<see cref="ChatManager.OpenForCouncil" />) over a 3D conversation scene rather than as
   ///   a pushed screen over the world map. Call <see cref="StageCouncil" /> instead of
   ///   <see cref="ChatManager.OpenForCouncil" /> directly; it starts a conversation with the presiding member
   ///   (the anchor) and, once the scene is loaded (polled by <see cref="Tick" /> from the submodule's per-frame
   ///   hook), opens the council over it. Falls back to the direct overlay (over the map) if there is no anchor
   ///   to converse with or the conversation cannot start (the rare edge <see cref="ChatManager.OpenForCouncil" />
   ///   already tolerated before this stager existed).
   /// </summary>
   public static class CouncilConversationStager
   {
      // Fallback: if our intercept line never fires (another mod's start line won the token), open anyway once
      // the conversation has been live this long. ~1 second at 60 fps. Mirrors CaptiveSceneStager.FallbackFrames.
      private const int FallbackFrames = 60;

      /// <summary>
      ///   Grace given to <c>OpenMapConversation</c> to register its conversation before we conclude it did
      ///   nothing. See <see cref="CaptiveSceneStager" />'s own constant for the full rationale (the same
      ///   engine call, the same failure mode): a call that started no conversation must open directly rather
      ///   than leave the staging armed forever, which (via <see cref="IsStaging" />) would jam every later
      ///   council too.
      /// </summary>
      private const int ConversationRegistrationFrames = 10;

      // Frames to let the conversation scene render after our intercept line fires, so the council opens over a
      // fully set-up mission screen rather than mid-transition.
      private const int SettleFrames = 3;

      private static int _armedFrames;
      private static int _fallbackFrames;
      private static bool _lineReached;
      private static Pending _pending;
      private static int _settleFrames;

      /// <summary>True while a conversation scene is loading and the council is waiting to open over it.</summary>
      public static bool IsStaging => _pending != null;

      /// <summary>
      ///   Registers the intercept dialogue line: while a council is staging, this line wins the conversation's
      ///   "start" token (priority above the vanilla first-meeting intro, so "Who are you?" never shows), and
      ///   its consequence hands the conversation over to the council. Routes to a nonexistent output token so
      ///   the vanilla conversation idles silently behind the council until ChatManager.Close ends it, the same
      ///   proven pattern <see cref="CaptiveSceneStager" /> uses. Its own line id so the two stagers, each gated
      ///   on their own static IsStaging, never collide or compete for the same token.
      /// </summary>
      public static void Register(CampaignGameStarter starter)
      {
         starter.AddDialogLine(
            "cr_council_stage_intercept",
            "start", "cr_council_stage_hold",
            "One moment, before we begin.",
            () => IsStaging,
            OnInterceptLineReached,
            200);
      }

      /// <summary>
      ///   Stages a council of <paramref name="scope" /> as a conversation scene: resolves the anchor (the
      ///   presiding member, same regard-ranked pick <see cref="ChatManager.OpenForCouncil" /> makes internally),
      ///   opens a map conversation with them, and arms the pending state <see cref="Tick" /> polls. Runs the
      ///   same guards <see cref="ModCheats.Council" /> already reported before it called
      ///   <see cref="ChatManager.OpenForCouncil" /> directly, so a refusal reads exactly as it did before this
      ///   stager existed. Returns a short line for the console: either a refusal, or confirmation that the
      ///   council is convening. <paramref name="parleyTargetFactionId" /> is the Parley's own extra parameter
      ///   (null for every other scope): the StringId of the specific enemy faction this parley was convened
      ///   against, carried through the whole staging pipeline to the eventual <see cref="ChatManager.OpenForCouncil" />
      ///   call.
      /// </summary>
      public static string StageCouncil(CouncilScope scope, string parleyTargetFactionId = null)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior == null) return "No campaign is running.";
         if (Mission.Current != null) return "You cannot convene in the middle of a scene.";

         string blocked = ChatManager.PreCouncilBlockReason();
         if (blocked != null) return blocked;

         List<Hero> seated = CouncilRosterResolver.Seated(scope, parleyTargetFactionId);

         if (seated.Count == 0)
            return $"Nobody is seated for a {scope} council right now (nobody available, or nobody of that house).";

         Hero anchor = ResolveAnchor(seated, behavior);

         if (anchor == null) return "Could not seat an anchor for the council.";

         BeginStagedConversation(scope, anchor, parleyTargetFactionId);

         return $"Convening the council ({scope})...";
      }

      /// <summary>
      ///   Pumped every frame from the submodule. Once the conversation scene is loaded (a Mission is live) and
      ///   the chat is not already open, opens the council over the scene. Fail-soft: any error opens directly
      ///   so the sitting is never lost.
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
         // screen a few frames to settle, then open the council over the loaded scene.
         if (_lineReached)
         {
            if (++_settleFrames < SettleFrames) return;
            OpenPending("intercept line");

            return;
         }

         // Fallback: the conversation is live (scene loading/loaded) but our line never fired, another mod's
         // start line may have won the token. Open anyway after a grace period so the sitting is never lost.
         bool conversationActive = Hero.OneToOneConversationHero != null || Mission.Current != null;

         // The STATE that says the call failed, checked before the conversation-active guard below: a
         // successful OpenMapConversation registers its conversation immediately and only then loads a scene,
         // so "the call returned and no conversation exists" IS the failure, not a slow load. See
         // CaptiveSceneStager.Tick for the full rationale (the same engine call, the same reasoning).
         if (!conversationActive && ++_armedFrames >= ConversationRegistrationFrames)
         {
            ModLog.Error("[COUNCIL STAGE] OpenMapConversation registered no conversation; opening directly so the "
                       + "staging cannot jam every later council.");
            OpenPending("no conversation was registered");

            return;
         }

         if (!conversationActive) return;
         if (++_fallbackFrames < FallbackFrames) return;
         OpenPending("fallback (intercept line never fired)");
      }

      private sealed class Pending
      {
         public CouncilScope Scope;

         /// <summary>The Parley's own carried state (null for every other scope); see <see cref="StageCouncil" />'s own doc.</summary>
         public string ParleyTargetFactionId;
      }

      #region private

      /// <summary>
      ///   Opens a map conversation with <paramref name="anchor" /> and arms the pending state <see cref="Tick" />
      ///   polls; falls back to the direct (unstaged) open when there is no anchor to converse with, no
      ///   <see cref="Campaign.ConversationManager" />, the player is at sea, or the conversation fails to start.
      ///   Mirrors CaptiveSceneStager.BeginStagedConversation.
      /// </summary>
      private static void BeginStagedConversation(CouncilScope scope, Hero anchor, string parleyTargetFactionId = null)
      {
         var pending = new Pending {Scope = scope, ParleyTargetFactionId = parleyTargetFactionId};

         CharacterObject partner = anchor?.CharacterObject;
         PartyBase partnerParty = anchor?.PartyBelongedTo?.Party;

         if (partner == null || Campaign.Current?.ConversationManager == null)
         {
            ModLog.Info("[COUNCIL STAGE] no conversation partner available, opening directly on the map.");
            OpenDirect(pending);

            return;
         }

         // Never ask the engine for a conversation scene while the player is at sea, see
         // CaptiveSceneStager.BeginStagedConversation for the full rationale (no scene exists for open water,
         // and the load never completes). The direct overlay needs no scene at all.
         if (MobileParty.MainParty?.IsCurrentlyAtSea == true)
         {
            ModLog.Info("[COUNCIL STAGE] the player is at sea, where no conversation scene can load, opening directly on the map.");
            OpenDirect(pending);

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
            Campaign.Current.ConversationManager.OpenMapConversation(playerData, partnerData);
            ModLog.Info($"[COUNCIL STAGE] conversation started with {partner.StringId}; awaiting the scene to load.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COUNCIL STAGE] OpenMapConversation failed: {ex.GetType().Name}: {ex.Message}, opening directly on the map.");
            _pending = null;
            OpenDirect(pending);
         }
      }

      /// <summary>Consequence of the intercept line: the conversation is ready; the tick opens the council next frame.</summary>
      private static void OnInterceptLineReached()
      {
         _lineReached = true;
         ModLog.Info("[COUNCIL STAGE] intercept line reached, the conversation is ready for the council.");
      }

      private static void OpenDirect(Pending p)
      {
         ChatManager.OpenForCouncil(p.Scope, parleyTargetFactionId: p.ParleyTargetFactionId);
      }

      private static void OpenPending(string via)
      {
         Pending p = _pending;
         _pending = null;
         _lineReached = false;
         _settleFrames = 0;
         _fallbackFrames = 0;
         _armedFrames = 0;

         try
         {
            ModLog.Info($"[COUNCIL STAGE] opening the council over the scene via {via} " +
                        $"(Mission.Current={Mission.Current != null}, " +
                        $"convoHero={Hero.OneToOneConversationHero?.Name?.ToString() ?? "none"}).");
            // allowInScene: the scene we staged IS a live Mission by this point (same as the settlement-hosted
            // council this parameter was built for), so the ordinary InScene refusal must be bypassed here.
            ChatManager.OpenForCouncil(p.Scope, allowInScene: true, parleyTargetFactionId: p.ParleyTargetFactionId);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COUNCIL STAGE] opening over scene failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The same regard-ranked pick ChatManager.OpenForCouncil makes internally, resolved here first because the anchor is who we converse WITH to load the scene.</summary>
      private static Hero ResolveAnchor(List<Hero> seated, NpcMemoryCampaignBehavior behavior)
      {
         var facts = seated
                    .Select(h => new CouncilSeatFacts(h.StringId, behavior.Store.Get(h.StringId)?.ReputationWithPlayer ?? 0))
                    .ToList();
         int anchorIndex = CouncilAnchorPolicy.SelectAnchor(facts);

         return anchorIndex >= 0 ? seated[anchorIndex] : seated[0];
      }

      #endregion
   }
}
