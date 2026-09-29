// Code written by Gabriel Mailhot, 28/09/2026.
// The balance-of-power survey, in the game (Gabriel, 28/09/2026: measure before building any push toward balance). A
// developer instrument, OFF until `cr.balance_survey on`: then every week it reads every realm, and it notes every war,
// peace, alliance and call to war, appending each as a line to Logs\CR_BalanceSurvey_<campaign>.csv. The file is the
// survey's only store, so a long accelerated run survives crashes and sessions, and `cr.balance_survey report` reads it
// back through BalanceSurveyPolicy. The only thing saved in the campaign is whether the survey is on.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic.WorldAlmanac;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>Records the balance of power week by week while switched on.</summary>
   public sealed class BalanceSurveyBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CR_BalanceSurveyOn_v1";

      private bool _on;

      public BalanceSurveyBehavior()
      {
         Instance = this;
      }

      public static BalanceSurveyBehavior Instance { get; private set; }

      /// <summary>Whether the survey is recording.</summary>
      public bool IsOn => _on;

      /// <summary>This campaign's survey file.</summary>
      public static string FilePath
         => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "Logs",
                         $"CR_BalanceSurvey_{Sanitize(Campaign.Current?.UniqueGameId ?? "campaign")}.csv");

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
      {
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, () => { if (_on) SafeRun.Step("BalanceSurvey.Weekly", RecordReading); });
         CampaignEvents.WarDeclared.AddNonSerializedListener(this, (a, b, _) => OnPair("war", a as Kingdom, b as Kingdom));
         CampaignEvents.MakePeace.AddNonSerializedListener(this, (a, b, _) => OnPair("peace", a as Kingdom, b as Kingdom));
         CampaignEvents.OnAllianceStartedEvent.AddNonSerializedListener(this, (a, b) => OnPair("alliance", a, b));
         CampaignEvents.OnAllianceEndedEvent.AddNonSerializedListener(this, (a, b) => OnPair("alliance_end", a, b));
         CampaignEvents.OnCallToWarAgreementStartedEvent.AddNonSerializedListener(this, (calling, called, against) => OnCallToWar(calling, called, against));
      }

      public override void SyncData(IDataStore dataStore)
      {
         try { dataStore.SyncData(SaveKey, ref _on); }
         catch (Exception ex) { ModLog.Error($"[SURVEY] save flag failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Switches the survey on (taking a first reading at once) or off.</summary>
      public void Switch(bool on)
      {
         _on = on;
         if (on) RecordReading();
      }

      /// <summary>Takes one reading now and appends it to the file, whether or not the survey is on.</summary>
      public void RecordReading()
      {
         List<SurveyRow> rows = Reading();
         Append(rows.Select(BalanceSurveyPolicy.ToLine));
         ModLog.Info($"[SURVEY] day {Today}: {rows.Count} realm(s) read into {FilePath}.");
      }

      /// <summary>This week's reading of every living realm, written nowhere (the cr.testall seam and the weekly tick share it).</summary>
      public static List<SurveyRow> Reading()
      {
         List<KingdomPowerFacts> field = KingdomPowerFactsBuilder.Build();
         Dictionary<string, double> index = PowerIndexPolicy.Compute(field).Ranked.ToDictionary(s => s.KingdomId, s => s.PowerIndex);
         int day = Today;

         return field.Select(f => new SurveyRow {
            Day = day,
            KingdomId = f.KingdomId,
            KingdomName = f.KingdomName,
            PowerIndex = index.TryGetValue(f.KingdomId, out double p) ? p : 0,
            Strength = f.FieldStrength + f.GarrisonStrength,
            Towns = f.Towns,
            Castles = f.Castles,
            Villages = f.Villages,
            Wars = f.ActiveWars,
            Allies = Kingdom.All?.FirstOrDefault(k => k.StringId == f.KingdomId)?.AlliedKingdoms?.Count ?? 0
         }).ToList();
      }

      /// <summary>Reads this campaign's survey file back and summarises it; null when there is no file yet.</summary>
      public static (BalanceSurveySummary Summary, int Lines)? Report()
      {
         if (!File.Exists(FilePath)) return null;

         string[] lines = File.ReadAllLines(FilePath);
         (List<SurveyRow> rows, List<SurveyEvent> events) = BalanceSurveyPolicy.Parse(lines);

         return (BalanceSurveyPolicy.Summarize(rows, events), lines.Length);
      }

      #region private

      private void OnPair(string kind, Kingdom a, Kingdom b)
      {
         if (!_on || a == null || b == null) return;

         SafeRun.Step("BalanceSurvey.Event", () =>
         {
            Kingdom leader = FiefLeader();
            bool against = kind == "alliance"
               ? leader != null && leader != a && leader != b && (a.IsAtWarWith(leader) || b.IsAtWarWith(leader))
               : leader != null && (a == leader || b == leader);

            Append(new[] {BalanceSurveyPolicy.ToLine(Event(kind, a, b, against))});
         });
      }

      private void OnCallToWar(Kingdom calling, Kingdom called, Kingdom against)
      {
         if (!_on || calling == null || called == null) return;

         SafeRun.Step("BalanceSurvey.CallToWar", () =>
         {
            Kingdom leader = FiefLeader();
            Append(new[] {BalanceSurveyPolicy.ToLine(Event("call_to_war", calling, called, leader != null && against == leader))});
         });
      }

      private static SurveyEvent Event(string kind, Kingdom a, Kingdom b, bool againstLeader)
         => new SurveyEvent {
            Day = Today, Kind = kind, AId = a.StringId, AName = a.Name?.ToString() ?? a.StringId,
            BId = b.StringId, BName = b.Name?.ToString() ?? b.StringId, AgainstLeader = againstLeader
         };

      /// <summary>The realm holding the most fiefs right now, a town counting twice a castle, as the survey reads shares.</summary>
      private static Kingdom FiefLeader()
         => Kingdom.All?.Where(k => k != null && !k.IsEliminated)
                   .OrderByDescending(k => k.Fiefs.Sum(t => t.IsTown ? 2 : 1))
                   .FirstOrDefault();

      private static void Append(IEnumerable<string> lines)
      {
         try
         {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bool fresh = !File.Exists(path);
            var all = new List<string>();
            if (fresh) all.Add(BalanceSurveyPolicy.Header);
            all.AddRange(lines);
            File.AppendAllLines(path, all);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SURVEY] could not write the survey file: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private static string Sanitize(string s)
      {
         foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');

         return s;
      }

      #endregion
   }
}
