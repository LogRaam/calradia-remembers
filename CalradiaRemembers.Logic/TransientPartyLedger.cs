// Code written by Gabriel Mailhot, 26/09/2026.
// The bookkeeping of the mod's TRANSIENT parties: the ones it spawns for a job and never saves (couriers, sergeants,
// bounty hunters). The event-listeners map showed the same shape copied three times (spawn, steer, never saved,
// despawn before a save, a flag so the mod's own despawn is not read as a loss), which is the map proposing an
// architecture change rather than guarding one (Gabriel, 26/09/2026). This is the pure half of the one service that
// replaces the copies: which party is whose, for what role, with what tag (the realm a band of hunters answers to).
//
// Pure: the engine half (TransientPartyRegistry) spawns nothing itself; it records what its owners spawn, despawns
// them before a save, and tells an owner when one of its parties is truly destroyed.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>One tracked transient party.</summary>
   public sealed class TransientPartyEntry
   {
      public string PartyId { get; set; } = "";

      /// <summary>What it is for ("arrest.sergeants", "arrest.hunters").</summary>
      public string Role { get; set; } = "";

      /// <summary>An owner's own label for it (the realm a band of hunters answers to), or null.</summary>
      public string Tag { get; set; }
   }

   /// <summary>Which transient party is whose. Pure bookkeeping.</summary>
   public sealed class TransientPartyLedger
   {
      private readonly Dictionary<string, TransientPartyEntry> _byId = new(StringComparer.Ordinal);

      /// <summary>How many parties are tracked.</summary>
      public int Count => _byId.Count;

      /// <summary>Tracks a party under a role (and an optional tag), replacing any earlier entry for the same id.</summary>
      public void Track(string partyId, string role, string tag = null)
      {
         if (string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(role)) return;

         _byId[partyId] = new TransientPartyEntry {PartyId = partyId, Role = role, Tag = tag};
      }

      /// <summary>Stops tracking a party; true when it was tracked.</summary>
      public bool Forget(string partyId) => !string.IsNullOrEmpty(partyId) && _byId.Remove(partyId);

      /// <summary>The entry for a party, or null when it is not one of ours.</summary>
      public TransientPartyEntry Find(string partyId)
         => !string.IsNullOrEmpty(partyId) && _byId.TryGetValue(partyId, out TransientPartyEntry e) ? e : null;

      /// <summary>Every tracked party of a role, in id order (stable for tests and logs).</summary>
      public IReadOnlyList<TransientPartyEntry> OfRole(string role)
         => _byId.Values.Where(e => e.Role == role).OrderBy(e => e.PartyId, StringComparer.Ordinal).ToList();

      /// <summary>The tracked party of a role carrying a tag, or null.</summary>
      public TransientPartyEntry WithTag(string role, string tag)
         => _byId.Values.FirstOrDefault(e => e.Role == role && e.Tag == tag);

      /// <summary>Every tracked id (what a save must despawn).</summary>
      public IReadOnlyList<string> AllIds() => _byId.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();

      /// <summary>Forgets every entry whose party no longer exists, as the caller reports; returns how many.</summary>
      public int ForgetMissing(Func<string, bool> exists)
      {
         if (exists == null) return 0;

         List<string> gone = _byId.Keys.Where(id => !exists(id)).ToList();
         foreach (string id in gone) _byId.Remove(id);

         return gone.Count;
      }
   }
}
