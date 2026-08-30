// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Appearance;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Duels;
using CalradiaRemembers.Escort;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Logic.Escort;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Social;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Styles;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Nemesis;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Retainer;
using CalradiaRemembers.Services;
using CalradiaRemembers.Social;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Verbs;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
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

      /// <summary>
      ///   Candidate pool size for the message-relevant world-fact scan (<see cref="ResolveQueryRelevantFacts" />):
      ///   generous on purpose, so a fact that is KNOWN but ranks below the small ambient top-N (the
      ///   "WHAT YOU'VE HEARD" cap) stays a candidate here instead of being invisible to a pointed question.
      /// </summary>
      private const int QueryRelevantCandidatePool = 50;

      /// <summary>Cap on how many message-relevant facts render in the new "WHAT YOU KNOW THAT TOUCHES ON THIS" block.</summary>
      private const int QueryRelevantMaxLines = 3;

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

      // Prisoner trade (08/07/2026): the NPC may BUY a captive the player holds, or SELL one his clan holds.
      private static readonly string[] PrisonerTradeVerbs = {"buy_prisoner", "sell_prisoner"};

      /// <summary>
      ///   04/07/2026: give_troops and spend_influence, the RECIPROCAL of the two court verbs above (the
      ///   PLAYER reinforces a needy NPC instead of the NPC favouring the player) — taught right after them.
      /// </summary>
      private static readonly string[] ReciprocalCourtVerbs = {"give_troops", "spend_influence"};

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
         bool isConversationOpening = false,
         bool isCollectiveCaptiveScene = false,
         bool isCaptorScene = false,
         bool companionActingOnCaptive = false,
         bool isBroughtCaptiveTurn = false,
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
         string captiveSceneFocus = null,
         string captiveSceneStyle = null,
         CompanionAudienceReason companionAudience = CompanionAudienceReason.None,
         bool audienceRetirementIsLanded = false,
         bool resolveInterception = true,
         bool isRoundTableTurn = false,
         bool isPostDuel = false,
         bool postDuelPlayerWon = false,
         string postDuelCourtedHeroName = null,
         bool? playerAttemptSucceeds = null)
      {
         int daysSinceLastMeeting = ComputeDaysSinceLastMeeting(profile, bridge);
         bool npcIsPrisonerOfAnother = IsPrisonerOfAnother(hero);
         PlayerStatusVsNpc playerStatus = playerStatusOverride ?? ResolvePlayerStatus(hero, profile, bridge);

         // Live presence: how this NPC is at the player's side right now. It suppresses the elapsed-gap note for
         // co-travellers (a companion riding with the player no longer sounds abandoned) and adds a present-tense
         // "you are with the player like THIS" line to the prompt.
         PresenceTier presence = PlayerPresenceResolver.Resolve(hero);
         string presencePlace = presence == PresenceTier.SamePlace
            ? Hero.MainHero?.CurrentSettlement?.Name?.ToString()
            : null;
         bool atSea = MobileParty.MainParty?.IsCurrentlyAtSea ?? false;

         // Duels: conditional teaching, the exact contract ResolveCompanionAskingPrice already uses (a field set
         // only when the game can truly honour the action). DuelEligibilityResolver is the ONE shared gate the
         // chat action handler re-checks at commit time, so the prompt-time teaching below and the bridge's
         // final word can never quietly diverge.
         bool duelChallengeAvailable = DuelEligibilityResolver.CanDuelNow(hero, profile, out DuelVenue duelVenue);
         string duelVenueLabel = duelChallengeAvailable
            ? DuelEligibilityResolver.VenueLabel(duelVenue)
            : null;

         // Logged only on the interesting verdict (a duel IS being offered this turn): an ordinary "not
         // eligible" would fire on nearly every conversation in the game and drown the log in noise.
         if (duelChallengeAvailable)
            ModLog.Info($"[DUEL] challenge_duel taught this turn vs {hero?.Name} ({duelVenueLabel}).");

         // follow_me / dismiss_escort: the same "prompt is advice, bridge is law" conditional-teaching contract
         // as CompanionAskingPrice/DuelChallengeAvailable above. EscortEligibilityPolicy.CanEscort is the ONE
         // shared gate FollowMeBehavior.TryStart re-checks at commit time, so the two can never quietly diverge.
         bool npcIsEscortingPlayer = ResolveNpcIsEscortingPlayer(hero);
         bool npcCanEscortPlayer = ResolveNpcCanEscortPlayer(hero, npcIsEscortingPlayer);

         // ride_with_me / part_ways: the idle-lord complement of follow_me, gated behind RetainerBehavior.Enabled
         // (ON by default via the MCM "Lords can ride in your party" toggle, the party-screen feel having been
         // validated in-game). RideWithMeEligibilityPolicy.CanRideWithPlayer is the ONE shared gate the bridge
         // re-checks at commit time, and it is the exact complement of EscortEligibilityPolicy (inverted on the
         // leads-a-party fact), so a hero is ever offered follow_me OR ride_with_me, never both.
         bool npcIsRidingWithPlayer = ResolveNpcIsRidingWithPlayer(hero);
         bool npcCanRideWithPlayer = ResolveNpcCanRideWithPlayer(hero, profile, npcIsRidingWithPlayer);

         // appoint_governor (2026-08-05, the reference personal-command verb): the 1:1 teaching gate, the same
         // "prompt is advice, bridge is law" conditional-teaching contract as the escort/duel offers above. The
         // pure GovernorAppointmentPolicy.CanOfferAppointment (mirroring the council's own ResolutionOfferingResolver
         // eligibility for the appointee) is the ONE gate CourtActionResolver.TryAppointGovernor re-checks at commit;
         // the vacant-fief list doubles as the target_fief grounding the prompt names verbatim.
         string appointableFiefs = ResolveAppointableGovernorFiefs();
         bool npcCanBeAppointedGovernor = GovernorAppointmentPolicy.CanOfferAppointment(
            playerLeadsClan: Clan.PlayerClan?.Leader == Hero.MainHero,
            appointeeInPlayerClan: hero?.Clan != null && hero.Clan == Clan.PlayerClan,
            appointeeAvailable: hero is {IsAlive: true} && !hero.IsPrisoner && hero != Hero.MainHero,
            hasVacantGovernableFief: !string.IsNullOrEmpty(appointableFiefs),
            // v1.34.2 governance hardening (finding 2): withhold the offer for an appointee already governing
            // another fief (one seat per governor) or away on an errand (cannot be seated in absentia). Both are
            // re-checked at commit by CourtActionResolver.TryAppointGovernor, the same prompt-is-advice contract.
            appointeeNotGoverningElsewhere: hero?.GovernorOf == null,
            appointeeNotAway: hero != null && CompanionMissionBehavior.Instance?.IsAway(hero.StringId) != true);

         // assign_party_role / rejoin_party / dispatch_mission (2026-08-05, party-command batch): the 1:1 teaching
         // gates, the same "prompt is advice, bridge is law" conditional-teaching contract as appoint_governor above.
         // Each pure PartyCommandOfferPolicy gate mirrors the council's own offering eligibility for that verb (the
         // TargetInPlayerParty fact for assign/dispatch, the TargetOutOfPlayerParty fact for rejoin), reduced to
         // facts about this ONE conversation NPC that CourtActionResolver.TryAssignPartyRole / TryRejoinParty /
         // TryDispatchMission re-check at commit.
         bool npcIsPlayerCompanion = hero?.IsPlayerCompanion == true;
         bool npcInMainParty = hero != null && hero.PartyBelongedTo == MobileParty.MainParty;
         bool npcAlive = hero is {IsAlive: true};
         bool npcIsPrisoner = hero?.IsPrisoner == true;
         bool npcLeadsOwnCompany = hero != null && hero.PartyBelongedTo?.LeaderHero == hero;
         bool npcAvailableForErrand = hero != null && CompanionMissionBehavior.Instance != null
            && CompanionMissionBehavior.Instance.IsAway(hero.StringId) != true;

         // dispatch_mission absorbed gather_news (2026-08-05): the errand offer covers the SAME population the old
         // gather_news gate did (CompanionMissionBehavior.IsUsableCompanion: a player companion OR a member of the
         // player's own clan, e.g. the player's own kin who ride along but are not "companions"), so no one who could
         // formerly be sent to gather news loses the offer. TryDispatchMission re-checks this same widened set.
         bool npcCanRideForPlayer = npcIsPlayerCompanion || hero != null && hero.Clan == Clan.PlayerClan;

         bool npcCanBeAssignedPartyRole = PartyCommandOfferPolicy.CanOfferAssignRole(npcIsPlayerCompanion, npcInMainParty);
         bool npcCanRejoinParty = PartyCommandOfferPolicy.CanOfferRejoinParty(npcIsPlayerCompanion, npcAlive, npcIsPrisoner, npcInMainParty, npcLeadsOwnCompany);
         bool npcCanBeDispatchedOnMission = PartyCommandOfferPolicy.CanOfferDispatchMission(npcCanRideForPlayer, npcInMainParty, npcAvailableForErrand);

         // grant_fief / revoke_fief / expel_from_clan (2026-08-05, suzerain-command batch): the 1:1 teaching gates,
         // the same "prompt is advice, bridge is law" conditional-teaching contract as the verbs above. grant/revoke
         // require the player to actually RULE a kingdom and the conversation NPC to be a vassal of a separate house
         // in that same kingdom (the FiefTransferPolicy gates), with a real fief to grant (from the player's own
         // clan) or revoke (from the NPC's own clan); the fief lists double as the target_fief grounding. expel
         // requires the NPC to be a live, present companion of the player's clan (the ExpulsionPolicy gate). Each is
         // re-checked at commit by CourtActionResolver.TryGrantFief / TryRevokeFief / TryExpelFromClan.
         bool playerIsKingdomLeader = Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom.Leader == Hero.MainHero;
         bool npcIsVassalInPlayerKingdom = hero?.Clan != null && hero.Clan != Clan.PlayerClan
            && hero.Clan.Kingdom != null && hero.Clan.Kingdom == Clan.PlayerClan?.Kingdom;

         string grantableFiefs = ResolveGrantableCrownFiefs();
         string revocableFiefs = ResolveRevocableFiefsOf(hero);
         bool npcCanBeGrantedFief = FiefTransferPolicy.CanOfferGrant(playerIsKingdomLeader, npcIsVassalInPlayerKingdom, !string.IsNullOrEmpty(grantableFiefs));
         bool npcCanHaveFiefRevoked = FiefTransferPolicy.CanOfferRevoke(playerIsKingdomLeader, npcIsVassalInPlayerKingdom, !string.IsNullOrEmpty(revocableFiefs));

         bool playerLeadsClan = Clan.PlayerClan?.Leader == Hero.MainHero;
         bool npcIsSpouse = hero != null && hero == Hero.MainHero?.Spouse;
         bool npcCanBeExpelledFromClan = ExpulsionPolicy.CanOfferExpel(playerLeadsClan, npcIsPlayerCompanion, npcIsSpouse, npcAlive, npcAvailableForErrand);

         // grant_stipend / swear_oath (2026-08-05, the two personal-command verbs that touch PERSISTED save state):
         // the 1:1 teaching gates, the same "prompt is advice, bridge is law" conditional-teaching contract as the
         // verbs above. Each pure gate mirrors the council's own offering eligibility (StipendPolicy.CanOfferStipend
         // reduces ChatViewModel.PlayerCanAffordStipend + the seated-companion fact; OathOfferPolicy.CanOfferOath
         // reduces ChatViewModel.AnySeatedMemberCanSwearOath), and each is re-checked at commit by
         // CourtActionResolver.TryGrantStipend / TrySwearOath against LIVE persisted state, so neither can ever
         // double-fund a stipend or double-record an oath already outstanding for this hero.
         bool npcHasActiveStipend = hero != null && CouncilStipendBehavior.Instance?.HasActive(hero.StringId) == true;
         bool playerCanAffordMinTranche = Hero.MainHero != null && Hero.MainHero.Gold >= StipendPolicy.MinTrancheCost;
         bool npcCanBeGrantedStipend = StipendPolicy.CanOfferStipend(npcIsPlayerCompanion, npcHasActiveStipend, playerCanAffordMinTranche);

         bool npcHasActiveCommitment = hero != null && CommitmentBehavior.Instance?.HasActive(hero.StringId) == true;
         // Plausibly qualifies for at least ONE whitelisted oath kind, mirroring AnySeatedMemberCanSwearOath's own
         // disjunction: holds gold (pay_gold), leads a party of their own (protect), or leads their own kingdom or
         // independent clan (keep_peace). WHICH kind actually fits is re-validated at the bridge.
         bool npcQualifiesForSomeOath = hero != null && (
            hero.Gold > 0
            || hero.PartyBelongedTo != null && hero.PartyBelongedTo.LeaderHero == hero
            || hero.Clan?.Kingdom != null && hero.Clan.Kingdom.Leader == hero
            || hero.Clan != null && hero.Clan.Kingdom == null && hero.Clan.Leader == hero);
         bool npcCanSwearOath = OathOfferPolicy.CanOfferOath(npcAlive, npcIsPrisoner, hero == Hero.MainHero, npcHasActiveCommitment, npcQualifiesForSomeOath);

         // mediate_peace (2026-08-06, third-party war mediation): the 1:1 teaching gate, the same "prompt is advice,
         // bridge is law" conditional-teaching contract as the verbs above. The pure PeaceMediationPolicy.CanMediate
         // is the ONE gate BannerlordGameStateBridge.ExecuteMediatePeace re-checks at commit; the list of enemy
         // realms this leader is at war with doubles as the target_faction grounding the prompt names verbatim. Only
         // a FACTION LEADER can be mediated with, and only when the player carries genuine noble standing (clan tier)
         // and the leader holds them in deep trust. Regard reads the mod's OWN personal ledger, falling back to the
         // vanilla relation when no profile exists yet for this leader.
         bool npcIsFactionLeader = hero?.IsFactionLeader == true;
         string mediatablePeaceFactions = npcIsFactionLeader ? ResolveMediatablePeaceFactions(hero) : null;
         int playerRegardWithLeader = profile?.ReputationWithPlayer ?? (hero != null && Hero.MainHero != null ? hero.GetRelation(Hero.MainHero) : 0);
         int playerStatureTier = Clan.PlayerClan?.Tier ?? 0;
         bool playerCanMediatePeace = PeaceMediationPolicy.CanMediate(
            npcIsFactionLeader,
            !string.IsNullOrEmpty(mediatablePeaceFactions),
            playerRegardWithLeader,
            playerStatureTier);

         // recruit_prisoner + its symmetric negative (empty-promise fix, 2026-08-14): both CanRecruitPrisoner
         // and CaptiveLordWithheldFromDefection below are derived from THIS SAME verdict, computed once, so the
         // two can never both be true. A held captive lord who is not (yet) eligible (regard too low, or their
         // own clan's leader) must still never be taught to CLAIM acceptance.
         // Custody is the executor's OWN broad test (party OR a player-clan settlement), not the party-only
         // NpcIsCaptive standing: a lord held in a player-clan dungeon was accepted by the executor yet never
         // taught, so the offer (and this negative) were silently withheld for him (2026-08-14 settlement gap).
         bool playerHoldsPrisoner = BannerlordGameStateBridge.IsHeldByPlayer(hero);
         bool canRecruitPrisoner = ResolveCanRecruitPrisoner(hero, profile, playerHoldsPrisoner);
         bool captiveLordWithheldFromDefection = playerHoldsPrisoner
            && hero != null && hero.IsLord
            && !canRecruitPrisoner;

         return new() {
            PlayerStatus = playerStatus,
            SharedRealmName = bridge.GetSharedRealmName(hero),
            WarStatus = bridge.GetDiplomaticStatus(hero),
            DaysSinceLastMeeting = daysSinceLastMeeting,
            MeetingGapNote = PresenceDescriber.ShowsElapsedGap(presence)
               ? MeetingGapDescriber.Describe(daysSinceLastMeeting, IsPlayerHousehold(hero, profile))
               : null,
            PresenceNote = PresenceDescriber.Describe(presence, presencePlace, atSea),
            Scene = bridge.GetCurrentScene(),
            ContextualNames = string.IsNullOrWhiteSpace(playerMessage)
               ? null
               : ContextualNameResolver.Resolve(hero, playerMessage, bridge),
            KnownWanderers = ResolveKnownWanderers(hero, playerMessage),
            NpcCurrentActivity = bridge.GetCurrentActivity(hero),
            CurrentLocationNote = BuildLocationNote(hero),
            CurrentLocationName = Hero.MainHero?.CurrentSettlement?.Name?.ToString() ?? "",
            GeographyNote = SettlementContextResolver.Resolve(hero, playerMessage),
            AtSea = atSea,
            // Perception layer (2026-07-21): how the player is dressed, so an NPC can notice it. The mod-side
            // reader classifies from the live equipment (CalradiaRemembers.Logic.PlayerDressPolicy); mapped
            // 1:1 onto the SDK's own PlayerDressState (that project cannot reference CalradiaRemembers.Logic).
            PlayerDress = MapDressState(PlayerDressResolver.Resolve()),
            OpeningDisposition = FirstImpressionResolver.Resolve(hero, profile, bridge),
            PlayerSpouseName = Hero.MainHero?.Spouse?.IsAlive == true
               ? Hero.MainHero.Spouse.Name?.ToString()
               : null,
            PlayerFatherName = FormatParent(Hero.MainHero?.Father),
            PlayerMotherName = FormatParent(Hero.MainHero?.Mother),
            PlayerClanTier = Hero.MainHero?.Clan?.Tier ?? 0,
            PlayerPartyTroopCount = ResolvePlayerPartyTroopCount(hero),
            PlayerArenaRank = ResolveArenaRank(),
            PlayerArenaWins = ResolveArenaWins(),
            Witnesses = witnesses,
            PrivacyRequested = privacyRequested,
            IsWitnessExchangeTurn = isWitnessExchangeTurn,
            IsConversationOpening = isConversationOpening,
            IsLastWitnessExchange = isLastWitnessExchange,
            // IsRoundTableTurn is the one council flag the chat prompt path still carries: it is armed ONLY by the
            // cr.council_test spike (RoundTableController.CouncilTurnsForced). The REAL council (2026-08-22,
            // Council Increment 4) runs entirely through its own one-call group-scene pipeline
            // (ChatViewModel.RunCouncilTurnAsync / CouncilPromptBuilder), so this path no longer knows it exists:
            // the narrator/addressed/offering flags it once set are gone.
            IsRoundTableTurn = isRoundTableTurn,
            // Post-duel: the conversation that resumes after a fought duel, coloured by who prevailed. Drives
            // AppendPostDuelContext so the register (a beaten NPC, or a victorious one) holds on every turn.
            IsPostDuel = isPostDuel,
            PostDuelPlayerWon = postDuelPlayerWon,
            PostDuelCourtedHeroName = postDuelCourtedHeroName,
            // This turn's pre-drawn verdict for a captive player's physical attempt: the mod decides, the model
            // narrates (an LLM judging the struggle it is also writing always sides with its own story).
            PlayerAttemptSucceeds = playerAttemptSucceeds,
            IsCollectiveCaptiveScene = isCollectiveCaptiveScene,
            IsCaptorScene = isCaptorScene,
            CompanionActingOnCaptive = companionActingOnCaptive,
            IsBroughtCaptiveTurn = isBroughtCaptiveTurn,
            CaptiveFearCoerced = ResolveCaptiveFearCoerced(hero, profile, playerStatus),
            CaptorAllowsPersuasionRelease = ResolveCaptorReleaseVerdict(hero, playerStatus).AllowsPersuasion,
            CaptorAllowsRansomRelease = ResolveCaptorReleaseVerdict(hero, playerStatus).AllowsRansom,
            // Captivity EXECUTION (2026-08-04, player request via the "AI Executes" mod): the mod-side half of
            // the gate PlayerExecutionPolicy.CanExecute re-checks in full at the bridge before anything real
            // happens. This only decides whether the LLM is even TAUGHT the execute_player action this turn.
            CaptorMayExecutePlayer = ResolveCaptorMayExecutePlayer(hero, playerStatus),
            NativeQuestNote = ResolveNativeQuestNote(hero),
            NemesisRecaptureNote = ResolveNemesisRecaptureNote(hero, playerStatus),
            CaptiveIntent = captiveIntent,
            CaptiveSceneAtSea = ResolveCaptiveSceneAtSea(hero, playerStatus),
            CaptiveSceneFocus = captiveSceneFocus,
            CaptiveSceneStyle = captiveSceneStyle,
            CaptorKnowsPlayerName = profile?.CaptorLearnedPlayerName ?? false,
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
            // A brigand by station: a faceless bandit, OR a named nemesis who still belongs to a bandit clan.
            // Drives the coarse-voice overlay so a nemesis does not read like a lord. He keeps his name and
            // memory (the "does not know you" perception stays on CaptorIsBandit above), only his VOICE coarsens.
            CaptorIsBrigand = captorIsBandit || IsBanditClanHero(hero),
            // A bandit captor whose profile ALREADY carries memories of the player (a recurring nemesis on a
            // re-capture, or later turns of a scene where they introduced themselves) knows who he holds:
            // the "brigands do not know your name" perception then yields to a greeting by name.
            CaptorKnowsPlayer = captorIsBandit && profile?.Events is {Count: > 0},
            // Appearance: the PLAYER described in the third person (Subject) so the NPC pictures them
            // consistently, and the SPEAKING NPC's own body in the second person (Self) so it does not invent
            // contradictory looks. Derived body facts + any player-authored prose, both may be empty (renders none).
            PlayerAppearance = ComposeAppearance(Hero.MainHero, AppearanceVoice.Subject),
            NpcSelfAppearance = ComposeAppearance(hero, AppearanceVoice.Self),
            // Equipment-awareness pillar (2026-08-29): a modder reported CR feeds NO equipment to the prompt at
            // all, so an NPC's reaction to fine or shabby gear is pure guesswork. Same Subject/Self split as
            // appearance just above: the PLAYER's notable gear read in the third person, the SPEAKING NPC's own
            // gear (a gift now worn) read in the second person so it can reference it in its own words.
            PlayerGear = ComposeGear(Hero.MainHero, AppearanceVoice.Subject),
            NpcSelfGear = ComposeGear(hero, AppearanceVoice.Self),
            PlayerLooksWealthy = captorIsBandit && PlayerLooksWealthy(),
            PlayerLooksImportant = captorIsBandit && PlayerLooksImportant(),
            CompanionAskingPrice = ResolveCompanionAskingPrice(hero),
            // Same conditional-teaching contract as CompanionAskingPrice just above: the challenge_duel verb is
            // taught exactly when DuelEligibilityResolver (the same gate the bridge re-checks at commit time)
            // rules the game can actually stage the fight this instant.
            DuelChallengeAvailable = duelChallengeAvailable,
            DuelVenueLabel = duelVenueLabel,
            // The grounded quest menu (viable-quest referential): the tasks this giver can TRULY hand out,
            // pre-validated against the live world so a spoken offer always registers. Skipped for a captor
            // (quest teaching is suppressed in captive scenes anyway, no point scanning).
            ViableQuestMenu = playerStatus == PlayerStatusVsNpc.Captive
               ? null
               : ViableQuestScanner.BuildMenu(hero, bridge?.GetCurrentGameDay() ?? 0),
            // No quest is taught or offered in NSFW mode: an intimate scene must never mint a quest from a vow
            // (player report 2026-08-23). Roleplay and Enhanced keep quests; captivity already suppresses them.
            SuppressQuests = LlmServices.ActiveModelMode == ManualModelMode.Nsfw,
            // COUNCIL_ACTIONS.md Partie 5 (the "Caladog" case): mirrors the MCM opt-in straight through, off by
            // default so the existing "never a fief or marriage as the price of a task" guardrail stays intact
            // for every player who never touches this menu.
            FiefAndMarriageQuestRewardsAllowed = ModSettings.AllowFiefAndMarriageQuestRewards,
            HeldLordPrisoners = ResolveHeldLordPrisoners(),
            NpcIsPrisonerOfAnother = npcIsPrisonerOfAnother,
            NpcCaptorName = npcIsPrisonerOfAnother
               ? hero.PartyBelongedToAsPrisoner?.MapFaction?.Name?.ToString()
               : null,
            MercenaryOfferKingdom = ResolveMercenaryOfferKingdom(hero),
            // Global fact, deliberately NOT scoped to this NPC's own kingdom (unlike MercenaryOfferKingdom
            // above): whoever the player is talking to, the King who hired them (or the player) may raise
            // ending a contract that is currently real. See EncounterContext.PlayerIsMercenary.
            PlayerIsMercenary = Clan.PlayerClan?.IsUnderMercenaryService ?? false,
            VassalOfferKingdom = ResolveVassalOfferKingdom(hero),
            // follow_me: taught only when EscortEligibilityPolicy.CanEscort says the game can truly honour it
            // right now (see the computation just above the object initializer). dismiss_escort is the mirror,
            // taught only while THIS lord's own party is genuinely escorting the player.
            NpcCanEscortPlayer = npcCanEscortPlayer,
            NpcIsEscortingPlayer = npcIsEscortingPlayer,
            // ride_with_me / part_ways: the idle-lord complement of follow_me, taught only when
            // RideWithMeEligibilityPolicy says the game can honour it right now AND RetainerBehavior.Enabled is on
            // (see the computation just above the object initializer). part_ways is the mirror, taught only while
            // THIS lord is genuinely riding in the player's party as a retainer.
            NpcCanRideWithPlayer = npcCanRideWithPlayer,
            NpcIsRidingWithPlayer = npcIsRidingWithPlayer,
            // appoint_governor: the 1:1 teaching gate + the vacant-fief grounding (see the computation just above
            // the initializer). PromptBuilder also suppresses this on a council/round-table turn, where the
            // [RESOLUTION] channel owns appoint_governor instead, and in a captive scene.
            NpcCanBeAppointedGovernor = npcCanBeAppointedGovernor,
            AppointableFiefs = appointableFiefs,
            // assign_party_role / rejoin_party / dispatch_mission: the 1:1 teaching gates (see the computation just
            // above the initializer). PromptBuilder also suppresses each on a council/round-table turn, where the
            // [RESOLUTION] channel owns these instead, and in a captive scene.
            NpcCanBeAssignedPartyRole = npcCanBeAssignedPartyRole,
            NpcCanRejoinParty = npcCanRejoinParty,
            NpcCanBeDispatchedOnMission = npcCanBeDispatchedOnMission,
            // grant_fief / revoke_fief / expel_from_clan: the 1:1 teaching gates + the fief-name grounding (see the
            // computation just above the initializer). PromptBuilder also suppresses each on a council/round-table
            // turn, where the [RESOLUTION] channel owns these instead, and in a captive scene.
            NpcCanBeGrantedFief = npcCanBeGrantedFief,
            GrantableFiefs = grantableFiefs,
            NpcCanHaveFiefRevoked = npcCanHaveFiefRevoked,
            RevocableFiefs = revocableFiefs,
            NpcCanBeExpelledFromClan = npcCanBeExpelledFromClan,
            // grant_stipend / swear_oath: the 1:1 teaching gates for the two persisted-behaviour verbs (see the
            // computation just above the initializer). PromptBuilder also suppresses each on a council/round-table
            // turn, where the [RESOLUTION] channel owns these instead, and in a captive scene.
            NpcCanBeGrantedStipend = npcCanBeGrantedStipend,
            NpcCanSwearOath = npcCanSwearOath,
            // mediate_peace: the 1:1 teaching gate + the enemy-realm grounding (see the computation just above the
            // initializer). PromptBuilder also suppresses this on a council/round-table turn and in a captive scene,
            // the same carve-out every sibling 1:1 offer applies.
            PlayerCanMediatePeace = playerCanMediatePeace,
            MediatablePeaceFactions = mediatablePeaceFactions,
            LordRecruitEligible = ResolveLordRecruitEligible(hero, profile),
            CanRecruitPrisoner = canRecruitPrisoner,
            // The negative teaching's gate: see the shared computation just above the initializer.
            CaptiveLordWithheldFromDefection = captiveLordWithheldFromDefection,
            CanRecruitNotable = ResolveCanRecruitNotable(hero, profile),
            // teach_skill: the conditional-teaching contract, same shape as CompanionAskingPrice/CanRecruitNotable
            // just above (see ResolveTeachableSkills below): taught exactly when the NPC genuinely has mastery
            // to teach, per CalradiaRemembers.Logic.TeachSkillPolicy.CanTeach.
            TeachableSkills = ResolveTeachableSkills(hero),
            // Council Tier 2: still-standing council-born accords between this NPC and the player (tribute, stipend,
            // pact, hostage), one first-person clause each, so the character can reference them in ordinary talk.
            StandingAccords = ResolveStandingAccords(hero),
            SchemeAgentTargetName = ResolveSchemeAgentTarget(hero),
            SchemeTargetsThisNpc = ResolveSchemeTargetsThisNpc(hero),
            // One-shot and consumed on read, so ONLY resolve it on a real LLM turn, never on the dead
            // open-time build in OnPlayerSelectedChat, which would swallow the directive before the lord ever
            // speaks (the reason the interception opener never actually reached the model).
            InterceptionReason = resolveInterception
               ? ResolveInterceptionReason(hero)
               : null,
            // C5 feedback loop: consumed on read like InterceptionReason, so only pull it on a real LLM turn,
            // never on the dead open-time build (which would swallow the nudge before the model ever sees it).
            LlmFormatFeedbackNote = resolveInterception
               ? LlmFeedbackNotes.ConsumeFor(hero?.StringId)
               : null,
            StanceNote = StanceService.Describe(profile),
            StanceConsequenceHint = ResolveStanceConsequenceHint(hero, profile),
            GrudgeNote = ResolveGrudgeNote(hero, profile),
            RegardShadowNote = ResolveRegardShadowNote(hero, profile),
            AppreciationNote = ResolveAppreciationNote(hero, profile),
            SpouseDivorceDemandNote = ResolveSpouseDivorceDemandNote(hero),
            PlayerEndOwnMarriageNote = ResolvePlayerEndOwnMarriageNote(hero),
            SpouseEstrangementNote = ResolveSpouseEstrangementNote(hero),
            NpcMarriageFrayingNote = ResolveNpcMarriageFrayingNote(hero),
            StationGuidelinesOverride = ResolveStationGuidelines(hero),
            LoveMatchEligible = ResolveLoveMatchEligible(hero, profile),
            MarriageBlockedBecause = ResolveMarriageBlockReason(hero, profile),
            LoveMatchReluctant = ResolveLoveMatchReluctant(hero, profile),
            LoveMatchBlessed = ResolveLoveMatchBlessed(hero),
            // "Marry your own companion": when the eligible partner is one of the player's OWN companions, the
            // marriage teaching adds a line so the model knows the wedding turns them from a hired retainer into
            // the player's spouse and a full member of the house (they must not narrate themselves still a mere
            // companion afterward). Only meaningful when LoveMatchEligible is true.
            PartnerIsOwnCompanion = MarryCompanionResolver.IsEligibleCompanionMatch(hero),
            NpcSpouseIsPlayer = hero?.Spouse == Hero.MainHero && Hero.MainHero != null,
            NpcIsPlayerHousehold = IsPlayerHousehold(hero, profile),
            MarriedSeductionStance = ResolveMarriedSeductionStance(hero, profile),
            SpouseMarriedUnwillingly = ResolveSpouseMarriedUnwillingly(hero),
            ConsortEligible = ResolveConsortEligible(hero, profile),
            SecretLoverEligible = ResolveSecretLoverEligible(hero, profile),
            OpenRelationshipEligible = ResolveOpenRelationshipEligible(hero, profile),
            PartnerLoverKnown = ResolvePartnerLoverKnown(profile),
            PartnerLoverName = ResolvePartnerLoverName(profile),
            PartnerLoverIsSecret = profile?.Romantic?.LoverIsSecret ?? false,
            ConceptionRisk = ResolveConceptionRisk(hero),
            PowerBalanceNote = ResolvePowerBalanceNote(hero, profile),
            MarriageProspects = MarriageProspectResolver.Build(hero),
            MatchmakerNpcKin = MatchmakerResolver.BuildNpcKin(hero),
            MatchmakerPlayerKin = MatchmakerResolver.BuildPlayerKin(),
            // The player's chosen writing voice, served from memory unless the pick or the file changed.
            NarrativeStyle = StyleService.CurrentRules(),
            WorldRumorsBlock = ResolveWorldRumors(hero),
            RealmNewsLine = ResolveRealmNews(hero),
            PlayerDeedsHeard = ResolvePlayerDeedsHeard(hero),
            QueryRelevantFactsBlock = ResolveQueryRelevantFacts(hero, playerMessage, (int) CampaignTime.Now.ToDays),
            CompanionOnErrand = ResolveCompanionOnErrand(hero),
            CompanionNewsReport = CompanionMissionBehavior.Instance?.ConsumeNewsReport(hero),
            CompanionMoodNote = ResolveCompanionMoodNote(hero),
            CompanionCampNote = ResolveCompanionCampNote(hero),
            IntimacyBargainEligible = IntimacyBargainEligibility.IsEligible(hero),
            IntimacyLeverageActive = IntimacyBargainEligibility.IsEligible(hero) && profile?.IntimacyLeverageHeld == true,
            BastardMotherTone = ResolveBastardMotherTone(hero),
            BastardBlackmailDemand = BastardBehavior.Instance?.BlackmailDemandOver(hero) ?? 0,
            // Fixes a player report: a pregnant NPC woman denied being with child in chat because her
            // pregnancy state was never injected into the prompt. See ResolvePregnancyNote.
            PregnancyNote = ResolvePregnancyNote(hero),
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

            // The PLAYER holds this settlement. Naming the owner by name below would tell the NPC the town
            // "belongs to <PlayerName>" without their ever connecting that name to the person in front of them,
            // so a notable or garrisoned lord in the player's OWN town asked "what brings you here?" as if to a
            // stranger (player report). Say plainly that their interlocutor is this place's lord.
            if (owner != null && owner == Hero.MainHero)
               return $"You are in {s.Name}, which the player before you holds: they are its lord, and you " +
                      "dwell here under their rule. Address them as the holder of this place, not a passing stranger.";

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

      /// <summary>
      ///   Composes a hero's appearance block from derived body facts plus any authored prose, in the given
      ///   voice. Returns null (renders nothing) for a null hero or when nothing stands out and no prose exists.
      /// </summary>
      private static string ComposeAppearance(Hero hero, AppearanceVoice voice)
      {
         if (hero == null) return null;

         string composed = AppearanceComposer.Compose(
            AppearanceFactsExtractor.Extract(hero),
            AppearanceProseBehavior.Instance?.Get(hero.StringId),
            voice);

         return string.IsNullOrWhiteSpace(composed)
            ? null
            : composed;
      }

      /// <summary>
      ///   Equipment-awareness pillar: composes a hero's notable-gear clause from their equipped items (both
      ///   loadouts, battle and civilian), in the given voice. Returns null (renders nothing) for a null hero
      ///   or when nothing about their gear stands out (an ordinary loadout is deliberately left unvoiced).
      /// </summary>
      private static string ComposeGear(Hero hero, AppearanceVoice voice)
      {
         if (hero == null) return null;

         string composed = EquipmentNotabilityPolicy.Compose(
            EquipmentFactsExtractor.Extract(hero), hero.IsFemale, voice);

         return string.IsNullOrWhiteSpace(composed)
            ? null
            : composed;
      }

      private static int ComputeDaysSinceLastMeeting(NpcProfile profile, IGameStateBridge bridge)
      {
         if (profile == null || profile.Events == null || profile.Events.Count == 0)
            return -1;

         int latestEventDay = profile.Events.Max(e => e.gameDay);
         int currentDay = bridge.GetCurrentGameDay();

         return Math.Max(0, currentDay - latestEventDay);
      }

      /// <summary>
      ///   True when this hero belongs to the player's own house: their spouse, a committed consort, or close
      ///   kin (a parent, child, or sibling). Such a bond apart is framed as a married life split between road
      ///   and hearth, never as a fading acquaintance the player has not seen in a while (the "wives feel left
      ///   behind" report). Everyone else keeps the ordinary elapsed-gap note.
      /// </summary>
      // Internal so FirstImpressionResolver can exempt the household from the stranger-framed opening line.
      internal static bool IsPlayerHousehold(Hero hero, NpcProfile profile)
      {
         if (hero == null || Hero.MainHero == null) return false;
         if (Hero.MainHero.Spouse == hero) return true;
         if (profile?.Romantic is {IsConsort: true}) return true;
         if (Hero.MainHero.Father == hero || Hero.MainHero.Mother == hero) return true;
         if (hero.Father == Hero.MainHero || hero.Mother == Hero.MainHero) return true;

         return Hero.MainHero.Siblings != null && Hero.MainHero.Siblings.Contains(hero);
      }

      /// <summary>
      ///   Stolen Love pillar (gated by the MCM toggle): for an NPC married to a THIRD PARTY, whether the player
      ///   has genuinely won them past their marriage and their own nature, via the pure
      ///   <see cref="SeductionEligibilityPolicy" />. Returns <see cref="MarriedSeductionStance.NotApplicable" />
      ///   when the pillar is off, when the NPC is unwed or wed to the player, so the ordinary infidelity rule is
      ///   left untouched unless the host opts in.
      /// </summary>
      private static MarriedSeductionStance ResolveMarriedSeductionStance(Hero hero, NpcProfile profile)
      {
         if (!ModSettings.EnableStolenLove) return MarriedSeductionStance.NotApplicable;
         if (hero?.Spouse is not {IsAlive: true}) return MarriedSeductionStance.NotApplicable;
         if (hero.Spouse == Hero.MainHero) return MarriedSeductionStance.NotApplicable; // married to the player, not a third party

         return SeductionEligibilityPolicy.Evaluate(SeductionFactsMapper.For(hero, profile)).Seducible
            ? MarriedSeductionStance.Seducible
            : MarriedSeductionStance.Resistant;
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

      /// <summary>Maps the mod's own pure <see cref="PlayerDressBand" /> onto the SDK's local <see cref="PlayerDressState" />.</summary>
      private static PlayerDressState MapDressState(PlayerDressBand band)
      {
         switch (band)
         {
            case PlayerDressBand.Naked: return PlayerDressState.Naked;
            case PlayerDressBand.Rags: return PlayerDressState.Rags;
            case PlayerDressBand.Armoured: return PlayerDressState.Armoured;
            default: return PlayerDressState.Civilian;
         }
      }

      /// <summary>The magnitude of the heaviest LIVE grudge this hero holds against the player, or 0 when none.</summary>
      private static int HeaviestGrudgeAgainstPlayer(Hero hero)
      {
         IReadOnlyList<Grudge> grudges = GrudgeBehavior.Instance?.GrudgesFor(hero?.StringId);

         if (grudges == null) return 0;

         string playerId = Hero.MainHero?.StringId;
         var max = 0;
         foreach (Grudge g in grudges)
            if (!g.IsResolved && g.TargetId == playerId && g.Magnitude > max)
               max = g.Magnitude;

         return max;
      }

      /// <summary>
      ///   True when the player carries real political weight (a clan of noble standing or
      ///   notable influence), so a bandit might suspect a valuable, ransomable figure — even
      ///   without knowing the name. Never exposes the actual identity.
      /// </summary>
      /// <summary>
      ///   True when <paramref name="hero" /> is a member of a bandit clan (a nemesis boss), so his captor voice should
      ///   coarsen to a brigand's.
      /// </summary>
      private static bool IsBanditClanHero(Hero hero)
      {
         try { return hero?.Clan?.IsBanditFaction == true; }
         catch { return false; }
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
      ///   For a pregnant female NPC, resolves her pregnancy state into the second-person prompt line via
      ///   <see cref="PregnancyNotePolicy" /> (fixes a player report: she otherwise has no idea she is
      ///   pregnant and denies it in chat, since this state was never injected into the prompt). The father
      ///   is namable ONLY when he is her LEGAL spouse: any other father (the player in an affair, another
      ///   lord, or the hidden "Unknown" bandit father) stays a secret, preserving the hidden-bastard
      ///   pillar. Empty string when she is not pregnant or nothing could be resolved.
      /// </summary>
      private static string ResolvePregnancyNote(Hero npcHero)
      {
         try
         {
            if (npcHero is not {IsFemale: true, IsPregnant: true}) return "";

            if (!PregnancyService.TryGetPregnancyInfo(npcHero, out Hero father, out int daysRemaining)) return "";

            bool fatherIsPlayer = father == Hero.MainHero;
            bool fatherNamable = father != null && father == npcHero.Spouse;
            string fatherName = fatherNamable
               ? father.Name?.ToString()
               : "";
            var totalDuration = (int) (Campaign.Current?.Models?.PregnancyModel?.PregnancyDurationInDays ?? 0f);

            return PregnancyNotePolicy.Describe(true, fatherIsPlayer, fatherName, fatherNamable, daysRemaining, totalDuration);
         }
         catch { return ""; }
      }

      /// <summary>
      ///   The player-clan towns and castles with no governor seated, as a ready "Name, Name" list for the
      ///   appoint_governor teaching (grounds the target_fief field on a real, currently-vacant holding). Null when
      ///   the player leads no such clan or every fief already has a governor. Mirrors
      ///   <see cref="Assemblies.ResolutionOfferingResolver" />'s own vacancy rule (a town or castle owned by the
      ///   player's clan with no governor), so the 1:1 offering and the council's own offering read the same
      ///   vacancy and can never disagree over what is open to grant.
      /// </summary>
      private static string ResolveAppointableGovernorFiefs()
      {
         Clan clan = Clan.PlayerClan;

         if (clan?.Settlements == null) return null;

         List<string> names = clan.Settlements
            .Where(s => (s.IsTown || s.IsCastle) && s.Town?.Governor == null)
            .Select(s => s.Name?.ToString())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

         return names.Count > 0
            ? string.Join(", ", names)
            : null;
      }

      /// <summary>
      ///   The player-clan towns and castles a sovereign could grant to a vassal, as a ready "Name, Name" list for
      ///   the grant_fief teaching (grounds the target_fief field on a real crown fief). Null when the player's clan
      ///   holds no such fief. Mirrors CourtActionResolver.TryGrantFief's own eligible-fief rule (a town or castle
      ///   owned by the player's clan), so the 1:1 offering and the commit read the same fiefs.
      /// </summary>
      private static string ResolveGrantableCrownFiefs()
      {
         Clan clan = Clan.PlayerClan;

         if (clan?.Settlements == null) return null;

         List<string> names = clan.Settlements
            .Where(s => s.IsTown || s.IsCastle)
            .Select(s => s.Name?.ToString())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

         return names.Count > 0
            ? string.Join(", ", names)
            : null;
      }

      /// <summary>
      ///   The conversation NPC's own clan's towns and castles a sovereign could revoke, as a ready "Name, Name" list
      ///   for the revoke_fief teaching (grounds the target_fief field on a real fief the vassal actually holds). Null
      ///   when the NPC has no separate clan or that clan holds no such fief. Mirrors CourtActionResolver.TryRevokeFief's
      ///   own eligible-fief rule (a town or castle owned by the spoliated vassal's clan), so the 1:1 offering and the
      ///   commit read the same fiefs.
      /// </summary>
      private static string ResolveRevocableFiefsOf(Hero hero)
      {
         Clan clan = hero?.Clan;

         if (clan == null || clan == Clan.PlayerClan || clan.Settlements == null) return null;

         List<string> names = clan.Settlements
            .Where(s => s.IsTown || s.IsCastle)
            .Select(s => s.Name?.ToString())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

         return names.Count > 0
            ? string.Join(", ", names)
            : null;
      }

      /// <summary>
      ///   The enemy realms this faction LEADER's own faction is currently at war with, as a ready "Name, Name" list
      ///   for the mediate_peace teaching (grounds the target_faction field on a real war). Null when the hero leads
      ///   no faction or holds no war. Resolves from the SAME pool CouncilLift.ResolveFactionByName draws from (live
      ///   Kingdoms and independent Clans), so the grounded list and the bridge's own name resolution can never
      ///   disagree over which realms count. Since a leader may hold several wars at once, the whole set is offered
      ///   so the model can name WHICH enemy realm the peace is brokered with.
      /// </summary>
      private static string ResolveMediatablePeaceFactions(Hero hero)
      {
         try
         {
            IFaction leaderFaction = hero?.MapFaction;

            if (leaderFaction == null) return null;

            var names = new List<string>();

            if (Kingdom.All != null)
               foreach (Kingdom k in Kingdom.All)
                  if (k != leaderFaction && FactionManager.IsAtWarAgainstFaction(leaderFaction, k))
                  {
                     string n = k.Name?.ToString();
                     if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
                  }

            if (Clan.All != null)
               foreach (Clan c in Clan.All)
                  if (c.Kingdom == null && c != leaderFaction && FactionManager.IsAtWarAgainstFaction(leaderFaction, c))
                  {
                     string n = c.Name?.ToString();
                     if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
                  }

            return names.Count > 0
               ? string.Join(", ", names)
               : null;
         }
         catch { return null; }
      }

      /// <summary>
      ///   Stance mechanical behaviour-gating (increment 1, Fear): true when the held captive's live
      ///   Fear/Respect and traits, run through the SAME <see cref="StanceGatePolicy.ResolveCoercion" />
      ///   the bridge consults before actually releasing them, resolve to
      ///   <see cref="CoercionOutcome.YieldsSofterTerms" /> — so the freedom-bargain prompt can ask for
      ///   notably harsher terms. Advice only: <see cref="EncounterContext.CaptiveFearCoerced" /> never
      ///   itself changes anything, it only colours what the LLM proposes; the bridge remains the law
      ///   that plants the hidden grudge on an actual yield. Mirrors the freedom bargain's own
      ///   NpcIsCaptive eligibility gate (<c>PromptBuilder.AppendPrisonerFreedomBargain</c>) so the note
      ///   never appears when the bargain itself is not taught. A null profile (no stance ever tracked)
      ///   leaves it false, same as the bridge's own untracked-captive default.
      /// </summary>
      private static bool ResolveCaptiveFearCoerced(Hero hero, NpcProfile profile, PlayerStatusVsNpc playerStatus)
      {
         try
         {
            if (playerStatus != PlayerStatusVsNpc.NpcIsCaptive || profile == null) return false;

            NpcStance stance = StanceService.ToStance(profile);
            int honor = TraitLevel(hero, DefaultTraits.Honor);
            int valor = TraitLevel(hero, DefaultTraits.Valor);
            int calculating = TraitLevel(hero, DefaultTraits.Calculating);

            CoercionResult coercion = StanceGatePolicy.ResolveCoercion(stance.Fear, stance.Respect, honor, valor, calculating);

            return coercion.Outcome == CoercionOutcome.YieldsSofterTerms;
         }
         catch { return false; }
      }

      /// <summary>
      ///   True when a captive scene is physically happening at sea. Player report: captured by Sea Raiders,
      ///   the captive scene was narrated as a land camp with a tent and a campfire because nothing told the
      ///   model the ship was still under them. <c>MobileParty.MainParty</c> (the general <see cref="AtSea" />
      ///   flag above) cannot answer this: the player's own party is gone once they are taken prisoner, so it
      ///   reads stale/false. The actual vessel in play is either the party now HOLDING the player captive
      ///   (<see cref="Hero.PartyBelongedToAsPrisoner" />) or the captor NPC's own live party — checked both
      ///   ways since either can be the ship, depending on how the capture was staged.
      /// </summary>
      private static bool ResolveCaptiveSceneAtSea(Hero hero, PlayerStatusVsNpc playerStatus)
      {
         try
         {
            if (playerStatus != PlayerStatusVsNpc.Captive) return false;

            bool holdingPartyAtSea = Hero.MainHero?.PartyBelongedToAsPrisoner?.MobileParty?.IsCurrentlyAtSea == true;
            bool captorPartyAtSea = hero?.PartyBelongedTo?.IsCurrentlyAtSea == true;

            return holdingPartyAtSea || captorPartyAtSea;
         }
         catch { return false; }
      }

      /// <summary>
      ///   Captivity release gate (advice side): when the PLAYER is this NPC's captive, resolves whether the
      ///   captor is open to freeing them, via the pure <see cref="CaptorReleasePolicy" /> reading the captor's
      ///   Mercy and the heaviest live grudge they hold against the player. The bridge re-checks the same gate
      ///   before it commits a real freeing (the prompt proposes, the bridge disposes). Refuses everything when
      ///   the player is not this NPC's captive.
      /// </summary>
      private static CaptorReleaseVerdict ResolveCaptorReleaseVerdict(Hero hero, PlayerStatusVsNpc playerStatus)
      {
         try
         {
            if (playerStatus != PlayerStatusVsNpc.Captive || hero == null)
               return new CaptorReleaseVerdict(false, false);

            int mercy = TraitLevel(hero, DefaultTraits.Mercy);
            int grudge = HeaviestGrudgeAgainstPlayer(hero);

            return CaptorReleasePolicy.Resolve(mercy, grudge, false);
         }
         catch { return new CaptorReleaseVerdict(false, false); }
      }

      /// <summary>
      ///   Captivity EXECUTION (2026-08-04, player request via the "AI Executes" mod, COUNCIL_ACTIONS.md 10.6):
      ///   whether the captor is even TAUGHT the execute_player action this turn. Every guard the pure
      ///   <see cref="PlayerExecutionPolicy" /> exposes, re-composed here from live engine facts: the MCM
      ///   opt-in (default OFF), the player genuinely captive to THIS hero right now, and the same Hardcore
      ///   content gate the rest of the captive-scene vocabulary already requires (matching PromptBuilder's
      ///   own <c>AdultLevel >= Hardcore</c> gate on <c>AppendCaptivePlayerRules</c>). This is advice only:
      ///   the bridge re-runs the SAME pure policy, from its own live facts, before it ever calls
      ///   KillCharacterAction, so a stale or premature "yes" here can never itself kill the player.
      /// </summary>
      private static bool ResolveCaptorMayExecutePlayer(Hero hero, PlayerStatusVsNpc playerStatus)
      {
         try
         {
            bool playerIsCaptiveOfThisHero = playerStatus == PlayerStatusVsNpc.Captive
               && Hero.MainHero?.PartyBelongedToAsPrisoner?.LeaderHero == hero;

            return PlayerExecutionPolicy.CanExecute(
               ModSettings.AllowCaptorExecutions,
               playerIsCaptiveOfThisHero,
               ModSettings.AdultContentLevel == AdultContentLevel.Hardcore,
               !CampaignOptions.IsLifeDeathCycleDisabled);
         }
         catch { return false; }
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
      /// <summary>
      ///   Open relationship (Inc 2): true only for a COMMITTED partner of the player (the player's own spouse,
      ///   or a consort/committed bond) who is not ALREADY on open terms. Gates the prompt's open_relationship
      ///   teaching so it is offered only where a real bond exists to open, never to a stranger or a mere
      ///   flirtation. The bridge re-validates the same guard at execution (the prompt is advice, the bridge is
      ///   law).
      /// </summary>
      private static bool ResolveOpenRelationshipEligible(Hero hero, NpcProfile profile)
      {
         if (hero == null || Hero.MainHero == null) return false;
         if (profile?.Romantic == null) return false;
         if (profile.Romantic.IsOpenArrangement) return false; // already open

         bool isSpouse = hero == Hero.MainHero.Spouse;
         bool isCommittedConsort = profile.Romantic.IsConsort || profile.Romantic.Status == RomanticStatus.Committed;

         return isSpouse || isCommittedConsort;
      }

      /// <summary>
      ///   Inc 3b-b: true when this partner has a lover the player already KNOWS about (TakenLoverId set AND
      ///   LoverKnownToPlayer). Gates the prompt's conversation-around-a-known-lover teaching (open mention vs
      ///   secret-affair confrontation, plus the end_affair / close_relationship vocabulary). Never before the
      ///   lover is surfaced to the player, so a still-secret affair never leaks through the prompt.
      /// </summary>
      private static bool ResolvePartnerLoverKnown(NpcProfile profile)
      {
         if (profile?.Romantic == null) return false;

         return !string.IsNullOrEmpty(profile.Romantic.TakenLoverId) && profile.Romantic.LoverKnownToPlayer;
      }

      /// <summary>
      ///   Inc 3b-b: the known lover named through <see cref="HeroLabel" /> (namesake-safe), so the partner can
      ///   speak of him by name. Null when the lover is not currently known to the player or cannot be resolved.
      /// </summary>
      private static string ResolvePartnerLoverName(NpcProfile profile)
      {
         if (!ResolvePartnerLoverKnown(profile)) return null;

         Hero lover = Hero.FindFirst(h => h.StringId == profile.Romantic.TakenLoverId);
         if (lover == null) return null;

         return HeroLabel.Describe(lover.Name?.ToString(), lover.Clan?.Name?.ToString(), lover.Culture?.Name?.ToString());
      }

      private static bool ResolveConsortEligible(Hero hero, NpcProfile profile)
      {
         if (hero == null || Hero.MainHero == null) return false;
         if (profile?.Romantic == null) return false;
         if (!profile.Romantic.IsCompatibleWith(Hero.MainHero.IsFemale)) return false;
         if (profile.Romantic.IsConsort) return false; // already a consort
         RomanticStatus status = profile.Romantic.Status;

         // M-R1: require an ACTIVE intimate bond, not a raw ordinal test. Estranged/Broken sort after Intimate in
         // the enum, so "< Intimate" wrongly let a dead romance qualify for a consort offer.
         if (!status.IsIntimateOrDeeper()) return false;
         if (status == RomanticStatus.Committed) return false;

         return profile.ReputationWithPlayer >= 60;
      }

      /// <summary>
      ///   The holder's dominant live grudge's TODAY's effective magnitude (0 when none), the exact number
      ///   <see cref="AppreciationPolicy.TarnishedMagnitude" /> needs to sour a standing appreciation.
      /// </summary>
      private static int ResolveDominantLiveGrudgeMagnitude(Hero hero)
      {
         var today = (int) CampaignTime.Now.ToDays;
         IReadOnlyList<Grudge> live = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);

         if (live == null || live.Count == 0) return 0;

         // M-A1 (regard audit): only a grudge held against the PLAYER may sour an appreciation TOWARD the player.
         // A freed captive's CaptorCruelty grudge is held against their captor, not their rescuer; without this
         // filter it tarnished the player's kindness to zero ("will remember your kindness" then lied).
         string playerId = Hero.MainHero?.StringId;
         TraitFacts traits = GrudgeBridge.BuildTraitFacts(hero);

         var max = 0;
         foreach (Grudge g in live)
         {
            if (g == null || g.TargetId != playerId) continue;
            int m = GrudgePolicy.EffectiveMagnitude(g, today, traits);
            if (m > max) max = m;
         }

         return max;
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
      ///   increment-1 verbs, their 04/07/2026 reciprocals give_troops/spend_influence, plus increment 2's
      ///   <c>pledge_against</c>). Each verb's own section (and its [ACTION] format) is included only when
      ///   currently eligible, so the LLM is never taught a verb the bridge would refuse. Null when none
      ///   apply right now, or while the player is this NPC's captive (none of these favours make sense from
      ///   a dungeon). The verbs are composed via <see cref="VerbRegistry" /> (<see cref="IConversationVerb" />);
      ///   pledge_against stays inline here (untouched by this spike) between the two ComposeTeachings calls,
      ///   preserving its original position.
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

         // give_troops + spend_influence (04/07/2026): the reciprocal direction, taught right after.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, ReciprocalCourtVerbs));

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

         // Prisoner trade: buy (the NPC pays for a captive the player holds) and sell (the player pays
         // for one the NPC's clan holds). Each verb gates itself on the live holdings.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, PrisonerTradeVerbs));

         // end_marriage (DIVORCE, Phase 1): the first verb designed for the contract from the start.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, DivorceVerbs));

         // make_amends + grudge_terms (Grudges pillar, resolution + discovery halves): taught last of all.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, GrudgeVerbs));

         // Extension Surface actions guidance (30/08/2026): an externally-registered action's Guidance
         // callback, run REGARDLESS of eligibility, so a currently-ineligible action can still tell the NPC
         // to decline in-character instead of roleplaying agreement to a deed that will then fail to execute.
         // In normal play nothing external is registered, so this appends nothing.
         sb.Append(VerbRegistry.Instance.ComposeGuidance(verbCtx));

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
            IReadOnlyList<Grudge> allLive = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);

            if (allLive == null || allLive.Count == 0) return null;

            // M-A1 (regard audit): only grudges the NPC holds against the PLAYER may colour how they treat the
            // player this turn. A grudge against a third party is their business with that person, not a lens on
            // the player, so it must neither be voiced here nor drive the tone ruling toward the player.
            string playerId = Hero.MainHero?.StringId;
            var live = new List<Grudge>();
            foreach (Grudge g in allLive)
               if (g != null && g.TargetId == playerId)
                  live.Add(g); // order preserved: allLive is already ranked, so live[0] is the dominant player-targeted grudge.

            if (live.Count == 0) return null;

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
      ///   Stance mechanical behaviour-gating (increment 3, Respect, ratified 2026-07-04; join_clan soupape
      ///   ratified 2026-07-05): additionally requires <see cref="StanceGatePolicy.CommandsRespectToJoinClan" />,
      ///   reading the lord's live Respect (<see cref="StanceService.ToStance" />) and relation
      ///   (<see cref="NpcProfile.ClanRelationWithPlayer" />) — a lord who holds the player in contempt will
      ///   not throw in their lot, however cordial the relation. The join_clan variant uses a HIGH relation
      ///   bypass (<see cref="StanceGatePolicy.RecruitRespectRelationBypass" />, 80) precisely because
      ///   <see cref="LordRecruitment.MinRelation" /> (60) already gates relation: an adored lord (80+) still
      ///   joins on relation alone, but a merely-eligible lord (60..79) must ALSO respect the player, so the
      ///   Respect floor genuinely binds. Skipped entirely when there is no stance profile (join_clan never
      ///   depended on one before this increment, so an untracked lord is not newly narrowed).
      /// </summary>
      private static bool ResolveLordRecruitEligible(Hero hero, NpcProfile profile)
      {
         try
         {
            LordRecruitmentFacts facts = LordRecruitmentFactsBuilder.Build(hero);

            if (facts == null || !LordRecruitment.IsEligible(facts)) return false;
            if (profile == null) return true; // no stance profile to gate on — inherit prior behaviour

            return StanceGatePolicy.CommandsRespectToJoinClan(StanceService.ToStance(profile).Respect, profile.ClanRelationWithPlayer ?? 0);
         }
         catch { return false; }
      }

      /// <summary>
      ///   v1.34.3 recruit_prisoner: true when this NPC is a hero the player currently holds prisoner, is not
      ///   their own clan's leader, and the player's personal regard has reached the strong floor needed to
      ///   persuade a captured enemy to switch sides. The teaching gate half of the shared
      ///   <see cref="PrisonerRecruitmentPolicy" /> decision the bridge re-runs before executing
      ///   (<see cref="GameState.BannerlordGameStateBridge.ExecuteRecruitPrisoner" />). Held-by-player is the
      ///   caller-supplied <paramref name="playerHoldsPrisoner" />, the executor's OWN broad custody test
      ///   (<see cref="GameState.BannerlordGameStateBridge.IsHeldByPlayer" />: the player's party OR a
      ///   player-clan settlement), NOT the party-only <see cref="PlayerStatusVsNpc.NpcIsCaptive" /> standing:
      ///   before this the offer was withheld for a lord held in a player-clan dungeon even though the executor
      ///   would have honoured him. Regard is the mod's own personal ledger, never the possibly-unsynced clan
      ///   relation. This result is also the source the caller derives <c>CaptiveLordWithheldFromDefection</c>
      ///   from (the "cannot yet defect" negative teaching, 2026-08-14): see the shared computation right above
      ///   the <c>Build</c> initializer.
      /// </summary>
      private static bool ResolveCanRecruitPrisoner(Hero hero, NpcProfile profile, bool playerHoldsPrisoner)
      {
         try
         {
            if (hero == null || !playerHoldsPrisoner) return false;

            bool notLeader = hero.Clan?.Leader != hero;

            return PrisonerRecruitmentPolicy.CanRecruit(true, !notLeader, profile?.ReputationWithPlayer ?? 0);
         }
         catch { return false; }
      }

      /// <summary>
      ///   recruit_notable: true when this NPC is a settlement notable the player can persuade to leave their
      ///   post and take service. The teaching-gate half of the shared <see cref="NotableRecruitmentPolicy" />
      ///   decision the bridge re-runs before executing
      ///   (<see cref="GameState.BannerlordGameStateBridge.ExecuteRecruitNotable" />). The successor fact is read
      ///   from the SAME authority the executor uses
      ///   (<see cref="GameState.BannerlordGameStateBridge.FindNotableSuccessor" />), so the offer never appears
      ///   unless a real replacement exists for the town. Regard is the mod's own personal ledger, never the
      ///   possibly-unsynced clan relation. Never taught to a lord, wanderer, prisoner or stranger.
      /// </summary>
      private static bool ResolveCanRecruitNotable(Hero hero, NpcProfile profile)
      {
         try
         {
            if (hero == null || !hero.IsNotable) return false;

            bool hasSuccessor = BannerlordGameStateBridge.FindNotableSuccessor(hero) != null;

            return NotableRecruitmentPolicy.CanRecruit(
               hero.IsNotable, hero.IsAlive, hero.IsPrisoner,
               hero.Clan == Clan.PlayerClan, profile?.ReputationWithPlayer ?? 0, hasSuccessor);
         }
         catch { return false; }
      }

      /// <summary>
      ///   teach_skill: the skills this NPC has genuine mastery in right now, one display name per skill whose
      ///   live value clears TeachSkillPolicy.CanTeach's floor. Null when the hero is missing/dead or knows
      ///   nothing worth teaching, so AppendTeachSkill skips the section entirely rather than teaching an empty
      ///   list. Scans every DefaultSkills entry via TaleWorlds.CampaignSystem.Extensions.Skills.All, the same
      ///   live source ExecuteTeachSkill re-validates the LLM's named skill against.
      /// </summary>
      private static IReadOnlyList<string> ResolveTeachableSkills(Hero hero)
      {
         try
         {
            if (hero == null || !hero.IsAlive) return null;

            var teachable = new List<string>();
            foreach (SkillObject skill in Skills.All)
            {
               if (skill == null) continue;
               if (TeachSkillPolicy.CanTeach(hero.GetSkillValue(skill)))
                  teachable.Add(skill.Name?.ToString() ?? skill.StringId);
            }

            return teachable.Count > 0 ? teachable : null;
         }
         catch { return null; }
      }

      /// <summary>
      ///   Council Tier 2: gathers this NPC's STILL-STANDING council-born accords with the player into one
      ///   first-person clause each (see CalradiaRemembers.Logic.Assemblies.CouncilAccordClause), by asking each
      ///   live accord behavior for a record touching this hero. Null when none stand, so AppendStandingAccords
      ///   skips the section. The wording and DIRECTION of each clause is the unit-tested pure describer's job;
      ///   this reader only resolves the live records and the names.
      /// </summary>
      private static IReadOnlyList<string> ResolveStandingAccords(Hero hero)
      {
         try
         {
            if (hero is not {IsAlive: true}) return null;

            Hero player = Hero.MainHero;
            if (player == null) return null;

            string playerName = player.Name?.ToString() ?? "";
            var clauses = new List<string>();

            CouncilTribute tribute = CouncilTributeBehavior.Instance?.Get(hero.StringId);
            if (tribute != null)
               clauses.Add(CouncilAccordClause.TributeOwedToPlayer(playerName, tribute.AmountPerDay, tribute.DaysRemaining, tribute.MissedDays));

            CouncilStipend stipend = CouncilStipendBehavior.Instance?.Get(hero.StringId);
            if (stipend != null)
               clauses.Add(CouncilAccordClause.StipendGrantedByPlayer(playerName, stipend.AmountPerDay, stipend.DaysRemaining));

            // The pact is faction-level: it appears only for a hero of the exact faction it was struck with (a pact
            // is never both sides the player's own faction, so this stays null for the player's own lords).
            CouncilPact pact = CouncilPactBehavior.Instance?.Get(player.MapFaction, hero.MapFaction);
            if (pact != null)
               clauses.Add(CouncilAccordClause.PactWithPlayer(playerName, pact.DaysRemaining));

            CouncilHostage held = CouncilHostageBehavior.Instance?.GetByHostage(hero.StringId);
            if (held != null)
               clauses.Add(CouncilAccordClause.HeldAsHostageByPlayer(playerName, held.DaysRemaining));

            CouncilHostage pledged = CouncilHostageBehavior.Instance?.GetByPledger(hero.StringId);
            if (pledged != null)
            {
               string hostageName = Hero.FindFirst(h => h.StringId == pledged.HostageId)?.Name?.ToString();
               clauses.Add(CouncilAccordClause.HostageGivenToPlayer(playerName, hostageName, pledged.DaysRemaining));
            }

            return clauses.Count > 0 ? clauses : null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"ResolveStandingAccords failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>True when A.2 already granted the family's blessing for this NPC.</summary>
      private static bool ResolveLoveMatchBlessed(Hero hero)
      {
         if (hero == null) return false;

         return MarriageApprovalBehavior.Instance?.IsApproved(hero.StringId) ?? false;
      }

      /// <summary>
      ///   True when the NPC themselves is a viable marriage candidate AND regard is deep enough to
      ///   make the prospect real — EITHER the mod's own personal-regard ledger OR the live vanilla
      ///   clan relation reaching <see cref="MarriageEligibility.MarriageEligibilityThreshold" />. The
      ///   vanilla relation is read LIVE via <see cref="Hero.GetRelation" /> (never a possibly-unsynced
      ///   profile field), so relation built the ordinary way (gifts, favors, quests) counts toward a
      ///   match, not only the mod's own hidden ledger. The prompt then teaches the <c>marry</c> action;
      ///   the bridge re-validates with the SAME threshold before executing
      ///   (<see cref="GameState.BannerlordGameStateBridge.ExecuteMarry" />). The live-state guards the
      ///   bridge enforces (<see cref="MarriageGuardPolicy.MarriageAllowed" />: no captive on either
      ///   side, no active war between the two) are checked HERE too, so the action is never taught in a
      ///   state where the bridge would refuse it — a taught-then-refused marriage left the NPC
      ///   believing the wedding had happened.
      /// </summary>
      private static bool ResolveLoveMatchEligible(Hero hero, NpcProfile profile)
         => ResolveMarriageBlockReason(hero, profile) == MarriageBlockReason.None;

      /// <summary>
      ///   The ONE ordered pass that decides both whether a marriage is on offer and, when it is not, WHICH
      ///   obstacle stands in the way. Eligibility is derived from it rather than computed beside it, so the
      ///   reason the NPC voices can never disagree with the gate the bridge enforces (player report,
      ///   2026-07-23: a player earned the family's blessing and was told nothing about the gate he had
      ///   actually missed). The order runs from the most absolute bar to the most temporary, so the reason
      ///   given is the truest one: a player already wed cannot marry ANYONE, whatever else is also true.
      ///   The war test comes from <see cref="BannerlordGameStateBridge.IsAtWarWithPlayer" />, the same helper
      ///   the bridge itself uses when it re-validates before sealing.
      /// </summary>
      internal static MarriageBlockReason ResolveMarriageBlockReason(Hero hero, NpcProfile profile)
      {
         if (hero == null || Hero.MainHero == null) return MarriageBlockReason.NotMarriageable;
         if (Hero.MainHero.Spouse != null) return MarriageBlockReason.PlayerAlreadyWed;

         // "Marry your own companion" carve-out (2026-08-11): the SameClan bar exists to forbid wedding blood
         // KIN, and a hired wanderer companion who happens to share the clan banner is not the case that custom
         // is about. So an ELIGIBLE non-kin companion (MarryCompanionResolver, the single authority the bridge's
         // marry re-check shares) does NOT emit SameClan and falls through to the ordinary gates below; a genuine
         // blood-kin clan member (a sibling, child, or parent in your house) still returns SameClan, so the one
         // line the bar truly holds is untouched. MarriageBlockOverridePolicy's SameClan=HARD split is left as-is:
         // this is a more specific path that stops the resolver EMITTING SameClan for a companion, not an override.
         if (hero.Clan == Clan.PlayerClan && !MarryCompanionResolver.IsEligibleCompanionMatch(hero))
            return MarriageBlockReason.SameClan;

         if (!MarriageProspectResolver.IsMarriageable(Hero.MainHero, hero)) return MarriageBlockReason.NotMarriageable;

         bool atWar = BannerlordGameStateBridge.IsAtWarWithPlayer(hero);

         // Captivity and war are one pure predicate (vanilla forbids both at its own marriage screen), so they
         // are asked together and then told apart only to name the obstacle.
         if (!MarriageGuardPolicy.MarriageAllowed(hero.IsPrisoner, Hero.MainHero.IsPrisoner, atWar))
            return atWar ? MarriageBlockReason.AtWar : MarriageBlockReason.Captive;

         if (RegardMeetsMarriageBar(hero, profile)) return MarriageBlockReason.None;

         // Ratified 2026-07-23: the family's blessing OPENS the match even below the regard bar (a political
         // union; the bride's unwon heart becomes a ForcedMarriage grudge at the wedding, planted by the
         // bridge). Only a match with neither her heart nor her family behind it stays barred.
         if (ResolveLoveMatchBlessed(hero)) return MarriageBlockReason.None;

         // Courtship trial (author's story): a prospect who rode at the player's side long enough to serve the
         // trial she set has reached out to accept, so the match opens even below the regard bar. This overrides
         // ONLY the soft RegardTooLow gate reached here; every HARD blocker was already returned above, so the
         // trial is beyond their reach by construction. MarriageBlockOverridePolicy states that soft/hard split
         // explicitly so it can never silently widen.
         // A courtship trial OR a Stolen Love divorce (they left their own spouse for the player) is the earned
         // path that opens the match below the regard bar: making them leave their marriage IS the courtship
         // (ratified decision A). Both override ONLY the soft RegardTooLow reached here; every HARD blocker was
         // already returned above.
         if (MarriageBlockOverridePolicy.CanOverride(MarriageBlockReason.RegardTooLow,
                ResolveCourtshipTrialPassed(hero) || ResolveStolenLoveDivorcee(hero)))
            return MarriageBlockReason.None;

         return MarriageBlockReason.RegardTooLow;
      }

      /// <summary>True when this NPC has SERVED a courtship trial (rode with the player long enough to accept the match).</summary>
      private static bool ResolveCourtshipTrialPassed(Hero hero)
      {
         if (hero == null) return false;

         return CourtshipTrialBehavior.Instance?.IsTrialPassed(hero.StringId) ?? false;
      }

      /// <summary>
      ///   True when this NPC left their own spouse for the player through the Stolen Love pillar and is now free
      ///   to wed: their estrangement concluded (<see cref="NpcMarriageEstrangementBehavior.HasConcludedFor" />)
      ///   AND they are actually single now. The singleness check matters because a divorce that could not
      ///   complete (a clan-leading leaving spouse) is also marked concluded, and we must never open a marriage to
      ///   a still-married NPC.
      /// </summary>
      private static bool ResolveStolenLoveDivorcee(Hero hero)
      {
         if (hero?.Spouse != null) return false;

         return NpcMarriageEstrangementBehavior.Instance?.HasConcludedFor(hero?.StringId) ?? false;
      }

      /// <summary>The two-sided regard read every marriage gate shares: the mod's personal ledger OR the live vanilla relation reaching the EFFECTIVE bar (MCM "Marriage Regard Needed", capped at the shipped 50).</summary>
      private static bool RegardMeetsMarriageBar(Hero hero, NpcProfile profile)
         => (profile?.ReputationWithPlayer ?? 0) >= ModSettings.MarriageRegardThreshold ||
            (Hero.MainHero?.GetRelation(hero) ?? 0) >= ModSettings.MarriageRegardThreshold;

      /// <summary>
      ///   True when marriage is on offer ONLY through the family's blessing: the match is otherwise clear but
      ///   the NPC's own regard is still below the bar. Tells the prompt this would be a POLITICAL union, so
      ///   the NPC may accept out of duty, coldly, and never has to pretend a love that is not there.
      ///   A match opened by a COURTSHIP TRIAL is deliberately NOT reluctant: the prospect set the trial and
      ///   passing it settled her heart, so she accepts of her own will, not as duty her house imposed.
      /// </summary>
      private static bool ResolveLoveMatchReluctant(Hero hero, NpcProfile profile)
         => ResolveMarriageBlockReason(hero, profile) == MarriageBlockReason.None
            && !RegardMeetsMarriageBar(hero, profile)
            && !ResolveCourtshipTrialPassed(hero)
            && !ResolveStolenLoveDivorcee(hero); // one who left their spouse for the player is willing, not dutiful

      /// <summary>
      ///   True when this NPC is the player's spouse AND still carries a live ForcedMarriage grudge over how
      ///   the union was sealed. Read LIVE from the grudge ledger on purpose, with no flag of its own: the
      ///   grudge decays and can be resolved, and the moment it dies the spouse's consent rules return to the
      ///   ordinary marital footing by themselves. That is the whole redemption arc, and a separate persisted
      ///   flag would have to be kept in sync with it by hand.
      /// </summary>
      private static bool ResolveSpouseMarriedUnwillingly(Hero hero)
      {
         if (hero == null || Hero.MainHero == null || hero.Spouse != Hero.MainHero) return false;

         var today = (int) CampaignTime.Now.ToDays;
         IReadOnlyList<Grudge> live = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);

         return live != null && live.Any(g =>
            g.Source == GrudgeSource.ForcedMarriage && g.TargetId == Hero.MainHero.StringId);
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

         // v1.34.2 governance hardening (finding 4): a realm the player is at war with does not take them on as
         // its hired swords. Withhold the offer so it is never taught; ExecuteJoinAsMercenary re-checks the same
         // war fact at commit through MercenaryEnlistmentPolicy.
         bool playerFreeOfKingdom = Clan.PlayerClan?.Kingdom == null;
         bool playerAtWar = Hero.MainHero?.MapFaction != null && FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, npcKingdom);

         return MercenaryEnlistmentPolicy.CanJoin(playerFreeOfKingdom, playerAtWar)
            ? npcKingdom.Name?.ToString()
            : null;
      }

      /// <summary>
      ///   Returns the NPC's kingdom name when the NPC truly RULES that kingdom (unlike
      ///   <see cref="ResolveMercenaryOfferKingdom" />, any lord may offer a mercenary contract, but only the
      ///   ruler may grant vassalage) AND the player's clan is not already a full vassal of a kingdom (see
      ///   <see cref="VassalageOfferPolicy.CanJoin" />, the same pure gate <c>ExecuteJoinAsVassal</c> re-checks
      ///   at commit time). Null otherwise, the vassalage-offer section is then not taught at all.
      /// </summary>
      private static string ResolveVassalOfferKingdom(Hero hero)
      {
         if (hero is not {IsLord: true}) return null;

         Kingdom npcKingdom = hero.Clan?.Kingdom;

         if (npcKingdom == null) return null;

         Clan playerClan = Clan.PlayerClan;
         bool playerAlreadyVassal = playerClan?.Kingdom != null && !playerClan.IsUnderMercenaryService;

         // v1.34.2 governance hardening (finding 4): the player cannot swear fealty to a kingdom they are actively
         // fighting. Withhold the offer; ExecuteJoinAsVassal re-checks the same war fact at commit.
         bool playerAtWar = Hero.MainHero?.MapFaction != null && FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, npcKingdom);

         return VassalageOfferPolicy.CanJoin(npcKingdom.Leader == hero, playerAlreadyVassal, playerAtWar)
            ? npcKingdom.Name?.ToString()
            : null;
      }

      /// <summary>
      ///   follow_me's own conditional-teaching gate: restricted to lords (a wanderer or bandit leading a
      ///   scratch party is not who "escort me for a while" is meant for), re-running the exact
      ///   <see cref="EscortEligibilityPolicy.CanEscort" /> guard <see cref="Escort.FollowMeBehavior.TryStart" />
      ///   re-checks at commit time.
      /// </summary>
      private static bool ResolveNpcCanEscortPlayer(Hero hero, bool alreadyEscorting)
      {
         if (hero is not {IsLord: true}) return false;

         MobileParty party = hero.PartyBelongedTo;
         bool leadsParty = party != null && party.LeaderHero == hero;
         bool atWar = hero.MapFaction != null && Hero.MainHero?.MapFaction != null &&
                      FactionManager.IsAtWarAgainstFaction(hero.MapFaction, Hero.MainHero.MapFaction);
         bool inArmy = party?.Army != null || party?.SiegeEvent != null;
         bool factionAtWar = FactionIsAtWarWithAnyone(hero.MapFaction);

         return EscortEligibilityPolicy.CanEscort(leadsParty, atWar, inArmy, alreadyEscorting, factionAtWar);
      }

      /// <summary>
      ///   True when <paramref name="faction" /> holds a live war against ANY other faction, not only the player's
      ///   own (the author's C-guard concern: a lord whose realm is at war must stay home for his liege's muster
      ///   call, not ride off with the player and quietly weaken his faction). Walks the SAME faction pool
      ///   <see cref="ResolveMediatablePeaceFactions" /> already draws from (live Kingdoms plus independent Clans,
      ///   a Clan holding a Kingdom being represented by that Kingdom already), so this answer can never disagree
      ///   with that list. Guarded so a missing engine snapshot never throws mid-turn.
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

      /// <summary>True while THIS lord's own party is genuinely escorting the player right now (drives dismiss_escort).</summary>
      private static bool ResolveNpcIsEscortingPlayer(Hero hero)
         => hero != null && FollowMeBehavior.Instance != null && FollowMeBehavior.Instance.IsEscorting(hero.StringId);

      /// <summary>
      ///   ride_with_me's conditional-teaching gate: the exact complement of
      ///   <see cref="ResolveNpcCanEscortPlayer" />, re-running the shared
      ///   <see cref="RideWithMeEligibilityPolicy.CanRideWithPlayer" /> guard the bridge re-checks at commit time.
      ///   Returns false unconditionally while the feature is disabled, so an other-clan lord riding in the
      ///   player's roster is never even taught until the author switches the path on with cr.retainer enable on.
      /// </summary>
      private static bool ResolveNpcCanRideWithPlayer(Hero hero, NpcProfile profile, bool alreadyRiding)
      {
         if (!RetainerBehavior.Enabled) return false;
         if (hero == null) return false;

         MobileParty party = hero.PartyBelongedTo;
         bool leadsParty = party != null && party.LeaderHero == hero;
         bool atWar = hero.MapFaction != null && Hero.MainHero?.MapFaction != null &&
                      FactionManager.IsAtWarAgainstFaction(hero.MapFaction, Hero.MainHero.MapFaction);

         return RideWithMeEligibilityPolicy.CanRideWithPlayer(
            isLord: hero.IsLord,
            isAlive: hero.IsAlive,
            isPrisoner: hero.IsPrisoner,
            leadsParty: leadsParty,
            isPlayerClan: hero.Clan == Clan.PlayerClan,
            atWar: atWar,
            alreadyRiding: alreadyRiding,
            regard: profile?.ReputationWithPlayer ?? 0,
            factionAtWar: FactionIsAtWarWithAnyone(hero.MapFaction));
      }

      /// <summary>
      ///   True while THIS lord is genuinely riding in the player's party as a retainer right now (drives
      ///   part_ways). Also gated behind the feature switch so the parting offer never appears while disabled.
      /// </summary>
      private static bool ResolveNpcIsRidingWithPlayer(Hero hero)
         => RetainerBehavior.Enabled && hero != null && RetainerBehavior.Instance != null
            && RetainerBehavior.Instance.IsRetainer(hero.StringId);

      /// <summary>
      ///   Builds a prompt note listing the native/vanilla quests THIS NPC gave the player that are still
      ///   ongoing, so the giver never acts baffled about a task they set outside CR dialogue (player report:
      ///   an NPC "confused" about a quest in the journal they themselves issued). Excludes CR's own quest
      ///   mirrors, already surfaced by their own systems. Null when there are none.
      /// </summary>
      private static string ResolveNativeQuestNote(Hero hero)
      {
         try
         {
            if (hero == null || Campaign.Current?.QuestManager == null) return null;

            var lines = new List<string>();
            foreach (QuestBase quest in Campaign.Current.QuestManager.Quests)
            {
               if (quest == null || !quest.IsOngoing || quest.QuestGiver != hero) continue;
               if (quest is CalradiaRemembersQuest) continue;
               if (quest is CommitmentQuest) continue;

               var title = quest.Title?.ToString();
               if (!string.IsNullOrWhiteSpace(title)) lines.Add("- " + title.Trim());
            }

            if (lines.Count == 0) return null;

            return "TASKS FROM THE REALM'S AFFAIRS YOU SET THIS PLAYER (the game's own quests, still open):\n" + string.Join("\n", lines) + "\nYou know you asked this of them; if they raise it, speak as the one who set it, never baffled.";
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MAPPING] ResolveNativeQuestNote failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Nemesis pillar: when the PLAYER is this captor's prisoner and the captor is a nemesis who has
      ///   recaptured them after a prior escape, a note has him acknowledge it in character and play colder.
      ///   Null when the captor is not a recaptured nemesis.
      /// </summary>
      private static string ResolveNemesisRecaptureNote(Hero hero, PlayerStatusVsNpc playerStatus)
      {
         try
         {
            if (playerStatus != PlayerStatusVsNpc.Captive || hero == null) return null;

            int recaptures = NemesisBehavior.Instance?.RecaptureCountFor(hero) ?? 0;

            if (recaptures <= 0) return null;

            return "THIS PRISONER HAS SLIPPED YOU BEFORE, AND NOW YOU HAVE THEM AGAIN: you are colder and more " + "careful this time. Make it plain, in your own words, that they will not escape you a second time.";
         }
         catch { return null; }
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
      ///   The player's own party troop count, fed to ANY interlocutor: every caller of <see cref="Build" />
      ///   is a physical, face-to-face conversation (chat view, dialog flow, round table), where the force
      ///   riding at the player's back is a VISIBLE fact, not a metagame number. The exact figure never
      ///   reaches the prompt — the SDK renders it as a coarse qualitative band — so a stranger or an
      ///   enemy may safely receive it. Excluded: a captive player (<c>Hero.MainHero.IsPrisoner</c> — the
      ///   party no longer follows them into their cell, and the "force at their back" would be a lie).
      ///   When the player LEADS an army, what stands behind them is the whole army, not their own party.
      /// </summary>
      private static int ResolvePlayerPartyTroopCount(Hero hero)
      {
         try
         {
            if (hero == null || hero == Hero.MainHero || !hero.IsAlive) return 0;

            MobileParty main = MobileParty.MainParty;
            if (main == null) return 0;

            bool leadsArmy = main.Army != null && main.Army.LeaderParty == main;

            return PlayerForcePolicy.Resolve(
               Hero.MainHero?.IsPrisoner == true,
               main.MemberRoster?.TotalManCount ?? 0,
               leadsArmy,
               leadsArmy ? main.Army.Parties.Sum(p => p?.MemberRoster?.TotalManCount ?? 0) : 0);
         }
         catch { return 0; }
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
      ///   True when this NPC is one of the player's OWN companions, bonded deeply enough through shared war
      ///   (regard ≥ 50, orientation-compatible, not already attached), that they could become the player's
      ///   discreet secret lover. Unlike the consort bond, it does NOT require a prior Intimate arc: the depth of
      ///   the comrade bond is what makes the confession plausible. The adult-content gate is applied in the
      ///   prompt (AppendSecretLoverProposal) and at the audience trigger, not here.
      /// </summary>
      private static bool ResolveSecretLoverEligible(Hero hero, NpcProfile profile)
      {
         if (ModSettings.AdultContentLevel == AdultContentLevel.Off) return false; // adult-content gate lives here
         if (hero == null || Hero.MainHero == null) return false;
         if (!hero.IsPlayerCompanion) return false; // brotherhood/sisterhood: the player's own companions only
         if (profile?.Romantic == null) return false;
         if (!profile.Romantic.IsCompatibleWith(Hero.MainHero.IsFemale)) return false;
         if (profile.Romantic.IsConsort || profile.Romantic.IsSecretLover) return false;
         if (hero == Hero.MainHero.Spouse) return false;

         RomanticStatus status = profile.Romantic.Status;

         if (status == RomanticStatus.Committed || status == RomanticStatus.Estranged || status == RomanticStatus.Broken) return false;

         return profile.ReputationWithPlayer >= 50;
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
      ///   Divorce escalation: set when THIS NPC is the player's own living spouse AND their estrangement
      ///   is currently under way (<see cref="SpouseDivorceDemandBehavior.IsEstranging" />). Composes the
      ///   full directive in the spouse's own voice, folding in the accept_divorce [ACTION] teaching
      ///   directly (see <see cref="EncounterContext.SpouseEstrangementNote" />'s own doc comment for why
      ///   this bypasses <see cref="VerbRegistry" />.ExtraActionTeachings). Deliberately does NOT teach
      ///   decline_divorce: an estrangement already under way is not something to refuse in words, only to
      ///   accept or to genuinely repair through deeds. Null otherwise.
      /// </summary>
      private static string ResolveSpouseEstrangementNote(Hero hero)
      {
         try
         {
            if (hero is not {IsAlive: true} || Hero.MainHero == null) return null;
            if (hero != Hero.MainHero.Spouse) return null;
            if (SpouseDivorceDemandBehavior.Instance?.IsEstranging(hero.StringId) != true) return null;

            var sb = new StringBuilder();
            sb.AppendLine("SPOUSE_DIVORCE_ESCALATION, YOU ARE ENDING THIS MARRIAGE YOURSELF:");
            sb.AppendLine("You pressed the player to end this marriage before, and were refused, more than once. You");
            sb.AppendLine("have resolved to wait no longer: you have begun ending it yourself, on your own timeline,");
            sb.AppendLine("whether or not the player consents. Speak as someone who has run out of patience but not");
            sb.AppendLine("out of feeling: weary, resolved, perhaps still grieving what the marriage once was.");
            sb.AppendLine("If the player plainly consents to end the marriage now, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: accept_divorce");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine("Only a genuinely warmer bond, proven over time by real deeds and not by words alone spoken");
            sb.AppendLine("in this conversation, would ever call this off. Do not treat a kind word or a single");
            sb.AppendLine("apology as enough; stay resolved unless you truly feel the change in how they treat you.");
            sb.AppendLine();

            return sb.ToString();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MAPPING] ResolveSpouseEstrangementNote failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Stolen Love pillar (Increment 2): when this NPC is married to a THIRD PARTY and their marriage is
      ///   fraying because of an affair with the player (<see cref="NpcMarriageEstrangementBehavior" /> reports the
      ///   <see cref="EstrangementPhase.Fraying" /> phase), composes a note in the NPC's own voice, the growing
      ///   rift between the spouse they have and the player they now want. Deliberately promises NO divorce and
      ///   teaches no verb: the outcome and its action belong to a later increment, so voicing one here would be an
      ///   empty promise. Null when the pillar is off, the NPC is unwed or wed to the player, or nothing is fraying.
      /// </summary>
      private static string ResolveNpcMarriageFrayingNote(Hero hero)
      {
         try
         {
            if (!ModSettings.EnableStolenLove) return null;
            if (hero is not {IsAlive: true} || Hero.MainHero == null) return null;
            if (hero.Spouse is not {IsAlive: true} || hero.Spouse == Hero.MainHero) return null;

            NpcMarriageEstrangementBehavior behavior = NpcMarriageEstrangementBehavior.Instance;
            if (behavior == null || behavior.HasConcludedFor(hero.StringId)) return null;

            string spouse = hero.Spouse.Name?.ToString() ?? "your spouse";
            EstrangementPhase phase = behavior.PhaseFor(hero.StringId);

            if (phase == EstrangementPhase.Fraying)
            {
               var sb = new StringBuilder();
               sb.AppendLine("YOUR MARRIAGE IS FRAYING BECAUSE OF THE PLAYER:");
               sb.AppendLine($"Your heart has turned toward the player, and the distance between you and {spouse} has");
               sb.AppendLine("grown into a rift you can no longer ignore. You are torn: the life and the vows you share");
               sb.AppendLine($"with {spouse} on one side, and what you now feel for the player on the other. Speak as");
               sb.AppendLine("someone restless and conflicted, drawn a little closer to the player each time you meet and");
               sb.AppendLine("quietly unhappy at home. You do NOT declare that you will leave your spouse, and you promise");
               sb.AppendLine("the player no future: nothing is decided, and you would not say such a thing aloud yet.");
               sb.AppendLine();

               return sb.ToString();
            }

            if (phase == EstrangementPhase.ReadyToDivorce)
            {
               // The player is here to hear it: mark the declaration so the month-long divorce clock starts now.
               behavior.MarkDeclared(hero.StringId, (int) CampaignTime.Now.ToDays);

               var sb = new StringBuilder();
               sb.AppendLine("YOU HAVE RESOLVED TO LEAVE YOUR SPOUSE FOR THE PLAYER:");
               sb.AppendLine($"The rift with {spouse} is beyond mending and your heart is set on the player. Tell them");
               sb.AppendLine($"plainly, in your own words, that you will leave {spouse} to be with them. This is a settled");
               sb.AppendLine("resolve, not a whim: you will set the parting in motion, and it will be done before long.");
               sb.AppendLine("Speak as someone who has decided, tender toward the player, grieved but certain about the");
               sb.AppendLine("marriage you are ending. Do not haggle or seek permission: you are telling them your choice.");
               sb.AppendLine();

               return sb.ToString();
            }

            return null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MAPPING] ResolveNpcMarriageFrayingNote failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
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

      /// <summary>
      ///   A SECOND, message-relevant world-fact block: known facts this NPC would plausibly know that bear
      ///   specifically on what the player just said, deterministic (no LLM call anywhere in this path).
      ///   Fixes a modder report: awareness alone (<see cref="ResolveWorldRumors" />'s small top-N by
      ///   magnitude/faction/distance/recency) starves a fact the NPC DOES know but that never ranked high
      ///   enough to make the ambient cut, so a pointed question about it got a hallucinated answer instead
      ///   of the real one. Order is mandatory and never reversed: the KNOWLEDGE gate
      ///   (<see cref="WorldEventStore.GetKnownEventsRated" />) runs FIRST over a generous candidate pool, THEN
      ///   <see cref="WorldFactRelevancePolicy" /> ranks only the survivors against the message, so a fact the
      ///   NPC would not plausibly know can never surface merely because the player asked about it. Best-effort
      ///   de-dup against the ambient <see cref="ResolveWorldRumors" /> top-6 selection and
      ///   <see cref="ResolveRealmNews" />'s own pick (both drawn from the identical rated query, so the same
      ///   ids line up) so a fact is never shown twice under two headers; player-deed events are excluded here
      ///   too, they are already covered, attributed, by <see cref="ResolvePlayerDeedsHeard" />. Null when
      ///   nothing relevant surfaces.
      /// </summary>
      internal static string ResolveQueryRelevantFacts(Hero hero, string playerMessage, int today)
      {
         if (hero == null || string.IsNullOrWhiteSpace(playerMessage)) return null;

         try
         {
            IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> known =
               WorldEventStore.Instance?.GetKnownEventsRated(hero, today, QueryRelevantCandidatePool);

            if (known == null || known.Count == 0) return null;

            HashSet<string> messageKeywords = WorldFactRelevancePolicy.Tokenize(playerMessage);
            HashSet<string> messageEntityIds = ExtractMessageEntityIds(messageKeywords);

            if (messageKeywords.Count == 0 && messageEntityIds.Count == 0) return null;

            // The ambient blocks (WorldRumorsBlock's top-6, RealmNewsLine's own pick) draw from this SAME
            // rated query, so their exact event ids line up here: excluding them is a straight id compare,
            // not a re-implementation of their selection rules.
            var ambientIds = new HashSet<string>(known.Take(6)
               .Where(t => !t.Event.InvolvesPlayer && t.Event.Type != WorldEventType.WarDeclared && t.Event.Type != WorldEventType.PeaceDeclared)
               .Select(t => t.Event.Id)
               .Where(id => id != null));

            foreach ((WorldEvent e, RumorConfidence _) in known)
               if (e.Type == WorldEventType.WarDeclared || e.Type == WorldEventType.PeaceDeclared)
               {
                  if (e.Id != null) ambientIds.Add(e.Id);
                  break; // ResolveRealmNews takes only the first realm-level match
               }

            var scored = new List<(WorldEvent Event, RumorConfidence Confidence, WorldFactRelevance Relevance)>();
            foreach ((WorldEvent e, RumorConfidence conf) in known)
            {
               if (e.InvolvesPlayer || string.IsNullOrWhiteSpace(e.Summary)) continue; // player deeds -> ResolvePlayerDeedsHeard
               if (e.Id != null && ambientIds.Contains(e.Id)) continue;

               var entityIds = new List<string>();
               if (e.ActorIds != null) entityIds.AddRange(e.ActorIds);
               if (e.FactionIds != null) entityIds.AddRange(e.FactionIds);

               WorldFactRelevance relevance = WorldFactRelevancePolicy.Evaluate(entityIds, e.Summary, messageEntityIds, messageKeywords);
               if (relevance.IsRelevant) scored.Add((e, conf, relevance));
            }

            if (scored.Count == 0) return null;

            List<string> lines = scored
               .OrderByDescending(s => s.Relevance.Score)
               .Take(QueryRelevantMaxLines)
               .Select(s =>
               {
                  string tag = s.Confidence == RumorConfidence.Distant ? "(a distant, unverified rumour) "
                     : s.Confidence == RumorConfidence.Secondhand ? "(heard secondhand) "
                     : "";
                  return $"- {tag}{s.Event.Summary.TrimEnd('.', ' ')}.";
               })
               .ToList();

            return lines.Count > 0
               ? string.Join("\n", lines)
               : null;
         }
         catch { return null; }
      }

      /// <summary>
      ///   The hero/faction StringIds named in the player's message, found the same way
      ///   <see cref="ContextualNameResolver" /> matches hero names against message tokens (diacritic-folded,
      ///   name split on space/hyphen, short/stop-word parts ignored), mirrored here rather than shared so
      ///   this project's message-signal extraction stays self-contained next to the policy it feeds.
      /// </summary>
      private static HashSet<string> ExtractMessageEntityIds(HashSet<string> messageKeywords)
      {
         var ids = new HashSet<string>();
         if (messageKeywords == null || messageKeywords.Count == 0) return ids;

         var folded = new HashSet<string>(messageKeywords.Select(NameNormalization.Fold));

         try
         {
            foreach (Hero h in Hero.AllAliveHeroes)
            {
               if (h == null || h.IsHumanPlayerCharacter) continue;
               string name = h.Name?.ToString();
               if (!string.IsNullOrEmpty(name) && NameSharesFoldedToken(name, folded)) ids.Add(h.StringId);
            }

            if (Kingdom.All != null)
               foreach (Kingdom k in Kingdom.All)
               {
                  string name = k?.Name?.ToString();
                  if (!string.IsNullOrEmpty(name) && NameSharesFoldedToken(name, folded)) ids.Add(k.StringId);
               }

            if (Clan.All != null)
               foreach (Clan c in Clan.All)
               {
                  string name = c?.Name?.ToString();
                  if (!string.IsNullOrEmpty(name) && NameSharesFoldedToken(name, folded)) ids.Add(c.StringId);
               }
         }
         catch { /* best effort: a partial id set still lets keyword overlap alone carry relevance */ }

         return ids;
      }

      private static bool NameSharesFoldedToken(string name, HashSet<string> foldedMessageTokens)
      {
         foreach (string part in name.Split(new[] {' ', '-'}, StringSplitOptions.RemoveEmptyEntries))
         {
            if (part.Length < WorldFactRelevancePolicy.MinKeywordLength) continue;
            if (foldedMessageTokens.Contains(NameNormalization.Fold(part))) return true;
         }

         return false;
      }

      private static int TraitLevel(Hero hero, TraitObject trait)
      {
         try { return hero?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
      }

      #endregion
   }
}