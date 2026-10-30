// Code written by Gabriel Mailhot, 30/10/2026.
// cr.strategy: the balance of the realms, the one grown too strong, and the step each other realm would take against it
// (Gabriel, 29-30/10/2026: lords who play their realm's strategy through the game's own kingdom decisions).

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Strategy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.strategy [plan | run]: the strategists' view of the balance, and their weekly step on demand.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("strategy", "cr")]
      public static string Strategy(List<string> args)
      {
         LordStrategyBehavior b = LordStrategyBehavior.Instance;
         if (b == null || Campaign.Current == null) return "cr.strategy: no campaign loaded.";

         if (args.Count > 0 && args[0].ToLowerInvariant() == "aims")
         {
            Aims.StrategistAims.Weekly(LordStrategyBehavior.Today);
            List<Logic.Aims.StandingAim> held = Aims.AimsBehavior.Instance?.Book.All.Where(a => a.SourceMod == Aims.StrategistAims.Source).ToList() ?? new List<Logic.Aims.StandingAim>();
            var lines = new StringBuilder($"cr.strategy aims: {held.Count} standing (at most {Logic.Strategy.StrategistAimPolicy.MaxStandingAims}).\n");
            foreach (Logic.Aims.StandingAim a in held)
               lines.AppendLine($"- {Hero.FindFirst(h => h.StringId == a.NpcId)?.Name}: {a.MissionId}, raised {a.TimesRaised}x, until day {a.ExpiresOnDay}. {a.Want}");

            return lines.ToString();
         }

         if (args.Count > 0 && args[0].ToLowerInvariant() == "run")
         {
            LordStrategyBehavior.Proposal filed = b.Weekly(LordStrategyBehavior.Today);

            return filed == null
               ? "cr.strategy run: no realm dominates, or none could take a step (see cr.strategy plan)."
               : $"cr.strategy run: filed. {LordStrategyBehavior.News(filed)}";
         }

         Kingdom dominant = LordStrategyBehavior.DominantRealm();
         var sb = new StringBuilder();
         sb.AppendLine($"cr.strategy (MCM switch {(Foundation.ModSettings.EnableLordStrategy ? "ON" : "off")}):");
         foreach (Kingdom k in LordStrategyBehavior.Standing().OrderByDescending(k => k.CurrentTotalStrength))
            sb.AppendLine($"  {k.Name}: strength {k.CurrentTotalStrength:0}{(k == dominant ? "  <- dominates" : "")}");
         if (dominant == null) return sb.Append("No realm dominates: the strategists wait.").ToString();

         foreach (Kingdom realm in LordStrategyBehavior.Standing().Where(k => k != dominant))
         {
            string last = b.LastStepDay.TryGetValue(realm.StringId, out int day) ? $" (last step day {day})" : "";
            LordStrategyBehavior.Proposal p = LordStrategyBehavior.Prepare(realm, dominant, out string why);
            sb.AppendLine(p == null
               ? $"- {realm.Name}{last}: none, {why}."
               : $"- {realm.Name}{last}: {p.Move} by {p.House.Name} (support {p.Support:0}). {LordStrategyBehavior.News(p)}");
         }

         return sb.ToString();
      }
   }
}
