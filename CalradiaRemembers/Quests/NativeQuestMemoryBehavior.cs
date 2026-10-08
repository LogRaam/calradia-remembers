// Code written by Gabriel Mailhot, 08/10/2026.
// The giver of one of the game's own quests remembers it. tashmetu and Maifailun (Nexus, 08/10/2026): accepting a
// native quest closes the conversation in the game's own dialogue, and nothing was kept; the giver knew the quest only
// while it was open (EncounterContextBuilder.ResolveNativeQuestNote) and nothing once it ended. A memory is now written
// for the giver when such a quest begins and when it ends (NativeQuestMemory), through CR's own memory door. CR's own
// quest mirrors (its tasks, commitments and tales) are left out: they keep their own memories.

#region

using System;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>Writes the giver's memory of the game's own quests.</summary>
   public sealed class NativeQuestMemoryBehavior : CampaignBehaviorBase
   {
      public override void RegisterEvents()
      {
         CampaignEvents.OnQuestStartedEvent.AddNonSerializedListener(this, q => SafeRun.Step("NativeQuestMemory.Started", () => Remember(q, NativeQuestPhase.Started)));
         CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(this, (q, detail) =>
            SafeRun.Step("NativeQuestMemory.Ended", () => Remember(q, PhaseOf(detail))));
      }

      public override void SyncData(IDataStore dataStore)
      {
      }

      private static NativeQuestPhase PhaseOf(QuestBase.QuestCompleteDetails detail)
         => detail switch {
            QuestBase.QuestCompleteDetails.Success => NativeQuestPhase.Succeeded,
            QuestBase.QuestCompleteDetails.Fail => NativeQuestPhase.Failed,
            QuestBase.QuestCompleteDetails.FailWithBetrayal => NativeQuestPhase.Betrayed,
            QuestBase.QuestCompleteDetails.Timeout => NativeQuestPhase.TimedOut,
            QuestBase.QuestCompleteDetails.Cancel => NativeQuestPhase.Cancelled,
            _ => NativeQuestPhase.Invalid
         };

      private static void Remember(QuestBase quest, NativeQuestPhase phase)
      {
         if (quest == null || quest is CalradiaRemembersQuest || quest is CommitmentQuest || quest is AuthoredStoryQuest) return;
         Hero giver = quest.QuestGiver;
         if (giver == null || giver == Hero.MainHero) return;

         Record(giver.StringId, phase, quest.Title?.ToString());
      }

      /// <summary>Writes the memory (cr.testall drives it with a quest title of its own).</summary>
      internal static MemoryResult Record(string giverId, NativeQuestPhase phase, string questTitle)
      {
         NativeQuestMemory m = NativeQuestMemory.For(phase, Hero.MainHero?.Name?.ToString(), questTitle);
         if (m == null) return MemoryResult.RejectedInvalid;

         MemoryResult result = CrNpcMemory.AddMemory(new CrMemoryEntry {
            NpcId = giverId, Summary = m.Summary, Kind = m.Kind.ToString(), DaysAgo = 0, SourceMod = "CalradiaRemembers (native quests)"
         });
         ModLog.Info($"[NATIVE QUEST] {phase} '{questTitle}' remembered by {giverId}: {result}.");

         return result;
      }
   }
}
