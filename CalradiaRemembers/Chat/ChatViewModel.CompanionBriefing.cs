// Code written by Gabriel Mailhot, 07/09/2026.
// Companion-briefing pillar: the PRIVATE chat opened when the player asks, in the vanilla dialogue, for a
// moment to speak with his own people before "A word with you..." continues (see Dialogue/LlmDialogFlow.cs
// and Companions/CompanionBriefingCoordinator.cs for how the player gets here). Deliberate simplification,
// documented rather than forced: this is a private planning aside between the player and his own people, never
// a scene for the model to judge in character, so no LLM call opens or drives it. The player's own words are
// what matters (captured verbatim into each present companion's brief, see CalradiaRemembers.Logic.
// CompanionBriefPolicy and CompanionBriefingStore); a short canned acknowledgment from the anchor confirms it
// was heard, at zero API cost and with no risk of a refusal or derailment on what is really a utility screen.

#region

using System.Collections.Generic;
using CalradiaRemembers.Companions;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      // Canned acknowledgments only: see the file header for why this scene deliberately never calls the LLM.
      private static readonly string[] CompanionBriefingAcknowledgments = {
         "Understood. We'll see it done.",
         "As you say. We'll mind it.",
         "Consider it done.",
         "We hear you. It will be as you ask.",
         "Understood, leave it to us."
      };

      // Set ONLY for this private briefing chat itself, to the hero StringId of the lord the FOLLOWING
      // conversation is for. Never shown to anyone in this conversation: it exists purely so
      // FinalizeCompanionBriefs can tell this chat's own close apart from an ordinary conversation's close (see
      // CompanionBriefPolicy.HeroesToExpireOnClose's isBriefingChat parameter). Null for every other chat.
      private string _companionBriefingTargetId;

      /// <summary>True while THIS chat is the private briefing chat itself, never the ordinary conversation the brief rides into.</summary>
      private bool IsCompanionBriefingChat => _companionBriefingTargetId != null;

      /// <summary>
      ///   Opens the private briefing chat. <see cref="ChatManager.OpenForCompanionBriefing" /> has already set
      ///   the anchor (<see cref="_npcHero" />) and seated the rest of the chosen roster as witnesses; this only
      ///   marks the session and greets the player in the anchor's own voice.
      /// </summary>
      public void ActivateCompanionBriefing(string targetHeroId)
      {
         _mode = ChatMode.Conversation;
         _companionBriefingTargetId = targetHeroId;

         TitleText = "Calradia Remembers: A private word";
         CloseButtonText = "Done";
         SendButtonText = "Say";

         RefreshWitnessUi(); // now that IsCompanionBriefingChat reads true, hide the round-table/privacy buttons
         AppendNpcMessage("We're listening. What would you have us do?");
      }

      /// <summary>
      ///   Companion-briefing pillar, expiry sweep: called on EVERY chat close (<see cref="ChatManager.Close" />).
      ///   A cheap no-op whenever no brief is outstanding (the common case, feature unused this session). Never
      ///   clears a brief on THIS chat's own close (<see cref="IsCompanionBriefingChat" />): the brief must
      ///   survive to reach the conversation it was actually given for. On an ORDINARY conversation's close, any
      ///   currently-briefed companion who took part in it (the anchor or a witness) has their brief spent, see
      ///   <see cref="CompanionBriefPolicy.HeroesToExpireOnClose" /> for why this is exactly right even though a
      ///   brief carries no record of which target it was meant for (ruling #2: it is attached to the
      ///   companion, never to the conversation or the target).
      /// </summary>
      public void FinalizeCompanionBriefs()
      {
         if (CompanionBriefingStore.BriefedHeroIds.Count == 0) return;

         var participants = new List<string>();
         if (_npcHero != null) participants.Add(_npcHero.StringId);
         foreach (WitnessEntry w in _witnesses)
            if (!string.IsNullOrEmpty(w.HeroStringId))
               participants.Add(w.HeroStringId);

         CompanionBriefingStore.ExpireForConversationClose(participants, IsCompanionBriefingChat);
      }

      /// <summary>
      ///   The briefing chat's own send path, routed here from <see cref="ExecuteSendMessage" /> while
      ///   <see cref="IsCompanionBriefingChat" /> is true: captures the player's OWN words, trimmed, into EVERY
      ///   present companion's brief (the anchor and every witness all heard it, ruling #2 attaches the brief to
      ///   each companion who was there). Never an LLM summary: cheaper, and truer to what the player actually
      ///   asked for than a paraphrase would be.
      /// </summary>
      private void ExecuteSendCompanionBriefingMessage(string text)
      {
         AppendPlayerMessage(text);
         InputText = "";
         if (_isComposeOpen) IsComposeOpen = false;

         if (_npcHero != null) CompanionBriefingStore.AddInstruction(_npcHero.StringId, text);
         foreach (WitnessEntry w in _witnesses)
            if (!string.IsNullOrEmpty(w.HeroStringId))
               CompanionBriefingStore.AddInstruction(w.HeroStringId, text);

         AppendNpcMessage(Pick(CompanionBriefingAcknowledgments));
      }
   }
}
