// Code written by Gabriel Mailhot, 03/07/2026.
// DIVORCE, Phase 1 (NPC case): console diagnostics for end_marriage / DivorceProcessBehavior. Mirrors the
// cr.bastard_* commands (cr.bastard_season -> cr.divorce_tick) and the cr.relation real-pipeline pattern
// (cr.divorce dispatches through the SAME bridge.ExecuteAction a conversation uses, not a reimplementation).

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.divorce_info [name] — eligibility (+ the first failing gate), the live facts DivorcePolicy
        ///   reasons over, and any active estrangement process for the NPC. Read-only.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("divorce_info", "cr")]
        public static string DivorceInfo(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            DivorceEligibilityFacts facts = DivorceEligibilityResolver.GatherFacts(npc);
            bool eligible = DivorcePolicy.IsEligible(facts, out string reason);

            var sb = new StringBuilder();
            sb.AppendLine($"{npc.Name} — end_marriage eligibility: {(eligible ? "ELIGIBLE" : $"refused ({reason})")}");
            sb.AppendLine($"  married={facts.NpcIsMarried} relationWithSpouse={facts.NpcRelationWithSpouse} (unhappy<= {DivorcePolicy.UnhappyThreshold}) " +
                          $"relationWithPlayer={facts.NpcRelationWithPlayer} (needs>= {DivorcePolicy.RequiredPlayerBond(new NpcMemoryService.Core.Standing.VanillaRelation(facts.NpcRelationWithSpouse))}, slides with misery" +
                          $"{(facts.NpcIsRomanticallyBoundToPlayer ? "; WAIVED: romantically bound to you" : "")})");
            sb.AppendLine($"  eitherPregnant={facts.EitherPregnant} eitherPrisoner={facts.EitherPrisoner} " +
                          $"leavingSpouse[clanLeader={facts.LeavingSpouseIsClanLeader} leadsParty={facts.LeavingSpouseLeadsParty} governs={facts.LeavingSpouseGoverns}]");

            DivorceProcess active = DivorceProcessBehavior.Instance?.FindActive(npc.StringId);
            sb.AppendLine(active == null
                ? "  no active estrangement process"
                : $"  ACTIVE process: startDay={active.StartDay} endDay={active.EndDay} cumulativeDelayDays={active.CumulativeDelayDays} state={active.State}");

            return sb.ToString();
        }

        /// <summary>
        ///   cr.divorce [name] — seeds an estrangement process by dispatching end_marriage through the
        ///   REAL bridge.ExecuteAction path (the same one a conversation uses), so eligibility is
        ///   genuinely re-validated. Use cr.divorce_info first to check the gates.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("divorce", "cr")]
        public static string ForceDivorce(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, npc);

            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
                new GameAction {Type = "end_marriage", Parameters = new Dictionary<string, string>()},
                npc, new ActionContext(profile, null));

            return outcome == null
                ? $"{npc.Name}: end_marriage not eligible right now (run cr.divorce_info to see why)."
                : outcome.Message;
        }

        /// <summary>cr.divorce_tick — runs one day's advance for every active estrangement process now (testing, skips waiting on the real daily tick).</summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("divorce_tick", "cr")]
        public static string DivorceTick(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (DivorceProcessBehavior.Instance == null) return "Divorce system not active.";

            DivorceProcessBehavior.Instance.DebugRunDailyAdvance();

            return "Ran one day's advance for every active estrangement process. Use cr.divorce_info <name> to see the result.";
        }

        /// <summary>
        ///   cr.divorce_setup [name] — stages a divorcible scenario on an NPC so the feature can be tested even
        ///   from a campaign where no one you know is unhappily wed: if the NPC is single they are married off to
        ///   a relocatable partner, then the two eligibility gates are forced (an unhappy marriage + a strong
        ///   bond with you). Does NOT start the estrangement; run cr.divorce afterward. Mutates the save.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("divorce_setup", "cr")]
        public static string DivorceSetup(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
            if (npc == Hero.MainHero) return "Pick an NPC, not the player (Phase 1 is the NPC case).";

            var sb = new StringBuilder();

            if (npc.Spouse == null)
            {
                Hero partner = FindTestSpouse(npc);
                if (partner == null) return $"{npc.Name}: found no unmarried adult lord/lady to wed them to for the test.";
                MarriageAction.Apply(npc, partner);
                sb.AppendLine($"Wed {npc.Name} to {partner.Name} (test marriage).");
            }
            else
            {
                sb.AppendLine($"{npc.Name} is already wed to {npc.Spouse.Name}.");
            }

            Hero spouse = npc.Spouse;
            int wantSpouse = DivorcePolicy.UnhappyThreshold - 5; // comfortably unhappy
            if (spouse != null && npc.GetRelation(spouse) > wantSpouse)
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, spouse, wantSpouse - npc.GetRelation(spouse), false);
            if (npc.GetRelation(Hero.MainHero) < DivorcePolicy.BondThreshold)
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, Hero.MainHero, DivorcePolicy.BondThreshold - npc.GetRelation(Hero.MainHero), false);

            sb.AppendLine($"Forced relations: with spouse {(spouse != null ? npc.GetRelation(spouse) : 0)}, with you {npc.GetRelation(Hero.MainHero)}.");

            DivorceEligibilityFacts facts = DivorceEligibilityResolver.GatherFacts(npc);
            bool eligible = DivorcePolicy.IsEligible(facts, out string reason);
            sb.AppendLine(eligible
                ? $"{npc.Name} is now ELIGIBLE. Run 'cr.divorce {npc.Name}' to begin the estrangement, then 'cr.divorce_tick' to advance the days."
                : $"Still refused: {reason}. The leaving spouse may lead a party or govern a settlement; try a different NPC.");

            return sb.ToString();
        }

        /// <summary>An unmarried, adult, non-leader lord/lady of the opposite sex to wed the test NPC to (same culture preferred).</summary>
        private static Hero FindTestSpouse(Hero npc)
            => Hero.AllAliveHeroes.FirstOrDefault(h => IsTestSpouseCandidate(h, npc) && h.Culture == npc.Culture)
               ?? Hero.AllAliveHeroes.FirstOrDefault(h => IsTestSpouseCandidate(h, npc));

        private static bool IsTestSpouseCandidate(Hero h, Hero npc)
            => h != null && h != npc && h != Hero.MainHero && h.Spouse == null && h.IsAlive && !h.IsChild
               && h.IsLord && h.Clan != null && !h.IsClanLeader && h.IsFemale != npc.IsFemale;
    }
}
