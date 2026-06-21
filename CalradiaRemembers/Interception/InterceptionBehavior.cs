// Code written by Gabriel Mailhot, 21/06/2026.
// NPC-initiated interceptions: a nearby lord may ride out to the player rather than wait to be
// sought. The decision (who, why, the guards) is pure and unit-tested in
// CalradiaRemembers.Logic.InterceptionPolicy; this class is the engine glue (scan, pursue, notify).
// The "heard of a deed" trigger reads the world-event/scheme rumour store, so the simulation we built
// gives lords reasons to come for the player.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Interception
{
    /// <summary>
    ///   Once a day, at most one nearby lord may be sent to intercept the player when
    ///   <see cref="InterceptionPolicy"/> says a trigger fires (a hostile territorial challenge, or a
    ///   reaction to fresh news of the player). The lord's party engages the player's; the meeting
    ///   itself opens the ordinary "We need to talk" CR conversation. Guards (one pursuer at a time,
    ///   a per-lord cooldown, a pursuit timeout) keep it from becoming harassment. State is transient.
    /// </summary>
    public sealed class InterceptionBehavior : CampaignBehaviorBase
    {
        public static InterceptionBehavior Instance;

        private const float InterceptRange = 40f;        // a lord this close can plausibly ride out
        private const int DailyInterceptChancePercent = 25;
        private const int CooldownDays = 30;             // a given lord intercepts the player rarely
        private const int PursuitTimeoutDays = 4;        // release a chase that never connects
        private const int MaxCandidates = 12;            // bound the per-day work
        private const int RecentDeedWindowDays = 6;
        private const float ArrivalDistance = 3f;

        private string _activePursuerId;
        private int _activePursuerSince = -1;
        private readonly Dictionary<string, int> _cooldown = new Dictionary<string, int>();

        public InterceptionBehavior() => Instance = this;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        // Transient by design: a mid-pursuit reload simply lets the lord's AI return to its own plans.
        public override void SyncData(IDataStore dataStore) { }

        /// <summary>Forces the named lord to ride out and intercept the player (console testing).</summary>
        public string DebugForceIntercept(Hero lord)
        {
            if (lord?.PartyBelongedTo == null || MobileParty.MainParty == null) return $"{lord?.Name?.ToString() ?? "That hero"} has no party on the map to ride out.";
            BeginPursuit(lord, InterceptionTrigger.HeardOfDeed, (int) CampaignTime.Now.ToDays);
            return $"{lord.Name} now rides to intercept you.";
        }

        private void OnDailyTick()
        {
            try
            {
                int today = (int) CampaignTime.Now.ToDays;
                ReleaseStalePursuit(today);
                if (_activePursuerId != null) return;                       // one interception at a time
                if (MBRandom.RandomInt(100) >= DailyInterceptChancePercent) return;
                TryStartInterception(today);
            }
            catch (Exception ex) { ModLog.Error($"[INTERCEPT] daily tick failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        private void ReleaseStalePursuit(int today)
        {
            if (_activePursuerId == null) return;

            Hero pursuer = FindHero(_activePursuerId);
            MobileParty pursuerParty = pursuer?.PartyBelongedTo;
            MobileParty main = MobileParty.MainParty;

            bool invalid = pursuer == null || !pursuer.IsAlive || pursuerParty == null;
            bool timedOut = _activePursuerSince >= 0 && today - _activePursuerSince >= PursuitTimeoutDays;
            bool arrived = pursuerParty != null && main != null
                           && pursuerParty.GetPosition2D.Distance(main.GetPosition2D) < ArrivalDistance;

            if (invalid || timedOut || arrived)
            {
                _activePursuerId = null;
                _activePursuerSince = -1;
            }
        }

        private void TryStartInterception(int today)
        {
            MobileParty main = MobileParty.MainParty;
            if (main == null || Hero.MainHero == null) return;

            Vec2 playerPos = main.GetPosition2D;
            IFaction territory = NearestSettlementFaction(playerPos);
            int notoriety = (int) (Clan.PlayerClan?.Renown ?? 0f);

            int scanned = 0;
            foreach (MobileParty p in MobileParty.All)
            {
                if (scanned >= MaxCandidates) break;

                Hero lord = p?.LeaderHero;
                if (lord == null || !lord.IsLord || lord == Hero.MainHero) continue;
                if (p == main || p.IsBandit || p.MapFaction == null) continue;
                if (p.GetPosition2D.Distance(playerPos) > InterceptRange) continue;

                scanned++;

                var facts = new InterceptionFacts
                {
                    OnCooldown             = _cooldown.TryGetValue(lord.StringId, out int last) && today - last < CooldownDays,
                    AnotherPursuerActive   = _activePursuerId != null,
                    PlayerInOwnTerritory   = territory != null && territory == lord.MapFaction,
                    FactionHostileToPlayer = IsHostileToPlayer(lord),
                    PlayerNotoriety        = notoriety,
                    HeardRecentPlayerDeed  = HeardRecentPlayerDeed(lord, today)
                };

                InterceptionTrigger trigger = InterceptionPolicy.Choose(facts);
                if (trigger == InterceptionTrigger.None) continue;

                BeginPursuit(lord, trigger, today);
                return; // one interception started per day
            }
        }

        private void BeginPursuit(Hero lord, InterceptionTrigger trigger, int today)
        {
            lord.PartyBelongedTo.SetMoveEngageParty(MobileParty.MainParty, MobileParty.NavigationType.Default);
            _activePursuerId = lord.StringId;
            _activePursuerSince = today;
            _cooldown[lord.StringId] = today;

            string line = trigger == InterceptionTrigger.TerritorialPatrol
                ? $"{lord.Name} rides out to challenge you for crossing their lands."
                : $"{lord.Name} rides toward you, having heard of your recent deeds.";
            try { MBInformationManager.AddQuickInformation(new TextObject(line)); } catch { /* notification is best-effort */ }
            ModLog.Info($"[INTERCEPT] {lord.Name} pursues the player ({trigger}).");
        }

        #region private

        private static bool IsHostileToPlayer(Hero lord)
        {
            try
            {
                IFaction lf = lord.MapFaction, pf = Hero.MainHero?.MapFaction;
                if (lf != null && pf != null && lf != pf && FactionManager.IsAtWarAgainstFaction(lf, pf)) return true;
                return lord.GetRelation(Hero.MainHero) <= -20;
            }
            catch { return false; }
        }

        private static bool HeardRecentPlayerDeed(Hero lord, int today)
        {
            try
            {
                WorldEventStore store = WorldEventStore.Instance;
                if (store == null) return false;
                foreach (WorldEvent e in store.GetKnownEvents(lord, today, 4))
                    if (e.InvolvesPlayer && today - e.Day <= RecentDeedWindowDays) return true;
                return false;
            }
            catch { return false; }
        }

        private static IFaction NearestSettlementFaction(Vec2 pos)
        {
            try
            {
                Settlement best = null;
                float bestDist = float.MaxValue;
                foreach (Settlement s in Settlement.All)
                {
                    if (s == null) continue;
                    float d = s.GetPosition2D.Distance(pos);
                    if (d < bestDist) { bestDist = d; best = s; }
                }
                return best?.MapFaction;
            }
            catch { return null; }
        }

        private static Hero FindHero(string id)
            => string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h.StringId == id);

        #endregion
    }
}
