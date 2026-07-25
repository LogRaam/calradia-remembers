// Code written by Gabriel Mailhot, 24/07/2026.
// Extension Surface: the PUBLIC read/write door onto an NPC's stored memory. A third-party mod reads what CR
// already holds for an NPC (to avoid injecting duplicates) and writes new memories the NPC will treat as its
// own recollection in later conversations. Mirrors the Feed facade: validate at the door, resolve hero ids as
// stable StringIds, mutate nothing the caller owns, and never let a bad call throw into the game.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   The public NPC-memory API. A caller identifies the NPC by its hero StringId (never a display name),
   ///   reads its stored memories, and injects new ones. Injected memories become ordinary profile events, so
   ///   they surface in later conversations exactly like memories CR recorded itself.
   /// </summary>
   public static class CrNpcMemory
   {
      /// <summary>
      ///   The API's capability version, bumped only on a breaking change to a public member of
      ///   <see cref="CrNpcMemory" />, <see cref="CrConversation" />, or their contract types. A reflection-based
      ///   caller (a mod with no compile-time reference to CR) reads this before calling anything.
      /// </summary>
      public const int ApiVersion = 1;

      /// <summary>
      ///   The memories CR currently holds for the NPC with this hero StringId, oldest first. Empty when the id
      ///   is unknown, the NPC has no profile yet, or no campaign is loaded. Read this to avoid injecting a
      ///   duplicate of something CR already remembers.
      /// </summary>
      public static IReadOnlyList<CrMemoryRecord> GetMemories(string npcId)
      {
         if (string.IsNullOrWhiteSpace(npcId)) return new List<CrMemoryRecord>();

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcId);

         if (profile?.Events == null) return new List<CrMemoryRecord>();

         return profile.Events
                       .Select(e => new CrMemoryRecord(e.gameDay, e.type.ToString(), e.summary))
                       .ToList();
      }

      /// <summary>
      ///   Injects a memory onto an NPC. The NPC must resolve to a real hero; its profile is created if it does
      ///   not exist yet (so a caller can seed an NPC the player has never met). Returns why it was rejected, or
      ///   <see cref="MemoryResult.Added" /> on success. Mutates nothing the caller owns.
      /// </summary>
      public static MemoryResult AddMemory(CrMemoryEntry entry)
      {
         if (entry == null || string.IsNullOrWhiteSpace(entry.NpcId) || string.IsNullOrWhiteSpace(entry.Summary))
            return MemoryResult.RejectedInvalid;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null) return MemoryResult.RejectedNoStore;

         Hero hero = Hero.FindFirst(h => h.StringId == entry.NpcId);

         if (hero == null) return MemoryResult.RejectedUnknownHero;

         var today = (int) CampaignTime.Now.ToDays;
         NotableEvent injected = NpcMemoryInjection.BuildInjectedEvent(entry.Summary, entry.Kind, entry.DaysAgo, today);

         if (injected == null) return MemoryResult.RejectedInvalid;

         NpcProfile profile = behavior.Store.Get(entry.NpcId) ?? HeroProfileMapper.FromHero(hero);
         profile.Events.Add(injected);
         behavior.Store.Set(profile);
         ModLog.Info($"[MEMORY API] {entry.SourceMod ?? "a mod"} injected a memory on {hero.Name} (day {injected.gameDay}, {injected.type}): {injected.summary}");

         return MemoryResult.Added;
      }
   }
}
