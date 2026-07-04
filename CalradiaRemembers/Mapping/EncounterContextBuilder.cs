// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Verbs;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TraitFacts = CalradiaRemembers.Logic.Grudges.TraitFacts;

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
      ///   How many in-game days a battlefield defeat stays "fresh" enough to temper a lord's
      ///   defiance — roughly a season's quarter. Older losses fade from the power calculus.
      /// </summary>
      private const int RecentDefeatWindowDays = 21;

      /// <summary>Registration order of the court verbs taught AFTER pledge_against, in <see cref="VerbRegistry" />.</summary>
      private static readonly string[] CourtVerbsAfterPledge = {"release_prisoner"};

      /// <summary>Registration order of the court verbs taught BEFORE pledge_against, in <see cref="VerbRegistry" />.</summary>
      private static readonly string[] CourtVerbsBeforePledge = {"give_influence", "lend_troops", "sway_opinion"};

      /// <summary>DIVORCE, Phase 1: end_marriage is taught last, after every court verb above.</summary>
      private static readonly string[] DivorceVerbs = {"end_marriage"};

      /// <summary>
      ///   Grudges pillar, resolution + discovery halves: make_amends then grudge_terms, taught last of all, after every
      ///   verb above.
      /// </summary>
      private static readonly string[] GrudgeVerbs = {"make_amends", "grudge_terms"};

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
         IReadOnlyList<WitnessEntry> witnesses = null,
         bool privacyRequested = false,
         bool isWitnessExchangeTurn = false,
         bool isLastWitnessExchange = false,
         bool isCollectiveCaptiveScene = false,
         bool isCaptorScene = false,
         PlayerStatusVsNpc? playerStatusOverride = null,
         CaptiveSceneIntent captiveIntent = CaptiveSceneIntent.Interrogation,
         bool isFinalSceneBeat = false,
         bool captorIsBandit = false,
         CaptiveSceneStage sceneStage = CaptiveSceneStage.Intro,
         CaptiveAggressorKind aggressorKind = CaptiveAggressorKind.Lead,
         bool reactToPlayerIntervention = false,
         string captiveVictimName = null,
         bool playerCoercedAgainstCompanion = false,
         bool captiveVictimIsFemale = false,
         CompanionAudienceReason companionAudience = CompanionAudienceReason.None,
         bool audienceRetirementIsLanded = false)
      {
         int daysSinceLastMeeting = ComputeDaysSinceLastMeeting(profile, bridge);
         bool npcIsPrisonerOfAnother = IsPrisonerOfAnother(hero);
         PlayerStatusVsNpc playerStatus = playerStatusOverride ?? ResolvePlayerStatus(hero, profile, bridge);

         return new() {
            PlayerStatus = playerStatus,
            WarStatus = bridge.GetDiplomaticStatus(hero),
            DaysSinceLastMeeting = daysSinceLastMeeting,
            MeetingGapNote = MeetingGapDescriber.Describe(daysSinceLastMeeting),
            Scene = bridge.GetCurrentScene(),
            ContextualNames = string.IsNullOrWhiteSpace(playerMessage)
               ? null
               : ContextualNameResolver.Resolve(hero, playerMessage, bridge),
            KnownWanderers = ResolveKnownWanderers(hero, playerMessage),
            NpcCurrentActivity = bridge.GetCurrentActivity(hero),
            CurrentLocationNote = BuildLocationNote(hero),
            PlayerSpouseName = Hero.MainHero?.Spouse?.IsAlive == true
               ? Hero.MainHero.Spouse.Name?.ToString()
               : null,
            PlayerFatherName = FormatParent(Hero.MainHero?.Father),
            PlayerMotherName = FormatParent(Hero.MainHero?.Mother),
            PlayerClanTier = Hero.MainHero?.Clan?.Tier ?? 0,
            PlayerArenaRank = ResolveArenaRank(),
            PlayerArenaWins = ResolveArenaWins(),
            Witnesses = witnesses,
            PrivacyRequested = privacyRequested,
            IsWitnessExchangeTurn = isWitnessExchangeTurn,
            IsLastWitnessExchange = isLastWitnessExchange,
            IsCollectiveCaptiveScene = isCollectiveCaptiveScene,
            IsCaptorScene = isCaptorScene,
            CaptiveIntent = captiveIntent,
            CaptiveVictimName = captiveVictimName,
            PlayerCoercedAgainstCompanion = playerCoercedAgainstCompanion,
            CaptiveVictimIsFemale = captiveVictimIsFemale,
            CompanionAudience = companionAudience,
            AudienceRetirementIsLanded = audienceRetirementIsLanded,
            LeanLevel = ModSettings.LeanPrompt
               ? LeanPromptLevel.Lean
               : LeanPromptLevel.Full,
            IsFinalSceneBeat = isFinalSceneBeat,
            SceneStage = sceneStage,
            AggressorKind = aggressorKind,
            ReactToPlayerIntervention = reactToPlayerIntervention,
            CaptorIsBandit = captorIsBandit,
            PlayerLooksWealthy = captorIsBandit && PlayerLooksWealthy(),
            PlayerLooksImportant = captorIsBandit && PlayerLooksImportant(),
            CompanionAskingPrice = ResolveCompanionAskingPrice(hero),
            HeldLordPrisoners = ResolveHeldLordPrisoners(),
            NpcIsPrisonerOfAnother = npcIsPrisonerOfAnother,
            NpcCaptorName = npcIsPrisonerOfAnother
               ? hero.PartyBelongedToAsPrisoner?.MapFaction?.Name?.ToString()
               : null,
            MercenaryOfferKingdom = ResolveMercenaryOfferKingdom(hero),
            LordRecruitEligible = ResolveLordRecruitEligible(hero),
            SchemeAgentTargetName = ResolveSchemeAgentTarget(hero),
            SchemeTargetsThisNpc = ResolveSchemeTargetsThisNpc(hero),
            InterceptionReason = ResolveInterceptionReason(hero),
            StanceNote = StanceService.Describe(profile),
            StanceConsequenceHint = ResolveStanceConsequenceHint(hero, profile),
            GrudgeNote = ResolveGrudgeNote(hero, profile),
            RegardShadowNote = ResolveRegardShadowNote(hero, profile),
            AppreciationNote = ResolveAppreciationNote(hero, profile),
            SpouseDivorceDemandNote = ResolveSpouseDivorceDemandNote(hero),
            PlayerEndOwnMarriageNote = ResolvePlayerEndOwnMarriageNote(hero),
            StationGuidelinesOverride = ResolveStationGuidelines(hero),
            LoveMatchEligible = ResolveLoveMatchEligible(hero, profile),
            LoveMatchBlessed = ResolveLoveMatchBlessed(hero),
            ConsortEligible = ResolveConsortEligible(hero, profile),
            ConceptionRisk = ResolveConceptionRisk(hero),
            PowerBalanceNote = ResolvePowerBalanceNote(hero, profile),
            MarriageProspects = MarriageProspectResolver.Build(hero),
            WorldRumorsBlock = ResolveWorldRumors(hero),
            RealmNewsLine = ResolveRealmNews(hero),
            PlayerDeedsHeard = ResolvePlayerDeedsHeard(hero),
            CanGatherNews = ResolveCanGatherNews(hero),
            CompanionOnErrand = ResolveCompanionOnErrand(hero),
            CompanionNewsReport = CompanionMissionBehavior.Instance?.ConsumeNewsReport(hero),
            CompanionMoodNote = ResolveCompanionMoodNote(hero),
            CompanionCampNote = ResolveCompanionCampNote(hero),
            IntimacyBargainEligible = IntimacyBargainEligibility.IsEligible(hero),
            IntimacyLeverageActive = IntimacyBargainEligibility.IsEligible(hero) && profile?.IntimacyLeverageHeld == true,
            BastardMotherTone = ResolveBastardMotherTone(hero),
            BastardBlackmailDemand = BastardBehavior.Instance?.BlackmailDemandOver(hero) ?? 0,
            ExtraActionTeachings = ResolveExtraActionTeachings(hero, profile, playerStatus, bridge, playerMessage)
            // PlayerPurseGold is deliberately NOT fed: an NPC cannot see the player's liquid
            // coin, and naming the exact sum broke immersion. Affordability is enforced by the
            // bridge when the hire is executed, not foreseen in the prompt.
         };
      }

      /// <summary>
      ///   The PLAYER's own deeds that have reached this NPC by word, attributed to the person they are
      ///   speaking with and tagged with how each one lands on them (a wound to their side, a boon to it)
      ///   via the pure <see cref="PlayerDeedValence" />. The influence loop — the world reflecting the
      ///   player's acts back. Null when none have reached them, so the section is skipped entirely.
      /// </summary>
      internal static string ResolvePlayerDeedsHeard(Hero hero)
      {
         try
         {
            IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> rated = WorldEventStore.Instance?.GetKnownEventsRated(
               hero, (int) CampaignTime.Now.ToDays, 8);

            if (rated == null || rated.Count == 0) return null;

            string obsFactionId = hero?.MapFaction?.StringId;
            string playerFactionId = Hero.MainHero?.MapFaction?.StringId;
            Clan obsClan = hero?.Clan;

            var lines = new List<string>();
            foreach ((WorldEvent e, RumorConfidence conf) in rated)
            {
               if (!e.InvolvesPlayer || string.IsNullOrWhiteSpace(e.Summary)) continue;

               bool aggrieved = obsFactionId != null && e.AggrievedFactionIds != null && e.AggrievedFactionIds.Contains(obsFactionId);
               bool kinHarmed = obsClan != null && e.AggrievedActorIds != null && e.AggrievedActorIds.Any(id => IsClanmate(id, obsClan, hero));
               bool sharesPlayer = obsFactionId != null && obsFactionId == playerFactionId;

               string valence;
               switch (PlayerDeedValence.Classify(aggrieved, kinHarmed, sharesPlayer))
               {
                  case DeedValence.Wound: valence = "(this cut against you or your own) "; break;
                  case DeedValence.Boon: valence = "(this served your side) "; break;
                  default: valence = ""; break;
               }

               string doubt = conf == RumorConfidence.Distant
                  ? " — though none can fully vouch for it"
                  : "";
               lines.Add($"- {valence}{e.Summary.TrimEnd('.', ' ')}{doubt}.");

               if (lines.Count >= 3) break;
            }

            return lines.Count > 0
               ? string.Join("\n", lines)
               : null;
         }
         catch { return null; }
      }

      #region private

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
               return $"You are in {s.Name}, which belongs to {owner.Name}" +
                      (ownerClan.Name != null
                         ? $" of clan {ownerClan.Name}"
                         : "") +
                      ". It is NOT your holding — you are here as a guest, not its lord.";

            return $"You are in {s.Name}.";
         }
         catch
         {
            return null;
         }
      }

      private static int ComputeDaysSinceLastMeeting(NpcProfile profile, IGameStateBridge bridge)
      {
         if (profile == null || profile.Events == null || profile.Events.Count == 0)
            return -1;

         int latestEventDay = profile.Events.Max(e => e.gameDay);
         int currentDay = bridge.GetCurrentGameDay();

         return Math.Max(0, currentDay - latestEventDay);
      }

      /// <summary>Counts towns and castles belonging to a kingdom; returns 0 for a null kingdom.</summary>
      private static int CountKingdomFiefs(Kingdom kingdom)
      {
         if (kingdom == null) return 0;
         var count = 0;
         foreach (Settlement s in kingdom.Settlements)
            if (s.IsTown || s.IsCastle)
               count++;

         return count;
      }

      /// <summary>
      ///   Formats a parent's display name for the prompt, marking the deceased with
      ///   "the late …" so the NPC never speaks of a dead parent as living. Null when
      ///   the parent is unknown or unnamed.
      /// </summary>
      private static string FormatParent(Hero parent)
      {
         var name = parent?.Name?.ToString();

         if (string.IsNullOrWhiteSpace(name)) return null;

         return parent.IsAlive
            ? name
            : $"the late {name}";
      }

      /// <summary>True when the harmed hero (by StringId) is a clansman of the observer — a kinsman struck.</summary>
      private static bool IsClanmate(string heroId, Clan clan, Hero observer)
      {
         if (string.IsNullOrEmpty(heroId)) return false;
         Hero h = Hero.FindFirst(x => x.StringId == heroId);

         return h != null && h != observer && h.Clan == clan;
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

      /// <summary>True when the NPC is a captive of someone other than the player (case B: a rescue bargain).</summary>
      private static bool IsPrisonerOfAnother(Hero hero)
         => hero?.IsPrisoner == true && hero.PartyBelongedToAsPrisoner?.LeaderHero != Hero.MainHero;

      /// <summary>
      ///   True when the player's forces beat this NPC in a battle within the recency window. Read
      ///   from the profile memory the battle feeder already stamps on a defeated enemy lord
      ///   ("…saw you put their host to flight"), so no new persisted state is needed. Kept in sync
      ///   with <c>WorldEventFeeders.RecordBattlePerformanceIntoProfiles</c>.
      /// </summary>
      private static bool LostBattleToPlayerRecently(NpcProfile profile)
      {
         if (profile?.Events == null || profile.Events.Count == 0) return false;
         var today = (int) CampaignTime.Now.ToDays;
         foreach (NotableEvent e in profile.Events)
         {
            if (today - e.gameDay > RecentDefeatWindowDays) continue;

            if (!string.IsNullOrEmpty(e.summary) &&
                e.summary.IndexOf("put their host to flight",
                   StringComparison.OrdinalIgnoreCase) >=
                0)
               return true;
         }

         return false;
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
      ///   True when the player's worn battle gear is valuable enough that a brigand could
      ///   guess they are no common traveler. Sums the denar value of every equipped item
      ///   (arms, armor, mount). The threshold sits well above a starting kit but below a
      ///   fully end-game noble loadout, so it reads "this one has money behind them".
      /// </summary>
      private static bool PlayerLooksWealthy()
      {
         try
         {
            var h = Hero.MainHero;
            Equipment eq = h?.BattleEquipment;

            if (eq == null) return false;

            var value = 0;
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i <= EquipmentIndex.HorseHarness; i++)
            {
               ItemObject item = eq[i].Item;
               if (item != null) value += item.Value;
            }

            return value >= 8000;
         }
         catch { return false; }
      }

      /// <summary>
      ///   Appreciations pillar: every dominant LIVE appreciation this NPC feels toward the player, as one
      ///   chronicler-voice sentence each (numberless, knownness-mirrored, mirroring
      ///   <see cref="ResolveGrudgeNote" /> exactly), each TARNISHED by the holder's own dominant live
      ///   grudge (the same lookup <see cref="ResolveGrudgeNote" /> uses) via
      ///   <see cref="Logic.Appreciations.AppreciationPolicy.TarnishedMagnitude" /> before rendering — a
      ///   fresh grievance sours a standing warmth. A line that tarnishes to zero is dropped rather than
      ///   shown at full warmth. Null when the NPC feels no live kindness worth voicing this turn.
      /// </summary>
      private static string ResolveAppreciationNote(Hero hero, NpcProfile profile)
      {
         try
         {
            if (hero == null) return null;

            var today = (int) CampaignTime.Now.ToDays;
            IReadOnlyList<Appreciation> live = AppreciationBehavior.Instance?.LiveAppreciationsFor(hero, today);

            if (live == null || live.Count == 0) return null;

            // The same dominant-live-grudge magnitude ResolveGrudgeNote/ResolveRegardShadowNote already
            // resolve, read independently here so Appreciations stays fully decoupled from Grudges'
            // internal types (only the plain int crosses the boundary).
            int activeGrudgeMagnitude = ResolveDominantLiveGrudgeMagnitude(hero);

            Logic.Appreciations.TraitFacts traits = AppreciationBridge.BuildTraitFacts(hero);
            var lines = new List<string>();
            foreach (Appreciation appreciation in live)
            {
               int effective = AppreciationPolicy.EffectiveMagnitude(appreciation, today, traits);
               int tarnished = AppreciationPolicy.TarnishedMagnitude(effective, activeGrudgeMagnitude);
               string line = AppreciationNarrator.PromptLine(appreciation, appreciation.KnownToPlayer, tarnished);
               if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
            }

            return lines.Count == 0
               ? null
               : string.Join("\n", lines);
         }
         catch { return null; }
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
            List<KeyValuePair<Hero, int>> lb = Campaign.Current?.TournamentManager?.GetLeaderboard();

            if (lb == null) return 0;

            foreach (KeyValuePair<Hero, int> kvp in lb)
               if (kvp.Key == Hero.MainHero)
                  return kvp.Value;

            return 0;
         }
         catch { return 0; }
      }

      /// <summary>
      ///   When this NPC is the mother of a still-hidden bastard of the player's, her disposition as a tone
      ///   string ("mercenary"/"longing"/"resentful"/"pragmatic") so the prompt can have her raise the child
      ///   in character. Null otherwise. The conversation is one of TWO surfaces — the demand letter is the
      ///   other — and paying is settled by the bridge, never by the NPC's word.
      /// </summary>
      private static string ResolveBastardMotherTone(Hero hero)
      {
         var bastards = BastardBehavior.Instance;

         if (bastards == null || hero == null || !bastards.IsBastardMotherOfHidden(hero)) return null;

         return bastards.MotherDisposition(hero) switch {
            BastardMotherDisposition.Mercenary => "mercenary",
            BastardMotherDisposition.Longing => "longing",
            BastardMotherDisposition.Resentful => "resentful",
            _ => "pragmatic"
         };
      }

      /// <summary>
      ///   True when the speaking hero is one of the player's own companions, in the party, and free of an
      ///   active errand — so the errand offer is taught. The per-errand skill floor and every other guard
      ///   are re-checked at dispatch (the prompt is advice, the bridge is law), so this stays type-agnostic.
      /// </summary>
      private static bool ResolveCanGatherNews(Hero hero)
      {
         if (hero == null || hero == Hero.MainHero || !hero.IsAlive) return false;
         if (!(hero.IsPlayerCompanion || hero.Clan == Clan.PlayerClan)) return false;
         if (hero.PartyBelongedTo != MobileParty.MainParty) return false;

         return CompanionMissionBehavior.Instance?.IsAway(hero.StringId) != true;
      }

      /// <summary>
      ///   The vanilla hiring price when this hero is genuinely recruitable RIGHT NOW:
      ///   a living, free wanderer not yet in anyone's service, while the player's clan
      ///   still has a companion slot. Null otherwise — the recruitment section is then
      ///   not taught, so the NPC never agrees to a hire the game would refuse.
      /// </summary>
      private static int? ResolveCompanionAskingPrice(Hero hero)
         => RecruitmentPricing.GetHiringPrice(hero);

      /// <summary>
      ///   A directive for an own-companion who feels strongly (warmly or bitterly) about a FELLOW
      ///   companion — the one they feel most strongly about. Null when no peer stirs a strong opinion,
      ///   so the camp section is skipped.
      /// </summary>
      private static string ResolveCompanionCampNote(Hero hero)
      {
         if (hero == null || hero == Hero.MainHero || !hero.IsAlive || !hero.IsPlayerCompanion) return null;

         Hero other = null;
         var strongest = 0;
         foreach (Hero c in Hero.AllAliveHeroes)
         {
            if (c == hero || c == Hero.MainHero || !c.IsAlive || !c.IsPlayerCompanion) continue;
            int rel = hero.GetRelation(c);

            if (Math.Abs(rel) <= Math.Abs(strongest)) continue;
            strongest = rel;
            other = c;
         }

         if (other == null || !CompanionRapport.WorthRaising(strongest)) return null;

         return strongest > 0
            ? $"You think highly of your fellow companion {other.Name} — you value riding at their side."
            : $"You cannot abide your fellow companion {other.Name} — they grate on you, and you would as soon not share a camp.";
      }

      /// <summary>
      ///   A directive for an unhappy OWN-companion to voice their discontent — scaled to the happiness
      ///   band (restless → disgruntled → means to leave). Null for a content companion or a non-companion,
      ///   so the section is skipped.
      /// </summary>
      private static string ResolveCompanionMoodNote(Hero hero)
      {
         if (hero == null || hero == Hero.MainHero || !hero.IsAlive) return null;
         if (!(hero.IsPlayerCompanion || hero.Clan == Clan.PlayerClan)) return null;

         int score = CompanionHappinessService.Get(hero);

         if (score >= 50) return null; // content or better — nothing to voice

         if (CompanionHappiness.WantsToLeave(score))
            return "You are bitterly unhappy in the player's service and mean to LEAVE. You may say so plainly, " + "or give them one last chance to make it right — but your patience is at its end.";
         if (score < 25)
            return "You are disgruntled in the player's service and have begun to think of leaving. You may voice " + "your grievance — what has soured you, and what it would take to keep you.";

         return "You are restless in the player's service — your patience is thinning. You may let a coolness or a " + "small complaint show, without yet threatening to leave.";
      }

      /// <summary>
      ///   True when this hero is one of the player's own companions currently AWAY on an errand — the player
      ///   has ridden out and met them. Lets the prompt teach the recall_companion action so the player can
      ///   order them to abandon the task and return. (Away companions are NOT in the main party, so unlike the
      ///   dispatch gate we must not require it.)
      /// </summary>
      private static bool ResolveCompanionOnErrand(Hero hero)
      {
         if (hero == null || hero == Hero.MainHero || !hero.IsAlive) return false;
         if (!(hero.IsPlayerCompanion || hero.Clan == Clan.PlayerClan)) return false;

         return CompanionMissionBehavior.Instance?.IsAway(hero.StringId) == true;
      }

      /// <summary>
      ///   Which party, if either, could conceive from a completed vaginal act this scene — the pure
      ///   <see cref="ConceptionResolver" /> mapped to the prompt's <see cref="ConceptionRisk" />. Just
      ///   sex + life + fertile age; the prompt itself only TEACHES the action inside an intimate, adult
      ///   context, so computing it unconditionally here is harmless.
      /// </summary>
      private static ConceptionRisk ResolveConceptionRisk(Hero hero)
      {
         var player = Hero.MainHero;

         if (hero == null || player == null || hero == player) return ConceptionRisk.None;

         ConceptionRole role = ConceptionResolver.Resolve(
            player.IsFemale, player.IsAlive, player.Age,
            hero.IsFemale, hero.IsAlive, hero.Age);

         switch (role)
         {
            case ConceptionRole.PlayerIsMother: return ConceptionRisk.PlayerCanConceive;
            case ConceptionRole.NpcIsMother: return ConceptionRisk.NpcCanConceive;
            default: return ConceptionRisk.None;
         }
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
         if (profile.Romantic.IsConsort) return false; // already a consort
         RomanticStatus status = profile.Romantic.Status;

         if (status < RomanticStatus.Intimate) return false;
         if (status == RomanticStatus.Committed) return false;

         return profile.ReputationWithPlayer >= 60;
      }

      /// <summary>
      ///   Divorce, Phase 2b: set when THIS NPC is the player's own spouse and currently has a pending
      ///   divorce demand (<see cref="SpouseDivorceDemandBehavior.IsDemanding" />). Composes the full
      ///   directive in the spouse's own voice, folding in the accept_divorce / decline_divorce [ACTION]
      ///   teaching directly (see <see cref="EncounterContext.SpouseDivorceDemandNote" />'s own doc comment
      ///   for why this bypasses <see cref="VerbRegistry" />/ExtraActionTeachings: a single demand naming
      ///   exactly one NPC, not a composed set of independently-eligible verbs). Null otherwise.
      /// </summary>
      private static string ResolveSpouseDivorceDemandNote(Hero hero)
      {
         try
         {
            if (hero == null || Hero.MainHero == null) return null;
            if (hero != Hero.MainHero.Spouse) return null;
            if (SpouseDivorceDemandBehavior.Instance?.IsDemanding(hero.StringId) != true) return null;

            var sb = new StringBuilder();
            sb.AppendLine("SPOUSE_DIVORCE_DEMAND, YOU ARE PRESSING TO END THIS MARRIAGE:");
            sb.AppendLine("You have grown truly, deeply unhappy in this marriage, and you have written to the player of");
            sb.AppendLine("it. Now, in this very conversation, you press them plainly to consent to ending it. Speak as");
            sb.AppendLine("someone genuinely wounded and resolved, never petty or theatrical, and let your own nature set");
            sb.AppendLine("the register: cold resolve, quiet grief, or open reproach.");
            sb.AppendLine("If the player CONSENTS, agrees, or otherwise accepts ending the marriage, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: accept_divorce");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine("If the player FIRMLY AND CLEARLY refuses to end it, you may instead emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: decline_divorce");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine("If they deflect, stall, or you are simply unsure, emit neither action and react in character;");
            sb.AppendLine("the matter will come up again.");
            sb.AppendLine();

            return sb.ToString();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MAPPING] ResolveSpouseDivorceDemandNote failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Divorce, Phase 2a: set when THIS NPC is the player's own LIVING spouse, teaching the
      ///   player-initiated end_own_marriage action. Coexists cleanly alongside
      ///   <see cref="ResolveSpouseDivorceDemandNote" />'s own note when the spouse also happens to be
      ///   demanding a divorce of their own (Phase 2b) — both notes render together and name distinct
      ///   triggers (the spouse's demand vs the player's own free choice), so nothing here needs to gate on
      ///   that flag. Null when this NPC is not the player's living spouse.
      /// </summary>
      private static string ResolvePlayerEndOwnMarriageNote(Hero hero)
      {
         try
         {
            if (hero is not {IsAlive: true} || Hero.MainHero == null) return null;
            if (hero != Hero.MainHero.Spouse) return null;

            var sb = new StringBuilder();
            sb.AppendLine("PLAYER_END_OWN_MARRIAGE, THE PLAYER MAY CHOOSE TO END THIS MARRIAGE:");
            sb.AppendLine("You are speaking with the player's own spouse. The player may, in their own words, express a");
            sb.AppendLine("clear wish to end this marriage. If they state plainly and unambiguously that they want the");
            sb.AppendLine("marriage to end now, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: end_own_marriage");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine("Do not emit this on mere venting, a passing complaint, or an abstract question about");
            sb.AppendLine("divorce; only when the player clearly states they want to end the marriage now.");
            sb.AppendLine();

            return sb.ToString();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MAPPING] ResolvePlayerEndOwnMarriageNote failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   The holder's dominant live grudge's TODAY's effective magnitude (0 when none), the exact number
      ///   <see cref="AppreciationPolicy.TarnishedMagnitude" /> needs to sour a standing appreciation.
      /// </summary>
      private static int ResolveDominantLiveGrudgeMagnitude(Hero hero)
      {
         IReadOnlyList<Grudge> live = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, (int) CampaignTime.Now.ToDays);

         if (live == null || live.Count == 0) return 0;

         TraitFacts traits = GrudgeBridge.BuildTraitFacts(hero);

         return GrudgePolicy.EffectiveMagnitude(live[0], (int) CampaignTime.Now.ToDays, traits);
      }

      /// <summary>
      ///   The tone arbiter's ruling for the dominant live grudge (already resolved by the caller), from its today's
      ///   effective magnitude, knownness, and the NPC's personal reputation with the player.
      /// </summary>
      private static ToneRuling ResolveDominantToneRuling(NpcProfile profile, Grudge dominant, int today, TraitFacts traits)
      {
         int effective = GrudgePolicy.EffectiveMagnitude(dominant, today, traits);
         int reputation = profile?.ReputationWithPlayer ?? 0;

         return GrudgeToneArbiter.Resolve(effective, dominant.KnownToPlayer, reputation);
      }

      /// <summary>
      ///   RP with teeth: composes the ONE generic <see cref="EncounterContext.ExtraActionTeachings" />
      ///   block from the court-action eligibility resolvers in <see cref="CourtActionResolver" /> (the four
      ///   increment-1 verbs, plus increment 2's <c>pledge_against</c>). Each verb's own section (and its
      ///   [ACTION] format) is included only when currently eligible, so the LLM is never taught a verb the
      ///   bridge would refuse. Null when none apply right now, or while the player is this NPC's captive
      ///   (none of these favours make sense from a dungeon). The four increment-1 verbs are composed via
      ///   <see cref="VerbRegistry" /> (<see cref="IConversationVerb" />); pledge_against stays inline here
      ///   (untouched by this spike) between the two ComposeTeachings calls, preserving its original position.
      /// </summary>
      private static string ResolveExtraActionTeachings(Hero hero,
                                                        NpcProfile profile,
                                                        PlayerStatusVsNpc playerStatus,
                                                        IGameStateBridge bridge,
                                                        string playerMessage)
      {
         if (hero == null || profile == null) return null;
         if (playerStatus == PlayerStatusVsNpc.Captive) return null;

         var sb = new StringBuilder();
         var verbCtx = new VerbContext(hero, profile, bridge, playerMessage, null);

         // give_influence, lend_troops, sway_opinion: migrated to the IConversationVerb contract
         // (CalradiaRemembers.Verbs); the pledge_against block below still sits between these and
         // release_prisoner, so the two ComposeTeachings calls keep that exact interleaving.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, CourtVerbsBeforePledge));

         string pledgeRival = CourtActionResolver.ResolvePledgeRivalName(hero, profile);
         if (pledgeRival != null)
         {
            sb.AppendLine("PLEDGE_AGAINST, VOWING TO MOVE AGAINST A RIVAL WHO HAS WRONGED YOU:");
            sb.AppendLine($"You hold real, standing enmity toward {pledgeRival} — enough that you could, in this");
            sb.AppendLine("conversation, pledge to the player that you will move against them. This is a POLITICAL");
            sb.AppendLine("commitment (a plot set in motion against them, not open war): only emit this when you are");
            sb.AppendLine("genuinely resolved, never as idle talk — the player will hold you to your word.");
            sb.AppendLine("When you have genuinely resolved to do this, and only then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: pledge_against");
            sb.AppendLine($"target: {pledgeRival}");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine();
         }

         // release_prisoner: migrated to the IConversationVerb contract as well (see above).
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, CourtVerbsAfterPledge));

         // end_marriage (DIVORCE, Phase 1): the first verb designed for the contract from the start.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, DivorceVerbs));

         // make_amends + grudge_terms (Grudges pillar, resolution + discovery halves): taught last of all.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, GrudgeVerbs));

         return sb.Length == 0
            ? null
            : sb.ToString();
      }

      /// <summary>
      ///   Every dominant LIVE grudge as one chronicler-voice sentence each (numberless, knownness-mirrored),
      ///   plus the tone arbiter's ruling line LAST, so it reads as the final instruction. The dominant
      ///   grudge is <c>live[0]</c> (<see cref="GrudgeBehavior.LiveGrudgesFor" /> is already ranked by
      ///   today's effective magnitude via <see cref="GrudgePolicy.SelectDominant" />). Null when the NPC
      ///   nurses no live grudge worth voicing this turn.
      /// </summary>
      private static string ResolveGrudgeNote(Hero hero, NpcProfile profile)
      {
         try
         {
            if (hero == null) return null;

            var today = (int) CampaignTime.Now.ToDays;
            IReadOnlyList<Grudge> live = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);

            if (live == null || live.Count == 0) return null;

            TraitFacts traits = GrudgeBridge.BuildTraitFacts(hero);
            var lines = new List<string>();
            foreach (Grudge grudge in live)
            {
               int effective = GrudgePolicy.EffectiveMagnitude(grudge, today, traits);
               string line = GrudgeNarrator.PromptLine(grudge, grudge.KnownToPlayer, effective);
               if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
            }

            if (lines.Count == 0) return null;

            // Tone arbiter (owner ruling: a strong grudge DOMINATES the tone): appended LAST so it stands
            // as the final instruction, after every grudge sentence above.
            string toneLine = GrudgeToneArbiter.Line(ResolveDominantToneRuling(profile, live[0], today, traits), live[0].KnownToPlayer);
            if (!string.IsNullOrWhiteSpace(toneLine)) lines.Add(toneLine);

            return string.Join("\n", lines);
         }
         catch { return null; }
      }

      /// <summary>
      ///   A prompt-ready list of the LORD captives the player currently holds (name + faction), or
      ///   null when none. Notable/wanderer prisoners are excluded — a prisoner bargain is about lords.
      /// </summary>
      private static string ResolveHeldLordPrisoners()
      {
         List<string> lines = MobileParty.MainParty?.PrisonRoster?.GetTroopRoster()
                                         ?.Select(el => el.Character?.HeroObject)
                                         .Where(h => h is {IsLord: true, IsHumanPlayerCharacter: false})
                                         .Select(h => $"- {h.Name} ({h.MapFaction?.Name?.ToString() ?? h.Clan?.Name?.ToString() ?? "unknown"})")
                                         .ToList();

         return lines == null || lines.Count == 0
            ? null
            : string.Join("\n", lines);
      }

      /// <summary>
      ///   When this NPC sought the player out by intercepting them on the map, a one-shot opening
      ///   directive (consumed so it only colours the first turn). Null in an ordinary conversation.
      /// </summary>
      private static string ResolveInterceptionReason(Hero hero)
      {
         try { return InterceptionBehavior.Instance?.ConsumeInterceptionReason(hero); }
         catch { return null; }
      }

      /// <summary>
      ///   A SINGLE real wanderer the NPC could point the player to when asked where to hire a companion,
      ///   so the NPC names an actual hireable hero and their town instead of inventing one (the "Oros"
      ///   bug). Just ONE, drawn at random: a person would not plausibly know the comings and goings of
      ///   every wanderer in the land. Returns that one line when any is known, an EMPTY string when the
      ///   player asked but none are plausibly about (the prompt then sends them to the taverns), and null
      ///   when the player did not ask at all — so the section is skipped and costs no tokens. The gate is
      ///   the pure <see cref="RecruitIntentDetector" />. Only wanderers in the NPC's own town (certain) or
      ///   of the NPC's own culture (heard of) are eligible; one in the NPC's own tavern is preferred.
      /// </summary>
      private static string ResolveKnownWanderers(Hero hero, string playerMessage)
      {
         try
         {
            if (!RecruitIntentDetector.Detect(playerMessage)) return null;

            Settlement here = hero?.CurrentSettlement;
            CultureObject npcCulture = hero?.Culture;

            var local = new List<string>(); // in the NPC's own town — certain knowledge
            var heard = new List<string>(); // of the NPC's own culture — plausible hearsay
            foreach (Hero h in Hero.AllAliveHeroes)
            {
               if (h == Hero.MainHero || !h.IsAlive || !h.IsWanderer) continue;
               if (h.CompanionOf != null) continue; // already in someone's service
               if (h.PartyBelongedTo == MobileParty.MainParty) continue; // already with the player

               Settlement s = h.CurrentSettlement;

               if (s == null || !s.IsTown) continue; // only ones we can place (taverns are in towns)

               var name = h.Name?.ToString();

               if (string.IsNullOrWhiteSpace(name)) continue;

               if (here != null && s == here)
                  local.Add($"- {name} is here in {s.Name}, in the tavern.");
               else if (npcCulture != null && s.Culture == npcCulture)
                  heard.Add($"- {name} has been seen at {s.Name}.");
               // else: too far afield for this NPC to plausibly know — skip.
            }

            // Name ONE, at random — prefer one in the NPC's own tavern (certain) over distant hearsay.
            List<string> pool = local.Count > 0
               ? local
               : heard;

            if (pool.Count == 0) return string.Empty; // asked, but the NPC knows of none about

            return pool[MBRandom.RandomInt(pool.Count)];
         }
         catch { return null; }
      }

      /// <summary>
      ///   True when this lord could be persuaded to defect into the player's clan — gates the
      ///   <c>join_clan</c> teaching. Gathers live facts and delegates to the pure
      ///   <see cref="LordRecruitment" />; the bridge re-checks the same gate before sealing it.
      /// </summary>
      private static bool ResolveLordRecruitEligible(Hero hero)
      {
         try
         {
            LordRecruitmentFacts facts = LordRecruitmentFactsBuilder.Build(hero);

            return facts != null && LordRecruitment.IsEligible(facts);
         }
         catch { return false; }
      }

      /// <summary>True when A.2 already granted the family's blessing for this NPC.</summary>
      private static bool ResolveLoveMatchBlessed(Hero hero)
      {
         if (hero == null) return false;

         return MarriageApprovalBehavior.Instance?.IsApproved(hero.StringId) ?? false;
      }

      /// <summary>
      ///   True when the NPC themselves is a viable marriage candidate AND the personal
      ///   relation is deep enough (≥50) to make the prospect real. The prompt then
      ///   teaches the <c>marry</c> action; the bridge re-validates before executing.
      /// </summary>
      private static bool ResolveLoveMatchEligible(Hero hero, NpcProfile profile)
      {
         if (hero == null || Hero.MainHero == null) return false;
         if (Hero.MainHero.Spouse != null) return false; // player already wed
         if (hero.Clan == Clan.PlayerClan) return false; // can't marry own clan
         if (!MarriageProspectResolver.IsMarriageable(Hero.MainHero, hero)) return false;

         return (profile?.ReputationWithPlayer ?? 0) >= 50;
      }

      /// <summary>
      ///   Returns the NPC's kingdom name when the NPC is a lord in a kingdom AND the
      ///   player's clan is currently free of any kingdom obligation. Null otherwise —
      ///   the mercenary-offer section is then not taught at all.
      /// </summary>
      private static string ResolveMercenaryOfferKingdom(Hero hero)
      {
         if (hero is not {IsLord: true}) return null;

         Kingdom npcKingdom = hero.Clan?.Kingdom;

         if (npcKingdom == null) return null;

         // Player must not already be in any kingdom (vassal or mercenary).
         if (Clan.PlayerClan?.Kingdom != null) return null;

         return npcKingdom.Name?.ToString();
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

         bool firstMeeting = profile == null || profile.Events == null || profile.Events.Count == 0;

         if (firstMeeting && fromBridge == PlayerStatusVsNpc.Free)
            return PlayerStatusVsNpc.Stranger;

         return fromBridge;
      }

      /// <summary>
      ///   Gathers the live power-differential facts (fief counts, war state, captivity, a recent
      ///   defeat) and delegates the verdict to the pure <see cref="PowerBalance" /> classifier.
      ///   Only issued for lords — wanderers and notables have no kingdom standing to weigh.
      /// </summary>
      private static string ResolvePowerBalanceNote(Hero hero, NpcProfile profile)
      {
         try
         {
            if (hero is not {IsLord: true} || Hero.MainHero == null) return null;

            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
            Kingdom npcKingdom = hero.Clan?.Kingdom;

            return PowerBalance.DescribeFor(new PowerBalanceFacts {
               NpcIsLord = true,
               PlayerInKingdom = playerKingdom != null,
               NpcInKingdom = npcKingdom != null,
               PlayerKingdomFiefs = CountKingdomFiefs(playerKingdom),
               NpcKingdomFiefs = CountKingdomFiefs(npcKingdom),
               AtWar = playerKingdom != null && npcKingdom != null && FactionManager.IsAtWarAgainstFaction(playerKingdom, npcKingdom),
               NpcClanHasFiefs = hero.Clan?.Settlements != null && hero.Clan.Settlements.Any(s => s.IsTown || s.IsCastle),
               PlayerClanHasFiefs = Clan.PlayerClan?.Settlements != null && Clan.PlayerClan.Settlements.Any(s => s.IsTown || s.IsCastle),
               NpcIsPlayerPrisoner = IsPlayerPrisoner(hero),
               NpcRecentlyLostBattleToPlayer = LostBattleToPlayerRecently(profile)
            });
         }
         catch { return null; }
      }

      /// <summary>
      ///   The single greatest realm-level happening (a war declared, a peace struck) this NPC has heard
      ///   of — "the talk of all the realm". Drawn from the same awareness store as the rumours, best-known
      ///   first; null when none has reached them. Surfaced apart from the personal rumour list.
      /// </summary>
      private static string ResolveRealmNews(Hero hero)
      {
         try
         {
            IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> rated = WorldEventStore.Instance?.GetKnownEventsRated(
               hero, (int) CampaignTime.Now.ToDays, 8);

            if (rated == null || rated.Count == 0) return null;

            foreach ((WorldEvent e, RumorConfidence _) in rated)
            {
               if (string.IsNullOrWhiteSpace(e.Summary)) continue;

               if (e.Type == WorldEventType.WarDeclared || e.Type == WorldEventType.PeaceDeclared)
                  return $"- {e.Summary.TrimEnd('.', ' ')}.";
            }

            return null;
         }
         catch { return null; }
      }

      /// <summary>
      ///   Grudges pillar tone arbiter: a short clause the SDK's PromptBuilder places right after the
      ///   CURRENT STANCE regard line, when the dominant live grudge's ruling is Tempered or Dominates,
      ///   pointing the LLM at the grudge note before it even reaches it. Null when no live grudge shadows
      ///   the regard at all.
      /// </summary>
      private static string ResolveRegardShadowNote(Hero hero, NpcProfile profile)
      {
         try
         {
            if (hero == null) return null;

            var today = (int) CampaignTime.Now.ToDays;
            IReadOnlyList<Grudge> live = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);

            if (live == null || live.Count == 0) return null;

            TraitFacts traits = GrudgeBridge.BuildTraitFacts(hero);
            ToneRuling ruling = ResolveDominantToneRuling(profile, live[0], today, traits);

            return ruling == ToneRuling.WarmthUnshadowed
               ? null
               : "(but a grievance shadows this regard; see below)";
         }
         catch { return null; }
      }

      /// <summary>
      ///   When this NPC is secretly plotting against a rival and could enlist the player, the
      ///   target's name — gates the <c>scheme_assist</c> teaching. Delegates the eligibility to
      ///   <see cref="WorldEvents.SchemeStore" /> so the prompt and the bridge agree. Null otherwise.
      /// </summary>
      private static string ResolveSchemeAgentTarget(Hero hero)
      {
         try { return SchemeStore.Instance?.GetRecruitableTargetName(hero); }
         catch { return null; }
      }

      /// <summary>
      ///   True when a secret scheme is aimed at this NPC, so the player could warn them — gates the
      ///   <c>scheme_heed</c> teaching. Delegates to <see cref="WorldEvents.SchemeStore" />.
      /// </summary>
      private static bool ResolveSchemeTargetsThisNpc(Hero hero)
      {
         try { return SchemeStore.Instance?.HasSecretSchemeAgainst(hero) ?? false; }
         catch { return false; }
      }

      /// <summary>
      ///   How the NPC's earned posture inclines them to act this meeting — reads the live stance + their
      ///   traits through the pure <see cref="StanceConsequencePolicy" /> and maps the verdict to a prompt
      ///   line. "Stance with teeth", conversation side: a cold-shouldered lord won't deal, a secret killer
      ///   stays falsely civil, a true friend would warn you. Null when nothing is warranted.
      /// </summary>
      private static string ResolveStanceConsequenceHint(Hero hero, NpcProfile profile)
      {
         if (hero == null || profile == null) return null;

         StanceConsequence verdict = StanceConsequencePolicy.Resolve(
            StanceService.ToStance(profile),
            TraitLevel(hero, DefaultTraits.Honor),
            TraitLevel(hero, DefaultTraits.Valor),
            TraitLevel(hero, DefaultTraits.Mercy),
            TraitLevel(hero, DefaultTraits.Calculating));

         return verdict switch {
            StanceConsequence.ColdShoulder => "You are cold toward the player and unwilling to strike any bargain or do them a favour; keep them at arm's length.",
            StanceConsequence.Threat => "You hold the player in real enmity and do not hide your displeasure; you would not lift a finger to help them.",
            StanceConsequence.Plot => "You bear the player a cold, calculating hatred. Stay outwardly civil — even courteous — and give nothing away; you mean to undo them by quieter means, not here and not openly.",
            StanceConsequence.Intercept => "You despise the player and would gladly face them in open battle; your contempt is plain and unhidden.",
            StanceConsequence.Assassinate => "You want the player dead and mean to see it done — but you betray none of it here. Be smooth and false, courteous even, and never tip your hand.",
            StanceConsequence.WarmWelcome => "You are genuinely glad to see the player and meet them with real warmth.",
            StanceConsequence.Favor => "You hold the player in high regard and would gladly do them a good turn — offer help, a fair price, or an open door if the moment fits.",
            StanceConsequence.Warning => "You count the player a trusted friend; if you knew of anything moving against them, you would want to warn them.",
            _ => null
         };
      }

      /// <summary>
      ///   Grudges pillar: at most the per-NPC cap of dominant LIVE grudges this NPC nurses against the
      ///   player, each rendered as one chronicler-voice sentence via <see cref="GrudgeNarrator.PromptLine" />
      ///   (numberless, mirroring the knownness gate — named openly when known, concealed when hidden),
      ///   joined by newlines. Null when the NPC nurses no live grudge worth voicing this turn.
      /// </summary>
      /// <summary>
      ///   The station-matched behavior guidelines for this speaker (gang leader, notable, wanderer),
      ///   or null so the base lordly register applies. Fixes the player-reported "gang leader rising
      ///   from her throne" greeting: the voice register follows the SPEAKER's station, including for
      ///   witness-routed turns, since the context is rebuilt per speaker.
      /// </summary>
      private static string ResolveStationGuidelines(Hero hero)
      {
         if (hero == null) return null;

         string key = StationGuidelinesPolicy.ResolveStationKey(hero.Occupation.ToString());

         return StationGuidelinesPolicy.SelectStationText(key, NarrativeLoader.StationGuidelines);
      }

      /// <summary>
      ///   The recent THIRD-PARTY world happenings this NPC would plausibly have heard of (faction/
      ///   region/recency), pre-formatted as hearsay lines for the prompt. The player's OWN deeds are
      ///   pulled out here and routed to <see cref="ResolvePlayerDeedsHeard" /> instead, so they reach
      ///   the NPC attributed to the person before them rather than as faceless gossip. Null when
      ///   nothing relevant has reached them, so the "WHAT YOU'VE HEARD" section is skipped entirely.
      /// </summary>
      private static string ResolveWorldRumors(Hero hero)
      {
         try
         {
            IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> rated = WorldEventStore.Instance?.GetKnownEventsRated(
               hero, (int) CampaignTime.Now.ToDays, 6);

            if (rated == null || rated.Count == 0) return null;

            var lines = new List<string>();
            foreach ((WorldEvent e, RumorConfidence conf) in rated)
            {
               if (e.InvolvesPlayer || string.IsNullOrWhiteSpace(e.Summary)) continue; // player deeds → influence loop
               if (e.Type == WorldEventType.WarDeclared || e.Type == WorldEventType.PeaceDeclared) continue; // realm-level → RealmNewsLine
               string tag = conf == RumorConfidence.Distant ? "(a distant, unverified rumour) "
                  : conf == RumorConfidence.Secondhand ? "(heard secondhand) "
                  : "";
               lines.Add($"- {tag}{e.Summary.TrimEnd('.', ' ')}.");

               if (lines.Count >= 4) break;
            }

            return lines.Count > 0
               ? string.Join("\n", lines)
               : null;
         }
         catch { return null; }
      }

      private static int TraitLevel(Hero hero, TraitObject trait)
      {
         try { return hero?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
      }

      #endregion
   }
}