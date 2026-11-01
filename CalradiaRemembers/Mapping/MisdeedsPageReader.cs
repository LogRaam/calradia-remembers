// Code written by Gabriel Mailhot, 23/10/2026.
// What the encyclopedia's Misdeeds section shows on the player's page (Gabriel, 23/10/2026: "une autre section avec des
// actions reprehensibles", then "trahisons, extortions, et tout autres gestes qui pourraient etres classes comme
// misdeeds", "adultere est un misdeed"). The words and the gate are MisdeedsPagePolicy's; each kind of deed is read from
// ONE source so it is never told twice: the Trauma ledger (captive scenes, beatings, robberies), the game's death log
// (murders and executions, kept forever, after a battle too), the tasks the player let lapse or gave up, the misdeeds
// ledger (adultery, a captive handed to his enemies, a realm forsaken, a man of no house murdered, raids and sacks), and for saves older
// than that ledger the grudges of a captive handed over and of a spouse taken. Shared by the page and cr.test all
// heart_intimacy and misdeeds_page, so the tests read exactly what the player reads.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Trauma;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads what the player did of their own will for the Misdeeds section.</summary>
   internal static class MisdeedsPageReader
   {
      // The death log keeps how a man died in a private field (CharacterKilledLogEntry._actionDetail, 1.4.8).
      private static readonly FieldInfo DetailField =
         typeof(CharacterKilledLogEntry).GetField("_actionDetail", BindingFlags.NonPublic | BindingFlags.Instance);

      /// <summary>The Misdeeds lines of a hero's page, as shown. Empty on any page but the player's.</summary>
      internal static IReadOnlyList<string> LinesFor(Hero hero)
      {
         if (hero == null || !MisdeedsPagePolicy.Shows(hero == Hero.MainHero)) return Array.Empty<string>();

         var lines = new List<string>();
         foreach (MisdeedFacts deed in MisdeedsPagePolicy.Of(Records(), ModSettings.AdultContentLevel))
         {
            (string name, bool female) = Victim(deed.VictimId);
            if (name != null) lines.Add(MisdeedsPagePolicy.Line(deed, name, female, HeartPageReader.When));
         }

         return lines;
      }

      private static IEnumerable<MisdeedRecord> Records()
      {
         var all = new List<MisdeedRecord>();
         all.AddRange(TraumaLedgerBehavior.All().Select(MisdeedsPagePolicy.FromTrauma).Where(r => r != null));
         all.AddRange(MisdeedLedgerBehavior.All());
         all.AddRange(Deaths());
         all.AddRange(BrokenWords());
         all.AddRange(OlderGrudges());

         return all;
      }

      // Murders and executions by the player's hand or order, from the game's own log (only a victim of a house is logged).
      private static IEnumerable<MisdeedRecord> Deaths()
      {
         var found = new List<MisdeedRecord>();
         try
         {
            if (DetailField == null || Campaign.Current?.LogEntryHistory == null || Hero.MainHero == null) return found;
            foreach (CharacterKilledLogEntry e in Campaign.Current.LogEntryHistory.GetGameActionLogs<CharacterKilledLogEntry>(x => x.Killer == Hero.MainHero && x.Victim != null))
            {
               var detail = (KillCharacterAction.KillCharacterActionDetail) DetailField.GetValue(e);
               MisdeedKind? kind = detail == KillCharacterAction.KillCharacterActionDetail.Murdered ? MisdeedKind.Murdered
                                 : ExecutionDetail.IsExecution(detail) ? MisdeedKind.Executed
                                 : (MisdeedKind?) null;
               if (kind.HasValue) found.Add(new MisdeedRecord {VictimId = e.Victim.StringId, Kind = kind.Value, Day = (int) e.GameTime.ToDays});
            }
         }
         catch (Exception ex) { ModLog.Error($"[MISDEED] death log not read: {ex.GetType().Name}: {ex.Message}"); }

         return found;
      }

      // The tasks the player gave up or let lapse, from each giver's own profile.
      private static IEnumerable<MisdeedRecord> BrokenWords()
      {
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null) return Enumerable.Empty<MisdeedRecord>();

         return store.ListIds()
                     .SelectMany(id => (store.Get(id)?.ActiveQuests ?? new List<NpcMemoryService.Core.Models.InformalQuest>()).Select(q => MisdeedsPagePolicy.FromQuest(id, q)))
                     .Where(r => r != null)
                     .ToList();
      }

      // Saves older than the misdeeds ledger: a captive handed to his enemies and a spouse taken left a grudge, resolved
      // or not (a captive who later died was still handed over).
      private static IEnumerable<MisdeedRecord> OlderGrudges()
      {
         string me = Hero.MainHero?.StringId;
         IReadOnlyList<Grudge> grudges = Grudges.GrudgeBehavior.Instance?.All;
         if (me == null || grudges == null) return Enumerable.Empty<MisdeedRecord>();

         return grudges.Where(g => g != null && g.TargetId == me && (g.Source == GrudgeSource.DeliveredPrisoner || g.Source == GrudgeSource.StolenSpouse))
                       .Select(g => new MisdeedRecord {
                          VictimId = g.HolderId, Day = g.EventDay,
                          Kind = g.Source == GrudgeSource.DeliveredPrisoner ? MisdeedKind.HandedToEnemy : MisdeedKind.StoleSpouse
                       })
                       .ToList();
      }

      // A hero, alive or dead, a realm, or a place.
      private static (string Name, bool Female) Victim(string id)
      {
         Hero h = Hero.Find(id) ?? Hero.FindFirst(x => x.StringId == id);
         if (h != null) return (h.Name?.ToString(), h.IsFemale);

         Kingdom k = Kingdom.All.FirstOrDefault(x => x.StringId == id);

         return (k?.Name?.ToString() ?? Settlement.Find(id)?.Name?.ToString(), false);
      }
   }
}
