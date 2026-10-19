// Code written by Gabriel Mailhot, 11/08/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.courtship_trial [status|start|pass|clear] [name]
      ///   Courtship trial (a marriage prospect who agrees to wed BUT first rides at the player's side to be
      ///   tested). Lets the whole 42-day arc be verified in seconds instead of waiting two in-game seasons.
      ///   - status:       list every tracked trial (start day, passed, whether they accompany the player now).
      ///   - start [name]: begin a trial for the named prospect today (as if they had just begun accompanying).
      ///   - pass [name]:  force-complete the trial NOW through the real evaluation path: latches the passed flag
      ///                   AND queues her marriage-acceptance letter, so the marriage gate opens on next speaking.
      ///   - clear [name]: forget any trial for the named prospect.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("courtship_trial", "cr")]
      public static string CourtshipTrial(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         CourtshipTrialBehavior trial = CourtshipTrialBehavior.Instance;
         if (trial == null) return "Courtship trial behavior is not active (start a campaign first).";

         string sub = args is {Count: > 0}
            ? args[0].Trim().ToLowerInvariant()
            : "status";

         if (sub == "status") return trial.DescribeStatus();

         Hero hero = ResolveHero(args.Skip(1));
         if (hero == null) return "No matching hero. Usage: cr.courtship_trial [status|start|pass|clear] [name].";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         var today = (int) CampaignTime.Now.ToDays;

         if (sub == "clear")
            return trial.DebugForget(hero.StringId)
               ? $"Cleared the courtship trial for {hero.Name}."
               : $"{hero.Name} had no tracked courtship trial.";

         if (sub == "start")
         {
            trial.DebugEvaluate(hero, profile, true, today);

            return $"Began a courtship trial for {hero.Name} on day {today} (term {CourtshipTrialPolicy.TrialDays} days). " +
                   $"MarriageBlockedBecause={Mapping.EncounterContextBuilder.ResolveMarriageBlockReason(hero, profile)}.";
         }

         if (sub == "pass")
         {
            // Re-seed the start day in the past through the real path, then complete it: this passes the trial AND
            // queues her acceptance letter exactly as the daily tick would, rather than merely flipping a flag.
            trial.DebugForget(hero.StringId);
            trial.DebugEvaluate(hero, profile, true, today - CourtshipTrialPolicy.TrialDays);
            bool passed = trial.DebugEvaluate(hero, profile, true, today);

            return passed
               ? $"{hero.Name} PASSED the courtship trial: she reaches out to accept. " +
                 $"MarriageBlockedBecause={Mapping.EncounterContextBuilder.ResolveMarriageBlockReason(hero, profile)} (None means the offer is open). " +
                 "Talk to her to seal it."
               : $"{hero.Name} did not pass: they are hard-blocked from marrying the player right now " +
                 $"(MarriageBlockedBecause={Mapping.EncounterContextBuilder.ResolveMarriageBlockReason(hero, profile)}).";
         }

         return "Usage: cr.courtship_trial [status|start|pass|clear] [name].";
      }
   }
}
