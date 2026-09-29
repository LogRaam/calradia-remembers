// Code written by Gabriel Mailhot, 28/09/2026.
// The balance-of-power survey: a MEASUREMENT, not a feature. Gabriel (28/09/2026) asked whether Calradia needs a push
// toward balance (small realms allying against an empire, empires competing). The game 1.4.8 already forms alliances
// against a "threatening neighbour", but its war score still draws the strong toward the weak, and its alliances are
// capped (neighbours only, two at most, 84 days). Before any lever is built, this measures whether one realm actually
// runs away with the map: a weekly reading of every realm, and every war, peace and alliance, written to a file that
// survives the session, and summarised here.
//
// Share is measured on FIEFS (a town counts twice a castle), not troops: armies swing week to week with every battle,
// fiefs are what a realm holds, and a realm that swallows the map swallows fiefs.

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.WorldAlmanac
{
   /// <summary>One realm, one week.</summary>
   public sealed class SurveyRow
   {
      public int Day { get; set; }
      public string KingdomId { get; set; } = "";
      public string KingdomName { get; set; } = "";

      /// <summary>The CR Power Index (0..100, relative to the field) that week.</summary>
      public double PowerIndex { get; set; }

      /// <summary>Field plus garrison strength, the engine's estimate.</summary>
      public double Strength { get; set; }

      public int Towns { get; set; }
      public int Castles { get; set; }
      public int Villages { get; set; }

      /// <summary>Realms it is at war with.</summary>
      public int Wars { get; set; }

      /// <summary>Realms it is allied with.</summary>
      public int Allies { get; set; }
   }

   /// <summary>A diplomatic event: war, peace, alliance, alliance ended, call to war.</summary>
   public sealed class SurveyEvent
   {
      public int Day { get; set; }

      /// <summary>war, peace, alliance, alliance_end, call_to_war.</summary>
      public string Kind { get; set; } = "";

      public string AId { get; set; } = "";
      public string AName { get; set; } = "";
      public string BId { get; set; } = "";
      public string BName { get; set; } = "";

      /// <summary>
      ///   The event turned against the realm holding the most fiefs at that moment: a war declared on it, a peace
      ///   made with it, an alliance whose members were at war with it, a call to war against it.
      /// </summary>
      public bool AgainstLeader { get; set; }
   }

   /// <summary>
   ///   One reading of graphics memory, taken with each week's reading (29/09/2026: the overnight survey ended when the
   ///   engine ran out of graphics memory, and nothing said who had been filling it). -1 where the probe could not read.
   /// </summary>
   public sealed class SurveyVram
   {
      public int Day { get; set; }

      /// <summary>The game's own dedicated graphics memory, in MB.</summary>
      public long GameDedicatedMb { get; set; } = -1;

      /// <summary>The game's shared (system RAM used as graphics) memory, in MB.</summary>
      public long GameSharedMb { get; set; } = -1;

      /// <summary>The whole machine's dedicated graphics memory in use, in MB (other programs included).</summary>
      public long SystemDedicatedMb { get; set; } = -1;

      /// <summary>How many textures CR had loaded from files since the game started.</summary>
      public int CrTextures { get; set; }
   }

   /// <summary>What the memory readings show across a survey.</summary>
   public sealed class VramSummary
   {
      public int Readings { get; set; }
      public long GameAtStartMb { get; set; }
      public long GameAtEndMb { get; set; }
      public long GameHighestMb { get; set; }
      public long SystemHighestMb { get; set; }
      public int CrTexturesAtStart { get; set; }
      public int CrTexturesAtEnd { get; set; }

      /// <summary>The game's growth between readings where CR loaded no texture: what the game (or the machine) did alone.</summary>
      public long GrowthWhileCrLoadedNothingMb { get; set; }

      /// <summary>The game's growth between readings where CR did load textures.</summary>
      public long GrowthWhileCrLoadedMb { get; set; }

      /// <summary>The game's average growth per game day, first reading to last.</summary>
      public double MbPerGameDay { get; set; }
   }

   /// <summary>What a survey shows, from its first week to its last.</summary>
   public sealed class BalanceSurveySummary
   {
      public int Weeks { get; set; }
      public int FirstDay { get; set; }
      public int LastDay { get; set; }
      public int RealmsAtStart { get; set; }
      public int RealmsAtEnd { get; set; }
      public string LeaderAtStart { get; set; } = "";
      public string LeaderAtEnd { get; set; } = "";
      public double LeaderShareAtStart { get; set; }
      public double LeaderShareAtEnd { get; set; }
      public double HighestLeaderShare { get; set; }
      public string HighestLeader { get; set; } = "";
      public int HighestLeaderDay { get; set; }

      /// <summary>Concentration of fiefs (Herfindahl, 0..1: 1/n when n realms hold equal shares, 1 when one holds all).</summary>
      public double ConcentrationAtStart { get; set; }

      public double ConcentrationAtEnd { get; set; }
      public int Wars { get; set; }
      public int WarsOnLeader { get; set; }
      public int Alliances { get; set; }
      public int AlliancesAgainstLeader { get; set; }
      public int CallsToWar { get; set; }

      /// <summary>One realm ran away with the map (see <see cref="BalanceSurveyPolicy.IsRunaway" />).</summary>
      public bool Runaway { get; set; }
   }

   /// <summary>The survey's arithmetic and its file format. Pure.</summary>
   public static class BalanceSurveyPolicy
   {
      /// <summary>TUNE: a realm holding this share of all fiefs has run away with the map (7 to 9 realms share it at the start).</summary>
      public const double RunawayShare = 0.40;

      /// <summary>TUNE: a leader whose share rose this much since the first week is running away, even below <see cref="RunawayShare" />.</summary>
      public const double RunawayRise = 0.15;

      private const char Sep = ';';

      /// <summary>What a realm's fiefs weigh: a town counts twice a castle; villages follow their town or castle.</summary>
      public static double FiefWeight(SurveyRow row) => row == null ? 0 : row.Towns * 2.0 + row.Castles;

      /// <summary>Each realm's share of all fiefs in one week's rows, by kingdom id.</summary>
      public static Dictionary<string, double> Shares(IEnumerable<SurveyRow> week)
      {
         List<SurveyRow> rows = (week ?? Enumerable.Empty<SurveyRow>()).Where(r => r != null).ToList();
         double total = rows.Sum(FiefWeight);
         var shares = new Dictionary<string, double>();
         foreach (SurveyRow r in rows) shares[r.KingdomId] = total > 0 ? FiefWeight(r) / total : 0;

         return shares;
      }

      /// <summary>The Herfindahl concentration of one week's shares: the sum of squared shares.</summary>
      public static double Concentration(IEnumerable<double> shares) => (shares ?? Enumerable.Empty<double>()).Sum(s => s * s);

      /// <summary>One realm has run away: it holds <see cref="RunawayShare" />, or its share rose <see cref="RunawayRise" /> since the start.</summary>
      public static bool IsRunaway(double leaderShareAtEnd, double leaderShareAtStartOfThatRealm)
         => leaderShareAtEnd >= RunawayShare || leaderShareAtEnd - leaderShareAtStartOfThatRealm >= RunawayRise;

      /// <summary>Summarises a survey: who led, how far, how concentrated, and how often the world turned on the leader.</summary>
      public static BalanceSurveySummary Summarize(IReadOnlyList<SurveyRow> rows, IReadOnlyList<SurveyEvent> events)
      {
         var summary = new BalanceSurveySummary();
         List<IGrouping<int, SurveyRow>> weeks = (rows ?? new List<SurveyRow>()).Where(r => r != null).GroupBy(r => r.Day).OrderBy(g => g.Key).ToList();

         if (weeks.Count > 0)
         {
            summary.Weeks = weeks.Count;
            summary.FirstDay = weeks[0].Key;
            summary.LastDay = weeks[weeks.Count - 1].Key;
            summary.RealmsAtStart = weeks[0].Count();
            summary.RealmsAtEnd = weeks[weeks.Count - 1].Count();

            Dictionary<string, double> first = Shares(weeks[0]);
            Dictionary<string, double> last = Shares(weeks[weeks.Count - 1]);
            (string startId, double startShare) = Leader(first);
            (string endId, double endShare) = Leader(last);
            summary.LeaderAtStart = NameOf(weeks[0], startId);
            summary.LeaderShareAtStart = startShare;
            summary.LeaderAtEnd = NameOf(weeks[weeks.Count - 1], endId);
            summary.LeaderShareAtEnd = endShare;
            summary.ConcentrationAtStart = Concentration(first.Values);
            summary.ConcentrationAtEnd = Concentration(last.Values);

            foreach (IGrouping<int, SurveyRow> week in weeks)
            {
               (string id, double share) = Leader(Shares(week));
               if (share <= summary.HighestLeaderShare) continue;

               summary.HighestLeaderShare = share;
               summary.HighestLeader = NameOf(week, id);
               summary.HighestLeaderDay = week.Key;
            }

            first.TryGetValue(endId ?? "", out double endLeaderAtStart);
            summary.Runaway = IsRunaway(endShare, endLeaderAtStart);
         }

         List<SurveyEvent> all = (events ?? new List<SurveyEvent>()).Where(e => e != null).ToList();
         summary.Wars = all.Count(e => e.Kind == "war");
         summary.WarsOnLeader = all.Count(e => e.Kind == "war" && e.AgainstLeader);
         summary.Alliances = all.Count(e => e.Kind == "alliance");
         summary.AlliancesAgainstLeader = all.Count(e => e.Kind == "alliance" && e.AgainstLeader);
         summary.CallsToWar = all.Count(e => e.Kind == "call_to_war");

         return summary;
      }

      /// <summary>
      ///   Summarises the memory readings the probe could take: where the game started and ended, its peak, the machine's
      ///   peak, and the game's growth split by whether CR loaded any texture between two readings.
      /// </summary>
      public static VramSummary SummarizeVram(IReadOnlyList<SurveyVram> readings)
      {
         List<SurveyVram> taken = (readings ?? new List<SurveyVram>()).Where(r => r != null && r.GameDedicatedMb >= 0)
                                                                       .OrderBy(r => r.Day).ToList();
         var summary = new VramSummary {Readings = taken.Count};
         if (taken.Count == 0) return summary;

         SurveyVram first = taken[0], last = taken[taken.Count - 1];
         summary.GameAtStartMb = first.GameDedicatedMb;
         summary.GameAtEndMb = last.GameDedicatedMb;
         summary.GameHighestMb = taken.Max(r => r.GameDedicatedMb);
         summary.SystemHighestMb = taken.Max(r => r.SystemDedicatedMb);
         summary.CrTexturesAtStart = first.CrTextures;
         summary.CrTexturesAtEnd = last.CrTextures;

         for (var i = 1; i < taken.Count; i++)
         {
            long growth = taken[i].GameDedicatedMb - taken[i - 1].GameDedicatedMb;
            if (taken[i].CrTextures == taken[i - 1].CrTextures) summary.GrowthWhileCrLoadedNothingMb += growth;
            else summary.GrowthWhileCrLoadedMb += growth;
         }

         int days = last.Day - first.Day;
         summary.MbPerGameDay = days > 0 ? (double) (last.GameDedicatedMb - first.GameDedicatedMb) / days : 0;

         return summary;
      }

      /// <summary>A memory reading as one line of the survey file (a line older readers skip).</summary>
      public static string ToLine(SurveyVram v)
         => string.Join(Sep.ToString(), "V", v.Day.ToString(CultureInfo.InvariantCulture),
                        v.GameDedicatedMb.ToString(CultureInfo.InvariantCulture), v.GameSharedMb.ToString(CultureInfo.InvariantCulture),
                        v.SystemDedicatedMb.ToString(CultureInfo.InvariantCulture), v.CrTextures.ToString(CultureInfo.InvariantCulture));

      /// <summary>Reads the memory lines back from a survey file; other lines, and torn ones, are skipped.</summary>
      public static List<SurveyVram> ParseVram(IEnumerable<string> lines)
      {
         var readings = new List<SurveyVram>();
         foreach (string line in lines ?? Enumerable.Empty<string>())
         {
            string[] f = (line ?? "").Split(Sep);
            if (f.Length != 6 || f[0] != "V") continue;

            try
            {
               readings.Add(new SurveyVram {
                  Day = Int(f[1]), GameDedicatedMb = Long(f[2]), GameSharedMb = Long(f[3]), SystemDedicatedMb = Long(f[4]), CrTextures = Int(f[5])
               });
            }
            catch (FormatException)
            {
               // torn by a crash mid-write: skipped
            }
         }

         return readings;
      }

      /// <summary>A kingdom row as one line of the survey file.</summary>
      public static string ToLine(SurveyRow r)
         => string.Join(Sep.ToString(), "K", r.Day.ToString(CultureInfo.InvariantCulture), Clean(r.KingdomId), Clean(r.KingdomName),
                        r.PowerIndex.ToString("0.0", CultureInfo.InvariantCulture), r.Strength.ToString("0", CultureInfo.InvariantCulture),
                        r.Towns.ToString(CultureInfo.InvariantCulture), r.Castles.ToString(CultureInfo.InvariantCulture),
                        r.Villages.ToString(CultureInfo.InvariantCulture), r.Wars.ToString(CultureInfo.InvariantCulture),
                        r.Allies.ToString(CultureInfo.InvariantCulture));

      /// <summary>An event as one line of the survey file.</summary>
      public static string ToLine(SurveyEvent e)
         => string.Join(Sep.ToString(), "E", e.Day.ToString(CultureInfo.InvariantCulture), Clean(e.Kind), Clean(e.AId), Clean(e.AName),
                        Clean(e.BId), Clean(e.BName), e.AgainstLeader ? "1" : "0");

      /// <summary>The header line a survey file opens with, so a spreadsheet names its columns.</summary>
      public const string Header = "type;day;id_or_kind;name_or_a_id;power_or_a_name;strength_or_b_id;towns_or_b_name;castles_or_against_leader;villages;wars;allies";

      /// <summary>Reads a survey file's lines back; lines it cannot read (the header, a torn last line) are skipped.</summary>
      public static (List<SurveyRow> Rows, List<SurveyEvent> Events) Parse(IEnumerable<string> lines)
      {
         var rows = new List<SurveyRow>();
         var events = new List<SurveyEvent>();

         foreach (string line in lines ?? Enumerable.Empty<string>())
         {
            string[] f = (line ?? "").Split(Sep);
            try
            {
               if (f.Length == 11 && f[0] == "K")
                  rows.Add(new SurveyRow {
                     Day = Int(f[1]), KingdomId = f[2], KingdomName = f[3], PowerIndex = Dbl(f[4]), Strength = Dbl(f[5]),
                     Towns = Int(f[6]), Castles = Int(f[7]), Villages = Int(f[8]), Wars = Int(f[9]), Allies = Int(f[10])
                  });
               else if (f.Length == 8 && f[0] == "E")
                  events.Add(new SurveyEvent {
                     Day = Int(f[1]), Kind = f[2], AId = f[3], AName = f[4], BId = f[5], BName = f[6], AgainstLeader = f[7] == "1"
                  });
            }
            catch (FormatException)
            {
               // A line torn by a crash mid-write: skipped, the rest of the survey still reads.
            }
         }

         return (rows, events);
      }

      #region private

      private static (string Id, double Share) Leader(Dictionary<string, double> shares)
         => shares.Count == 0 ? (null, 0) : shares.OrderByDescending(s => s.Value).ThenBy(s => s.Key, StringComparer.Ordinal).Select(s => (s.Key, s.Value)).First();

      private static string NameOf(IEnumerable<SurveyRow> week, string id) => week.FirstOrDefault(r => r.KingdomId == id)?.KingdomName ?? id ?? "";

      private static string Clean(string s) => (s ?? "").Replace(Sep, ',').Replace('\r', ' ').Replace('\n', ' ').Trim();

      private static int Int(string s) => int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

      private static long Long(string s) => long.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

      private static double Dbl(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

      #endregion
   }
}
