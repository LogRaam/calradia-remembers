// Code written by Gabriel Mailhot, 23/06/2026.
// Companion missions (ROADMAP) — engine glue for "bring me the news": the companion physically LEAVES the
// player's party as a real clan party (vanilla-serialized → the errand survives saves with no save-scum),
// rides to a town, lingers, and rides back, then reports. The party is a true map presence, so it can be
// waylaid and the companion taken prisoner — in which case the news waits with them and is delivered when
// they next rejoin you (escape / ransom / your rescue). Decision logic is pure (CalradiaRemembers.Logic).
//
// PERSISTENCE: only the logical mission is serialized here (JSON byte[]); the companion + their party are
// native campaign objects the engine already saves. A reload restores the in-flight state exactly — the
// quality is rolled AT DISPATCH and the news gathered AT ARRIVAL, both persisted, so neither can be re-rolled.
//
// MVP SEAM (next increment): the report is announced as a banner; it will become a CR CONVERSATION (the
// companion's own voice, with the town's rumours injected) — the seam is BuildReport / DeliverReport.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Travel;
using CalradiaRemembers.WorldEvents;
using Helpers;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Missions
{
    /// <summary>
    ///   Tracks the errands companions are away on and advances each daily through its travel arc. The
    ///   companion rides as a real clan party; this owns only the logical, persisted mission state.
    /// </summary>
    public sealed class CompanionMissionBehavior : CampaignBehaviorBase
    {
        public static CompanionMissionBehavior Instance;

        private const string SaveKey        = "CalradiaRemembers_CompanionMissions_v2bytes";
        private const string SaveKeyReports = "CalradiaRemembers_CompanionReports_v1bytes";

        // Tuning (flagged for in-game ratification).
        private const int   NewsDifficulty   = 30;  // a "bring me the news" errand's baseline difficulty
        private const int   MinNewsMagnitude = 35;  // skip trivial happenings from a news report
        private const int   EscortSize       = 8;   // a small escort so they are not caught the first day
        private const int   GoldBase         = 120; // base haul for a steal/barter errand, before skill + the quality scale
        private const int   SuitedSkillThreshold = 80; // governing-skill level at/above which an errand "suits" the companion
        private const float ArrivalDistance  = 2.0f; // map units counting as "arrived" at town / back at the player

        private List<CompanionMission> _active = new List<CompanionMission>();

        // News a returned companion is waiting to TELL the player, keyed by companion StringId. Set on
        // homecoming, consumed on the first conversation with them (persisted so it survives a save in between).
        private Dictionary<string, string> _pendingReports = new Dictionary<string, string>();

        public CompanionMissionBehavior() => Instance = this;

        public int ActiveCount => _active.Count;

        /// <summary>True when the companion with this StringId is currently away on an errand.</summary>
        public bool IsAway(string companionId) => _active.Any(m => m.CompanionId == companionId);

        /// <summary>
        ///   The news a just-returned companion has to tell, if any — returned ONCE and then cleared, so it
        ///   colours only the homecoming conversation. Null when this hero has no report waiting.
        /// </summary>
        public string ConsumeNewsReport(Hero npc)
        {
            if (npc == null || !_pendingReports.TryGetValue(npc.StringId, out string report)) return null;
            _pendingReports.Remove(npc.StringId);
            return report;
        }

        // ── Dispatch ──────────────────────────────────────────────────────────

        /// <summary>
        ///   Sends a companion to gather news, optionally scoped to a topic (<paramref name="about"/> — a
        ///   faction, culture, or town). Detaches them into their own clan party bound for the resolved
        ///   town. Returns a player-facing message; refuses (with a reason) when ineligible.
        /// </summary>
        public (bool Dispatched, string Message) Dispatch(Hero companion, CompanionMissionType type, string about)
        {
            if (companion == null || companion == Hero.MainHero) return (false, "No companion to send.");
            if (!IsUsableCompanion(companion)) return (false, $"{companion.Name} is not a companion in your party to send.");
            if (IsAway(companion.StringId)) return (false, $"{companion.Name} is already away on an errand.");

            int skill = GoverningSkillValue(companion, type);
            if (!CompanionMissions.CanAttempt(skill))
                return (false, $"{companion.Name} is too unschooled in {CompanionMissions.GoverningSkill(type).ToString().ToLowerInvariant()} for that errand.");

            Settlement dest = ResolveDestination(about);
            if (dest == null) return (false, "There is no town to send them to.");

            int roll = MBRandom.RandomInt(0, 100);
            Logic.MissionResult quality = CompanionMissions.Resolve(type, skill, roll, NewsDifficulty).Result;

            MobileParty party = SpawnTravelingCompanionParty(companion, dest);
            if (party == null) return (false, $"{companion.Name} could not set out just now.");

            _active.Add(new CompanionMission
            {
                Id                      = Guid.NewGuid().ToString("N"),
                CompanionId             = companion.StringId,
                CompanionName           = companion.Name?.ToString() ?? "Your companion",
                Type                    = type,
                DestinationSettlementId = dest.StringId,
                DestinationName         = dest.Name?.ToString() ?? "a town",
                PartyId                 = party.StringId,
                DispatchDay             = Today,
                Difficulty              = NewsDifficulty,
                Phase                   = CompanionMissionPhase.Outbound,
                Quality                 = quality
            });

            ModLog.Info($"[MISSION] {companion.Name} rides for {dest.Name} on {type} (quality {quality}, party {party.StringId}).");
            string scope = string.IsNullOrWhiteSpace(about) ? "" : $" ({about.Trim()})";
            return (true, $"{companion.Name} rides for {dest.Name} on a {ErrandVerb(type)} errand{scope}.");
        }

        private static string ErrandVerb(CompanionMissionType type)
        {
            switch (type)
            {
                case CompanionMissionType.Spy:    return "scouting";
                case CompanionMissionType.Steal:  return "thieving";
                case CompanionMissionType.Barter: return "trading";
                case CompanionMissionType.Envoy:  return "envoy";
                default:                          return "news";
            }
        }

        private static int GoverningSkillValue(Hero companion, CompanionMissionType type)
        {
            SkillObject skill;
            switch (CompanionMissions.GoverningSkill(type))
            {
                case MissionSkill.Roguery: skill = DefaultSkills.Roguery; break;
                case MissionSkill.Trade:   skill = DefaultSkills.Trade; break;
                case MissionSkill.Charm:   skill = DefaultSkills.Charm; break;
                default:                   skill = DefaultSkills.Scouting; break;
            }
            return companion.GetSkillValue(skill);
        }

        // ── Daily advance ─────────────────────────────────────────────────────

        private void OnDailyTick()
        {
            if (_active.Count == 0) return;
            foreach (CompanionMission m in _active.ToList())
                if (Advance(m))
                    _active.Remove(m);
        }

        /// <summary>Advances one mission a day. Returns true when it is finished and should be dropped.</summary>
        private bool Advance(CompanionMission m)
        {
            Hero companion = HeroById(m.CompanionId);
            if (companion == null || !companion.IsAlive)
            {
                DestroyParty(m); // the hero is gone — clean up any stray errand party
                return true;
            }

            switch (m.Phase)
            {
                case CompanionMissionPhase.Outbound:
                {
                    if (companion.IsPrisoner) { m.Phase = CompanionMissionPhase.AwaitingReunion; m.PartyId = null; return false; }
                    MobileParty party = FindParty(m.PartyId);
                    if (party == null) { m.Phase = CompanionMissionPhase.AwaitingReunion; return false; }

                    Settlement dest = Settlement.Find(m.DestinationSettlementId);
                    if (dest == null) { ReturnHome(m, party); return false; } // town vanished — just come back

                    if (ArrivedAt(party, dest))
                    {
                        PerformErrand(m, dest);
                        m.LingerUntilDay = Today + CompanionMissions.DurationDays(m.Type, m.Difficulty);
                        m.Phase = CompanionMissionPhase.Lingering;
                    }
                    else if (party.CurrentSettlement != dest)
                    {
                        TravelingParty.DirectToSettlement(party, dest);
                    }
                    return false;
                }

                case CompanionMissionPhase.Lingering:
                {
                    if (companion.IsPrisoner) { m.Phase = CompanionMissionPhase.AwaitingReunion; m.PartyId = null; return false; }
                    if (Today >= m.LingerUntilDay) ReturnHome(m, FindParty(m.PartyId));
                    return false;
                }

                case CompanionMissionPhase.Returning:
                {
                    if (companion.IsPrisoner) { m.Phase = CompanionMissionPhase.AwaitingReunion; m.PartyId = null; return false; }
                    MobileParty party = FindParty(m.PartyId);
                    if (party == null) { m.Phase = CompanionMissionPhase.AwaitingReunion; return false; }

                    if (NearPlayer(party)) { Reunite(m, companion, party); return true; }
                    TravelingParty.DirectToMovingPoint(party, MobileParty.MainParty.GetPosition2D);
                    return false;
                }

                case CompanionMissionPhase.AwaitingReunion:
                {
                    // Captured. Wait until they are free AND back in the player's party (escape/ransom/rescue),
                    // then deliver whatever news they had managed to gather.
                    if (!companion.IsPrisoner && companion.PartyBelongedTo == MobileParty.MainParty)
                    {
                        CompanionHappinessService.ApplyDriver(companion, HappinessDriver.Freed);
                        DeliverReport(m, companion);
                        return true;
                    }
                    return false;
                }

                default:
                    return true;
            }
        }

        private void ReturnHome(CompanionMission m, MobileParty party)
        {
            m.Phase = CompanionMissionPhase.Returning;
            if (party != null) TravelingParty.DirectToMovingPoint(party, MobileParty.MainParty.GetPosition2D);
        }

        /// <summary>Brings the hero back into the player's party, disbands the errand party, and reports.</summary>
        private void Reunite(CompanionMission m, Hero companion, MobileParty party)
        {
            try { AddHeroToPartyAction.Apply(companion, MobileParty.MainParty, false); }
            catch (Exception ex) { ModLog.Error($"[MISSION] rejoin failed for {m.CompanionName}: {ex.Message}"); }

            TravelingParty.Destroy(party); // now leaderless — remove the escort party from the world
            m.PartyId = null;
            DeliverReport(m, companion);
        }

        // ── News + report ─────────────────────────────────────────────────────

        /// <summary>Secures the errand's payload at the destination — news, intel, gold, or mood — scaled by quality.</summary>
        private void PerformErrand(CompanionMission m, Settlement dest)
        {
            switch (CompanionMissions.PayloadOf(m.Type))
            {
                case CompanionMissionPayload.Intel: GatherIntel(m, dest); break;
                case CompanionMissionPayload.Gold:  ComputeGold(m, dest); break;
                case CompanionMissionPayload.Mood:  GatherMood(m, dest); break;
                default:                            GatherNews(m, dest); break;
            }
            m.NewsGathered = true;
        }

        private void GatherNews(CompanionMission m, Settlement town)
        {
            var rated = WorldEventStore.Instance?.GetKnownEventsRatedAt(town, Today, 4)
                        ?? new List<(WorldEvent Event, RumorConfidence Confidence)>();
            int count = CompanionMissions.NewsItemsForResult(m.Quality, rated.Count);

            m.News = rated.Take(count)
                .Select(t => t.Event?.Summary?.TrimEnd('.', ' '))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            ModLog.Info($"[MISSION] {m.CompanionName} gathered {m.News.Count} news item(s) at {m.DestinationName} (quality {m.Quality}).");
        }

        private void GatherIntel(CompanionMission m, Settlement dest)
        {
            var lines = new List<string>();
            string faction = dest.MapFaction?.Name?.ToString() ?? "no banner";
            string owner   = dest.OwnerClan?.Leader?.Name?.ToString() ?? dest.OwnerClan?.Name?.ToString() ?? "an unknown hand";
            int garrison   = dest.Town?.GarrisonParty?.MemberRoster?.TotalManCount ?? 0;

            lines.Add($"{dest.Name} is held by {owner} of {faction}");
            lines.Add($"its garrison numbers about {garrison} fighting men, with militia near {(int) dest.Militia}");
            if (dest.Town != null) lines.Add($"the town's prosperity stands around {(int) dest.Town.Prosperity}");
            Hero lord = dest.OwnerClan?.Leader;
            if (lord != null)
                lines.Add($"{lord.Name} was last known {(lord.CurrentSettlement != null ? $"at {lord.CurrentSettlement.Name}" : "in the field")}");

            int count = CompanionMissions.NewsItemsForResult(m.Quality, lines.Count);
            m.News = lines.Take(count).ToList();
            ModLog.Info($"[MISSION] {m.CompanionName} scouted {dest.Name}: {m.News.Count} item(s) (quality {m.Quality}).");
        }

        private void ComputeGold(CompanionMission m, Settlement dest)
        {
            // Steal is Roguery-based, Barter Trade-based — the governing skill sets the base haul either way.
            Hero companion = HeroById(m.CompanionId);
            int baseAmount = GoldBase + (companion != null ? GoverningSkillValue(companion, m.Type) : 0);
            m.LootGold = CompanionMissions.LootForResult(m.Quality, baseAmount);
            ModLog.Info($"[MISSION] {m.CompanionName} {(m.Type == CompanionMissionType.Steal ? "stole" : "traded")} at {dest.Name}: {m.LootGold} denars (quality {m.Quality}).");
        }

        private void GatherMood(CompanionMission m, Settlement dest)
        {
            var lines = new List<string>();
            IFaction faction = dest.MapFaction;
            string name   = faction?.Name?.ToString() ?? "the locals";
            string leader = faction?.Leader?.Name?.ToString();

            lines.Add(leader != null ? $"{name} answers to {leader}" : $"{name} keeps its own counsel");

            var enemies = Kingdom.All
                .Where(k => faction != null && k != faction && FactionManager.IsAtWarAgainstFaction(faction, k))
                .Select(k => k.Name?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            lines.Add(enemies.Count > 0 ? $"they are at war with {string.Join(", ", enemies)}" : "they are at peace with their neighbours");
            lines.Add($"the mood about {dest.Name} reads as {(dest.Town != null && dest.Town.Loyalty >= 50 ? "settled" : "restless")}");

            int count = CompanionMissions.NewsItemsForResult(m.Quality, lines.Count);
            m.News = lines.Take(count).ToList();
            ModLog.Info($"[MISSION] {m.CompanionName} took the measure of {name}: {m.News.Count} item(s) (quality {m.Quality}).");
        }

        private void DeliverReport(CompanionMission m, Hero companion)
        {
            // A gold errand (steal or barter) reaches the player's coffers on reunion (no giver — found coin).
            if (m.LootGold > 0)
                try { GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, m.LootGold, true); }
                catch (Exception ex) { ModLog.Error($"[MISSION] gold grant failed: {ex.Message}"); }

            // The report is delivered as a CONVERSATION: stash a directive for the companion to voice on the
            // next talk (consumed once), and nudge the player to go speak with them. The full templated
            // text still goes to the log for diagnostics.
            if (companion != null)
                _pendingReports[companion.StringId] = BuildNewsContext(m);

            string town = m.DestinationName ?? "their errand";
            MBInformationManager.AddQuickInformation(
                new TextObject($"{m.CompanionName} has returned from {town} — have a word with them."),
                0, companion?.CharacterObject);
            ModLog.Info($"[MISSION] report ready for {m.CompanionName}:\n{BuildReport(m)}");

            ApplyMissionHappiness(m, companion);
        }

        /// <summary>How the errand's outcome moved the companion's happiness — succeeded/failed, and whether it suited them.</summary>
        private static void ApplyMissionHappiness(CompanionMission m, Hero companion)
        {
            if (companion == null) return;

            if (m.Quality == Logic.MissionResult.Success)
                CompanionHappinessService.ApplyDriver(companion, HappinessDriver.ErrandSucceeded);
            else if (m.Quality == Logic.MissionResult.Failure)
                CompanionHappinessService.ApplyDriver(companion, HappinessDriver.ErrandFailed);

            bool suited = GoverningSkillValue(companion, m.Type) >= SuitedSkillThreshold;
            CompanionHappinessService.ApplyDriver(companion, suited ? HappinessDriver.SuitedErrand : HappinessDriver.UnsuitedErrand);
        }

        /// <summary>The directive injected into the homecoming conversation — the raw payload for the companion to voice.</summary>
        private string BuildNewsContext(CompanionMission m)
        {
            string town = m.DestinationName ?? "the town";

            if (!m.NewsGathered)
                return $"You were waylaid on the road to {town} and taken prisoner, winning free only lately — your errand came to nothing.";

            switch (m.Type)
            {
                case CompanionMissionType.Spy:
                    return m.News.Count == 0
                        ? $"You stole a look at {town} for the player but learned little of use."
                        : $"You scouted {town} for the player. What you observed:\n{Bullets(m.News)}";
                case CompanionMissionType.Steal:
                    return m.LootGold > 0
                        ? $"You slipped into {town} and got away with {m.LootGold} denars for the player."
                        : $"You tried to lift something in {town}, but came away empty-handed.";
                case CompanionMissionType.Barter:
                    return m.LootGold > 0
                        ? $"You traded shrewdly at {town} and turned a profit of {m.LootGold} denars for the player."
                        : $"You haggled at {town}, but the markets gave you nothing worth bringing home.";
                case CompanionMissionType.Envoy:
                    return m.News.Count == 0
                        ? $"You carried the player's word toward {town} but learned little of the mood there."
                        : $"You went as the player's envoy to {town}. What you took of their temper:\n{Bullets(m.News)}";
                default:
                    return m.News.Count == 0
                        ? $"You rode to {town} and back, but the roads and the town were quiet — you have little of substance to report."
                        : $"You rode to {town} and gathered word there. What you learned:\n{Bullets(m.News)}";
            }
        }

        private static string Bullets(IEnumerable<string> lines) => string.Join("\n", lines.Select(s => $"- {s}."));

        private string BuildReport(CompanionMission m)
        {
            string name = m.CompanionName ?? "Your companion";
            string town = m.DestinationName ?? "the town";

            if (!m.NewsGathered)
                return $"{name} returns from captivity with nothing — the errand was cut short.";

            switch (m.Type)
            {
                case CompanionMissionType.Spy:
                    return m.News.Count == 0
                        ? $"{name} scouted {town} but learned little."
                        : $"{name} scouted {town}:\n{Bullets(m.News)}";
                case CompanionMissionType.Steal:
                case CompanionMissionType.Barter:
                    return m.LootGold > 0
                        ? $"{name} returns from {town} with {m.LootGold} denars."
                        : $"{name} returns from {town} empty-handed.";
                case CompanionMissionType.Envoy:
                    return m.News.Count == 0
                        ? $"{name} returns from {town} with little read on its mood."
                        : $"{name} returns from {town} with the temper of the place:\n{Bullets(m.News)}";
                default:
                    return m.News.Count == 0
                        ? $"{name} returns from {town}, but the roads were quiet — little worth reporting."
                        : $"{name} returns from {town} with word:\n{Bullets(m.News)}";
            }
        }

        // ── Capture ───────────────────────────────────────────────────────────

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner == null) return;
            CompanionMission m = _active.FirstOrDefault(x => x.CompanionId == prisoner.StringId
                                                             && x.Phase != CompanionMissionPhase.AwaitingReunion);
            if (m == null) return;

            m.Phase = CompanionMissionPhase.AwaitingReunion;
            m.PartyId = null; // the errand party is gone with the capture
            CompanionHappinessService.ApplyDriver(prisoner, HappinessDriver.Captured);
            ModLog.Info($"[MISSION] {m.CompanionName} was taken prisoner — the news waits until they rejoin you.");
        }

        // ── Console test path ─────────────────────────────────────────────────

        /// <summary>Force-completes the first active errand NOW (gather + reunite + report), skipping travel.</summary>
        public string ForceResolveFirst()
        {
            CompanionMission m = _active.FirstOrDefault();
            if (m == null) return "No companion is away on an errand.";

            Hero companion = HeroById(m.CompanionId);
            Settlement dest = Settlement.Find(m.DestinationSettlementId);
            if (!m.NewsGathered && dest != null) GatherNews(m, dest);

            MobileParty party = FindParty(m.PartyId);
            if (party != null && companion != null) Reunite(m, companion, party);
            else
            {
                if (companion != null && !companion.IsPrisoner && companion.PartyBelongedTo != MobileParty.MainParty)
                    try { AddHeroToPartyAction.Apply(companion, MobileParty.MainParty, false); } catch { /* best effort */ }
                DeliverReport(m, companion);
            }

            _active.Remove(m);
            return BuildReport(m);
        }

        /// <summary>A one-line-per-errand summary of what is in flight.</summary>
        public string DescribeActive()
        {
            if (_active.Count == 0) return "No companions are away on errands.";
            return string.Join("\n", _active.Select(m =>
                $"- {m.CompanionName}: {m.Phase} → {m.DestinationName} (quality {m.Quality}{(m.NewsGathered ? ", news in hand" : "")})."));
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_active, Formatting.None)); }
                catch (Exception ex)
                {
                    ModLog.Error($"[MISSION] serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[MISSION] byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }

                byte[] reportBytes;
                try { reportBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_pendingReports, Formatting.None)); }
                catch { reportBytes = Encoding.UTF8.GetBytes("{}"); }
                try { dataStore.SyncData(SaveKeyReports, ref reportBytes); }
                catch (Exception ex) { ModLog.Error($"[MISSION] reports save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch { /* key absent in older saves */ }

                if (bytes != null && bytes.Length > 0)
                    try { _active = JsonConvert.DeserializeObject<List<CompanionMission>>(Encoding.UTF8.GetString(bytes)) ?? new List<CompanionMission>(); }
                    catch (Exception ex)
                    {
                        _active = new List<CompanionMission>();
                        ModLog.Error($"[MISSION] load failed — dropping in-flight errands so the campaign opens. {ex.GetType().Name}: {ex.Message}");
                    }

                byte[] reportBytes = null;
                try { dataStore.SyncData(SaveKeyReports, ref reportBytes); }
                catch { /* key absent in older saves */ }
                if (reportBytes != null && reportBytes.Length > 0)
                    try { _pendingReports = JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(reportBytes)) ?? new Dictionary<string, string>(); }
                    catch { _pendingReports = new Dictionary<string, string>(); }

                ModLog.Info($"[MISSION] loaded {_active.Count} in-flight errand(s), {_pendingReports.Count} pending report(s) from save.");
            }
        }

        #region private

        private static int Today => (int) CampaignTime.Now.ToDays;

        private static bool IsUsableCompanion(Hero h)
            => h != null && h.IsAlive && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan)
               && h.PartyBelongedTo == MobileParty.MainParty;

        private static Hero HeroById(string id)
            => string.IsNullOrEmpty(id) ? null : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

        private static MobileParty FindParty(string id)
            => string.IsNullOrEmpty(id) ? null : MobileParty.All.FirstOrDefault(p => p.StringId == id);

        private static bool ArrivedAt(MobileParty party, Settlement dest)
            => party.CurrentSettlement == dest || party.GetPosition2D.Distance(dest.GetPosition2D) < ArrivalDistance;

        private static bool NearPlayer(MobileParty party)
            => MobileParty.MainParty != null
               && party.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D) < ArrivalDistance;

        /// <summary>
        ///   The town a "news about X" errand rides to: the pure <see cref="NewsDestinationSelector"/>
        ///   scores all towns (host-ordered by distance, so a tie / no-match resolves to the nearest).
        /// </summary>
        private static Settlement ResolveDestination(string about)
        {
            Vec2 from = MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero;
            var towns = Settlement.All.Where(s => s.IsTown)
                .OrderBy(s => s.GetPosition2D.Distance(from))
                .ToList();
            if (towns.Count == 0) return null;

            var candidates = towns.Select(s => new NewsDestinationCandidate(
                s.Name?.ToString(), s.MapFaction?.Name?.ToString(), s.Culture?.Name?.ToString())).ToList();

            int idx = NewsDestinationSelector.Choose(about, candidates);
            return idx >= 0 ? towns[idx] : towns[0];
        }

        /// <summary>Detaches the companion into their own clan party (vanilla-serialized) with a small escort, bound for the town.</summary>
        private static MobileParty SpawnTravelingCompanionParty(Hero companion, Settlement dest)
        {
            try
            {
                MobileParty party = MobilePartyHelper.CreateNewClanMobileParty(companion, Clan.PlayerClan);
                if (party == null) return null;

                CharacterObject troop = companion.Culture?.BasicTroop ?? Hero.MainHero?.Culture?.BasicTroop;
                if (troop != null) party.MemberRoster.AddToCounts(troop, EscortSize);

                TravelingParty.DirectToSettlement(party, dest);
                return party;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[MISSION] could not spawn errand party for {companion?.Name}: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static void DestroyParty(CompanionMission m)
        {
            MobileParty party = FindParty(m.PartyId);
            if (party != null) TravelingParty.Destroy(party);
            m.PartyId = null;
        }

        #endregion
    }
}
