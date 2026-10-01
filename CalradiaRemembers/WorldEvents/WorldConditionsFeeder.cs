// Code written by Gabriel Mailhot, 08/08/2026.
// A dedicated WEEKLY feeder that scans the campaign for standing WORLD CONDITIONS with no vanilla event of their
// own (war looming, a town in hardship) and records each as a WorldEvent, so it enters the SAME rumour / "what
// you've heard" / chronicle pipeline as every other happening (never a side-channel). Designed so adding a NEW
// condition = writing one scanner method and adding it to the _scanners list. All engine reads are guarded: a
// world-condition scan must never crash the weekly tick. Dedup is save-safe: it queries the persisted
// WorldEventStore for a same-subject event, not a private cooldown dict, so it survives save/reload for free.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>
   ///   Weekly scanner for standing world conditions. Owned by <see cref="Behaviors.NpcMemoryCampaignBehavior" />
   ///   and wired on session launch (mirrors <see cref="WorldEventFeeders" />). Ships two conditions (war looming
   ///   between two kingdoms, and a town in economic hardship); each DECISION lives in a pure, tested policy
   ///   (<see cref="WarLoomingPolicy" /> / <see cref="HardshipPolicy" />) and this feeder only reads engine facts
   ///   and calls it.
   /// </summary>
   public sealed class WorldConditionsFeeder
   {
      private const int MaxKingdomPairScan = 40; // bound the O(n^2) pairwise diplomacy scan (few kingdoms, capped defensively)
      private const int MaxTownScan = 300; // bound the town sweep in long/heavily-modded campaigns

      private const int WarLoomingMagnitude = 60; // realm-level tension: news travels far (below WarDeclared's 75)
      private const int HardshipMagnitude = 30; // a town's plight: moderate reach

      // A food shortage read: stocks running low AND still falling. Kept here (an interpretation of engine reads),
      // not in the pure policy, which only receives the resulting bool.

      private readonly List<Func<ConditionScanResult>> _scanners;

      public WorldConditionsFeeder()
      {
         // Adding a NEW world condition = write one scanner method and add it here.
         _scanners = new List<Func<ConditionScanResult>> {
            ScanWarLooming,
            ScanHardship
         };
      }

      private static WorldEventStore Store => WorldEventStore.Instance;
      private static int Today => (int) CampaignTime.Now.ToDays;

      public void Subscribe()
      {
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
         ModLog.Info("WorldConditionsFeeder subscribed.");
      }

      /// <summary>
      ///   Runs every condition scanner once and returns a per-condition tally. Shared by the weekly tick and the
      ///   cr.worldcond console command. Each scanner runs behind its own guard: one throwing must never starve the
      ///   others sharing this pass.
      /// </summary>
      public IReadOnlyList<ConditionScanResult> RunScan()
      {
         var results = new List<ConditionScanResult>();

         foreach (Func<ConditionScanResult> scanner in _scanners)
         {
            var captured = default(ConditionScanResult);
            SafeRun.Step("WorldConditionsFeeder.RunScan", () => captured = scanner());
            results.Add(captured);
         }

         return results;
      }

      #region private

      private void OnWeeklyTick() => RunScan();

      // ── Condition #1: war looming between two kingdoms ─────────────────────

      private ConditionScanResult ScanWarLooming()
      {
         if (Store == null) return new ConditionScanResult("war-looming", 0);

         List<Kingdom> kingdoms = Kingdom.All?.Where(k => k is {IsEliminated: false}).Take(MaxKingdomPairScan).ToList();
         if (kingdoms == null || kingdoms.Count < 2) return new ConditionScanResult("war-looming", 0);

         DiplomacyModel diplomacy = Campaign.Current?.Models?.DiplomacyModel;
         if (diplomacy == null) return new ConditionScanResult("war-looming", 0);

         int today = Today;
         var recorded = 0;

         for (var i = 0; i < kingdoms.Count; i++)
            for (int j = i + 1; j < kingdoms.Count; j++)
            {
               Kingdom a = kingdoms[i],
                       b = kingdoms[j];
               // One pair throwing (a diplomacy read, a store call) must not stop the rest of the scan.
               SafeRun.Step($"WorldConditionsFeeder.WarLooming({a?.Name}, {b?.Name})", () =>
               {
                  if (TryRecordWarLooming(diplomacy, a, b, today)) recorded++;
               });
            }

         return new ConditionScanResult("war-looming", recorded);
      }

      private static bool TryRecordWarLooming(DiplomacyModel diplomacy, Kingdom a, Kingdom b, int today)
      {
         if (a == null || b == null || Store == null) return false;

         bool alreadyAtWar = a.IsAtWarWith(b);

         StanceLink stance = a.GetStanceWith(b);
         int peaceDays = stance != null
            ? Math.Max(0, today - (int) stance.PeaceDeclarationDate.ToDays)
            : int.MaxValue; // never had a stance record: treat peace as long-standing

         // Tension in EITHER direction means war looms for the pair (each side is evaluated by its own ruling clan).
         float scoreAB = diplomacy.GetScoreOfDeclaringWar(a, b, a.RulingClan, out TextObject _);
         float scoreBA = diplomacy.GetScoreOfDeclaringWar(b, a, b.RulingClan, out TextObject _);
         double warScore = Math.Max(scoreAB, scoreBA);

         WorldEvent last = Store.MostRecentMatching(WorldEventType.WarLooming,
            e => e.FactionIds != null && e.FactionIds.Contains(a.StringId) && e.FactionIds.Contains(b.StringId));
         int? daysSinceLast = last == null
            ? (int?) null
            : today - last.Day;

         if (!WarLoomingPolicy.ShouldRecord(warScore, alreadyAtWar, peaceDays, daysSinceLast)) return false;

         Store.Record(new WorldEvent {
            Type = WorldEventType.WarLooming,
            Day = today,
            Magnitude = WarLoomingMagnitude,
            InvolvesPlayer = false,
            Summary = $"Tension mounts between {a.Name} and {b.Name}; many expect war before long.",
            FactionIds = new List<string> {a.StringId, b.StringId}
         });

         return true;
      }

      // ── Condition #2: a town in economic hardship ─────────────────────────

      private ConditionScanResult ScanHardship()
      {
         if (Store == null) return new ConditionScanResult("hardship", 0);

         int today = Today;
         var recorded = 0;
         var scanned = 0;

         foreach (Settlement s in Settlement.All ?? Enumerable.Empty<Settlement>())
         {
            if (scanned >= MaxTownScan) break;
            if (s is not {IsTown: true}) continue;
            scanned++;

            Settlement settlement = s;
            SafeRun.Step($"WorldConditionsFeeder.Hardship({settlement?.Name})", () =>
            {
               if (TryRecordHardship(settlement, today)) recorded++;
            });
         }

         return new ConditionScanResult("hardship", recorded);
      }

      private static bool TryRecordHardship(Settlement settlement, int today)
      {
         if (settlement?.Town == null || Store == null) return false;

         // Only towns held by a real kingdom clan are worth surfacing; skip bandit / rebel / no-kingdom holdings.
         Kingdom kingdom = settlement.OwnerClan?.Kingdom;
         if (kingdom == null || kingdom.IsEliminated) return false;

         Town town = settlement.Town;
         // One reading of "hungry" in the mod (the engine-facts map found it written twice, 01/10/2026): the same rule decides
         // what a hungry town remembers of the player's trade. It also counts a town the game itself calls starving.
         bool foodShortage = TownFoodPolicy.IsHungry(FoodTradeMemoryBehavior.FactsOf(settlement));
         var prosperity = (int) town.Prosperity;

         WorldEvent last = Store.MostRecentMatching(WorldEventType.Hardship, e => e.PlaceId == settlement.StringId);
         int? daysSinceLast = last == null
            ? (int?) null
            : today - last.Day;

         if (!HardshipPolicy.ShouldRecord(foodShortage, prosperity, HardshipPolicy.ProsperityFloor, daysSinceLast)) return false;

         // Honest phrasing: name the actual distress the player could verify in the town screen.
         string summary = foodShortage
            ? $"Rations run short in {settlement.Name}; the common folk grumble."
            : $"Hard times have come to {settlement.Name}; trade dwindles and the common folk grumble.";

         Store.Record(new WorldEvent {
            Type = WorldEventType.Hardship,
            Day = today,
            PlaceId = settlement.StringId,
            Magnitude = HardshipMagnitude,
            InvolvesPlayer = false,
            Summary = summary,
            FactionIds = kingdom.StringId != null
               ? new List<string> {kingdom.StringId}
               : new List<string>()
         });

         return true;
      }

      #endregion

      /// <summary>The per-condition tally of one weekly pass: a short label and how many events it recorded.</summary>
      public readonly struct ConditionScanResult
      {
         public string Label { get; }
         public int Recorded { get; }

         public ConditionScanResult(string label, int recorded)
         {
            Label = label;
            Recorded = recorded;
         }
      }
   }
}
