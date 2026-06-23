// Code written by Gabriel Mailhot, 17/05/2026.

#region

using System;
using System.Linq;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>
    ///   Constructs an <see cref="EncounterContext" /> for a given NPC, using
    ///   the game state bridge for live data and the stored profile for
    ///   historical data (last-meeting computation).
    /// </summary>
    public static class EncounterContextBuilder
    {
        /// <summary>
        ///   Builds an <see cref="EncounterContext" /> for the given NPC.
        ///   When <paramref name="playerMessage" /> is provided, the resolver scans it
        ///   for hero names and injects a contextual hint so the NPC can answer
        ///   questions about third parties accurately — or ask for clarification when
        ///   a name is ambiguous.
        /// </summary>
        public static EncounterContext Build(
            Hero hero,
            NpcProfile profile,
            IGameStateBridge bridge,
            string playerMessage = null,
            System.Collections.Generic.IReadOnlyList<NpcMemoryService.Core.Models.WitnessEntry> witnesses = null,
            bool privacyRequested = false,
            bool isWitnessExchangeTurn    = false,
            bool isLastWitnessExchange    = false,
            bool isCollectiveCaptiveScene = false,
            PlayerStatusVsNpc? playerStatusOverride = null,
            CaptiveSceneIntent captiveIntent = CaptiveSceneIntent.Interrogation,
            bool isFinalSceneBeat = false,
            bool captorIsBandit = false,
            CaptiveSceneStage sceneStage = CaptiveSceneStage.Intro,
            CaptiveAggressorKind aggressorKind = CaptiveAggressorKind.Lead,
            bool reactToPlayerIntervention = false) => new() {
            PlayerStatus           = playerStatusOverride ?? ResolvePlayerStatus(hero, profile, bridge),
            WarStatus              = bridge.GetDiplomaticStatus(hero),
            DaysSinceLastMeeting   = ComputeDaysSinceLastMeeting(profile, bridge),
            Scene                  = bridge.GetCurrentScene(),
            ContextualNames        = string.IsNullOrWhiteSpace(playerMessage)
                ? null
                : ContextualNameResolver.Resolve(hero, playerMessage, bridge),
            NpcCurrentActivity     = bridge.GetCurrentActivity(hero),
            CurrentLocationNote    = BuildLocationNote(hero),
            PlayerSpouseName       = Hero.MainHero?.Spouse?.IsAlive == true
                ? Hero.MainHero.Spouse.Name?.ToString()
                : null,
            PlayerFatherName       = FormatParent(Hero.MainHero?.Father),
            PlayerMotherName        = FormatParent(Hero.MainHero?.Mother),
            PlayerClanTier         = Hero.MainHero?.Clan?.Tier ?? 0,
            PlayerArenaRank        = ResolveArenaRank(),
            PlayerArenaWins        = ResolveArenaWins(),
            Witnesses              = witnesses,
            PrivacyRequested       = privacyRequested,
            IsWitnessExchangeTurn    = isWitnessExchangeTurn,
            IsLastWitnessExchange    = isLastWitnessExchange,
            IsCollectiveCaptiveScene = isCollectiveCaptiveScene,
            CaptiveIntent            = captiveIntent,
            IsFinalSceneBeat         = isFinalSceneBeat,
            SceneStage               = sceneStage,
            AggressorKind            = aggressorKind,
            ReactToPlayerIntervention = reactToPlayerIntervention,
            CaptorIsBandit           = captorIsBandit,
            PlayerLooksWealthy       = captorIsBandit && PlayerLooksWealthy(),
            PlayerLooksImportant     = captorIsBandit && PlayerLooksImportant(),
            CompanionAskingPrice     = ResolveCompanionAskingPrice(hero),
            HeldLordPrisoners        = ResolveHeldLordPrisoners(),
            NpcIsPrisonerOfAnother   = IsPrisonerOfAnother(hero),
            NpcCaptorName            = IsPrisonerOfAnother(hero) ? hero.PartyBelongedToAsPrisoner?.MapFaction?.Name?.ToString() : null,
            MercenaryOfferKingdom    = ResolveMercenaryOfferKingdom(hero),
            LordRecruitEligible      = ResolveLordRecruitEligible(hero),
            SchemeAgentTargetName    = ResolveSchemeAgentTarget(hero),
            SchemeTargetsThisNpc     = ResolveSchemeTargetsThisNpc(hero),
            InterceptionReason       = ResolveInterceptionReason(hero),
            StanceNote               = Stance.StanceService.Describe(profile),
            LoveMatchEligible        = ResolveLoveMatchEligible(hero, profile),
            LoveMatchBlessed         = ResolveLoveMatchBlessed(hero),
            ConsortEligible          = ResolveConsortEligible(hero, profile),
            PowerBalanceNote         = ResolvePowerBalanceNote(hero, profile),
            MarriageProspects        = MarriageProspectResolver.Build(hero),
            WorldRumorsBlock         = ResolveWorldRumors(hero),
            PlayerDeedsHeard         = ResolvePlayerDeedsHeard(hero)
            // PlayerPurseGold is deliberately NOT fed: an NPC cannot see the player's liquid
            // coin, and naming the exact sum broke immersion. Affordability is enforced by the
            // bridge when the hire is executed, not foreseen in the prompt.
        };

        #region private

        /// <summary>
        ///   The recent THIRD-PARTY world happenings this NPC would plausibly have heard of (faction/
        ///   region/recency), pre-formatted as hearsay lines for the prompt. The player's OWN deeds are
        ///   pulled out here and routed to <see cref="ResolvePlayerDeedsHeard"/> instead, so they reach
        ///   the NPC attributed to the person before them rather than as faceless gossip. Null when
        ///   nothing relevant has reached them, so the "WHAT YOU'VE HEARD" section is skipped entirely.
        /// </summary>
        private static string ResolveWorldRumors(Hero hero)
        {
            try
            {
                var rated = WorldEvents.WorldEventStore.Instance?.GetKnownEventsRated(
                    hero, (int) CampaignTime.Now.ToDays, 6);
                if (rated == null || rated.Count == 0) return null;

                var lines = new System.Collections.Generic.List<string>();
                foreach ((WorldEvents.WorldEvent e, RumorConfidence conf) in rated)
                {
                    if (e.InvolvesPlayer || string.IsNullOrWhiteSpace(e.Summary)) continue; // player deeds → influence loop
                    string tag = conf == RumorConfidence.Distant ? "(a distant, unverified rumour) "
                               : conf == RumorConfidence.Secondhand ? "(heard secondhand) "
                               : "";
                    lines.Add($"- {tag}{e.Summary.TrimEnd('.', ' ')}.");
                    if (lines.Count >= 4) break;
                }

                return lines.Count > 0 ? string.Join("\n", lines) : null;
            }
            catch { return null; }
        }

        /// <summary>
        ///   The PLAYER's own deeds that have reached this NPC by word, attributed to the person they are
        ///   speaking with and tagged with how each one lands on them (a wound to their side, a boon to it)
        ///   via the pure <see cref="PlayerDeedValence"/>. The influence loop — the world reflecting the
        ///   player's acts back. Null when none have reached them, so the section is skipped entirely.
        /// </summary>
        internal static string ResolvePlayerDeedsHeard(Hero hero)
        {
            try
            {
                var rated = WorldEvents.WorldEventStore.Instance?.GetKnownEventsRated(
                    hero, (int) CampaignTime.Now.ToDays, 8);
                if (rated == null || rated.Count == 0) return null;

                string obsFactionId    = hero?.MapFaction?.StringId;
                string playerFactionId = Hero.MainHero?.MapFaction?.StringId;

                var lines = new System.Collections.Generic.List<string>();
                foreach ((WorldEvents.WorldEvent e, RumorConfidence conf) in rated)
                {
                    if (!e.InvolvesPlayer || string.IsNullOrWhiteSpace(e.Summary)) continue;

                    bool aggrieved = obsFactionId != null && e.AggrievedFactionIds != null
                                     && e.AggrievedFactionIds.Contains(obsFactionId);
                    bool sharesPlayer = obsFactionId != null && obsFactionId == playerFactionId;

                    string valence;
                    switch (PlayerDeedValence.Classify(aggrieved, sharesPlayer))
                    {
                        case DeedValence.Wound: valence = "(this cut against you or your own) "; break;
                        case DeedValence.Boon:  valence = "(this served your side) "; break;
                        default:                valence = ""; break;
                    }
                    string doubt = conf == RumorConfidence.Distant ? " — though none can fully vouch for it" : "";
                    lines.Add($"- {valence}{e.Summary.TrimEnd('.', ' ')}{doubt}.");
                    if (lines.Count >= 3) break;
                }

                return lines.Count > 0 ? string.Join("\n", lines) : null;
            }
            catch { return null; }
        }

        /// <summary>
        ///   Formats a parent's display name for the prompt, marking the deceased with
        ///   "the late …" so the NPC never speaks of a dead parent as living. Null when
        ///   the parent is unknown or unnamed.
        /// </summary>
        private static string FormatParent(Hero parent)
        {
            string name = parent?.Name?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return null;
            return parent.IsAlive ? name : $"the late {name}";
        }

        /// <summary>
        ///   The vanilla hiring price when this hero is genuinely recruitable RIGHT NOW:
        ///   a living, free wanderer not yet in anyone's service, while the player's clan
        ///   still has a companion slot. Null otherwise — the recruitment section is then
        ///   not taught, so the NPC never agrees to a hire the game would refuse.
        /// </summary>
        private static int? ResolveCompanionAskingPrice(Hero hero)
            => GameState.RecruitmentPricing.GetHiringPrice(hero);

        /// <summary>True when the NPC is a captive of someone other than the player (case B: a rescue bargain).</summary>
        private static bool IsPrisonerOfAnother(Hero hero)
            => hero?.IsPrisoner == true && hero.PartyBelongedToAsPrisoner?.LeaderHero != Hero.MainHero;

        /// <summary>
        ///   A prompt-ready list of the LORD captives the player currently holds (name + faction), or
        ///   null when none. Notable/wanderer prisoners are excluded — a prisoner bargain is about lords.
        /// </summary>
        private static string ResolveHeldLordPrisoners()
        {
            var lines = MobileParty.MainParty?.PrisonRoster?.GetTroopRoster()
                ?.Select(el => el.Character?.HeroObject)
                .Where(h => h != null && h.IsLord && !h.IsHumanPlayerCharacter)
                .Select(h => $"- {h.Name} ({h.MapFaction?.Name?.ToString() ?? h.Clan?.Name?.ToString() ?? "unknown"})")
                .ToList();

            return lines == null || lines.Count == 0 ? null : string.Join("\n", lines);
        }

        /// <summary>
        ///   Returns the NPC's kingdom name when the NPC is a lord in a kingdom AND the
        ///   player's clan is currently free of any kingdom obligation. Null otherwise —
        ///   the mercenary-offer section is then not taught at all.
        /// </summary>
        private static string ResolveMercenaryOfferKingdom(Hero hero)
        {
            if (hero == null || !hero.IsLord) return null;

            Kingdom npcKingdom = hero.Clan?.Kingdom;
            if (npcKingdom == null) return null;

            // Player must not already be in any kingdom (vassal or mercenary).
            if (Clan.PlayerClan?.Kingdom != null) return null;

            return npcKingdom.Name?.ToString();
        }

        /// <summary>
        ///   True when this lord could be persuaded to defect into the player's clan — gates the
        ///   <c>join_clan</c> teaching. Gathers live facts and delegates to the pure
        ///   <see cref="LordRecruitment"/>; the bridge re-checks the same gate before sealing it.
        /// </summary>
        private static bool ResolveLordRecruitEligible(Hero hero)
        {
            try
            {
                LordRecruitmentFacts facts = GameState.LordRecruitmentFactsBuilder.Build(hero);
                return facts != null && LordRecruitment.IsEligible(facts);
            }
            catch { return false; }
        }

        /// <summary>
        ///   When this NPC is secretly plotting against a rival and could enlist the player, the
        ///   target's name — gates the <c>scheme_assist</c> teaching. Delegates the eligibility to
        ///   <see cref="WorldEvents.SchemeStore"/> so the prompt and the bridge agree. Null otherwise.
        /// </summary>
        private static string ResolveSchemeAgentTarget(Hero hero)
        {
            try { return WorldEvents.SchemeStore.Instance?.GetRecruitableTargetName(hero); }
            catch { return null; }
        }

        /// <summary>
        ///   True when a secret scheme is aimed at this NPC, so the player could warn them — gates the
        ///   <c>scheme_heed</c> teaching. Delegates to <see cref="WorldEvents.SchemeStore"/>.
        /// </summary>
        private static bool ResolveSchemeTargetsThisNpc(Hero hero)
        {
            try { return WorldEvents.SchemeStore.Instance?.HasSecretSchemeAgainst(hero) ?? false; }
            catch { return false; }
        }

        /// <summary>
        ///   When this NPC sought the player out by intercepting them on the map, a one-shot opening
        ///   directive (consumed so it only colours the first turn). Null in an ordinary conversation.
        /// </summary>
        private static string ResolveInterceptionReason(Hero hero)
        {
            try { return Interception.InterceptionBehavior.Instance?.ConsumeInterceptionReason(hero); }
            catch { return null; }
        }

        /// <summary>
        ///   How many in-game days a battlefield defeat stays "fresh" enough to temper a lord's
        ///   defiance — roughly a season's quarter. Older losses fade from the power calculus.
        /// </summary>
        private const int RecentDefeatWindowDays = 21;

        /// <summary>
        ///   Gathers the live power-differential facts (fief counts, war state, captivity, a recent
        ///   defeat) and delegates the verdict to the pure <see cref="PowerBalance"/> classifier.
        ///   Only issued for lords — wanderers and notables have no kingdom standing to weigh.
        /// </summary>
        private static string ResolvePowerBalanceNote(Hero hero, NpcProfile profile)
        {
            try
            {
                if (hero == null || !hero.IsLord || Hero.MainHero == null) return null;

                Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
                Kingdom npcKingdom    = hero.Clan?.Kingdom;

                return PowerBalance.DescribeFor(new PowerBalanceFacts {
                    NpcIsLord          = true,
                    PlayerInKingdom    = playerKingdom != null,
                    NpcInKingdom       = npcKingdom != null,
                    PlayerKingdomFiefs = CountKingdomFiefs(playerKingdom),
                    NpcKingdomFiefs    = CountKingdomFiefs(npcKingdom),
                    AtWar              = playerKingdom != null && npcKingdom != null
                                         && FactionManager.IsAtWarAgainstFaction(playerKingdom, npcKingdom),
                    NpcClanHasFiefs    = hero.Clan?.Settlements != null
                                         && hero.Clan.Settlements.Any(s => s.IsTown || s.IsCastle),
                    PlayerClanHasFiefs = Clan.PlayerClan?.Settlements != null
                                         && Clan.PlayerClan.Settlements.Any(s => s.IsTown || s.IsCastle),
                    NpcIsPlayerPrisoner           = IsPlayerPrisoner(hero),
                    NpcRecentlyLostBattleToPlayer = LostBattleToPlayerRecently(profile)
                });
            }
            catch { return null; }
        }

        /// <summary>True when this hero is held captive by the player's own faction right now.</summary>
        private static bool IsPlayerPrisoner(Hero hero)
        {
            try
            {
                if (hero?.IsPrisoner != true) return false;
                IFaction captor = hero.PartyBelongedToAsPrisoner?.MapFaction;
                return captor != null && captor == Hero.MainHero?.MapFaction;
            }
            catch { return false; }
        }

        /// <summary>
        ///   True when the player's forces beat this NPC in a battle within the recency window. Read
        ///   from the profile memory the battle feeder already stamps on a defeated enemy lord
        ///   ("…saw you put their host to flight"), so no new persisted state is needed. Kept in sync
        ///   with <c>WorldEventFeeders.RecordBattlePerformanceIntoProfiles</c>.
        /// </summary>
        private static bool LostBattleToPlayerRecently(NpcProfile profile)
        {
            if (profile?.Events == null || profile.Events.Count == 0) return false;
            int today = (int) CampaignTime.Now.ToDays;
            foreach (NotableEvent e in profile.Events)
            {
                if (today - e.gameDay > RecentDefeatWindowDays) continue;
                if (!string.IsNullOrEmpty(e.summary) && e.summary.IndexOf("put their host to flight",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>Counts towns and castles belonging to a kingdom; returns 0 for a null kingdom.</summary>
        private static int CountKingdomFiefs(Kingdom kingdom)
        {
            if (kingdom == null) return 0;
            int count = 0;
            foreach (Settlement s in kingdom.Settlements)
                if (s.IsTown || s.IsCastle) count++;
            return count;
        }

        /// <summary>
        ///   True when the NPC themselves is a viable marriage candidate AND the personal
        ///   relation is deep enough (≥50) to make the prospect real. The prompt then
        ///   teaches the <c>marry</c> action; the bridge re-validates before executing.
        /// </summary>
        private static bool ResolveLoveMatchEligible(Hero hero, NpcProfile profile)
        {
            if (hero == null || Hero.MainHero == null) return false;
            if (Hero.MainHero.Spouse != null) return false;           // player already wed
            if (hero.Clan == Clan.PlayerClan) return false;           // can't marry own clan
            if (!MarriageProspectResolver.IsMarriageable(Hero.MainHero, hero)) return false;
            return (profile?.ReputationWithPlayer ?? 0) >= 50;
        }

        /// <summary>True when A.2 already granted the family's blessing for this NPC.</summary>
        private static bool ResolveLoveMatchBlessed(Hero hero)
        {
            if (hero == null) return false;
            return MarriageApprovalBehavior.Instance?.IsApproved(hero.StringId) ?? false;
        }

        /// <summary>
        ///   True when the NPC and the player share a deep enough romantic bond
        ///   (status ≥ Intimate, not yet Committed, reputation ≥ 60, orientation-compatible)
        ///   that a consort bond is conceivable. Available even if the player is already wed —
        ///   the consort path bypasses the native marriage engine entirely.
        /// </summary>
        private static bool ResolveConsortEligible(Hero hero, NpcProfile profile)
        {
            if (hero == null || Hero.MainHero == null) return false;
            if (profile?.Romantic == null) return false;
            if (!profile.Romantic.IsCompatibleWith(Hero.MainHero.IsFemale)) return false;
            if (profile.Romantic.IsConsort) return false;                               // already a consort
            RomanticStatus status = profile.Romantic.Status;
            if (status < RomanticStatus.Intimate) return false;
            if (status == RomanticStatus.Committed) return false;
            return profile.ReputationWithPlayer >= 60;
        }

        /// <summary>
        ///   True when the player's worn battle gear is valuable enough that a brigand could
        ///   guess they are no common traveler. Sums the denar value of every equipped item
        ///   (arms, armor, mount). The threshold sits well above a starting kit but below a
        ///   fully end-game noble loadout, so it reads "this one has money behind them".
        /// </summary>
        private static bool PlayerLooksWealthy()
        {
            try
            {
                Hero h = Hero.MainHero;
                Equipment eq = h?.BattleEquipment;
                if (eq == null) return false;

                int value = 0;
                for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i <= EquipmentIndex.HorseHarness; i++)
                {
                    ItemObject item = eq[i].Item;
                    if (item != null) value += item.Value;
                }

                return value >= 8000;
            }
            catch { return false; }
        }

        /// <summary>
        ///   True when the player carries real political weight (a clan of noble standing or
        ///   notable influence), so a bandit might suspect a valuable, ransomable figure — even
        ///   without knowing the name. Never exposes the actual identity.
        /// </summary>
        private static bool PlayerLooksImportant()
        {
            try
            {
                Clan c = Hero.MainHero?.Clan;
                if (c == null) return false;
                return c.Tier >= 3 || c.Influence >= 150f;
            }
            catch { return false; }
        }

        /// <summary>
        ///   Describes where the conversation happens and who holds it, so an NPC who is a GUEST in
        ///   another lord's settlement does not claim the hall as their own (tester report). Null when
        ///   the NPC is not in a settlement or ownership is unknown.
        /// </summary>
        private static string BuildLocationNote(Hero hero)
        {
            try
            {
                Settlement s = hero?.CurrentSettlement;
                if (s == null) return null;

                Clan ownerClan = s.OwnerClan;
                if (ownerClan != null && hero.Clan != null && ownerClan == hero.Clan)
                    return $"You are in {s.Name}, a holding of your own clan.";

                Hero owner = ownerClan?.Leader;
                if (owner != null && owner != hero)
                    return $"You are in {s.Name}, which belongs to {owner.Name}"
                         + (ownerClan?.Name != null ? $" of clan {ownerClan.Name}" : "")
                         + ". It is NOT your holding — you are here as a guest, not its lord.";

                return $"You are in {s.Name}.";
            }
            catch
            {
                return null;
            }
        }

        private static int ResolveArenaRank()
        {
            try { return Campaign.Current?.TournamentManager?.GetLeaderBoardRank(Hero.MainHero) ?? 0; }
            catch { return 0; }
        }

        private static int ResolveArenaWins()
        {
            try
            {
                var lb = Campaign.Current?.TournamentManager?.GetLeaderboard();
                if (lb == null) return 0;
                foreach (var kvp in lb)
                    if (kvp.Key == Hero.MainHero) return kvp.Value;
                return 0;
            }
            catch { return 0; }
        }

        private static int ComputeDaysSinceLastMeeting(NpcProfile profile, IGameStateBridge bridge)
        {
            if (profile == null || profile.Events == null || profile.Events.Count == 0)
                return -1;

            var latestEventDay = profile.Events.Max(e => e.gameDay);
            var currentDay = bridge.GetCurrentGameDay();

            return Math.Max(0, currentDay - latestEventDay);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        ///   If the profile has no events, treat the player as a Stranger; this
        ///   is more LLM-friendly than the bridge's default "Free", which would
        ///   imply the NPC already knows the player exists.
        /// </summary>
        private static PlayerStatusVsNpc ResolvePlayerStatus(
            Hero hero,
            NpcProfile profile,
            IGameStateBridge bridge)
        {
            PlayerStatusVsNpc fromBridge = bridge.GetPlayerStatus(hero);

            var firstMeeting = profile == null || profile.Events == null || profile.Events.Count == 0;

            if (firstMeeting && fromBridge == PlayerStatusVsNpc.Free)
                return PlayerStatusVsNpc.Stranger;

            return fromBridge;
        }

        #endregion
    }
}