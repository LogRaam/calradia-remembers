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
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Companions;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      // Set ONLY for this private briefing chat itself, to the hero StringId of the lord the FOLLOWING
      // conversation is for. Never shown to anyone in this conversation: it exists purely so
      // FinalizeCompanionBriefs can tell this chat's own close apart from an ordinary conversation's close (see
      // CompanionBriefPolicy.HeroesToExpireOnClose's isBriefingChat parameter). Null for every other chat.
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

         TitleText = "Calradia Remembers: A private word";
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
         AppendNpcMessage("We're listening. What is it?");
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
         // Write it down first: expiry below may clear the very text the memory is made of.
         WriteBriefingToMemory();

         if (CompanionBriefingStore.BriefedHeroIds.Count == 0) return;

         var participants = new List<string>();
         if (_npcHero != null) participants.Add(_npcHero.StringId);
         foreach (WitnessEntry w in _witnesses)
            if (!string.IsNullOrEmpty(w.HeroStringId))
               participants.Add(w.HeroStringId);

         CompanionBriefingStore.ExpireForConversationClose(participants, IsCompanionBriefingChat);
      }

      /// <summary>
      ///   Called from <see cref="ExecuteSendMessage" /> as a side effect BEFORE the ordinary pipeline runs,
      ///   while <see cref="IsCompanionBriefingChat" /> is true: captures the player's OWN words, trimmed, into
      ///   EVERY present companion's brief (the anchor and every chosen witness all heard it, ruling #2 attaches
      ///   the brief to each companion who was there). Never an LLM summary: cheaper, and truer to what the
      ///   player actually asked for than a paraphrase would be. Ordinary send processing continues unchanged
      ///   after this returns, so the anchor (or whichever companion the player addresses) answers for real.
      /// </summary>
      private void RecordCompanionBriefingInstruction(string text)
      {
         if (_npcHero != null) CompanionBriefingStore.AddInstruction(_npcHero.StringId, text);
         foreach (WitnessEntry w in _witnesses)
            if (!string.IsNullOrEmpty(w.HeroStringId))
               CompanionBriefingStore.AddInstruction(w.HeroStringId, text);
      }

      /// <summary>
      ///   Writes what the player asked for into each briefed companion's own memory, PRIVATELY, as the
      ///   briefing closes.
      ///   <para>
      ///     Gabriel's design, 2026-09-13: "les décisions prises devraient se retrouver dans la mémoire du
      ///     compagnon, et c'est la mémoire qui devrait être utilisée ensuite". It is the better vehicle for
      ///     three reasons - it is durable across saves where the standing brief dies with the session, it is
      ///     bounded by the memory pipeline rather than by a character count that ate his mission statement,
      ///     and it is truer: the companion acts because he REMEMBERS being asked, not because a hidden field
      ///     commands him.
      ///   </para>
      ///   <para>
      ///     Private is what makes it safe. Until now the briefing DROPPED its memories outright, because a
      ///     companion's memories travel into the prompt of whoever is speaking - so "Arwa asked me to charm
      ///     Ira" would have been read out in Ira's own prompt while she stood there. See NotableEvent.IsPrivate
      ///     and the filter in BuildWitnessMemory, which is the single path by which that could happen.
      ///   </para>
      /// </summary>
      private void WriteBriefingToMemory()
      {
         if (!IsCompanionBriefingChat) return;

         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         var store = behavior?.Store;
         if (store == null) return;

         // The same clock every other memory is stamped from, read through the bridge rather than the engine
         // directly, so a briefing memory sorts beside the rest instead of into its own era.
         int day = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string targetName = _companionBriefingTargetName;

         foreach (string heroId in BriefedParticipantIds())
         {
            string instruction = CompanionBriefingStore.Peek(heroId);
            if (string.IsNullOrWhiteSpace(instruction)) continue;

            NpcProfile profile = store.Get(heroId);
            if (profile?.Events == null) continue;

            string summary = string.IsNullOrWhiteSpace(targetName)
               ? $"The player drew me aside for a private word and asked this of me: {instruction}"
               : $"Before the meeting with {targetName}, the player drew me aside and asked this of me: {instruction}";

            profile.Events.Add(new NotableEvent(day, NotableEventType.Other, summary) {IsPrivate = true});
            store.Set(profile);

            ModLog.Info($"[BRIEFING] wrote a private memory of the errand for {profile.Name ?? heroId} " +
                        $"({summary.Length} chars).");
         }
      }

      /// <summary>Everyone who stood in the private word: the anchor and every companion present.</summary>
      private IEnumerable<string> BriefedParticipantIds()
      {
         if (!string.IsNullOrEmpty(_npcHero?.StringId)) yield return _npcHero.StringId;

         foreach (WitnessEntry w in _witnesses)
            if (!string.IsNullOrEmpty(w?.HeroStringId))
               yield return w.HeroStringId;
      }
   }
}
