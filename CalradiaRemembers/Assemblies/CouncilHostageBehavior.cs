// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "give_hostage", Kimi's own "v1
// invite d'honneur" recommendation): rounds out the Parley toolkit's persisted-behavior trio alongside
// CouncilStipendBehavior and CouncilTributeBehavior, mirroring their own SyncData shape exactly (byte[] JSON via
// Newtonsoft.Json, idempotent load, graceful on missing/corrupt, brand-new additive save key, nothing else
// touched).
//
// MECHANIC (author's design call, adopting COUNCIL_ACTIONS.md's own v1 recipe verbatim): the hostage is
// physically moved into the PLAYER'S OWN PARTY via AddHeroToPartyAction.Apply(hostage, MobileParty.MainParty,
// false) - the SAME call rejoin_party/lend_troops' own delegation-arrival already use - rather than
// hero.StayingInSettlement in a fief. Chosen over a fief residence for two reasons: (1) it needs no fief at all
// (a player who rules no settlement can still take a hostage, the "invite d'honneur" framing does not require a
// throne room), and (2) it mirrors the SAME "your own court" framing SettleRejoinParty already uses for a
// returning companion. TryStart's own eligibility re-check (bridge is law) is the SAME
// HostageEligibilityPolicy.IsEligible predicate the offering fact and CouncilLift.SettleGiveHostage both already
// ran, via CalradiaRemembers.Assemblies.HostageCandidateResolver - never a second, drifting copy of the rule.
//
// RETURN AT TERM'S END (Advance, decompiled-API-verified via ilspycmd against TaleWorlds.CampaignSystem.dll):
// tries, in order, (1) the ORIGINAL pledger, if still alive and still leading their own party -
// AddHeroToPartyAction.Apply(hostage, pledgerParty, false), mirroring how a hostage's own captor-of-good-faith
// bond is with the person who gave them; (2) failing that, ANY OTHER alive lord of the same home clan who leads
// a party of their own (the pledger may have died, been captured, or lost their party since the term began -
// COUNCIL_ACTIONS.md's own "le garant meurt/change de faction -> l'otage retourne a sa famille" case); (3)
// failing that, the home clan's own HomeSettlement via TeleportHeroAction.ApplyImmediateTeleportToSettlement
// (decompiled: strips any party membership first, then EnterSettlementAction.ApplyForCharacterOnly - the SAME
// vanilla path a governor with no party of their own already uses to physically reside somewhere); (4) if NONE
// of the three exist (the pledger's whole clan has been eliminated with no settlement left to its name, a rare
// edge COUNCIL_ACTIONS.md itself flags as "l'accord garanti est rompu... trancher"), the hostage is left exactly
// where they are (still in the player's own party) and the record is dropped regardless - documented as a v1
// best-effort rather than a spike into settlement-less hero placement this slice does not need.
//
// EDGE CASES (this task's own simplified scope, narrower than COUNCIL_ACTIONS.md's fuller "v2 captif contraint"
// design - deliberately NOT built, see this file's own final paragraph): (a) the hostage dies of any cause while
// resident - the record is simply dropped, mirroring CouncilStipendBehavior's own "payee dies -> stipend ends"
// and CouncilTributeBehavior's own "payer dies -> tribute ends" precedent (no new GrudgeSource added this slice,
// the SAME "not this build's scope" discipline CouncilTributeBehavior's own header already documents for
// TributeDefaulted); (b) the pledger's own clan is eliminated mid-term - the hostage is released home immediately
// (best-effort per the fallback chain above) rather than waiting out the remaining days for a clan that no
// longer exists, and the record is dropped; (c) war re-erupts between the two factions during the term - v1
// deliberately does NOT escalate the hostage to a war prisoner (COUNCIL_ACTIONS.md's own "l'otage devient
// automatiquement prisonnier de guerre" is the DOCUMENTED v2 punitive path, not built here): the hostage simply
// completes their fixed term and returns home as agreed, because this v1 "invite d'honneur" design's own
// honesty rests on NEVER claiming the hostage is anything but a free guest - inventing a war-prisoner escalation
// here would be exactly the "the prompt is advice, the bridge is law" violation this codebase exists to prevent,
// since nothing in this slice's own prompt teaching ever promises that consequence to the player.
//
// v2 (DOCUMENTED FOLLOW-UP, NOT built this slice): COUNCIL_ACTIONS.md's own "captif contraint" - if the guaranteed
// accord is later broken, the garant may execute or imprison the hostage (reusing execute_prisoner's own
// GrudgeSource.ExecutedKin / the mod's existing prisoner flow). That punitive path needs its own accord-breach
// detection and sanction design; this slice ships ONLY the honest "guest of good faith" mechanic.
#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic.Assemblies;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>Persists active give_hostage terms, moves the hostage into the player's own party, and returns them home when the fixed term ends.</summary>
    public sealed class CouncilHostageBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_CouncilHostages_v1bytes";

        /// <summary>The one Residence value this build ever writes (see this file's own header for why a party, never a fief, was chosen).</summary>
        private const string MainPartyResidence = "MainParty";

        public static CouncilHostageBehavior Instance;

        private List<CouncilHostage> _active = new List<CouncilHostage>();

        public CouncilHostageBehavior()
        {
            Instance = this;
        }

        public int ActiveCount => _active.Count;

        /// <summary>Council Tier 2: the live term this hero is HELD under (they are the hostage), or null. Additive read-only accessor for the 1:1 prompt (see CouncilAccordClause).</summary>
        public CouncilHostage GetByHostage(string heroId)
            => string.IsNullOrEmpty(heroId) ? null : _active.Find(h => h.HostageId == heroId);

        /// <summary>Council Tier 2: the live term this hero PLEDGED (their house gave the hostage to the player), or null.</summary>
        public CouncilHostage GetByPledger(string heroId)
            => string.IsNullOrEmpty(heroId) ? null : _active.Find(h => h.PledgerId == heroId);

        /// <summary>Dev/test only (cr.seed_accord): inject a live hostage record directly, bypassing eligibility/party moves, so the encyclopedia panel can be validated without a parley playthrough.</summary>
        public void DebugSeed(string hostageId, string pledgerId, string homeClanId, int days)
        {
            if (string.IsNullOrEmpty(hostageId)) return;
            _active.Add(new CouncilHostage {HostageId = hostageId, PledgerId = pledgerId, HomeClanId = homeClanId, DaysRemaining = days, Residence = MainPartyResidence});
        }

        /// <summary>
        ///   Starts a new hostage term: <paramref name="hostage" /> must STILL pass HostageEligibilityPolicy right
        ///   now (bridge is law - re-checked here even though the caller, CouncilLift.SettleGiveHostage, already
        ///   resolved them through the SAME eligible pool, since the world can move between resolution and this
        ///   call in principle). On success, moves them into MobileParty.MainParty and records the term at
        ///   HostagePolicy.TermDays. Refuses (with a player-facing <paramref name="failReason" />) when the pledger
        ///   or hostage cannot be found, or the hostage no longer passes eligibility.
        /// </summary>
        public bool TryStart(Hero pledger, Hero hostage, out string failReason)
        {
            failReason = null;

            if (pledger is not {IsAlive: true})
            {
                failReason = "they could not be found.";

                return false;
            }

            if (hostage is not {IsAlive: true})
            {
                failReason = "the named kinsman could not be found.";

                return false;
            }

            if (!HostageCandidateResolver.EligibleRelativesOf(pledger).Contains(hostage))
            {
                failReason = "they are not an eligible kinsman to give as a hostage right now.";

                return false;
            }

            AddHeroToPartyAction.Apply(hostage, MobileParty.MainParty, false);

            _active.Add(new CouncilHostage {
                HostageId = hostage.StringId,
                PledgerId = pledger.StringId,
                HomeClanId = pledger.Clan?.StringId,
                DaysRemaining = HostagePolicy.TermDays,
                Residence = MainPartyResidence
            });

            ModLog.Info($"[HOSTAGE] {hostage.Name} enters the player's court as a hostage of good faith from {pledger.Name}, for {HostagePolicy.TermDays} days.");

            return true;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_active, Formatting.None)); }
                catch (Exception ex)
                {
                    ModLog.Error($"[HOSTAGE] serialize failed - persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[HOSTAGE] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in a save written before this behavior existed: brand new, additive key, so it
                    // degrades to no active hostages, never a crash.
                }

                if (bytes is {Length: > 0})
                    try { _active = JsonConvert.DeserializeObject<List<CouncilHostage>>(Encoding.UTF8.GetString(bytes)) ?? new List<CouncilHostage>(); }
                    catch (Exception ex)
                    {
                        _active = new List<CouncilHostage>();
                        ModLog.Error($"[HOSTAGE] load failed - dropping any in-flight hostage terms so the campaign opens. {ex.GetType().Name}: {ex.Message}");
                    }
                else
                    _active = new List<CouncilHostage>();

                ModLog.Info($"[HOSTAGE] loaded {_active.Count} active hostage term(s) from save.");
            }
        }

        #region private

        private void OnDailyTick()
        {
            if (_active.Count == 0) return;

            foreach (CouncilHostage h in _active.ToList())
            {
                CouncilHostage hostageTerm = h;
                // One hostage's advance throwing must not stall every other hostage's term this tick, mirroring
                // CouncilStipendBehavior.OnDailyTick's/CouncilTributeBehavior.OnDailyTick's own SafeRun.Step wrapping.
                SafeRun.Step($"CouncilHostageBehavior.Advance({hostageTerm.HostageId})", () => Advance(hostageTerm));
            }
        }

        /// <summary>
        ///   Decrements one day of the term, or ends it early on the two documented edge cases (hostage dies; home
        ///   clan is eliminated), or completes it and sends the hostage home once DaysRemaining reaches zero.
        /// </summary>
        private void Advance(CouncilHostage h)
        {
            Hero hostage = Hero.AllAliveHeroes.FirstOrDefault(hero => hero.StringId == h.HostageId);

            // (a) The hostage died of any cause while resident: drop the record, mirroring CouncilStipendBehavior's
            // own "payee dies -> stipend ends" precedent. No grudge/vendetta this slice (see this file's own header).
            if (hostage == null)
            {
                _active.Remove(h);
                ModLog.Info($"[HOSTAGE] term for {h.HostageId} ended: the hostage could no longer be found.");

                return;
            }

            Clan homeClan = string.IsNullOrEmpty(h.HomeClanId) ? null : Clan.All?.FirstOrDefault(c => c.StringId == h.HomeClanId);

            // (b) The pledger's own clan is eliminated mid-term: release home immediately (best-effort) rather than
            // waiting out days owed to a house that no longer exists.
            if (homeClan is {IsEliminated: true} || homeClan == null && !string.IsNullOrEmpty(h.HomeClanId))
            {
                SendHome(h, hostage, homeClan);
                _active.Remove(h);
                ModLog.Info($"[HOSTAGE] {hostage.Name}'s home clan is gone; released home early.");

                return;
            }

            h.DaysRemaining--;

            if (h.DaysRemaining <= 0)
            {
                SendHome(h, hostage, homeClan);
                _active.Remove(h);
                ModLog.Info($"[HOSTAGE] {hostage.Name}'s term is complete; sent home.");
            }
        }

        /// <summary>
        ///   The return-home fallback chain (see this file's own header): the original pledger's own live party,
        ///   then any other alive lord of the home clan leading a party, then the home clan's own HomeSettlement,
        ///   then (last resort) leaves the hostage exactly where they are.
        /// </summary>
        private static void SendHome(CouncilHostage h, Hero hostage, Clan homeClan)
        {
            Hero pledger = Hero.AllAliveHeroes.FirstOrDefault(hero => hero.StringId == h.PledgerId);

            if (pledger is {IsAlive: true, IsPrisoner: false} && pledger.PartyBelongedTo?.LeaderHero == pledger)
            {
                try { AddHeroToPartyAction.Apply(hostage, pledger.PartyBelongedTo, false); }
                catch (Exception ex) { ModLog.Error($"[HOSTAGE] return to pledger's party failed: {ex.Message}"); }

                return;
            }

            Hero otherHomeLord = homeClan?.Heroes?.FirstOrDefault(hero =>
                hero != hostage && hero is {IsAlive: true, IsPrisoner: false} && hero.PartyBelongedTo?.LeaderHero == hero);

            if (otherHomeLord != null)
            {
                try { AddHeroToPartyAction.Apply(hostage, otherHomeLord.PartyBelongedTo, false); }
                catch (Exception ex) { ModLog.Error($"[HOSTAGE] return to home clan's party failed: {ex.Message}"); }

                return;
            }

            if (homeClan?.HomeSettlement != null)
            {
                try { TeleportHeroAction.ApplyImmediateTeleportToSettlement(hostage, homeClan.HomeSettlement); }
                catch (Exception ex) { ModLog.Error($"[HOSTAGE] return to home settlement failed: {ex.Message}"); }

                return;
            }

            // Last resort (rare: no home party, no home settlement - a clan destroyed to the last fief): the hostage
            // simply remains in the player's own party, logged rather than forced anywhere unverified.
            ModLog.Info($"[HOSTAGE] {hostage.Name} has no home party or settlement to return to; remains at the player's own court.");
        }

        #endregion
    }
}
