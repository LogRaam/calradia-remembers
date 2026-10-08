// Code written by Gabriel Mailhot, 08/10/2026.
// The memory a quest giver keeps of one of the game's own quests (tashmetu and Maifailun, Nexus, 08/10/2026: nothing of
// a native quest was remembered once the game's dialogue closed on it). Written by the giver, in the giver's words.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Where a native quest stands, as the game reports it.</summary>
   public enum NativeQuestPhase
   {
      Started,
      Succeeded,
      Failed,
      TimedOut,
      Betrayed,
      Cancelled,
      Invalid
   }

   /// <summary>A memory for the giver of a native quest.</summary>
   public sealed class NativeQuestMemory
   {
      public NotableEventType Kind { get; init; }
      public string Summary { get; init; }

      /// <summary>The giver's memory of the quest at this phase, or null when there is nothing to remember (voided, untitled).</summary>
      public static NativeQuestMemory For(NativeQuestPhase phase, string playerName, string questTitle)
      {
         if (string.IsNullOrWhiteSpace(questTitle)) return null;
         string who = string.IsNullOrWhiteSpace(playerName) ? "the player" : playerName.Trim();
         string task = questTitle.Trim();

         return phase switch {
            NativeQuestPhase.Started => new NativeQuestMemory {Kind = NotableEventType.Agreement, Summary = $"I asked {who} to see to a task of mine: {task}."},
            NativeQuestPhase.Succeeded => new NativeQuestMemory {Kind = NotableEventType.Collaboration, Summary = $"{who} did what I asked: {task}."},
            NativeQuestPhase.Failed => new NativeQuestMemory {Kind = NotableEventType.Other, Summary = $"{who} failed the task I set: {task}."},
            NativeQuestPhase.TimedOut => new NativeQuestMemory {Kind = NotableEventType.Other, Summary = $"The task I set {who} ran out of time undone: {task}."},
            NativeQuestPhase.Betrayed => new NativeQuestMemory {Kind = NotableEventType.Betrayal, Summary = $"{who} turned against the task I set: {task}."},
            NativeQuestPhase.Cancelled => new NativeQuestMemory {Kind = NotableEventType.Other, Summary = $"The task I set {who} came to nothing: {task}."},
            _ => null
         };
      }
   }
}
