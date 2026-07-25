// Code written by Gabriel Mailhot, 25/07/2026.
// ASSEMBLY Inc 1, first slice: a console harness to summon an assembly and watch the RSVP resolve against the
// live world, before any menu, letter, or party movement is wired. It exercises the real seam
// (AssemblyInvitationFactsBuilder -> AssemblyRsvpPolicy) and the persisted state (AssemblyBehavior), so the
// convene logic can be validated in game while the outward-facing flow is built around it.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        private const int AssemblyDeadlineDays = 7;
        private const int AssemblyInviteeSampleCap = 8;

        /// <summary>
        ///   cr.assembly_summon [companions|lords|all]
        ///   Convenes an assembly at the current town/castle with the player as host, resolves each invitee's
        ///   RSVP through the real policy, and reports the roster with the reason and the resulting status.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("assembly_summon", "cr")]
        public static string AssemblySummon(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
            if (AssemblyBehavior.Instance == null) return "Assembly system not active (load a campaign first).";

            Hero player = Hero.MainHero;
            Settlement host = Settlement.CurrentSettlement;

            if (host == null) return "Stand inside a town or castle to summon an assembly (no host settlement here).";
            if (!host.IsTown && !host.IsCastle) return $"{host.Name} is not a town or castle; an assembly needs a hall.";

            string which = args?.FirstOrDefault()?.ToLowerInvariant() ?? "companions";
            List<Hero> invitees = PickInvitees(which, player);

            if (invitees.Count == 0) return $"No eligible invitees found for '{which}'. Try companions, lords, or all.";

            var today = (int) CampaignTime.Now.ToDays;
            AssemblyBehavior.Instance.Begin(host.StringId, player.StringId, today, today + AssemblyDeadlineDays,
                invitees.Select(h => h.StringId));

            var sb = new StringBuilder();
            sb.AppendLine($"Assembly summoned at {host.Name} ({invitees.Count} invited, deadline day {today + AssemblyDeadlineDays}):");
            foreach (Hero h in invitees)
            {
                AssemblyInvitationFacts facts = AssemblyInvitationFactsBuilder.Build(h, host, player);
                RsvpOutcome rsvp = AssemblyRsvpPolicy.Decide(facts);
                AssemblyInviteeStatus status = AssemblyConvener.InitialStatus(h, player, facts, rsvp);
                AssemblyBehavior.Instance.SetStatus(h.StringId, status);
                sb.AppendLine($"  {h.Name}: {rsvp.Decision} ({Describe(rsvp.Reason)}) -> {status}  [regard {facts.RegardToInitiator}, dist tier {facts.DistanceTier}]");
            }

            sb.Append("cr.assembly_status to review, cr.assembly_cancel to stand it down.");

            return sb.ToString();
        }

        /// <summary>cr.assembly_status: prints the convened roster and the present/en-route/declined/awaiting tally.</summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("assembly_status", "cr")]
        public static string AssemblyStatus(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

            AssemblyBehavior b = AssemblyBehavior.Instance;

            if (b == null || !b.HasActiveAssembly) return "No assembly is convened. Summon one with cr.assembly_summon.";

            AssemblyRecord rec = b.Pending;
            AssemblyProgress p = b.Progress();
            Settlement host = Settlement.Find(rec.HostSettlementId);

            var sb = new StringBuilder();
            sb.AppendLine($"Assembly at {host?.Name?.ToString() ?? rec.HostSettlementId} (deadline day {rec.Deadline}):");
            sb.AppendLine($"  Present {p.Present} | En route {p.InTransit} | Declined {p.Declined} | Awaiting {p.Awaiting} of {p.Total}.");
            foreach (AssemblyInviteeRecord i in rec.Invitees)
            {
                Hero h = Hero.FindFirst(x => x.StringId == i.HeroId);
                sb.AppendLine($"  {h?.Name?.ToString() ?? i.HeroId}: {i.Status}");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>cr.assembly_cancel: stands the pending assembly down (slice 1: clears the state).</summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("assembly_cancel", "cr")]
        public static string AssemblyCancel(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

            AssemblyBehavior b = AssemblyBehavior.Instance;

            if (b == null || !b.HasActiveAssembly) return "No assembly to cancel.";

            b.Cancel();

            return "Assembly stood down.";
        }

        private static List<Hero> PickInvitees(string which, Hero player)
        {
            IEnumerable<Hero> companions = Clan.PlayerClan?.Companions?.Where(h => h != null && h.IsAlive && !h.IsPrisoner)
                                           ?? Enumerable.Empty<Hero>();
            IEnumerable<Hero> lords = Hero.AllAliveHeroes.Where(h => h != null && h != player && h.IsLord && !h.IsPrisoner);

            IEnumerable<Hero> pool;
            switch (which)
            {
                case "lords":
                    pool = lords;
                    break;
                case "all":
                    pool = companions.Concat(lords);
                    break;
                default:
                    pool = companions;
                    break;
            }

            return pool.Distinct().Take(AssemblyInviteeSampleCap).ToList();
        }

        private static string Describe(RsvpReason reason)
        {
            switch (reason)
            {
                case RsvpReason.Accepted: return "accepts";
                case RsvpReason.DeclinedUnavailable: return "cannot come";
                case RsvpReason.DeclinedEnemyGround: return "enemy ground";
                case RsvpReason.DeclinedHostile: return "no goodwill";
                case RsvpReason.DeclinedNotWorthTravel: return "not worth the ride";
                default: return reason.ToString();
            }
        }
    }
}
