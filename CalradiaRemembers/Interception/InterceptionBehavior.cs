// Code written by Gabriel Mailhot, 21/06/2026.
// NPC-initiated interceptions: a nearby lord may ride out to the player rather than wait to be
// sought. The decision (who, why, the guards) is pure and unit-tested in
// CalradiaRemembers.Logic.InterceptionPolicy; this class is the engine glue (scan, pursue, notify).
// The "heard of a deed" trigger reads the world-event/scheme rumour store, so the simulation we built
// gives lords reasons to come for the player.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
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
        private const int MaxCandidates = 12;            // bound the per-day work
        private const int RecentDeedWindowDays = 6;

        private const string CooldownSaveKey = "CalradiaRemembers_Intercept_v1bytes";

        private string _activePursuerId;
        private InterceptionTrigger _activePursuerTrigger = InterceptionTrigger.None;
        private int _activePursuerSince = -1;
        private readonly Dictionary<string, int> _cooldown = new Dictionary<string, int>();

        public InterceptionBehavior() => Instance = this;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        /// <summary>
        ///   Persists only the per-lord cooldowns (as a UTF-8 byte[], like the other CR stores), so a
        ///   lord cannot intercept the player again right after a reload. The active pursuit itself is
        ///   transient by design — a mid-pursuit reload simply lets the lord's AI return to its plans.
        /// </summary>
        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_cooldown, Formatting.None)); }
                catch (Exception ex)
                {
                    ModLog.Error($"[INTERCEPT] serialize failed — persisting no cooldowns this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("{}");
                }

                try { dataStore.SyncData(CooldownSaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[INTERCEPT] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(CooldownSaveKey, ref bytes); }
                catch { /* key absent in older saves */ }

                if (bytes != null && bytes.Length > 0)
                {
                    try
                    {
                        var loaded = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(bytes));
                        _cooldown.Clear();
                        if (loaded != null)
                            foreach (KeyValuePair<string, int> kv in loaded) _cooldown[kv.Key] = kv.Value;
                    }
                    catch (Exception ex)
                    {
                        _cooldown.Clear();
                        ModLog.Error($"[INTERCEPT] load failed — cooldowns dropped for this load. {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///   If <paramref name="npc"/> is the lord currently intercepting the player, returns a
        ///   one-shot opening directive for the conversation (consuming it so it only colours the
        ///   first turn) and ends the pursuit. Null otherwise. Called as the CR chat context is built.
        /// </summary>
        public string ConsumeInterceptionReason(Hero npc)
        {
            if (npc == null || _activePursuerId == null || npc.StringId != _activePursuerId) return null;

            InterceptionTrigger trigger = _activePursuerTrigger;
            _activePursuerId = null;
            _activePursuerSince = -1;
            _activePursuerTrigger = InterceptionTrigger.None;

            return trigger == InterceptionTrigger.TerritorialPatrol
                ? "It is YOU who sought this meeting: you rode out to challenge the player for crossing your realm's " +
                  "lands. Open on that footing — demand their business here, wary and territorial — rather than waiting on them."
                : "It is YOU who sought this meeting: word of the player's recent deeds reached you and you rode out to " +
                  "see them while it was fresh. Open by raising what you heard — be it admiration, alarm, or a reckoning.";
        }

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

            // NB: do NOT clear on arrival — the pursuit ends when the player actually talks to them
            // (ConsumeInterceptionReason) so the opener survives the meeting. Otherwise the pure rule
            // gives up a stale chase (timeout) or a hopeless one (the player is well clear and faster).
            bool invalid = pursuer == null || !pursuer.IsAlive || pursuerParty == null || main == null;
            bool giveUp = !invalid && InterceptionPolicy.ShouldAbandonChase(
                pursuerParty.Speed, main.Speed,
                pursuerParty.GetPosition2D.Distance(main.GetPosition2D),
                today - _activePursuerSince);

            if (invalid || giveUp)
            {
                if (giveUp) ModLog.Info($"[INTERCEPT] {pursuer?.Name} gives up the chase.");
                _activePursuerId = null;
                _activePursuerSince = -1;
                _activePursuerTrigger = InterceptionTrigger.None;
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
            _activePursuerTrigger = trigger;
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
