// Code written by Gabriel Mailhot, 22/10/2026.
// enact_edict: the player decrees an edict through one of his people (Gabriel, 21/10/2026: edicts on towns and realms,
// "ville et royaume"). Taught to a member of the player's house, the governor of one of his towns, or, when he rules, a
// lord of his realm: someone who would carry the decree out. What the edict does comes only from the closed catalogue;
// the player confirms it [Y/n], the question naming its effect, term and price, BEFORE it is proclaimed (gating before
// declaration), and the decree is checked whole by the same rules the world's lords obey.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Edicts;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Actions;
using CalradiaRemembers.Logic.Edicts;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>What the player decrees, read from the action and the world.</summary>
    internal sealed class EdictRequest
    {
        public EdictScope Scope { get; set; }
        public Settlement Town { get; set; }
        public EdictEffect Effect { get; set; }
        public int Days { get; set; }
        public string Name { get; set; }

        /// <summary>Where it holds, in words ("Pravend", "all the towns of Vlandia").</summary>
        public string Where { get; set; }
    }

    /// <summary>enact_edict: the player decrees an edict of the catalogue for one of his towns, or his realm, through this character.</summary>
    public sealed class EnactEdictVerb : IConversationVerb
    {
        public string Name => "enact_edict";

        public bool IsEligible(VerbContext ctx) => EdictBehavior.Instance != null && SpeaksForThePlayer(ctx?.Npc) && (PlayerTowns().Count > 0 || PlayerRealm() != null);

        public string TeachingText(VerbContext ctx)
        {
            if (!IsEligible(ctx)) return null;
            string player = Hero.MainHero.Name.ToString();
            List<Settlement> towns = PlayerTowns();
            Kingdom realm = PlayerRealm();

            var sb = new StringBuilder();
            sb.AppendLine("ENACT_EDICT, AN EDICT THE PLAYER DECREES THROUGH YOU:");
            string holds = towns.Count == 0 ? "" : $"{player} holds {string.Join(", ", towns.Select(t => t.Name.ToString()))}";
            string rules = realm == null ? "" : $"{(holds.Length > 0 ? " and rules" : $"{player} rules")} {realm.Name}";
            sb.AppendLine($"{holds}{rules}. If {player} plainly decrees an edict for one of these towns{(realm != null ? ", or for the whole realm" : "")}, you carry it out.");
            sb.AppendLine("Only these edicts exist; each buys its gain with a cost, and holds 30 to 90 days:");
            foreach (EdictEffect e in EdictCatalog.All) sb.AppendLine($"- {e.Key}: {e.Describe()}.");
            sb.AppendLine($"It costs {player} {EdictCatalog.TownInfluenceCost} influence for a town{(realm != null ? $", {EdictCatalog.RealmInfluenceCost} for the realm" : "")}; one edict per town at a time.");
            IEnumerable<string> inForce = towns.Select(t => EdictBehavior.Instance.HoldingOn(t.Town)).Where(e => e != null).Distinct()
                                               .Select(e => $"{e.Name} ({e.Effect().Key}, {e.DaysLeft(EdictBehavior.Today)} days left)");
            string held = string.Join("; ", inForce);
            if (held.Length > 0) sb.AppendLine($"In force now: {held}.");
            sb.AppendLine($"{player} is asked to confirm it before it is proclaimed: never say it is proclaimed or in force before then.");
            sb.AppendLine("If the player plainly decrees one, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: enact_edict");
            sb.AppendLine("kind: the edict's key, as listed above");
            sb.AppendLine($"town: the town's name{(realm != null ? ", or realm" : "")}");
            sb.AppendLine("days: how long, 30 to 90 (60 if not said)");
            sb.AppendLine("name: the name the player gave it, if any");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                Hero npc = ctx?.Npc;
                EdictRequest r = Read(action, npc, out string refusal);
                if (r == null)
                {
                    ModLog.Info($"[EDICT] enact_edict refused: {refusal}");

                    return ActionOutcome.Refused($"({refusal})");
                }

                Edict e = EdictBehavior.Instance.Decree(Hero.MainHero, r.Scope, r.Town, r.Effect.Kind, r.Days, r.Name, out string why);
                if (e == null) return ActionOutcome.Refused($"({why})");

                string player = Hero.MainHero.Name.ToString();
                ctx.Profile?.Events.Add(new NotableEvent(EdictBehavior.Today, NotableEventType.Collaboration,
                    $"{player} decreed {e.Name} for {r.Where} through me: {r.Effect.Describe()}, for {e.EndDay - e.EnactedDay} days."));

                return new ActionOutcome($"{e.Name} is decreed for {r.Where}: {r.Effect.Describe()}, for {e.EndDay - e.EnactedDay} days ({EdictCatalog.InfluenceCost(r.Scope)} influence spent).", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[EDICT] enact_edict failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        ///   The question the player answers before the edict is proclaimed, or null with the reason it would be refused
        ///   (then the action runs inline and the refusal is said). Shares <see cref="Read" /> with the executor, so what
        ///   the player says yes to is what is decreed.
        /// </summary>
        internal static string Question(GameAction action, Hero npc, out string refusal)
        {
            EdictRequest r = Read(action, npc, out refusal);

            return r == null
                ? null
                : ActionConfirmationPolicy.BuildEdictQuestion(new EdictConfirmationFacts {
                    EdictName = Named(action), Title = r.Effect.Title, Where = r.Where, Effects = r.Effect.Describe(),
                    Days = EdictCatalog.Term(r.Days), Influence = EdictCatalog.InfluenceCost(r.Scope)
                });
        }

        /// <summary>The decree the action asks for, checked whole; null with the reason when it cannot be made.</summary>
        internal static EdictRequest Read(GameAction action, Hero npc, out string refusal)
        {
            refusal = null;
            EdictBehavior edicts = EdictBehavior.Instance;
            if (edicts == null || Hero.MainHero == null) { refusal = "No edict can be decreed now."; return null; }
            if (!SpeaksForThePlayer(npc)) { refusal = $"{npc?.Name} cannot carry an edict of yours."; return null; }

            if (action?.Parameters == null) { refusal = "No edict was named."; return null; }
            action.Parameters.TryGetValue("kind", out string kindText);
            EdictEffect effect = EdictCatalog.Find(kindText);
            if (effect == null) { refusal = $"No edict by that name; the edicts are {string.Join(", ", EdictCatalog.All.Select(e => e.Key))}."; return null; }

            action.Parameters.TryGetValue("days", out string daysText);
            int.TryParse(daysText?.Trim().Split(' ')[0], out int days);
            action.Parameters.TryGetValue("town", out string townText);
            string where = townText?.Trim() ?? "";

            Kingdom realm = PlayerRealm();
            bool realmWide = realm != null && (where.Equals("realm", StringComparison.OrdinalIgnoreCase) || where.IndexOf(realm.Name.ToString(), StringComparison.OrdinalIgnoreCase) >= 0);
            Settlement town = realmWide ? null : TownFor(where, npc);
            if (!realmWide && town == null) { refusal = string.IsNullOrEmpty(where) ? "Which town is the edict for?" : $"{where} is no town of yours."; return null; }

            EdictScope scope = realmWide ? EdictScope.Realm : EdictScope.Town;
            string why = edicts.WhyNot(Hero.MainHero, scope, town);
            if (why != null) { refusal = why; return null; }

            return new EdictRequest {
                Scope = scope, Town = town, Effect = effect, Days = days, Name = Named(action),
                Where = realmWide ? $"all the towns of {realm.Name}" : town.Name.ToString()
            };
        }

        private static string Named(GameAction action)
            => action?.Parameters != null && action.Parameters.TryGetValue("name", out string n) && !string.IsNullOrWhiteSpace(n) ? n.Trim() : null;

        /// <summary>The player's town by name, else the one this character governs, else his only town.</summary>
        private static Settlement TownFor(string written, Hero npc)
        {
            List<Settlement> towns = PlayerTowns();
            if (!string.IsNullOrEmpty(written))
                return towns.FirstOrDefault(t => t.Name.ToString().Equals(written, StringComparison.OrdinalIgnoreCase))
                       ?? towns.FirstOrDefault(t => written.IndexOf(t.Name.ToString(), StringComparison.OrdinalIgnoreCase) >= 0);
            Settlement governed = npc?.GovernorOf?.Settlement;
            if (governed != null && towns.Contains(governed)) return governed;

            return towns.Count == 1 ? towns[0] : null;
        }

        /// <summary>The towns the player's house holds.</summary>
        internal static List<Settlement> PlayerTowns()
            => Clan.PlayerClan?.Fiefs?.Where(f => f.IsTown).Select(f => f.Settlement).ToList() ?? new List<Settlement>();

        /// <summary>The realm the player rules, or null.</summary>
        internal static Kingdom PlayerRealm() => Clan.PlayerClan?.Kingdom is Kingdom k && k.Leader == Hero.MainHero ? k : null;

        /// <summary>
        ///   Someone through whom the player decrees: of his house, the governor of one of his towns, or, when he rules, a
        ///   lord of his realm. Anyone else has no hand in his towns.
        /// </summary>
        internal static bool SpeaksForThePlayer(Hero npc)
        {
            if (npc == null || npc == Hero.MainHero) return false;
            if (npc.Clan != null && npc.Clan == Clan.PlayerClan) return true;
            if (npc.GovernorOf?.Settlement?.OwnerClan == Clan.PlayerClan) return true;

            return PlayerRealm() is Kingdom k && npc.IsLord && npc.Clan?.Kingdom == k;
        }
    }
}
