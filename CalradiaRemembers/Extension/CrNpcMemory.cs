// Code written by Gabriel Mailhot, 24/07/2026.
// Extension Surface: the PUBLIC read/write door onto an NPC's stored memory. A third-party mod reads what CR
// already holds for an NPC (to avoid injecting duplicates) and writes new memories the NPC will treat as its
// own recollection in later conversations. Mirrors the Feed facade: validate at the door, resolve hero ids as
// stable StringIds, mutate nothing the caller owns, and never let a bad call throw into the game.

#region

using System;
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
      ///   <para>
      ///     PRIVATE memories are withheld (audit, 13/09/2026). A private memory is one CR deliberately keeps
      ///     out of every other character's prompt - a word the player spoke to this companion alone - and
      ///     <see cref="CrMemoryRecord" /> has no field in which to say so, so a caller could neither know nor
      ///     honour it. Withholding costs this API nothing: its stated job is to let a caller avoid duplicating
      ///     what CR already knows, and nothing outside CR ever writes a private memory to duplicate.
      ///   </para>
      /// </summary>
      public static IReadOnlyList<CrMemoryRecord> GetMemories(string npcId)
      {
         if (string.IsNullOrWhiteSpace(npcId)) return new List<CrMemoryRecord>();

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcId);

         if (profile?.Events == null) return new List<CrMemoryRecord>();

         return profile.Events
                       .Where(e => e is {IsPrivate: false})
                       .Select(e => new CrMemoryRecord(e.gameDay, e.type.ToString(), e.summary))
                       .ToList();
      }

      /// <summary>
      ///   The character's own personal regard for the player, on Calradia Remembers' ledger (the one built in
      ///   conversation, -100..100; never the game's relation), or null when CR holds no profile for them yet
      ///   (tashmetu, 07/10/2026). Read-only: regard moves only through CR's own deeds; RegardChanged reports each move.
      /// </summary>
      public static int? GetRegard(string npcId)
      {
         if (string.IsNullOrWhiteSpace(npcId)) return null;

         return NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcId)?.ReputationWithPlayer;
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
         NotableEvent injected = NpcMemoryInjection.BuildInjectedEvent(entry.Summary, entry.Kind, entry.DaysAgo, today, entry.Private);

         if (injected == null) return MemoryResult.RejectedInvalid;

         NpcProfile profile = behavior.Store.Get(entry.NpcId) ?? HeroProfileMapper.FromHero(hero);
         profile.Events.Add(injected);
         behavior.Store.Set(profile);
         ModLog.Info($"[MEMORY API] {entry.SourceMod ?? "a mod"} injected a memory on {hero.Name} (day {injected.gameDay}, {injected.type}): {injected.summary}");

         return MemoryResult.Added;
      }

      /// <summary>
      ///   Fired each time a character's PERSONAL REGARD for the player changes, whatever changed it (a conversation, a
      ///   letter, a quest, a grievance put to rest, jealousy...), with the value before and after and the cause. Only
      ///   an actual change is announced. Asked for through tashmetu (29/09/2026), for a player who wanted regard to
      ///   move the game's own relation.
      ///   <para>
      ///     CR itself never writes the game's relation ("clan standing") from regard, on purpose: the native value is
      ///     effectively clan-wide, and regard's pacing (a +1 cap, a cooldown) is what stops a player talking their way
      ///     to the top. A listener that mirrors regard into it takes that balance on itself. Listeners only listen:
      ///     each is called under its own guard, on the game thread, after the change has happened.
      ///   </para>
      /// </summary>
      public static event Action<CrRegardChange> RegardChanged;

      /// <summary>Re-raises the SDK's one regard-change notice as this public event (subscribed once, at module load).</summary>
      internal static void ForwardRegardChange(NpcMemoryService.Core.Standing.RegardChange change)
      {
         Action<CrRegardChange> handlers = RegardChanged;
         if (handlers == null || change == null) return;

         var arg = new CrRegardChange {
            NpcId = change.NpcId, Before = change.Before, After = change.After,
            Cause = NpcMemoryService.Core.Standing.RegardCause.AsReported(change.Cause, Diagnostics.CrSelfTest.IsRunning),
            SaveId = CrConversation.CurrentSaveId(), GameDay = Campaign.Current != null ? (int) CampaignTime.Now.ToDays : 0
         };

         foreach (Delegate handler in handlers.GetInvocationList())
            try { ((Action<CrRegardChange>) handler)(arg); }
            catch (Exception ex)
            {
               ModLog.Error($"[MEMORY API] a RegardChanged subscriber threw and was isolated: {ex.GetType().Name}: {ex.Message}");
            }
      }
   }

   /// <summary>One change of a character's personal regard for the player (see <see cref="CrNpcMemory.RegardChanged" />).</summary>
   public sealed class CrRegardChange
   {
      /// <summary>The character's hero StringId.</summary>
      public string NpcId { get; set; }

      /// <summary>The loaded campaign's unique id, as in the conversation events.</summary>
      public string SaveId { get; set; }

      /// <summary>The in-game day of the change.</summary>
      public int GameDay { get; set; }

      /// <summary>Regard before the change, -100 to 100 (CR's own ledger, never the game's relation).</summary>
      public int Before { get; set; }

      /// <summary>Regard after the change, -100 to 100. Never equal to <see cref="Before" />.</summary>
      public int After { get; set; }

      /// <summary>
      ///   Why it changed: "conversation", "letter", "quest", "commitment", "compensation", "grudge", "jealousy",
      ///   "divorce", "rescue", "stance", "violence", "departure" or "unspecified". New causes may be added.
      /// </summary>
      public string Cause { get; set; }
   }
}
