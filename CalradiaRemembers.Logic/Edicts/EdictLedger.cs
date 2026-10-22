// Code written by Gabriel Mailhot, 22/10/2026.
// The edicts decreed in this campaign (Gabriel, 21/10/2026: "ville et royaume. Les PNJ peuvent aussi. Un edit peut etre
// conteste."). Saved as one additive JSON key; an old save loads with an empty ledger. Which edict holds on a town is
// read here, so the game's models, the prompt and the console all agree: a town's own edict comes first, else its
// realm's, unless the town forced the realm's edict off by contesting it.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Edicts
{
   /// <summary>How an edict stands.</summary>
   public enum EdictStatus
   {
      /// <summary>In force.</summary>
      Active,

      /// <summary>Its term ran out.</summary>
      Expired,

      /// <summary>Whoever decreed it no longer holds the town or the throne, or is gone.</summary>
      Lapsed,

      /// <summary>The people contested it until it was withdrawn (everywhere, for a town edict).</summary>
      Repealed,

      /// <summary>Withdrawn by whoever decreed it.</summary>
      Withdrawn
   }

   /// <summary>One edict decreed in this campaign (saved as JSON; every field additive).</summary>
   public sealed class Edict
   {
      /// <summary>Stable id ("edict_12").</summary>
      public string Id { get; set; }

      /// <summary>Which edict of the catalogue.</summary>
      public EdictKind Kind { get; set; }

      /// <summary>One town, or a whole realm.</summary>
      public EdictScope Scope { get; set; }

      /// <summary>The town's settlement id, or the realm's kingdom id.</summary>
      public string TargetId { get; set; }

      /// <summary>Whoever decreed it.</summary>
      public string EnactorId { get; set; }

      /// <summary>The name it is known by ("the Bread Edict"); the catalogue's title when none was given.</summary>
      public string Name { get; set; }

      /// <summary>The player decreed it.</summary>
      public bool ByPlayer { get; set; }

      /// <summary>The day it was decreed.</summary>
      public int EnactedDay { get; set; }

      /// <summary>The day its term runs out.</summary>
      public int EndDay { get; set; }

      /// <summary>How it stands.</summary>
      public EdictStatus Status { get; set; }

      /// <summary>The day it stopped holding, when it did.</summary>
      public int EndedDay { get; set; }

      /// <summary>Days of grievance per town (settlement id): how long its people have suffered it while restless.</summary>
      public Dictionary<string, int> Grievance { get; set; } = new();

      /// <summary>The towns whose people have openly contested it.</summary>
      public List<string> ContestedIn { get; set; } = new();

      /// <summary>The towns that forced a realm edict off: it no longer holds there.</summary>
      public List<string> RepealedIn { get; set; } = new();

      /// <summary>
      ///   What it does, read from the catalogue each time. A method, not a property: a save must never carry the
      ///   catalogue's numbers, or loading an old save would write them back into the shared catalogue.
      /// </summary>
      public EdictEffect Effect() => EdictCatalog.Of(Kind);

      /// <summary>Days left in force (0 once ended or run out).</summary>
      public int DaysLeft(int today) => Status != EdictStatus.Active ? 0 : System.Math.Max(0, EndDay - today);
   }

   /// <summary>The campaign's edicts and which one holds where.</summary>
   public sealed class EdictLedger
   {
      /// <summary>TUNE: ended edicts kept for the realm's memory and the console; older ones are dropped.</summary>
      public const int EndedKept = 30;

      /// <summary>Every edict, in force or ended.</summary>
      public List<Edict> Edicts { get; set; } = new();

      /// <summary>The last id number handed out.</summary>
      public int LastId { get; set; }

      /// <summary>The edicts in force (a method, so a save never carries it).</summary>
      public IEnumerable<Edict> InForce() => Edicts.Where(e => e.Status == EdictStatus.Active);

      /// <summary>
      ///   Drops edicts of a kind this build does not know (a save from a later build): the ledger loads with the rest
      ///   rather than failing whole. Returns how many were dropped.
      /// </summary>
      public int DropUnknownKinds() => Edicts.RemoveAll(e => e == null || !System.Enum.IsDefined(typeof(EdictKind), e.Kind));

      /// <summary>The town's own edict in force, or null.</summary>
      public Edict OwnOf(string townId)
         => string.IsNullOrEmpty(townId) ? null : InForce().FirstOrDefault(e => e.Scope == EdictScope.Town && e.TargetId == townId);

      /// <summary>The realm's edict in force, or null.</summary>
      public Edict OfRealm(string kingdomId)
         => string.IsNullOrEmpty(kingdomId) ? null : InForce().FirstOrDefault(e => e.Scope == EdictScope.Realm && e.TargetId == kingdomId);

      /// <summary>
      ///   The edict that holds on this town: its own first, else its realm's unless this town forced it off; null when
      ///   none does. One edict per town at a time, so its effects never stack.
      /// </summary>
      public Edict HoldingOn(string townId, string kingdomId)
      {
         Edict own = OwnOf(townId);
         if (own != null) return own;
         Edict realm = OfRealm(kingdomId);

         return realm != null && !realm.RepealedIn.Contains(townId) ? realm : null;
      }

      /// <summary>Records a new edict in force, with a fresh id.</summary>
      public Edict Decree(Edict e)
      {
         LastId++;
         e.Id = $"edict_{LastId}";
         e.Status = EdictStatus.Active;
         Edicts.Add(e);

         return e;
      }

      /// <summary>Ends an edict, and forgets the oldest ended ones past <see cref="EndedKept" />.</summary>
      public void End(Edict e, EdictStatus how, int today)
      {
         if (e == null || e.Status != EdictStatus.Active) return;
         e.Status = how;
         e.EndedDay = today;

         List<Edict> ended = Edicts.Where(x => x.Status != EdictStatus.Active).OrderBy(x => x.EndedDay).ToList();
         foreach (Edict old in ended.Take(ended.Count - EndedKept)) Edicts.Remove(old);
      }

      /// <summary>The day the last edict on this town (its own) ended; null when none did.</summary>
      public int? LastEndedOn(string townOrRealmId)
         => Edicts.Where(e => e.TargetId == townOrRealmId && e.Status != EdictStatus.Active).Select(e => (int?) e.EndedDay).Max();
   }
}
