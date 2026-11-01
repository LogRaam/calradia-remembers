// Code written by Gabriel Mailhot, 15/10/2026.
// cr.war_duty: what the vassal war duty sees today (Maifailun, Gabriel, 15/10/2026). Read-only.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using TaleWorlds.CampaignSystem;
using CalradiaRemembers.Logic.WarDuty;
using CalradiaRemembers.WarDuty;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.help_pleas: the calls for help open, and when each writer last asked.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("help_pleas", "cr")]
      public static string HelpPleasStatus(List<string> args)
      {
         HelpPleas.HelpPleaBehavior behavior = HelpPleas.HelpPleaBehavior.Instance;
         if (behavior == null) return "cr.help_pleas: no campaign loaded.";

         string open = behavior.Ledger.Open.Count == 0
            ? "  none open"
            : string.Join("\n", behavior.Ledger.Open.Select(p => $"  {p.Id}: {p.SenderId}, {p.Crisis}, asks {p.Ask} {p.Amount} {p.PlaceName}, reached day {p.ReceivedOnDay?.ToString() ?? "not yet"}, deadline {p.DeadlineDay?.ToString() ?? "none"}, answer {p.Answer}"));

         return $"cr.help_pleas (today day {HelpPleas.HelpPleaBehavior.Today}, unasked letters {(ModSettings.UnaskedLetters ? "ON" : "OFF")}):\n{open}\n  writers who have asked: {behavior.Ledger.LastPleaDay.Count}";
      }

      /// <summary>cr.raid_restraint: what each lord drew in the wars against the player.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("raid_restraint", "cr")]
      public static string RaidRestraintStatus(List<string> args)
      {
         Stance.RaidRestraintBehavior behavior = Stance.RaidRestraintBehavior.Instance;
         if (behavior == null) return "cr.raid_restraint: no campaign loaded.";

         string draws = behavior.Ledger.Draws.Count == 0
            ? "  no draw yet (friends and kin draw at the next daily tick, once you hold a village)"
            : string.Join("\n", behavior.Ledger.Draws.Select(d => $"  {d.HeroId} ({d.FactionId}), day {d.Day}: {(d.IsSparing ? "spares your villages" : d.Spares ? $"no longer spares them: {d.BrokenBecause}" : "drew to raid")}{(d.PlayerWasTold ? ", wrote to you" : "")}"));

         return $"cr.raid_restraint ({(ModSettings.RaidRestraint ? "ON" : "OFF")}):\n{draws}";
      }

      /// <summary>cr.help_plea &lt;name&gt; [coin|food]: a chosen character writes to the player now, for coin by default.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("help_plea", "cr")]
      public static string HelpPleaForce(List<string> args)
      {
         HelpPleas.HelpPleaBehavior behavior = HelpPleas.HelpPleaBehavior.Instance;
         if (behavior == null) return "cr.help_plea: no campaign loaded.";
         if (args == null || args.Count == 0) return "Usage: cr.help_plea <name> [coin|food]";

         string name = args[0];
         Hero writer = Hero.FindFirst(h => !h.IsDead && h.Name.ToString().IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0);
         if (writer == null) return $"cr.help_plea: nobody alive matches '{name}'.";

         bool food = args.Count > 1 && args[1] == "food";
         Logic.HelpPleas.HelpCrisis crisis = food ? Logic.HelpPleas.HelpCrisis.Starving : Logic.HelpPleas.HelpCrisis.Ruined;
         int today = HelpPleas.HelpPleaBehavior.Today;
         int amount = food ? Logic.HelpPleas.HelpPleaPolicy.FoodAsked : System.Math.Max(Logic.HelpPleas.HelpPleaPolicy.CoinAsked(Hero.MainHero.Gold), Logic.HelpPleas.HelpPleaPolicy.MinCoin);
         TaleWorlds.CampaignSystem.Settlements.Settlement place = writer.Clan?.Settlements.FirstOrDefault(s => s.IsFortification);
         if (food && place == null) return $"cr.help_plea: {writer.Name}'s house holds no town or castle to feed.";

         Logic.HelpPleas.HelpPlea plea = behavior.Open(writer, crisis, writer.Clan, place, null, amount, $"console:{writer.StringId}:{today}", today);

         return $"cr.help_plea: {writer.Name} writes ({plea.Id}, {plea.Ask} {plea.Amount}). The letter takes its usual road.";
      }

      /// <summary>cr.war_duty: the oath, the realm's wars, the last service, the verdict today and the reckoning in progress.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("war_duty", "cr")]
      public static string WarDutyStatus(List<string> args)
      {
         WarDutyBehavior behavior = WarDutyBehavior.Instance;
         if (behavior == null) return "cr.war_duty: no campaign loaded.";

         int today = WarDutyBehavior.Today;
         WarServiceLedger l = behavior.Ledger;
         WarDutyFacts facts = l.Facts(today, WarDutyBehavior.Live());
         WarDutyCase c = l.Case;

         string wars = l.WarSeenSince.Count == 0 ? "none" : string.Join(", ", l.WarSeenSince.Select(w => $"{w.Key} since day {w.Value}"));
         string reckoning = c == null
            ? "none"
            : $"{c.Stage}, ruler {c.RulerId}, opened day {c.OpenedOnDay}, deadline day {c.DeadlineDay}, levy {c.LevyPaid}/{c.LevyAmount}, appeared {c.Appeared}";

         return $"cr.war_duty (MCM {(ModSettings.EnableWarDuty ? "ON" : "OFF")}, window {ModSettings.WarDutyWindowDays} days, today day {today}):\n"
              + $"  sworn to: {l.SwornKingdomId ?? "none"} since day {l.SwornSinceDay}\n"
              + $"  wars: {wars}\n"
              + $"  last served: {(l.LastServedDay.HasValue ? "day " + l.LastServedDay.Value : "never seen")}; last called: {(l.LastCalledDay.HasValue ? "day " + l.LastCalledDay.Value : "never")}\n"
              + $"  today: {WarDutyPolicy.Decide(facts)}\n"
              + $"  reckoning: {reckoning}";
      }
   }
}
