// Code written by Gabriel Mailhot, 05/09/2026.
// The pure core of the "recent-matter approach" (design [[project_npc_recent_matter]]): when a lord approaches
// the player to raise a recent matter, he takes the MOST RECENT one he knows of, and HOW he speaks of it depends
// on his relationship to it (the register). This file is engine-agnostic: the mod builds classified candidates
// (from the NPC's profile memories + the world events he knows of, with the participant/player-involvement flags),
// and this selector picks the freshest one he has not already raised. Phase 1 covers matters involving the player
// (a lived memory, or a rumor about the player's own deeds); the world-gossip register is a later phase.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Interception
{
   /// <summary>How a lord relates to a recent matter, which decides the tone he raises it in.</summary>
   public enum RecentMatterRegister
   {
      /// <summary>He lived it: a first-hand experience (a profile memory, or a world event he took part in). Spoken in the first person, as his own recollection.</summary>
      LivedMemory,

      /// <summary>He only heard of it: a world event about the PLAYER's deeds he did not take part in. Spoken as hearsay.</summary>
      RumorAboutPlayer
   }

   /// <summary>One recent matter a lord could raise with the player: what it is, when it happened, its register, and a stable key for the once-per-NPC guard.</summary>
   public sealed class RecentMatter
   {
      public RecentMatter(string key, int day, string summary, RecentMatterRegister register)
      {
         Key = key ?? "";
         Day = day;
         Summary = summary ?? "";
         Register = register;
      }

      /// <summary>Stable dedup key (NOT NPC-scoped: the caller scopes it per NPC). A world event uses its Id; a profile memory a day+type+summary composite.</summary>
      public string Key { get; }

      /// <summary>The in-game day the matter happened (freshest wins).</summary>
      public int Day { get; }

      /// <summary>The plain fact, named in the opener so the lord raises THIS matter rather than whatever the model invents.</summary>
      public string Summary { get; }

      public RecentMatterRegister Register { get; }
   }

   /// <summary>Picks the single matter a lord raises: the most recent one, within a recency window, that he has not already raised.</summary>
   public static class RecentMatterSelector
   {
      /// <summary>
      ///   From the classified <paramref name="candidates" />, returns the freshest matter within
      ///   <paramref name="windowDays" /> of <paramref name="today" /> whose key is not in <paramref name="raisedKeys" />
      ///   (the once-per-NPC guard), or null when the lord has nothing fresh left to raise. A future-dated or
      ///   blank-key candidate is ignored. Ties on the same day break toward a LIVED memory first (a shared
      ///   experience is more natural to open on than distant hearsay), then by key for a deterministic result.
      /// </summary>
      public static RecentMatter PickFreshest(
         IEnumerable<RecentMatter> candidates,
         ISet<string> raisedKeys,
         int today,
         int windowDays)
      {
         if (candidates == null) return null;

         RecentMatter best = null;

         foreach (RecentMatter m in candidates)
         {
            if (m == null || string.IsNullOrEmpty(m.Key)) continue;
            if (m.Day > today || today - m.Day > windowDays) continue;
            if (raisedKeys != null && raisedKeys.Contains(m.Key)) continue;

            if (best == null || IsBetter(m, best)) best = m;
         }

         return best;
      }

      // Freshest wins; on an exact-day tie a lived memory beats a rumor; on a further tie the smaller key wins so
      // the choice is stable regardless of enumeration order.
      private static bool IsBetter(RecentMatter a, RecentMatter b)
      {
         if (a.Day != b.Day) return a.Day > b.Day;
         if (a.Register != b.Register) return a.Register == RecentMatterRegister.LivedMemory;

         return string.CompareOrdinal(a.Key, b.Key) < 0;
      }
   }
}
