// Code written by Gabriel Mailhot, 17/10/2026.
// Authored history, increment 2: the file in the game (docs/design/AUTHORED_HISTORY.md). Gabriel, 17/10/2026: "je
// pourrais mettre en scene des evenements du passe qui ne se sont pas produits dans le temps de jeu"; "it can also be
// able to set a starting vanilla. A file per save ID. File editable with an external tool.. something like a web page
// with a timeline." At each load (and on cr.history_reload) the campaign's history file is read, validated by the pure
// AuthoredHistoryLoader, written into the long-term tier (the file owns those entries), and its starting relations are
// applied through a saved ledger so a reload never stacks them. The mod also writes a snapshot of the world for the
// editor page, and a problems file saying what was set aside.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.AuthoredHistory;
using CalradiaRemembers.Mapping;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>Loads the campaign's authored history file into the world and keeps its relation ledger.</summary>
   public sealed class AuthoredHistoryBehavior : CampaignBehaviorBase
   {
      private const string LedgerSaveKey = "CalradiaRemembers_AuthoredHistoryLedger_v1bytes";

      /// <summary>The world event status of an authored event the game has no kind for ("authored_betrayal").</summary>
      public const string AuthoredStatusPrefix = "authored_";

      private List<HistoryRelationLedgerEntry> _ledger = new();

      public AuthoredHistoryBehavior() => Instance = this;

      public static AuthoredHistoryBehavior Instance { get; private set; }

      /// <summary>The folder of every campaign's history files.</summary>
      public static string Folder
         => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "CalradiaRemembers", "History");

      /// <summary>This campaign's history file.</summary>
      public static string HistoryPath => Path.Combine(Folder, CampaignFileStem + ".history.json");

      /// <summary>This campaign's world snapshot, for the editor page.</summary>
      public static string WorldPath => Path.Combine(Folder, CampaignFileStem + ".world.json");

      /// <summary>What the last load set aside, and why.</summary>
      public static string ProblemsPath => Path.Combine(Folder, CampaignFileStem + ".history.problems.txt");

      /// <summary>The ledger entries in force (for testall).</summary>
      internal IReadOnlyList<HistoryRelationLedgerEntry> Ledger => _ledger;

      private static string CampaignFileStem
      {
         get
         {
            string id = Campaign.Current?.UniqueGameId ?? "campaign";

            return new string(id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
         }
      }

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => SafeRun.Step("AuthoredHistory.Load", () => Sync()));
         // The editor needs a fresh world, but only for an author: no file, no snapshot written behind the player's back.
         CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, () =>
         {
            if (File.Exists(HistoryPath)) SafeRun.Step("AuthoredHistory.Snapshot", () => WriteSnapshot());
         });
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new List<HistoryRelationLedgerEntry>(_ledger), Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[HISTORY] ledger serialize failed, persisting none this save: {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(LedgerSaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[HISTORY] ledger save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(LedgerSaveKey, ref bytes); }
            catch { bytes = null; } // an older save has no ledger

            _ledger = new List<HistoryRelationLedgerEntry>();
            if (bytes == null || bytes.Length == 0) return;

            try { _ledger = JsonConvert.DeserializeObject<List<HistoryRelationLedgerEntry>>(Encoding.UTF8.GetString(bytes)) ?? new List<HistoryRelationLedgerEntry>(); }
            catch (Exception ex) { ModLog.Error($"[HISTORY] ledger load failed, starting empty: {ex.GetType().Name}: {ex.Message}"); }
         }
      }

      /// <summary>Reads this campaign's file (none: an empty history) and brings the world in line with it. Returns a report.</summary>
      public string Sync()
      {
         string json = null;
         try
         {
            if (File.Exists(HistoryPath)) json = File.ReadAllText(HistoryPath);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[HISTORY] could not read {HistoryPath}: {ex.Message}");

            return $"Could not read the history file: {ex.Message}";
         }

         // No file and nothing the file ever did: nothing to touch, nothing to write.
         if (json == null && _ledger.Count == 0 && WorldEventStore.Instance?.AllHistory().Any(e => e.IsAuthored) != true) return "No history file for this campaign.";

         return SyncFrom(json);
      }

      /// <summary>The sync on a given text (the testall seam: the real path, without touching the author's file).</summary>
      internal string SyncFrom(string json)
      {
         WorldEventStore store = WorldEventStore.Instance;
         if (store == null) return "World-event store is not active.";

         var problems = new List<string>();
         AuthoredHistoryDocument doc = AuthoredHistoryLoader.Parse(json, problems);
         if (doc == null)
         {
            // A broken file changes nothing: better the history of the last good load than none.
            WriteProblems(problems);

            return problems[0];
         }

         HistoryWorldFacts world = WorldFacts();
         AuthoredHistoryLoadResult result = AuthoredHistoryLoader.Validate(doc, world);
         problems.AddRange(result.Problems);

         int removed = store.ReplaceAuthored(result.Events.Select(ToWorldEvent));

         HistoryRelationPlan plan = HistoryRelationLedger.Plan(result.Events, _ledger, CurrentRelation, world.PlayerId);
         foreach (HistoryRelationStep step in plan.Steps) Apply(step);
         _ledger = plan.Ledger;

         WriteProblems(problems);
         string report = $"Authored history: {result.Events.Count} event(s) in the world (replacing {removed}), {plan.Steps.Count} relation move(s), "
                         + $"{problems.Count} problem(s){(problems.Count > 0 ? ", see " + ProblemsPath : "")}.";
         ModLog.Info("[HISTORY] " + report);

         return report;
      }

      /// <summary>Writes the world snapshot the editor page reads. Returns its path.</summary>
      public string WriteSnapshot()
      {
         Directory.CreateDirectory(Folder);
         HistoryWorldFacts facts = WorldFacts();
         CampaignTime start = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime;

         var snapshot = new {
            format = 1,
            campaign = Campaign.Current.UniqueGameId,
            calendar = new {
               daysInSeason = CampaignTime.DaysInSeason, seasonsInYear = CampaignTime.SeasonsInYear, startDay = facts.CampaignStartDay,
               startYear = start.GetYear, startSeason = AuthoredHistoryLoader.Seasons[(int) start.GetSeasonOfYear], today = (int) CampaignTime.Now.ToDays,
               year = CampaignTime.Now.GetYear
            },
            player = facts.PlayerId,
            heroes = AllHeroes().Select(h => new {
               id = h.StringId, name = h.Name?.ToString(), clan = h.Clan?.StringId, culture = h.Culture?.Name?.ToString(), female = h.IsFemale,
               born = h.BirthDay.GetYear, dead = h.IsDead, spouse = h.Spouse?.StringId, lord = h.IsLord
            }),
            clans = Clan.All.Where(c => c != null && !c.IsBanditFaction).Select(c => new {
               id = c.StringId, name = c.Name?.ToString(), kingdom = c.Kingdom?.StringId, leader = c.Leader?.StringId
            }),
            kingdoms = Kingdom.All.Select(k => new {id = k.StringId, name = k.Name?.ToString(), ruler = k.Leader?.StringId}),
            settlements = Settlement.All.Where(s => s != null && !s.IsHideout).Select(s => new {
               id = s.StringId, name = s.Name?.ToString(), type = s.IsTown ? "town" : s.IsCastle ? "castle" : s.IsVillage ? "village" : "other",
               owner = s.OwnerClan?.StringId
            }),
            recorded = (WorldEventStore.Instance?.AllHistory() ?? new List<WorldEvent>()).Where(e => !e.IsAuthored).Select(e => new {
               id = e.Id, day = e.Day, kind = WorldEventStore.HistoryKindOf(e).ToString(), summary = e.Summary
            })
         };

         File.WriteAllText(WorldPath, JsonConvert.SerializeObject(snapshot, Formatting.Indented));

         return WorldPath;
      }

      /// <summary>Creates an empty history file for this campaign when there is none. Returns true when it created one.</summary>
      public static bool EnsureHistoryFile()
      {
         Directory.CreateDirectory(Folder);
         if (File.Exists(HistoryPath)) return false;

         var empty = new AuthoredHistoryDocument {Campaign = Campaign.Current?.UniqueGameId, Title = "The past of this campaign"};
         File.WriteAllText(HistoryPath, JsonConvert.SerializeObject(empty, Formatting.Indented));

         return true;
      }

      #region private

      private static IEnumerable<Hero> AllHeroes()
         => Hero.AllAliveHeroes.Concat(Hero.DeadOrDisabledHeroes).Where(h => h != null && (h.IsLord || h.IsWanderer || h.IsNotable || h == Hero.MainHero))
                .GroupBy(h => h.StringId).Select(g => g.First());

      private static HistoryWorldFacts WorldFacts() => new() {
         DaysInSeason = CampaignTime.DaysInSeason,
         SeasonsInYear = CampaignTime.SeasonsInYear,
         CampaignStartDay = (int) Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ToDays,
         PlayerId = Hero.MainHero?.StringId,
         Heroes = AllHeroes().Select(h => new HistoryHeroFacts {
            Id = h.StringId, Name = h.Name?.ToString(), BirthDay = (int) h.BirthDay.ToDays, IsDead = h.IsDead, SpouseId = h.Spouse?.StringId
         }).ToList(),
         // The same sets the snapshot offers the editor page, so the page and the game never disagree on a place or realm.
         PlaceIds = new HashSet<string>(Settlement.All.Where(s => s != null && !s.IsHideout).Select(s => s.StringId)),
         RealmIds = new HashSet<string>(Kingdom.All.Select(k => k.StringId).Concat(Clan.All.Where(c => c != null && !c.IsBanditFaction).Select(c => c.StringId)))
      };

      // The kinds the game records map to their own type, so the long-term memory and its readers treat an authored
      // battle as a battle; the rest are Other with an authored status (HistoryKind.Authored).
      private static WorldEvent ToWorldEvent(AcceptedHistoryEvent a)
      {
         var e = new WorldEvent {
            Id = "authored:" + a.Id, AuthoredId = a.Id, Day = a.Day, Magnitude = a.Importance, Summary = a.Summary, ActorIds = a.ActorIds.ToList(),
            AggrievedActorIds = a.HarmedIds.ToList(), FactionIds = a.RealmIds.ToList(), PlaceId = a.PlaceId, KnownOnlyToThoseInvolved = a.Secret,
            KnownToIds = a.KnownByIds.ToList(), InvolvesPlayer = a.InvolvesPlayer, SourceMod = "CalradiaRemembers.AuthoredHistory"
         };

         switch (a.Kind)
         {
            case "battle":
            case "siege": e.Type = WorldEventType.Battle; break;
            case "fief_taken": e.Type = WorldEventType.SettlementCaptured; break;
            case "execution": e.Type = WorldEventType.Execution; break;
            case "marriage": e.Type = WorldEventType.Marriage; break;
            case "divorce": e.Type = WorldEventType.Divorce; break;
            case "war": e.Type = WorldEventType.WarDeclared; break;
            case "peace": e.Type = WorldEventType.PeaceDeclared; break;
            case "rebellion": e.Type = WorldEventType.Rebellion; break;
            case "scheme": e.Type = WorldEventType.Rivalry; e.Status = SchemeStore.DiscoveredStatus; break;
            case "capture": e.Type = WorldEventType.Other; e.Status = LongTermHistoryPolicy.CaptureStatus; break;
            case "fell_in_battle": e.Type = WorldEventType.Other; e.Status = LongTermHistoryPolicy.FellInBattleStatus; break;
            default: e.Type = WorldEventType.Other; e.Status = AuthoredStatusPrefix + a.Kind; break;
         }

         return e;
      }

      private static Hero Find(string id) => string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h.StringId == id);

      private static int CurrentRelation(string a, string b)
      {
         Hero ha = Find(a), hb = Find(b);

         return ha != null && hb != null ? CharacterRelationManager.GetHeroRelation(ha, hb) : 0;
      }

      // Set directly rather than through ChangeRelationAction, which scales a rise by the diplomacy model: the ledger must
      // know exactly what the file did. A step with the player moves the mod's personal regard too, the ledger an NPC's
      // feeling toward the player is judged on (NpcProfile.ReputationWithPlayer).
      private static void Apply(HistoryRelationStep step)
      {
         Hero a = Find(step.A), b = Find(step.B);
         if (a == null || b == null) return;

         int value = Math.Max(-VanillaRelation.Bound, Math.Min(VanillaRelation.Bound, CharacterRelationManager.GetHeroRelation(a, b) + step.Delta));
         a.SetPersonalRelation(b, value);

         if (!step.TouchesPlayer) return;

         Hero npc = a == Hero.MainHero ? b : a;
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null) return;

         NpcProfile profile = store.Get(npc.StringId) ?? HeroProfileMapper.FromHero(npc);
         ProfileMutator.ApplyReputationDelta(profile, step.Delta);
         store.Set(profile);
      }

      private static void WriteProblems(List<string> problems)
      {
         try
         {
            Directory.CreateDirectory(Folder);
            if (problems.Count == 0)
            {
               if (File.Exists(ProblemsPath)) File.Delete(ProblemsPath);

               return;
            }

            File.WriteAllLines(ProblemsPath, problems);
            foreach (string p in problems) ModLog.Info("[HISTORY] " + p);
         }
         catch (Exception ex) { ModLog.Error($"[HISTORY] could not write the problems file: {ex.Message}"); }
      }

      #endregion
   }
}
