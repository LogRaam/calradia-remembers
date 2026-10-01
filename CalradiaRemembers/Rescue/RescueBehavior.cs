// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Rescue;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
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
        private const int MaxFriends = 5;
        private const int MaxPendingBuffer = 64;

        public static RescueBehavior Instance;

        // Battle-freed lords buffered as HeroPrisonerReleased fires (during resolution), then resolved on the
        // following MapEventEnded once we know whether the player won and whose side was defeated.
        private readonly List<PendingRelease> _pending = new List<PendingRelease>();

        // Once-per-lord: a lord already thanked for a rescue is not credited again (persisted across saves).
        private HashSet<string> _credited = new HashSet<string>();

        public RescueBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
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

            if (!RescueEligibilityPolicy.CanBeARescue(KindOf(detail))) return;
            if (prisoner == null || !prisoner.IsLord || prisoner == Hero.MainHero) return;

            _pending.Add(new PendingRelease {Prisoner = prisoner, CaptorParty = party, CaptorFaction = faction});
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
                if (_pending.Count == 0 || mapEvent == null) return;

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

                foreach (PendingRelease pr in _pending)
                {
                    if (!BelongsToDefeatedSide(pr, defeatedParties, defeatedFactions)) continue;
                    CreditRescue(pr.Prisoner, pr.CaptorParty, pr.CaptorFaction, publicAction, today);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[RESCUE] resolve failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _pending.Clear();
            }
        }

        private void CreditRescue(Hero prisoner, PartyBase captorParty, IFaction captorFaction, bool publicAction, int today, bool brokenOut = false)
        {
            if (prisoner == null || _credited.Contains(prisoner.StringId)) return;

            bool prisonerIsBandit = prisoner.Clan != null && prisoner.Clan.IsBanditFaction;
            bool inPlayerClan = prisoner.Clan == Hero.MainHero?.Clan;
            if (!RescueEligibilityPolicy.IsPlayerRescue(new RescueEligibilityPolicy.ReleaseFacts {PlayerWonBattle = !brokenOut, ReleasedAfterBattle = !brokenOut, BrokenOutByPlayer = brokenOut, PrisonerIsLord = prisoner.IsLord, PrisonerIsBandit = prisonerIsBandit, PrisonerInPlayerClan = inPlayerClan, PrisonerIsPlayer = prisoner == Hero.MainHero}))
                return;

            _credited.Add(prisoner.StringId);

            string captorDesc = DescribeCaptor(captorParty, captorFaction);
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
            Hero captor = ResolveCaptorHero(captorParty, captorFaction);
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

        private static string DescribeCaptor(PartyBase captorParty, IFaction captorFaction)
        {
            if (captorParty?.Settlement != null) return $"the dungeons of {captorParty.Settlement.Name}";
            if (captorFaction != null) return $"the hands of {captorFaction.Name}";

            return "captivity";
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
        }

        #endregion
    }
}
