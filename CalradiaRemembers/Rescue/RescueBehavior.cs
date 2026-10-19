// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Rescue;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Rescue
{
    /// <summary>
    ///   Freeing a captive lord by force is a strong commitment, and the world now remembers it. When the player
    ///   wins the battle or siege that frees a non-bandit lord (not one of the player's own clan), the freed lord
    ///   and their close family record a lasting first-person memory and warm toward the player; a few close
    ///   friends of the rescued lord warm a little too. The captor whose prize was taken has a chance to trace
    ///   the deed back to the player and, if they do, nurses a grudge over it (a public siege is easier to trace
    ///   and openly resented). All the numbers and gates live in pure, tested policies in
    ///   <see cref="CalradiaRemembers.Logic.Rescue" /> and <c>CaptorGrudgePolicy</c>.
    /// </summary>
    public sealed class RescueBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_Rescues_v1";

        // zhenja1 (10/10/2026): lords the player took from a beaten foe who held them, by hero id: who held them, and
        // the day. Let go by the player's choice within RescueFromFoePolicy.WindowDays, they were rescued.
        private const string TakenFromFoeSaveKey = "CalradiaRemembers_TakenFromFoe_v1";
        private const int MaxFriends = 5;
        private const int MaxPendingBuffer = 64;

        public static RescueBehavior Instance;

        // Battle-freed lords buffered as HeroPrisonerReleased fires (during resolution), then resolved on the
        // following MapEventEnded once we know whether the player won and whose side was defeated.
        private readonly List<PendingRelease> _pending = new List<PendingRelease>();

        // Once-per-lord: a lord already thanked for a rescue is not credited again (persisted across saves).
        private HashSet<string> _credited = new HashSet<string>();

        /// <summary>The player's battle under way: the lords its enemies held, by hero id, read when it began.</summary>
        private readonly Dictionary<string, HeldBy> _heldByFoeThisBattle = new Dictionary<string, HeldBy>();

        /// <summary>Lords the player took from a beaten foe and still holds, by hero id (saved).</summary>
        private Dictionary<string, HeldBy> _takenFromFoe = new Dictionary<string, HeldBy>();

        public RescueBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Additive, migration-safe: a newline-joined list of credited lord ids (tiny, well under the native
            // string serializer's limit). Absent on an old save, it simply loads empty.
            string blob = _credited.Count > 0 ? string.Join("\n", _credited) : "";
            dataStore.SyncData(SaveKey, ref blob);

            if (dataStore.IsLoading)
                _credited = string.IsNullOrEmpty(blob)
                    ? new HashSet<string>()
                    : new HashSet<string>(blob.Split('\n'));

            // Additive (10/10/2026): one line per lord, "id|captor label|settlement|faction|day"; absent on older saves.
            string taken = string.Join("\n", _takenFromFoe.Select(kv => string.Join("|", kv.Key, Clean(kv.Value.CaptorLabel), Clean(kv.Value.SettlementName), Clean(kv.Value.FactionName), kv.Value.Day)));
            dataStore.SyncData(TakenFromFoeSaveKey, ref taken);
            if (dataStore.IsLoading)
            {
                _takenFromFoe = new Dictionary<string, HeldBy>();
                foreach (string line in (taken ?? "").Split('\n'))
                {
                    string[] p = line.Split('|');
                    if (p.Length == 5 && int.TryParse(p[4], out int day))
                        _takenFromFoe[p[0]] = new HeldBy {CaptorLabel = Null(p[1]), SettlementName = Null(p[2]), FactionName = Null(p[3]), Day = day};
                }
            }
        }

        /// <summary>
        ///   Forces the full rescue chain on <paramref name="lord" /> as if the player had just freed them by a
        ///   public siege, ignoring battle detection and the once-per-lord gate. For <c>cr.rescue</c> testing.
        /// </summary>
        public string ForceRescueForTest(Hero lord)
        {
            if (lord == null) return "No such lord.";
            _credited.Remove(lord.StringId);
            var today = (int) CampaignTime.Now.ToDays;
            CreditRescue(lord, lord.PartyBelongedToAsPrisoner, lord.MapFaction, true, today);

            return $"Rescue chain applied for {lord.Name} (self + family + friends grateful; captor grudge rolled).";
        }

        #region private

        private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction faction, EndCaptivityDetail detail, bool showNotification)
        {
            // TWO ways a beaten captor's prisoner goes free, and we listened for one. The engine frees them
            // itself (ReleasedAfterBattle) only when no winner wants them; when the winner is the PLAYER, vanilla
            // opens the free-or-capture conversation instead and letting them go is ReleasedByChoice. That is the
            // common case, and the one Lordfadooboo hit (Nexus, 20/09/2026): Lady Mesui thanked him on the spot,
            // in vanilla, and remembered nothing of it afterwards. The battle guard downstream
            // (BelongsToDefeatedSide) is what keeps this a rescue: freeing someone out of the player's OWN
            // dungeon leaves their captor party on the winning side, so it is never credited here.
            // The game's own prison break, with the player named as the one who did it (01/10/2026): a rescue
            // with no battle to wait for, credited at once. A break is done by stealth, so it is not public.
            if (PlayerPrisonBreakPatch.IsUnderway(prisoner, out _))
            {
                CreditRescue(prisoner, party, faction, false, (int) CampaignTime.Now.ToDays, brokenOut: true);

                return;
            }

            // Whatever ends their captivity (a ransom, an escape, a release), the mark of a lord taken from a foe goes.
            HeldBy taken = null;
            if (prisoner != null && _takenFromFoe.TryGetValue(prisoner.StringId, out taken)) _takenFromFoe.Remove(prisoner.StringId);

            if (!RescueEligibilityPolicy.CanBeARescue(KindOf(detail))) return;
            if (prisoner == null || !prisoner.IsLord || prisoner == Hero.MainHero) return;

            // zhenja1 (10/10/2026): taken from a beaten foe and let go later from the player's own custody: a rescue,
            // remembered with who held them. The battle is over by then, so it is credited at once.
            int today = (int) CampaignTime.Now.ToDays;
            // Only a release from the PLAYER's custody (their party, the loot not yet in a party, a dungeon of theirs): a
            // lord the foe kept and let go of himself owes the player nothing.
            bool fromPlayersCustody = party == null || party == PartyBase.MainParty || party.Owner == Hero.MainHero
                                      || party.Settlement?.OwnerClan == Clan.PlayerClan;
            if (taken != null && fromPlayersCustody)
            {
                if (RescueFromFoePolicy.IsRescue(new CustodyReleaseFacts {ReleasedByChoice = detail == EndCaptivityDetail.ReleasedByChoice, TakenFromFoeOnDay = taken.Day, Today = today}))
                    CreditRescue(prisoner, null, null, false, today, heldBy: taken);

                return;
            }

            // Freed from the player's loot during the battle the foe lost: who held them was read when it began.
            _heldByFoeThisBattle.TryGetValue(prisoner.StringId, out HeldBy heldBy);
            _pending.Add(new PendingRelease {Prisoner = prisoner, CaptorParty = party, CaptorFaction = faction, HeldBy = heldBy});
            if (_pending.Count > MaxPendingBuffer) _pending.RemoveAt(0);
        }

        /// <summary>The engine's own reason for the release, as the pure policy names it.</summary>
        private static RescueEligibilityPolicy.ReleaseKind KindOf(EndCaptivityDetail detail)
            => detail switch {
                EndCaptivityDetail.ReleasedAfterBattle => RescueEligibilityPolicy.ReleaseKind.AfterBattle,
                EndCaptivityDetail.ReleasedByChoice => RescueEligibilityPolicy.ReleaseKind.ByChoice,
                _ => RescueEligibilityPolicy.ReleaseKind.Other
            };

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent == null || (_pending.Count == 0 && (!mapEvent.IsPlayerMapEvent || _heldByFoeThisBattle.Count == 0))) return;

                bool playerWon = mapEvent.IsPlayerMapEvent && mapEvent.HasWinner
                                                           && mapEvent.PlayerSide != BattleSideEnum.None
                                                           && mapEvent.WinningSide == mapEvent.PlayerSide;
                if (!playerWon)
                {
                    _pending.Clear();

                    return;
                }

                bool publicAction = mapEvent.IsSiegeOutside; // storming a settlement is public; a field win is not
                HashSet<PartyBase> defeatedParties = CollectDefeated(mapEvent, out HashSet<string> defeatedFactions);
                var today = (int) CampaignTime.Now.ToDays;

                // Lords the player took from the beaten foe and keeps for now: a release later, within the window, is a rescue.
                foreach (KeyValuePair<string, HeldBy> held in _heldByFoeThisBattle)
                {
                    Hero h = Hero.FindFirst(x => x.StringId == held.Key);
                    // Still a prisoner when the battle ends: in the player's loot, then their party or a dungeon of theirs.
                    if (h != null && h.IsPrisoner)
                        _takenFromFoe[held.Key] = new HeldBy {CaptorLabel = held.Value.CaptorLabel, CaptorId = held.Value.CaptorId, SettlementName = held.Value.SettlementName, FactionName = held.Value.FactionName, Day = today};
                }

                foreach (PendingRelease pr in _pending)
                {
                    if (pr.HeldBy == null && !BelongsToDefeatedSide(pr, defeatedParties, defeatedFactions)) continue;
                    CreditRescue(pr.Prisoner, pr.CaptorParty, pr.CaptorFaction, publicAction, today, heldBy: pr.HeldBy);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[RESCUE] resolve failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _pending.Clear();
                if (mapEvent != null && mapEvent.IsPlayerMapEvent) _heldByFoeThisBattle.Clear();
            }
        }

        /// <summary>
        ///   zhenja1 (10/10/2026): the lords the player's enemies hold when a battle of the player's begins. The game hands
        ///   a beaten party's prisoners to the player's loot, so who held them must be read before the battle moves them.
        /// </summary>
        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            try
            {
                if (mapEvent == null) return;
                BattleSideEnum enemy = attackerParty == PartyBase.MainParty ? BattleSideEnum.Defender
                    : defenderParty == PartyBase.MainParty ? BattleSideEnum.Attacker : BattleSideEnum.None;
                if (enemy == BattleSideEnum.None) return;

                _heldByFoeThisBattle.Clear();
                foreach (MapEventParty mp in mapEvent.PartiesOnSide(enemy))
                {
                    PartyBase holder = mp?.Party;
                    if (holder?.PrisonRoster == null) continue;
                    foreach (TroopRosterElement e in holder.PrisonRoster.GetTroopRoster())
                    {
                        Hero held = e.Character?.HeroObject;
                        if (held == null || held == Hero.MainHero || !held.IsLord) continue;
                        _heldByFoeThisBattle[held.StringId] = HeldByOf(holder, (int) CampaignTime.Now.ToDays);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[RESCUE] battle start read failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>Who holds a prisoner in this party, as the rescued would name it.</summary>
        private static HeldBy HeldByOf(PartyBase holder, int day)
        {
            Hero captor = ResolveCaptorHero(holder, holder?.MapFaction);
            return new HeldBy {
                CaptorLabel = captor == null ? null : HeroLabel.Describe(new HeroLabel.HeroNaming {Name = captor.Name?.ToString(), ClanName = captor.Clan?.Name?.ToString(), CultureName = captor.Culture?.Name?.ToString()}),
                CaptorId = captor?.StringId, SettlementName = holder?.Settlement?.Name?.ToString(), FactionName = holder?.MapFaction?.Name?.ToString(), Day = day
            };
        }

        private static string Clean(string s) => (s ?? "").Replace("|", " ").Replace("\n", " ");
        private static string Null(string s) => string.IsNullOrEmpty(s) ? null : s;

        private void CreditRescue(Hero prisoner, PartyBase captorParty, IFaction captorFaction, bool publicAction, int today, bool brokenOut = false, HeldBy heldBy = null)
        {
            if (prisoner == null || _credited.Contains(prisoner.StringId)) return;

            bool prisonerIsBandit = prisoner.Clan != null && prisoner.Clan.IsBanditFaction;
            bool inPlayerClan = prisoner.Clan == Hero.MainHero?.Clan;
            if (!RescueEligibilityPolicy.IsPlayerRescue(new RescueEligibilityPolicy.ReleaseFacts {PlayerWonBattle = !brokenOut, ReleasedAfterBattle = !brokenOut, BrokenOutByPlayer = brokenOut, PrisonerIsLord = prisoner.IsLord, PrisonerIsBandit = prisonerIsBandit, PrisonerInPlayerClan = inPlayerClan, PrisonerIsPlayer = prisoner == Hero.MainHero}))
                return;

            _credited.Add(prisoner.StringId);

            // zhenja1 (10/10/2026): the memory names the lord who held them, not only the realm.
            HeldBy who = heldBy ?? HeldByOf(captorParty, today);
            if (heldBy == null && captorParty == null && captorFaction != null) who = new HeldBy {FactionName = captorFaction.Name?.ToString(), Day = today};
            string captorDesc = RescueFromFoePolicy.CaptorPhrase(new RescueCaptorFacts {CaptorLabel = who.CaptorLabel, SettlementName = who.SettlementName, FactionName = who.FactionName});
            List<(Hero hero, string label)> family = ResolveFamily(prisoner);
            var familyIds = new HashSet<string>(family.Select(f => f.hero.StringId));

            // 1. The rescued lord.
            ApplyGratitude(prisoner, RescueTie.RescuedSelf,
                $"You freed me from {captorDesc}. I will not forget that you came for me.", today);

            // 2. Their close family.
            foreach ((Hero rel, string label) in family)
                ApplyGratitude(rel, RescueTie.Family,
                    $"You freed my {label} {prisoner.Name} from {captorDesc}. My house owes you a debt.", today);

            // 3. A few close friends (regard only, no memory).
            foreach (Hero friend in ResolveFriends(prisoner, familyIds))
                ApplyGratitude(friend, RescueTie.Friend, null, today);

            // 4. The captor, if they trace it back to the player, resents it.
            Hero captor = (who.CaptorId != null ? Hero.FindFirst(h => h.StringId == who.CaptorId) : null) ?? ResolveCaptorHero(captorParty, captorFaction);
            if (captor != null && captor != Hero.MainHero && captor.IsAlive && GrudgeBehavior.Instance != null
                && CaptorGrudgePolicy.Discovers(MBRandom.RandomFloat, publicAction))
            {
                GrudgeBehavior.Instance.TryPlant(captor, Hero.MainHero, GrudgeSource.PrisonerRescued,
                    $"tearing {prisoner.Name} out of my hands after I had taken them prisoner",
                    CaptorGrudgePolicy.GrudgeMagnitude(publicAction),
                    CaptorGrudgePolicy.KnownToPlayer(publicAction), today);
            }

            ModLog.Info($"[RESCUE] player freed {prisoner.Name} from {captorDesc} " +
                        $"(family {family.Count}, public={publicAction}).");
        }

        private static void ApplyGratitude(Hero hero, RescueTie tie, string memoryText, int today)
        {
            if (hero == null || hero == Hero.MainHero || !hero.IsAlive) return;

            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return;

            NpcProfile profile = store.Get(hero.StringId);
            if (profile == null)
            {
                if (tie == RescueTie.Friend) return; // do not spawn a profile just for a minor friend
                profile = HeroProfileMapper.FromHero(hero);
            }

            RescueGratitude g = RescueGratitudePolicy.GratitudeFor(tie);
            if (g.Magnitude != 0) ProfileMutator.ApplyReputationDelta(profile, g.Magnitude, NpcMemoryService.Core.Standing.RegardCause.Rescue);
            if (g.RecordMemory && !string.IsNullOrEmpty(memoryText))
                profile.Events.Add(new NotableEvent(today, NotableEventType.Other, memoryText));

            store.Set(profile);
        }

        private static List<(Hero hero, string label)> ResolveFamily(Hero prisoner)
        {
            var list = new List<(Hero hero, string label)>();

            string child = prisoner.IsFemale ? "daughter" : "son";
            string parent = prisoner.IsFemale ? "mother" : "father";
            string sibling = prisoner.IsFemale ? "sister" : "brother";
            string spouse = prisoner.IsFemale ? "wife" : "husband";

            void Add(Hero h, string label)
            {
                if (h == null || !h.IsAlive || h == Hero.MainHero || h == prisoner) return;
                if (!RescueGratitudePolicy.CanFeelTheDebt(h.IsChild)) return;
                if (list.Any(x => x.hero == h)) return;
                list.Add((h, label));
            }

            Add(prisoner.Spouse, spouse);
            Add(prisoner.Father, child);
            Add(prisoner.Mother, child);
            if (prisoner.Children != null)
                foreach (Hero c in prisoner.Children)
                    Add(c, parent);
            if (prisoner.Siblings != null)
                foreach (Hero s in prisoner.Siblings)
                    Add(s, sibling);
            Add(prisoner.Clan?.Leader, "kin");

            return list;
        }

        private static List<Hero> ResolveFriends(Hero prisoner, HashSet<string> excludeIds)
        {
            return Hero.AllAliveHeroes
                .Where(h => h != prisoner && h != Hero.MainHero && h.IsLord && !h.IsHumanPlayerCharacter)
                .Where(h => RescueGratitudePolicy.CanFeelTheDebt(h.IsChild))
                .Where(h => !excludeIds.Contains(h.StringId))
                .Where(h => RescueGratitudePolicy.QualifiesAsFriend(new NpcMemoryService.Core.Standing.VanillaRelation(prisoner.GetRelation(h))))
                .OrderByDescending(h => prisoner.GetRelation(h))
                .Take(MaxFriends)
                .ToList();
        }

        private static Hero ResolveCaptorHero(PartyBase captorParty, IFaction captorFaction)
            => captorParty?.LeaderHero
               ?? captorParty?.Settlement?.OwnerClan?.Leader
               ?? captorFaction?.Leader;

        private static HashSet<PartyBase> CollectDefeated(MapEvent mapEvent, out HashSet<string> defeatedFactions)
        {
            var parties = new HashSet<PartyBase>();
            defeatedFactions = new HashSet<string>();

            MapEventSide side = mapEvent.GetMapEventSide(mapEvent.DefeatedSide);
            if (side?.Parties == null) return parties;

            foreach (MapEventParty mp in side.Parties)
            {
                PartyBase p = mp.Party;
                if (p == null) continue;
                parties.Add(p);
                string f = p.MapFaction?.StringId;
                if (!string.IsNullOrEmpty(f)) defeatedFactions.Add(f);
            }

            return parties;
        }

        private static bool BelongsToDefeatedSide(PendingRelease pr, HashSet<PartyBase> defeatedParties, HashSet<string> defeatedFactions)
        {
            if (pr.CaptorParty != null && defeatedParties.Contains(pr.CaptorParty)) return true;
            string f = pr.CaptorFaction?.StringId;

            return !string.IsNullOrEmpty(f) && defeatedFactions.Contains(f);
        }

        private struct PendingRelease
        {
            public Hero Prisoner;
            public PartyBase CaptorParty;
            public IFaction CaptorFaction;
            public HeldBy HeldBy;
        }

        /// <summary>Who held a lord the player freed, and when the player took them.</summary>
        private sealed class HeldBy
        {
            public string CaptorLabel;
            public string CaptorId;
            public string SettlementName;
            public string FactionName;
            public int Day;
        }

        /// <summary>Self-test seam: a lord marked as taken from a beaten foe on <paramref name="day" />, held by <paramref name="captor" />.</summary>
        internal void DebugMarkTakenFromFoe(Hero lord, Hero captor, int day)
            => _takenFromFoe[lord.StringId] = new HeldBy {CaptorLabel = captor?.Name?.ToString(), CaptorId = captor?.StringId, FactionName = captor?.MapFaction?.Name?.ToString(), Day = day};

        /// <summary>Self-test seam: the release handling, as the game's ReleasedByChoice would call it.</summary>
        internal void DebugReleasedByChoice(Hero lord) => OnHeroPrisonerReleased(lord, PartyBase.MainParty, Hero.MainHero?.MapFaction, EndCaptivityDetail.ReleasedByChoice, false);

        /// <summary>Self-test seam: forget a credited rescue (cleanup).</summary>
        internal void DebugForgetCredit(Hero lord) => _credited.Remove(lord.StringId);

        #endregion
    }
}
