// Code written by Gabriel Mailhot, 22/10/2026.
// What a character knows of the edicts where they live (Gabriel, 21/10/2026: edicts on towns and realms, contestable):
// the edict holding on the town they stand in or call home, their realm's, and one their town forced off. The town's
// tooltip shows an edict; its people must be able to speak of it. Worded by the pure EdictNote.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Edicts;
using CalradiaRemembers.Logic.Edicts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The "edicts where you live" lines for a character, or null.</summary>
   public static class EdictNoteResolver
   {
      /// <summary>TUNE: the most edict lines a character is given.</summary>
      private const int MaxLines = 3;

      /// <summary>TUNE: days a town remembers forcing its own edict off.</summary>
      private const int ForcedOffRememberedDays = 20;

      /// <summary>The lines, or null when no edict touches where they live.</summary>
      public static string Resolve(Hero npc)
      {
         try
         {
            EdictBehavior b = EdictBehavior.Instance;
            if (npc == null || b == null || b.Ledger.Edicts.Count == 0) return null;

            int today = EdictBehavior.Today;
            List<Settlement> towns = new[] {npc.CurrentSettlement, npc.HomeSettlement}.Where(s => s != null && s.IsTown).Distinct().ToList();
            var lines = new List<string>();
            var seen = new HashSet<Edict>();

            foreach (Settlement town in towns)
            {
               Edict e = b.HoldingOn(town.Town);
               if (e != null && seen.Add(e)) lines.Add(Line(e, npc, town, today, forcedOff: false));
               Edict off = b.Ledger.Edicts.FirstOrDefault(x => x.RepealedIn.Contains(town.StringId) || (x.Status == EdictStatus.Repealed && x.TargetId == town.StringId && today - x.EndedDay <= ForcedOffRememberedDays));
               if (off != null && seen.Add(off)) lines.Add(Line(off, npc, town, today, forcedOff: true));
            }

            Edict realm = npc.MapFaction is Kingdom k ? b.Ledger.OfRealm(k.StringId) : null;
            if (realm != null && seen.Add(realm)) lines.Add(Line(realm, npc, null, today, forcedOff: false));

            List<string> said = lines.Where(l => l != null).Take(MaxLines).ToList();

            return said.Count == 0 ? null : string.Join("\n", said);
         }
         catch
         {
            return null;
         }
      }

      private static string Line(Edict e, Hero npc, Settlement town, int today, bool forcedOff)
      {
         string enactor = e.EnactorId == Hero.MainHero?.StringId ? Hero.MainHero.Name.ToString() : Hero.Find(e.EnactorId)?.Name?.ToString() ?? "a lord now gone";
         string where = forcedOff || e.Scope == EdictScope.Town ? town?.Name?.ToString() ?? Settlement.Find(e.TargetId)?.Name?.ToString()
            : $"all the towns of {Kingdom.All.FirstOrDefault(k => k.StringId == e.TargetId)?.Name}";
         string id = town?.StringId;
         int grievance = id != null && e.Grievance.TryGetValue(id, out int g) ? g : 0;

         return EdictNote.Line(new EdictNoteFacts {
            Name = e.Name, EnactorName = enactor, SpeakerDecreedIt = e.EnactorId == npc.StringId, Where = where, Effects = e.Effect.Describe(),
            DaysLeft = e.DaysLeft(today), Contested = id != null && e.ContestedIn.Contains(id), Grumbling = grievance > 0, ForcedOff = forcedOff
         });
      }
   }
}
