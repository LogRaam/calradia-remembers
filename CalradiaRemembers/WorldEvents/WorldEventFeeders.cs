// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 (world-event / rumor store): mechanical feeders — campaign-event listeners that turn real
// happenings into WorldEvents (zero LLM). Player deeds also leave a memory in the profiles of the
// lords who were present, so they simply KNOW you fought well rather than waiting to be told.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Stance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.WorldEvents
{
    /// <summary>
    ///   Subscribes to the campaign events that matter for rumor and chronicle, and records a
    ///   <see cref="WorldEvent"/> for each. Owned by <see cref="NpcMemoryCampaignBehavior"/> and
    ///   wired on session launch (mirrors <c>LetterTriggerEvaluator.Subscribe</c>).
    /// </summary>
    public sealed class WorldEventFeeders
    {
        // ── Rivalry gossip (feeder #3) ────────────────────────────────────────
        private const int RivalryThreshold       = -30; // mutual relation at/below = bitter rivals
        private const int MaxRivalriesPerWeek     = 2;   // ambient, not a flood
        private const int RivalryCooldownDays     = 40;  // don't re-gossip the same feud too soon
        private const int MaxLordsPerKingdomScan  = 40;  // bound the O(n^2) intra-kingdom pass

        private readonly Dictionary<string, int> _rivalryGossipDay = new Dictionary<string, int>();

        public void Subscribe()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnHeroesMarried);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            ModLog.Info("WorldEventFeeders subscribed.");
        }

        /// <summary>
        ///   Once a week, surface a couple of bitter intra-kingdom rivalries as ambient gossip —
        ///   feuding vassals of the same crown, inferred from their native standing. At most one
        ///   per kingdom per pass, each pairing on a cooldown so it does not repeat too soon.
        /// </summary>
        private void OnWeeklyTick()
        {
            if (Store == null) return;
            int today = Today;
            int emitted = 0;

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (emitted >= MaxRivalriesPerWeek) break;

                var lords = new List<Hero>();
                foreach (Clan clan in kingdom.Clans)
                {
                    foreach (Hero h in clan.Heroes)
                        if (h != null && h.IsAlive && h.IsLord && h != Hero.MainHero)
                            lords.Add(h);
                    if (lords.Count >= MaxLordsPerKingdomScan) break;
                }

                if (TryEmitKingdomRivalry(lords, kingdom, today)) emitted++;
            }
        }

        private bool TryEmitKingdomRivalry(List<Hero> lords, Kingdom kingdom, int today)
        {
            for (int i = 0; i < lords.Count; i++)
                for (int j = i + 1; j < lords.Count; j++)
                {
                    if (lords[i].GetRelation(lords[j]) > RivalryThreshold) continue;

                    string key = PairKey(lords[i], lords[j]);
                    if (_rivalryGossipDay.TryGetValue(key, out int last) && today - last < RivalryCooldownDays) continue;

                    _rivalryGossipDay[key] = today;
                    Store.Record(new WorldEvent {
                        Type       = WorldEventType.Rivalry,
                        Day        = today,
                        Magnitude  = 22,
                        Summary    = $"{lords[i].Name} and {lords[j].Name} are at bitter odds.",
                        ActorIds   = Ids(lords[i], lords[j]),
                        FactionIds = kingdom?.StringId != null ? new List<string> { kingdom.StringId } : new List<string>()
                    });
                    return true;
                }
            return false;
        }

        private static string PairKey(Hero a, Hero b)
            => string.CompareOrdinal(a.StringId, b.StringId) < 0
                ? a.StringId + "|" + b.StringId
                : b.StringId + "|" + a.StringId;

        private static WorldEventStore Store => WorldEventStore.Instance;
        private static int Today => (int) CampaignTime.Now.ToDays;
        private static string PlayerName => Hero.MainHero?.Name?.ToString() ?? "the player";

        // ── Battles ───────────────────────────────────────────────────────────

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || Store == null) return;
            try
            {
                if (mapEvent.WinningSide == BattleSideEnum.None) return;

                // Only a siege or a clash of two armies is worth recording (into world-events, the
                // chronicle, and lords' profiles) — a field skirmish or a bandit chase is noise that
                // would bloat the save. This also skips the per-lord profile memory + stance bumps below.
                if (!Logic.BattleNotability.IsNotable(
                        mapEvent.EventType == MapEvent.BattleTypes.Siege,
                        SideHasArmy(mapEvent, BattleSideEnum.Attacker),
                        SideHasArmy(mapEvent, BattleSideEnum.Defender)))
                    return;

                BattleSideEnum loseSide = mapEvent.WinningSide == BattleSideEnum.Attacker
                    ? BattleSideEnum.Defender
                    : BattleSideEnum.Attacker;

                Hero winLeader = SideLeader(mapEvent, mapEvent.WinningSide);
                Hero loseLeader = SideLeader(mapEvent, loseSide);

                // Need at least one real lord, and skip bandit-only affairs.
                bool winLord = winLeader is { IsLord: true } && !IsBandit(winLeader);
                bool loseLord = loseLeader is { IsLord: true } && !IsBandit(loseLeader);
                if (!winLord && !loseLord) return;

                bool playerAttacker = mapEvent.AttackerSide?.IsMainPartyAmongParties() == true;
                bool playerDefender = mapEvent.DefenderSide?.IsMainPartyAmongParties() == true;
                bool playerIn = playerAttacker || playerDefender;
                bool playerWon = playerIn && mapEvent.WinningSide == (playerAttacker ? BattleSideEnum.Attacker : BattleSideEnum.Defender);

                Settlement place = mapEvent.MapEventSettlement;
                string near = place != null ? $" near {place.Name}" : "";
                string winName = LeaderName(winLeader);
                string loseName = LeaderName(loseLeader);

                var e = new WorldEvent
                {
                    Type           = playerIn ? WorldEventType.PlayerFeat : WorldEventType.Battle,
                    Day            = Today,
                    PlaceId        = place?.StringId,
                    Magnitude      = playerIn ? Math.Max(40, BattleMagnitude(mapEvent)) : BattleMagnitude(mapEvent),
                    InvolvesPlayer = playerIn,
                    Summary        = $"{winName} defeated {loseName} in battle{near}.",
                    ActorIds       = Ids(winLeader, loseLeader),
                    FactionIds     = FactionIds(winLeader, loseLeader),
                    // Influence loop: a battle the player WON harms the beaten side; a loss harms no one
                    // by the player's hand, so it aggrieves nobody (the foe who won is not the player's deed).
                    AggrievedFactionIds = playerIn && playerWon ? FactionIds(loseLeader) : new List<string>(),
                    AggrievedActorIds   = playerIn && playerWon ? Ids(loseLeader) : new List<string>()
                };
                if (playerIn)
                    e.ChronicleLine = playerWon
                        ? $"the lord {PlayerName} met {loseName} in open battle{near}, and by sword and valour put the foe to flight"
                        : $"the lord {PlayerName} gave battle to {winName}{near}, but fortune turned against him and the field was lost";

                Store.Record(e);

                if (playerIn) RecordBattlePerformanceIntoProfiles(mapEvent, playerAttacker, playerWon, near);
            }
            catch (Exception ex) { ModLog.Error($"[WORLD] battle feeder failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        /// <summary>Leaves a memory of the player's battlefield showing in the profiles of lords who were present.</summary>
        private static void RecordBattlePerformanceIntoProfiles(MapEvent mapEvent, bool playerAttacker, bool playerWon, string near)
        {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior?.Store == null) return;

            BattleSideEnum playerSide = playerAttacker ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
            int day = Today;

            foreach (BattleSideEnum side in new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender })
            {
                var parties = mapEvent.GetMapEventSide(side)?.Parties;
                if (parties == null) continue;
                bool ally = side == playerSide;

                foreach (MapEventParty mp in parties)
                {
                    Hero h = mp.Party?.LeaderHero;
                    if (h == null || h == Hero.MainHero || !h.IsLord || IsBandit(h)) continue;
                    NpcProfile profile = behavior.Store.Get(h.StringId);
                    if (profile == null) continue; // only lords the player has actually met

                    // NB: the enemy-lost phrase "put their host to flight" is also read by
                    // EncounterContextBuilder.LostBattleToPlayerRecently to feed the POWER BALANCE
                    // note — keep the two in sync if this wording changes.
                    string summary = ally
                        ? $"Fought at your side when you {(playerWon ? "broke the enemy" : "stood against the enemy")}{near}."
                        : $"Faced you in battle and {(playerWon ? "saw you put their host to flight" : "held the field against you")}{near}.";
                    profile.Events.Add(new NotableEvent(day, NotableEventType.Other, summary));

                    // Stance: a victory the player won earns respect (and, from a beaten foe, fear).
                    if (playerWon)
                    {
                        if (ally) StanceService.OnFoughtBesideYou(profile);
                        else StanceService.OnDefeatedInBattle(profile);
                    }

                    behavior.Store.Set(profile);
                }
            }
        }

        // ── Marriages ─────────────────────────────────────────────────────────

        private void OnHeroesMarried(Hero a, Hero b, bool showNotification)
        {
            if (a == null || b == null || Store == null) return;
            try
            {
                bool playerIn = a == Hero.MainHero || b == Hero.MainHero;
                Hero other = a == Hero.MainHero ? b : a;
                var e = new WorldEvent
                {
                    Type           = playerIn ? WorldEventType.PlayerFeat : WorldEventType.Marriage,
                    Day            = Today,
                    Magnitude      = playerIn ? 45 : 35,
                    InvolvesPlayer = playerIn,
                    Summary        = $"{a.Name} wed {b.Name}.",
                    ActorIds       = Ids(a, b),
                    FactionIds     = FactionIds(a, b)
                };
                if (playerIn)
                    e.ChronicleLine = $"{PlayerName} was joined in marriage to {other.Name}, that two houses might be made one";
                Store.Record(e);
            }
            catch (Exception ex) { ModLog.Error($"[WORLD] marriage feeder failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        // ── Executions ────────────────────────────────────────────────────────

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            if (victim == null || Store == null) return;
            if (detail != KillCharacterAction.KillCharacterActionDetail.Executed) return; // only deliberate executions are news
            try
            {
                bool playerIn = killer == Hero.MainHero;
                var e = new WorldEvent
                {
                    Type           = playerIn ? WorldEventType.PlayerFeat : WorldEventType.Execution,
                    Day            = Today,
                    Magnitude      = 65,
                    InvolvesPlayer = playerIn,
                    Summary        = killer != null ? $"{killer.Name} had {victim.Name} executed." : $"{victim.Name} was executed.",
                    ActorIds       = Ids(victim, killer),
                    FactionIds     = FactionIds(victim, killer),
                    // Influence loop: an execution by the player wounds the victim's own faction deeply.
                    AggrievedFactionIds = playerIn ? FactionIds(victim) : new List<string>(),
                    AggrievedActorIds   = playerIn ? Ids(victim) : new List<string>()
                };
                if (playerIn)
                    e.ChronicleLine = $"by the judgement of {PlayerName}, {victim.Name} was put to death, and men spoke of it long after";
                Store.Record(e);
            }
            catch (Exception ex) { ModLog.Error($"[WORLD] execution feeder failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        // ── Sieges / conquests ────────────────────────────────────────────────

        private void OnSettlementOwnerChanged(
            Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement == null || Store == null) return;
            if (detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return; // only conquests
            try
            {
                Hero taker = capturerHero ?? newOwner;
                bool playerIn = taker == Hero.MainHero;
                var e = new WorldEvent
                {
                    Type           = playerIn ? WorldEventType.PlayerFeat : WorldEventType.SettlementCaptured,
                    Day            = Today,
                    PlaceId        = settlement.StringId,
                    Magnitude      = settlement.IsTown ? 70 : settlement.IsCastle ? 55 : 35,
                    InvolvesPlayer = playerIn,
                    Summary        = $"{taker?.Name?.ToString() ?? "A host"} took {settlement.Name}" +
                                     (oldOwner != null ? $" from {oldOwner.Name}." : "."),
                    ActorIds       = Ids(taker, oldOwner),
                    FactionIds     = FactionIds(taker, oldOwner),
                    // Influence loop: a conquest by the player wounds the dispossessed lord's faction.
                    AggrievedFactionIds = playerIn ? FactionIds(oldOwner) : new List<string>(),
                    AggrievedActorIds   = playerIn ? Ids(oldOwner) : new List<string>()
                };
                if (playerIn)
                    e.ChronicleLine = $"{PlayerName} laid siege to {settlement.Name} and wrested it from {oldOwner?.Name?.ToString() ?? "its lord"}, and the banners were changed upon its walls";
                Store.Record(e);
            }
            catch (Exception ex) { ModLog.Error($"[WORLD] siege feeder failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        // ── Wars & peace (realm-level rumor, not personal deeds) ───────────────

        private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
            => RecordDiplomacy(faction1, faction2, WorldEventType.WarDeclared, 75, "declared war on");

        private void OnMakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
            => RecordDiplomacy(faction1, faction2, WorldEventType.PeaceDeclared, 60, "made peace with");

        private static void RecordDiplomacy(IFaction f1, IFaction f2, WorldEventType type, int magnitude, string verb)
        {
            if (f1 == null || f2 == null || Store == null) return;
            try
            {
                Store.Record(new WorldEvent
                {
                    Type       = type,
                    Day        = Today,
                    Magnitude  = magnitude,
                    Summary    = $"{f1.Name} {verb} {f2.Name}.",
                    FactionIds = new List<string> { f1.StringId, f2.StringId }.Where(s => s != null).ToList()
                });
            }
            catch (Exception ex) { ModLog.Error($"[WORLD] diplomacy feeder failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>True when any party on this side of the battle belongs to an army.</summary>
        private static bool SideHasArmy(MapEvent mapEvent, BattleSideEnum side)
        {
            var parties = mapEvent.GetMapEventSide(side)?.Parties;
            if (parties == null) return false;
            foreach (MapEventParty p in parties)
                if (p.Party?.MobileParty?.Army != null) return true;
            return false;
        }

        private static Hero SideLeader(MapEvent mapEvent, BattleSideEnum side)
        {
            MapEventSide s = mapEvent.GetMapEventSide(side);
            Hero leader = s?.LeaderParty?.LeaderHero;
            if (leader != null) return leader;
            return s?.Parties?.Select(p => p.Party?.LeaderHero).FirstOrDefault(h => h is { IsLord: true });
        }

        // A battle fought over a settlement (a siege assault) carries further than a field skirmish.
        private static int BattleMagnitude(MapEvent mapEvent)
            => mapEvent.MapEventSettlement != null ? 45 : 35;

        private static bool IsBandit(Hero h) => h?.MapFaction?.IsBanditFaction == true;

        private static string LeaderName(Hero h) => h?.Name?.ToString() ?? "a host";

        private static List<string> Ids(params Hero[] heroes)
            => heroes.Where(h => h != null).Select(h => h.StringId).ToList();

        private static List<string> FactionIds(params Hero[] heroes)
            => heroes.Where(h => h?.MapFaction != null).Select(h => h.MapFaction.StringId).Distinct().ToList();
    }
}
