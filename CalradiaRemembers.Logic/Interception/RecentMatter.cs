// Code written by Gabriel Mailhot, 05/09/2026.
// The pure core of the "recent-matter approach" (design [[project_npc_recent_matter]]): when a lord approaches
// the player to raise a recent matter, he takes the MOST RECENT one he knows of, and HOW he speaks of it depends
// on his relationship to it (the register). This file is engine-agnostic: the mod builds classified candidates
// (from the NPC's profile memories + the world events he knows of, with the participant/player-involvement flags),
// and this selector picks the freshest one he has not already raised. Phase 1 covers matters involving the player
// (a lived memory, or a rumor about the player's own deeds); the world-gossip register is a later phase.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Interception
{
   /// <summary>How a lord relates to a recent matter, which decides the tone he raises it in.</summary>
   public enum RecentMatterRegister
   {
      /// <summary>He lived it: a first-hand experience (a profile memory, or a world event he took part in). Spoken in the first person, as his own recollection.</summary>
      LivedMemory,

      /// <summary>He only heard of it: a world event about the PLAYER's deeds he did not take part in. Spoken as hearsay.</summary>
      RumorAboutPlayer,

      /// <summary>
      ///   World gossip that does NOT involve the player and that he did not take part in: a noteworthy happening
      ///   (a realm readying for war, a court scandal) he only heard of. He does not comment on it, he ASKS the
      ///   player what THEY know. Always yields to a matter that involves the player (below), so a lord raises
      ///   something between the two of them first, and only reaches for gossip when nothing personal is fresh.
      /// </summary>
      WorldGossip
   }

   /// <summary>One recent matter a lord could raise with the player: what it is, when it happened, its register, and a stable key for the once-per-NPC guard.</summary>
   public sealed class RecentMatter
   {
      public RecentMatter(string key, int day, string summary, RecentMatterRegister register,
         bool touchesTheSpeaker = false, int weight = 0)
      {
         Key = key ?? "";
         Day = day;
         Summary = summary ?? "";
         Register = register;
         TouchesTheSpeaker = touchesTheSpeaker;
         Weight = weight;
      }

      /// <summary>Stable dedup key (NOT NPC-scoped: the caller scopes it per NPC). A world event uses its Id; a profile memory a day+type+summary composite.</summary>
      public string Key { get; }

      /// <summary>The in-game day the matter happened (freshest wins).</summary>
      public int Day { get; }

      /// <summary>The plain fact, named in the opener so the lord raises THIS matter rather than whatever the model invents.</summary>
      public string Summary { get; }

      public RecentMatterRegister Register { get; }

      /// <summary>
      ///   Whether this happened in THIS speaker's own place or to his own people: his settlement, his clan's
      ///   holdings, or a faction he belongs to. The mechanical stand-in for "does it affect him".
      /// </summary>
      public bool TouchesTheSpeaker { get; }

      /// <summary>
      ///   How big the thing was, carried straight from the world event's Magnitude. Zero for a profile memory,
      ///   which is never weighed against anything (see <see cref="RecentMatterSelector" />: size only ever
      ///   sorts gossip against gossip).
      /// </summary>
      public int Weight { get; }
   }

   /// <summary>Picks the single matter a lord raises: the most recent one, within a recency window, that he has not already raised.</summary>
   public static class RecentMatterSelector
   {
      /// <summary>
      ///   Marks a ledger entry for gossip only OFFERED (named in an invitation the player saw, taken up or declined),
      ///   not yet spoken: "offered:npcId|matterKey". It bars everyone else, not the one who offered it, who must still
      ///   find his matter when the player accepts.
      /// </summary>
      public const string OfferedPrefix = "offered:";

      /// <summary>
      ///   True when this piece of world gossip was already raised with the player, or offered to them, by anyone other
      ///   than the one who offered it. The once-per-NPC ledger (entries "npcId|matterKey") stopped one character asking
      ///   twice, never the next one: froggyluv (Nexus, 17/10/2026) was stopped in town after town by someone asking what
      ///   he knew of the same war, then of the same capture, "So it seems kind of stuck in a loop". Each piece of news
      ///   is asked about once, by whoever asks first.
      /// </summary>
      public static bool GossipAlreadyRaisedByAnyone(IEnumerable<string> raisedLedger, string matterKey, string askingNpcId)
      {
         if (string.IsNullOrEmpty(matterKey) || raisedLedger == null) return false;

         string ownOffer = OfferedPrefix + askingNpcId + "|" + matterKey;

         return raisedLedger.Any(entry => entry != null && entry != ownOffer && entry.EndsWith("|" + matterKey, StringComparison.Ordinal));
      }

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

      // A matter that involves the player (a lived memory or a rumor about their deeds) ALWAYS beats impersonal
      // world gossip, so a lord raises something between the two of them before reaching for hearsay about others.
      // Within the same tier: freshest wins; on an exact-day tie the lower register wins (lived before rumor before
      // gossip); on a further tie the smaller key wins, so the choice is stable regardless of enumeration order.
      private static bool IsBetter(RecentMatter a, RecentMatter b)
      {
         bool aPersonal = a.Register != RecentMatterRegister.WorldGossip;
         bool bPersonal = b.Register != RecentMatterRegister.WorldGossip;
         if (aPersonal != bPersonal) return aPersonal;

         // AMONG GOSSIP, NEARNESS THEN SIZE, AND ONLY THEN THE CLOCK.
         //
         // fkasad (Nexus, 19/09/2026): "Less than an hour ago, I arrived at a village that was being looted by
         // an enemy army, intending to defend it. The notable waiting at the gate called out to me to talk
         // about a new war taking place much further south, a conflict we aren't even involved in. Damn it!
         // His village was burning."
         //
         // Nothing was broken in the gathering: the notable knew both. The choice between them was made on the
         // DAY ALONE, so a war declared this morning three hundred leagues away outranked the raid on his own
         // village that started yesterday. Recency is a fine tie-break and a terrible first question.
         //
         // The data to do better was already on the world event and was being thrown away on the way in:
         // PlaceId and FactionIds say whether it touches him, Magnitude says how big it was. No thresholds are
         // introduced here and none are needed; this is an ORDER, not a filter, so nothing is ever hidden that
         // was not already going to lose to something else.
         //
         // Deliberately scoped to gossip. A matter between him and the player keeps exactly the ordering it
         // had, because weighing a quiet conversation memory against a battle by "size" would bury the most
         // personal material under the loudest, which is the opposite of the point.
         if (!aPersonal)
         {
            if (a.TouchesTheSpeaker != b.TouchesTheSpeaker) return a.TouchesTheSpeaker;
            if (a.Weight != b.Weight) return a.Weight > b.Weight;
         }

         if (a.Day != b.Day) return a.Day > b.Day;
         if (a.Register != b.Register) return a.Register < b.Register;

         return string.CompareOrdinal(a.Key, b.Key) < 0;
      }
   }
}
