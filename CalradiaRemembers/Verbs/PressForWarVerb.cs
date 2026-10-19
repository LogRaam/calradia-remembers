// Code written by Gabriel Mailhot, 18/10/2026.
// press_for_war: Gabriel (18/10/2026), "je veux aussi ajouter le fait qu'on puisse influencer en chat pour la guerre".
// Any lord may be persuaded; his house then files the game's own war proposal (a kingdom decision the clans vote on
// and the ruler decides), as the game's own houses do, so the mod never declares a war (his rule of 02/07/2026: only
// the ruler declares, a lord may only press). The cost is "le risque que la faction visee apprenne que le joueur parle
// contre eux": the talk is court gossip, and a lord of the target realm it reaches keeps a hidden grudge. The player
// sees nothing special, beyond a war more likely to break out.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   press_for_war: a lord, truly persuaded by the player, resolves to press his realm's court toward war against a
    ///   named realm. Taught only with the realms his house could really propose a war against (PressForWarPolicy), each
    ///   with how his house leans by the game's own support, so he never promises what the game would then refuse.
    /// </summary>
    public sealed class PressForWarVerb : IConversationVerb
    {
        /// <summary>The most realms listed to the lord, nearest the war his house would welcome first.</summary>
        private const int MaxRealmsTaught = 6;

        public string Name => "press_for_war";

        public bool IsEligible(VerbContext ctx) => Targets(ctx?.Npc).Count > 0;

        public string TeachingText(VerbContext ctx)
        {
            List<(Kingdom Realm, HouseLeaning Leaning)> targets = Targets(ctx?.Npc);
            if (targets.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("PRESS_FOR_WAR, CARRYING THE CASE FOR WAR TO YOUR REALM'S COURT, AT THE PLAYER'S WORD:");
            sb.AppendLine("If the player argues that your realm should make war on another, and the case truly persuades you,");
            sb.AppendLine("you may resolve to carry it to your realm's court: your house will put the war to the realm's vote,");
            sb.AppendLine("at the cost of its own influence. The houses vote and the ruler decides; you cannot know the outcome,");
            sb.AppendLine("so never say the war is declared or certain. Emit it ONLY when you are truly persuaded, in character,");
            sb.AppendLine("never to please the player. The realms your house could press war against now:");
            foreach ((Kingdom realm, HouseLeaning leaning) in targets)
                sb.AppendLine($"- {realm.Name}: {PressForWarPolicy.LeaningWords(leaning)}.");
            sb.AppendLine("A house against the war is hard to move; a torn one may be won by a strong case.");
            sb.AppendLine("If you truly resolve to press it, emit the [ACTION] block this turn:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: press_for_war");
            sb.AppendLine("target: the realm's name, as listed above");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                Hero npc = ctx?.Npc;
                NpcProfile profile = ctx?.Profile;
                if (npc?.Clan == null || profile == null) return null;

                action.Parameters.TryGetValue("target", out string targetName);
                Kingdom target = ResolveRealm(targetName);
                if (target == null) return ActionOutcome.Refused("(No realm by that name is known.)");

                string why = PressForWarPolicy.WhyNot(Facts(npc.Clan, target));
                if (why != null)
                {
                    ModLog.Info($"[WARPRESS] press_for_war refused: {npc.Name} against {target.Name}: {why}.");

                    return ActionOutcome.Refused($"({npc.Name} cannot carry a war against {target.Name} to court now: {why}.)");
                }

                if (!PressForWarPolicy.PlayerSpokeOfRealm(ctx.ActionContext?.PlayerWordsThisConversation, NamesOf(target)))
                {
                    ModLog.Info($"[WARPRESS] press_for_war refused: the player never spoke of {target.Name} in this conversation.");

                    return ActionOutcome.Refused($"(Nothing was said of {target.Name} in this conversation; {npc.Name} resolves nothing.)");
                }

                var today = (int) CampaignTime.Now.ToDays;
                Press(npc, target, today);
                string player = Hero.MainHero?.Name?.ToString() ?? "the player";
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"At {player}'s word, resolved to press {npc.MapFaction?.Name} toward war on {target.Name} at court."));

                return new ActionOutcome($"{npc.Name} resolves to carry the case for war on {target.Name} to court.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[WARPRESS] press_for_war failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        ///   The lord's house is now pressing: the proposal waits for him to reach court, and the talk goes out as court
        ///   gossip, watched for the target realm's ruler and house heads (the risk Gabriel named, through the same
        ///   delayed reveal sway_opinion uses).
        /// </summary>
        internal static void Press(Hero npc, Kingdom target, int today)
        {
            PressForWarBehavior.Instance?.Queue(npc, target, PressForWarPolicy.DueDay(today));

            string player = Hero.MainHero?.Name?.ToString() ?? "the player";
            var rumour = new WorldEvent {
                Type = WorldEventType.WarLooming,
                Day = today,
                PlaceId = npc.CurrentSettlement?.StringId,
                InvolvesPlayer = true,
                Magnitude = PressForWarPolicy.RumourMagnitude,
                Summary = $"{npc.Name} was heard urging war on {target.Name}, at {player}'s word.",
                ActorIds = new List<string> {npc.StringId, Hero.MainHero?.StringId}.Where(id => id != null).ToList(),
                FactionIds = npc.MapFaction != null ? new List<string> {npc.MapFaction.StringId} : new List<string>()
            };
            WorldEventStore.Instance?.Record(rumour);
            DebugLastRumourId = rumour.Id;

            string cause = $"Word reached them that {player} had urged {npc.Name} to press {npc.MapFaction?.Name} into war against their realm.";
            foreach (Hero head in target.Clans.Where(c => c != null && !c.IsEliminated).Select(c => c.Leader).Where(h => h != null && h != Hero.MainHero && Mapping.HeroPresence.IsInTheWorld(h)))
                SwayExposureBehavior.Instance?.QueueExposure(head, rumour.Id, PressForWarPolicy.ExposureMagnitude, today, cause: cause);

            ModLog.Info($"[WARPRESS] {npc.Name} ({npc.Clan?.Name}) will press {npc.MapFaction?.Name} toward war on {target.Name} on day {PressForWarPolicy.DueDay(today)}.");
        }

        /// <summary>cr.testall seam: the gossip the last pressing recorded, so a test can find and remove it.</summary>
        internal static string DebugLastRumourId { get; private set; }

        /// <summary>The realms this lord's house could press a war against now, with its leaning, the most welcome first.</summary>
        internal static List<(Kingdom Realm, HouseLeaning Leaning)> Targets(Hero npc)
        {
            var none = new List<(Kingdom, HouseLeaning)>();
            if (npc is not {IsLord: true} || !Mapping.HeroPresence.IsInTheWorld(npc) || npc.Clan?.Kingdom == null || npc == Hero.MainHero) return none;

            return Kingdom.All.Where(k => k != null && PressForWarPolicy.WhyNot(Facts(npc.Clan, k)) == null)
                          .Select(k => (k, Leaning(npc.Clan, k)))
                          .OrderByDescending(t => (int) t.Item2)
                          .Take(MaxRealmsTaught)
                          .ToList();
        }

        /// <summary>The game's facts for this house pressing its realm against that one.</summary>
        internal static PressForWarFacts Facts(Clan house, Kingdom target)
        {
            Kingdom realm = house?.Kingdom;
            bool another = realm != null && target != null && target != realm && !target.IsEliminated;

            return new PressForWarFacts {
                HouseInARealm = realm != null,
                IsPlayersHouse = house == Clan.PlayerClan,
                TargetIsAnotherRealm = another,
                AlreadyAtWar = another && FactionManager.IsAtWarAgainstFaction(realm, target),
                Allied = another && realm.IsAllyWith(target),
                DaysSincePeace = another ? (int) realm.GetStanceWith(target).PeaceDeclarationDate.ElapsedDaysUntilNow : null,
                WarProposalPending = realm?.UnresolvedDecisions.Any(d => d is DeclareWarDecision) == true,
                HouseAlreadyPressing = PressForWarBehavior.Instance?.IsPressing(house?.StringId) == true,
                HouseInfluence = house?.Influence ?? 0f,
                ProposalCost = house == null ? int.MaxValue : Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingWar(house)
            };
        }

        /// <summary>How the house leans on the war, by the game's own support for it.</summary>
        internal static HouseLeaning Leaning(Clan house, Kingdom target)
        {
            try { return PressForWarPolicy.Leaning(new DeclareWarDecision(house, target).CalculateSupport(house)); }
            catch { return HouseLeaning.Torn; }
        }

        private static IEnumerable<string> NamesOf(Kingdom k)
            => new[] {k?.Name?.ToString(), k?.Culture?.Name?.ToString(), k?.Leader?.Name?.ToString()};

        private static Kingdom ResolveRealm(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string n = name.Trim();

            return Kingdom.All.FirstOrDefault(k => !k.IsEliminated && string.Equals(k.Name?.ToString(), n, StringComparison.OrdinalIgnoreCase))
                   ?? Kingdom.All.FirstOrDefault(k => !k.IsEliminated && k.Name != null
                                                      && (n.IndexOf(k.Name.ToString(), StringComparison.OrdinalIgnoreCase) >= 0
                                                          || k.Name.ToString().IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0));
        }
    }

    /// <summary>
    ///   The pressings under way: a persuaded lord's house files the game's own war proposal when he reaches court, if
    ///   the game still allows it then (a peace made meanwhile, another proposal, an emptied treasury of influence each
    ///   drop it, logged). Saved under an additive key; an old save starts with none.
    /// </summary>
    public sealed class PressForWarBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_PressForWar_v1bytes";

        public static PressForWarBehavior Instance;

        private List<PendingPress> _pending = new();

        public PressForWarBehavior()
        {
            Instance = this;
        }

        /// <summary>One house's pressing, waiting for its lord to reach court.</summary>
        public sealed class PendingPress
        {
            public string ClanId { get; set; }
            public string PresserId { get; set; }
            public string TargetId { get; set; }
            public int DueDay { get; set; }
        }

        public IReadOnlyList<PendingPress> Pending => _pending;

        public bool IsPressing(string clanId) => !string.IsNullOrEmpty(clanId) && _pending.Any(p => p.ClanId == clanId);

        public void Queue(Hero presser, Kingdom target, int dueDay)
        {
            if (presser?.Clan == null || target == null || IsPressing(presser.Clan.StringId)) return;
            _pending.Add(new PendingPress {ClanId = presser.Clan.StringId, PresserId = presser.StringId, TargetId = target.StringId, DueDay = dueDay});
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () => SafeRun.Step("PressForWar.Daily", () => Daily((int) CampaignTime.Now.ToDays)));
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(new List<PendingPress>(_pending))); }
                catch (Exception ex)
                {
                    ModLog.Error($"[WARPRESS] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[WARPRESS] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // A save written before this behavior: none pending.
                }

                try { _pending = bytes is {Length: > 0} ? Newtonsoft.Json.JsonConvert.DeserializeObject<List<PendingPress>>(Encoding.UTF8.GetString(bytes)) ?? new List<PendingPress>() : new List<PendingPress>(); }
                catch (Exception ex)
                {
                    _pending = new List<PendingPress>();
                    ModLog.Error($"[WARPRESS] load failed, pressings dropped for this load. {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        ///   The decision this pressing would file now, or null with the reason: the same checks the daily step runs, without
        ///   filing anything (an AI realm's vote resolves the moment a decision is added).
        /// </summary>
        internal DeclareWarDecision Prepare(PendingPress p, out string whyNot)
        {
            Clan house = Clan.All.FirstOrDefault(c => c.StringId == p?.ClanId);
            Kingdom target = Kingdom.All.FirstOrDefault(k => k.StringId == p?.TargetId);
            Hero presser = Hero.FindFirst(h => h.StringId == p?.PresserId);
            if (house == null || target == null) { whyNot = "the house or the realm is gone"; return null; }
            if (presser == null || !Mapping.HeroPresence.IsInTheWorld(presser) || presser.Clan != house) { whyNot = "the lord who meant to press it is no longer of the house"; return null; }

            // Its own pending entry is not "another pressing".
            PressForWarFacts f = PressForWarVerb.Facts(house, target);
            whyNot = PressForWarPolicy.WhyNot(new PressForWarFacts {
                HouseInARealm = f.HouseInARealm, IsPlayersHouse = f.IsPlayersHouse, TargetIsAnotherRealm = f.TargetIsAnotherRealm,
                AlreadyAtWar = f.AlreadyAtWar, Allied = f.Allied, DaysSincePeace = f.DaysSincePeace, WarProposalPending = f.WarProposalPending,
                HouseAlreadyPressing = false, HouseInfluence = f.HouseInfluence, ProposalCost = f.ProposalCost
            });
            if (whyNot != null) return null;

            var decision = new DeclareWarDecision(house, target);
            if (!decision.IsAllowed()) { whyNot = "the game does not allow that war now"; return null; }

            return decision;
        }

        /// <summary>cr.testall seam: forgets this house's pressing, filing nothing.</summary>
        internal void DebugForget(string clanId) => _pending.RemoveAll(p => p.ClanId == clanId);

        private void Daily(int today)
        {
            foreach (PendingPress p in _pending.Where(p => p.DueDay <= today).ToList())
            {
                _pending.Remove(p);
                DeclareWarDecision decision = Prepare(p, out string whyNot);
                if (decision == null)
                {
                    ModLog.Info($"[WARPRESS] {p.ClanId}'s pressing against {p.TargetId} dropped: {whyNot}.");

                    continue;
                }

                // The game's own proposal: the house pays its influence, the clans vote, the ruler decides.
                decision.Kingdom.AddDecision(decision);
                ModLog.Info($"[WARPRESS] {decision.ProposerClan.Name} proposes war on {decision.FactionToDeclareWarOn.Name} in {decision.Kingdom.Name}.");
            }
        }
    }
}
