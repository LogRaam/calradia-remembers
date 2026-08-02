// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Appearance;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Duels;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Social;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Styles;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Nemesis;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Social;
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
         bool isCouncilNarratorTurn = false,
         string addressedCouncilMemberName = null,
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

         return new() {
            PlayerStatus = playerStatus,
            SharedRealmName = bridge.GetSharedRealmName(hero),
            WarStatus = bridge.GetDiplomaticStatus(hero),
            DaysSinceLastMeeting = daysSinceLastMeeting,
            MeetingGapNote = PresenceDescriber.ShowsElapsedGap(presence)
               ? MeetingGapDescriber.Describe(daysSinceLastMeeting)
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
            IsRoundTableTurn = isRoundTableTurn,
            // Council narrator model (2026-08-02): a real, player-led council's own flag, ALONGSIDE
            // IsRoundTableTurn above (kept true so every other council rule sharing that block still applies).
            // See EncounterContext.IsCouncilNarratorTurn for the full rationale.
            IsCouncilNarratorTurn = isCouncilNarratorTurn,
            AddressedCouncilMemberName = addressedCouncilMemberName,
            // This turn's pre-drawn verdict for a captive player's physical attempt: the mod decides, the model
            // narrates (an LLM judging the struggle it is also writing always sides with its own story).
            PlayerAttemptSucceeds = playerAttemptSucceeds,
            IsCollectiveCaptiveScene = isCollectiveCaptiveScene,
            IsCaptorScene = isCaptorScene,
            CaptiveFearCoerced = ResolveCaptiveFearCoerced(hero, profile, playerStatus),
            CaptorAllowsPersuasionRelease = ResolveCaptorReleaseVerdict(hero, playerStatus).AllowsPersuasion,
            CaptorAllowsRansomRelease = ResolveCaptorReleaseVerdict(hero, playerStatus).AllowsRansom,
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
            HeldLordPrisoners = ResolveHeldLordPrisoners(),
            NpcIsPrisonerOfAnother = npcIsPrisonerOfAnother,
            NpcCaptorName = npcIsPrisonerOfAnother
               ? hero.PartyBelongedToAsPrisoner?.MapFaction?.Name?.ToString()
               : null,
            MercenaryOfferKingdom = ResolveMercenaryOfferKingdom(hero),
            LordRecruitEligible = ResolveLordRecruitEligible(hero, profile),
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
            StationGuidelinesOverride = ResolveStationGuidelines(hero),
            LoveMatchEligible = ResolveLoveMatchEligible(hero, profile),
            MarriageBlockedBecause = ResolveMarriageBlockReason(hero, profile),
            LoveMatchReluctant = ResolveLoveMatchReluctant(hero, profile),
            LoveMatchBlessed = ResolveLoveMatchBlessed(hero),
            NpcSpouseIsPlayer = hero?.Spouse == Hero.MainHero && Hero.MainHero != null,
            SpouseMarriedUnwillingly = ResolveSpouseMarriedUnwillingly(hero),
            ConsortEligible = ResolveConsortEligible(hero, profile),
            SecretLoverEligible = ResolveSecretLoverEligible(hero, profile),
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
            CanGatherNews = ResolveCanGatherNews(hero),
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
         if (hero.Clan == Clan.PlayerClan) return MarriageBlockReason.SameClan;
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
         return ResolveLoveMatchBlessed(hero) ? MarriageBlockReason.None : MarriageBlockReason.RegardTooLow;
      }

      /// <summary>The two-sided regard read every marriage gate shares: the mod's personal ledger OR the live vanilla relation reaching the bar.</summary>
      private static bool RegardMeetsMarriageBar(Hero hero, NpcProfile profile)
         => (profile?.ReputationWithPlayer ?? 0) >= MarriageEligibility.MarriageEligibilityThreshold ||
            (Hero.MainHero?.GetRelation(hero) ?? 0) >= MarriageEligibility.MarriageEligibilityThreshold;

      /// <summary>
      ///   True when marriage is on offer ONLY through the family's blessing: the match is otherwise clear but
      ///   the NPC's own regard is still below the bar. Tells the prompt this would be a POLITICAL union, so
      ///   the NPC may accept out of duty, coldly, and never has to pretend a love that is not there.
      /// </summary>
      private static bool ResolveLoveMatchReluctant(Hero hero, NpcProfile profile)
         => ResolveMarriageBlockReason(hero, profile) == MarriageBlockReason.None
            && !RegardMeetsMarriageBar(hero, profile);

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

         // Player must not already be in any kingdom (vassal or mercenary).
         if (Clan.PlayerClan?.Kingdom != null) return null;

         return npcKingdom.Name?.ToString();
      }

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