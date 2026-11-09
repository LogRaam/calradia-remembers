// Code written by Gabriel Mailhot, 11/07/2026.

#region

using CalradiaRemembers.Behaviors;
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
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Companions;
using CalradiaRemembers.Logic.Verbs;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Bastards;
using CalradiaRemembers.Logic.Captivity;
using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Logic.Escort;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Items;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Logic.Providers;
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
using NpcMemoryService.Core.Knowledge;
using NpcMemoryService.Core.Extension;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TraitFacts = CalradiaRemembers.Logic.Grudges.TraitFacts;
using NpcMemoryService.Core.Standing;

#endregion

using NpcMemoryService.Core.Calendar;
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
      ///   defiance: one Calradian season (21 days; see ChronicleCalendar). Older losses fade from the power
      ///   calculus. The comment here said "a season's quarter" until 21/09/2026, which was simply wrong.
      /// </summary>
      private static int RecentDefeatWindowDays => WorldCalendar.DaysPerSeason;

      /// <summary>
      ///   Candidate pool size for the message-relevant world-fact scan (<see cref="ResolveQueryRelevantFacts" />):
      ///   generous on purpose, so a fact that is KNOWN but ranks below the small ambient top-N (the
      ///   "WHAT YOU'VE HEARD" cap) stays a candidate here instead of being invisible to a pointed question.
      /// </summary>
      private const int QueryRelevantCandidatePool = 50;

      /// <summary>Cap on how many message-relevant facts render in the new "WHAT YOU KNOW THAT TOUCHES ON THIS" block.</summary>
      private const int QueryRelevantMaxLines = 3;

      /// <summary>Registration order of the court verbs taught AFTER pledge_against, in <see cref="VerbRegistry" />.</summary>
      private static readonly string[] CourtVerbsAfterPledge = {"release_prisoner", "press_for_war", "enact_edict"};

      /// <summary>
      ///   The task and match verbs (reinforce_garrison, share_intel, deliver_recruits, arrange_match): registered since
      ///   2.6.10 to 2.7.19 but in no list until 22/10/2026, so their own teaching (the garrison's count, the match the
      ///   head of house is asked to give, tashmetu's groom-side ask of 20/10/2026) never reached a prompt; only their
      ///   catalogue line did. Each gates itself on a live task or match, so most characters are taught nothing here.
      /// </summary>
      private static readonly string[] TaskVerbs = {"reinforce_garrison", "share_intel", "deliver_recruits", "arrange_match"};

      /// <summary>Registration order of the court verbs taught BEFORE pledge_against, in <see cref="VerbRegistry" />.</summary>
      private static readonly string[] CourtVerbsBeforePledge = {"give_influence", "lend_troops", "sway_opinion"};

      /// <summary>DIVORCE, Phase 1: end_marriage is taught last, after every court verb above.</summary>
      private static readonly string[] DivorceVerbs = {"end_marriage"};

      /// <summary>A ruler's one road to her hand: the player leaves their house for hers (26/10/2026).</summary>
      private static readonly string[] HouseVerbs = {"leave_house_to_wed"};

      /// <summary>
      ///   Grudges pillar, resolution + discovery halves: make_amends then grudge_terms, taught last of all, after every
      ///   verb above.
      /// </summary>
      private static readonly string[] GrudgeVerbs = {"make_amends", "grudge_terms", "hear_account"};

      /// <summary>
      ///   admit_secret_lover: the player owning a whispered secret bond. Registered since 05/09/2026 and taught only
      ///   while there is a bond this NPC does not already know, but in no list until 22/10/2026, so its teaching never
      ///   reached a prompt and the model was left with the catalogue line alone.
      /// </summary>
      private static readonly string[] ConfessionVerbs = {"admit_secret_lover"};

      /// <summary>pay_bounty (froggyluv and Gabriel, 23/10/2026): a lord of a realm with a price on the player's head takes it off for gold.</summary>
      private static readonly string[] LawVerbs = {"pay_bounty"};

      /// <summary>extort (Gabriel, 23/10/2026): only to someone the player has a hold over that the game can verify; expose_secret: only while they hold an affair to tell.</summary>
      private static readonly string[] ExtortionVerbs = {"extort", "expose_secret"};

      /// <summary>reconcile_with (kurushimi00, 06/11/2026): only while someone present is someone this character holds a pardonable wrong against.</summary>
      private static readonly string[] PeaceVerbs = {"reconcile_with"};

      /// <summary>
      ///   CR's own verbs taught by another path, so they belong in no list here: pay_for_silence inline in the held-secret
      ///   section (its TeachingText is null), rob_npc, assault_npc and kill_npc by their catalogue line, in both compositions.
      /// </summary>
      internal static readonly string[] VerbsTaughtElsewhere = {"pay_for_silence", "rob_npc", "assault_npc", "kill_npc"};

      /// <summary>Every verb name a teaching list here composes, for the coverage guard (cr.test every_verb_taught).</summary>
      internal static IEnumerable<string> VerbsInTeachingLists
         => CourtVerbsBeforePledge.Concat(ReciprocalCourtVerbs).Concat(CourtVerbsAfterPledge).Concat(TaskVerbs).Concat(PrisonerTradeVerbs)
                                  .Concat(DivorceVerbs).Concat(HouseVerbs).Concat(GrudgeVerbs).Concat(ConfessionVerbs).Concat(LawVerbs).Concat(ExtortionVerbs).Concat(PeaceVerbs);

      // Prisoner trade (08/07/2026): the NPC may BUY a captive the player holds, or SELL one his clan holds.
      private static readonly string[] PrisonerTradeVerbs = {"buy_prisoner", "sell_prisoner", "exchange_prisoners"};

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
      // The blocks taught to whoever reads the turn's actions, the empty ones left out; null when none.
      private static string JoinReaderBlocks(params string[] blocks)
      {
         List<string> parts = blocks.Where(b => !string.IsNullOrWhiteSpace(b)).ToList();

         return parts.Count == 0 ? null : string.Join("\n\n", parts);
      }

      // The item the player picked from the bag, by id, or null.
      private static TaleWorlds.Core.ItemObject OfferedItem(string itemId)
      {
         if (string.IsNullOrWhiteSpace(itemId)) return null;
         try { return TaleWorlds.ObjectSystem.MBObjectManager.Instance?.GetObject<TaleWorlds.Core.ItemObject>(itemId); }
         catch
         {
            return null; // an id that no longer resolves: the character simply sees no offer
         }
      }

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
         // How many memories this character already held when THIS conversation opened. Everything past it was
         // recorded during the exchange the model is already reading verbatim, and rendering it again as
         // notable history is what made characters restate the same answer every turn (player, 17/09/2026).
         // Null means unset and keeps the old behaviour; ZERO is a real answer and cuts everything, since a
         // character the player has never met held no memories when the conversation opened.
         int? historyKnownThrough = null,
         int? questsKnownThrough = null,
         IReadOnlyCollection<string> memoriesDiscussedAtOpening = null,
         // True on the live chat, the one road that fills tokens in the raw reply (NpcChatService).
         bool offerMoneyTokens = false,
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
         bool postDuelWasBrawl = false,
         string postDuelCourtedHeroName = null,
         bool? playerAttemptSucceeds = null,
         // Companion-briefing pillar, revised per Gabriel's ruling (2026-09-07): true for every turn of the
         // PRIVATE briefing chat itself, so the prompt can tell the model what kind of scene this is (the
         // target is absent and cannot hear it) instead of letting a companion answer as though they were
         // standing right there. companionBriefingTargetName names that absent person for the framing.
         bool isCompanionBriefingScene = false,
         string companionBriefingTargetName = null,
         // 3chmidt (13/10/2026): the item the player held out with the bag this turn, so the character knows what it
         // is worth and what their own purse can pay before naming a price.
         string offeredItemId = null,
         // One path for actions, step 3 (Gabriel, 25/10/2026): true when this turn's tags are read from the character's
         // own reply (ActionReaderPolicy), so the prose prompt is taught the turn's own action blocks. False keeps what
         // every other path had before: those blocks taught to the interpreter only.
         bool proseReadsItsOwnActions = false)
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
            new GovernorAppointmentPolicy.AppointmentOfferFacts {
               PlayerLeadsClan = Clan.PlayerClan?.Leader == Hero.MainHero,
               AppointeeInPlayerClan = hero?.Clan != null && hero.Clan == Clan.PlayerClan,
               AppointeeAvailable = hero is {IsAlive: true} && !hero.IsPrisoner && hero != Hero.MainHero,
               HasVacantGovernableFief = !string.IsNullOrEmpty(appointableFiefs),
               // v1.34.2 governance hardening (finding 2): withhold the offer for an appointee already governing
               // another fief (one seat per governor) or away on an errand (cannot be seated in absentia). Both are
               // re-checked at commit by CourtActionResolver.TryAppointGovernor, the same prompt-is-advice contract.
               AppointeeNotGoverningElsewhere = hero?.GovernorOf == null,
               AppointeeNotAway = hero != null && CompanionMissionBehavior.Instance?.IsAway(hero.StringId) != true
            });

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

         bool npcCanBeAssignedPartyRole = PartyCommandOfferPolicy.CanOfferAssignRole(new PartyCommandOfferPolicy.AssignRoleFacts {NpcIsPlayerCompanion = npcIsPlayerCompanion, NpcInMainParty = npcInMainParty});
         bool npcCanRejoinParty = PartyCommandOfferPolicy.CanOfferRejoinParty(new PartyCommandOfferPolicy.RejoinPartyFacts {NpcIsPlayerCompanion = npcIsPlayerCompanion, NpcAlive = npcAlive, NpcIsPrisoner = npcIsPrisoner, NpcInMainParty = npcInMainParty, NpcLeadsOwnCompany = npcLeadsOwnCompany});
         bool npcCanBeDispatchedOnMission = PartyCommandOfferPolicy.CanOfferDispatchMission(new PartyCommandOfferPolicy.DispatchMissionFacts {NpcCanRideForPlayer = npcCanRideForPlayer, NpcInMainParty = npcInMainParty, NpcAvailableForErrand = npcAvailableForErrand});

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
         bool npcCanBeGrantedFief = FiefTransferPolicy.CanOfferGrant(new FiefTransferPolicy.GrantOfferFacts {PlayerIsKingdomLeader = playerIsKingdomLeader, NpcIsVassalInPlayerKingdom = npcIsVassalInPlayerKingdom, HasGrantableFief = !string.IsNullOrEmpty(grantableFiefs)});
         bool npcCanHaveFiefRevoked = FiefTransferPolicy.CanOfferRevoke(new FiefTransferPolicy.RevokeOfferFacts {PlayerIsKingdomLeader = playerIsKingdomLeader, NpcIsVassalInPlayerKingdom = npcIsVassalInPlayerKingdom, NpcHoldsRevocableFief = !string.IsNullOrEmpty(revocableFiefs)});

         bool playerLeadsClan = Clan.PlayerClan?.Leader == Hero.MainHero;
         bool npcIsSpouse = hero != null && hero == Hero.MainHero?.Spouse;
         bool npcCanBeExpelledFromClan = ExpulsionPolicy.CanOfferExpel(new ExpulsionPolicy.ExpulsionFacts {PlayerIsClanLeader = playerLeadsClan, TargetIsCompanion = npcIsPlayerCompanion, TargetIsSpouse = npcIsSpouse, TargetAlive = npcAlive, TargetAvailable = npcAvailableForErrand});

         // grant_stipend / swear_oath (2026-08-05, the two personal-command verbs that touch PERSISTED save state):
         // the 1:1 teaching gates, the same "prompt is advice, bridge is law" conditional-teaching contract as the
         // verbs above. Each pure gate mirrors the council's own offering eligibility (StipendPolicy.CanOfferStipend
         // reduces ChatViewModel.PlayerCanAffordStipend + the seated-companion fact; OathOfferPolicy.CanOfferOath
         // reduces ChatViewModel.AnySeatedMemberCanSwearOath), and each is re-checked at commit by
         // CourtActionResolver.TryGrantStipend / TrySwearOath against LIVE persisted state, so neither can ever
         // double-fund a stipend or double-record an oath already outstanding for this hero.
         bool npcHasActiveStipend = hero != null && CouncilStipendBehavior.Instance?.HasActive(hero.StringId) == true;
         bool playerCanAffordMinTranche = Hero.MainHero != null && Hero.MainHero.Gold >= StipendPolicy.MinTrancheCost;
         bool npcCanBeGrantedStipend = StipendPolicy.CanOfferStipend(new StipendPolicy.StipendOfferFacts {NpcIsPlayerCompanion = npcIsPlayerCompanion, NpcHasActiveStipend = npcHasActiveStipend, PlayerCanAffordMinTranche = playerCanAffordMinTranche});

         bool npcHasActiveCommitment = hero != null && CommitmentBehavior.Instance?.HasActive(hero.StringId) == true;
         // Plausibly qualifies for at least ONE whitelisted oath kind, mirroring AnySeatedMemberCanSwearOath's own
         // disjunction: holds gold (pay_gold), leads a party of their own (protect), or leads their own kingdom or
         // independent clan (keep_peace). WHICH kind actually fits is re-validated at the bridge.
         bool npcQualifiesForSomeOath = hero != null && (
            hero.Gold > 0
            || hero.PartyBelongedTo != null && hero.PartyBelongedTo.LeaderHero == hero
            || hero.Clan?.Kingdom != null && hero.Clan.Kingdom.Leader == hero
            || hero.Clan != null && hero.Clan.Kingdom == null && hero.Clan.Leader == hero);
         bool npcCanSwearOath = OathOfferPolicy.CanOfferOath(new OathOfferPolicy.OathOfferFacts {NpcAlive = npcAlive, NpcIsPrisoner = npcIsPrisoner, NpcIsPlayer = hero == Hero.MainHero, NpcHasActiveCommitment = npcHasActiveCommitment, NpcQualifiesForSomeKind = npcQualifiesForSomeOath});

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
         int playerStatureTier = StatureReader.Player.ClanTier;
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
         CaptiveOffer captiveOffer = ResolveCaptiveOffer(hero, profile, playerStatus, canRecruitPrisoner);
         bool captiveLordWithheldFromDefection = playerHoldsPrisoner
            && hero != null && hero.IsLord
            && !canRecruitPrisoner;

         // Built here rather than inside the initializer so the BEARER'S seat axis and the SEAT FACTS are the
         // same read. Two predicates that must agree are two predicates that will one day disagree, and the
         // whole pillar exists because "he would know this" and "somebody told him" drifted apart.
         SeatStandingFacts seatStanding = BuildSeatStanding(hero);
         MarketWordFacts marketWord = BuildMarketWord(hero);
         PartyPost partyPost = ReadPartyPost(hero);

         // ONE reader for the station facts, shared with the COUNCIL (StationContextFor). Two readers is how a
         // seated governor ends up knowing less than the same man in a private word.
         EncounterContext station = StationContextFor(hero);
         EncounterContext knowledge = KnowledgeContextFor(hero, profile);
         OwnVenturesFacts ventures = BuildOwnVentures(hero);

         // Read once: the prompt's own level and the level third-party variables are told must be the same.
         LeanPromptLevel leanLevel = ModSettings.LeanPrompt ? LeanPromptLevel.Lean : LeanPromptLevel.Full;

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
            ForcesInSight = ForcesInSightReader.Read(hero),
            TournamentsHeld = TournamentsHeardReader.Read(hero),
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
            PlayerClanTier = StatureReader.Player.ClanTier,
            PlayerPartyTroopCount = ResolvePlayerPartyTroopCount(hero),
            NpcPartyTroopCount = ResolveNpcPartyTroopCount(hero),
            PlayerArenaRank = ResolveArenaRank(),
            PlayerArenaWins = ResolveArenaWins(),
            Witnesses = witnesses,
            PrivacyRequested = privacyRequested,
            IsWitnessExchangeTurn = isWitnessExchangeTurn,
            IsConversationOpening = isConversationOpening,
            HistoryKnownThrough = historyKnownThrough,
            HistoryMemoryCap = ModSettings.PromptMemoryLimit > 0 ? ModSettings.PromptMemoryLimit : null,
            MemoriesDiscussedAtOpening = memoriesDiscussedAtOpening,
            QuestsKnownThrough = questsKnownThrough,
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
            PostDuelWasBrawl = postDuelWasBrawl,
            PostDuelCourtedHeroName = postDuelCourtedHeroName,
            // This turn's pre-drawn verdict for a captive player's physical attempt: the mod decides, the model
            // narrates (an LLM judging the struggle it is also writing always sides with its own story).
            PlayerAttemptSucceeds = playerAttemptSucceeds,
            IsCollectiveCaptiveScene = isCollectiveCaptiveScene,
            IsCaptorScene = isCaptorScene,
            CompanionActingOnCaptive = companionActingOnCaptive,
            IsBroughtCaptiveTurn = isBroughtCaptiveTurn,
            CaptiveFearCoerced = ResolveCaptiveFearCoerced(hero, profile, playerStatus),
            CaptiveTerms = captiveOffer?.Terms.Select(term => term.ToString().ToLowerInvariant()).ToList() ?? new List<string>(),
            CaptiveRansomCeiling = captiveOffer?.RansomCeiling ?? 0,
            CaptiveFealtyRealm = captiveOffer != null && captiveOffer.Terms.Contains(CaptiveTerm.Fealty) ? Hero.MainHero?.Clan?.Kingdom?.Name?.ToString() : null,
            CaptiveDisposition = captiveOffer == null ? null
               : captiveOffer.Disposition == CaptiveDisposition.OffersFirst ? "offers_first"
               : captiveOffer.Disposition == CaptiveDisposition.StandsOnPride ? "stands_on_pride" : "bargains",
            CaptorAllowsPersuasionRelease = ResolveCaptorReleaseVerdict(hero, playerStatus).AllowsPersuasion,
            CaptorAllowsRansomRelease = ResolveCaptorReleaseVerdict(hero, playerStatus).AllowsRansom,
            // Captivity EXECUTION (2026-08-04, player request via the "AI Executes" mod): the mod-side half of
            // the gate PlayerExecutionPolicy.CanExecute re-checks in full at the bridge before anything real
            // happens. This only decides whether the LLM is even TAUGHT the execute_player action this turn.
            CaptorMayExecutePlayer = ResolveCaptorMayExecutePlayer(hero, playerStatus),
            NativeQuestNote = ResolveNativeQuestNote(hero),
            NemesisRecaptureNote = ResolveNemesisRecaptureNote(hero, playerStatus),
            CaptiveIntent = captiveIntent,
            CaptorRansomPrice = ResolveCaptorOffers(hero, playerStatus, captiveIntent) ? Quests.CaptorTermsBehavior.RansomThePlayerCanPay() : 0,
            CaptorParoleDays = ResolveCaptorOffers(hero, playerStatus, captiveIntent) && CaptorAudiencePolicy.OffersParole(captiveIntent) ? CaptorAudiencePolicy.ParoleDays : 0,
            CaptorParoleRealm = hero?.MapFaction?.Name?.ToString(),
            SparesPlayerVillages = RaidRestraintBehavior.SparesThePlayer(hero),
            LegendKnown = LegendBehavior.LineFor(hero),
            CaptorExchangeCaptive = ResolveCaptorOffers(hero, playerStatus, captiveIntent) ? Labelled(Quests.CaptorTermsBehavior.ExchangeableCaptive(hero)) : null,
            CaptiveSceneAtSea = ResolveCaptiveSceneAtSea(hero, playerStatus),
            CaptiveSceneFocus = captiveSceneFocus,
            CaptiveSceneStyle = captiveSceneStyle,
            CaptorKnowsPlayerName = profile?.CaptorLearnedPlayerName ?? false,
            CaptiveVictimName = captiveVictimName,
            PlayerCoercedAgainstCompanion = playerCoercedAgainstCompanion,
            CaptiveVictimIsFemale = captiveVictimIsFemale,
            CompanionAudience = companionAudience,
            AudienceRetirementIsLanded = audienceRetirementIsLanded,
            // HakiTakiUmba (09/10/2026): the player's family asks for a word too, and speaks as kin.
            AudienceFromKin = companionAudience != CompanionAudienceReason.None
                              && TravellingAudiencePolicy.IsKin(Missions.CompanionAudienceTopicResolver.TravellerFactsOf(hero)),
            LeanLevel = leanLevel,
            ItemOnOffer = OfferedItem(offeredItemId)?.Name?.ToString(),
            ItemOnOfferValue = OfferedItem(offeredItemId)?.Value ?? 0,
            ItemOnOfferMost = OfferedItem(offeredItemId) is { } onOffer ? ItemSalePolicy.MostFor(onOffer.Value, NatureReader.LovesKindOf(onOffer, hero, profile)) : null,
            // RaveN017Mod (01/11/2026): what the character holds and would give, before they name a sum.
            Purse = hero?.Gold,
            GiftCeiling = hero == null ? null : ExchangeService.GiftCeilingFor(hero, profile),
            PriceReference = PriceReferenceReader.Full,
            PriceReferenceCompact = PriceReferenceReader.Compact,
            MoneyTokens = offerMoneyTokens && hero != null ? ExchangeService.MoneyTokensFor(hero, profile, OfferedItem(offeredItemId), NatureReader.LovesKindOf(OfferedItem(offeredItemId), hero, profile)) : null,
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
            PlayerGear = ComposeGear(Hero.MainHero, AppearanceVoice.Subject, EquipmentLoadoutPolicy.PreferCivilianLoadout(bridge.GetCurrentScene())),
            NpcSelfGear = ComposeGear(hero, AppearanceVoice.Self, EquipmentLoadoutPolicy.PreferCivilianLoadout(bridge.GetCurrentScene())),
            PlayerLooksWealthy = captorIsBandit && PlayerLooksWealthy(),
            PlayerLooksImportant = captorIsBandit && PlayerLooksImportant(),
            CompanionAskingPrice = ResolveCompanionAskingPrice(hero),
            // Same conditional-teaching contract as CompanionAskingPrice just above: the challenge_duel verb is
            // taught exactly when DuelEligibilityResolver (the same gate the bridge re-checks at commit time)
            // rules the game can actually stage the fight this instant.
            DuelChallengeAvailable = duelChallengeAvailable,
            // Gabriel (28/10/2026): nobles duel, everyone else brawls; avalanche24: no brawl started without a grievance.
            DuelIsBrawl = duelChallengeAvailable && DuelEligibilityResolver.KindOf(hero) == Logic.Duels.DuelKind.Brawl,
            NpcMayNotCallOut = duelChallengeAvailable && !DuelEligibilityResolver.MayCallOut(hero, profile),
            // fearente (07/11/2026): the answer to the player's challenge, weighed before the character speaks.
            DuelRefusesPlayersChallenge = duelChallengeAvailable && DuelEligibilityResolver.WouldRefusePlayer(hero, profile, witnesses?.Count ?? 0),
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
            // Station, unconditionally: see the EncounterContext docs. A ruler used to learn his own rank only
            // through the vassal offer above, which is gated, so he denied the crown everywhere else. Read
            // through StationContextFor, the ONE reader the council shares (audit, 15/09/2026: a seated
            // governor did not know he governed, because the table had its own, emptier idea of a person).
            RuledRealmName = station.RuledRealmName,
            RuledRealmRulerTitle = station.RuledRealmRulerTitle,
            LeadsOwnClan = station.LeadsOwnClan,

            // The post the man actually holds. Read from the engine's own GovernorOf, which this file ALREADY
            // reads a few lines below to decide whether appoint_governor may be offered: the fact was here all
            // along, used to gate an action and never to state a truth (fkasad, 2026-09-11).
            // The speaking character's own house, for the station pack's clan-head line. Read from the LIVE
            // hero rather than from the stored profile string, which is a snapshot taken when the profile was
            // first built and would be stale for anyone who has changed house since.
            SpeakerClanName = station.SpeakerClanName,
            GovernedSettlementName = station.GovernedSettlementName,
            GovernsForPlayerHouse = hero?.GovernorOf?.Settlement?.OwnerClan != null
                                    && hero.GovernorOf.Settlement.OwnerClan == Clan.PlayerClan,

            // Knowledge composition, increment 1 (ROADMAP: "WHAT A CHARACTER KNOWS"). Who this person is, and
            // what they are to the player: the factory weighs both, because role alone would hand a stranger
            // the player's ledger while fixing one companion. Read through KnowledgeContextFor, the ONE reader
            // the council shares — the table had no knowledge at all before 16/09/2026, so a lord who could
            // name his house's fiefs in a private word could name nothing at his own council.
            Bearer = knowledge.Bearer,
            HouseStanding = knowledge.HouseStanding,
            SeatStanding = knowledge.SeatStanding,
            HouseMeans = knowledge.HouseMeans,
            OwnBody = knowledge.OwnBody,
            MarketWord = knowledge.MarketWord,
            Underworld = knowledge.Underworld,
            Folklore = knowledge.Folklore,
            OwnVentures = knowledge.OwnVentures,
            HeldFor = knowledge.HeldFor,
            CaptiveNotoriety = knowledge.CaptiveNotoriety,
            // An authored quest this character is part of now (docs/design/AUTHORED_QUESTS.md).
            AuthoredQuestBlock = Quests.AuthoredQuestBehavior.Instance?.PromptBlockFor(hero),
            // Its choices by words open with this character, taught to whoever reads this turn's actions; and, at
            // Hardcore in a lovemaking the game permits, the gestures this character loves or dislikes (Gabriel,
            // 26/10/2026). Never in a captive scene: a forced act belongs to Trauma and Misdeeds.
            StoryChoicesForInterpreter = JoinReaderBlocks(Quests.AuthoredQuestBehavior.Instance?.InterpreterChoicesFor(hero),
               IntimateActApplier.ReaderBlock(hero, profile, bridge,
                  playerStatusOverride != null || isCaptorScene || isCollectiveCaptiveScene || companionActingOnCaptive || isBroughtCaptiveTurn)),
            ProseReadsItsOwnActions = proseReadsItsOwnActions,
            // fkasad (07/10/2026): the tale is their errand; a hideout task beside it put two targets in the journal.
            InActiveWrittenTale = Quests.AuthoredQuestBehavior.Instance?.HoldsARole(hero?.StringId) == true,
            PartyStanding = knowledge.PartyStanding,

            // The post the mod's own verb assigned and never mentioned. Named here so the station pack can
            // say it, exactly as GovernedSettlementName is named for the governorship.
            PartyPostHeld = station.PartyPostHeld,

            // The last of the player-granted posts nobody had told the holder about. The register has existed
            // since ride_with_me shipped and was read only by the action menu, never by a conversation.
            RidesAsRetainer = station.RidesAsRetainer,
            HereAndNow = knowledge.HereAndNow,
            PersonalBonds = knowledge.PersonalBonds,
            DayMood = knowledge.DayMood,
            DayMoodCause = knowledge.DayMoodCause,
            WitPermitted = knowledge.WitPermitted,
            DayMoodNow = Chat.ConversationMood.NowFor(hero?.StringId, knowledge.DayMood),
            SelfEsteem = knowledge.SelfEsteem,
            Audience = BuildListeningAudience(profile, witnesses),
            // follow_me: taught only when EscortEligibilityPolicy.CanEscort says the game can truly honour it
            // right now (see the computation just above the object initializer). dismiss_escort is the mirror,
            // taught only while THIS lord's own party is genuinely escorting the player.
            NpcCanEscortPlayer = npcCanEscortPlayer,
            // He may ride, but his realm is at war, so the muster may call him away. He is told to say so
            // as he agrees rather than leaving the player to find out when he goes (Gabriel, 2026-09-10).
            // The same warning for ride_with_me, now that a lord of a warring realm may ride in the player's party
            // (Lordfadooboo, Maifailun, Nexus, 01-03/11/2026). The two verbs never both apply to one lord.
            NpcMayBeCalledToMuster = (npcCanEscortPlayer
                                      && EscortEligibilityPolicy.ShouldWarnOfMuster(
                                         EscortFactsFor(hero, ResolveNpcIsEscortingPlayer(hero))))
                                     || (npcCanRideWithPlayer && RideWithMeEligibilityPolicy.ShouldWarnOfMuster(
                                         new RideWithMeEligibilityPolicy.RideWithMeFacts {RealmAtWar = FactionIsAtWarWithAnyone(hero?.MapFaction)})),
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
            // take_into_service: the mirror direction, see ResolveCompanionsOfferableToThisLord below.
            CompanionsOfferableToThisLord = ResolveCompanionsOfferableToThisLord(hero),
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
            // Set AFTER the line above on purpose: resolving the reason is what records the seeker, so on the
            // first turn this reads the fact that call just wrote, and on every later turn it reads it again.
            NpcSoughtThePlayer = InterceptionBehavior.Instance?.SoughtThePlayerThisConversation(hero) ?? false,
            // CrAims (tashmetu, 14/10/2026): the lord's own aim, and whether this conversation gave him leave to raise
            // it (decided once, when it opened). Null when he holds none.
            OwnAimWant = OwnAim(hero)?.Want,
            OwnAimPlayerPart = OwnAim(hero)?.PlayerPart,
            OwnAimApproach = OwnAim(hero)?.Approach,
            OwnAimMayRaise = OwnAim(hero)?.MayRaise ?? false,
            WarDutyTerms = WarDutyTerms(hero),
            // Resolved HERE, on the game's own thread, rather than inside the prompt builder: the chat call runs
            // the builder inside a Task.Run, and a third-party {{variable}} provider that reads Hero.MainHero or
            // Campaign.Current live (tashmetu's bridge does, on purpose) must never be handed a thread-pool
            // thread while the simulation runs. Context building is always on the main thread.
            ExternalPromptVariables = ResolveExternalPromptVariables(hero, profile, leanLevel),
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
            // The Prompt Composer (increment 2): the order of this character's sections under the player's prompt profile,
            // by kind (null: the stock order). The same station the guidelines above follow decides the kind.
            PromptLayout = PromptProfileService.LayoutFor(PromptKindPolicy.Of(new PromptKindFacts {
               IsPlayerSpouse = npcIsSpouse, IsPlayerCompanion = npcIsPlayerCompanion, IsLord = hero?.IsLord == true,
               InCaptiveScene = isCaptorScene || isCollectiveCaptiveScene || playerStatus == PlayerStatusVsNpc.Captive || playerStatus == PlayerStatusVsNpc.NpcIsCaptive,
               StationKey = StationKeyOf(hero)
            })),
            // Increment 3: the sections the profile rewords (read with the profile; each meets its own gate in the SDK).
            PromptTexts = PromptProfileService.Texts,
            LoveMatchEligible = ResolveLoveMatchEligible(hero, profile),
            MarriageBlockedBecause = ResolveMarriageBlockReason(hero, profile),
            LeaveHouseClosedBecause = ResolveLeaveHouseObstacle(hero, profile),
            LoveMatchReluctant = ResolveLoveMatchReluctant(hero, profile),
            LoveMatchBlessed = ResolveLoveMatchBlessed(hero),
            // "Marry your own companion": when the eligible partner is one of the player's OWN companions, the
            // marriage teaching adds a line so the model knows the wedding turns them from a hired retainer into
            // the player's spouse and a full member of the house (they must not narrate themselves still a mere
            // companion afterward). Only meaningful when LoveMatchEligible is true.
            PartnerIsOwnCompanion = MarryCompanionResolver.IsEligibleCompanionMatch(hero),
            NpcSpouseIsPlayer = hero?.Spouse == Hero.MainHero && Hero.MainHero != null,
            // avalanche24 (04/11/2026): the bond stated with its season, the same calendar the Heart page dates it by.
            RomanticBondSince = profile?.Romantic?.StatusSinceDay is int bondDay ? HeartPageReader.When(bondDay) : null,
            // fearente (26/10/2026): the game counts a hired companion in the player's clan; identity says what they are.
            // A companion the player married is a spouse, never a hireling (CR's own wedding clears CompanionOf; another mod's may not).
            IsHiredCompanion = hero != null && hero.IsPlayerCompanion && hero.Spouse != Hero.MainHero,
            OwnStation = SocialStationReader.Who(hero),
            BesideThePlayer = PlayerRungReader.LineFor(hero),
            KinshipToPlayer = ResolveKinshipToPlayer(hero),
            DaysSinceJoiningPlayer = hero != null && hero.IsPlayerCompanion && profile?.JoinedPlayerCompanyDay is int joined
               ? (int) CampaignTime.Now.ToDays - joined
               : null,
            NpcIsPlayerHousehold = IsPlayerHousehold(hero, profile),
            MarriedSeductionStance = ResolveMarriedSeductionStance(hero, profile),
            SpouseMarriedUnwillingly = ResolveSpouseMarriedUnwillingly(hero),
            ConsortEligible = ResolveConsortEligible(hero, profile),
            SecretLoverEligible = ResolveSecretLoverEligible(hero, profile),
            OpenRelationshipEligible = ResolveOpenRelationshipEligible(hero, profile),
            AcceptOtherPartnersEligible = ResolveAcceptOtherPartnersEligible(hero, profile),
            PartnerLoverKnown = ResolvePartnerLoverKnown(profile),
            PartnerLoverName = ResolvePartnerLoverName(profile),
            PartnerLoverIsSecret = profile?.Romantic?.LoverIsSecret ?? false,
            HeldSecretLoverSubject = ResolveHeldSecretLoverSubject(hero),
            HeldSecretHushRecentlyPaid = ResolveHeldSecretHushRecentlyPaid(hero),
            ConceptionRisk = ResolveConceptionRisk(hero),
            PowerBalanceNote = ResolvePowerBalanceNote(hero, profile),
            MarriageProspects = MarriageProspectResolver.Build(hero),
            MatchmakerNpcKin = MatchmakerResolver.BuildNpcKin(hero),
            MatchmakerPlayerKin = MatchmakerResolver.BuildPlayerKin(),
            // The player's chosen writing voice, served from memory unless the pick or the file changed.
            NarrativeStyle = StyleService.CurrentRules(),
            // The news now travels as FACTS rather than as a pre-formatted block: how much of it a character
            // carries is a rule about people, and RealmNewsPack owns it. The two older fields are left on
            // EncounterContext and still render if some other mod sets them - the host simply no longer does,
            // so nothing is said twice.
            RealmNews = knowledge.RealmNews,

            // Player report 2026-09-12: the tolerance existed and never reached the prompt, so a character
            // whose profile said "comfortable with multiple partners" was docked for exactly that and could
            // not say why. Gated on the PLAYER's clan tier, like the jealousy path it explains, so the two
            // cannot disagree about who is grand enough to keep consorts.
            SharedPartnersAreCustomary =
               JealousyCultureLoader.GetCulture(hero?.Culture?.Name?.ToString())
                                    .ToleratesSharedPartners(StatureReader.Player.ClanTier),
            PlayerDeedsHeard = ResolvePlayerDeedsHeard(hero),
            PlayerFameLine = PlayerFameResolver.Resolve(hero, profile, bridge, historyKnownThrough),
            EdictsNote = EdictNoteResolver.Resolve(hero),
            QueryRelevantFactsBlock = ResolveQueryRelevantFacts(hero, playerMessage, (int) CampaignTime.Now.ToDays),
            PastOnMentionBlock = ResolvePastOnMention(hero, playerMessage, (int) CampaignTime.Now.ToDays),
            CompanionOnErrand = ResolveCompanionOnErrand(hero),
            CompanionNewsReport = CompanionMissionBehavior.Instance?.ConsumeNewsReport(hero),
            CompanionMoodNote = ResolveCompanionMoodNote(hero),
            CompanionCampNote = ResolveCompanionCampNote(hero),
            // Companion-briefing pillar: the model needs to know it is IN that private word right now.
            // (What the briefing PRODUCES no longer travels through this context at all: since 13/09/2026 it
            // is an IsPrivate NotableEvent on each participant's own profile, which reaches their prompt
            // through the ordinary history and is filtered out of everyone else's by BuildWitnessMemory.)
            IsCompanionBriefingScene = isCompanionBriefingScene,
            CompanionBriefingTargetName = companionBriefingTargetName,
            IntimacyBargainEligible = IntimacyBargainEligibility.IsEligible(hero),
            IntimacyLeverageActive = IntimacyBargainEligibility.IsEligible(hero) && profile?.IntimacyLeverageHeld == true,
            BastardMotherTone = ResolveBastardMotherTone(hero),
            BastardBlackmailDemand = BastardBehavior.Instance?.BlackmailDemandOver(hero) ?? 0,
            // Fixes a player report: a pregnant NPC woman denied being with child in chat because her
            // pregnancy state was never injected into the prompt. See ResolvePregnancyNote.
            PregnancyNote = ResolvePregnancyNote(hero),
            ExtraActionTeachings = ResolveExtraActionTeachings(hero, profile, playerStatus, bridge, playerMessage, witnesses),
            UnavailableDeeds = ResolveUnavailableDeeds(hero, profile, playerStatus, bridge, playerMessage)
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
            CaptureStake? capture = null;
            foreach ((WorldEvent e, RumorConfidence conf) in rated)
            {
               if (!e.InvolvesPlayer || string.IsNullOrWhiteSpace(e.Summary)) continue;

               bool aggrieved = obsFactionId != null && e.AggrievedFactionIds != null && e.AggrievedFactionIds.Contains(obsFactionId);
               bool kinHarmed = obsClan != null && e.AggrievedActorIds != null && e.AggrievedActorIds.Any(id => IsClanmate(id, obsClan, hero));
               // Fakade (08/10/2026): a capture is framed once, by the closest stake among the captures heard.
               if (e.Status == CaptureNorm.Status)
               {
                  CaptureStake stake = CaptureNorm.StakeOf(kinHarmed, aggrieved);
                  if (capture == null || stake > capture) capture = stake;
               }
               bool sharesPlayer = obsFactionId != null && obsFactionId == playerFactionId;

               string valence;
               switch (PlayerDeedValence.Classify(new PlayerDeedValence.ObserverStakeFacts {FactionAggrieved = aggrieved, KinHarmed = kinHarmed, SharesPlayerSide = sharesPlayer}))
               {
                  case DeedValence.Wound: valence = "(this cut against you or your own) "; break;
                  case DeedValence.Boon: valence = "(this served your side) "; break;
                  default: valence = ""; break;
               }

               string doubt = conf == RumorConfidence.Distant
                  ? " — though none can fully vouch for it"
                  : "";
               lines.Add($"- {valence}{RumorDiscretion.TagFor(e.KnownOnlyToThoseInvolved)}{e.Summary.TrimEnd('.', ' ')}{doubt}.");

               if (lines.Count >= 3) break;
            }

            if (lines.Count > 0 && capture is CaptureStake heardStake) lines.Add(CaptureNorm.Line(heardStake));

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
      ///   voice, then what other mods add of their looks (CrAppearance, since 2.8.4), never changing CR's own. Returns
      ///   null (renders nothing) for a null hero or when nothing stands out, no prose exists and no mod adds a line.
      /// </summary>
      internal static string ComposeAppearance(Hero hero, AppearanceVoice voice)
      {
         if (hero == null) return null;

         string composed = AppearanceComposer.Compose(
            AppearanceFactsExtractor.Extract(hero),
            AppearanceProseBehavior.Instance?.Get(hero.StringId),
            voice);

         return AppearanceComposer.WithOutsideLines(composed, CrAppearance.LinesFor(hero.StringId, voice == AppearanceVoice.Subject));
      }

      /// <summary>
      ///   Equipment-awareness pillar: composes a hero's FACTUAL worn-gear clause from the SCENE-APPROPRIATE
      ///   loadout (civilian inside a settlement, battle in the field: <paramref name="preferCivilian" />, from
      ///   <see cref="EquipmentLoadoutPolicy" />), in the given voice. Full gear, not standouts only (chosen
      ///   2026-09-02, fkasad/TOR report): the model is handed what the hero actually wears so it never guesses
      ///   the wrong kit. Returns null (renders nothing) for a null hero or when nothing at all is equipped.
      /// </summary>
      private static string ComposeGear(Hero hero, AppearanceVoice voice, bool preferCivilian)
      {
         if (hero == null) return null;

         string composed = EquipmentNotabilityPolicy.ComposeWorn(
            EquipmentFactsExtractor.Extract(hero, preferCivilian), hero.IsFemale, voice);

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
      internal static int HeaviestGrudgeAgainstPlayer(Hero hero)
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
      ///   ("...saw them put my host to flight"), so no new persisted state is needed. Kept in sync
      ///   with <c>WorldEventFeeders.RecordBattlePerformanceIntoProfiles</c>. BOTH phrasings are matched:
      ///   memories written before 20/09/2026 say "put their host to flight" and sit in every existing save,
      ///   and a lord who beat the player then must not stop reading as beaten now.
      /// </summary>
      private static bool LostBattleToPlayerRecently(NpcProfile profile)
      {
         if (profile?.Events == null || profile.Events.Count == 0) return false;
         var today = (int) CampaignTime.Now.ToDays;
         foreach (NotableEvent e in profile.Events)
         {
            if (today - e.gameDay > RecentDefeatWindowDays) continue;

            if (string.IsNullOrEmpty(e.summary)) continue;

            if (e.summary.IndexOf("put my host to flight", StringComparison.OrdinalIgnoreCase) >= 0
                || e.summary.IndexOf("put their host to flight", StringComparison.OrdinalIgnoreCase) >= 0)
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

            // "Important" is a house of landed nobility (Stature.LandedNobleTier, the 3 this line always held) or real sway.
            return StatureReader.Player.IsLandedNoble || c.Influence >= 150f;
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

            return PregnancyNotePolicy.Describe(new PregnancyNotePolicy.PregnancyFacts {IsPregnant = true, FatherIsPlayer = fatherIsPlayer, FatherName = fatherName, FatherNamable = fatherNamable, DaysRemaining = daysRemaining, TotalDurationDays = totalDuration});
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
      /// <summary>
      ///   What a captive of the player may offer for her freedom (CaptiveOfferPolicy; Maifailun and Gabriel, 14/10/2026).
      ///   Null for anyone who is not the player's captive. Ransom is paid by her house's head (herself when she leads it).
      ///   Internal (not private) so the bridge's counter_offer executor can re-decide the SAME offer at execution,
      ///   the same way the prompt that taught it was decided (the prompt is advice, the bridge is law).
      /// </summary>
      internal static CaptiveOffer ResolveCaptiveOffer(Hero hero, NpcProfile profile, PlayerStatusVsNpc playerStatus, bool canRecruitPrisoner)
      {
         try
         {
            if (playerStatus != PlayerStatusVsNpc.NpcIsCaptive || hero == null || Hero.MainHero == null) return null;

            Hero payer = hero.Clan?.Leader ?? hero;
            Kingdom playersRealm = Hero.MainHero.Clan?.Kingdom;
            int comesOfAge = Campaign.Current?.Models?.AgeModel?.HeroComesOfAge ?? 18;

            return CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {
               Traits = new CalradiaRemembers.Logic.Appreciations.TraitFacts {
                  Honor = TraitLevel(hero, DefaultTraits.Honor), Valor = TraitLevel(hero, DefaultTraits.Valor), Mercy = TraitLevel(hero, DefaultTraits.Mercy),
                  Calculating = TraitLevel(hero, DefaultTraits.Calculating), Generosity = TraitLevel(hero, DefaultTraits.Generosity)
               },
               Fear = profile != null ? StanceService.ToStance(profile).Fear : 0,
               RansomMeans = payer.Gold,
               LeadsHerClan = hero.Clan != null && hero.Clan.Leader == hero,
               PlayerServesARealm = playersRealm != null,
               HerClanIsInThatRealm = playersRealm != null && hero.Clan?.Kingdom == playersRealm,
               RulesHerRealm = hero.Clan?.Kingdom != null && hero.Clan.Kingdom.RulingClan == hero.Clan,
               ServiceIsOpen = canRecruitPrisoner,
               ContentLevel = ModSettings.AdultContentLevel,
               BothOfAge = hero.Age >= comesOfAge && Hero.MainHero.Age >= comesOfAge,
               OrientationAllowsThePlayer = profile?.Romantic?.IsCompatibleWith(Hero.MainHero.IsFemale) == true
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CAPTIVE] offer could not be decided for {hero?.Name}: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static bool ResolveCaptiveFearCoerced(Hero hero, NpcProfile profile, PlayerStatusVsNpc playerStatus)
      {
         try
         {
            if (playerStatus != PlayerStatusVsNpc.NpcIsCaptive || profile == null) return false;

            NpcStance stance = StanceService.ToStance(profile);
            int honor = TraitLevel(hero, DefaultTraits.Honor);
            int valor = TraitLevel(hero, DefaultTraits.Valor);
            int calculating = TraitLevel(hero, DefaultTraits.Calculating);

            CoercionResult coercion = StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = stance.Fear, Respect = stance.Respect, Honor = honor, Valor = valor, Calculating = calculating});

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

            return PlayerExecutionPolicy.CanExecute(new PlayerExecutionPolicy.ExecutionFacts {McmOn = ModSettings.AllowCaptorExecutions, PlayerIsCaptive = playerIsCaptiveOfThisHero, Hardcore = ModSettings.AdultContentLevel == AdultContentLevel.Hardcore, DeathAllowed = !CampaignOptions.IsLifeDeathCycleDisabled});
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
            int rel = PairBondReader.Points(hero, c);

            if (Math.Abs(rel) <= Math.Abs(strongest)) continue;
            strongest = rel;
            other = c;
         }

         if (other == null || !CompanionRapport.WorthRaising(new NpcMemoryService.Core.Standing.VanillaRelation(strongest))) return null;

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

         // froggyluv (04/11/2026): only the recorded causes are named, so no broken promise is invented.
         return CompanionGrievancePolicy.MoodNote(score, CompanionHappinessService.Grievances.Recent(hero.StringId, (int) CampaignTime.Now.ToDays));
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

         ConceptionRole role = ConceptionResolver.Resolve(new ConceptionResolver.ConceptionFacts {
            Player = new ConceptionResolver.Participant
               {IsFemale = player.IsFemale, IsAlive = player.IsAlive, Age = player.Age},
            Npc = new ConceptionResolver.Participant
               {IsFemale = hero.IsFemale, IsAlive = hero.IsAlive, Age = hero.Age}
         });

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
      /// <summary>
      ///   Plural acceptance (22/09/2026, yozakura12): true when this committed partner may be asked to accept
      ///   that the PLAYER keeps others while she remains his alone. Asks the SAME pure rule the bridge asks at
      ///   execution, through the SAME facts, so the prompt cannot promise what the bridge refuses: the two
      ///   halves of open_relationship's decision had drifted apart, and docs/maps/gates.md exists because of it.
      /// </summary>
      private static bool ResolveAcceptOtherPartnersEligible(Hero hero, NpcProfile profile)
      {
         try
         {
            return PluralAcceptancePolicy.CanBeOffered(BannerlordGameStateBridge.PluralAcceptanceFactsFor(hero, profile));
         }
         catch { return false; }
      }

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

         return HeroLabel.Describe(new HeroLabel.HeroNaming {Name = lover.Name?.ToString(), ClanName = lover.Clan?.Name?.ToString(), CultureName = lover.Culture?.Name?.ToString()});
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

         return profile.ReputationWithPlayer >= Regard.DeepBondTier;
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
                                                        string playerMessage,
                                                        IReadOnlyList<WitnessEntry> witnesses = null)
      {
         if (hero == null || profile == null) return null;
         if (playerStatus == PlayerStatusVsNpc.Captive) return null;

         var sb = new StringBuilder();
         var verbCtx = new VerbContext(hero, profile, bridge, playerMessage, null) {
            OthersPresentIds = witnesses?.Select(w => w?.HeroStringId).Where(id => !string.IsNullOrEmpty(id) && id != hero.StringId).ToList()
         };

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

         // The task and match verbs, each taught only while its task or match is live.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, TaskVerbs));

         // Prisoner trade: buy (the NPC pays for a captive the player holds) and sell (the player pays
         // for one the NPC's clan holds). Each verb gates itself on the live holdings.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, PrisonerTradeVerbs));

         // end_marriage (DIVORCE, Phase 1): the first verb designed for the contract from the start.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, DivorceVerbs));

         // leave_house_to_wed: only to a ruler who heads her house, her heart won, an heir at the player's side.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, HouseVerbs));

         // make_amends + grudge_terms (Grudges pillar, resolution + discovery halves): taught last of all.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, GrudgeVerbs));

         // admit_secret_lover, only while the player has a secret bond this NPC does not know.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, ConfessionVerbs));

         // pay_bounty, only from a lord of a realm with a price on the player's head.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, LawVerbs));

         // extort and expose_secret, only while the player really holds what they would use.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, ExtortionVerbs));

         // reconcile_with, only while someone present is someone this character could make peace with.
         sb.Append(VerbRegistry.Instance.ComposeTeachings(verbCtx, PeaceVerbs));

         // Their own hidden affair: theirs to keep, never to lie about (Gabriel, 01/11/2026). The bond pack refuses a secret by design.
         sb.Append(HiddenAffairBehavior.Instance?.PromptFor(hero));

         // Another mod's actions (CrActions.Register): each one's TeachingText while its IsEligible holds. They had
         // no place in the name lists above, so until 05/10/2026 they were taught nothing but their catalog entry
         // (tashmetu, Bridges to Remember). In normal play nothing external is registered, so this appends nothing.
         sb.Append(VerbRegistry.Instance.ComposeExternalTeachings(verbCtx));

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
      ///   The other half of <see cref="ResolveExtraActionTeachings" />, and the one that was missing: the deeds
      ///   this conversation CANNOT deliver, named before the model speaks rather than refused after it has
      ///   spoken (Gabriel's ruling, 09/09/2026). A verb had two states until today, taught in full or absent,
      ///   and absence does not read to a language model as "forbidden" but as "not mentioned", so it supplied
      ///   the permission itself and the player met the contradiction one deed later.
      ///   <para>
      ///     Costs no extra LLM call, which is the point worth keeping in view: a gate is a function of world
      ///     state, never of what the model is about to say, so it is always knowable in advance. Only a deed's
      ///     PARAMETER (which price, which item, which person) depends on the utterance.
      ///   </para>
      ///   Null when nothing is shut worth saying, which is most turns: each verb decides for itself when its
      ///   deed is plausibly in play (see <see cref="IStatesUnavailability{TContext}" />), and silence is the
      ///   default. Skipped for a captive player for the same reason the teachings are.
      /// </summary>
      private static string ResolveUnavailableDeeds(Hero hero,
                                                    NpcProfile profile,
                                                    PlayerStatusVsNpc playerStatus,
                                                    IGameStateBridge bridge,
                                                    string playerMessage)
      {
         if (hero == null || profile == null) return null;
         if (playerStatus == PlayerStatusVsNpc.Captive) return null;

         var verbCtx = new VerbContext(hero, profile, bridge, playerMessage, null);
         List<DeedRefusal> refusals = VerbRegistry.Instance.ComposeUnavailability(verbCtx);

         // follow_me is taught inline rather than through the registry, so it contributes here. It is the deed a
         // player actually complained about (Nexus, 2026-09-10: "never follows me despite it agreeing to"), and
         // for the usual reason: an ineligible verb was simply absent, and absence reads to a model as "nobody
         // mentioned it" rather than as "you cannot". Named only on the NEAR MISS, where he leads a party and
         // could plainly do it, which is exactly when a player finds the silence arbitrary.
         DeedRefusal escort = EscortRefusalNote(hero, profile);
         if (escort != null) refusals.Add(escort);

         string block = DeedUnavailabilityPolicy.Compose(refusals);

         return string.IsNullOrWhiteSpace(block) ? null : block;
      }

      /// <summary>
      ///   The closed door on riding escort, or null to stay silent. Silent when the escort is available (it is
      ///   being taught in full), and silent when the lord leads no party of his own, since "he cannot escort
      ///   you" is not worth a line about a wanderer in a tavern who never could.
      /// </summary>
      private static DeedRefusal EscortRefusalNote(Hero hero, NpcProfile profile)
      {
         if (hero is not {IsLord: true}) return null;

         EscortRefusal refusal = EscortRefusalFor(hero, ResolveNpcIsEscortingPlayer(hero));

         if (refusal == EscortRefusal.None || refusal == EscortRefusal.NoPartyOfHisOwn) return null;

         return new DeedRefusal {
            Deed = "have your party ride escort to theirs",
            Reason = refusal switch {
               EscortRefusal.AtWarWithPlayer => "your realm and theirs are at war, and you will not ride beside an army you may soon fight",
               EscortRefusal.CommittedToWar => "your men are already committed, to an army or a siege or a field you cannot leave",
               EscortRefusal.AlreadyEscorting => "you are already riding escort to someone, and you will not promise it twice",
               _ => ""
            }
         };
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
      ///   Every third-party <c>{{variable}}</c>, resolved now, on this thread. Never throws: a bridge whose
      ///   provider fails must cost the conversation nothing (the registry already guards each provider, so this
      ///   is the belt to its braces), and an empty map simply means no bridge registered anything.
      /// </summary>
      private static IReadOnlyDictionary<string, string> ResolveExternalPromptVariables(Hero hero, NpcProfile profile, LeanPromptLevel leanLevel)
      {
         try
         {
            return PromptVariableRegistry.Compose(new PromptVarFacts {
               NpcId = profile?.Id ?? hero?.StringId ?? "",
               RelationToPlayer = profile?.ReputationWithPlayer ?? 0,
               // The level of the prompt these values go into, so a bridge can shrink with CR (tashmetu, 25/09/2026).
               Lean = leanLevel == LeanPromptLevel.Lean
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PROMPTVAR] resolving third-party variables failed: {ex.GetType().Name}: {ex.Message}");

            return new Dictionary<string, string>();
         }
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
      /// <summary>The terms a liege holds the player to for sitting out his war (the vassal war duty); null when nothing is owed.</summary>
      private static string WarDutyTerms(Hero hero)
      {
         try { return GameState.WarLevyDesk.TermsFor?.Invoke(hero); }
         catch { return null; }
      }

      private static Aims.AimForContext OwnAim(Hero hero)
      {
         try { return Aims.AimsBehavior.Instance?.ForContext(hero, CrConversation.CurrentSessionId); }
         catch (Exception ex)
         {
            ModLog.Error($"[AIMS] context read failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

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
      /// <summary>
      ///   take_into_service: the player's own companions THIS lord could take into their household, by name,
      ///   or null when there is nobody to give over (which is also how the verb stays untaught).
      ///   <para>
      ///     Runs the SAME pure gate the bridge runs
      ///     (<see cref="CompanionServiceTransferPolicy.Evaluate" />) once per companion, so a spouse, a
      ///     sitting governor, or someone away on an errand never reaches the prompt at all. That is the
      ///     ratified shape: an NPC must not be able to promise what the game will refuse.
      ///   </para>
      ///   <para>
      ///     Built 13/09/2026 on Gabriel's ruling. Fail-soft: any trouble reading the household leaves the
      ///     verb untaught, which costs a scene rather than risking a wrong one.
      ///   </para>
      /// </summary>
      // The last take_into_service verdict logged, so the line appears when the answer CHANGES rather than on
      // every context build. Keyed by lord too, so moving between conversations always reports afresh.
      private static string _lastOfferableVerdict;

      private static string ResolveCompanionsOfferableToThisLord(Hero lord)
      {
         try
         {
            if (lord == null || Hero.MainHero == null) return null;

            Clan playerClan = Clan.PlayerClan;
            if (playerClan?.Companions == null) return null;

            Clan lordClan = lord.Clan;

            // A hero of the player's OWN clan can never be offered anyone (the policy refuses on
            // LordIsOfThePlayersOwnClan), so there is no verdict here worth computing or logging - and this is
            // most of the calls, since every companion in the scene gets a context of their own.
            if (lordClan == playerClan) return null;

            var offerable = new List<string>();

            // Clan.Heroes, not Clan.Companions: turn_nemesis and recruit_prisoner swear a hero in by assigning
            // Hero.Clan directly and never set CompanionOf, so IsPlayerCompanion is false for exactly the
            // retainers a player is proudest of. See BannerlordGameStateBridge.HouseholdOfferableTo.
            foreach (Hero c in playerClan.Heroes)
            {
               if (c is not {IsAlive: true}) continue;

               var facts = new CompanionServiceTransferFacts {
                  LordIsAlive = lord.IsAlive,
                  LordIsPrisoner = lord.IsPrisoner,
                  LordIsThePlayer = lord == Hero.MainHero,
                  LordHasClan = lordClan != null,
                  LordClanIsPlayerClan = lordClan == playerClan,
                  CompanionResolved = true,
                  CompanionIsAlive = c.IsAlive,
                  CompanionIsPrisoner = c.IsPrisoner,
                  CompanionIsInYourHousehold = c.Clan == playerClan,
                  CompanionLeadsThePlayersClan = c == Hero.MainHero || c == playerClan.Leader,
                  CompanionIsPlayerFamily = BannerlordGameStateBridge.IsPlayersFamilyForGate(c),
                  CompanionIsPlayerSpouse = c == Hero.MainHero.Spouse,
                  CompanionAwayOnErrand = CompanionMissionBehavior.Instance?.IsAway(c.StringId) == true,
                  CompanionGovernsPlayerFief = c.GovernorOf != null
               };

               if (CompanionServiceTransferPolicy.Evaluate(facts) == CompanionServiceTransferBlock.None)
                  offerable.Add(c.Name?.ToString());
            }

            offerable.RemoveAll(string.IsNullOrWhiteSpace);

            // SAID OUT LOUD, because Gabriel's log could not answer "was the verb even on the table?" - the
            // same blind spot that cost a day. But said ONCE per answer, not once per context build: this runs
            // several times a turn, and the first version put four identical lines into his log before he had
            // even closed the briefing. A diagnostic nobody can skim is a diagnostic nobody reads, which is the
            // fault it was added to fix.
            string named = offerable.Count == 0 ? null : string.Join(", ", offerable);
            string verdict = $"{lord.StringId}|{named ?? "(nobody)"}";

            if (verdict != _lastOfferableVerdict)
            {
               _lastOfferableVerdict = verdict;
               ModLog.Info($"[TAKE-INTO-SERVICE] {lord.Name} could be offered: {named ?? "(nobody)"}.");
            }

            return named;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CONTEXT] take_into_service gate failed; leaving it untaught: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static bool ResolveCanRecruitNotable(Hero hero, NpcProfile profile)
      {
         try
         {
            if (hero == null || !hero.IsNotable) return false;

            bool hasSuccessor = BannerlordGameStateBridge.FindNotableSuccessor(hero) != null;

            return NotableRecruitmentPolicy.CanRecruit(new NotableRecruitmentPolicy.NotableRecruitmentFacts {IsNotable = hero.IsNotable, IsAlive = hero.IsAlive, IsPrisoner = hero.IsPrisoner, IsPlayerClanAlready = hero.Clan == Clan.PlayerClan, Regard = profile?.ReputationWithPlayer ?? 0, HasEligibleSuccessor = hasSuccessor});
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

            // Only what this person could teach THIS player: a master of a craft the player already knows
            // better has nothing to show them there, and the lesson would be offered and then refused.
            Hero player = Hero.MainHero;
            var teachable = new List<string>();
            foreach (SkillObject skill in Skills.All)
            {
               if (skill == null) continue;
               int playerLevel = player?.GetSkillValue(skill) ?? 0;
               if (TeachSkillPolicy.CanTeachPlayer(hero.GetSkillValue(skill), playerLevel))
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
               clauses.Add(CouncilAccordClause.TributeOwedToPlayer(new CouncilAccordClause.TributeToPlayer {PlayerName = playerName, AmountPerDay = tribute.AmountPerDay, DaysRemaining = tribute.DaysRemaining, MissedDays = tribute.MissedDays}));

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

         // RaveN017Mod (26/10/2026): the game weds no two heads of houses and refuses in silence, so a ruler who
         // heads her house is told it, and says what she can offer instead (Gabriel: she never leaves her throne).
         if (MarriageProspectResolver.HeadsOfTwoHouses(Hero.MainHero, hero)) return MarriageBlockReason.LeadsTheirOwnHouse;

         bool atWar = BannerlordGameStateBridge.IsAtWarWithPlayer(hero);

         // Captivity and war are one pure predicate (vanilla forbids both at its own marriage screen), so they
         // are asked together and then told apart only to name the obstacle.
         if (!MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = hero.IsPrisoner, PlayerPrisoner = Hero.MainHero.IsPrisoner, AtWarWithCandidate = atWar}))
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
      // Her one road is named closed only where it is hers to offer: a ruler who heads her house, facing a player who heads theirs.
      private static LeaveHouseObstacle ResolveLeaveHouseObstacle(Hero hero, NpcProfile profile)
         => ResolveMarriageBlockReason(hero, profile) == MarriageBlockReason.LeadsTheirOwnHouse
            ? LeaveHouseForRulerPolicy.ObstacleToName(LeaveHouseForRulerBehavior.WhyNot(hero, LeaveHouseRoad.GiveHerAll))
            : LeaveHouseObstacle.None;

      /// <summary>What <paramref name="hero" /> is to the player by blood (DepaHN, 27/10/2026), read off the game's family links.</summary>
      // A captor's terms are on offer only in a talk, and never from one the release gate refuses.
      private static bool ResolveCaptorOffers(Hero hero, PlayerStatusVsNpc playerStatus, CaptiveSceneIntent intent)
         => CaptiveSceneIntentRules.IsAudience(intent) && !ResolveCaptorReleaseVerdict(hero, playerStatus).RefusesAll;

      internal static string ResolveKinshipToPlayer(Hero hero)
      {
         Hero me = Hero.MainHero;
         if (hero == null || me == null || hero == me) return null;

         bool ParentOf(Hero child, Hero parent) => child != null && parent != null && (child.Father == parent || child.Mother == parent);
         bool Siblings(Hero a, Hero b) => a != null && b != null && a != b
                                          && ((a.Father != null && a.Father == b.Father) || (a.Mother != null && a.Mother == b.Mother));
         Hero[] myParents = {me.Father, me.Mother};
         Hero[] theirParents = {hero.Father, hero.Mother};

         return PlayerKinshipPolicy.Label(new PlayerKinFacts {
            IsFemale = hero.IsFemale,
            IsChild = ParentOf(hero, me),
            IsParent = ParentOf(me, hero),
            IsSibling = Siblings(hero, me),
            IsGrandchild = theirParents.Any(p => ParentOf(p, me)),
            IsGrandparent = myParents.Any(p => ParentOf(p, hero)),
            IsNephewOrNiece = theirParents.Any(p => Siblings(p, me)),
            IsUncleOrAunt = myParents.Any(p => Siblings(p, hero)),
            IsCousin = theirParents.Any(tp => myParents.Any(mp => Siblings(tp, mp)))
         });
      }

      internal static bool RegardMeetsMarriageBar(Hero hero, NpcProfile profile)
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
      /// <summary>The realm this hero actually rules, or null. No further condition: it states who they are.</summary>
      /// <summary>
      ///   Who this character is, for the knowledge factory. Plain engine reads; the RULE about what any of it
      ///   entitles them to know lives in the SDK, pure and unit-tested, never here.
      /// </summary>
      /// <summary>
      ///   Who is listening, so a pack can decide what would actually be SAID rather than only what is known.
      ///   A confidence is not given to a room, and not to someone barely known.
      /// </summary>
      /// <summary>
      ///   The season, the hour, the weather, and where on the map they are standing. Every value is one engine
      ///   read; nothing is inferred and nothing is stored.
      ///   <para>
      ///     Gabriel, 2026-09-12: "Il faudrait que le NPC ait conscience de où il se trouve sur la carte Monde."
      ///     CurrentLocationNote and GeographyNote both ground the settlement a character STANDS IN and both go
      ///     null the moment the party is on the open map, which is most of the game. So the WHERE here is only
      ///     ever filled for the country: inside a settlement the older field still owns that sentence, and two
      ///     sources for one fact is how two sources come to disagree.
      ///   </para>
      /// </summary>
      private static HereAndNowFacts BuildHereAndNow(Hero hero)
      {
         try
         {
            CampaignTime now = CampaignTime.Now;
            MobileParty party = hero?.PartyBelongedTo ?? MobileParty.MainParty;
            Settlement inside = hero?.CurrentSettlement ?? party?.CurrentSettlement;
            Settlement nearest = inside ?? NearestSettlementTo(party);

            return new HereAndNowFacts {
               Season = ReadSeason(now),
               HourOfDay = now.GetHourOfDay,
               IsNight = now.IsNightTime,
               Weather = ReadWeather(party),
               NearestPlace = nearest?.Name?.ToString(),
               IsInsideThatPlace = inside != null,
               BearingToPlace = inside != null ? null : BearingFrom(party, nearest),
               RealmName = (nearest?.MapFaction ?? party?.MapFaction)?.Name?.ToString()
            };
         }
         catch (Exception ex)
         {
            // Read on every single conversation: a bad read must cost the sentence, never the conversation.
            ModLog.Error($"[knowledge] here-and-now read failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Squared map distance, via the MapPoint helper so one type owns the arithmetic.</summary>
      private static float SquaredDistance(Vec2 a, Vec2 b)
         => new MapPoint(a.x, a.y).SquaredDistanceTo(new MapPoint(b.x, b.y));

      private static Season ReadSeason(CampaignTime now)
      {
         switch (now.GetSeasonOfYear)
         {
            case CampaignTime.Seasons.Spring: return Season.Spring;
            case CampaignTime.Seasons.Summer: return Season.Summer;
            case CampaignTime.Seasons.Autumn: return Season.Autumn;
            case CampaignTime.Seasons.Winter: return Season.Winter;
            default: return Season.Unknown;
         }
      }

      /// <summary>The engine's own weather event where the party stands, mapped onto the SDK's engine-free enum.</summary>
      private static Weather ReadWeather(MobileParty party)
      {
         if (party == null || Campaign.Current?.Models?.MapWeatherModel == null) return Weather.Unknown;

         switch (Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(party.GetPosition2D))
         {
            case MapWeatherModel.WeatherEvent.Clear: return Weather.Clear;
            case MapWeatherModel.WeatherEvent.LightRain: return Weather.LightRain;
            case MapWeatherModel.WeatherEvent.HeavyRain: return Weather.HeavyRain;
            case MapWeatherModel.WeatherEvent.Snowy: return Weather.Snow;
            case MapWeatherModel.WeatherEvent.Blizzard: return Weather.Blizzard;
            case MapWeatherModel.WeatherEvent.Storm: return Weather.Storm;
            default: return Weather.Unknown;
         }
      }

      /// <summary>The closest settlement worth naming, so a party in the country still knows roughly where it is.</summary>
      private static Settlement NearestSettlementTo(MobileParty party)
      {
         if (party == null || Settlement.All == null) return null;

         Settlement best = null;
         var bestDistance = float.MaxValue;

         foreach (Settlement candidate in Settlement.All)
         {
            if (candidate?.IsHideout != false) continue;

            float distance = SquaredDistance(candidate.GetPosition2D, party.GetPosition2D);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = candidate;
         }

         return best;
      }

      /// <summary>
      ///   How that place lies from here, in the words the mod already uses for a journey. Reuses
      ///   GeographyDescriber rather than composing a second phrasing for the same idea.
      /// </summary>
      private static string BearingFrom(MobileParty party, Settlement place)
      {
         if (party == null || place == null) return null;

         var from = new MapPoint(party.GetPosition2D.x, party.GetPosition2D.y);
         var to = new MapPoint(place.GetPosition2D.x, place.GetPosition2D.y);

         return GeographyDescriber.Bearing(
            from.StepTo(to), (float) Math.Sqrt(SquaredDistance(party.GetPosition2D, place.GetPosition2D)));
      }

      private static ListeningAudience BuildListeningAudience(NpcProfile profile, IReadOnlyList<WitnessEntry> witnesses)
         => new() {
            Regard = profile?.ReputationWithPlayer ?? 0,
            InPrivate = witnesses == null || witnesses.Count == 0
         };

      /// <summary>
      ///   The people this character carries who are NOT the player, drawn from what the simulation already
      ///   runs and nobody could mention (Gabriel, 2026-09-11).
      ///   <para>
      ///     Two sources today, both of them things that genuinely happen to NPCs without the player: a live
      ///     grudge whose TARGET is somebody else, and this character's own lover. Family, friends and rivalries
      ///     are the obvious next sources and are deliberately not guessed at here.
      ///   </para>
      ///   <para>
      ///     A SECRET affair is passed through with its discretion intact rather than filtered out here: the
      ///     pack never renders a secret, and keeping the judgement in one place is what stops this from
      ///     becoming a second authority on what may be told.
      ///   </para>
      /// </summary>
      private static PersonalBondsFacts BuildPersonalBonds(Hero hero, NpcProfile profile)
      {
         if (hero == null) return null;

         var bonds = new List<PersonalBond>();
         string playerId = Hero.MainHero?.StringId;
         int today = (int) CampaignTime.Now.ToDays;

         // Grievances against somebody other than the player. GrudgeNote already carries the ones aimed AT the
         // player; these are the ones he has never had anyone to tell.
         IReadOnlyList<Grudge> live = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);
         if (live != null)
            foreach (Grudge grudge in live)
            {
               if (grudge == null || string.IsNullOrEmpty(grudge.TargetId)) continue;
               if (grudge.TargetId == playerId) continue;

               string name = NameOf(grudge.TargetId);
               if (string.IsNullOrWhiteSpace(name)) continue;

               bonds.Add(new PersonalBond {
                  PersonName = name,
                  Kind = BondKind.Enemy,
                  Note = string.IsNullOrWhiteSpace(grudge.CauseSummary) ? null : grudge.CauseSummary.Trim(),
                  Discretion = Discretion.Open
               });
            }

         // His own lover, if he has one. A secret affair keeps its secrecy all the way through: the pack refuses
         // to render it, which is how the mystery-lover pillar keeps sole ownership of when it may surface.
         string loverId = profile?.Romantic?.TakenLoverId;
         if (!string.IsNullOrEmpty(loverId) && loverId != playerId)
         {
            string name = NameOf(loverId);
            if (!string.IsNullOrWhiteSpace(name))
               bonds.Add(new PersonalBond {
                  PersonName = name,
                  Kind = BondKind.Beloved,
                  Discretion = profile.Romantic.LoverIsSecret ? Discretion.Secret : Discretion.Guarded
               });
         }

         return bonds.Count == 0 ? null : new PersonalBondsFacts {Bonds = bonds};
      }

      /// <summary>A hero's display name from their id, or null when the world no longer has them.</summary>
      private static string NameOf(string heroId)
      {
         if (string.IsNullOrEmpty(heroId)) return null;

         Hero found = Hero.FindFirst(h => h != null && h.StringId == heroId);

         return found?.Name?.ToString();
      }

      /// <summary>
      ///   Who this character is, as the knowledge factory weighs it. <paramref name="holdsASeat" /> is passed
      ///   in rather than recomputed: it is true exactly when the seat facts were actually read, so the pack's
      ///   "he would know this" cannot drift from the host's "here it is".
      /// </summary>
      private static KnowledgeBearer BuildKnowledgeBearer(Hero hero, bool holdsASeat, bool hasAMarket,
                                                          PartyPost partyPost, bool ownsVentures)
      {
         if (hero == null) return null;

         return new KnowledgeBearer {
            IsLord = hero.IsLord,
            IsPlayerCompanion = hero.IsPlayerCompanion,
            IsOfPlayerClan = hero.Clan != null && hero.Clan == Clan.PlayerClan,
            IsNotable = hero.IsNotable,
            // Their trade AND somewhere to ply it. Derived from the same read that supplies the facts, so
            // "he would know his market" and "here is his market" cannot part company.
            TradesForALiving = hasAMarket
                               && (hero.Occupation == Occupation.Merchant
                                   || hero.Occupation == Occupation.Artisan),
            IsUnderworld = hero.Occupation == Occupation.GangLeader
                           || hero.Occupation == Occupation.Gangster,
            RidesWithThePlayer = hero.PartyBelongedTo != null && hero.PartyBelongedTo == MobileParty.MainParty,
            PartyPost = partyPost,
            OwnsVentures = ownsVentures,
            HasHouse = hero.Clan != null,
            HoldsASeat = holdsASeat,

            // No courier reaches a cell (Gabriel, 2026-09-12). The packs that are TODAY'S news are simply not
            // carried; the ones that are durable, or that he is standing in, still are.
            IsCutOff = hero.IsPrisoner
         };
      }

      /// <summary>
      ///   The standing of this character's OWN house, read live and thrown away with the encounter. Derived,
      ///   never stored: a persisted copy would be a second world model beside Bannerlord's, which this project
      ///   already pays for twice (two diverging regard ledgers, and romantic profiles generated once so later
      ///   rule changes never reach existing heroes).
      ///   <para>
      ///     Returns null when there is no house, and the pack reads that as UNSUPPLIED rather than as a house
      ///     with nothing: the completeness sweep needs those two to stay distinguishable, because their
      ///     confusion is the fkasad class of bug.
      ///   </para>
      /// </summary>
      private static HouseStandingFacts BuildHouseStanding(Hero hero)
      {
         Clan clan = hero?.Clan;
         if (clan == null || string.IsNullOrWhiteSpace(clan.Name?.ToString())) return null;

         var fiefs = new List<string>();
         if (clan.Fiefs != null)
            foreach (Town town in clan.Fiefs)
            {
               string name = town?.Settlement?.Name?.ToString();
               if (!string.IsNullOrWhiteSpace(name)) fiefs.Add(name);
            }

         var wars = new List<string>();
         IFaction mine = clan.MapFaction;
         if (mine != null && Kingdom.All != null)
            foreach (Kingdom other in Kingdom.All)
            {
               if (other == null || other == mine || other.IsEliminated) continue;
               if (!FactionManager.IsAtWarAgainstFaction(mine, other)) continue;

               string name = other.Name?.ToString();
               if (!string.IsNullOrWhiteSpace(name)) wars.Add(name);
            }

         return new HouseStandingFacts {
            HouseName = clan.Name.ToString(),
            IsPlayerHouse = clan == Clan.PlayerClan,
            Fiefs = fiefs,
            AtWarWith = wars
         };
      }

      /// <summary>
      ///   How the places this character answers for are actually doing. Read live and thrown away with the
      ///   encounter, like every other pack: a stored copy would be a second world model beside Bannerlord's.
      ///   <para>
      ///     Built after the first live cr.memory_bench run (2026-09-12), where a character given two fief names
      ///     and nothing else produced a harvest, grain lost to rot, granary stores and a garrison "at strength",
      ///     none of it in her prompt. Every reading below is one of those invented sentences made true.
      ///   </para>
      /// </summary>
      private static SeatStandingFacts BuildSeatStanding(Hero hero)
      {
         if (hero == null) return null;

         try
         {
            // Closest first: what he governs day to day before what he merely owns. The pack describes only the
            // first two, so the order here is what decides which places he actually speaks about.
            List<SeatFacts> seats = HeroSeats.ClosestFirst(hero)
                                             .Take(SeatStandingFacts.MaxSeatsDescribed)
                                             .Select(seat => ReadSeat(seat.Place, seat.Role))
                                             .ToList();

            // The pack only describes a place it knows something about, so the host must count them the same
            // way. Otherwise a seat read as all-unknown would be CARRIED and not SUPPLIED, and the sweep would
            // report a fault that is really two of my own rules disagreeing.
            seats = seats.Where(seat => seat != null && seat.HasAnyReading).ToList();

            return seats.Count == 0 ? null : new SeatStandingFacts {Seats = seats};
         }
         catch (Exception ex)
         {
            // Read on every conversation with a landed character: a bad read costs the sentence, not the scene.
            ModLog.Error($"[knowledge] seat standing read failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>One place, as the engine currently has it. Bands, because a lord has never counted any of it.</summary>
      /// <summary>
      ///   A town as its own gang leader knows it, in the seat pack's bands: the scout_town report reads the same facts
      ///   the gang leader's prompt was given (01/10/2026), so what he says and what is recorded cannot disagree.
      /// </summary>
      internal static SeatFacts ReadTownAsItsNotablesKnowIt(Settlement place)
         => place == null ? null : ReadSeat(place, SeatRole.Notable);

      private static SeatFacts ReadSeat(Settlement place, SeatRole role)
      {
         Town town = place.Town;

         return new SeatFacts {
            PlaceName = place.Name?.ToString(),
            Role = role,
            Kind = KindOf(place),
            IsPlayerHolding = place.OwnerClan != null && place.OwnerClan == Clan.PlayerClan,
            Prosperity = ReadProsperity(place),
            FoodStores = ReadFoodStores(town),
            FoodDirection = ReadFoodTrend(town),
            Garrison = ReadGarrison(town),
            Loyalty = ReadLoyalty(town),
            Security = ReadSecurity(town),
            IsUnderSiege = place.IsUnderSiege,
            IsBeingRaided = place.IsUnderRaid,
            IsLooted = place.Village != null && place.Village.VillageState == Village.VillageStates.Looted,
            IsInRebellion = place.InRebelliousState,
            IsStarving = place.IsStarving,
            // chmidt (23/10/2026): who lays the siege, and who sits in its dungeon (and whether the player left them).
            BesiegerName = place.IsUnderSiege ? Labelled(place.SiegeEvent?.BesiegerCamp?.LeaderParty?.LeaderHero) : null,
            PrisonersOfNote = PrisonersOfNote(place)
         };
      }

      // The prisoners of rank in a place's dungeon, labelled, with who left them when it was the player.
      private static List<string> PrisonersOfNote(Settlement place)
      {
         var named = new List<string>();
         if (place?.Party?.PrisonRoster == null) return named;

         string player = Hero.MainHero?.Name?.ToString();
         foreach (TaleWorlds.CampaignSystem.Roster.TroopRosterElement el in place.Party.PrisonRoster.GetTroopRoster())
         {
            Hero h = el.Character?.HeroObject;
            if (h == null || !h.IsLord || h == Hero.MainHero) continue;

            string label = Labelled(h);
            named.Add(PlayerDungeons.LeftThereByThePlayer(h) && player != null ? $"{label}, left there by {player}" : label);
            if (named.Count >= SeatFacts.MaxPrisonersNamed) break;
         }

         return named;
      }

      private static string Labelled(Hero h)
         => h == null ? null : HeroLabel.Describe(new HeroLabel.HeroNaming {Name = h.Name?.ToString(), ClanName = h.Clan?.Name?.ToString()});

      private static NpcMemoryService.Core.Knowledge.SettlementKind KindOf(Settlement place)
      {
         if (place.IsTown) return NpcMemoryService.Core.Knowledge.SettlementKind.Town;
         if (place.IsCastle) return NpcMemoryService.Core.Knowledge.SettlementKind.Castle;
         if (place.IsVillage) return NpcMemoryService.Core.Knowledge.SettlementKind.Village;

         return NpcMemoryService.Core.Knowledge.SettlementKind.Unknown;
      }

      /// <summary>
      ///   Prosperity has no natural ceiling to measure against, so it is read as a RANK among places of the
      ///   same kind rather than against a threshold invented here. That is also how a lord would say it: he
      ///   knows his town is richer than most towns, not that it scores 5163.
      /// </summary>
      private static ProsperityBand ReadProsperity(Settlement place)
      {
         float mine = WealthOf(place);
         if (mine <= 0f || Settlement.All == null) return ProsperityBand.Unknown;

         int poorer = 0, counted = 0;

         foreach (Settlement other in Settlement.All)
         {
            if (other == null || other.IsHideout || KindOf(other) != KindOf(place)) continue;

            counted++;
            if (WealthOf(other) < mine) poorer++;
         }

         if (counted < 4) return ProsperityBand.Unknown;

         float rank = poorer / (float) counted;

         if (rank < 0.25f) return ProsperityBand.Poor;
         if (rank < 0.55f) return ProsperityBand.Middling;
         if (rank < 0.80f) return ProsperityBand.Comfortable;

         return ProsperityBand.Rich;
      }

      /// <summary>A village's wealth is its hearths; a town's or castle's is its prosperity.</summary>
      private static float WealthOf(Settlement place)
         => place.Village != null ? place.Village.Hearth : place.Town?.Prosperity ?? 0f;

      /// <summary>The granary against the engine's OWN ceiling, so no threshold here has to be defended.</summary>
      private static FoodBand ReadFoodStores(Town town)
      {
         if (town == null) return FoodBand.Unknown;

         int ceiling = town.FoodStocksUpperLimit();
         if (ceiling <= 0) return FoodBand.Unknown;

         float share = town.FoodStocks / ceiling;

         if (share < 0.10f) return FoodBand.Empty;
         if (share < 0.35f) return FoodBand.Thin;
         if (share < 0.75f) return FoodBand.Adequate;

         return FoodBand.Full;
      }

      /// <summary>Which way it is going, which is the half of a steward's report that decides what to do.</summary>
      private static FoodTrend ReadFoodTrend(Town town)
      {
         if (town == null) return FoodTrend.Unknown;

         if (town.FoodChange < -0.5f) return FoodTrend.Falling;
         if (town.FoodChange > 0.5f) return FoodTrend.Rising;

         return FoodTrend.Steady;
      }

      /// <summary>
      ///   The garrison against the engine's own party-size limit for the place, so "under strength" means what
      ///   the game means by it rather than what a number here decided it should mean.
      /// </summary>
      private static GarrisonBand ReadGarrison(Town town)
      {
         if (town == null) return GarrisonBand.Unknown;

         PartyBase garrison = town.GarrisonParty?.Party;
         if (garrison == null) return GarrisonBand.Skeleton;

         // One home for the bands since a governor may also ASK for men by them (GarrisonStrength, 01/10/2026).
         return GarrisonStrength.BandOf(garrison.NumberOfHealthyMembers, garrison.PartySizeLimit);
      }

      /// <summary>On the engine's own 0-100 loyalty scale; below 25 is where vanilla itself starts worrying.</summary>
      private static LoyaltyBand ReadLoyalty(Town town)
      {
         if (town == null) return LoyaltyBand.Unknown;

         if (town.Loyalty < 25f) return LoyaltyBand.Rebellious;
         if (town.Loyalty < 50f) return LoyaltyBand.Restless;
         if (town.Loyalty < 75f) return LoyaltyBand.Content;

         return LoyaltyBand.Devoted;
      }

      /// <summary>On the engine's own 0-100 security scale. Low security IS banditry on the roads.</summary>
      private static SecurityBand ReadSecurity(Town town)
      {
         if (town == null) return SecurityBand.Unknown;

         if (town.Security < 25f) return SecurityBand.Lawless;
         if (town.Security < 60f) return SecurityBand.Uneasy;

         return SecurityBand.Settled;
      }

      /// <summary>
      ///   What this character's own house can bring to bear today: coin, men, and weight at court. The rest
      ///   of fkasad's report ("territories, armies, treasury, wars"), of which house_standing answered the
      ///   first and last on 2026-09-11.
      ///   <para>
      ///     Every band is a RANK among comparable houses rather than a threshold invented here: coin, men and
      ///     influence all have no ceiling, and a lord compares his house with other houses anyway. Bandit and
      ///     minor factions are excluded from the comparison because they would drag a distribution meant to
      ///     describe noble houses.
      ///   </para>
      /// </summary>
      private static HouseMeansFacts BuildHouseMeans(Hero hero)
      {
         Clan clan = hero?.Clan;
         if (clan == null) return null;

         try
         {
            return new HouseMeansFacts {
               Treasury = ReadTreasury(clan),
               OwesTheCrown = clan.DebtToKingdom > 0,
               Muster = ReadMuster(clan),
               RidingWithAnArmy = RidesWithAnArmy(clan),
               Influence = ReadInfluence(clan),

               // Named by the role audit: PlayerIsMercenary was carried all along and the NPC's own service
               // was not, so a captain under contract could not say who pays him.
               SellsSwordsTo = clan.IsUnderMercenaryService ? clan.Kingdom?.Name?.ToString() : null
            };
         }
         catch (Exception ex)
         {
            ModLog.Error($"[knowledge] house means read failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Every house worth comparing against: noble, alive, and not a bandit or a minor faction.</summary>
      private static List<Clan> ComparableHouses()
      {
         var houses = new List<Clan>();
         if (Clan.All == null) return houses;

         foreach (Clan other in Clan.All)
         {
            if (other == null || other.IsEliminated || other.IsBanditFaction || other.IsMinorFaction) continue;

            houses.Add(other);
         }

         return houses;
      }

      /// <summary>Where this house's value sits among comparable houses, 0 (poorest) to 1 (richest).</summary>
      private static float? RankAmong(List<Clan> houses, Clan mine, Func<Clan, float> value)
      {
         if (houses.Count < 4) return null;

         float ours = value(mine);
         var below = 0;

         foreach (Clan other in houses)
            if (value(other) < ours)
               below++;

         return below / (float) houses.Count;
      }

      /// <summary>The house_means treasury band, for the mood of the day (DayMoodReader) to read the same purse.</summary>
      internal static TreasuryBand ReadTreasuryFor(Clan clan) => clan == null ? TreasuryBand.Unknown : ReadTreasury(clan);

      private static TreasuryBand ReadTreasury(Clan clan)
      {
         float? rank = RankAmong(ComparableHouses(), clan, c => c.Gold);
         if (rank == null) return TreasuryBand.Unknown;

         if (rank < 0.20f) return TreasuryBand.Empty;
         if (rank < 0.50f) return TreasuryBand.Strained;
         if (rank < 0.85f) return TreasuryBand.Sound;

         return TreasuryBand.Deep;
      }

      private static MusterBand ReadMuster(Clan clan)
      {
         // Nobody in the field is a READING, not a missing one: a lord with no men cannot send you any, which
         // is exactly the promise the mod must stop him making.
         if (MenUnderArms(clan) <= 0) return MusterBand.Nothing;

         float? rank = RankAmong(ComparableHouses(), clan, MenUnderArms);
         if (rank == null) return MusterBand.Unknown;

         if (rank < 0.40f) return MusterBand.Small;
         if (rank < 0.85f) return MusterBand.Respectable;

         return MusterBand.Formidable;
      }

      private static float MenUnderArms(Clan clan)
      {
         if (clan?.WarPartyComponents == null) return 0f;

         var men = 0;

         foreach (WarPartyComponent component in clan.WarPartyComponents)
         {
            MobileParty party = component?.MobileParty;
            if (party == null || !party.IsActive) continue;

            men += party.MemberRoster?.TotalManCount ?? 0;
         }

         return men;
      }

      /// <summary>Men on the map and men on campaign are different answers to "can you come with me?".</summary>
      private static bool RidesWithAnArmy(Clan clan)
      {
         if (clan?.WarPartyComponents == null) return false;

         foreach (WarPartyComponent component in clan.WarPartyComponents)
            if (component?.MobileParty?.Army != null)
               return true;

         return false;
      }

      /// <summary>
      ///   Court standing is only meaningful inside a court, so the comparison is against the other houses of
      ///   the same realm. A landless or realmless house has no court to carry weight in, and says nothing.
      /// </summary>
      private static InfluenceBand ReadInfluence(Clan clan)
      {
         Kingdom kingdom = clan.Kingdom;
         if (kingdom?.Clans == null) return InfluenceBand.Unknown;

         var peers = new List<Clan>();

         foreach (Clan other in kingdom.Clans)
         {
            if (other == null || other.IsEliminated) continue;

            peers.Add(other);
         }

         float? rank = RankAmong(peers, clan, c => c.Influence);
         if (rank == null) return InfluenceBand.Unknown;

         if (rank < 0.25f) return InfluenceBand.Negligible;
         if (rank < 0.60f) return InfluenceBand.Modest;
         if (rank < 0.85f) return InfluenceBand.Considerable;

         return InfluenceBand.Commanding;
      }

      /// <summary>
      ///   The state of the body this conversation is happening in. Found by auditing EncounterContext rather
      ///   than by a player report: it returned ZERO for wound, injury and health while the engine kept
      ///   IsWounded, HitPoints, MaxHitPoints and IsDisabled all along.
      ///   <para>
      ///     The engine's own IsWounded flag decides whether they count as wounded at all, and the hit-point
      ///     ratio only grades HOW badly - the same deference here_and_now shows the engine's night reading
      ///     over arithmetic on the hour.
      ///   </para>
      /// </summary>
      private static OwnBodyFacts BuildOwnBody(Hero hero)
      {
         if (hero == null) return null;

         try
         {
            return new OwnBodyFacts {
               Hurt = ReadHurt(hero),
               // Not Hero.IsDisabled: in the engine that is HeroState == Disabled, a hero TAKEN OUT OF THE WORLD, not
               // a maiming (confirmed from the game's code, 30/09/2026). The game keeps no record of a lasting
               // injury, so nothing is claimed; a mod that adds one can supply it here.
               IsMaimed = false
            };
         }
         catch (Exception ex)
         {
            ModLog.Error($"[knowledge] body read failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   The band, decided by a pure policy so all four of its answers can be shown to be reachable. The
      ///   first version of this lived here inline and TWO of its four were impossible - see BodyHurtPolicy for
      ///   the seeded lord at 60% health who answered "I fare well enough".
      /// </summary>
      /// <summary>The own_body hurt band, for the mood of the day (DayMoodReader) to read the same body.</summary>
      internal static HurtBand ReadHurtFor(Hero hero) => ReadHurt(hero);

      private static HurtBand ReadHurt(Hero hero)
      {
         switch (BodyHurtPolicy.Read(hero.HitPoints, hero.MaxHitPoints, hero.IsWounded, hero.WoundedHealthLimit))
         {
            case BodyHurtPolicy.Hurt.Unhurt: return HurtBand.Unhurt;
            case BodyHurtPolicy.Hurt.Scratched: return HurtBand.Scratched;
            case BodyHurtPolicy.Hurt.Wounded: return HurtBand.Wounded;
            case BodyHurtPolicy.Hurt.Grievous: return HurtBand.Grievous;
            default: return HurtBand.Unknown;
         }
      }

      /// <summary>
      ///   What a trader knows of their own market: what the place makes, and what is dear or cheap in it.
      ///   <para>
      ///     The basket is the goods that actually pass through THIS market - a town's bound villages'
      ///     production, or a village's own - rather than a scan of every item in the game. That keeps the
      ///     read bounded (a handful of villages) and keeps the answer relevant: a merchant talks about what
      ///     comes through his own gates, not about the price of everything.
      ///   </para>
      /// </summary>
      private static MarketWordFacts BuildMarketWord(Hero hero)
      {
         Settlement place = hero?.HomeSettlement ?? hero?.CurrentSettlement;
         if (place == null) return null;

         try
         {
            List<ItemObject> basket = GoodsOf(place);

            var produces = new List<string>();
            foreach (ItemObject good in basket)
            {
               string name = good?.Name?.ToString();
               if (!string.IsNullOrWhiteSpace(name)) produces.Add(name);
            }

            var facts = new MarketWordFacts {
               PlaceName = place.Name?.ToString(),
               Produces = produces,
               Goods = PricedGoods(place, basket)
            };

            // A merchant in a castle, or in a town whose bound villages yield nothing readable, has a trade
            // and no market to describe. Returning an empty facts object would have the sweep report a fault
            // that is really two of my own rules disagreeing - the personal_bonds lesson, for the third time.
            return facts.HasAnyReading ? facts : null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[knowledge] market read failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>The goods this place deals in: its own production, or that of the villages bound to it.</summary>
      private static List<ItemObject> GoodsOf(Settlement place)
      {
         var goods = new List<ItemObject>();

         if (place.Village?.VillageType?.PrimaryProduction != null)
            goods.Add(place.Village.VillageType.PrimaryProduction);

         if (place.BoundVillages != null)
            foreach (Village bound in place.BoundVillages)
            {
               ItemObject made = bound?.VillageType?.PrimaryProduction;
               if (made != null && !goods.Contains(made)) goods.Add(made);
            }

         return goods;
      }

      /// <summary>
      ///   Each good against what it is normally worth. Both halves come from the engine - the local price and
      ///   the item's own base Value - so no threshold here is one this file invented. A village has no market
      ///   of its own, so it reports what it makes and says nothing about price.
      /// </summary>
      private static List<MarketGood> PricedGoods(Settlement place, List<ItemObject> basket)
      {
         var priced = new List<MarketGood>();

         TownMarketData market = place.Town?.MarketData;
         if (market == null) return priced;

         foreach (ItemObject good in basket)
         {
            if (good == null || good.Value <= 0) continue;

            int here = market.GetPrice(good, null, false, null);
            if (here <= 0) continue;

            float against = here / (float) good.Value;

            priced.Add(new MarketGood {
               Name = good.Name?.ToString(),
               Price = against > 1.25f ? PriceBand.Dear
                  : against < 0.80f ? PriceBand.Cheap
                  : PriceBand.Ordinary
            });
         }

         return priced;
      }

      /// <summary>
      ///   What the streets know: how badly the player is wanted in THIS character's realm, and who holds the
      ///   criminal trade of the place they are standing in.
      ///   <para>
      ///     The engine classifies the crime rating itself - mild, moderate, severe - so no threshold here is
      ///     one this file invented. Same deference own_body shows IsWounded and here_and_now shows IsNight.
      ///   </para>
      ///   <para>
      ///     Gabriel, 2026-09-12: "plusieurs joueurs jouent des malfrats avec le mod Fourberie." The voice of
      ///     a gang leader already existed in the behaviour guidelines; this is what he knows.
      ///   </para>
      /// </summary>
      private static UnderworldFacts BuildUnderworld(Hero hero)
      {
         if (hero == null) return null;

         try
         {
            Settlement place = hero.CurrentSettlement ?? hero.HomeSettlement;
            Hero boss = GangLeaderOf(place);

            return new UnderworldFacts {
               PlayerNotoriety = ReadNotoriety(hero.MapFaction),
               PlaceName = place?.Name?.ToString(),
               StreetsRunBy = boss == null || boss == hero ? null : boss.Name?.ToString(),
               YouRunTheseStreets = boss != null && boss == hero
            };
         }
         catch (Exception ex)
         {
            ModLog.Error($"[knowledge] underworld read failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   How the law of this realm sees the player, on the engine's own classification. A realmless
      ///   character has no law to answer to and reports nothing rather than vouching for anybody.
      /// </summary>
      private static Notoriety ReadNotoriety(IFaction faction)
      {
         CrimeModel crime = Campaign.Current?.Models?.CrimeModel;
         if (crime == null || faction == null) return Notoriety.Unknown;

         if (crime.IsPlayerCrimeRatingSevere(faction)) return Notoriety.Severe;
         if (crime.IsPlayerCrimeRatingModerate(faction)) return Notoriety.Moderate;
         if (crime.IsPlayerCrimeRatingMild(faction)) return Notoriety.Mild;

         return Notoriety.Clean;
      }

      /// <summary>Whoever holds the criminal trade of this place, when somebody does.</summary>
      private static Hero GangLeaderOf(Settlement place)
      {
         if (place?.Notables == null) return null;

         foreach (Hero notable in place.Notables)
            if (notable is {IsAlive: true} && notable.Occupation == Occupation.GangLeader)
               return notable;

         return null;
      }

      /// <summary>
      ///   What word has brought this character, RAW and uncapped. The cut belongs to RealmNewsPack, because
      ///   how far somebody's word reaches is a fact about them and not a constant.
      ///   <para>
      ///     What this replaces: ResolveWorldRumors asked the store for six and then kept FOUR, for everybody
      ///     alike - a village headman and a marshal of the realm heard exactly as much, from exactly as far.
      ///     The awareness filter underneath is geographic and temporal and never asked who was listening.
      ///   </para>
      /// </summary>
      private static RealmNewsFacts BuildRealmNews(Hero hero)
      {
         if (hero == null) return null;

         try
         {
            var tidings = new List<Tiding>();

            IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> rated =
               WorldEventStore.Instance?.GetKnownEventsRated(hero, (int) CampaignTime.Now.ToDays, 12);

            if (rated != null)
               foreach ((WorldEvent e, RumorConfidence confidence) in rated)
               {
                  // Player deeds go round the influence loop; realm-level war and peace ARE the headline.
                  if (e.InvolvesPlayer || string.IsNullOrWhiteSpace(e.Summary)) continue;
                  if (e.Type == WorldEventType.WarDeclared || e.Type == WorldEventType.PeaceDeclared) continue;

                  tidings.Add(new Tiding {Text = RumorDiscretion.TagFor(e.KnownOnlyToThoseInvolved) + e.Summary, Confidence = MapConfidence(confidence)});
               }

            var facts = new RealmNewsFacts {Headline = ResolveRealmNews(hero), Tidings = tidings};

            return facts.HasAnyReading ? facts : null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[knowledge] realm news read failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>The store's own rating, on the SDK's engine-free scale. Same words, same meaning.</summary>
      private static TidingConfidence MapConfidence(RumorConfidence confidence)
      {
         switch (confidence)
         {
            case RumorConfidence.Distant: return TidingConfidence.Distant;
            case RumorConfidence.Secondhand: return TidingConfidence.Secondhand;
            default: return TidingConfidence.Firsthand;
         }
      }

      /// <summary>
      ///   The post this character holds in the player's own company, from the engine's own reckoning of who
      ///   actually fills each one.
      ///   <para>
      ///     EFFECTIVE rather than assigned on purpose: Bannerlord falls back to whoever is best when no one is
      ///     appointed, and the man doing the job is the man who would know the job. A companion who has never
      ///     been formally named quartermaster but IS the quartermaster should not be told he is nobody.
      ///   </para>
      /// </summary>
      private static PartyPost ReadPartyPost(Hero hero)
      {
         MobileParty party = MobileParty.MainParty;

         if (hero == null || party == null || hero.PartyBelongedTo != party) return PartyPost.None;

         if (party.EffectiveQuartermaster == hero) return PartyPost.Quartermaster;
         if (party.EffectiveScout == hero) return PartyPost.Scout;
         if (party.EffectiveSurgeon == hero) return PartyPost.Surgeon;
         if (party.EffectiveEngineer == hero) return PartyPost.Engineer;

         return PartyPost.None;
      }

      /// <summary>
      ///   How the player's company is faring, for the people riding in it. Every band leans on a reading the
      ///   engine already owns - GetNumDaysForFoodToLast, the 0-100 morale, the roster's own wounded count - so
      ///   no threshold here is one this file invented.
      /// </summary>
      private static PartyStandingFacts BuildPartyStanding(Hero hero)
      {
         MobileParty party = MobileParty.MainParty;

         if (hero == null || party == null || hero.PartyBelongedTo != party) return null;

         try
         {
            var facts = new PartyStandingFacts {
               Provisions = ReadProvisions(party),
               Mood = ReadMood(party),
               Wounded = ReadWounded(party)
            };

            return facts.HasAnyReading ? facts : null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[knowledge] party standing read failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>The engine's own count of days, banded: a quartermaster says "a week or so", not "eleven".</summary>
      private static ProvisionBand ReadProvisions(MobileParty party)
      {
         int days = party.GetNumDaysForFoodToLast();

         if (days <= 0) return ProvisionBand.Starving;
         if (days < 7) return ProvisionBand.Short;
         if (days < 21) return ProvisionBand.Sufficient;

         return ProvisionBand.Plentiful;
      }

      /// <summary>On the engine's own 0-100 morale, where 50 is the settled middle it drifts back towards.</summary>
      private static CampMood ReadMood(MobileParty party)
      {
         if (party.Morale < 25f) return CampMood.Mutinous;
         if (party.Morale < 45f) return CampMood.Sullen;
         if (party.Morale > 70f) return CampMood.HighHearted;

         return CampMood.Steady;
      }

      private static WoundedBand ReadWounded(MobileParty party)
      {
         TroopRoster roster = party.MemberRoster;

         if (roster == null || roster.TotalManCount <= 0) return WoundedBand.Unknown;

         float hurt = (roster.TotalManCount - roster.TotalHealthyCount) / (float) roster.TotalManCount;

         if (hurt > 0.50f) return WoundedBand.Most;
         if (hurt > 0.20f) return WoundedBand.Many;

         return WoundedBand.Few;
      }

      /// <summary>
      ///   The caravans and workshops this character owns. Read ONCE and used both to supply the facts and to
      ///   set the bearer's axis, the rule HoldsASeat and the market both learned the hard way.
      /// </summary>
      private static OwnVenturesFacts BuildOwnVentures(Hero hero)
      {
         if (hero == null) return null;

         try
         {
            var shops = new List<OwnedWorkshop>();

            if (hero.OwnedWorkshops != null)
               foreach (Workshop shop in hero.OwnedWorkshops)
               {
                  string trade = shop?.WorkshopType?.JobName?.ToString() ?? shop?.WorkshopType?.Name?.ToString();
                  if (string.IsNullOrWhiteSpace(trade)) continue;

                  shops.Add(new OwnedWorkshop {
                     Trade = trade.ToLowerInvariant(),
                     PlaceName = shop.Settlement?.Name?.ToString()
                  });
               }

            var facts = new OwnVenturesFacts {
               Caravans = hero.OwnedCaravans?.Count ?? 0,
               Workshops = shops
            };

            return facts.HasAnyReading ? facts : null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[knowledge] ventures read failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   How long this character has been held, in the words a person would use. Null when they are free.
      ///   <para>
      ///     Closes the hole the cut-off rule opened: a captive no longer carries the news packs, which is
      ///     right, but a man whose knowledge was quietly REMOVED does not know it is missing and answers as
      ///     though he still had it. Withholding without saying so causes the very invention it meant to stop.
      ///   </para>
      ///   <para>
      ///     Phrased rather than counted, like every other band: a prisoner knows it has been "months now",
      ///     and a figure would be one the player could check against a save.
      ///   </para>
      /// </summary>
      private static string ReadHeldFor(Hero hero)
      {
         if (hero is not {IsPrisoner: true}) return null;

         try
         {
            var days = (int) (CampaignTime.Now - hero.CaptivityStartTime).ToDays;

            if (days < 0) return "for a while now";
            if (days < 3) return "for a few days";
            if (days < 21) return "for some weeks now";
            if (days < 84) return "for months now";

            return "for more than a year";
         }
         catch
         {
            // A captive whose start date cannot be read is still a captive, and the point of the line is the
            // CUT-OFF rather than the arithmetic.
            return "for a while now";
         }
      }

      /// <summary>
      ///   A hero's STATION facts, and nothing else: what they rule, head, govern, or hold. Returned as an
      ///   EncounterContext because that is what <see cref="NpcMemoryService.Core.Knowledge.StationPack" />
      ///   reads, so the one-clause form the COUNCIL needs and the paragraphs a private word needs are two
      ///   renderings of one set of facts rather than two opinions about what a station is.
      ///
      ///   Exists because the council had none of this at all: a seated governor did not know he governed and
      ///   a seated king did not know he ruled, which is the Derthert/Garios failure StationPack was written
      ///   to end, still live in a whole conversation mode (audit, 15/09/2026).
      /// </summary>
      /// <summary>
      ///   The tales of the hero's OWN people (their culture, not their realm's: a Battanian sworn to Vlandia grew up on
      ///   Battanian tales), with the one they love best first, and those another mod gives this very character
      ///   (CrFolklore, since 2.8.4). Null when neither has any.
      /// </summary>
      internal static FolkloreFacts BuildFolklore(Hero hero)
         => hero == null
            ? null
            : CalradiaRemembers.Logic.Folklore.FolkTalePolicy.Compose(hero.StringId, NarrativeLoader.FolkTales(hero.Culture?.StringId), CrFolklore.TalesFor(hero));

      /// <summary>
      ///   A hero's KNOWLEDGE facts: the bearer that decides what a person like this would know, and every
      ///   fact set the packs read. The companion to <see cref="StationContextFor" /> — that one is what they
      ///   ARE, this is what they KNOW — and shared with the COUNCIL for the same reason: the table had no
      ///   knowledge at all, so a lord who could name his house's fiefs in a private word could name nothing
      ///   at his own council (audit, 15/09/2026).
      ///
      ///   AUDIENCE IS DELIBERATELY LEFT UNSET. Discretion is fail-closed on a null audience, so nothing a
      ///   character holds as Secret is rendered through this context. At a table that is the right default
      ///   and not a limitation: a council is the definition of "in front of others".
      /// </summary>
      public static EncounterContext KnowledgeContextFor(Hero hero, NpcProfile profile,
                                                        LeanPromptLevel lean = LeanPromptLevel.Full)
      {
         SeatStandingFacts seatStanding = BuildSeatStanding(hero);
         MarketWordFacts marketWord = BuildMarketWord(hero);
         OwnVenturesFacts ventures = BuildOwnVentures(hero);
         PartyPost partyPost = ReadPartyPost(hero);
         (NpcMemoryService.Core.Mood.DayMood? Mood, bool Wit, NpcMemoryService.Core.Mood.DayCause Cause) mood = DayMoodReader.DrawDay(hero);

         return new EncounterContext {
            // The interpreter's memory names the player in the third person (Gabriel, 17/10/2026: "he" for a woman).
            PlayerIsFemale = Hero.MainHero?.IsFemale,
            // Carried on the context because the packs budget themselves by it. Build supplies its own level
            // from the player's setting and never reads this one; the COUNCIL asks for Compact explicitly,
            // since a table multiplies the cost by the number of seats.
            LeanLevel = lean,
            Bearer = BuildKnowledgeBearer(hero, seatStanding != null, marketWord != null, partyPost,
                                          ventures != null),
            HouseStanding = BuildHouseStanding(hero),
            SeatStanding = seatStanding,
            HouseMeans = BuildHouseMeans(hero),
            OwnBody = BuildOwnBody(hero),
            MarketWord = marketWord,
            Underworld = BuildUnderworld(hero),
            Folklore = BuildFolklore(hero),
            OwnVentures = ventures,
            HeldFor = ReadHeldFor(hero),
            CaptiveNotoriety = hero is {IsPrisoner: true} ? CalradiaRemembers.Logic.Captivity.CaptiveNotoriety.Describe(StatureReader.Of(hero), hero.IsClanLeader) : null,
            PartyStanding = BuildPartyStanding(hero),
            HereAndNow = BuildHereAndNow(hero),
            PersonalBonds = BuildPersonalBonds(hero, profile),
            RealmNews = BuildRealmNews(hero),
            DayMood = mood.Mood,
            DayMoodCause = mood.Cause,
            WitPermitted = mood.Wit,
            SelfEsteem = Standing.SelfEsteemService.BandFor(hero, profile)
         };
      }

      /// <summary>
      ///   The player's STANDING beside one character, and nothing else. Built for the paths that send a
      ///   prompt without an encounter: a letter is written by somebody who is not standing in front of
      ///   anyone, and it still has to know what the player is to them.
      ///   <para>
      ///     Player report (zhenja1zhenja1, 17/09/2026): a lord he had merely beaten in a tournament wrote to
      ///     him as though he rode with that lord's retinue. The letter path sends EncounterContext.Empty, so
      ///     the standing reached the model as Unknown, which renders nothing, and nothing is what gets
      ///     filled in. See PlayerStandingNote for the other half of the fix.
      ///   </para>
      ///   Fail-soft: a hero the engine cannot find, or a bridge that is not up, yields the empty context the
      ///   letter used to send, never an exception on a background generation.
      /// </summary>
      public static EncounterContext StandingContextFor(NpcProfile profile, int? historyKnownThrough = null)
      {
         try
         {
            IGameStateBridge bridge = NpcMemoryCampaignBehavior.Instance?.GameStateBridge;
            Hero hero = profile?.Id == null
               ? null
               : Hero.FindFirst(h => h != null && h.StringId == profile.Id);

            if (bridge == null || hero == null) return EncounterContext.Empty;

            return new EncounterContext {
               PlayerIsFemale = Hero.MainHero?.IsFemale,
               PlayerStatus = ResolvePlayerStatus(hero, profile, bridge),
               SharedRealmName = bridge.GetSharedRealmName(hero),
               // Letters take the same Task.Run path as a conversation, so a third-party variable provider must
               // be resolved here, on this thread, exactly as in Build (tashmetu, 21/09/2026).
               // A letter is built in full (its LeanLevel is left at the default), so its variables are too.
               ExternalPromptVariables = ResolveExternalPromptVariables(hero, profile, LeanPromptLevel.Full),
               // An envelope written when opened knows only what its sender knew the day he sent it (31/10/2026).
               HistoryKnownThrough = historyKnownThrough,
               HistoryMemoryCap = ModSettings.PromptMemoryLimit > 0 ? ModSettings.PromptMemoryLimit : null
            };
         }
         catch (Exception ex)
         {
            ModLog.Info($"[LETTER] could not read the player's standing ({ex.GetType().Name}); writing without it.");

            return EncounterContext.Empty;
         }
      }

      public static EncounterContext StationContextFor(Hero hero)
         => new() {
            RuledRealmName = ResolveRuledRealmName(hero),
            RuledRealmRulerTitle = ResolveRuledRealmRulerTitle(hero),
            LeadsOwnClan = hero?.Clan?.Leader == hero && hero?.Clan != null,
            SpeakerClanName = hero?.Clan?.Name?.ToString(),
            GovernedSettlementName = hero?.GovernorOf?.Settlement?.Name?.ToString(),
            PartyPostHeld = ResolvePartyPostName(hero),
            RidesAsRetainer = hero?.StringId != null
                              && RetainerBehavior.Instance?.IsRetainer(hero.StringId) == true
         };

      /// <summary>The party post this hero holds in the player's company, lowercased, or null for none.</summary>
      private static string ResolvePartyPostName(Hero hero)
      {
         PartyPost post = ReadPartyPost(hero);

         return post == PartyPost.None ? null : post.ToString().ToLowerInvariant();
      }

      private static string ResolveRuledRealmName(Hero hero)
         => hero?.MapFaction is Kingdom k && k.Leader == hero ? k.Name?.ToString() : null;

      /// <summary>
      ///   That realm's own word for its ruler, from the engine (King, Emperor, Khan, Sultan...). Read rather than
      ///   assumed: the player who reported this wrote "Kings AND Emperors", and one word cannot serve both.
      /// </summary>
      private static string ResolveRuledRealmRulerTitle(Hero hero)
         => hero?.MapFaction is Kingdom k && k.Leader == hero ? k.EncyclopediaRulerTitle?.ToString() : null;

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

         return VassalageOfferPolicy.CanJoin(new VassalageOfferPolicy.VassalageFacts {NpcIsKingdomRuler = npcKingdom.Leader == hero, PlayerAlreadyVassal = playerAlreadyVassal, PlayerAtWarWithKingdom = playerAtWar})
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

         return EscortRefusalFor(hero, alreadyEscorting) == EscortRefusal.None;
      }

      /// <summary>
      ///   Which door is shut on a follow_me escort, or None. Split out so the verb can NAME its refusal rather
      ///   than merely vanish from the prompt: a player reported (2026-09-10) that lords "never follow me despite
      ///   agreeing to", which is what an untaught verb produces every time.
      ///   <para>
      ///     COMMITTED to a war, not merely at war. Until today a lord was refused whenever his realm fought
      ///     anyone at all, and in an ordinary campaign that is nearly always, so the feature was very nearly
      ///     unreachable. An army, a siege or a live battle means his party is genuinely spoken for; a war on the
      ///     far side of Calradia does not.
      ///   </para>
      /// </summary>
      private static EscortRefusal EscortRefusalFor(Hero hero, bool alreadyEscorting)
      {
         if (hero is not {IsLord: true}) return EscortRefusal.NoPartyOfHisOwn;

         return EscortEligibilityPolicy.Refusal(EscortFactsFor(hero, alreadyEscorting));
      }

      /// <summary>The live campaign read behind every escort question, gathered once so no two callers disagree.</summary>
      private static EscortFacts EscortFactsFor(Hero hero, bool alreadyEscorting)
      {
         MobileParty party = hero?.PartyBelongedTo;

         return new EscortFacts {
            LeadsOwnParty = party != null && party.LeaderHero == hero,
            AtWarWithPlayer = hero?.MapFaction != null && Hero.MainHero?.MapFaction != null
                              && FactionManager.IsAtWarAgainstFaction(hero.MapFaction, Hero.MainHero.MapFaction),
            CommittedToWar = party?.Army != null || party?.SiegeEvent != null || party?.MapEvent != null,
            AlreadyEscorting = alreadyEscorting,
            RealmAtWar = FactionIsAtWarWithAnyone(hero?.MapFaction)
         };
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

         return RideWithMeEligibilityPolicy.CanRideWithPlayer(new RideWithMeEligibilityPolicy.RideWithMeFacts {
            IsLord = hero.IsLord,
            IsAlive = hero.IsAlive,
            IsPrisoner = hero.IsPrisoner,
            LeadsOwnParty = leadsParty,
            IsPlayerClan = hero.Clan == Clan.PlayerClan,
            AtWarWithPlayer = atWar,
            AlreadyRiding = alreadyRiding,
            Regard = profile?.ReputationWithPlayer ?? 0,
            RealmAtWar = FactionIsAtWarWithAnyone(hero.MapFaction)
         });
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

      /// <summary>
      ///   The NPC's OWN party troop count (player report: a lord asked about his command could not
      ///   say how many men rode with him). A lord knows his own strength intimately, so unlike the
      ///   player's count this needs no visibility judgement, but the SHAPE of the answer is the
      ///   same judgement (a captive commands no one; a lord who LEADS an army answers for the whole
      ///   army), so this reuses the pure <see cref="PlayerForcePolicy" /> rather than forking it.
      ///   0 when the NPC leads no field party (a governor keeping to a settlement): the prompt
      ///   stays silent rather than calling him troop-poor.
      /// </summary>
      private static int ResolveNpcPartyTroopCount(Hero hero)
      {
         try
         {
            if (hero == null || hero == Hero.MainHero || !hero.IsAlive) return 0;

            MobileParty party = hero.PartyBelongedTo;
            if (party == null) return 0;

            bool leadsArmy = party.Army != null && party.Army.LeaderParty == party;

            return PlayerForcePolicy.Resolve(
               hero.IsPrisoner,
               party.MemberRoster?.TotalManCount ?? 0,
               leadsArmy,
               leadsArmy ? party.Army.Parties.Sum(p => p?.MemberRoster?.TotalManCount ?? 0) : 0);
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
                  return $"- {RumorDiscretion.TagFor(e.KnownOnlyToThoseInvolved)}{e.Summary.TrimEnd('.', ' ')}.";
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
      /// <summary>
      ///   Mystery-lover pillar (phase 3b): the subject label to name in the prompt when THIS NPC is recorded in
      ///   the knowledge ledger as knowing the player is that subject's secret lover (learned only because the
      ///   player confessed it, or because it propagated from one who was told). Adult-gated here, alongside the
      ///   secret-lover eligibility gate. Null when this NPC holds no such secret. The subject is naturally never
      ///   the NPC themselves: the confession verb excludes the listener from the ledger.
      /// </summary>
      private static string ResolveHeldSecretLoverSubject(Hero hero)
      {
         if (ModSettings.AdultContentLevel == AdultContentLevel.Off) return null; // adult-content gate lives here
         if (hero == null || Hero.MainHero == null) return null;

         SecretLoverGossipBehavior gossip = SecretLoverGossipBehavior.Instance;
         if (gossip == null) return null;

         foreach (string subjectId in gossip.PlayerSecretLoverSubjectIds())
         {
            if (!gossip.Knows(hero.StringId, subjectId)) continue;

            Hero subject = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h.StringId == subjectId);
            if (subject == null) continue;

            return HeroLabel.Describe(new HeroLabel.HeroNaming {Name = subject.Name?.ToString(), ClanName = subject.Clan?.Name?.ToString(), CultureName = subject.Culture?.Name?.ToString()});
         }

         return null;
      }

      /// <summary>
      ///   Mystery-lover pillar (phase 3b-3): true when the player has recently paid THIS NPC to hush a secret this
      ///   NPC holds (via <see cref="HushMoneyPolicy.IsRecentlyPaid" />), so the leverage prompt eases off. Adult-
      ///   gated alongside the held-secret resolver. False when no such secret or no recent payment.
      /// </summary>
      private static bool ResolveHeldSecretHushRecentlyPaid(Hero hero)
      {
         if (ModSettings.AdultContentLevel == AdultContentLevel.Off) return false;
         if (hero == null || Hero.MainHero == null) return false;

         SecretLoverGossipBehavior gossip = SecretLoverGossipBehavior.Instance;
         if (gossip == null) return false;

         var today = (int) CampaignTime.Now.ToDays;

         foreach (string subjectId in gossip.PlayerSecretLoverSubjectIds())
            if (gossip.Knows(hero.StringId, subjectId) && HushMoneyPolicy.IsRecentlyPaid(gossip.HushPaidDay(hero.StringId, subjectId), today))
               return true;

         return false;
      }

      private static bool ResolveSecretLoverEligible(Hero hero, NpcProfile profile)
         => SecretBondEligibility.IsEligible(SecretBondFactsFor(hero, profile));

      /// <summary>
      ///   Reads the live campaign into the facts <see cref="SecretBondEligibility" /> judges. Every consumer of
      ///   that decision (this teaching gate, the bridge's take_as_secret_lover guard, the confession policy)
      ///   goes through the ONE rule rather than keeping its own list of conditions, because keeping their own
      ///   is precisely how they drifted: the two gates had quietly stopped agreeing about an ended romance and
      ///   about the adult-content switch, on top of the regard bar a player reported (2026-09-09).
      /// </summary>
      internal static SecretBondFacts SecretBondFactsFor(Hero hero, NpcProfile profile)
         => new() {
            AdultContentEnabled = ModSettings.AdultContentLevel != AdultContentLevel.Off,
            IsPlayerCompanion = hero != null && hero.IsPlayerCompanion,
            HasRomanticProfile = profile?.Romantic != null,
            OrientationCompatible = Hero.MainHero != null && profile?.Romantic?.IsCompatibleWith(Hero.MainHero.IsFemale) == true,
            AlreadyBoundToPlayer = profile?.Romantic?.IsConsort == true
                                   || profile?.Romantic?.IsSecretLover == true
                                   || (hero != null && hero == Hero.MainHero?.Spouse),
            Status = profile?.Romantic?.Status ?? RomanticStatus.None,
            Regard = profile?.ReputationWithPlayer ?? 0,
            Warmth = ModSettings.RelationWarmth
         };

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

         StanceConsequence verdict = StanceConsequencePolicy.Resolve(StanceService.ToStance(profile), new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = TraitLevel(hero, DefaultTraits.Honor), Valor = TraitLevel(hero, DefaultTraits.Valor), Mercy = TraitLevel(hero, DefaultTraits.Mercy), Calculating = TraitLevel(hero, DefaultTraits.Calculating)});

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
      /// <summary>The station the behaviour guidelines follow (StationGuidelinesPolicy), or null for the lordly register.</summary>
      private static string StationKeyOf(Hero hero)
      {
         if (hero == null) return null;

         bool isPlayerCompanion = hero.CompanionOf == Clan.PlayerClan;
         bool holdsFief = hero.IsClanLeader && (hero.Clan?.Settlements?.Any(s => s != null && (s.IsTown || s.IsCastle)) ?? false);

         return StationGuidelinesPolicy.ResolveStationKey(hero.Occupation.ToString(), isPlayerCompanion, holdsFief);
      }

      internal static string ResolveStationGuidelines(Hero hero)
      {
         if (hero == null) return null;

         // The wanderer, companion, lord ladder: a recruited companion keeps the raw "Wanderer" occupation,
         // so a sworn shieldmate at the player's side would otherwise be voiced as a loose tavern free blade.
         // The real standing is read from who they serve and whether they are landed, not occupation alone.
         // (In CR a fief is granted across houses only, so a player companion can never hold one WHILE a
         // companion: the fief rung fires once they are ennobled into their own landed clan, which the base
         // register already fits. It is kept explicit here so the ladder is complete and future-proof.)
         bool isPlayerCompanion = hero.CompanionOf == Clan.PlayerClan;
         bool holdsFief = hero.IsClanLeader && (hero.Clan?.Settlements?.Any(s => s != null && (s.IsTown || s.IsCastle)) ?? false);
         string key = StationGuidelinesPolicy.ResolveStationKey(hero.Occupation.ToString(), isPlayerCompanion, holdsFief);
         // A notable's own trade file first, when a player or a mod wrote one (Windmolen, 19/10/2026).
         string trade = key == StationGuidelinesPolicy.NotableKey ? StationGuidelinesPolicy.TradeKeyOf(hero.Occupation.ToString()) : null;
         string text = StationGuidelinesPolicy.SelectStationText(key, trade, NarrativeLoader.StationGuidelines);

         // A lord resolves to no station file and used to fall through to the base register, which is
         // injected session-wide (LlmServices -> BehaviorGuidelinesOverride) and therefore cannot know who is
         // speaking. Resolving it HERE instead, where the hero is in hand, is what lets the FACTION BEHAVIOR
         // section be narrowed to his own culture. PromptBuilder prefers this override over the session one,
         // so a lord receives the same text he always did, minus the seven cultures he is not.
         if (string.IsNullOrWhiteSpace(text)) text = NarrativeLoader.BehaviorGuidelines;

         // The realm only when it is the character's own people's (tashmetu, 27/09/2026): a companion's realm is
         // the player's and a notable's is whoever holds the town, and neither may pull in or retitle a block.
         return FactionGuidelinesPolicy.NarrowToOwnCulture(
            text, hero.Culture?.Name?.ToString(),
            FactionGuidelinesPolicy.RealmOfOwnPeople(hero.MapFaction?.Name?.ToString(), hero.MapFaction?.Culture?.StringId, hero.Culture?.StringId));
      }

      /// <summary>
      ///   A SECOND, message-relevant world-fact block: known facts this NPC would plausibly know that bear
      ///   specifically on what the player just said, deterministic (no LLM call anywhere in this path).
      ///   Fixes a modder report: awareness alone (the ambient news pack's small top-N by
      ///   magnitude/faction/distance/recency) starves a fact the NPC DOES know but that never ranked high
      ///   enough to make the ambient cut, so a pointed question about it got a hallucinated answer instead
      ///   of the real one. Order is mandatory and never reversed: the KNOWLEDGE gate
      ///   (<see cref="WorldEventStore.GetKnownEventsRated" />) runs FIRST over a generous candidate pool, THEN
      ///   <see cref="WorldFactRelevancePolicy" /> ranks only the survivors against the message, so a fact the
      ///   NPC would not plausibly know can never surface merely because the player asked about it. Best-effort
      ///   de-dup against the ambient realm-news selection and
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
                  return $"- {RumorDiscretion.TagFor(s.Event.KnownOnlyToThoseInvolved)}{tag}{s.Event.Summary.TrimEnd('.', ' ')}.";
               })
               .ToList();

            return lines.Count > 0
               ? string.Join("\n", lines)
               : null;
         }
         catch { return null; }
      }

      /// <summary>
      ///   The long-term past the player just named (Gabriel, 17/10/2026: the long-term memory serves "des conversations
      ///   entre joueur et NPC sur le passe"; docs/design/AUTHORED_HISTORY.md section 8): the events of the world's
      ///   long-term memory, authored history included, that name a person, a place or a realm the player named and that
      ///   the speaker would know. PastOnMentionPolicy picks at most three and says how long ago, and whether he lived it
      ///   or knows it only as told. Only a name brings up the past, never a keyword: "the battle yesterday" must not raise
      ///   a battle of ten years ago. The dead are names here too, since history is full of them. Null when nothing named
      ///   is in his past.
      /// </summary>
      internal static string ResolvePastOnMention(Hero hero, string playerMessage, int today)
      {
         if (hero == null || string.IsNullOrWhiteSpace(playerMessage) || WorldEventStore.Instance == null) return null;

         try
         {
            HashSet<string> keywords = WorldFactRelevancePolicy.Tokenize(playerMessage);
            HashSet<string> named = ExtractMessageEntityIds(keywords);
            AddNamedDeadAndPlaces(keywords, named);
            named.Remove(hero.StringId); // "you" said by name is not a question about his past

            if (named.Count == 0) return null;

            var request = new PastMentionRequest {
               NamedIds = named, Today = today, DaysInYear = CampaignTime.DaysInSeason * CampaignTime.SeasonsInYear,
               SpeakerBirthDay = (int) hero.BirthDay.ToDays
            };

            IEnumerable<PastCandidateFacts> known = WorldEventStore.Instance.KnownPastNaming(hero, named)
               .Select(t => new PastCandidateFacts {
                  Id = t.Event.Id, EntityIds = WorldEventStore.NamedIdsOf(t.Event), Summary = t.Event.Summary, Day = t.Event.Day,
                  Magnitude = t.Event.Magnitude, Tie = t.Tie, Secret = t.Event.KnownOnlyToThoseInvolved
               });

            return PastOnMentionPolicy.Block(known, request);
         }
         catch { return null; }
      }

      // The past names the dead (an executed father, a lord fallen at a siege) and places: added to the names the
      // recent-news scan already found among the living, kingdoms and clans. Hideouts are no one's past. Only a
      // distinctive part of a name counts ("Castle", "Bog" or an epithet names no one, PastOnMentionPolicy.IdsNamedIn).
      private static void AddNamedDeadAndPlaces(HashSet<string> messageKeywords, HashSet<string> ids)
      {
         if (messageKeywords == null || messageKeywords.Count == 0) return;

         var folded = new HashSet<string>(messageKeywords.Select(NameNormalization.Fold));

         try
         {
            List<KeyValuePair<string, string>> dead = Hero.DeadOrDisabledHeroes.Where(h => h != null && !h.IsHumanPlayerCharacter)
                                                          .Select(h => new KeyValuePair<string, string>(h.StringId, h.Name?.ToString())).ToList();
            List<KeyValuePair<string, string>> places = Settlement.All.Where(s => s != null && !s.IsHideout)
                                                                  .Select(s => new KeyValuePair<string, string>(s.StringId, s.Name?.ToString())).ToList();

            ids.UnionWith(PastOnMentionPolicy.IdsNamedIn(dead, folded, NameNormalization.Fold, WorldFactRelevancePolicy.MinKeywordLength));
            ids.UnionWith(PastOnMentionPolicy.IdsNamedIn(places, folded, NameNormalization.Fold, WorldFactRelevancePolicy.MinKeywordLength));
         }
         catch { /* best effort: the living, the realms and the clans still count */ }
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