// Code written by Gabriel Mailhot, 05/07/2026.
// Stages a captive encounter as a CONVERSATION SCENE (3D) instead of a bare overlay on the world map. On the
// heavy War Sails naval map, opening the chat as an overlay leaves that map rendering behind it and leaks
// render-textures (the create_render_texture VRAM crash). A lord/tavern conversation never crashes because it
// rides a light conversation MISSION that suspends the map. So we do the same here: start a conversation with
// the captor (loading the 3D scene), then open the captive chat over that scene. ChatManager.Close already ends
// the vanilla conversation, so the scene is torn down for free on close.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
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
      // Frames to let the conversation scene render after our intercept line fires, so the chat opens over a
      // fully set-up mission screen rather than mid-transition.
      private const int SettleFrames = 3;

      // Fallback: if our intercept line never fires (another mod's start line won the token), open anyway once
      // the conversation has been live this long. ~1 second at 60 fps.
      private const int FallbackFrames = 60;

      private static Pending _pending;
      private static bool _lineReached;
      private static int _settleFrames;
      private static int _fallbackFrames;

      /// <summary>True while a conversation scene is loading and the captive chat is waiting to open over it.</summary>
      public static bool IsStaging => _pending != null;

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
            () => IsStaging,
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
         if (partner == null || Campaign.Current?.ConversationManager == null)
         {
            ModLog.Info("[CAPTIVE STAGE] no conversation partner available — opening directly on the map.");
            OpenDirect(pending);

            return;
         }

         try
         {
            PartyBase captorParty =
               captor?.PartyBelongedTo?.Party
            ?? Hero.MainHero?.PartyBelongedToAsPrisoner?.MobileParty?.Party;

            var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
            var partnerData = new ConversationCharacterData(partner, captorParty);

            _pending = pending;
            _lineReached = false;
            _settleFrames = 0;
            _fallbackFrames = 0;
            Campaign.Current.ConversationManager.OpenMapConversation(playerData, partnerData);
            ModLog.Info($"[CAPTIVE STAGE] conversation started with {partner.StringId}; awaiting the scene to load.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CAPTIVE STAGE] OpenMapConversation failed: {ex.GetType().Name}: {ex.Message} — opening directly on the map.");
            _pending = null;
            OpenDirect(pending);
         }
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
         if (!conversationActive) return;
         if (++_fallbackFrames < FallbackFrames) return;
         OpenPending("fallback (intercept line never fired)");
      }

      /// <summary>Consequence of the intercept line: the conversation is ready; the tick opens the chat next frame.</summary>
      private static void OnInterceptLineReached()
      {
         _lineReached = true;
         ModLog.Info("[CAPTIVE STAGE] intercept line reached — the conversation is ready for the chat.");
      }

      private static void OpenPending(string via)
      {
         Pending p = _pending;
         _pending = null;
         _lineReached = false;
         _settleFrames = 0;
         _fallbackFrames = 0;

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

      private static void OpenDirect(Pending p)
      {
         ChatManager.OpenForCaptiveEncounter(
            p.Captor, p.Profile, p.Witnesses, p.IsCollective, p.Intent,
            p.PortraitCharacter, p.VictimCompanion, p.PlayerCoerced);
      }

      private sealed class Pending
      {
         public Hero Captor;
         public bool IsCollective;
         public CaptiveSceneIntent Intent;
         public bool PlayerCoerced;
         public CharacterObject PortraitCharacter;
         public NpcProfile Profile;
         public Hero VictimCompanion;
         public List<WitnessEntry> Witnesses;
      }
   }
}
