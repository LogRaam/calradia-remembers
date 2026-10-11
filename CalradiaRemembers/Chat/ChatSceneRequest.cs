// Code written by Gabriel Mailhot, 26/08/2026.
// One request object for every CR chat scene, built by COMPOSITION (a shared core plus exactly one per-kind
// payload), never by inheritance. ChatSceneStager.Open consumes it and is the single door that upholds the
// invariant: a CR chat that may originate on the bare campaign map ALWAYS opens over a live Mission, never as
// an overlay on the map (which leaks VRAM and leaves a background that never repaints). A new chat kind is a
// new enum value plus a new payload, wired into the stager's dispatch; no request subclass is ever added.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Duels;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>Which CR chat a <see cref="ChatSceneRequest" /> opens once its conversation scene is live.</summary>
   public enum ChatSceneKind
   {
      /// <summary>The captor (NPC or bandit) summons the player, who is the captive. <see cref="ChatManager.OpenForCaptiveEncounter" />.</summary>
      VictimEncounter,

      /// <summary>The player holds the NPC prisoner and drives the scene. <see cref="ChatManager.OpenForCaptorScene" />.</summary>
      CaptorScene,

      /// <summary>A plain forced meeting (a lord who intercepted the player, a fief audience): the ordinary CR conversation chat.</summary>
      PlainConversation,

      /// <summary>A companion the player already leads asks for a word. <see cref="ChatManager.OpenForCompanionAudienceDirect" />.</summary>
      CompanionAudience,

      /// <summary>The conversation that resumes right after a duel, coloured by who prevailed. <see cref="ChatManager.OpenForPostDuel" />.</summary>
      PostDuel,

      /// <summary>
      ///   The meeting a companion briefing was FOR: the player asked the target's leave for a private word,
      ///   gave his people their instructions, and is now brought back to the target with those people at his
      ///   side. See CompanionBriefingCoordinator.
      /// </summary>
      BriefedReturn,

      /// <summary>
      ///   An anonymous envoy, come under a flag of truce to treat for a captive the player holds (prisoner exchange,
      ///   fkasad 25/09/2026). No hero: a troop of the lord's people stands in the shot. See ChatManager.OpenForEnvoyAudienceDirect.
      /// </summary>
      EnvoyAudience,

      /// <summary>
      ///   A written tale that began on the map (Gabriel, 10/10/2026): the player went to find the giver, who travels
      ///   with them. The ordinary conversation, the player seeking; no interception opener. See
      ///   LlmDialogFlow.OpenChatForTaleMeeting.
      /// </summary>
      TaleMeeting
   }

   /// <summary>
   ///   Everything <see cref="ChatSceneStager.Open" /> needs to stage a scene and open the right chat over it,
   ///   assembled by COMPOSITION: the shared fields below plus exactly one non-null per-kind payload. There is
   ///   deliberately no request subclass per kind: a caller fills the core and the one payload its kind reads,
   ///   and the stager dispatches on <see cref="Kind" />.
   /// </summary>
   public sealed class ChatSceneRequest
   {
      /// <summary>Which chat to open; selects which payload the stager reads and which activation it calls.</summary>
      public ChatSceneKind Kind;

      /// <summary>The hero the engine converses with to load the scene. Null only for a faceless bandit captor, which then needs <see cref="PortraitCharacter" />.</summary>
      public Hero Partner;

      /// <summary>The stored profile of <see cref="Partner" /> (or the synthetic captor profile). Passed to the chat's activation.</summary>
      public NpcProfile Profile;

      /// <summary>Fallback troop face when <see cref="Partner" /> is a faceless bandit (no hero to load a portrait from).</summary>
      public CharacterObject PortraitCharacter;

      /// <summary>
      ///   The party the map conversation attaches its scene to, resolved by the builder for this kind (a field
      ///   party, the holding party, the settlement's party, or the main party). Unused inside a settlement (the
      ///   interior mission path) and when a scene is already live (the direct open).
      /// </summary>
      public PartyBase AttachParty;

      /// <summary>
      ///   Prior conversation to replay into the opened chat so it resumes seamlessly (the transcript that led up
      ///   to a duel, carried into the post-duel scene). Null for every kind that starts a fresh exchange.
      /// </summary>
      public ConversationTranscript SeedTranscript;

      /// <summary>Set for <see cref="ChatSceneKind.VictimEncounter" /> and <see cref="ChatSceneKind.CaptorScene" />; null otherwise.</summary>
      public CaptiveScenePayload Captive;

      /// <summary>Set for <see cref="ChatSceneKind.CompanionAudience" />; null otherwise.</summary>
      public CompanionAudiencePayload Audience;

      /// <summary>
      ///   Companion-briefing return: the companions the player drew aside, to be seated in the meeting they
      ///   were briefed FOR. They were already chosen when the briefing was launched, so the ordinary witness
      ///   picker must not ask again (Gabriel, 2026-09-13).
      /// </summary>
      public List<Hero> BriefedCompanions;

      /// <summary>Set for <see cref="ChatSceneKind.PostDuel" />; null otherwise.</summary>
      public PostDuelPayload PostDuel;

      /// <summary>Set for <see cref="ChatSceneKind.EnvoyAudience" />: the envoy's mandate and the lord behind him.</summary>
      public EnvoyAudience Envoy;

      /// <summary>Set for <see cref="ChatSceneKind.EnvoyAudience" />: how the envoy is shown ("Envoy of X").</summary>
      public string EnvoyName;
   }

   /// <summary>The captive-scene specifics; both the victim direction and the captor direction read these.</summary>
   public sealed class CaptiveScenePayload
   {
      /// <summary>Others present in the scene, as active participants (collective) or as bystanders.</summary>
      public List<WitnessEntry> Witnesses;

      /// <summary>True when the witnesses are active participants in the scene, not mere onlookers.</summary>
      public bool IsCollective;

      /// <summary>What the scene is for (interrogation, desire, domination, a reckoning...).</summary>
      public CaptiveSceneIntent Intent;

      /// <summary>A companion held alongside the player, brought into the victim scene; null when the player is alone.</summary>
      public Hero VictimCompanion;

      /// <summary>True when the player is present under coercion (a brought captive), shaping the framing.</summary>
      public bool PlayerCoerced;
   }

   /// <summary>The companion-audience specifics.</summary>
   public sealed class CompanionAudiencePayload
   {
      /// <summary>Why the companion asked for the word (grievance, retirement, a found-topic report).</summary>
      public CompanionAudienceReason Reason;

      /// <summary>True when the audience opens ashore/landed (affects the opener framing).</summary>
      public bool Landed;

      /// <summary>The specific fact the mod found for a found-topic reason (the actual rumour/memory/grudge/party condition); null for the original reasons.</summary>
      public string TopicDetail;
   }

   /// <summary>The post-duel specifics: who prevailed and what the duel was over, so the resumed chat is coloured correctly.</summary>
   public sealed class PostDuelPayload
   {
      /// <summary>True when the PLAYER won the duel (the NPC concedes); false when the NPC won (the NPC prevailed).</summary>
      public bool PlayerWon;

      /// <summary>What drove the duel (honour affront, courtship rivalry), carried into the post-duel prompt.</summary>
      public DuelReason Reason;

      /// <summary>The third party a courtship duel was fought over; null for any other reason.</summary>
      public Hero CourtedHero;
   }
}
