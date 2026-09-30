// Code written by Gabriel Mailhot, 26/09/2026.
// fkasad's grid for a mercenary's departure (Nexus, 25/09/2026; ratified by Gabriel). The rules are
// MercenaryDeparturePolicy's (pure, tested); this is the engine half:
//
//   - When the player's contract ends (LeaveAsMercenary, fed by WorldEventFeeders), the realm's situation is read (at
//     war? losing, by its strength against its enemies'?) and the departure judged: at peace, let go with thanks, or
//     abandoned. The realm's lords remember it on the mod's own regard ledger and in their memory, doubled against a
//     company of their own people, and word of it gets about.
//   - The departure is kept for BetrayalWindowDays: joining the former employer's enemy within it upgrades the verdict
//     to a betrayal, charging only the difference.
//   - Joining a realm of one's own people in a war it is losing is remembered well by its lords.
//   - The GAME's own companies are judged by the same grid (Zivkyn, Nexus: "why does nobody resent the
//     Skolderbrotva?"): the realm's lords take it out on the company's leader through the game's own relation (the
//     mod's regard is toward the player only), and word goes about only of an abandonment or a betrayal.
//   - The player leaving a realm as a VASSAL holding no fief is judged by the same grid, the oath weighing like
//     kinship (fkasad, 24/09/2026). One who leaves with fiefs takes them to war against the realm: not judged here.
//
// Additive save keys.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>Judges the end of the player's mercenary contracts, and remembers them.</summary>
   public sealed class MercenaryReputationBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CR_MercenaryDeparture_v1bytes";

      // The game's companies: their last departure, by clan id, kept for BetrayalWindowDays (additive key, 27/09/2026).
      private const string CompaniesSaveKey = "CR_MercenaryCompanyDepartures_v1bytes";

      private Dictionary<string, Departure> _companies = new();

      public static MercenaryReputationBehavior Instance { get; private set; }

      private Departure _last;

      public MercenaryReputationBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      private static string PlayerName => Hero.MainHero?.Name?.ToString() ?? "the player";

      public override void RegisterEvents()
      {
      }

      /// <summary>
      ///   The player's mercenary contract with <paramref name="kingdom" /> ended: judged by the realm's situation,
      ///   remembered by its lords, and kept for a later betrayal. Returns false when nothing could be judged (the
      ///   caller then records the plain contract end).
      /// </summary>
      public bool OnLeft(Kingdom kingdom)
      {
         try
         {
            if (kingdom == null) return false;

            (bool atWar, float own, float enemies) = WarFooting(kingdom);
            MercenaryDepartureVerdict verdict = MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {
               EmployerAtWar = atWar, EmployerStrength = own, EnemiesStrength = enemies
            });
            bool sameCulture = SameCulture(kingdom);

            Remember(kingdom, verdict, MercenaryDeparturePolicy.RegardShift(verdict, sameCulture), null);
            _last = new Departure {KingdomId = kingdom.StringId, Day = Today, Verdict = verdict, SameCulture = sameCulture};
            ModLog.Info($"[MERCENARY] contract with {kingdom.Name} ended: {verdict} (at war {atWar}, strength {own:0} vs {enemies:0}, same culture {sameCulture}).");

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MERCENARY] judging the departure failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   The player's clan left <paramref name="kingdom" /> as a vassal. Holding no fief, the departure is judged by
      ///   the grid with the oath counted, and kept for a later betrayal. Returns true when the judged news was
      ///   recorded; false when the caller must record the plain departure (fiefs held, the realm at peace, or a
      ///   failure).
      /// </summary>
      public bool OnLeftAsVassal(Kingdom kingdom)
      {
         try
         {
            if (kingdom == null) return false;

            int fiefs = Clan.PlayerClan?.Fiefs?.Count ?? 0;
            if (!MercenaryDeparturePolicy.JudgesAVassalsDeparture(fiefs))
            {
               ModLog.Info($"[MERCENARY] left {kingdom.Name} as a vassal holding {fiefs} fief(s): the war that follows is the consequence, not judged.");

               return false;
            }

            (bool atWar, float own, float enemies) = WarFooting(kingdom);
            MercenaryDepartureVerdict verdict = MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {
               EmployerAtWar = atWar, EmployerStrength = own, EnemiesStrength = enemies
            });
            bool sameCulture = SameCulture(kingdom);

            _last = new Departure {KingdomId = kingdom.StringId, Day = Today, Verdict = verdict, SameCulture = sameCulture, Sworn = true};
            ModLog.Info($"[MERCENARY] left {kingdom.Name} as a vassal holding no fief: {verdict} (at war {atWar}, strength {own:0} vs {enemies:0}, same culture {sameCulture}).");

            if (verdict == MercenaryDepartureVerdict.AtPeace) return false;

            Remember(kingdom, verdict, MercenaryDeparturePolicy.RegardShift(verdict, sameCulture, sworn: true), null, sworn: true);

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MERCENARY] judging the vassal's departure failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   The player's clan took service with <paramref name="kingdom" /> (as a company or a vassal): a recent
      ///   departure from its enemy becomes a betrayal, and coming to one's own people in a losing war is remembered.
      /// </summary>
      public void OnJoined(Kingdom kingdom)
      {
         try
         {
            if (kingdom == null) return;

            if (_last != null)
            {
               Kingdom former = Kingdom.All?.FirstOrDefault(k => k.StringId == _last.KingdomId);
               bool atWarWithFormer = former != null && former != kingdom && FactionManager.IsAtWarAgainstFaction(kingdom, former);

               if (former != null && MercenaryDeparturePolicy.IsBetrayal(_last.Day, Today, atWarWithFormer))
               {
                  int upgrade = MercenaryDeparturePolicy.BetrayalUpgradeShift(_last.Verdict, _last.SameCulture, _last.Sworn);
                  Remember(former, MercenaryDepartureVerdict.Betrayal, upgrade, kingdom.Name?.ToString(), _last.Sworn, departure: false);
                  ModLog.Info($"[MERCENARY] joining {kingdom.Name} turns the departure from {former.Name} into a betrayal ({upgrade} regard).");
               }

               _last = null;
            }

            (bool atWar, float own, float enemies) = WarFooting(kingdom);
            if (atWar && MercenaryDeparturePolicy.IsAidOfKin(SameCulture(kingdom), MercenaryDeparturePolicy.IsLosing(own, enemies)))
               RememberAidOfKin(kingdom);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MERCENARY] judging the new service failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   One of the GAME's mercenary companies left a realm, took a new contract, or both at once. Judged by the same
      ///   grid as the player's: the realm's lords take it out on the company's leader (the game's relation), word goes
      ///   about of an abandonment or a betrayal, and joining the former employer's enemy within the window is a betrayal.
      /// </summary>
      public void OnCompanyChangedKingdom(Clan company, Kingdom oldKingdom, Kingdom newKingdom, bool leftAsMercenary, bool joinedAsMercenary)
      {
         try
         {
            if (company == null || company == Clan.PlayerClan || company.Leader == null) return;

            // Straight over to the former employer's enemy in one step: one piece of news (the betrayal), not two.
            bool straightToEnemy = joinedAsMercenary && oldKingdom != null && newKingdom != null && newKingdom != oldKingdom
                                   && FactionManager.IsAtWarAgainstFaction(newKingdom, oldKingdom);

            if (oldKingdom != null && (leftAsMercenary || joinedAsMercenary)) JudgeCompanyDeparture(company, oldKingdom, announce: !straightToEnemy);
            if (joinedAsMercenary && newKingdom != null) JudgeCompanyNewService(company, newKingdom);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MERCENARY] judging {company?.Name}'s change of contract failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Test seam (cr.testall): how <paramref name="company" /> leaving <paramref name="kingdom" /> now would be judged and worded, applying nothing.</summary>
      internal (MercenaryDepartureVerdict Verdict, string Summary) DebugJudgeCompany(Clan company, Kingdom kingdom)
      {
         (bool atWar, float own, float enemies) = WarFooting(kingdom);
         MercenaryDepartureVerdict verdict = MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {EmployerAtWar = atWar, EmployerStrength = own, EnemiesStrength = enemies});

         return (verdict, MercenaryDeparturePolicy.CompanySummary(verdict, company?.Name?.ToString(), kingdom?.Name?.ToString()));
      }

      /// <summary>
      ///   Test seam (cr.testall): how the player leaving <paramref name="kingdom" /> as a vassal would be taken right
      ///   now, WITHOUT applying anything: whether the grid judges it (no fief held), the verdict, and the regard shift.
      /// </summary>
      internal (bool Judged, MercenaryDepartureVerdict Verdict, int Shift, string Summary) DebugJudgeVassal(Kingdom kingdom)
      {
         bool judged = MercenaryDeparturePolicy.JudgesAVassalsDeparture(Clan.PlayerClan?.Fiefs?.Count ?? 0);
         (bool atWar, float own, float enemies) = WarFooting(kingdom);
         MercenaryDepartureVerdict verdict = MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {EmployerAtWar = atWar, EmployerStrength = own, EnemiesStrength = enemies});

         return (judged, verdict, MercenaryDeparturePolicy.RegardShift(verdict, SameCulture(kingdom), sworn: true),
                 MercenaryDeparturePolicy.VassalSummary(verdict, PlayerName, kingdom?.Name?.ToString()));
      }

      /// <summary>
      ///   Test seam (cr.testall): the verdict a contract with <paramref name="kingdom" /> would end on right now, and
      ///   the realm's footing, WITHOUT applying anything (no regard, no memory, no news).
      /// </summary>
      internal (MercenaryDepartureVerdict Verdict, bool AtWar, float Own, float Enemies) DebugJudge(Kingdom kingdom)
      {
         (bool atWar, float own, float enemies) = WarFooting(kingdom);

         return (MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {EmployerAtWar = atWar, EmployerStrength = own, EnemiesStrength = enemies}),
                 atWar, own, enemies);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_last, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[MERCENARY] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("null");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[MERCENARY] save failed: {ex.GetType().Name}: {ex.Message}"); }

            byte[] companies;
            try { companies = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_companies, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[MERCENARY] companies serialize failed, persisting none this save. {ex.GetType().Name}: {ex.Message}");
               companies = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(CompaniesSaveKey, ref companies); }
            catch (Exception ex) { ModLog.Error($"[MERCENARY] companies save failed: {ex.GetType().Name}: {ex.Message}"); }

            return;
         }

         byte[] loadedCompanies = null;
         try { dataStore.SyncData(CompaniesSaveKey, ref loadedCompanies); }
         catch
         {
            /* key absent in older saves */
         }

         _companies = new Dictionary<string, Departure>();
         if (loadedCompanies is {Length: > 0})
            try { _companies = JsonConvert.DeserializeObject<Dictionary<string, Departure>>(Encoding.UTF8.GetString(loadedCompanies)) ?? new Dictionary<string, Departure>(); }
            catch (Exception ex) { ModLog.Error($"[MERCENARY] companies load failed, their departures are dropped. {ex.GetType().Name}: {ex.Message}"); }

         byte[] loaded = null;
         try { dataStore.SyncData(SaveKey, ref loaded); }
         catch
         {
            /* key absent in older saves */
         }

         if (loaded is not {Length: > 0}) return;

         try { _last = JsonConvert.DeserializeObject<Departure>(Encoding.UTF8.GetString(loaded)); }
         catch (Exception ex)
         {
            _last = null;
            ModLog.Error($"[MERCENARY] load failed, the last departure is dropped. {ex.GetType().Name}: {ex.Message}");
         }
      }

      #region private

      /// <summary>Whether the realm is at war, its own strength, and its enemies' combined strength.</summary>
      private static (bool AtWar, float Own, float Enemies) WarFooting(Kingdom kingdom)
      {
         List<Kingdom> enemies = (Kingdom.All ?? new List<Kingdom>())
                                 .Where(k => k != null && k != kingdom && !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(k, kingdom))
                                 .ToList();

         return (enemies.Count > 0, kingdom.CurrentTotalStrength, enemies.Sum(k => k.CurrentTotalStrength));
      }

      private static bool SameCulture(Kingdom kingdom)
         => kingdom?.Culture != null && Hero.MainHero?.Culture == kingdom.Culture;

      /// <summary>The realm's lords (its clan leaders, the ruler first), never the player's own clan.</summary>
      private static IEnumerable<Hero> RealmLords(Kingdom kingdom)
         => (kingdom.Clans ?? Enumerable.Empty<Clan>())
            .Where(c => c != null && c != Clan.PlayerClan && !c.IsEliminated)
            .Select(c => c.Leader)
            .Where(h => h is {IsAlive: true} && h != Hero.MainHero)
            .OrderByDescending(h => h == kingdom.Leader);

      /// <summary>
      ///   The realm's lords take the verdict to heart (regard and memory), and word of it gets about. A
      ///   <paramref name="sworn" /> vassal's departure is worded as a renounced oath, and marked as the player leaving
      ///   the realm (it replaces the plain departure news, which carried that mark), unless it is only the later
      ///   upgrade to a betrayal.
      /// </summary>
      private static void Remember(Kingdom kingdom, MercenaryDepartureVerdict verdict, int regardShift, string enemyName, bool sworn = false, bool departure = true)
      {
         string realm = kingdom.Name?.ToString();
         string memory = sworn
            ? MercenaryDeparturePolicy.VassalLordMemory(verdict, PlayerName, enemyName)
            : MercenaryDeparturePolicy.LordMemory(verdict, PlayerName, enemyName);
         ShiftLords(kingdom, regardShift, memory);

         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = sworn ? MercenaryDeparturePolicy.VassalMagnitude(verdict) : MercenaryDeparturePolicy.Magnitude(verdict),
            InvolvesPlayer = true,
            Summary = sworn
               ? MercenaryDeparturePolicy.VassalSummary(verdict, PlayerName, realm, enemyName)
               : MercenaryDeparturePolicy.Summary(verdict, PlayerName, realm, enemyName),
            ActorIds = new List<string> {Hero.MainHero?.StringId}.Where(id => id != null).ToList(),
            FactionIds = new List<string> {kingdom.StringId},
            ChronicleLine = sworn
               ? MercenaryDeparturePolicy.VassalChronicleLine(verdict, PlayerName, realm, enemyName)
               : MercenaryDeparturePolicy.ChronicleLine(verdict, PlayerName, realm, enemyName),
            Status = sworn && departure ? WorldEventFeeders.PlayerLeftRealmStatus : null
         });
      }

      /// <summary>A realm of the player's own people, losing its war, remembers the company that came to its aid.</summary>
      private static void RememberAidOfKin(Kingdom kingdom)
      {
         ShiftLords(kingdom, MercenaryDeparturePolicy.AidOfKinGratitude, MercenaryDeparturePolicy.AidOfKinMemory(PlayerName));

         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = 50,
            InvolvesPlayer = true,
            Summary = $"{PlayerName} rode to the aid of {kingdom.Name}, a realm of their own people, while the war went against it.",
            ActorIds = new List<string> {Hero.MainHero?.StringId}.Where(id => id != null).ToList(),
            FactionIds = new List<string> {kingdom.StringId},
            ChronicleLine = $"{PlayerName} rode to the aid of {kingdom.Name}, their own people, in its hour of need"
         });
         ModLog.Info($"[MERCENARY] {kingdom.Name}, a realm of the player's own people, remembers the company come to its aid.");
      }

      /// <summary>Applies a regard shift and a memory to each of the realm's lords (a profile is made for one who has none).</summary>
      private static void ShiftLords(Kingdom kingdom, int shift, string memory)
      {
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null || (shift == 0 && memory == null)) return;

         foreach (Hero lord in RealmLords(kingdom))
         {
            NpcProfile profile = store.Get(lord.StringId);
            if (profile == null)
            {
               profile = HeroProfileMapper.FromHero(lord);
               store.Set(profile);
            }

            NpcMemoryService.Core.Services.ProfileMutator.ApplyReputationDelta(profile, shift, NpcMemoryService.Core.Standing.RegardCause.Departure);
            if (memory != null) profile.Events.Add(new NotableEvent(Today, NotableEventType.Other, memory));
            store.Set(profile);
         }
      }

      /// <summary>A company's departure from <paramref name="kingdom" />, judged, taken out on its leader, and kept for a betrayal.</summary>
      private void JudgeCompanyDeparture(Clan company, Kingdom kingdom, bool announce)
      {
         (bool atWar, float own, float enemies) = WarFooting(kingdom);
         MercenaryDepartureVerdict verdict = MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {
            EmployerAtWar = atWar, EmployerStrength = own, EnemiesStrength = enemies
         });
         bool sameCulture = company.Culture != null && company.Culture == kingdom.Culture;

         ShiftLordsTowardCompany(kingdom, company, MercenaryDeparturePolicy.RegardShift(verdict, sameCulture));
         if (announce && MercenaryDeparturePolicy.CompanyDepartureIsNews(verdict)) RecordCompanyNews(company, kingdom, verdict, null);

         foreach (string stale in _companies.Where(d => Today - d.Value.Day > MercenaryDeparturePolicy.BetrayalWindowDays).Select(d => d.Key).ToList())
            _companies.Remove(stale);
         _companies[company.StringId] = new Departure {KingdomId = kingdom.StringId, Day = Today, Verdict = verdict, SameCulture = sameCulture};

         ModLog.Info($"[MERCENARY] company {company.Name} left {kingdom.Name}: {verdict} (at war {atWar}, strength {own:0} vs {enemies:0}).");
      }

      /// <summary>A company took a new contract: going over to its former employer's enemy within the window is a betrayal.</summary>
      private void JudgeCompanyNewService(Clan company, Kingdom kingdom)
      {
         if (!_companies.TryGetValue(company.StringId, out Departure last)) return;

         Kingdom former = Kingdom.All?.FirstOrDefault(k => k.StringId == last.KingdomId);
         bool atWarWithFormer = former != null && former != kingdom && FactionManager.IsAtWarAgainstFaction(kingdom, former);
         _companies.Remove(company.StringId);

         if (former == null || !MercenaryDeparturePolicy.IsBetrayal(last.Day, Today, atWarWithFormer)) return;

         ShiftLordsTowardCompany(former, company, MercenaryDeparturePolicy.BetrayalUpgradeShift(last.Verdict, last.SameCulture));
         RecordCompanyNews(company, former, MercenaryDepartureVerdict.Betrayal, kingdom.Name?.ToString());
         ModLog.Info($"[MERCENARY] company {company.Name} went over to {kingdom.Name}, {former.Name}'s enemy: a betrayal.");
      }

      /// <summary>The realm's lords take it out on the company's leader, through the game's own relation.</summary>
      private static void ShiftLordsTowardCompany(Kingdom kingdom, Clan company, int shift)
      {
         if (shift == 0 || company.Leader == null) return;

         foreach (Hero lord in RealmLords(kingdom).Where(h => h.Clan != company))
            TaleWorlds.CampaignSystem.Actions.ChangeRelationAction.ApplyRelationChangeBetweenHeroes(lord, company.Leader, shift, false);
      }

      private static void RecordCompanyNews(Clan company, Kingdom kingdom, MercenaryDepartureVerdict verdict, string enemyName)
      {
         Kingdom enemy = enemyName == null ? null : Kingdom.All?.FirstOrDefault(k => k.Name?.ToString() == enemyName);
         var factions = new List<string> {kingdom.StringId};
         if (enemy != null) factions.Add(enemy.StringId);

         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = WorldEventType.Other,
            Day = Today,
            Magnitude = MercenaryDeparturePolicy.Magnitude(verdict),
            Summary = MercenaryDeparturePolicy.CompanySummary(verdict, company.Name?.ToString(), kingdom.Name?.ToString(), enemyName),
            ActorIds = new List<string> {company.Leader?.StringId}.Where(id => id != null).ToList(),
            FactionIds = factions,
            ChronicleLine = MercenaryDeparturePolicy.CompanyChronicleLine(verdict, company.Name?.ToString(), kingdom.Name?.ToString(), enemyName)
         });
      }

      /// <summary>The last contract the player left, kept for a later betrayal.</summary>
      private sealed class Departure
      {
         public string KingdomId { get; set; }
         public int Day { get; set; }
         public MercenaryDepartureVerdict Verdict { get; set; }
         public bool SameCulture { get; set; }

         /// <summary>A vassal's departure (the oath counted), not a company's; absent in saves before 2.6.6, so false.</summary>
         public bool Sworn { get; set; }
      }

      #endregion
   }
}
