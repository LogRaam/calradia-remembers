// Code written by Gabriel Mailhot, 04/08/2026.
// follow_me (1:1 bridge action): persists which lords currently escort the player's own party on the map, and
// advances/ends each daily. Mirrors CouncilStipendBehavior/CouncilHostageBehavior's own SyncData shape (byte[]
// JSON via Newtonsoft.Json, idempotent load, graceful on missing/corrupt, brand-new additive save key, nothing
// else touched).
//
// ESCORT MECHANIC (decompile-confirmed via ilspycmd against the shipped TaleWorlds.CampaignSystem.dll,
// MobileParty + MobilePartyAi): MobileParty.SetMoveEscortParty(MobileParty target, NavigationType
// navigationType, bool isTargetingPort) is the real API - an instance method on MobileParty itself, not on its
// .Ai - already used by the mod for the ambush convoy's own lord ride-out (Stance/AmbushCaptivityBehavior.cs,
// "lordParty.SetMoveEscortParty(convoy, MobileParty.NavigationType.Default, false)"). That call is a bare,
// unlocked nudge, and its own comment explains why: "his AI is never hard-locked, so he resumes after" - a
// lord's own AI keeps re-deciding a new DefaultBehavior on its regular cycle unless
// Ai.SetDoNotMakeNewDecisions(true) holds it. For a ONE-OFF ride-out that is fine; for a term meant to last
// EscortEligibilityPolicy.TermDays days it is not, so this behavior instead uses the SAME AI-lock dance
// TravelingParty.DirectToSettlement/DirectToMovingPoint already use for a party we mean to keep steering:
// release the lock, reissue the order (re-targeting the player's CURRENT, moved position), then re-lock.
// Ending an escort is the mirror: release the lock and stop giving new orders, letting the engine's own AI
// resume choosing this lord's business exactly as it would for any other free lord party.
#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic.Escort;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Escort
{
    /// <summary>Persists active follow_me escorts (a lord's own party riding with the player) and advances/ends each daily.</summary>
    public sealed class FollowMeBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_FollowEscorts_v1bytes";

        public static FollowMeBehavior Instance;

        private List<EscortRecord> _active = new List<EscortRecord>();

        public FollowMeBehavior()
        {
            Instance = this;
        }

        public int ActiveCount => _active.Count;

        /// <summary>True when this hero (by StringId) currently has their party escorting the player.</summary>
        public bool IsEscorting(string lordId)
            => !string.IsNullOrEmpty(lordId) && _active.Any(e => e.LordId == lordId);

        /// <summary>The StringIds of every lord currently escorting the player (a snapshot copy, safe to enumerate).</summary>
        public IEnumerable<string> EscortingLordIds => _active.Select(e => e.LordId).ToList();

        /// <summary>A one-line-per-escort summary, for diagnostics.</summary>
        public string DescribeActive()
        {
            if (_active.Count == 0) return "No lord is currently escorting you.";

            return string.Join("\n", _active.Select(e =>
            {
                Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == e.LordId);

                return $"- {lord?.Name?.ToString() ?? e.LordId}: {e.DaysRemaining} day(s) remaining.";
            }));
        }

        /// <summary>
        ///   Starts a new escort for <paramref name="lord" />: re-validates the SAME
        ///   <see cref="EscortEligibilityPolicy.CanEscort" /> gate the prompt teaching used ("the prompt is
        ///   advice, the bridge is law"), then steers the lord's own party onto the player's with the AI-lock
        ///   dance <c>TravelingParty</c> already uses, for the fixed <see cref="EscortEligibilityPolicy.TermDays" />
        ///   -day term. Refuses (with a player-facing <paramref name="failReason" />) on any guard failure.
        /// </summary>
        public bool TryStart(Hero lord, out string failReason)
        {
            failReason = null;

            if (lord is not {IsAlive: true} || Hero.MainHero == null || MobileParty.MainParty == null)
            {
                failReason = "they could not be found.";

                return false;
            }

            MobileParty party = lord.PartyBelongedTo;

            if (!EscortEligibilityPolicy.CanEscort(
                   leadsParty: party != null && party.LeaderHero == lord,
                   atWar: IsAtWarWithPlayer(lord),
                   inArmy: party?.Army != null || party?.SiegeEvent != null,
                   alreadyEscorting: IsEscorting(lord.StringId),
                   factionAtWar: FactionIsAtWarWithAnyone(lord.MapFaction)))
            {
                failReason = "they cannot ride with you right now.";

                return false;
            }

            StartEscortAi(party);

            _active.Add(new EscortRecord {LordId = lord.StringId, DaysRemaining = EscortEligibilityPolicy.TermDays});
            ModLog.Info($"[ESCORT] {lord.Name} begins escorting the player for {EscortEligibilityPolicy.TermDays} days.");

            return true;
        }

        /// <summary>
        ///   Ends an active escort early (dismiss_escort): releases the AI lock so the lord's own AI resumes
        ///   choosing their business, and drops the record. Refuses (with a player-facing
        ///   <paramref name="failReason" />) when this lord is not currently escorting.
        /// </summary>
        public bool TryDismiss(Hero lord, out string failReason)
        {
            failReason = null;
            EscortRecord record = lord != null ? _active.FirstOrDefault(e => e.LordId == lord.StringId) : null;

            if (record == null)
            {
                failReason = "they are not currently riding with you.";

                return false;
            }

            EndEscort(record, lord);
            ModLog.Info($"[ESCORT] {lord.Name} breaks off the escort early, at the player's word.");

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
                    ModLog.Error($"[ESCORT] serialize failed - persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[ESCORT] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in a save written before this behavior existed: brand new, additive key, so it
                    // degrades to no active escorts, never a crash.
                }

                if (bytes is {Length: > 0})
                    try { _active = JsonConvert.DeserializeObject<List<EscortRecord>>(Encoding.UTF8.GetString(bytes)) ?? new List<EscortRecord>(); }
                    catch (Exception ex)
                    {
                        _active = new List<EscortRecord>();
                        ModLog.Error($"[ESCORT] load failed - dropping any in-flight escorts so the campaign opens. {ex.GetType().Name}: {ex.Message}");
                    }
                else
                    _active = new List<EscortRecord>();

                ModLog.Info($"[ESCORT] loaded {_active.Count} active escort(s) from save.");
            }
        }

        #region private

        /// <summary>
        ///   Decrements one day, ends the escort on any EscortContinuationPolicy fact (term elapsed, the lord
        ///   died/was captured/went to war with the player), else reissues the escort order so it tracks the
        ///   player's CURRENT position - a stale target is how a "soft" escort order drifts off course over a
        ///   long journey (see AmbushCaptivityBehavior's own note on its bare, one-shot nudge).
        /// </summary>
        private void Advance(EscortRecord r)
        {
            Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == r.LordId);
            bool isDead = lord == null;
            bool isCaptured = lord != null && lord.IsPrisoner;
            bool isAtWar = lord != null && IsAtWarWithPlayer(lord);

            r.DaysRemaining--;

            if (EscortContinuationPolicy.ShouldEnd(r.DaysRemaining, isDead, isCaptured, isAtWar))
            {
                EndEscort(r, lord);
                ModLog.Info($"[ESCORT] escort ended for {lord?.Name?.ToString() ?? r.LordId} " +
                            $"(daysRemaining={r.DaysRemaining}, dead={isDead}, captured={isCaptured}, atWar={isAtWar}).");

                return;
            }

            if (lord?.PartyBelongedTo != null) StartEscortAi(lord.PartyBelongedTo);
        }

        /// <summary>
        ///   Releases the AI lock and drops the record. Deliberately does NOT try to relocate the lord's party
        ///   anywhere - a freed lord party simply resumes the engine's own AI decision-making on its next tick,
        ///   the same as any other lord party the mod has never touched.
        /// </summary>
        private void EndEscort(EscortRecord r, Hero lord)
        {
            _active.Remove(r);

            if (lord?.PartyBelongedTo != null)
                try
                {
                    lord.PartyBelongedTo.Ai.SetDoNotMakeNewDecisions(false);
                    lord.PartyBelongedTo.SetMoveModeHold();
                }
                catch (Exception ex) { ModLog.Error($"[ESCORT] release failed for {lord.Name}: {ex.Message}"); }
        }

        private static bool IsAtWarWithPlayer(Hero lord)
            => lord.MapFaction != null && Hero.MainHero?.MapFaction != null &&
               FactionManager.IsAtWarAgainstFaction(lord.MapFaction, Hero.MainHero.MapFaction);

        /// <summary>
        ///   True when <paramref name="faction" /> holds a live war against ANY other faction, not only the
        ///   player's own (the author's C-guard concern: a lord whose realm is at war must stay home for his
        ///   liege's muster call, not ride off escorting the player and quietly weaken his faction). Walks the
        ///   same Kingdoms + independent-Clans pool EncounterContextBuilder's own peace-mediation teaching draws
        ///   from, so the teaching gate and this re-check can never disagree. Guarded so a missing engine
        ///   snapshot never throws mid-tick.
        /// </summary>
        private static bool FactionIsAtWarWithAnyone(IFaction faction)
        {
            try
            {
                if (faction == null) return false;

                if (Kingdom.All != null)
                    foreach (Kingdom k in Kingdom.All)
                        if (k != faction && FactionManager.IsAtWarAgainstFaction(faction, k)) return true;

                if (Clan.All != null)
                    foreach (Clan c in Clan.All)
                        if (c.Kingdom == null && c != faction && FactionManager.IsAtWarAgainstFaction(faction, c)) return true;

                return false;
            }
            catch { return false; }
        }

        private void OnDailyTick()
        {
            if (_active.Count == 0) return;

            foreach (EscortRecord r in _active.ToList())
            {
                EscortRecord record = r;
                // One escort's advance throwing must not stall every other lord's escort this tick, mirroring
                // CouncilStipendBehavior.OnDailyTick's/CouncilHostageBehavior.OnDailyTick's own SafeRun.Step wrapping.
                SafeRun.Step($"FollowMeBehavior.Advance({record.LordId})", () => Advance(record));
            }
        }

        /// <summary>
        ///   The AI-lock dance <c>TravelingParty.DirectToSettlement</c>/<c>DirectToMovingPoint</c> already use for
        ///   a party the mod means to keep steering across many ticks: release any existing lock, issue the
        ///   escort order (re-targeting the player's current position), then re-lock so the lord's own AI does
        ///   not immediately reclaim the party for its usual business.
        /// </summary>
        private static void StartEscortAi(MobileParty party)
        {
            party.Ai.SetDoNotMakeNewDecisions(false);
            party.SetMoveEscortParty(MobileParty.MainParty, MobileParty.NavigationType.Default, false);
            party.Ai.SetDoNotMakeNewDecisions(true);
        }

        #endregion
    }
}
