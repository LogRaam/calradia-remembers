// Code written by Gabriel Mailhot, 02/07/2026.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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

      /// <summary>
      ///   cr.give_troops [name] — forces give_troops (04/07/2026, the reciprocal of lend_troops) through
      ///   the REAL bridge.ExecuteAction path (the same one a conversation uses), so eligibility is
      ///   genuinely re-validated. Reports the underlying facts (relation, the NPC party's strength against
      ///   the weakened threshold, the player's own spare capacity) first, so a refusal is diagnosable
      ///   without guessing which gate failed.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("give_troops", "cr")]
      public static string ForceGiveTroops(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero npc = ResolveHero(args);
         if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
         if (npc == Hero.MainHero) return "Pick an NPC, not the player.";

         NpcProfile profile = GetOrCreateProfile(behavior, npc);

         var sb = new StringBuilder();
         int relation = npc.GetRelation(Hero.MainHero);
         sb.AppendLine($"{npc.Name} — give_troops: relation={relation} (needs >= {CourtActionPolicy.GiveTroopsMinRelation})");

         MobileParty npcParty = npc.PartyBelongedTo;
         if (npcParty != null && npcParty.LeaderHero == npc)
         {
            int npcHealthy = CourtActionResolver.HealthyRegularTroopCount(npcParty.MemberRoster);
            int sizeLimit = npcParty.Party.PartySizeLimit;
            sb.AppendLine($"  {npc.Name}'s party: {npcHealthy}/{sizeLimit} healthy troops (weakened threshold: under " +
                          $"{CourtActionPolicy.GiveTroopsWeakenedFraction:P0} of a size limit >= {CourtActionPolicy.GiveTroopsMinPartySizeLimit})");
         }
         else
         {
            sb.AppendLine($"  {npc.Name} does not lead their own party.");
         }

         MobileParty playerParty = MobileParty.MainParty;
         if (playerParty != null)
         {
            int playerHealthy = CourtActionResolver.HealthyRegularTroopCount(playerParty.MemberRoster);
            sb.AppendLine($"  Your party: {playerHealthy} healthy troops (must keep >= {CourtActionPolicy.GiveTroopsPlayerFloor} and >= " +
                          $"{CourtActionPolicy.GiveTroopsPlayerMinRetainFraction:P0} of the total)");
         }

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "give_troops"}, npc, new ActionContext(profile, null));

         sb.AppendLine(outcome == null
            ? "give_troops not eligible right now (see the facts above)."
            : outcome.Message);

         return sb.ToString();
      }

      /// <summary>
      ///   cr.spend_influence [name] — forces spend_influence (04/07/2026, the reciprocal of give_influence)
      ///   through the REAL bridge.ExecuteAction path, reporting the underlying facts (relation, the NPC
      ///   clan's influence against the need threshold, the player clan's own reserve) so a refusal is
      ///   diagnosable without guessing which gate failed.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("spend_influence", "cr")]
      public static string ForceSpendInfluence(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero npc = ResolveHero(args);
         if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
         if (npc == Hero.MainHero) return "Pick an NPC, not the player.";

         NpcProfile profile = GetOrCreateProfile(behavior, npc);

         var sb = new StringBuilder();
         int relation = npc.GetRelation(Hero.MainHero);
         sb.AppendLine($"{npc.Name} — spend_influence: relation={relation} (needs >= {CourtActionPolicy.SpendInfluenceMinRelation})");
         sb.AppendLine(npc.Clan != null
            ? $"  {npc.Name}'s clan influence: {npc.Clan.Influence:0} (needs < {CourtActionPolicy.SpendInfluenceNpcNeedThreshold} to count as needy)"
            : $"  {npc.Name} has no clan.");
         sb.AppendLine($"  Your clan influence: {Clan.PlayerClan.Influence:0} (must retain {CourtActionPolicy.InfluenceBufferMultiplier}x the grant in reserve)");

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "spend_influence"}, npc, new ActionContext(profile, null));

         sb.AppendLine(outcome == null
            ? "spend_influence not eligible right now (see the facts above)."
            : outcome.Message);

         return sb.ToString();
      }
   }
}
