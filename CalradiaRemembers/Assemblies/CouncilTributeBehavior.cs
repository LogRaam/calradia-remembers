// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module: tribute's daily-tick payer, mirroring CouncilStipendBehavior's own SyncData
// shape exactly (byte[] JSON via Newtonsoft.Json, idempotent load, graceful on missing/corrupt, additive save
// key). Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "tribute"): the Parley's own submission/buy-off motion, the
// SEATED ENEMY leader pledges a recurring DAILY payment out of their own coffers to the player - the money moves
// the OPPOSITE direction of grant_stipend (which escrows the WHOLE 30-day tranche out of the PLAYER's purse up
// front). A tribute is PAY-AS-YOU-GO instead: nothing is reserved at TryStart, and each daily tick checks the
// payer's LIVE gold fresh (TributePolicy.CanPay) before paying out, so it degrades gracefully (a missed day is
// tolerated up to TributePolicy.MissedDaysGraceThreshold consecutive misses) rather than clamping the payer's
// gold negative or failing the whole term because ONE day came up short.
//
// Robustness guard beyond COUNCIL_ACTIONS.md's own written cases (documented, not silently added): if war breaks
// out again between the payer's own faction and the player's between two ticks, the tribute is VOIDED outright
// (no penalty, no report) rather than continuing to charge an active war enemy - COUNCIL_ACTIONS.md's own Kimi
// review names this exact case ("guerre declaree entre les deux clans -> tribut annule immediatement").
//
// Deliberately NOT built (documented follow-ups, mirroring SettleRevokeFief's own deferred-defection TODO):
// (1) surviving the payer's own leader dying or being replaced (COUNCIL_ACTIONS.md's Kimi review argues a
// tribute should bind the CLAN, not the hero; this build keeps CouncilStipend's own hero-id shape instead, see
// CouncilTribute's own header, so a payer who dies mid-term quietly ends the tribute, same as a stipend payee
// dying); (2) cancelling a tribute if the payer's clan later joins the receiver's own kingdom.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Assemblies;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>Persists active tributes and pays each out daily, pay-as-you-go, from the payer's own live gold.</summary>
    public sealed class CouncilTributeBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_CouncilTributes_v1bytes";

        // COUNCIL_ACTIONS.md's own Kimi review: a broken tribute earns a real but LIGHT relation hit, "-15".
        // Deliberately NOT a new GrudgeSource (the doc's own suggestion, TributeDefaulted): a GrudgeSource enum
        // value is a persisted-ordinal-sensitive change (see GrudgeSource's own "last in enum" discipline
        // elsewhere in this codebase), a heavier commitment than this slice's scope calls for. A plain relation
        // hit captures the ordinary consequence without that risk; a dedicated grudge source is a natural v2,
        // mirroring how RevokeFief/ExpelFromClan's own grudges were added once their executors landed.
        private const int BrokenTributeRelationHit = -15;

        public static CouncilTributeBehavior Instance;

        private List<CouncilTribute> _active = new List<CouncilTribute>();

        public CouncilTributeBehavior()
        {
            Instance = this;
        }

        public int ActiveCount => _active.Count;

        /// <summary>Council Tier 2: the running tribute this payer owes the player, or null when none. Additive read-only accessor for the 1:1 prompt (see CouncilAccordClause).</summary>
        public CouncilTribute Get(string payerHeroId)
            => string.IsNullOrEmpty(payerHeroId) ? null : _active.Find(t => t.PayerHeroId == payerHeroId);

        /// <summary>Dev/test only (cr.seed_accord): inject a live tribute record directly, bypassing every guard, so the encyclopedia panel can be validated without a full parley playthrough.</summary>
        public void DebugSeed(string payerHeroId, int amountPerDay, int days)
        {
            if (string.IsNullOrEmpty(payerHeroId)) return;
            _active.Add(new CouncilTribute {PayerHeroId = payerHeroId, AmountPerDay = amountPerDay, DaysRemaining = days, MissedDays = 0});
        }

        /// <summary>
        ///   Starts a new tribute from <paramref name="payerLeader" /> at <paramref name="amountPerDay" />
        ///   denars/day (re-clamped here to TributePolicy's [50, 2000] range - "the prompt is advice, the bridge
        ///   is law"), for the FIXED 30-day term. Unlike CouncilStipendBehavior.TryStart, nothing is charged or
        ///   reserved here: this is PAY-AS-YOU-GO, so the first payment (and every affordability check) happens
        ///   at the next daily tick, never here. Refuses (with a player-facing <paramref name="failReason" />)
        ///   when the payer is dead/gone or leads no house distinct from the player's own (the same cross-clan
        ///   guard SettleGiveInfluence/SettleGrantFief already apply): a tribute paid to oneself is meaningless.
        /// </summary>
        public bool TryStart(Hero payerLeader, int amountPerDay, out string failReason)
        {
            failReason = null;

            if (!Mapping.HeroPresence.IsInTheWorld(payerLeader))
            {
                failReason = "they could not be found.";

                return false;
            }

            if (payerLeader.Clan == null || payerLeader.Clan == Clan.PlayerClan)
            {
                failReason = "they lead no house distinct from the player's own to pay tribute from.";

                return false;
            }

            int clamped = TributePolicy.ClampAmountPerDay(amountPerDay);

            _active.Add(new CouncilTribute {
                PayerHeroId = payerLeader.StringId,
                AmountPerDay = clamped,
                DaysRemaining = TributePolicy.TributeDays,
                MissedDays = 0
            });

            ModLog.Info($"[TRIBUTE] started from {payerLeader.Name}: {clamped}/day for {TributePolicy.TributeDays} days.");

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
                    ModLog.Error($"[TRIBUTE] serialize failed - persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[TRIBUTE] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in a save written before this behavior existed: brand new, additive key, so it
                    // degrades to no active tributes, never a crash.
                }

                if (bytes is {Length: > 0})
                    try { _active = JsonConvert.DeserializeObject<List<CouncilTribute>>(Encoding.UTF8.GetString(bytes)) ?? new List<CouncilTribute>(); }
                    catch (Exception ex)
                    {
                        _active = new List<CouncilTribute>();
                        ModLog.Error($"[TRIBUTE] load failed - dropping any in-flight tributes so the campaign opens. {ex.GetType().Name}: {ex.Message}");
                    }
                else
                    _active = new List<CouncilTribute>();

                ModLog.Info($"[TRIBUTE] loaded {_active.Count} active tribute(s) from save.");
            }
        }

        #region private

        private void OnDailyTick()
        {
            if (_active.Count == 0) return;

            foreach (CouncilTribute t in _active.ToList())
            {
                CouncilTribute tribute = t;
                // One tribute's advance throwing must not stall every other tribute's payout this tick, mirroring
                // CouncilStipendBehavior.OnDailyTick's own SafeRun.Step wrapping.
                SafeRun.Step($"CouncilTributeBehavior.Advance({tribute.PayerHeroId})", () => Advance(tribute));
            }
        }

        /// <summary>
        ///   Pays one day, pay-as-you-go, straight from the payer's OWN live gold to Hero.MainHero, and decrements
        ///   the term. A payer who has died simply ends the tribute (no clan-survives-leader-change enhancement
        ///   this slice, see this file's own header), and a payer whose faction has gone back to war with the
        ///   player VOIDS the tribute outright (no penalty). Otherwise: TributePolicy.CanPay gates the payment
        ///   against the payer's CURRENT gold (never reserved in advance); an unaffordable day increments
        ///   MissedDays instead of clamping the payer negative, resetting to zero on the next successful payment
        ///   (a genuine bad patch is tolerated); MissedDaysGraceThreshold consecutive misses breaks the tribute
        ///   early with a light relation hit, COUNCIL_ACTIONS.md's own documented default handling.
        /// </summary>
        private void Advance(CouncilTribute t)
        {
            Hero payer = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == t.PayerHeroId);

            if (payer == null)
            {
                _active.Remove(t);

                return;
            }

            if (Hero.MainHero != null && payer.MapFaction != null && Hero.MainHero.MapFaction != null
                && FactionManager.IsAtWarAgainstFaction(payer.MapFaction, Hero.MainHero.MapFaction))
            {
                _active.Remove(t);
                ModLog.Info($"[TRIBUTE] voided for {payer.Name}: war has resumed between the two factions.");

                return;
            }

            if (Hero.MainHero != null && TributePolicy.CanPay(payer.Gold, t.AmountPerDay))
            {
                try { GiveGoldAction.ApplyBetweenCharacters(payer, Hero.MainHero, t.AmountPerDay, true); }
                catch (Exception ex) { ModLog.Error($"[TRIBUTE] payout failed from {payer.Name}: {ex.Message}"); }

                t.MissedDays = 0;
            }
            else
            {
                t.MissedDays++;

                if (t.MissedDays >= TributePolicy.MissedDaysGraceThreshold)
                {
                    _active.Remove(t);

                    if (Hero.MainHero != null)
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(payer, Hero.MainHero, BrokenTributeRelationHit, false);

                    try
                    {
                        MBInformationManager.AddQuickInformation(
                            new TaleWorlds.Localization.TextObject($"{payer.Name} has defaulted on the tribute owed to you."));
                    }
                    catch (Exception ex) { ModLog.Error($"[TRIBUTE] default banner failed: {ex.GetType().Name}: {ex.Message}"); }

                    ModLog.Info($"[TRIBUTE] broken by {payer.Name} after {t.MissedDays} consecutive missed days.");

                    return;
                }
            }

            t.DaysRemaining--;

            if (t.DaysRemaining <= 0)
            {
                _active.Remove(t);
                ModLog.Info($"[TRIBUTE] term completed for {payer.Name}.");
            }
        }

        #endregion
    }
}
