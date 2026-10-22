// Code written by Gabriel Mailhot, 22/10/2026.
// cr.edict: the edicts in force and recently ended, and the dev's levers (Gabriel, 21/10/2026: edicts on towns and
// realms, by the player and by the world's lords; an edict may be contested).

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Edicts;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Edicts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.edict [decree kind town|realm [days] | withdraw id | day | world]: the edicts, and their levers.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("edict", "cr")]
      public static string Edict(List<string> args)
      {
         EdictBehavior b = EdictBehavior.Instance;
         if (b == null) return "cr.edict: no campaign loaded.";
         int today = EdictBehavior.Today;
         string verb = args.Count > 0 ? args[0].ToLowerInvariant() : "";

         switch (verb)
         {
            case "decree":
            {
               EdictEffect effect = args.Count > 1 ? EdictCatalog.Find(args[1]) : null;
               if (effect == null) return $"cr.edict decree <kind> <town|realm> [days]; kinds: {string.Join(", ", EdictCatalog.All.Select(e => e.Key))}";
               bool realm = args.Count > 2 && args[2].ToLowerInvariant() == "realm";
               Settlement town = realm || args.Count < 3 ? Hero.MainHero?.CurrentSettlement : Settlement.All.FirstOrDefault(s => s.IsTown && s.Name.ToString().ToLowerInvariant().Contains(args[2].ToLowerInvariant()));
               int days = args.Count > 3 && int.TryParse(args[3], out int d) ? d : 0;
               Hero enactor = realm ? Hero.MainHero?.Clan?.Kingdom?.Leader : town?.OwnerClan?.Leader;
               Logic.Edicts.Edict e = b.Decree(enactor, realm ? EdictScope.Realm : EdictScope.Town, town, effect.Kind, days, null, out string refusal);

               return e == null ? $"cr.edict: refused for {enactor?.Name}: {refusal}" : $"cr.edict: {e.Id} '{e.Name}' by {enactor.Name}, until day {e.EndDay}.";
            }
            case "withdraw":
            {
               Logic.Edicts.Edict e = args.Count > 1 ? b.Ledger.InForce.FirstOrDefault(x => x.Id == args[1]) : null;

               return b.Withdraw(e, "cr.edict") ? $"cr.edict: {e.Id} withdrawn." : "cr.edict withdraw <id>: no edict in force by that id.";
            }
            case "day":
               b.Daily(today);

               return "cr.edict: one day of edicts run (ends, grievances; the world's lords only on a seventh day with the MCM switch on).";
            case "world":
               b.WorldDecrees(today);

               return "cr.edict: the world's lords weighed their towns once (MCM switch not read).";
         }

         var lines = new List<string> {$"cr.edict (world's lords {(ModSettings.EnableNpcEdicts ? "ON" : "OFF")}, today day {today}):"};
         foreach (Logic.Edicts.Edict e in b.Ledger.Edicts.OrderByDescending(x => x.EnactedDay))
         {
            string where = e.Scope == EdictScope.Realm ? $"realm {e.TargetId}" : Settlement.Find(e.TargetId)?.Name?.ToString() ?? e.TargetId;
            string grief = e.Grievance.Count == 0 ? "" : $", grievance {string.Join(" ", e.Grievance.Select(g => $"{g.Key}:{g.Value}"))}";
            lines.Add($"  {e.Id} {e.Status} '{e.Name}' ({e.Effect.Key}) on {where} by {e.EnactorId}{(e.ByPlayer ? " (player)" : "")}, day {e.EnactedDay} to {e.EndDay}"
                      + $"{grief}{(e.ContestedIn.Count > 0 ? ", contested in " + string.Join(" ", e.ContestedIn) : "")}{(e.RepealedIn.Count > 0 ? ", forced off in " + string.Join(" ", e.RepealedIn) : "")}");
         }

         if (lines.Count == 1) lines.Add("  none decreed.");

         return string.Join("\n", lines);
      }
   }
}
