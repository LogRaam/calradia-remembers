// Code written by Gabriel Mailhot, 28/10/2026.
// The world's initiative as the director sees it (Gabriel, 28/10/2026: one conductor for whose turn it is to come to the
// player unasked): who is waiting at the door, who had a turn, the budget and the moment. How a player tunes it.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Initiative;
using CalradiaRemembers.Logic.Initiative;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.knocks: the world's initiative right now: the settings, the moment, who waits and who had a turn.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("knocks", "cr")]
      public static string Knocks(List<string> args)
      {
         WorldInitiativeBehavior director = WorldInitiativeBehavior.Instance;
         if (director == null || Campaign.Current == null) return "No campaign is running.";

         double now = CampaignTime.Now.ToHours;
         InitiativeSettings s = ModSettings.WorldInitiative;
         InitiativeMoment m = director.MomentAt(now);
         WorldInitiativeState st = director.State;
         double spent = st.Grants.Where(g => now - g.GrantedAtHour < s.WindowDays * 24.0).Sum(g => g.Cost);
         string H(double h) => h >= double.MaxValue / 2 ? "never" : $"{h:0}h ago";

         var sb = new StringBuilder();
         sb.AppendLine(ModSettings.WorldInitiativeOff
                          ? "The world's initiative is OFF: every system paces itself (MCM, The World's Initiative)."
                          : $"Budget {spent:0.#}/{s.PerWindow:0.#} over {s.WindowDays} days; at once {m.ActiveCount}/{s.AtOnce}; gap {s.MinGapHours:0}h; "
                            + $"settling {s.SettlingDelayHours:0}h; quiet after your own talk {s.QuietAfterOwnTalkHours:0}h; fair turn every {s.FairTurnDays} days.");
         sb.AppendLine($"Now: {(m.Busy ? "busy" : "free")}; settled {H(m.HoursSinceSettled)}; your own talk {H(m.HoursSinceOwnTalk)}.");
         if (director.LastDecision != null)
            sb.AppendLine($"Last asked: {director.LastAskedKey} -> {director.LastDecision.Verdict}"
                          + (director.LastDecision.Granted != null ? $" ({director.LastDecision.Granted.Key}, {director.LastDecision.Ground})" : ""));

         sb.AppendLine(st.Requests.Count == 0 ? "Nobody waits at the door." : "Waiting at the door:");
         foreach (InitiativeRequest r in st.Requests.OrderBy(r => r.Rank).ThenBy(r => r.FiledAtHour))
         {
            double asked = r.LastAskedAtHour > 0 ? r.LastAskedAtHour : r.FiledAtHour;
            bool live = now - asked <= s.StaleAfterHours;
            sb.AppendLine($"  {r.Key} [{r.Family}, {r.Rank}] waiting {(now - r.FiledAtHour) / 24.0:0.#}d, asked {now - asked:0}h ago"
                          + (live ? $", weight {WorldInitiativePolicy.DrawWeight(r, st.Grants, s, now):0.##}" : ", not asking now"));
         }

         sb.AppendLine(st.Grants.Count == 0 ? "No turn given yet." : "Last turns given:");
         foreach (InitiativeGrant g in st.Grants.OrderByDescending(g => g.GrantedAtHour).Take(10))
            sb.AppendLine($"  {(now - g.GrantedAtHour) / 24.0:0.#}d ago: {g.Key} [{g.Family}]{(g.Cost < 1 ? $", cost {g.Cost:0.#}" : "")}");

         return sb.ToString().TrimEnd();
      }

      /// <summary>
      ///   cr.awaited [name]: marks or unmarks a correspondence as awaited (the ribbon bookmark, Gabriel 28/10/2026): their
      ///   letters come before others' in the mailbox and press with the world's initiative. Until the ribbon is in the book.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("awaited", "cr")]
      public static string Awaited(List<string> args)
      {
         if (!TryContext(out Behaviors.NpcMemoryCampaignBehavior behavior, out string error)) return error;
         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcMemoryService.Core.Models.NpcProfile profile = GetOrCreateProfile(behavior, hero);
         profile.LettersAwaited = !profile.LettersAwaited;
         behavior.Store.Set(profile);

         return profile.LettersAwaited
            ? $"{hero.Name}'s letters are awaited: they come before others' (the budget still holds)."
            : $"{hero.Name}'s letters are no longer awaited.";
      }

      /// <summary>
      ///   cr.ribbon a|b: which ribbon drawing the letter book shows, while Gabriel compares the two (29/10/2026).
      ///   Session-only; takes effect the next time the book opens.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("ribbon", "cr")]
      public static string Ribbon(List<string> args)
      {
         string style = args != null && args.Count > 0 ? args[0].Trim().ToLowerInvariant() : "";
         if (style != "a" && style != "b")
            return $"The letter book shows ribbon '{Letters.LetterViewModel.RibbonStyle}'. Usage: cr.ribbon a|b (a: broad, gold-trimmed; b: narrow, gold clasp).";

         Letters.LetterViewModel.RibbonStyle = style;

         return $"The letter book will show ribbon '{style}' the next time it opens.";
      }
   }
}
