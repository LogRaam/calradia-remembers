// Code written by Gabriel Mailhot, 02/07/2026.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.rp_verbs [name]
      ///   RP with teeth, increment 1: reports which of the four court-action verbs (give_influence,
      ///   lend_troops, sway_opinion, release_prisoner) are currently eligible for this NPC, and the
      ///   amounts/lists <see cref="CourtActionResolver" /> would teach the LLM this conversation.
      ///   Diagnostic only — the same resolvers the prompt gating and the bridge use, mutates nothing.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("rp_verbs", "cr")]
      public static string ShowRpVerbs(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         var sb = new StringBuilder();
         sb.AppendLine($"{hero.Name} — RP-with-teeth verb eligibility:");

         sb.Append("  give_influence  : ");
         sb.AppendLine(CourtActionResolver.TryGiveInfluence(hero, profile, out int influence)
            ? $"ELIGIBLE — would grant {influence}"
            : "not eligible");

         sb.Append("  lend_troops     : ");
         sb.AppendLine(CourtActionResolver.TryLendTroops(hero, profile, out int troops)
            ? $"ELIGIBLE — would lend {troops}"
            : "not eligible");

         sb.Append("  sway_opinion    : ");
         sb.AppendLine(CourtActionResolver.CanTeachSway(hero, profile)
            ? "ELIGIBLE (always-on for a lord/notable with a profile; genuine persuasion is a prompt guard, not a game gate)"
            : "not eligible");

         List<Hero> held = CourtActionResolver.HeldPrisonersOf(hero);
         sb.Append("  release_prisoner: ");
         sb.AppendLine(held.Count > 0
            ? $"ELIGIBLE — holds {string.Join(", ", held.Select(h => h.Name.ToString()))}"
            : "not eligible (holds no hero prisoners)");

         return sb.ToString();
      }
   }
}
