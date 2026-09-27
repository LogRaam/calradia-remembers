// Code written by Gabriel Mailhot, 26/09/2026.
// The one service for the mod's TRANSIENT parties (sergeants, bounty hunters; couriers later): parties spawned for a
// job and never saved. The event-listeners map showed the same shape copied in each owner (a despawn listener on
// OnBeforeSaveEvent, a MobilePartyDestroyed listener, a flag so the mod's own despawn is not read as a loss), which is
// the map proposing an architecture change (Gabriel, 26/09/2026). One listener of each now, here.
//
//   - An owner spawns its party as before (TravelingParty.Spawn) and hands it to Track, with a role, an optional tag,
//     and what to do if the party is truly destroyed (beaten, starved).
//   - Before every save, every tracked party is taken off the map; owners send them again after a load, as they
//     always did, since a transient party is never in the save.
//   - Release takes one off the map on the owner's word; that despawn is never reported as a loss.
//   - A party tracked with a RESTORE is put back where it stood once the save is over, the couriers' own recipe
//     generalised (26/09/2026). Without it, a save sent sergeants and bounty hunters back to their town on the next
//     hour, so with frequent autosaves a band riding from far off might never reach the player. After a LOAD nothing
//     is restored (the party was never saved): owners send theirs again, as before.
//
// The bookkeeping is TransientPartyLedger's (pure, tested).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Travel
{
   /// <summary>Tracks the mod's transient parties, despawns them before a save, and reports a true loss to their owner.</summary>
   public sealed class TransientPartyRegistry : CampaignBehaviorBase
   {
      public static TransientPartyRegistry Instance { get; private set; }

      private readonly TransientPartyLedger _ledger = new();
      private readonly Dictionary<string, Action<MobileParty, PartyBase>> _onDestroyed = new(StringComparer.Ordinal);
      private readonly Dictionary<string, Func<Vec2, MobileParty>> _onRestore = new(StringComparer.Ordinal);

      // What a save took off the map and will put back once it is over.
      private readonly List<Pending> _toRestore = new();

      // Set while the registry itself despawns a party, so that despawn is never reported as a loss.
      private bool _releasing;

      public TransientPartyRegistry()
      {
         Instance = this;
      }

      public override void RegisterEvents()
      {
         CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, ReleaseAll);
         CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(this, OnSaveOver);
         CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
      }

      public override void SyncData(IDataStore dataStore)
      {
         // Nothing: a transient party is never saved. Its owner sends it again after a load.
      }

      /// <summary>
      ///   Tracks a party its owner just spawned. <paramref name="onDestroyed" /> is told if the party is TRULY
      ///   destroyed (beaten, starved), never when the registry itself takes it off the map.
      /// </summary>
      public void Track(MobileParty party, string role, string tag = null, Action<MobileParty, PartyBase> onDestroyed = null,
                        Func<Vec2, MobileParty> onRestore = null)
      {
         if (party == null) return;

         _ledger.Track(party.StringId, role, tag);
         if (onDestroyed != null) _onDestroyed[party.StringId] = onDestroyed;
         else _onDestroyed.Remove(party.StringId);
         if (onRestore != null) _onRestore[party.StringId] = onRestore;
         else _onRestore.Remove(party.StringId);
      }

      /// <summary>Test seam (cr.testall): a save's despawn and the restore that follows it, without saving.</summary>
      internal void DebugSaveCycle()
      {
         ReleaseAll();
         OnSaveOver(true, "cr_selftest");
      }

      /// <summary>The live tracked parties of a role (entries whose party is gone are dropped first).</summary>
      public IReadOnlyList<MobileParty> OfRole(string role)
      {
         SweepMissing();

         return _ledger.OfRole(role).Select(e => Find(e.PartyId)).Where(p => p != null).ToList();
      }

      /// <summary>The live tracked party of a role carrying a tag, or null.</summary>
      public MobileParty WithTag(string role, string tag)
      {
         SweepMissing();
         TransientPartyEntry entry = _ledger.WithTag(role, tag);

         return entry == null ? null : Find(entry.PartyId);
      }

      /// <summary>The entry for a party, or null when it is not one of the mod's transient parties.</summary>
      public TransientPartyEntry EntryOf(MobileParty party) => party == null ? null : _ledger.Find(party.StringId);

      /// <summary>Takes one tracked party off the map on its owner's word; never reported as a loss.</summary>
      public void Release(MobileParty party)
      {
         if (party == null) return;

         Forget(party.StringId);
         Despawn(party);
      }

      /// <summary>Takes every tracked party of a role off the map.</summary>
      public void ReleaseRole(string role)
      {
         foreach (TransientPartyEntry entry in _ledger.OfRole(role))
         {
            MobileParty party = Find(entry.PartyId);
            Forget(entry.PartyId);
            Despawn(party);
         }
      }

      #region private

      /// <summary>
      ///   Before a save: every tracked party off the map, whatever its role; one tracked with a restore is remembered,
      ///   where it stood, to be put back once the save is over.
      /// </summary>
      private void ReleaseAll()
      {
         _toRestore.Clear();

         foreach (string id in _ledger.AllIds())
         {
            MobileParty party = Find(id);
            TransientPartyEntry entry = _ledger.Find(id);
            _onDestroyed.TryGetValue(id, out Action<MobileParty, PartyBase> destroyed);
            _onRestore.TryGetValue(id, out Func<Vec2, MobileParty> restore);

            if (party != null && restore != null && entry != null)
               _toRestore.Add(new Pending(entry.Role, entry.Tag, party.GetPosition2D, destroyed, restore));

            Forget(id);
            Despawn(party);
         }
      }

      /// <summary>The save is over: each party that asked for it is put back where it stood, and tracked again.</summary>
      private void OnSaveOver(bool isSuccessful, string saveName)
      {
         List<Pending> pending = _toRestore.ToList();
         _toRestore.Clear();

         foreach (Pending p in pending)
            try
            {
               MobileParty again = p.Restore(p.Position);
               if (again != null) Track(again, p.Role, p.Tag, p.OnDestroyed, p.Restore);
            }
            catch (Exception ex)
            {
               ModLog.Error($"[TRANSIENT] restoring a {p.Role} party after the save failed: {ex.GetType().Name}: {ex.Message}");
            }

         if (pending.Count > 0) ModLog.Info($"[TRANSIENT] {pending.Count} transient part(y/ies) put back after the save.");
      }

      private sealed class Pending
      {
         public Pending(string role, string tag, Vec2 position, Action<MobileParty, PartyBase> onDestroyed, Func<Vec2, MobileParty> restore)
         {
            Role = role;
            Tag = tag;
            Position = position;
            OnDestroyed = onDestroyed;
            Restore = restore;
         }

         public string Role { get; }
         public string Tag { get; }
         public Vec2 Position { get; }
         public Action<MobileParty, PartyBase> OnDestroyed { get; }
         public Func<Vec2, MobileParty> Restore { get; }
      }

      /// <summary>A tracked party destroyed by anything but the registry: its owner is told, then it is forgotten.</summary>
      private void OnPartyDestroyed(MobileParty destroyed, PartyBase destroyer)
      {
         if (_releasing || destroyed == null) return;

         string id = destroyed.StringId;
         if (_ledger.Find(id) == null) return;

         _onDestroyed.TryGetValue(id, out Action<MobileParty, PartyBase> owner);
         Forget(id);

         try { owner?.Invoke(destroyed, destroyer); }
         catch (Exception ex) { ModLog.Error($"[TRANSIENT] an owner's loss handler failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void Despawn(MobileParty party)
      {
         if (party == null) return;

         _releasing = true;
         try { TravelingParty.Destroy(party); }
         catch (Exception ex) { ModLog.Error($"[TRANSIENT] despawning {party.StringId} failed: {ex.GetType().Name}: {ex.Message}"); }
         finally { _releasing = false; }
      }

      private void Forget(string id)
      {
         _ledger.Forget(id);
         _onDestroyed.Remove(id);
         _onRestore.Remove(id);
      }

      private void SweepMissing()
      {
         foreach (string id in _ledger.AllIds().Where(id => Find(id) == null).ToList()) Forget(id);
      }

      private static MobileParty Find(string id)
         => string.IsNullOrEmpty(id) ? null : MobileParty.All.FirstOrDefault(p => p.StringId == id && p.IsActive);

      #endregion
   }
}
