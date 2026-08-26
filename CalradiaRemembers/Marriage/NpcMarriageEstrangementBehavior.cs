// Code written by Gabriel Mailhot, 26/08/2026.
// Stolen Love pillar, Increment 2 (engine): tracks each married NPC whose marriage is fraying because of an
// ongoing affair with the player, and paces that rift toward a divorce. Player-experience-first: it never
// simulates NPC-NPC affairs across Calradia, it only watches the NPCs the PLAYER has actually seduced (their
// stored profile carries RomanticStatus.SecretLover, set by the SDK on a married-NPC intimacy). Mirrors
// DivorceProcessBehavior's shape (daily tick, additive byte[] save key, degrade-to-empty load). The actual
// divorce and the jilted spouse's grudge are Increment 3, which reads the ReadyToDivorce phase this exposes.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>
   ///   Watches the player's married affair partners and advances a per-NPC estrangement (see
   ///   <see cref="EstrangementProgressPolicy" />). Begins one when a seducible, third-party-married NPC becomes
   ///   the player's secret lover; prunes it when the affair ends, the NPC dies, or the marriage is already gone.
   ///   Exposes the current phase so the prompt can voice a fraying marriage and Increment 3 can act on a spouse
   ///   ready to leave. Gated by the MCM EnableStolenLove toggle.
   /// </summary>
   public sealed class NpcMarriageEstrangementBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_StolenLoveEstrangement_v1bytes";
      public static NpcMarriageEstrangementBehavior Instance;

      private List<StolenLoveEstrangement> _active = new();

      public NpcMarriageEstrangementBehavior() => Instance = this;

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
         => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

      /// <summary>The estrangement phase for this NPC, or <see cref="EstrangementPhase.None" /> when none is tracked for them.</summary>
      public EstrangementPhase PhaseFor(string npcId)
      {
         StolenLoveEstrangement e = Find(npcId);

         return e == null
            ? EstrangementPhase.None
            : EstrangementProgressPolicy.Phase(Today - e.StartDay, e.Difficulty);
      }

      /// <summary>A 0..100 sense of how far this NPC's marriage has come apart, for the fraying note. 0 when none is tracked.</summary>
      public int ProgressFor(string npcId)
      {
         StolenLoveEstrangement e = Find(npcId);

         return e == null ? 0 : EstrangementProgressPolicy.ProgressPercent(Today - e.StartDay, e.Difficulty);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try
            {
               // Serialize a frozen copy so an autosave racing the daily tick cannot throw mid-serialize.
               var snapshot = new List<StolenLoveEstrangement>(_active);
               bytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
            }
            catch (Exception ex)
            {
               ModLog.Error($"[STOLEN_LOVE] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = System.Text.Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[STOLEN_LOVE] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves, additive-key save-safety */
            }

            if (bytes is {Length: > 0})
               try { _active = JsonConvert.DeserializeObject<List<StolenLoveEstrangement>>(System.Text.Encoding.UTF8.GetString(bytes)) ?? new List<StolenLoveEstrangement>(); }
               catch (Exception ex)
               {
                  _active = new List<StolenLoveEstrangement>();
                  ModLog.Error($"[STOLEN_LOVE] load failed, estrangements dropped for this load so the campaign still opens. {ex.GetType().Name}: {ex.Message}");
               }
         }
      }

      #region private

      private StolenLoveEstrangement Find(string npcId)
         => string.IsNullOrEmpty(npcId) ? null : _active.FirstOrDefault(e => e.NpcId == npcId);

      private void OnDailyTick()
      {
         if (!ModSettings.EnableStolenLove) return; // dormant when the pillar is off; state is kept, not torn down

         var behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.Store == null) return;

         SafeRun.Step("NpcMarriageEstrangementBehavior.BeginNewAffairs", () => BeginNewAffairs(behavior, Today));
         SafeRun.Step("NpcMarriageEstrangementBehavior.PruneEnded", () => PruneEnded(behavior));
      }

      /// <summary>Begins an estrangement for any seducible, third-party-married NPC who has become the player's secret lover and is not yet tracked.</summary>
      private void BeginNewAffairs(NpcMemoryCampaignBehavior behavior, int today)
      {
         foreach (string id in behavior.Store.ListIds())
         {
            if (_active.Any(e => e.NpcId == id)) continue;

            NpcProfile profile = behavior.Store.Get(id);
            if (profile?.Romantic?.Status != RomanticStatus.SecretLover) continue;

            Hero npc = Hero.FindFirst(h => h != null && h.StringId == id);
            if (npc?.Spouse is not {IsAlive: true}) continue;
            if (npc.Spouse == Hero.MainHero) continue;

            SeductionVerdict verdict = SeductionEligibilityPolicy.Evaluate(SeductionFactsMapper.For(npc, profile));
            if (!verdict.Seducible) continue;

            _active.Add(new StolenLoveEstrangement {
               NpcId = id,
               SpouseId = npc.Spouse.StringId,
               StartDay = today,
               Difficulty = verdict.Difficulty
            });
            ModLog.Info($"[STOLEN_LOVE] {npc.Name}'s marriage to {npc.Spouse.Name} begins to fray (difficulty {verdict.Difficulty}).");
         }
      }

      /// <summary>Drops any estrangement whose NPC is gone, whose marriage already ended or changed, or whose affair with the player is over.</summary>
      private void PruneEnded(NpcMemoryCampaignBehavior behavior)
      {
         _active.RemoveAll(e =>
         {
            Hero npc = Hero.FindFirst(h => h != null && h.StringId == e.NpcId);
            if (npc == null || !npc.IsAlive) return true;
            if (npc.Spouse == null || npc.Spouse.StringId != e.SpouseId) return true; // divorced, widowed, or spouse changed

            NpcProfile profile = behavior.Store.Get(e.NpcId);

            return profile?.Romantic?.Status != RomanticStatus.SecretLover; // the affair is over
         });
      }

      #endregion
   }
}
