// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar increment 1 (persistence layer): owns the persisted roster of NemesisIdentity records
// (the persistent named bandit bosses), save-safe like GrudgeBehavior. A corrupt blob degrades to an empty
// roster rather than blocking the load; the SyncData key is additive so old saves load as an empty roster.
// The pure decisions (which recurring nemesis to reuse) live in NemesisRosterPolicy; this class only stores
// and queries. The bandit-capture hook that mints/attaches a Hero as captor is a later increment-1 piece.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Nemesis
{
   /// <summary>
   ///   The save-persisted store of nemesis identities. Additive and save-safe: absent key or corrupt blob
   ///   yields an empty roster, never a broken load. Query via <see cref="FindRecurring" /> (delegates to the
   ///   pure <see cref="NemesisRosterPolicy" />); mint and add via <see cref="Add" />.
   /// </summary>
   public sealed class NemesisBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_Nemesis_v1bytes";

      public static NemesisBehavior Instance;

      private NemesisRoster _roster = new NemesisRoster();

      public NemesisBehavior()
      {
         Instance = this;
      }

      /// <summary>Every nemesis on record, living and dead.</summary>
      public IReadOnlyList<NemesisIdentity> All => _roster.Nemeses;

      public override void RegisterEvents()
      {
         // The bandit-capture hook (mint/reuse + attach the Hero as captor) lands in a later increment-1 piece.
      }

      /// <summary>The living nemesis of this band culture anchored to this region, or null (mint a new one).</summary>
      public NemesisIdentity FindRecurring(string bandCultureKey, string regionId)
         => NemesisRosterPolicy.FindRecurring(_roster.Nemeses, bandCultureKey, regionId);

      /// <summary>Adds a freshly-minted nemesis identity to the roster.</summary>
      public void Add(NemesisIdentity identity)
      {
         if (identity != null && !string.IsNullOrEmpty(identity.Id)) _roster.Nemeses.Add(identity);
      }

      /// <summary>The nemesis embodied by this Hero StringId, or null.</summary>
      public NemesisIdentity GetByHero(string heroStringId)
         => string.IsNullOrEmpty(heroStringId) ? null : _roster.Nemeses.FirstOrDefault(n => n.HeroStringId == heroStringId);

      /// <summary>
      ///   The regional recurring roster's core: reuse the LIVING nemesis of this band culture in this region
      ///   if one exists, else MINT a new latent identity (name/epithet via <see cref="NemesisNameGenerator" />,
      ///   no Hero yet — the bandit-capture hook attaches a real Hero to it). The seed varies by roster size so
      ///   fresh bosses do not all share a name.
      /// </summary>
      public NemesisIdentity FindOrMint(string bandCultureKey, string regionId, int today)
      {
         NemesisIdentity existing = FindRecurring(bandCultureKey, regionId);
         if (existing != null) return existing;

         NemesisNameResult name = NemesisNameGenerator.Generate(bandCultureKey, _roster.Nemeses.Count);
         var minted = new NemesisIdentity {
            Id = "nemesis_" + Guid.NewGuid().ToString("N"),
            Name = name.Name,
            Epithet = name.Epithet,
            BandCultureKey = bandCultureKey,
            RegionId = regionId,
            IsAlive = true,
            MintedDay = today
         };

         Add(minted);
         ModLog.Info($"[NEMESIS] minted {minted.Name} {minted.Epithet} ({bandCultureKey}) in region {regionId}.");

         return minted;
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               // Serialize a frozen copy, not the live list, so an autosave racing a main-thread mutation
               // never throws mid-serialize and drops the whole blob (the GrudgeBehavior/SchemeStore pattern).
               var snapshot = new NemesisRoster {
                  Nemeses = new List<NemesisIdentity>(_roster.Nemeses),
                  MigrationVersion = _roster.MigrationVersion
               };
               bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[NEMESIS] serialize failed, skipping this save: {ex.GetType().Name}: {ex.Message}");

               return;
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[NEMESIS] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch { bytes = null; }

            if (bytes is {Length: > 0})
               try { _roster = JsonConvert.DeserializeObject<NemesisRoster>(Encoding.UTF8.GetString(bytes)) ?? new NemesisRoster(); }
               catch (Exception ex)
               {
                  _roster = new NemesisRoster();
                  ModLog.Error($"[NEMESIS] load failed — roster reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }
            else
               _roster = new NemesisRoster();

            ModLog.Info($"[NEMESIS] loaded {_roster.Nemeses.Count} nemesis identity(ies).");
         }
      }
   }
}
