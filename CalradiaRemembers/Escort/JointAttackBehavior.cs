// Code written by Gabriel Mailhot, 25/11/2026.
// attack_together: one army of a lord's party and the player's, raised against an enemy within reach (JointAttackPolicy).
// froggyluv (Nexus, 25/11/2026): a lord planned at length how they would attack an enemy party together, and "once the
// conversation ended, she took off the other direction." Gabriel (25/11/2026): the lord who agrees raises an army of the
// two parties, the most powerful leads, and it attacks the right target (party, town, village, castle, ship).
//
// ENGINE (decompiled 1.5.7): Kingdom.CreateArmy(leader, focusSettlement, type) builds the army and, for an AI leader,
// Army.Gather -> FindBestGatheringSettlementAndMoveTheLeader sends him to a fortress NEAR the focus to wait for members:
// exactly "she took off the other direction". So right after creating it this overrides the army's objective and the
// leader's move order, and holds the leader's AI (SetDoNotMakeNewDecisions, the lock FollowMeBehavior uses):
// AiPartyThinkBehavior.PartyHourlyAiTick returns at once for a locked party, so no new decision (and no "disband, I have
// something else to do") overrides the march. Once the leader is engaged (a battle, a siege) the lock is released, so the
// engine's own siege and battle AI carry it through; if the enemy slips away the lock and the order come back.
// Army.MoveLeaderToGatheringLocationIfNeeded casts AiBehaviorObject to Settlement, so for a party target the objective is
// left null (a state the engine itself uses, FinishArmyObjective). Vanilla joins the player to a lord's army with
// MainParty.Army = army + AddPartyToMergedParties (LordConversationsCampaignBehavior.lord_considers_army_on_consequence),
// and a lord to the player's the same way; no influence is charged on either path for the player's party.
// The army is raised after the conversation closes (MainThreadDispatcher.EnqueueForCampaign), on the map, never inside a
// scene or a menu transition.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Escort;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Escort
{
    /// <summary>Raises, steers and ends the one army a lord forms with the player to attack a named enemy.</summary>
    public sealed class JointAttackBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_JointArmy_v1bytes";

        public static JointAttackBehavior Instance;

        private JointArmyRecord _active;

        public JointAttackBehavior()
        {
            Instance = this;
        }

        /// <summary>The lord's StringId while a joint army CR raised is in the field, else null.</summary>
        public string ActiveLordId => _active?.LordId;

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, () => SafeRun.Step("JointAttackBehavior.Hourly", Advance));
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(_active == null ? "" : JsonConvert.SerializeObject(_active, Formatting.None)); }
                catch (Exception ex)
                {
                    ModLog.Error($"[JOINT ARMY] serialize failed - persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = new byte[0];
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[JOINT ARMY] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // A save written before this behaviour existed: additive key, no army in the field.
                }

                _active = null;
                if (bytes is {Length: > 0})
                    try { _active = JsonConvert.DeserializeObject<JointArmyRecord>(Encoding.UTF8.GetString(bytes)); }
                    catch (Exception ex) { ModLog.Error($"[JOINT ARMY] load failed - no joint army carried over. {ex.GetType().Name}: {ex.Message}"); }
            }
        }

        // ── Facts, for the teaching gate, the confirmation and the bridge alike ──────────────────────────────

        /// <summary>The enemies within reach this lord and the player could march on, nearest first (JointAttackPolicy.Candidates).</summary>
        public static IReadOnlyList<JointAttackCandidate> CandidatesFor(Hero lord)
        {
            var all = new List<JointAttackCandidate>();
            try
            {
                Kingdom realm = lord?.Clan?.Kingdom;
                MobileParty main = MobileParty.MainParty;
                MobileParty his = lord?.PartyBelongedTo;
                if (realm == null || main == null) return all;

                Vec2 at = main.GetPosition2D;
                bool bothCanSail = main.HasNavalNavigationCapability && his != null && his.HasNavalNavigationCapability;

                foreach (MobileParty p in MobileParty.All)
                {
                    if (p == null || !p.IsActive || p == main || p == his || p.CurrentSettlement != null || p.IsGarrison || p.IsMilitia) continue;
                    if (p.Army != null && p.Army.LeaderParty != p) continue; // an army is named by its leader's party
                    if (!p.IsBandit && (p.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(p.MapFaction, realm))) continue;
                    float d = p.GetPosition2D.Distance(at);
                    if (d > JointAttackPolicy.ReachMapUnits) continue;
                    all.Add(new JointAttackCandidate {
                        Id = p.StringId, Name = p.Name?.ToString() ?? p.StringId, Kind = JointAttackTargetKind.Party,
                        AtSea = p.IsCurrentlyAtSea, BothCanSail = bothCanSail, Distance = d, Men = p.Army?.TotalManCount ?? p.MemberRoster?.TotalManCount ?? 0
                    });
                }

                foreach (Settlement s in Settlement.All)
                {
                    if (s == null || !(s.IsVillage || s.IsTown || s.IsCastle) || s.MapFaction == null) continue;
                    if (!FactionManager.IsAtWarAgainstFaction(s.MapFaction, realm)) continue;
                    if (s.IsVillage && (s.IsRaided || s.IsUnderRaid)) continue;
                    float d = s.GetPosition2D.Distance(at);
                    if (d > JointAttackPolicy.ReachMapUnits) continue;
                    all.Add(new JointAttackCandidate {
                        Id = s.StringId, Name = s.Name?.ToString() ?? s.StringId,
                        Kind = s.IsVillage ? JointAttackTargetKind.Village : JointAttackTargetKind.Fortress,
                        Distance = d, Men = s.IsVillage ? 0 : s.Town?.GarrisonParty?.MemberRoster?.TotalManCount ?? 0
                    });
                }
            }
            catch (Exception ex) { ModLog.Error($"[JOINT ARMY] reading the targets failed: {ex.GetType().Name}: {ex.Message}"); }

            return JointAttackPolicy.Candidates(all);
        }

        /// <summary>What deciding whether this lord can raise the army now rests on, read off the live campaign.</summary>
        public static JointAttackFacts FactsFor(Hero lord, IReadOnlyList<JointAttackCandidate> candidates)
        {
            MobileParty his = lord?.PartyBelongedTo;
            MobileParty main = MobileParty.MainParty;
            Kingdom realm = lord?.Clan?.Kingdom;

            return new JointAttackFacts {
                NpcLeadsOwnParty = his != null && his.LeaderHero == lord && Mapping.ActionGate.Allows(lord, Logic.ActionKind.Escort),
                SameRealm = realm != null && Clan.PlayerClan?.Kingdom == realm,
                NpcCommittedElsewhere = his?.Army != null || his?.SiegeEvent != null || his?.MapEvent != null,
                PlayerNotFree = Hero.MainHero == null || Hero.MainHero.IsPrisoner || main == null || main.Army != null || main.SiegeEvent != null || main.MapEvent != null,
                AnyTarget = candidates != null && candidates.Count > 0,
                Player = SideOf(Hero.MainHero, main, realm),
                Npc = SideOf(lord, his, realm)
            };
        }

        /// <summary>The Y/n question for this lord's attack_together, or null when the bridge would refuse it (it says why).</summary>
        public static string Question(Hero lord, string targetNamed)
        {
            IReadOnlyList<JointAttackCandidate> candidates = CandidatesFor(lord);
            JointAttackFacts facts = FactsFor(lord, candidates);
            if (!JointAttackPolicy.CanRaise(facts)) return null;
            JointAttackCandidate target = JointAttackPolicy.ResolveOrNearest(candidates, targetNamed);
            JointAttackLeader? leader = JointAttackPolicy.Leader(facts.Player, facts.Npc);

            return target == null || leader == null ? null : JointAttackPolicy.Question(leader.Value, lord.Name?.ToString(), target.Name);
        }

        // ── The bridge's door ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        ///   attack_together, after the player's yes: re-checks the gate the teaching used, then raises the army once the
        ///   conversation has closed. <paramref name="message" /> is what the player reads now, success or refusal.
        /// </summary>
        public bool TryRaise(Hero lord, string targetNamed, out string message)
        {
            IReadOnlyList<JointAttackCandidate> candidates = CandidatesFor(lord);
            JointAttackFacts facts = FactsFor(lord, candidates);
            JointAttackRefusal refusal = JointAttackPolicy.Refusal(facts);
            if (_active != null) refusal = JointAttackRefusal.PlayerNotFree;
            if (refusal != JointAttackRefusal.None)
            {
                message = _active != null ? "(You already march in a joint army.)" : JointAttackPolicy.Describe(refusal, lord?.Name?.ToString());

                return false;
            }

            JointAttackCandidate target = JointAttackPolicy.ResolveOrNearest(candidates, targetNamed);
            JointAttackLeader leader = JointAttackPolicy.Leader(facts.Player, facts.Npc) ?? JointAttackLeader.Npc;
            string lordId = lord.StringId;
            MainThreadDispatcher.EnqueueForCampaign(() => SafeRun.Step("JointAttackBehavior.Raise", () => Raise(lordId, target, leader)));
            ModLog.Info($"[JOINT ARMY] {lord.Name} agrees to attack {target.Name} ({target.Id}, {target.Kind}) with the player; {leader} leads; raised when the conversation closes.");
            message = leader == JointAttackLeader.Npc
                ? $"When you take your leave, you march in {lord.Name}'s army against {target.Name}."
                : $"When you take your leave, {lord.Name} joins your army against {target.Name}.";

            return true;
        }

        /// <summary>cr.test all seam: raises the army at once, the conversation closed or not. Null on success, else why not.</summary>
        internal string DebugRaise(Hero lord, out JointAttackCandidate target, out JointAttackLeader leader)
        {
            IReadOnlyList<JointAttackCandidate> candidates = CandidatesFor(lord);
            JointAttackFacts facts = FactsFor(lord, candidates);
            target = JointAttackPolicy.ResolveOrNearest(candidates, null);
            leader = JointAttackPolicy.Leader(facts.Player, facts.Npc) ?? JointAttackLeader.Npc;
            JointAttackRefusal refusal = JointAttackPolicy.Refusal(facts);
            if (refusal != JointAttackRefusal.None) return refusal.ToString();

            return Raise(lord.StringId, target, leader) ? null : "the army did not form";
        }

        /// <summary>cr.test all seam: ends the army CR raised, as the term's end would.</summary>
        internal void DebugEnd() => End(JointAttackEnd.TermOver, quiet: true);

        // ── Raising, steering, ending ────────────────────────────────────────────────────────────────────────

        private bool Raise(string lordId, JointAttackCandidate target, JointAttackLeader leader)
        {
            Hero lord = Hero.FindFirst(h => h.StringId == lordId);
            MobileParty his = lord?.PartyBelongedTo;
            MobileParty main = MobileParty.MainParty;
            Kingdom realm = lord?.Clan?.Kingdom;
            if (lord == null || his == null || main == null || realm == null || target == null || _active != null) return false;

            // Re-read at the moment of raising: minutes may have passed since the yes.
            JointAttackFacts facts = FactsFor(lord, CandidatesFor(lord));
            JointAttackRefusal refusal = JointAttackPolicy.Refusal(facts);
            if (refusal != JointAttackRefusal.None)
            {
                Notify(JointAttackPolicy.Describe(refusal, lord.Name?.ToString()));

                return false;
            }

            Settlement targetSettlement = Settlement.Find(target.Id);
            MobileParty targetParty = targetSettlement == null ? MobileParty.All.FirstOrDefault(p => p.StringId == target.Id) : null;
            if (targetSettlement == null && targetParty == null)
            {
                Notify($"The enemy {lord.Name} meant to attack can no longer be found.");

                return false;
            }

            // A lord escorting the player under follow_me would read the army as his liege's call and apologise: end it plainly first.
            if (FollowMeBehavior.Instance?.IsEscorting(lordId) == true) FollowMeBehavior.Instance.TryDismiss(lord, out _);

            Settlement focus = targetSettlement ?? NearestFortress(targetParty);
            Army.ArmyTypes type = target.Kind == JointAttackTargetKind.Fortress ? Army.ArmyTypes.Besieger
               : target.Kind == JointAttackTargetKind.Village ? Army.ArmyTypes.Raider : Army.ArmyTypes.Patrolling;

            Army army;
            if (leader == JointAttackLeader.Npc)
            {
                realm.CreateArmy(lord, focus, type);
                army = his.Army;
                if (army == null) return Failed(lord, "the army did not form");
                army.AiBehaviorObject = targetSettlement; // null for a party: the engine reads the objective as a settlement
                main.Army = army;
                army.AddPartyToMergedParties(main);
                if (Settlement.CurrentSettlement != null)
                    try { GameMenu.SwitchToMenu("army_wait_at_settlement"); }
                    catch (Exception ex) { ModLog.Error($"[JOINT ARMY] could not open the army's waiting menu: {ex.Message}"); }
            }
            else
            {
                realm.CreateArmy(Hero.MainHero, focus, type);
                army = main.Army;
                if (army == null) return Failed(lord, "the army did not form");
                his.Army = army;
                army.AddPartyToMergedParties(his);
            }

            _active = new JointArmyRecord {
                LordId = lordId, TargetId = target.Id, TargetName = target.Name, TargetKind = target.Kind,
                PlayerLeads = leader == JointAttackLeader.Player, RaisedDay = (int) CampaignTime.Now.ToDays
            };
            if (!_active.PlayerLeads) Steer(his, targetSettlement, targetParty);
            ModLog.Info($"[JOINT ARMY] raised: {(leader == JointAttackLeader.Npc ? lord.Name.ToString() : "the player")} leads {lord.Name} and the player against {target.Name} ({type}).");
            Notify(_active.PlayerLeads
                ? $"{lord.Name} joins your army. The target: {target.Name}."
                : $"You march in {lord.Name}'s army. The target: {target.Name}.");

            return true;
        }

        /// <summary>Hourly: ends the army on JointAttackPolicy.End, else keeps a lord-led army on its target.</summary>
        private void Advance()
        {
            if (_active == null) return;

            Hero lord = Hero.FindFirst(h => h.StringId == _active.LordId);
            MobileParty his = lord?.PartyBelongedTo;
            MobileParty main = MobileParty.MainParty;
            Army army = _active.PlayerLeads ? main?.Army : his?.Army;
            Kingdom realm = lord?.Clan?.Kingdom;
            Settlement targetSettlement = Settlement.Find(_active.TargetId);
            MobileParty targetParty = targetSettlement == null ? MobileParty.All.FirstOrDefault(p => p.StringId == _active.TargetId) : null;
            IFaction enemy = targetSettlement?.MapFaction ?? targetParty?.MapFaction;

            JointAttackEnd end = JointAttackPolicy.End(new JointAttackProgressFacts {
                ArmyIntact = army != null && his?.Army == army && main?.Army == army,
                TargetDone = TargetDone(targetSettlement, targetParty, realm),
                StillAtWar = targetParty?.IsBandit == true || (enemy != null && realm != null && FactionManager.IsAtWarAgainstFaction(enemy, realm)),
                BothStanding = lord != null && Mapping.HeroPresence.IsInTheWorld(lord) && !lord.IsPrisoner && Hero.MainHero != null && !Hero.MainHero.IsPrisoner,
                DaysSinceRaised = (int) CampaignTime.Now.ToDays - _active.RaisedDay
            });
            if (end != JointAttackEnd.None)
            {
                End(end, quiet: false);

                return;
            }

            if (_active.PlayerLeads || his == null) return;

            bool engaged = his.MapEvent != null || his.SiegeEvent != null || his.BesiegedSettlement != null;
            if (engaged && !_active.Engaged)
            {
                // The engine's own battle and siege AI carry it from here.
                _active.Engaged = true;
                his.Ai.SetDoNotMakeNewDecisions(false);
                ModLog.Info($"[JOINT ARMY] engaged at {_active.TargetName}; the game's own AI carries the fight.");
            }
            else if (!engaged)
            {
                _active.Engaged = false;
                Steer(his, targetSettlement, targetParty);
            }
        }

        private void End(JointAttackEnd end, bool quiet)
        {
            JointArmyRecord r = _active;
            _active = null;
            if (r == null) return;

            Hero lord = Hero.FindFirst(h => h.StringId == r.LordId);
            MobileParty his = lord?.PartyBelongedTo;
            try
            {
                if (his != null) his.Ai.SetDoNotMakeNewDecisions(false);
                if (r.PlayerLeads)
                {
                    if (his != null && his.Army != null && his.Army == MobileParty.MainParty?.Army) his.Army = null;
                }
                else if (his?.Army != null && his.Army.LeaderParty == his)
                {
                    DisbandArmyAction.ApplyByObjectiveFinished(his.Army);
                }
            }
            catch (Exception ex) { ModLog.Error($"[JOINT ARMY] ending failed: {ex.GetType().Name}: {ex.Message}"); }

            ModLog.Info($"[JOINT ARMY] ended ({end}): {lord?.Name} and the player against {r.TargetName}.");
            if (!quiet) Notify(JointAttackPolicy.DescribeEnd(end, lord?.Name?.ToString()));
        }

        /// <summary>Points a lord-led army's leader at the target and holds his AI there (released once engaged).</summary>
        private static void Steer(MobileParty leaderParty, Settlement targetSettlement, MobileParty targetParty)
        {
            try
            {
                leaderParty.Ai.SetDoNotMakeNewDecisions(false);
                if (targetParty != null)
                    leaderParty.SetMoveEngageParty(targetParty, targetParty.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default);
                else if (targetSettlement != null && targetSettlement.IsVillage)
                    leaderParty.SetMoveRaidSettlement(targetSettlement, MobileParty.NavigationType.Default, false);
                else if (targetSettlement != null)
                    leaderParty.SetMoveBesiegeSettlement(targetSettlement, MobileParty.NavigationType.Default);
                leaderParty.Ai.SetDoNotMakeNewDecisions(true);
            }
            catch (Exception ex) { ModLog.Error($"[JOINT ARMY] steering failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        private static bool TargetDone(Settlement s, MobileParty p, Kingdom realm)
        {
            if (s != null)
                return s.IsVillage ? s.IsRaided || (realm != null && s.MapFaction == realm) : realm != null && s.MapFaction == realm;

            return p == null || !p.IsActive;
        }

        private static Settlement NearestFortress(MobileParty p)
        {
            Vec2 at = p.GetPosition2D;

            return Settlement.All.Where(s => s.IsFortification).OrderBy(s => s.GetPosition2D.DistanceSquared(at)).FirstOrDefault();
        }

        private static JointAttackSide SideOf(Hero hero, MobileParty party, Kingdom realm)
            => new JointAttackSide {
                RulesTheRealm = realm != null && realm.Leader == hero,
                IsMercenary = hero?.Clan?.IsUnderMercenaryService == true,
                ClanTier = hero?.Clan?.Tier ?? 0,
                PartyStrength = party?.Party?.EstimatedStrength ?? 0f
            };

        private bool Failed(Hero lord, string why)
        {
            ModLog.Error($"[JOINT ARMY] {why} for {lord?.Name}.");
            Notify($"The army with {lord?.Name} could not be raised.");

            return false;
        }

        private static void Notify(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            try { InformationManager.DisplayMessage(new InformationMessage(message)); }
            catch (Exception ex) { ModLog.Error($"[JOINT ARMY] could not tell the player: {ex.Message}"); }
        }

        /// <summary>The joint army in the field, saved as JSON under an additive key.</summary>
        private sealed class JointArmyRecord
        {
            public string LordId { get; set; }
            public string TargetId { get; set; }
            public string TargetName { get; set; }
            public JointAttackTargetKind TargetKind { get; set; }
            public bool PlayerLeads { get; set; }
            public int RaisedDay { get; set; }
            public bool Engaged { get; set; }
        }
    }
}
