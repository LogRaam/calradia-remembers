// Code written by Gabriel Mailhot, 05/08/2026.
// ASSEMBLY pillar, Council module: non_aggression_pact's own persisted-behavior, rounding out the Parley
// toolkit's persisted-behavior set alongside CouncilTributeBehavior and CouncilHostageBehavior, mirroring their
// own SyncData shape exactly (byte[] JSON via Newtonsoft.Json, idempotent load, graceful on missing/corrupt,
// brand-new additive save key, nothing else touched). Partie 8 (COUNCIL_ACTIONS.md's Kimi review,
// "non_aggression_pact"): see CalradiaRemembers.Logic.Assemblies.NonAggressionPactPolicy's own header for the
// HONEST-LIMITED scope this whole mechanic rests on (vanilla has no native inter-clan non-aggression, and vanilla
// AI does not respect one) before reading this file.
//
// THE TWO REAL DELIVERABLES this behavior provides:
// (a) IsUnderPact(IFaction, IFaction) - the query CalradiaRemembers.Quests.QuestFactory.Create reads before
//     emitting any CR-generated HOSTILE quest (attack_faction/attack_lord/raid_village/attack_caravan/siege/
//     capture_prisoner/execute_enemy/declare_war): a quest whose target faction is a current pact partner of the
//     PLAYER's own live faction is refused at the emission point, never narrated then silently dropped.
// (b) OnMapEventEnded - breach DETECTION (never causation, mirroring CalradiaRemembers.Commitments.
//     CommitmentBehavior.OnMapEventEnded's own "recognizes, never causes" discipline): a real MapEvent pitting a
//     party of one pact faction against a party of the other ends the pact immediately and applies the
//     documented consequence (NonAggressionPactPolicy's own header: a grudge ONLY when the player's own faction
//     is the attacker, a relation hit either way, and a WorldEvent note naming the true attacker).
#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>Persists active non-aggression pacts, decrements their term daily, and detects/sanctions a real breach.</summary>
    public sealed class CouncilPactBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_CouncilPacts_v1bytes";

        public static CouncilPactBehavior Instance;

        private List<CouncilPact> _active = new List<CouncilPact>();

        public CouncilPactBehavior()
        {
            Instance = this;
        }

        public int ActiveCount => _active.Count;

        /// <summary>
        ///   Starts a new pact between <paramref name="factionA" /> (the player's own live faction) and
        ///   <paramref name="factionB" /> (the parley enemy faction) for the FIXED term
        ///   (NonAggressionPactPolicy.TermDays). Refuses (with a player-facing <paramref name="failReason" />)
        ///   when either faction cannot be resolved, when they are the same faction (nothing to pact against
        ///   oneself), or when the two are already bound by a live pact (a redundant second pact would just
        ///   duplicate the term rather than extend it meaningfully - the offering fact itself already avoids
        ///   teaching this case, but the lift re-checks it anyway, "bridge is law").
        /// </summary>
        public bool TryStart(IFaction factionA, IFaction factionB, out string failReason)
        {
            failReason = null;

            if (factionA == null || factionB == null)
            {
                failReason = "no faction by that name could be found.";

                return false;
            }

            if (factionA == factionB)
            {
                failReason = "a pact against one's own house makes no sense.";

                return false;
            }

            if (IsUnderPact(factionA, factionB))
            {
                failReason = "the two houses are already bound by such a pact.";

                return false;
            }

            _active.Add(new CouncilPact {
                FactionAId = factionA.StringId,
                FactionBId = factionB.StringId,
                DaysRemaining = NonAggressionPactPolicy.TermDays
            });

            ModLog.Info($"[PACT] struck between {factionA.Name} and {factionB.Name} for {NonAggressionPactPolicy.TermDays} days.");

            return true;
        }

        /// <summary>True when <paramref name="factionA" /> and <paramref name="factionB" /> (in either order) are currently bound by a live pact.</summary>
        public bool IsUnderPact(IFaction factionA, IFaction factionB)
        {
            if (factionA == null || factionB == null) return false;

            return _active.Any(p => NonAggressionPactPolicy.IsMatch(new FactionPair(p.FactionAId, p.FactionBId), new FactionPair(factionA.StringId, factionB.StringId)));
        }

        /// <summary>Council Tier 2: the live pact binding these two factions (either order), or null when none. Additive read-only accessor for the 1:1 prompt (see CouncilAccordClause).</summary>
        public CouncilPact Get(IFaction factionA, IFaction factionB)
        {
            if (factionA == null || factionB == null) return null;

            return _active.Find(p => NonAggressionPactPolicy.IsMatch(new FactionPair(p.FactionAId, p.FactionBId), new FactionPair(factionA.StringId, factionB.StringId)));
        }

        /// <summary>Dev/test only (cr.seed_accord): inject a live pact record directly, bypassing every guard, so the encyclopedia panel can be validated without a parley playthrough.</summary>
        public void DebugSeed(string factionAId, string factionBId, int days)
        {
            if (string.IsNullOrEmpty(factionAId) || string.IsNullOrEmpty(factionBId)) return;
            _active.Add(new CouncilPact {FactionAId = factionAId, FactionBId = factionBId, DaysRemaining = days});
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_active, Formatting.None)); }
                catch (Exception ex)
                {
                    ModLog.Error($"[PACT] serialize failed - persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[PACT] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in a save written before this behavior existed: brand new, additive key, so it
                    // degrades to no active pacts, never a crash.
                }

                if (bytes is {Length: > 0})
                    try { _active = JsonConvert.DeserializeObject<List<CouncilPact>>(Encoding.UTF8.GetString(bytes)) ?? new List<CouncilPact>(); }
                    catch (Exception ex)
                    {
                        _active = new List<CouncilPact>();
                        ModLog.Error($"[PACT] load failed - dropping any in-flight pacts so the campaign opens. {ex.GetType().Name}: {ex.Message}");
                    }
                else
                    _active = new List<CouncilPact>();

                ModLog.Info($"[PACT] loaded {_active.Count} active pact(s) from save.");
            }
        }

        #region private

        private void OnDailyTick()
        {
            if (_active.Count == 0) return;

            foreach (CouncilPact p in _active.ToList())
            {
                CouncilPact pact = p;
                // One pact's advance throwing must not stall every other pact this tick, mirroring
                // CouncilTributeBehavior.OnDailyTick's/CouncilHostageBehavior.OnDailyTick's own SafeRun.Step wrapping.
                SafeRun.Step($"CouncilPactBehavior.Advance({pact.FactionAId}/{pact.FactionBId})", () => Advance(pact));
            }
        }

        /// <summary>Decrements one day of the term; at zero the pact simply LAPSES (no penalty, a term honestly served out), notified via a quick-info banner.</summary>
        private void Advance(CouncilPact pact)
        {
            pact.DaysRemaining--;

            if (pact.DaysRemaining > 0) return;

            _active.Remove(pact);

            IFaction factionB = ParleyFactionLookup.ByStringId(pact.FactionBId);
            ModLog.Info($"[PACT] term lapsed between the player and {factionB?.Name?.ToString() ?? pact.FactionBId}.");

            try
            {
                MBInformationManager.AddQuickInformation(
                    new TaleWorlds.Localization.TextObject($"The non-aggression pact with {factionB?.Name?.ToString() ?? "your neighbor"} has run its course."));
            }
            catch (Exception ex) { ModLog.Error($"[PACT] lapse banner failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        /// <summary>True when any party on this side belongs to the given faction.</summary>
        private static bool SideHasFaction(MBReadOnlyList<MapEventParty> parties, string factionId)
        {
            if (parties == null || string.IsNullOrEmpty(factionId)) return false;

            foreach (MapEventParty mp in parties)
                if (mp.Party?.MapFaction?.StringId == factionId)
                    return true;

            return false;
        }

        /// <summary>
        ///   Breach detection (recognizes, never causes, mirroring CommitmentBehavior.OnMapEventEnded's own
        ///   discipline): a real MapEvent pitting a party of one pact faction against a party of the other ends
        ///   the pact immediately, applies the documented consequence (see this file's own header and
        ///   NonAggressionPactPolicy's own header for the fault-directional grudge rule), and logs a WorldEvent
        ///   naming the TRUE attacker regardless of which side's memory records the grudge.
        /// </summary>
        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (_active.Count == 0 || mapEvent == null) return;

            foreach (CouncilPact p in _active.ToList())
            {
                CouncilPact pact = p;
                SafeRun.Step($"CouncilPactBehavior.CheckBreach({pact.FactionAId}/{pact.FactionBId})", () => CheckBreach(pact, mapEvent));
            }
        }

        private void CheckBreach(CouncilPact pact, MapEvent mapEvent)
        {
            MBReadOnlyList<MapEventParty> attackers = mapEvent.GetMapEventSide(BattleSideEnum.Attacker)?.Parties;
            MBReadOnlyList<MapEventParty> defenders = mapEvent.GetMapEventSide(BattleSideEnum.Defender)?.Parties;

            bool attackerIsA = SideHasFaction(attackers, pact.FactionAId);
            bool attackerIsB = SideHasFaction(attackers, pact.FactionBId);
            bool defenderIsA = SideHasFaction(defenders, pact.FactionAId);
            bool defenderIsB = SideHasFaction(defenders, pact.FactionBId);

            if (!NonAggressionPactPolicy.IsBreach(new NonAggressionPactPolicy.PactBreachFacts {AttackerIsFactionA = attackerIsA, AttackerIsFactionB = attackerIsB, DefenderIsFactionA = defenderIsA, DefenderIsFactionB = defenderIsB})) return;

            _active.Remove(pact);

            bool playerAtFault = NonAggressionPactPolicy.IsPlayerFactionAtFault(attackerIsA);
            IFaction factionA = ParleyFactionLookup.ByStringId(pact.FactionAId);
            IFaction factionB = ParleyFactionLookup.ByStringId(pact.FactionBId);
            string attackerName = (playerAtFault ? factionA : factionB)?.Name?.ToString() ?? "One house";
            string defenderName = (playerAtFault ? factionB : factionA)?.Name?.ToString() ?? "the other";

            Hero enemyLeader = factionB?.Leader;

            // The grudge only ever plants when the PLAYER's own side is the attacker (see NonAggressionPactPolicy's
            // own header for why the reverse direction has no honest mechanical equivalent in this codebase: an
            // NPC resenting the player for THEIR OWN faction's attack makes no causal sense). The relation hit and
            // WorldEvent note, in contrast, apply either way, fault-agnostic - a shattered pact strains the
            // relationship regardless of who struck first.
            if (playerAtFault && Mapping.HeroPresence.IsInTheWorld(enemyLeader) && Hero.MainHero != null)
                GrudgeBehavior.Instance?.TryPlant(enemyLeader, Hero.MainHero, GrudgeSource.PactBroken,
                    "swore a pact of non-aggression with us, then broke it with steel", NonAggressionPactPolicy.BreachGrudgeMagnitude,
                    knownToPlayer: true, (int) CampaignTime.Now.ToDays);

            if (Mapping.HeroPresence.IsInTheWorld(enemyLeader) && Hero.MainHero != null)
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(enemyLeader, Hero.MainHero, NonAggressionPactPolicy.BreachRelationHit, false);

            try
            {
                MBInformationManager.AddQuickInformation(
                    new TaleWorlds.Localization.TextObject($"{attackerName} has broken the non-aggression pact and attacked {defenderName}."));
            }
            catch (Exception ex) { ModLog.Error($"[PACT] breach banner failed: {ex.GetType().Name}: {ex.Message}"); }

            WorldEventStore.Instance?.Record(new WorldEvent {
                Type = WorldEventType.Other,
                Day = (int) CampaignTime.Now.ToDays,
                InvolvesPlayer = true,
                Magnitude = 30,
                Status = "pact_broken",
                Summary = $"{attackerName} broke the non-aggression pact and attacked {defenderName}.",
                ActorIds = new List<string>(),
                FactionIds = new List<string> {pact.FactionAId, pact.FactionBId}
            });

            ModLog.Info($"[PACT] broken between {factionA?.Name} and {factionB?.Name}: {attackerName} was the attacker.");
        }

        #endregion
    }
}
