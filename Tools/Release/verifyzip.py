# -*- coding: utf-8 -*-
# Verifies a release archive by what is IN the dlls, not by their timestamps. The negative checks matter most: a
# stale dll would still carry the old member, and only its absence proves the new build shipped.
# Usage: python Tools/Release/verifyzip.py <version> [compare-version]
#   <version>          the archive to check, CalradiaRemembers-v<version>.zip at the repo root (makezip.py writes it)
#   [compare-version]  optional older archive to list added/removed entries against, when it is still on disk
# Each cut APPENDS its own markers to MUST / MUST_NOT / LITERALS below (a member or literal only the new build has).
# Exit code 0 when the verdict is "archive matches the source", 1 otherwise.
import zipfile, re, os, sys

if len(sys.argv) < 2:
    sys.exit("usage: verifyzip.py <version> [compare-version]")
VERSION = sys.argv[1].lstrip("v")
BASE = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
NEW = os.path.join(BASE, "CalradiaRemembers-v%s.zip" % VERSION)
OLD = os.path.join(BASE, "CalradiaRemembers-v%s.zip" % sys.argv[2].lstrip("v")) if len(sys.argv) > 2 else None
if not os.path.exists(NEW):
    sys.exit("no archive at %s (run makezip.py %s first)" % (NEW, VERSION))

# The comparison is a nicety: Gabriel deletes old archives once a version is published. Its absence never stops
# the verification that matters, which is what is INSIDE the new dll.
zn = zipfile.ZipFile(NEW)
zo = zipfile.ZipFile(OLD) if OLD and os.path.exists(OLD) else None
if zo:
    new, old = set(zn.namelist()), set(zo.namelist())
    print("=== entries added since %s ===" % sys.argv[2])
    for n in sorted(new - old):
        print("  +", n)
    print("=== entries gone since %s ===" % sys.argv[2])
    for n in sorted(old - new):
        print("  -", n)

print()
print("=== SubModule version in the archive ===")
sub = zn.read("CalradiaRemembers/SubModule.xml").decode("utf-8", "ignore")
m = re.search(r'Version value="([^"]+)"', sub)
found_version = m.group(1) if m else "NOT FOUND"
print("  ", found_version)

DLLS = {
    "mod":   "CalradiaRemembers/bin/Win64_Shipping_Client/CalradiaRemembers.dll",
    "logic": "CalradiaRemembers/bin/Win64_Shipping_Client/CalradiaRemembers.Logic.dll",
    "sdk":   "CalradiaRemembers/bin/Win64_Shipping_Client/NpcMemoryService.Core.dll",
}
blobs = {k: zn.read(v) for k, v in DLLS.items()}

# Managed metadata keeps type and member names as UTF-8 in the #Strings heap, so a plain byte search is a
# sound presence test for a name this distinctive.
LITERALS = [
    ("sdk",   "Never write \"the player\""),   # 2.7.0: the Lean reminder
    ("sdk",   "In the memory lines just below"),   # 2.7.0: whose "you"
    ("sdk",   "The player asks what it would take to make things right"),
    ("sdk",   "WHO IS WHO: you are "),
    ("sdk",   "MEETINGS YOU CANNOT ARRANGE"),
    ("logic", "Fought at "),
    ("sdk",   "WHO SOUGHT WHOM: you approached the player"),
    ("sdk",   "THE PLAYER holds these lord captives, not you:"),
    ("logic", "The player serves no realm at present, sworn to no liege"),
    ("mod",   "player_left_realm"),
    ("sdk",   "Allegiances in these memories are as they stood then"),
    ("sdk",   "write to what it says"),
    ("logic", "You have already heard their account of it: "),
    ("mod",   "HEAR_ACCOUNT, THE PLAYER ANSWERING YOUR GRIEVANCE:"),
    ("sdk",   "The player gives their account of, or justification for, a grievance"),
    ("logic", "=== FURTHER GUIDANCE ==="),
    ("logic", "!#"),
    ("mod",   "over to you in chains, as you asked"),
    ("sdk",   "which is buy_prisoner (they buy it from the player)"),
    ("sdk",   "and the player knows less of them than you do"),
    ("mod",   "there is nothing to learn from them there"),
    ("mod",   " is family; placing kin in another house"),
    ("logic", "!replace"),
    ("logic", "!remove "),
    ("mod",   "cr_patch"),
    ("sdk",   "WHERE THE PLAYER STANDS WITH YOU:"),
    ("sdk",   "JUST AGREED, in this very conversation"),
    ("sdk",   "spirit: friendly"),
    ("sdk",   "WHICH NAMES ARE CURRENT:"),
    ("logic", "It seems to be about "),
    ("mod",   "refused a settlement duel from outside the walls"),
    # 2.5.20, fkasad: the conversation you stepped out of comes back.
    ("mod",   "[CONV RESUME] kept"),
    ("logic", "s side when we broke the enemy together"),   # 2.7.10, tashmetu: worded "{who}'s side" since
    # 2.5.22, tashmetu: fed events get a place, cr.heard explains who heard what.
    ("mod",   "is not a settlement StringId, so the event was kept without a place."),
    ("logic", "This character could not hear it even on the day it happened."),
    ("logic", " never hear it"),
    ("mod",   "Actions panel: "),
    # 2.5.23: roles on fed events.
    ("mod",   "The player is among its ActorHeroIds"),
    # 2.5.24: the envoy, the exchange, renown logging, peace captures.
    ("mod",   "An envoy under a flag of truce"),
    ("mod",   "I come under a flag of truce, with a message from my lord."),
    ("mod",   "[RENOWN] took "),
    ("sdk",   "AN ENVOY SENT BY "),
    ("logic", "Envoy of "),
    # 2.5.25: the envoy after fkasad's first audiences.
    ("mod",   "=== ENVOY PROMPT START ==="),
    ("mod",   "CalradiaRemembers_prompt_envoy.txt"),
    ("mod",   " goes home free"),
    ("sdk",   "You make the first offer, "),
    ("sdk",   "NO TRADE OF CAPTIVES"),
    # 2.5.26: retirement in the heir note, a non-destructive succession test, the prisoner-trade self-tests.
    ("sdk",   "who has stepped down and still lives."),
    ("mod",   "reframe a DETACHED profile for an heir"),
    ("mod",   "prisoner_ransom_home"),
    ("mod",   "exchange_prisoners"),
    # 2.6.0: courier secrets, arrests, trials, outlaws, override templates.
    ("mod",   "CR_CourierSecrets_v1bytes"),
    ("mod",   "CR_ArrestCase_v1bytes"),
    ("mod",   "CR_MercenaryDeparture_v1bytes"),
    ("mod",   "Sergeants of "),
    ("mod",   "Bounty hunters of "),
    ("mod",   "world.txt in one-to-one prompts"),
    ("sdk",   "THIS SITTING IS A TRIAL"),
    ("sdk",   "who has stepped down and still lives."),
    # 2.6.2: transient parties restored after a save, the friendly rung, the council seat hook.
    ("mod",   "letters.courier"),
    ("mod",   "put back after the save."),
    ("mod",   "council_seat.txt"),
    ("sdk",   "friendly regard"),
    ("sdk",   "'s own view (not common knowledge; it shows only when the matter touches it): "),
    # 2.6.3: tashmetu's ransom.
    ("logic", " named: that is all "),
    ("logic", " has no coin to give, so none changed hands.)"),
    ("mod",   "the most you can pay is "),
    ("mod",   " dropped: a deal in the same response already moves the coin."),
    # 2.6.4: refusals are rules, not stories; tashmetu's culture blocks and !block.
    ("logic", "That reason is a rule of the game, not an event in the story"),
    ("mod",   "This game's settings do not allow killing"),
    ("mod",   "The game does not allow an execution here, so none took place."),
    # 2.6.5: tashmetu's envoy.
    ("sdk",   "That ceiling is for you alone: never name it"),
    ("sdk",   "accepts YOUR last offer: settle at that figure."),
    ("sdk",   "That gold evens out what the two captives are worth"),
    # 2.6.6: regard by letter, the game's companies, the fiefless vassal.
    ("sdk",   "[LETTER_TONE] warmer | same | cooler"),
    ("mod",   " judged the player's letter "),
    ("logic", " renounced fealty to "),
    ("logic", "'s service in the middle of the war and went over to its enemy"),
    # 2.6.6 re-cut: the work first planned as 2.6.7.
    ("sdk",   "You do not write this letter in your own hand"),
    ("logic", "the chancellery of "),
    ("mod",   " will speak with you once you make landfall."),
    ("logic", "None of the providers pinned in "),
    ("mod",   "[DUEL] the player takes up their battle gear for the town duel."),
    ("sdk",   "conversation"),
    # 2.6.7
    ("logic", "in the field, riding alone, with no one at their back"),
    ("logic", "in the field, at the head of their own company"),
    ("mod",   "[ACTION GATE] "),
    # 2.6.8
    ("sdk",   "YOUR DAY:"),
    ("sdk",   "HOW YOU SEE YOURSELF:"),
    ("logic", " high, like someone used to being deferred to"),
    ("mod",   "[SELF-ESTEEM] "),
    ("mod",   "[MOOD] "),
    # 2.6.9
    ("mod",   " witness(es) left the room; you are alone with them."),
    ("sdk",   "self_test"),
    # 2.6.10
    ("sdk",   "- arrange_match (target_hero: the one you would wed)"),
    ("sdk",   "- ransom_envoy (target_hero)"),
    ("mod",   "SHARE_INTEL, TELLING WHAT YOU KNOW OF YOUR TOWN:"),
    ("mod",   "ARRANGE_MATCH, GIVING A HAND IN MARRIAGE:"),
    # 2.6.11
    ("sdk",   "Your own capture runs the other way: "),
    ("sdk",   "What was asked and answered in this conversation stays settled"),
    ("logic", "We are no longer wed."),
    ("logic", "Their want of mercy is extreme"),
    ("logic", "We were rebels."),
    ("mod",   "[CHAT] Escape: "),
    ("mod",   "Also answering to that name (pass the id to pick one): "),
    ("mod",   "a ruler"),
]
MUST = [
    # 2.6.0: names and an MCM attribute live as UTF-8 metadata, not UTF-16 literals.
    ("mod",   b"Arrests and outlaws"),
    ("logic", b"HasNoSeatedActor"),
    ("logic", b"BrigandageRealms"),
    # 2.6.1: the mercenary grid, the typed ledgers, the neutral rung.
    ("logic", b"MercenaryDeparturePolicy"),
    ("logic", b"BetrayalUpgradeShift"),
    ("sdk",   b"NeutralTier"),
    ("sdk",   b"WorldCalendar"),
    ("sdk",   b"VanillaRelation"),
    ("logic", b"GoldTransferLimit"),
    ("logic", b"CanFeelTheDebt"),
    ("logic", b"MinPlotterRelation"),
    ("logic", b"DirectionFor"),
    ("logic", b"TradeDirection"),
    ("mod",   b"PlayerHoldsCaptiveNamed"),
    ("sdk",   b"ExternalPromptVariables"),
    ("mod",   b"ResolveExternalPromptVariables"),
    ("sdk",   b"AppendRoleAnchor"),
    ("sdk",   b"AppendNoAppointments"),
    ("sdk",   b"EventSubjectNormalizer"),
    ("logic", b"NameOrPlayer"),
    ("logic", b"CanBeARescue"),
    ("logic", b"ReleaseKind"),
    ("sdk",   b"NpcSoughtThePlayer"),
    ("sdk",   b"AppendWhoSoughtWhom"),
    ("sdk",   b"AppendHeldPrisonersBrief"),
    ("mod",   b"SoughtThePlayerThisConversation"),
    ("mod",   b"ForgetConversationSeeker"),
    ("mod",   b"TestApproachFraming"),
    ("logic", b"RulerMayWrite"),
    ("logic", b"FormerLiegeSilenceDays"),
    ("logic", b"PremiseStillHolds"),
    ("mod",   b"DaysSincePlayerLeft"),
    ("mod",   b"RecordParting"),
    ("mod",   b"_builtPlayerFaction"),
    ("mod",   b"TestRealmDeparture"),
    ("logic", b"AnsweredDay"),
    ("logic", b"PlayerAccount"),
    ("logic", b"StillDrivesConfrontation"),
    ("logic", b"GrudgeHardenedByForfeit"),
    ("logic", b"ApplyGrudgeEffect"),
    ("mod",   b"HearAccountVerb"),
    ("mod",   b"TestGrudgeAnswered"),
    ("logic", b"ContributionHeading"),
    # 2.6.11
    ("logic", b"ConsoleTextRepair"),
    ("logic", b"MemoryAccentRepair"),
    ("logic", b"ChatEscapePolicy"),
    ("logic", b"CaptiveNotoriety"),
    ("logic", b"ForeignDivorceDetection"),
    ("logic", b"RetiredArchetypes"),
    ("logic", b"PersonaTraitIntensity"),
    ("logic", b"DiagnosticSubjectPicker"),
    ("logic", b"DuelPublicityPolicy"),
    ("logic", b"RebellionWording"),
    ("logic", b"SpouseTenurePolicy"),
    ("mod",   b"CrConsoleTextPatch"),
    ("mod",   b"ForeignDivorceBehavior"),
    ("mod",   b"WeddingDayBehavior"),
    ("mod",   b"RepairMemoryAccents"),
    ("mod",   b"HandleEscape"),
    ("sdk",   b"CaptiveNotoriety"),
    ("sdk",   b"IsFemale"),
    ("logic", b"CommentPrefix"),
    ("mod",   b"GetModulesNames"),
    ("mod",   b"TestNarrativePatchScope"),
    ("logic", b"DeliveryQuestMatcher"),
    ("mod",   b"TestDeliverPrisonerBySale"),
    ("logic", b"CanTeachPlayer"),
    ("logic", b"HouseholdKinPolicy"),
    ("logic", b"CompanionIsYourFamily"),
    ("mod",   b"HousekeepingClient"),
    ("mod",   b"HousekeepingModel"),
    ("logic", b"Housekeeping"),
    ("logic", b"NarrativePatchComposer"),
    ("logic", b"TouchesTheSpeaker"),
    ("mod",   b"NarrativePatchScanner"),
    ("logic", b"CanHoldATownDuel"),
    ("logic", b"AudienceTopicLine"),
    ("logic", b"Friendly"),
    ("mod",   b"NpcsRaiseWorldNews"),
    ("mod",   b"RecentMatterTopic"),
    ("logic", b"StagingGracePolicy"),
    ("logic", b"CanHoldATownDuel"),
    ("sdk",   b"PlayerStandingNote"),
    ("sdk",   b"QuestsKnownThrough"),
    ("mod",   b"PlayerCanFightADuelHere"),
    ("mod",   b"RequestAudienceNow"),
    ("mod",   b"TestConversationCut"),
    ("mod",   b"RecordThatThePlayerWroteBack"),
    ("mod",   b"StandingContextFor"),
    ("mod",   b"Notice"),
    ("logic", b"RumorAwarenessBreakdown"),
    ("logic", b"LastAgeKnown"),
    ("logic", b"FedEventReach"),
    ("logic", b"ActionsPanelGate"),
    ("mod",   b"ExplainAll"),
    ("mod",   b"ResolveSettlement"),
    ("mod",   b"TestHeardDiagnostic"),
    ("mod",   b"ActionsPanelSwitchedOn"),
    ("mod",   b"NearestSettlementToPlayer"),
    ("logic", b"IsPlayersOwnDeed"),
    ("mod",   b"ActorHeroIds"),
    ("mod",   b"TestFeedPlayerNamed"),
    ("mod",   b"ExchangePrisonersVerb"),
    ("mod",   b"PrisonerEnvoyBehavior"),
    ("mod",   b"OpenForEnvoyAudienceDirect"),
    ("mod",   b"TestSuccessionSwitch"),
    ("mod",   b"RememberTheWedding"),
    ("mod",   b"MarryThroughCr"),
    ("logic", b"PrisonerExchangePolicy"),
    ("logic", b"EnvoyMandatePolicy"),
    ("logic", b"PrisonerBuyerPolicy"),
    ("logic", b"CaptureQuestPolicy"),
    ("logic", b"SuccessionTrigger"),
    ("logic", b"WeddingMemoryText"),
    ("logic", b"RealmServiceWording"),
    ("logic", b"MayBeKeptOnClose"),
    ("logic", b"FamilyApproves"),
    ("sdk",   b"EnvoyPromptBuilder"),
    ("sdk",   b"WantsCaptiveBack"),
    # 2.6.2
    ("mod",   b"TransientPartyRegistry"),
    ("mod",   b"RestoreCourier"),
    ("mod",   b"CouncilSeatNoteResolver"),
    ("logic", b"TransientPartyLedger"),
    ("sdk",   b"CouncilSeatNote"),
    ("sdk",   b"FriendlyTier"),
    ("sdk",   b"SouredTier"),
    # 2.6.3
    ("logic", b"MostABuyerCanPay"),
    ("logic", b"IsSamePaymentAsADealThisTurn"),
    ("logic", b"TurnPaymentPolicy"),
    ("logic", b"PriceDifferenceNote"),
    ("logic", b"RealmOfOwnPeople"),
    ("logic", b"BlockDirective"),
    ("logic", b"SpokenFigure"),
    ("logic", b"PlayerGivesAway"),
    ("logic", b"EnvoyTurnFacts"),
    ("logic", b"IsOfPlayersClan"),
    ("logic", b"IsTheWantedCaptive"),
    ("logic", b"IsAnotherSide"),
    # 2.6.6
    ("logic", b"LetterRegardPolicy"),
    ("logic", b"StripToneLine"),
    ("logic", b"CompanyDepartureIsNews"),
    ("logic", b"JudgesAVassalsDeparture"),
    ("logic", b"VassalSummary"),
    ("mod",   b"LetterRegardService"),
    ("mod",   b"OnCompanyChangedKingdom"),
    ("mod",   b"OnLeftAsVassal"),
    ("sdk",   b"LastLetterRegardGainDay"),
    # 2.6.6 re-cut
    ("logic", b"StagingHostPolicy"),
    ("logic", b"CorrespondenceRankPolicy"),
    ("logic", b"Stature"),
    ("logic", b"ProviderPinFailurePolicy"),
    ("logic", b"BalanceSurveyPolicy"),
    ("logic", b"SummarizeVram"),
    ("mod",   b"StatureReader"),
    ("mod",   b"CrRegardChange"),
    ("mod",   b"ForgetAwaitingLandfall"),
    ("mod",   b"BalanceSurveyBehavior"),
    ("mod",   b"ArmThePlayer"),
    ("sdk",   b"RegardChanged"),
    ("sdk",   b"WrittenBy"),
    # 2.6.7
    ("logic", b"NpcActionGate"),
    ("logic", b"ActorFacts"),
    ("logic", b"LetterWhereaboutsPolicy"),
    ("logic", b"MaySeekAudience"),
    ("mod",   b"ActionGate"),
    # 2.6.8
    ("logic", b"MatchUnspacedVocative"),
    ("logic", b"ClanStandingSeedPolicy"),
    ("logic", b"DayMoodFactsPolicy"),
    ("logic", b"SelfEsteemPolicy"),
    ("logic", b"PerceivedImagePolicy"),
    ("mod",   b"SteerTowardPlayer"),
    ("mod",   b"HeroPresence"),
    ("mod",   b"DayMoodReader"),
    ("mod",   b"ConversationMood"),
    ("mod",   b"SelfEsteemBehavior"),
    ("mod",   b"PerceivedImageService"),
    ("mod",   b"ClanStandingSeedService"),
    ("sdk",   b"MoodLine"),
    ("sdk",   b"SelfEsteemLine"),
    ("sdk",   b"ClanStandingSeedDay"),
    # 2.6.9
    ("logic", b"SceneDecisionMerge"),
    ("sdk",   b"MayOfferTasks"),
    ("sdk",   b"AsReported"),
    # 2.6.10
    ("logic", b"MatchmakerPolicy"),
    ("logic", b"RansomEnvoyPolicy"),
    ("logic", b"CraftWeaponPolicy"),
    ("logic", b"SwayGroundingPolicy"),
    ("logic", b"CourtMarkerText"),
    ("logic", b"TownFoodPolicy"),
    ("mod",   b"FoodTradeMemoryBehavior"),
    ("mod",   b"PlayerPrisonBreakPatch"),
    ("mod",   b"ArrangeMatchVerb"),
    ("mod",   b"SettleTroopHandOver"),
    ("sdk",   b"AdvanceGold"),
    # 2.7.0
    ("logic", b"AuthoredQuestEngine"),
    ("logic", b"RecentMatterWording"),
    ("logic", b"BackgroundCallTimeout"),
    ("logic", b"GameRelationReading"),
    ("logic", b"ChatSkinFallbackPolicy"),
    ("mod",   b"ModulePaths"),
    ("mod",   b"ExplorationBackdrop"),
    ("mod",   b"AuthoredQuestBehavior"),
    ("sdk",   b"OwnGameRelationWithPlayer"),
    ("sdk",   b"AuthoredQuestBlock"),
    # 2.7.1
    ("logic", b"SceneChoiceMenu"),
    ("logic", b"TaleGrudge"),
    ("logic", b"AuthoredQuestFolders"),
    ("mod",   b"PromptBlockForLocalsOf"),
    ("sdk",   b"StoryChoicesForInterpreter"),
    # 2.7.2
    ("logic", b"RelationSummaryText"),
    ("logic", b"AuthoredCulturePolicy"),
    ("logic", b"GrudgeBannerText"),
    ("sdk",   b"IsReasoningMandatoryRefusal"),
    ("sdk",   b"HistoryWindowPolicy"),
    # 2.7.3
    ("mod",   "scene_close_gives_the_map_back".encode("utf-16-le")),
    ("logic", b"AuthoredBandSizePolicy"),
    ("logic", b"SceneSurroundingsText"),
    # 2.7.4
    ("logic", b"AuthoredBandMakeupPolicy"),
    ("logic", b"TaleJournalPolicy"),
    ("logic", b"BirthHouseText"),
    ("logic", b"SceneCompanionMemory"),
    ("sdk",   b"InActiveWrittenTale"),
    # 2.7.5
    ("logic", b"MapEncounterClosePolicy"),
    ("logic", b"CaptureNorm"),
    ("logic", b"AudienceLocationPolicy"),
    ("logic", b"SquareReadinessFacts"),
    ("mod",   b"CrRomance"),
    ("mod",   b"CaptureMemoryBehavior"),
    ("sdk",   b"StageDirectionDedupe"),
    # 2.7.6
    ("logic", b"AuthoredTaleImages"),
    ("logic", b"LetterReplyReason"),
    ("logic", b"ChatTitleText"),
    ("mod",   b"IsSceneImageVisible"),
    # 2.7.7
    ("logic", b"TaleModuleFolder"),
    ("mod",   "tale_mod_folders".encode("utf-16-le")),
    # 2.7.8
    ("logic", b"TravellingAudiencePolicy"),
    ("logic", b"CourierHirePlacePolicy"),
    ("logic", b"AuthoredDefeat"),
    ("logic", b"LongStanding"),
    ("logic", b"BattleIsRaid"),
    ("mod",   "kin_audience".encode("utf-16-le")),
    ("mod",   "courier_menu_option".encode("utf-16-le")),
    ("mod",   b"TaleCompanyFightNoChiefPatch"),
    ("sdk",   b"AudienceFromKin"),
    ("sdk",   "accents too".encode("utf-16-le")),
    # 2.7.9
    ("logic", b"CrvidRules"),
    ("logic", b"VideoClock"),
    ("logic", b"ScenarioPass"),
    ("logic", b"ScenarioPackFolders"),
    ("logic", b"ScenarioRuleMerge"),
    ("logic", b"ScenarioReferenceMatch"),
    ("logic", b"AuthoredGiverPolicy"),
    ("logic", b"TaleConceptionPolicy"),
    ("logic", b"AuthoredContentPolicy"),
    ("logic", b"InjectedMemoryRemovalPolicy"),
    ("logic", b"SceneImageLayout"),
    ("logic", b"ConversationIsTheStep"),
    ("logic", b"GameBoundRoles"),
    ("mod",   b"CrVideoPlayer"),
    ("mod",   b"CrvidDecoder"),
    ("mod",   b"CrvidPoster"),
    ("mod",   b"LiveSprites"),
    ("mod",   b"ExecuteVideoLoop"),
    ("mod",   "scenario_all_play".encode("utf-16-le")),
    ("mod",   "video_decoder".encode("utf-16-le")),
    ("sdk",   b"PromptSectionSpec"),
    ("logic", b"EnvoyVisitFacts"),
    ("logic", b"TutorialUnderWay"),
    ("mod",   b"StoryModeTutorial"),
    ("mod",   "envoy_pacing".encode("utf-16-le")),
    ("mod",   "tutorial_gate".encode("utf-16-le")),
    ("logic", b"EscapeCloseLatch"),
    ("mod",   b"TickEscapeClose"),
    ("logic", b"PlayerCanBeApproached"),
    ("logic", b"PlayerInSiegeOrBattle"),
    # 2.7.10
    ("logic", b"ItemSalePolicy"),
    ("logic", b"TradeBatchPolicy"),
    ("logic", b"CaptiveOfferPolicy"),
    ("logic", b"AudienceWhoLine"),
    ("logic", b"EncyclopediaDetour"),
    ("logic", b"MapEventSighting"),
    ("logic", b"ReturnDecision"),
    ("mod",   b"AudienceInquiry"),
    ("mod",   b"LocalContextState"),
    ("mod",   b"ExecuteCounterOffer"),
    ("mod",   b"ExecuteSellItem"),
    ("mod",   "captive_counter_offer".encode("utf-16-le")),
    ("mod",   "local_context".encode("utf-16-le")),
    ("sdk",   b"ContextFitPolicy"),
    ("sdk",   "counter_offer".encode("utf-16-le")),
    ("sdk",   "sell_item".encode("utf-16-le")),
    # 2.7.12
    ("logic", b"WarDutyPolicy"),
    ("logic", b"WarDutyLadder"),
    ("logic", b"AimBook"),
    ("logic", b"IsRoadValid"),
    ("logic", b"GreetingDeferencePolicy"),
    ("logic", b"PendingMatchBook"),
    ("logic", b"SettlementAudienceSafetyPolicy"),
    ("logic", b"KinDeathDescriber"),
    ("logic", b"SkirmishMemoryText"),
    ("logic", b"BackgroundCallHoldPolicy"),
    ("logic", b"GoldTransferLimit"),
    ("mod",   b"CrMatches"),
    ("mod",   b"WarLevyDesk"),
    ("mod",   "war_duty_audience".encode("utf-16-le")),
    ("mod",   "registered_match".encode("utf-16-le")),
    ("mod",   "mercenary_join_term".encode("utf-16-le")),
    ("sdk",   "war_duty_terms".encode("utf-16-le")),
    # 2.7.13
    ("logic", b"PastOnMentionPolicy"),
    ("logic", b"LongTermHistoryPolicy"),
    ("logic", b"AuthoredHistoryLoader"),
    ("logic", b"HistoryRelationLedger"),
    ("logic", b"CompanionPatrolPolicy"),
    ("logic", b"PatrolLetterText"),
    ("logic", b"ChatTemperaturePolicy"),
    ("logic", b"MeetingLeaveFacts"),
    ("logic", b"GossipAlreadyRaisedByAnyone"),
    ("logic", b"BattleRecordText"),
    ("logic", b"PlayerNameText"),
    ("logic", b"CouncilQuestTypePolicy"),
    ("mod",   b"CompanionPatrolBehavior"),
    ("mod",   b"CompanionPatrolQuest"),
    ("mod",   b"AuthoredHistoryBehavior"),
    ("mod",   b"TellTheGameTheMeetingIsOver"),
    ("mod",   "history_on_mention".encode("utf-16-le")),
    ("mod",   "gossip_once_per_news".encode("utf-16-le")),
    ("mod",   "close_leaves_meeting".encode("utf-16-le")),
    ("sdk",   b"PastOnMentionBlock"),
    # 2.7.14
    ("logic", b"ReplyLanguagePolicy"),
    ("logic", b"CompanionRumourFreshnessPolicy"),
    ("mod",   b"FreshnessFacts"),
    ("mod",   "reply_language_everywhere".encode("utf-16-le")),
    ("mod",   "companion_news_fresh".encode("utf-16-le")),
    ("sdk",   "LANGUAGE OF THE SCENE:".encode("utf-16-le")),
    # 2.7.15
    ("logic", b"PairRegardPolicy"),
    ("logic", b"ClanRegardListPolicy"),
    ("logic", b"SharedPastPolicy"),
    ("mod",   b"PairRegardReader"),
    ("mod",   b"EncyclopediaHeroPageRegardMixin"),
    ("mod",   b"SharedPastBehavior"),
    ("mod",   b"OnPagePropertyChanged"),
    ("mod",   "npc_regard_reason".encode("utf-16-le")),
    ("mod",   "shared_past_draw".encode("utf-16-le")),
]
MUST_NOT = [
    ("mod",   "patrol_spike".encode("utf-16-le")),   # 2.7.13: the patrol engine spike's console command, removed before release
    ("mod", b"ActionInterpreterTimingSetting"),   # 2.7.12: the dead "Actions Interpreter Timing" dropdown is removed
    ("mod",   "A predator wearing a lord's title".encode("utf-16-le")),   # 2.6.11: the retired archetype is gone (the testall keeps the 'Sadistic' opening on purpose)
    ("logic", b"MinPlotterRegard"),   # renamed: it reads the ENGINE ledger, not the mod's
    ("logic", b"CompanionIsYourOwnBlood"),   # renamed; a zip built before the family fix still carries it
    ("mod", b"SettlementCanHostADuel"),   # renamed; a stale mod dll would still carry it
    ("mod", b"SettleGarrisonQuest"),   # 2.6.10: replaced by SettleTroopHandOver; a stale dll would still carry it
    ("mod", b"ConcernedPartyBonus"),   # moved from the store into RumorAwareness in 2.5.22
    ("mod", b"PlayerIsActor"),   # the unreleased first cut of 2.5.23, replaced by ActorHeroIds
    ("mod", b"RespawnInFlight"),   # 2.6.2: couriers restored through TransientPartyRegistry
    ("sdk", b"FondTier"),   # 2.6.2: renamed FriendlyTier before release
    ("logic", b"ShortfallNote"),   # 2.6.4: now PriceDifferenceNote
    ("logic", b"IsPaymentForATradeThisTurn"),   # 2.6.4: now TurnPaymentPolicy
    ("logic", b"NpcInitiativeGate"),   # 2.6.8: renamed NpcActionGate
    ("logic", b"InitiatorFacts"),   # 2.6.8: renamed ActorFacts
    ("mod", b"InitiativeGate"),   # 2.6.8: renamed ActionGate
    ("logic", "s side when they broke the enemy".encode("utf-16-le")),   # 2.7.10: the "you" wording tashmetu reported
    ("sdk", "In the lines below, ".encode("utf-16-le")),   # 2.7.0: the unbounded history header is gone
    ("mod", b"CrVideoSurface"),   # 2.7.9: the native video spike, removed before release
    ("mod", b"CrFramePumpSpike"),   # 2.7.9: the frame pump spike, removed before release
    ("mod", "video_spike".encode("utf-16-le")),   # 2.7.9: its console command
]

print()
print("=== present (new code shipped) ===")
ok = True
for who, needle in MUST:
    found = needle in blobs[who]
    ok &= found
    print("  %-5s %-40s %s" % (who, needle.decode()[:40], "YES" if found else "*** MISSING ***"))

print("=== absent (proves it is not a stale build) ===")
for who, needle in MUST_NOT:
    found = needle in blobs[who]
    ok &= not found
    print("  %-5s %-40s %s" % (who, needle.decode()[:40], "absent, good" if not found else "*** STILL THERE ***"))

print()
print("=== string literals (UTF-16, the 2.5.11 verifier lesson) ===")
for who, lit in LITERALS:
    found = lit.encode("utf-16-le") in blobs[who]
    ok &= found
    print("  %-5s %-45s %s" % (who, lit[:45], "YES" if found else "*** MISSING ***"))

print()
print("=== modder guides (promised by the Nexus page since 2.4; missing until 2.5.23) ===")
GUIDES = {"HOWTO_WorldEvents.md": "ActorHeroIds", "HOWTO_Actions.md": "IsFemale", "HOWTO_Voicing.md": "LineSpoken", "EXTENSION_API.md": "CrConversation", "HOWTO_Quests.md": "cr_quests", "TaleEditor.html": "From the werewolf", "ScenarioComposer.html": "IF_HERO_IS:lord_1_14", "HistoryEditor.html": "Campaign begins"}
names = set(zn.namelist())
for g, needle in GUIDES.items():
    path = "CalradiaRemembers/Docs/" + g
    found = path in names and needle in zn.read(path).decode("utf-8", "ignore")
    ok &= found
    print("  %-25s %s" % (g, "YES" if found else "*** MISSING ***"))

print()
print("=== 2.7.0 shipped data ===")
for path, needle in [("CalradiaRemembers/ModuleData/Quests/wolf_of_the_wood/wolf_of_the_wood.json", "cr_wolf_of_the_wood"),
                     ("CalradiaRemembers/GUI/Prefabs/CalradiaRemembersSceneBackdrop.xml", "CalradiaRemembersSceneBackdropWindow"),
                     ("CalradiaRemembers/ModuleData/behavior_guidelines_wanderer.txt", "between engagements"),
                     ("CalradiaRemembers/ModuleData/Quests/wolf_of_the_wood/wolf_of_the_wood.json", '"image"'),
                     ("CalradiaRemembers/GUI/Prefabs/CalradiaRemembersChat_Manuscript.xml", "SceneImageArea"),
                     ("CalradiaRemembers/ModuleData/Quests/wolf_of_the_wood/wolf_of_the_wood.json", '"on_defeat"'),
                     ("CalradiaRemembers/Docs/HOWTO_Quests.md", "on_defeat"),
                     # 2.7.9
                     ("CalradiaRemembers/Docs/HOWTO_Quests.md", ".crvid"),
                     ("CalradiaRemembers/Docs/HOWTO_Quests.md", "{player}"),
                     ("CalradiaRemembers/Docs/ScenarioComposer.html", "IF_KINGDOM_EXISTS"),
                     ("CalradiaRemembers/Docs/EXTENSION_API.md", "RemoveMemory"),
                     ("CalradiaRemembers/GUI/Prefabs/CalradiaRemembersChat_Manuscript.xml", "ExecuteVideoLoop"),
                     ("CalradiaRemembers/ModuleData/Quests/the_drowned_bridge/the_drowned_bridge.json", '"image"'),
                     ("CalradiaRemembers/ModuleData/scenarios/rhagaea_marriage.json", "rhagaea_marriage"),
                     # 2.7.15
                     ("CalradiaRemembers/GUI/Prefabs/CR_HeroRegardWhyItems.xml", "CalradiaRegardWhyItems"),
                     ("CalradiaRemembers/GUI/Prefabs/CR_HeroRegardHousesItems.xml", "CalradiaRegardHousesItems")]:
    found = path in names and needle in zn.read(path).decode("utf-8", "ignore")
    ok &= found
    print("  %-70s %s" % (path[18:], "YES" if found else "*** MISSING ***"))
wolf_pictures = [n for n in names if n.startswith("CalradiaRemembers/ModuleData/Quests/wolf_of_the_wood/") and n.endswith(".jpg")]
ok &= len(wolf_pictures) == 13
print("  wolf pictures:", len(wolf_pictures))
queens = [n for n in names if n.endswith("ModuleData/faces.json") or "/QueenPortraits/" in n or "/the_kings_hall/" in n]
ok &= not queens
print("  queens held back (2.7.10):", "yes" if not queens else "*** NO: %s ***" % queens[:3])
portraits = [n for n in names if "/ChatPortraits/" in n]
ok &= not portraits
print("  ChatPortraits excluded:", "yes" if not portraits else "*** NO ***")

print()
if found_version != "v" + VERSION:
    ok = False
    print("*** SubModule says %s, the archive is named v%s ***" % (found_version, VERSION))
print("VERDICT:", "archive matches the source" if ok else "*** DO NOT SHIP ***")
sys.exit(0 if ok else 1)
