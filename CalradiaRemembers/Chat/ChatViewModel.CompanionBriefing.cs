// Code written by Gabriel Mailhot, 07/09/2026.
// Companion-briefing pillar: the PRIVATE chat opened when the player asks, in the vanilla dialogue, for a
// moment to speak with his own people before "A word with you..." continues (see Dialogue/LlmDialogFlow.cs
// and Companions/CompanionBriefingCoordinator.cs for how the player gets here).
// Revised per Gabriel's ruling (2026-09-07, after review of a first, LLM-free cut of this scene): "Ca doit
// etre une vraie conversation entre le joueur et son/ses compagnons" (this must be a REAL conversation between
// the player and his companion(s)). The chosen roster (never widened) answers through the ordinary LLM
// pipeline exactly like any other multi-participant scene: the anchor replies, every other chosen companion
// may react as a witness, and the player may address any of them by name. EncounterContext.IsCompanionBriefingScene
// (see EncounterContextBuilder/PromptBuilder) tells the model what kind of scene this is, so a companion knows
// the named target cannot hear this and is free to answer honestly, including pushing back or pleading
// unsuited, never a bare acknowledgment. This file only owns the capture side effect (the player's own words,
// verbatim, into every present companion's brief) and the session bookkeeping; the actual exchange runs
// through the SAME ExecuteSendMessage/RunLlmExchangeAsync path every other conversation uses.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Companions;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      // Set ONLY for this private briefing chat itself, to the hero StringId of the lord the FOLLOWING
      // conversation is for. Never shown to anyone in this conversation: it exists purely so
      // FinalizeCompanionBriefs can tell this chat's own close apart from an ordinary conversation's close,
      // which is what decides whether the private word gets written down. Null for every other chat.
      private bool _briefingMemoryWritten;
      private string _companionBriefingTargetId;

      // The absent target's display name, fed to EncounterContext.CompanionBriefingTargetName every turn of
      // this scene so the prompt framing can name WHO cannot hear this, rather than an abstract "someone".
      private string _companionBriefingTargetName;

      /// <summary>True while THIS chat is the private briefing chat itself, never the ordinary conversation the brief rides into.</summary>
      private bool IsCompanionBriefingChat => _companionBriefingTargetId != null;

      /// <summary>Whether this chat is the private word itself, read by ChatManager.Close to arm the return.</summary>
      public bool IsBriefingChat => IsCompanionBriefingChat;

      /// <summary>
      ///   Opens the private briefing chat. <see cref="ChatManager.OpenForCompanionBriefing" /> has already set
      ///   the anchor (<see cref="_npcHero" />) and seated the rest of the CHOSEN roster as witnesses (never the
      ///   whole party, see the coordinator's own picker); this marks the session, remembers the absent target's
      ///   name for the prompt framing, and greets the player in the anchor's own voice. From the player's first
      ///   message on, the exchange runs through the ordinary LLM pipeline like any other conversation.
      /// </summary>
      public void ActivateCompanionBriefing(Hero target)
      {
         _mode = ChatMode.Conversation;
         _companionBriefingTargetId = target?.StringId;
         _companionBriefingTargetName = target?.Name?.ToString();

         TitleText = "A private word";
         // The button says where it LEADS. Gabriel clicked "Done" expecting to be taken back to Ira and was
         // put on the map instead (2026-09-12); now the label promises the return the close actually performs.
         CloseButtonText = target?.Name == null
            ? "Done"
            : $"Rejoin {target.Name}";
         SendButtonText = "Say";

         // ChatManager.OpenForCompanionBriefing already called AddWitness for the non-anchor roster, which ran
         // its own RefreshWitnessUi while IsCompanionBriefingChat still read false (set just above): recompute
         // now so AllowsPrivacyRequest's gate (see its own doc) actually hides the privacy-request button.
         RefreshWitnessUi();
         // Written for a group and plural whatever the room held, so one companion drawn aside answered
         // "We're listening" with nobody beside him (Gabriel, 2026-09-13). The roster is already seated by
         // this point - OpenForCompanionBriefing adds every non-anchor companion before calling this - so the
         // count is known and the line can simply be true.
         AppendNpcMessage(_witnesses.Count == 0
            ? "I'm listening. What is it?"
            : "We're listening. What is it?");
      }

      /// <summary>
      ///   Writes the private word into the memory of EVERYONE who stood in it, as the briefing closes.
      ///   <para>
      ///     THE BRIEF IS GONE, and Gabriel's question is what killed it (2026-09-13): "pourquoi on doit encore
      ///     transporter le contenu du briefing?". There was exactly one reason, and it is written in the gate
      ///     this method now replaces - "the standing brief already carries what needs to survive; nothing else
      ///     may". The brief existed because a memory could not be kept PRIVATE. That stopped being true the
      ///     hour NotableEvent.IsPrivate was built, and a second carrier for one fact is the thing this
      ///     codebase keeps learning not to have.
      ///   </para>
      ///   <para>
      ///     It also kills the question that could not be answered honestly: WHERE in a briefing the
      ///     instruction sits. Bounding the text meant betting on it - his own briefing put the mission in the
      ///     first sentence and a 500-character tail-trim threw it away, so Sley walked in knowing the reward
      ///     and not the errand. There is no bet here. Every word the player said in that scene is kept, and
      ///     bounding is left to the memory compressor, whose job it actually is.
      ///   </para>
      ///   <para>
      ///     Written to EVERY participant, not just the anchor. An ordinary conversation gives the anchor a
      ///     summary and the onlookers a contentless "was present"; a briefing has no onlookers - the player
      ///     drew these people aside together and told them all the same thing.
      ///   </para>
      /// </summary>
      public void FinalizeCompanionBriefs()
      {
         if (!IsCompanionBriefingChat || _briefingMemoryWritten) return;
         _briefingMemoryWritten = true;

         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         var store = behavior?.Store;
         if (store == null) return;

         string instruction = PlayerWordsInThisBriefing();
         if (string.IsNullOrWhiteSpace(instruction)) return;

         int day = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string targetName = _companionBriefingTargetName;

         string summary = string.IsNullOrWhiteSpace(targetName)
            ? $"The player drew me aside for a private word and asked this of me: {instruction}"
            : $"Before the meeting with {targetName}, the player drew me aside and asked this of me: {instruction}";

         var written = 0;

         foreach (string heroId in BriefedParticipantIds())
         {
            NpcProfile profile = store.Get(heroId);
            if (profile?.Events == null) continue;

            profile.Events.Add(new NotableEvent(day, NotableEventType.Other, summary) {IsPrivate = true});
            store.Set(profile);
            written++;
         }

         if (written > 0)
            ModLog.Info($"[BRIEFING] the private word is now a private memory for {written} companion(s) " +
                        $"({summary.Length} chars).");
      }

      /// <summary>
      ///   Everything SAID in this briefing, in order, attributed. Verbatim rather than paraphrased: an
      ///   instruction is one of the few things worth quoting exactly, and taking ALL of it means nothing has
      ///   to guess which line was the order.
      ///   <para>
      ///     BOTH SIDES, since 13/09/2026 (audit). The first version kept only the player's lines, on the
      ///     reasoning that the instruction is what matters. But a briefing is a conversation, and Gabriel's
      ///     own words when he asked for this were "tout le briefing est important": a companion who
      ///     bargained, set a condition, or agreed reluctantly said something the meeting afterwards depends
      ///     on, and keeping only one half of it recorded an order nobody answered.
      ///   </para>
      ///   <para>
      ///     Attributed, because an unattributed join would read to the model as one continuous instruction
      ///     from the player and put its own past words in the player's mouth - the same class of confusion
      ///     SpeakerScriptGuard exists to undo at the other end.
      ///   </para>
      /// </summary>
      private string PlayerWordsInThisBriefing()
         => string.Join("\n",
               _transcript.Lines
                          .Where(l => l != null && !l.IsNarration && !string.IsNullOrWhiteSpace(l.Text))
                          .Select(l => l.IsPlayer
                                     ? $"[the player]: {l.Text.Trim()}"
                                     : $"[{l.SpeakerName ?? "one of your people"}]: {l.Text.Trim()}"))
             .Trim();

      /// <summary>Everyone who stood in the private word: the anchor and every companion present.</summary>
      private IEnumerable<string> BriefedParticipantIds()
      {
         if (!string.IsNullOrEmpty(_npcHero?.StringId)) yield return _npcHero.StringId;

         foreach (WitnessEntry w in _witnesses)
            if (!string.IsNullOrEmpty(w?.HeroStringId) && w.HeroStringId != _npcHero?.StringId)
               yield return w.HeroStringId;
      }
   }
}
