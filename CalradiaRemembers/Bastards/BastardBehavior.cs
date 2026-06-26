// Code written by Gabriel Mailhot, 26/06/2026.
// Bastards consequence layer — the hidden-birth thread. The only state we keep is which children are the
// player's bastards and whether each has been discovered (a one-way fact once it is out); their age is read
// live. Each SEASON we roll an age-keyed discovery chance per concealed bastard; on a hit the scandal is
// recorded into the world-event store (it spreads as rumour + feeds the chronicle) and dents the player's
// standing. Coming-of-age claims + blackmail are later threads. See the ROADMAP "Bastards" pillar.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Bastards
{
   /// <summary>
   ///   Tracks the player's bastards and, once a season, may bring a hidden one to light — a scandal recorded
   ///   into the world-event store + a hit to the player's standing. Minimal persisted state: child id →
   ///   discovered (one-way). Age (and thus the seasonal discovery odds) is read live from the hero.
   /// </summary>
   public sealed class BastardBehavior : CampaignBehaviorBase
   {
      // Tuning (flagged for in-game ratification).
      private const int ScandalMagnitude = 40;     // how far the gossip carries (0..100)
      private const int ScandalRenownLoss = 5;     // the dishonour to the player's name
      private const int ScandalInfluenceLoss = 10; // the political cost at court

      private const string SaveKey = "CalradiaRemembers_Bastards_v1bytes";

      public static BastardBehavior Instance;

      // childId -> discovered. Discovered is one-way (a known fact); age is read live from the hero.
      private Dictionary<string, bool> _bastards = new();
      private int _lastEvaluatedSeason = -1;

      public BastardBehavior() => Instance = this;

      /// <summary>The tracked bastards (child id → discovered) — for the cr.bastards diagnostic.</summary>
      public IReadOnlyDictionary<string, bool> Bastards => _bastards;

      public bool IsTracked(string childId) => childId != null && _bastards.ContainsKey(childId);

      public bool IsDiscovered(string childId)
         => childId != null && _bastards.TryGetValue(childId, out bool discovered) && discovered;

      public override void RegisterEvents()
      {
         CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_bastards, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[BASTARD] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[BASTARD] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves */
            }

            if (bytes is {Length: > 0})
               try { _bastards = JsonConvert.DeserializeObject<Dictionary<string, bool>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, bool>(); }
               catch (Exception ex)
               {
                  _bastards = new Dictionary<string, bool>();
                  ModLog.Error($"[BASTARD] load failed — dropped for this load. {ex.GetType().Name}: {ex.Message}");
               }
         }
      }

      // ── Birth: tag the player's non-marital children ──────────────────────

      private void OnGivenBirth(Hero mother, List<Hero> aliveChildren, int stillbornCount)
      {
         if (aliveChildren == null || Hero.MainHero == null) return;

         foreach (Hero child in aliveChildren)
         {
            if (child == null || !IsPlayersBastard(child) || _bastards.ContainsKey(child.StringId)) continue;

            _bastards[child.StringId] = false; // concealed
            ModLog.Info($"[BASTARD] a hidden child is born: {child.Name} (parents {child.Mother?.Name}/{child.Father?.Name}).");
         }
      }

      /// <summary>A player-parented child whose OTHER parent is not the player's spouse — born outside the marriage.</summary>
      private static bool IsPlayersBastard(Hero child)
      {
         Hero player = Hero.MainHero;
         bool playerIsParent = child.Father == player || child.Mother == player;
         if (!playerIsParent) return false;

         Hero otherParent = child.Father == player
            ? child.Mother
            : child.Father;

         return otherParent != null && otherParent != player.Spouse;
      }

      // ── Seasonal discovery roll ───────────────────────────────────────────

      private void OnWeeklyTick()
      {
         int season = (int) CampaignTime.Now.ToSeasons;
         if (season <= _lastEvaluatedSeason) return;

         _lastEvaluatedSeason = season;
         EvaluateSeason();
      }

      private void EvaluateSeason()
      {
         if (_bastards.Count == 0) return;

         // Snapshot the concealed ids first — Discover mutates the dictionary.
         List<string> concealed = _bastards.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();

         foreach (string id in concealed)
         {
            Hero child = FindHero(id);
            if (child is not {IsAlive: true}) continue;

            int chance = BastardDiscoveryPolicy.DiscoveryChancePercent((int) child.Age);
            if (MBRandom.RandomInt(100) < chance) Discover(child);
         }
      }

      /// <summary>
      ///   Brings a hidden bastard to light: marks it a known fact, records the scandal into the world-event
      ///   store (rumour + chronicle), and dents the player's standing. Returns false for an untracked hero.
      /// </summary>
      public bool Discover(Hero child)
      {
         if (child == null || !_bastards.ContainsKey(child.StringId)) return false;

         _bastards[child.StringId] = true;
         RecordScandal(child);

         if (Hero.MainHero?.Clan != null)
         {
            GainRenownAction.Apply(Hero.MainHero, -ScandalRenownLoss, true);
            ChangeClanInfluenceAction.Apply(Hero.MainHero.Clan, -ScandalInfluenceLoss);
         }

         ModLog.Info($"[BASTARD] discovered: {child.Name} — scandal recorded, renown-{ScandalRenownLoss} influence-{ScandalInfluenceLoss}.");

         return true;
      }

      private static void RecordScandal(Hero child)
      {
         WorldEventStore store = WorldEventStore.Instance;
         if (store == null) return;

         string motherName = child.Mother?.Name?.ToString() ?? "an unknown woman";
         string fatherName = child.Father?.Name?.ToString() ?? "an unknown man";

         var actors = new List<string> {child.StringId};
         if (child.Mother != null) actors.Add(child.Mother.StringId);
         if (child.Father != null) actors.Add(child.Father.StringId);

         var factions = new List<string>();
         if (Hero.MainHero?.MapFaction != null) factions.Add(Hero.MainHero.MapFaction.StringId);
         if (child.Mother?.MapFaction != null) factions.Add(child.Mother.MapFaction.StringId);

         store.Record(new WorldEvent {
            Type = WorldEventType.Scandal,
            Day = (int) CampaignTime.Now.ToDays,
            Magnitude = ScandalMagnitude,
            InvolvesPlayer = true,
            ActorIds = actors,
            FactionIds = factions,
            Summary = $"Word spreads that {child.Name} is the natural child of {fatherName} and {motherName}.",
            ChronicleLine = $"a whispered scandal: {child.Name}, born of {motherName} out of wedlock, was named {fatherName}'s own"
         });
      }

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         if (victim != null) _bastards.Remove(victim.StringId);
      }

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id)
            ? null
            : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

      // ── Debug seams (console tests / diagnostics) ─────────────────────────

      /// <summary>Force-tag a hero as a concealed player bastard (console testing, no real birth needed).</summary>
      public void DebugTrack(Hero child)
      {
         if (child != null && !_bastards.ContainsKey(child.StringId)) _bastards[child.StringId] = false;
      }

      /// <summary>Run the seasonal discovery evaluation immediately (console testing).</summary>
      public void DebugEvaluateSeason() => EvaluateSeason();
   }
}
