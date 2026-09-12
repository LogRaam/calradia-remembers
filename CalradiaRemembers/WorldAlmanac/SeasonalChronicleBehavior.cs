// Code written by Gabriel Mailhot, 12/09/2026.
// The seasonal historian, in the game. See SeasonalTurnPolicy for the reasoning and for Gabriel's three
// rulings; this file is the part that has to touch the campaign, and it is deliberately thin.
//
// Ruling 3 shaped it most: it must NOT become a fourth memory store. So the only thing persisted is ONE
// snapshot per realm - what the world looked like at the last turn of the season - and the history itself is
// WorldEvents, which already age, already have an awareness model, and already carry a confidence rating.
// A character then receives them through realm_news, which decides how much of it their standing commands.
//
// That is also ruling 2 satisfied without a line of its own: one historian for the world, and every character
// carries a VIEW of it, because the view is the machinery that already existed.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic.WorldAlmanac;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>
   ///   Takes a reading of every realm at each turn of the season, compares it with the last, and writes what
   ///   changed into the world as ordinary news.
   /// </summary>
   public sealed class SeasonalChronicleBehavior : CampaignBehaviorBase
   {
      /// <summary>How far a season's turn travels. Lives in the policy, beside the reasoning for it.</summary>
      public const int Magnitude = SeasonalTurnPolicy.Magnitude;

      /// <summary>Tags every row this feature writes, so the chronicle can be found and counted as a group.</summary>
      public const string Source = "CalradiaRemembers.SeasonalChronicle";

      [SaveableField(1)] private Dictionary<string, string> _lastSeen = new();

      [SaveableField(2)] private int _lastSeason = -1;

      public static SeasonalChronicleBehavior Instance { get; private set; }

      public override void RegisterEvents()
      {
         Instance = this;
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
         dataStore.SyncData("cr_chronicle_lastSeen", ref _lastSeen);
         dataStore.SyncData("cr_chronicle_lastSeason", ref _lastSeason);

         _lastSeen ??= new Dictionary<string, string>();
      }

      /// <summary>
      ///   Runs the turn immediately, whatever the calendar says. The console's way in, and the only way to
      ///   see a season's worth of history without waiting a season for it.
      /// </summary>
      public int DebugRunTurn() => RecordTurn(SeasonName(CampaignTime.Now.GetSeasonOfYear));

      /// <summary>
      ///   Records ONE entry against a deliberately perturbed reading of a real realm, and returns it. The
      ///   only way to prove the whole road end to end without waiting a season and hoping a kingdom happened
      ///   to gain a town: the caller removes the row afterwards.
      /// </summary>
      public WorldEvent DebugRecordSyntheticTurn()
      {
         foreach (KingdomPowerFacts facts in KingdomPowerFactsBuilder.Build())
         {
            if (string.IsNullOrEmpty(facts?.KingdomId)) continue;

            KingdomSeasonSnapshot now = SnapshotOf(facts);
            var poorer = new KingdomSeasonSnapshot {
               KingdomId = now.KingdomId, KingdomName = now.KingdomName,
               Holdings = now.Holdings - 1, Wealth = now.Wealth, FieldStrength = now.FieldStrength,
               Prosperity = now.Prosperity
            };

            string line = SeasonalTurnPolicy.Describe(poorer, now, "Autumn");
            if (line == null) continue;

            Record(facts, line);

            return WorldEventStore.Instance?.MostRecentMatching(
               WorldEventType.SeasonalTurn, e => e?.Summary == line);
         }

         return null;
      }

      #region private

      /// <summary>
      ///   Once per season, not once per day. The clock is checked daily because Bannerlord has no season
      ///   event of its own, which is cheap: an integer compare and nothing else on the days it does not fire.
      /// </summary>
      private void OnDailyTick()
      {
         try
         {
            var season = (int) CampaignTime.Now.GetSeasonOfYear;

            if (season == _lastSeason) return;

            // The FIRST tick of a save has nothing to compare against. Take the reading, say nothing, and let
            // the next turn of the season be the first entry - announcing a realm into existence would be a
            // fact nobody could have observed.
            bool firstEver = _lastSeason < 0;
            int written = RecordTurn(firstEver ? null : SeasonName((CampaignTime.Seasons) _lastSeason));

            _lastSeason = season;

            if (!firstEver) ModLog.Info($"[CHRONICLE] season turned: {written} realm(s) worth recording.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CHRONICLE] seasonal turn failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Reads every realm, records what moved since the last reading, and keeps the new reading. A null
      ///   season means "take the reading only" - the first turn of a save.
      /// </summary>
      private int RecordTurn(string seasonEnded)
      {
         var recorded = 0;

         foreach (KingdomPowerFacts facts in KingdomPowerFactsBuilder.Build())
         {
            if (string.IsNullOrEmpty(facts?.KingdomId)) continue;

            KingdomSeasonSnapshot now = SnapshotOf(facts);
            KingdomSeasonSnapshot before = Read(facts.KingdomId);

            if (seasonEnded != null && before != null)
            {
               string line = SeasonalTurnPolicy.Describe(before, now, seasonEnded);

               if (line != null)
               {
                  Record(facts, line);
                  recorded++;
               }
            }

            _lastSeen[facts.KingdomId] = Write(now);
         }

         return recorded;
      }

      private static void Record(KingdomPowerFacts facts, string line)
      {
         if (WorldEventStore.Instance == null) return;

         WorldEventStore.Instance.Record(new WorldEvent {
            Id = Guid.NewGuid().ToString("N"),
            Type = WorldEventType.SeasonalTurn,
            Day = (int) CampaignTime.Now.ToDays,
            Summary = line,
            FactionIds = new List<string> {facts.KingdomId},

            // Never the player's chronicle: this is the world's own account of itself, and the player's deeds
            // have their own road into it.
            InvolvesPlayer = false,
            IsPersistentDeed = false,
            Magnitude = Magnitude,
            SourceMod = Source
         });

         ModLog.Info($"[CHRONICLE] {line}");
      }

      private static KingdomSeasonSnapshot SnapshotOf(KingdomPowerFacts f)
         => new() {
            KingdomId = f.KingdomId,
            KingdomName = f.KingdomName,

            // Fortified holdings only: a village changing hands is not history.
            Holdings = f.Towns + f.Castles,
            Wealth = f.EstimatedWealth,
            FieldStrength = f.FieldStrength,
            Prosperity = f.TotalProsperity
         };

      /// <summary>
      ///   Six values as one delimited string. Bannerlord's save system takes a dictionary of strings without
      ///   argument; a dictionary of custom objects wants a SaveableTypeDefiner, and one snapshot per realm is
      ///   not worth a type registration that a future rename could break silently in old saves.
      /// </summary>
      private static string Write(KingdomSeasonSnapshot s)
         => string.Join("|", s.KingdomName?.Replace("|", " ") ?? "", s.Holdings.ToString(),
                        s.Wealth.ToString(), s.FieldStrength.ToString("F1"), s.Prosperity.ToString("F1"));

      private KingdomSeasonSnapshot Read(string kingdomId)
      {
         if (_lastSeen == null || !_lastSeen.TryGetValue(kingdomId, out string raw) || raw == null) return null;

         string[] parts = raw.Split('|');
         if (parts.Length < 5) return null;

         try
         {
            return new KingdomSeasonSnapshot {
               KingdomId = kingdomId,
               KingdomName = parts[0],
               Holdings = int.Parse(parts[1]),
               Wealth = long.Parse(parts[2]),
               FieldStrength = double.Parse(parts[3]),
               Prosperity = double.Parse(parts[4])
            };
         }
         catch
         {
            // A snapshot we cannot read is a season we cannot compare, which costs one entry. Silently
            // dropping it is right: the next turn takes a fresh reading and the chronicle resumes.
            return null;
         }
      }

      private static string SeasonName(CampaignTime.Seasons season)
      {
         switch (season)
         {
            case CampaignTime.Seasons.Spring: return "Spring";
            case CampaignTime.Seasons.Summer: return "Summer";
            case CampaignTime.Seasons.Autumn: return "Autumn";
            case CampaignTime.Seasons.Winter: return "Winter";
            default: return null;
         }
      }

      #endregion
   }
}
