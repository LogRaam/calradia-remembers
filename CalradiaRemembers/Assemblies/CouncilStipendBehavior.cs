// Code written by Gabriel Mailhot, 03/08/2026.
// ASSEMBLY pillar, Council module: grant_stipend's escrow, the FIRST council executor that touches PERSISTED
// SAVE STATE rather than merely mutating a live campaign object. Author's design (Kimi review, adopted as-is,
// COUNCIL_ACTIONS.md "grant_stipend"): a stipend is a RECURRING payment to a seated clan companion, funded up
// front for a FIXED 30-day tranche (StipendPolicy) so the promise can never fail mid-term. TryStart (called from
// CouncilLift.SettleGrantStipend at the lift) deducts the WHOLE tranche from the player's gold immediately into
// this behavior's own escrow; a daily tick then pays amountPerDay OUT of that escrow to the payee (crediting
// their gold, the only mechanical use NPC gold has) and refreshes a positive happiness driver on them
// (CompanionHappinessService, HappinessDriver.Paid) - the real payoff, since NPC gold itself is nearly inert in
// vanilla. The stipend ends when its 30 days elapse or its escrow empties, whichever comes first. No early
// revoke this slice (COUNCIL_ACTIONS.md notes revoke_stipend as a follow-up). Mirrors CompanionMissionBehavior's
// own SyncData shape (byte[] JSON via Newtonsoft.Json, idempotent load, graceful on missing/corrupt) and
// CouncilSessionBehavior's additive save-key discipline: a brand-new key, nothing else touched.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Missions;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>Persists active council-granted stipends and pays each out daily from its own mod-tracked escrow.</summary>
    public sealed class CouncilStipendBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_CouncilStipends_v1bytes";

        public static CouncilStipendBehavior Instance;

        private List<CouncilStipend> _active = new List<CouncilStipend>();

        public CouncilStipendBehavior()
        {
            Instance = this;
        }

        public int ActiveCount => _active.Count;

        /// <summary>
        ///   Starts a new stipend for <paramref name="payee" /> at <paramref name="amountPerDay" /> denars/day
        ///   (re-clamped here to StipendPolicy's [50, 500] range - "the prompt is advice, the bridge is law"),
        ///   funding the WHOLE 30-day tranche up front out of the player's own gold into this behavior's escrow.
        ///   Author's design: the promise is fully funded at the lift so it can never silently fail mid-term.
        ///   Refuses (with a player-facing <paramref name="failReason" />) when the player cannot cover the full
        ///   tranche or the payee is invalid; never charges a partial amount.
        /// </summary>
        public bool TryStart(Hero payee, int amountPerDay, out string failReason)
        {
            failReason = null;

            if (payee is not {IsAlive: true})
            {
                failReason = "they could not be found.";

                return false;
            }

            int clamped = StipendPolicy.ClampAmountPerDay(amountPerDay);
            int totalCost = StipendPolicy.TotalCost(clamped);

            if (Hero.MainHero == null || Hero.MainHero.Gold < totalCost)
            {
                failReason = $"the player's coffers cannot cover the full {totalCost}-denar tranche.";

                return false;
            }

            // The whole tranche leaves the player's purse NOW, into an escrow no hero holds (mirrors
            // BannerlordGameStateBridge.ExecuteAction's own "no hero recipient" take_gold path): the promise is
            // financed in full before a single denar is ever paid out, so it can never fail partway through.
            Hero.MainHero.ChangeHeroGold(-totalCost);

            _active.Add(new CouncilStipend {
                PayeeId = payee.StringId,
                AmountPerDay = clamped,
                DaysRemaining = StipendPolicy.TrancheDays,
                EscrowBalance = totalCost
            });

            ModLog.Info($"[STIPEND] started for {payee.Name}: {clamped}/day for {StipendPolicy.TrancheDays} days ({totalCost} denars escrowed).");

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
                    ModLog.Error($"[STIPEND] serialize failed - persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[STIPEND] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in a save written before this behavior existed: brand new, additive key, so it
                    // degrades to no active stipends, never a crash.
                }

                if (bytes is {Length: > 0})
                    try { _active = JsonConvert.DeserializeObject<List<CouncilStipend>>(Encoding.UTF8.GetString(bytes)) ?? new List<CouncilStipend>(); }
                    catch (Exception ex)
                    {
                        _active = new List<CouncilStipend>();
                        ModLog.Error($"[STIPEND] load failed - dropping any in-flight stipends so the campaign opens. {ex.GetType().Name}: {ex.Message}");
                    }
                else
                    _active = new List<CouncilStipend>();

                ModLog.Info($"[STIPEND] loaded {_active.Count} active stipend(s) from save.");
            }
        }

        #region private

        private void OnDailyTick()
        {
            if (_active.Count == 0) return;

            foreach (CouncilStipend s in _active.ToList())
            {
                CouncilStipend stipend = s;
                // One stipend's advance throwing must not stall every other stipend's payout this tick, mirroring
                // CompanionMissionBehavior.OnDailyTick's own SafeRun.Step wrapping.
                SafeRun.Step($"CouncilStipendBehavior.Advance({stipend.PayeeId})", () => Advance(stipend));
            }
        }

        /// <summary>
        ///   Pays one day out of the escrow, decrements the tranche, and drops the stipend once its days or its
        ///   escrow run out. A payee who has died simply ends the stipend (no early revoke this slice, see
        ///   COUNCIL_ACTIONS.md's revoke_stipend follow-up note); the remaining escrow is not refunded.
        /// </summary>
        private void Advance(CouncilStipend s)
        {
            Hero payee = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == s.PayeeId);

            if (payee == null)
            {
                _active.Remove(s);

                return;
            }

            int pay = Math.Min(s.AmountPerDay, s.EscrowBalance);

            if (pay > 0)
            {
                // The gold already left the player's purse in full at TryStart; it comes FROM the escrow here,
                // never from the player a second time - mirrors CompanionMissionBehavior.DeliverReport's own
                // "found coin" pattern (null giver) for the identical reason.
                try { GiveGoldAction.ApplyBetweenCharacters(null, payee, pay, true); }
                catch (Exception ex) { ModLog.Error($"[STIPEND] payout failed for {payee.Name}: {ex.Message}"); }

                s.EscrowBalance -= pay;
                CompanionHappinessService.ApplyDriver(payee, HappinessDriver.Paid);
            }

            s.DaysRemaining--;

            if (s.DaysRemaining <= 0 || s.EscrowBalance <= 0)
            {
                _active.Remove(s);
                ModLog.Info($"[STIPEND] ended for {payee.Name} (days remaining {s.DaysRemaining}, escrow {s.EscrowBalance}).");
            }
        }

        #endregion
    }
}
