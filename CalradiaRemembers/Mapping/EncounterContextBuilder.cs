// Code written by Gabriel Mailhot, 17/05/2026.

#region

using System;
using System.Linq;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
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
            MercenaryOfferKingdom    = ResolveMercenaryOfferKingdom(hero),
            LoveMatchEligible        = ResolveLoveMatchEligible(hero, profile),
            LoveMatchBlessed         = ResolveLoveMatchBlessed(hero),
            ConsortEligible          = ResolveConsortEligible(hero, profile),
            PowerBalanceNote         = ResolvePowerBalanceNote(hero),
            MarriageProspects        = MarriageProspectResolver.Build(hero)
            // PlayerPurseGold is deliberately NOT fed: an NPC cannot see the player's liquid
            // coin, and naming the exact sum broke immersion. Affordability is enforced by the
            // bridge when the hire is executed, not foreseen in the prompt.
        };

        #region private

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
        ///   Computes a prose note describing the power differential between the player's
        ///   realm and the NPC's kingdom. Returns null when the balance is roughly even
        ///   (within a 1.5× ratio) or when neither party belongs to a kingdom.
        ///   Only issued for lords — wanderers and notables have no kingdom standing to weigh.
        /// </summary>
        private static string ResolvePowerBalanceNote(Hero hero)
        {
            try
            {
                if (hero == null || !hero.IsLord || Hero.MainHero == null) return null;

                Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
                Kingdom npcKingdom    = hero.Clan?.Kingdom;

                // Both must be in a kingdom for the comparison to mean anything.
                if (playerKingdom == null && npcKingdom == null) return null;

                int playerFiefs = CountKingdomFiefs(playerKingdom);
                int npcFiefs    = CountKingdomFiefs(npcKingdom);

                bool atWar = playerKingdom != null && npcKingdom != null
                          && FactionManager.IsAtWarAgainstFaction(playerKingdom, npcKingdom);

                float ratio = npcFiefs > 0
                    ? (float) playerFiefs / npcFiefs
                    : (playerFiefs > 0 ? 10f : 1f);

                if (atWar)
                {
                    if (ratio >= 2f)
                        return "Your kingdom is at war with the player's realm, and the balance of power is heavily against you — they hold far more territory. Your position is precarious.";
                    if (ratio <= 0.5f)
                        return "Your kingdom is at war with the player's realm, and you hold a clear advantage — your forces and territory outmatch theirs.";
                    return "Your kingdom is at war with the player's realm. The outcome remains uncertain; neither side commands a decisive advantage.";
                }

                // At peace — only remark on a significant asymmetry (≥ 2× ratio, ≥ 3 fiefs).
                if (ratio >= 2f && playerFiefs >= 3)
                    return $"The player's realm is considerably more powerful than yours — they hold {playerFiefs} fiefs to your kingdom's {npcFiefs}. You are aware of this disparity.";
                if (ratio <= 0.5f && npcFiefs >= 3)
                    return $"Your kingdom outmatches the player's realm — {npcFiefs} fiefs to their {playerFiefs}. You hold the stronger position.";

                // Clan-level: NPC holds nothing while player's clan does.
                bool npcClanHasFiefs = hero.Clan?.Settlements != null
                    && hero.Clan.Settlements.Any(s => s.IsTown || s.IsCastle);
                bool playerClanHasFiefs = Clan.PlayerClan?.Settlements != null
                    && Clan.PlayerClan.Settlements.Any(s => s.IsTown || s.IsCastle);
                if (!npcClanHasFiefs && playerClanHasFiefs)
                    return "Your clan currently holds no fiefs, while the player's clan does. That gap in standing is not lost on you.";

                return null;
            }
            catch { return null; }
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