// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.scheme_advance
      ///   Advances every active scheme by one tick (no spawn) — to test the lifecycle without
      ///   waiting for daily ticks. Watch the result with cr.schemes.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme_advance", "cr")]
      public static string AdvanceSchemes(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         int before = store.Active.Count;
         store.DebugAdvance();

         return $"Advanced schemes one tick ({before} → {store.Active.Count} still active). See cr.schemes / CalradiaRemembers.log.";
      }

      /// <summary>
      ///   cr.intercept [grudge|appreciationowed|nemesisbounty|territorialpatrol|heardofdeed] [name]
      ///   Forces the named lord (with a party on the map) to ride out and intercept the player,
      ///   bypassing the eligibility roll, to test the pursuit without waiting for the daily tick. The
      ///   trigger defaults to HeardOfDeed when omitted (the original behaviour); for grudge/appreciationowed/
      ///   nemesisbounty, the lord's REAL grievance/kindness/eligible nemesis against the player is used when
      ///   one exists on file.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("intercept", "cr")]
      public static string ForceIntercept(List<string> args)
      {
         var behavior = InterceptionBehavior.Instance;

         if (behavior == null) return "Interception system is not active.";

         var trigger = InterceptionTrigger.HeardOfDeed;
         var nameTokens = new List<string>();
         foreach (string arg in args ?? new List<string>())
            if (Enum.TryParse(arg, true, out InterceptionTrigger parsed) && parsed != InterceptionTrigger.None) trigger = parsed;
            else nameTokens.Add(arg);

         Hero lord = ResolveHero(nameTokens);

         if (lord == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         return behavior.DebugForceIntercept(lord, trigger);
      }

      /// <summary>
      ///   cr.fief_audience [grudge|appreciationowed|nemesisbounty|heardofdeed] [name]
      ///   Forces the fief-audience "may I have a word" popup for the named partyless NPC standing in a
      ///   settlement (ladies, notables, a garrisoned lord with no field party), the settlement-entry
      ///   counterpart to cr.intercept, bypassing the pacing roll and the per-NPC cooldown to test the
      ///   Accept/Decline popup without waiting for a real entry. The trigger defaults to HeardOfDeed when
      ///   omitted; territorialpatrol is road-only (FiefAudiencePolicy.IsFiefValid) and is never fief-valid
      ///   however it is forced.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("fief_audience", "cr")]
      public static string ForceFiefAudience(List<string> args)
      {
         var behavior = FiefAudienceBehavior.Instance;

         if (behavior == null) return "Fief-audience system is not active.";

         var trigger = InterceptionTrigger.HeardOfDeed;
         var nameTokens = new List<string>();
         foreach (string arg in args ?? new List<string>())
            if (Enum.TryParse(arg, true, out InterceptionTrigger parsed) && parsed != InterceptionTrigger.None) trigger = parsed;
            else nameTokens.Add(arg);

         Hero npc = ResolveHero(nameTokens);

         return behavior.DebugForceOffer(npc, trigger);
      }

      /// <summary>
      ///   cr.scheme [name]
      ///   Force-plots a scheme: the named hero (or the current conversation NPC) plots against their
      ///   most-hated rival lord. Use cr.scheme_advance to step it, cr.schemes to watch.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme", "cr")]
      public static string ForceScheme(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         Hero plotter = ResolveHero(args);

         if (plotter == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Hero target = Hero.AllAliveHeroes
                           .Where(h => h != plotter && h != Hero.MainHero && h.IsLord)
                           .OrderBy(h => h.GetRelation(plotter))
                           .FirstOrDefault();

         if (target == null) return $"Found no rival lord for {plotter.Name} to plot against.";

         SchemeKind kind = MBRandom.RandomInt(2) == 0
            ? SchemeKind.Slander
            : SchemeKind.Sabotage;

         return store.ForcePlot(plotter, target, kind);
      }

      /// <summary>
      ///   cr.scheme_assist [name]
      ///   Force the player to abet the named plotter's secret scheme (bypasses the LLM). The plotter
      ///   must regard you enough and the plot must be secret — set relation first with cr.relation.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme_assist", "cr")]
      public static string ForceSchemeAssist(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         Hero plotter = ResolveHero(args);

         if (plotter == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         string result = store.PlayerAssists(plotter);

         return result ?? $"{plotter.Name} has no secret plot you are eligible to abet (regard >= 20, plot still secret, you not allied with the target).";
      }

      /// <summary>
      ///   cr.scheme_warn [name]
      ///   Force the player to warn the named hero of a secret plot against them (bypasses the LLM):
      ///   exposes the scheme so the target can counter it on later ticks.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme_warn", "cr")]
      public static string ForceSchemeWarn(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         Hero target = ResolveHero(args);

         if (target == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         string result = store.TargetHeeds(target);

         return result ?? $"There is no secret plot against {target.Name} to warn of.";
      }

      /// <summary>
      ///   cr.schemes
      ///   Lists the feud schemes currently in motion (plotter → target, kind, state, progress).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("schemes", "cr")]
      public static string ListSchemes(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         IReadOnlyList<Scheme> active = store.Active;

         if (active.Count == 0) return "No schemes are afoot.";

         var sb = new StringBuilder();
         sb.AppendLine($"{active.Count} scheme(s) afoot:");
         foreach (Scheme s in active)
         {
            string plotter = Hero.FindFirst(h => h.StringId == s.PlotterId)?.Name?.ToString() ?? s.PlotterId;
            string target = Hero.FindFirst(h => h.StringId == s.TargetId)?.Name?.ToString() ?? s.TargetId;
            sb.AppendLine($"  {plotter} → {target} | {s.Kind} | {s.State} | progress {s.Progress}");
         }

         return sb.ToString().TrimEnd();
      }
   }
}
