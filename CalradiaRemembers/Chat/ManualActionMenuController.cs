// Code written by Gabriel Mailhot, 21/08/2026.
// Manual action-injection menu (the third icon, beside the purse and the quill): lets the player EMIT AN
// ACTION BY HAND against the current conversation NPC, for when the Action Interpreter missed a deed or is
// turned off entirely. The player picks from the actions POSSIBLE RIGHT NOW; the pick is injected through
// the SAME bridge an LLM-proposed action uses (deterministic, no LLM call), preserving the earned-regard
// integrity: NPC-JUDGMENT actions (change_relation, sway_opinion, give_gold...) never appear here at all
// (ManualActionCatalog's own header names the boundary), so a manual click can never grant the player regard
// they have not earned in conversation.
//
// Gate reuse: every fact this resolves is read from the SAME pure policies the Action Interpreter's own 1:1
// teaching gates call (GovernorAppointmentPolicy, PartyCommandOfferPolicy, FiefTransferPolicy, StipendPolicy,
// PeaceMediationPolicy, PrisonerRecruitmentPolicy, NotableRecruitmentPolicy, EscortEligibilityPolicy,
// RideWithMeEligibilityPolicy, RecruitmentPricing, CourtActionResolver via GiveTroopsVerb/SpendInfluenceVerb),
// never re-derived. EncounterContextBuilder.Build itself is deliberately NOT called here: several of its
// fields are one-shot and CONSUMED ON READ for the next real LLM turn (InterceptionReason,
// LlmFormatFeedbackNote, CompanionNewsReport): calling it just to peek at a boolean gate would silently
// swallow a directive meant for the conversation's next real turn.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Escort;
using CalradiaRemembers.Duels;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Items;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Actions;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Escort;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Retainer;
using CalradiaRemembers.Verbs;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Owns the manual action-injection menu so it does not thread through the shared ChatViewModel partials,
   ///   mirroring <see cref="OfferController" /> and <see cref="ActionConfirmationController" />. Resolves which
   ///   actions are possible right now (<see cref="ManualActionAvailability" />), shows the menu, raises whatever
   ///   follow-up the pick needs (an amount, an item, a fief, a role, a mission, a realm), then injects the built
   ///   action through the confirmation controller (for the three player-spend/commit types) or straight through
   ///   the bridge, exactly mirroring <see cref="ActionConfirmationController.ApplyConfirmedAction" />'s pattern.
   /// </summary>
   internal sealed class ManualActionMenuController
   {
      private static readonly string[] MissionKinds = {"gathernews", "spy", "steal", "barter", "envoy"};
      private static readonly string[] MissionLabels = {"Gather news", "Spy", "Steal", "Barter", "Envoy"};
      private static readonly string[] PartyRoles = {"scout", "engineer", "quartermaster", "surgeon"};
      private static readonly string[] PartyRoleLabels = {"Scout", "Engineer", "Quartermaster", "Surgeon"};

      /// <summary>Opens the menu of actions possible right now against the conversation NPC.</summary>
      public void Execute(IManualActionMenuHost host, ActionConfirmationController confirmation)
      {
         if (host == null || confirmation == null || !host.CanOpenActionMenu) return;

         try
         {
            Hero npc = host.Npc;
            IGameStateBridge bridge = NpcMemoryCampaignBehavior.Instance?.GameStateBridge;

            if (npc == null || bridge == null) return;

            NpcProfile profile = host.Profile;
            ManualActionFacts facts = ResolveFacts(npc, profile, bridge);
            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(facts);

            if (available.Count == 0)
            {
               InformationManager.ShowInquiry(new InquiryData(
                  "Actions", $"There is nothing you can do to {npc.Name} right now.",
                  true, false, "OK", null, null, null), true);

               return;
            }

            List<InquiryElement> elements = available
               .Select(e => new InquiryElement(e, $"[{GroupLabel(e.Group)}] {e.DisplayLabel}", null))
               .ToList();

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
               "Actions",
               $"Choose an action to take with {npc.Name}:",
               elements, true, 1, 1, "Choose", "Cancel",
               picked =>
               {
                  if (picked?.FirstOrDefault()?.Identifier is not ManualActionEntry entry) return;
                  BeginParameterCapture(host, confirmation, entry, npc, profile, facts);
               },
               null));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION_MENU] open failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Raises whatever follow-up the picked entry's parameter kind needs, then injects the built action.</summary>
      private void BeginParameterCapture(IManualActionMenuHost host,
                                          ActionConfirmationController confirmation,
                                          ManualActionEntry entry,
                                          Hero npc,
                                          NpcProfile profile,
                                          ManualActionFacts facts)
      {
         switch (entry.ParameterKind)
         {
            case ManualActionParameterKind.None:
               Inject(host, confirmation, entry, new GameAction {Type = entry.Type}, new ActionContext(profile, null));

               return;

            case ManualActionParameterKind.Amount:
               PromptAmount(host, confirmation, entry, npc, profile, facts);

               return;

            case ManualActionParameterKind.ItemPick:
               ItemPickerService.ShowItemPicker(
                  (itemId, name, value) =>
                  {
                     var context = new ActionContext(profile, null) {OfferedItemId = itemId};
                     Inject(host, confirmation, entry, new GameAction {Type = entry.Type}, context);
                  });

               return;

            case ManualActionParameterKind.FiefPick:
               ShowPickList(host, confirmation, entry, profile, FiefsFor(entry.Type, facts), "Which fief?");

               return;

            case ManualActionParameterKind.RolePick:
               ShowPickList(host, confirmation, entry, profile, PartyRoles, PartyRoleLabels, "Which role?");

               return;

            case ManualActionParameterKind.MissionPick:
               ShowPickList(host, confirmation, entry, profile, MissionKinds, MissionLabels, "Which errand?");

               return;

            case ManualActionParameterKind.FactionPick:
               ShowPickList(host, confirmation, entry, profile, facts.MediatablePeaceFactions, "Broker peace with which realm?");

               return;
         }
      }

      /// <summary>Asks for a whole-number amount (denars, a stipend, a hiring price), then injects.</summary>
      private void PromptAmount(IManualActionMenuHost host,
                                 ActionConfirmationController confirmation,
                                 ManualActionEntry entry,
                                 Hero npc,
                                 NpcProfile profile,
                                 ManualActionFacts facts)
      {
         string message;
         if (entry.Type == "take_gold")
            message = $"How many denars do you give {npc.Name}? (You carry {Hero.MainHero?.Gold ?? 0:N0}.)";
         else if (entry.Type == "join_party")
            message = $"Agreed hiring price? (blank = the asking price of {facts.CompanionAskingPrice ?? 0:N0} denars)";
         else if (entry.Type == "grant_stipend")
            message = "Daily wage in denars (clamped to the policy's range)?";
         else
            message = "Amount?";

         InformationManager.ShowTextInquiry(new TextInquiryData(
            entry.DisplayLabel, message, true, true, "OK", "Cancel",
            text =>
            {
               string trimmed = text?.Trim() ?? "";
               bool parsed = int.TryParse(trimmed, out int amount);

               // join_party alone treats a blank entry as "use the vanilla asking price" (the bridge itself
               // defaults a non-positive proposed price to it); every other amount needs a real positive number,
               // or there is nothing to give/fund and the pick is quietly abandoned (fail soft).
               if (!parsed)
               {
                  if (entry.Type == "join_party" && trimmed.Length == 0) amount = 0;
                  else return;
               }

               if (amount < 0) return;
               if (amount == 0 && entry.Type != "join_party") return;

               var action = new GameAction {
                  Type = entry.Type,
                  Parameters = new Dictionary<string, string> {[entry.ParameterName] = amount.ToString()}
               };
               Inject(host, confirmation, entry, action, new ActionContext(profile, null));
            },
            null));
      }

      /// <summary>Shows a single-select pick list (fief/role/mission/faction name to plain value) and injects on the choice.</summary>
      private void ShowPickList(IManualActionMenuHost host,
                                 ActionConfirmationController confirmation,
                                 ManualActionEntry entry,
                                 NpcProfile profile,
                                 IReadOnlyList<string> values,
                                 string prompt)
         => ShowPickList(host, confirmation, entry, profile, values, values, prompt);

      /// <summary>Same as above, with a separate display label per value (the fixed role/mission lists).</summary>
      private void ShowPickList(IManualActionMenuHost host,
                                 ActionConfirmationController confirmation,
                                 ManualActionEntry entry,
                                 NpcProfile profile,
                                 IReadOnlyList<string> values,
                                 IReadOnlyList<string> labels,
                                 string prompt)
      {
         if (values == null || values.Count == 0)
         {
            InformationManager.ShowInquiry(new InquiryData(
               entry.DisplayLabel, "There is nothing eligible to name right now.", true, false, "OK", null, null, null), true);

            return;
         }

         var elements = new List<InquiryElement>();
         for (var i = 0; i < values.Count; i++)
            elements.Add(new InquiryElement(values[i], i < labels.Count ? labels[i] : values[i], null));

         MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            entry.DisplayLabel, prompt, elements, true, 1, 1, "Choose", "Cancel",
            picked =>
            {
               if (picked?.FirstOrDefault()?.Identifier is not string value) return;

               var action = new GameAction {
                  Type = entry.Type,
                  Parameters = new Dictionary<string, string> {[entry.ParameterName] = value}
               };
               Inject(host, confirmation, entry, action, new ActionContext(profile, null));
            },
            null));
      }

      /// <summary>The fief list grounding this fief-picking entry's target_fief: grant/appoint from the player's own crown, revoke from the NPC's.</summary>
      private static IReadOnlyList<string> FiefsFor(string type, ManualActionFacts facts)
      {
         if (string.Equals(type, "revoke_fief", StringComparison.OrdinalIgnoreCase)) return facts.RevocableFiefs;
         if (string.Equals(type, "appoint_governor", StringComparison.OrdinalIgnoreCase)) return facts.AppointableFiefs;

         return facts.GrantableFiefs; // grant_fief
      }

      /// <summary>
      ///   Routes the built action to the SAME confirmation controller an LLM-proposed action would use (for
      ///   take_gold/join_party/free_prisoner), or injects it straight into the bridge otherwise: the identical
      ///   pattern <see cref="ActionConfirmationController.ApplyConfirmedAction" /> already applies, so there is
      ///   no second execution path to keep in sync with the bridge's own re-validation.
      /// </summary>
      private void Inject(IManualActionMenuHost host,
                           ActionConfirmationController confirmation,
                           ManualActionEntry entry,
                           GameAction action,
                           ActionContext context)
      {
         try
         {
            if (entry.RequiresConfirmation)
            {
               ActionDisposition disposition = confirmation.Preprocess(action, host.Npc, host.Profile, context);

               if (disposition == ActionDisposition.Queued)
               {
                  confirmation.PromptNext(host);

                  return;
               }

               if (disposition == ActionDisposition.Skip)
               {
                  host.AppendOoc("» Already settled this conversation.");

                  return;
               }

               // ActionDisposition.ExecuteInline: ActionConfirmationPolicy disagreed that this type needs
               // confirming (should not happen for the three RequiresConfirmation entries, but fail soft
               // by injecting directly rather than dropping the player's pick silently).
            }

            // A duel is not a world mutation the bridge executes, it is chat flow control: it needs the room's
            // witnesses and the live transcript, and it ends the conversation. So it goes to the host, which
            // hands it to the SAME acceptance and arming road an LLM-emitted challenge takes.
            if (string.Equals(entry?.Type, ManualActionCatalog.ChallengeDuel.Type, StringComparison.OrdinalIgnoreCase))
            {
               host.ChallengeToDuel();

               return;
            }

            InjectDirect(host, action, context);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION_MENU] inject failed ({entry?.Type}): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Calls the bridge directly and reports the outcome, mirroring
      ///   <see cref="ActionConfirmationController.ApplyConfirmedAction" />'s report half exactly: a system
      ///   message line for the player, and, when the bridge refused, an out-of-character transcript note so
      ///   the model never treats a refused deed as done.
      /// </summary>
      private void InjectDirect(IManualActionMenuHost host, GameAction action, ActionContext context)
      {
         ActionOutcome outcome = NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.ExecuteAction(action, host.Npc, context);

         if (outcome == null) return;

         host.AppendSystemMessageLine(outcome.Message, outcome.IsPositive);

         string refusalNote = ActionRefusalNote.For(outcome.WasRefused, outcome.Message);
         if (refusalNote != null)
         {
            try { host.RecordTranscriptLine(true, "You", Hero.MainHero?.StringId, refusalNote); }
            catch (Exception ex) { ModLog.Error($"[ACTION_MENU] refusal note failed: {ex.GetType().Name}: {ex.Message}"); }
         }
      }

      private static string GroupLabel(ManualActionGroup group)
      {
         switch (group)
         {
            case ManualActionGroup.CoinAndGoods: return "Coin & Goods";
            case ManualActionGroup.Prisoner: return "Prisoner";
            case ManualActionGroup.Companion: return "Companion";
            case ManualActionGroup.Governance: return "Governance";
            case ManualActionGroup.Diplomacy: return "Diplomacy";
            default: return "Other";
         }
      }

      // ── Fact resolution: reuses the SAME policies EncounterContextBuilder's own 1:1 teaching gates call, ──
      // ── never re-deriving a gate. EncounterContextBuilder.Build itself is deliberately not called (see the ──
      // ── file header): several of its fields are one-shot and consumed on read for the next real LLM turn. ──

      private static ManualActionFacts ResolveFacts(Hero npc, NpcProfile profile, IGameStateBridge bridge)
      {
         try
         {
            int regard = profile?.ReputationWithPlayer ?? 0;

            bool npcIsPlayerPrisoner = npc.IsPrisoner && npc.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;
            bool heldByPlayer = BannerlordGameStateBridge.IsHeldByPlayer(npc);
            bool canRecruitPrisoner = heldByPlayer && PrisonerRecruitmentPolicy.CanRecruit(true, npc.Clan?.Leader == npc, regard);

            bool npcIsPlayerCompanion = npc.IsPlayerCompanion;
            bool npcInMainParty = npc.PartyBelongedTo == MobileParty.MainParty;
            bool npcAlive = npc.IsAlive;
            bool npcIsPrisoner = npc.IsPrisoner;
            bool npcLeadsOwnCompany = npc.PartyBelongedTo?.LeaderHero == npc;
            bool npcAvailableForErrand = CompanionMissionBehavior.Instance?.IsAway(npc.StringId) != true;
            bool npcCanRideForPlayer = npcIsPlayerCompanion || npc.Clan == Clan.PlayerClan;

            MobileParty party = npc.PartyBelongedTo;
            bool leadsParty = party != null && party.LeaderHero == npc;
            bool atWar = npc.MapFaction != null && Hero.MainHero?.MapFaction != null
                        && FactionManager.IsAtWarAgainstFaction(npc.MapFaction, Hero.MainHero.MapFaction);
            bool inArmy = party?.Army != null || party?.SiegeEvent != null;

            bool factionAtWar = FactionIsAtWarWithAnyone(npc.MapFaction);

            bool npcIsEscortingPlayer = FollowMeBehavior.Instance?.IsEscorting(npc.StringId) == true;
            // committedToWar (an army, a siege or a live battle) replaced the blanket "his realm is at war"
            // on 2026-09-10; see EscortEligibilityPolicy for the player report that forced it.
            bool npcCanEscortPlayer = npc.IsLord && EscortEligibilityPolicy.CanEscort(new EscortFacts {
               LeadsOwnParty = leadsParty,
               AtWarWithPlayer = atWar,
               CommittedToWar = inArmy,
               AlreadyEscorting = npcIsEscortingPlayer
            });

            bool npcIsRidingWithPlayer = RetainerBehavior.Enabled && RetainerBehavior.Instance?.IsRetainer(npc.StringId) == true;
            bool npcCanRideWithPlayer = RetainerBehavior.Enabled && RideWithMeEligibilityPolicy.CanRideWithPlayer(
               isLord: npc.IsLord, isAlive: npc.IsAlive, isPrisoner: npc.IsPrisoner, leadsParty: leadsParty,
               isPlayerClan: npc.Clan == Clan.PlayerClan, atWar: atWar, alreadyRiding: npcIsRidingWithPlayer, regard: regard,
               factionAtWar: factionAtWar);

            bool playerIsKingdomLeader = Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom.Leader == Hero.MainHero;
            bool npcIsVassalInPlayerKingdom = npc.Clan != null && npc.Clan != Clan.PlayerClan
                                              && npc.Clan.Kingdom != null && npc.Clan.Kingdom == Clan.PlayerClan?.Kingdom;

            List<string> grantableFiefs = CrownFiefsOf(Clan.PlayerClan);
            List<string> revocableFiefs = npc.Clan != null && npc.Clan != Clan.PlayerClan
               ? CrownFiefsOf(npc.Clan)
               : new List<string>();
            List<string> appointableFiefs = VacantCrownFiefsOf(Clan.PlayerClan);

            bool npcCanBeGrantedFief = FiefTransferPolicy.CanOfferGrant(playerIsKingdomLeader, npcIsVassalInPlayerKingdom, grantableFiefs.Count > 0);
            bool npcCanHaveFiefRevoked = FiefTransferPolicy.CanOfferRevoke(playerIsKingdomLeader, npcIsVassalInPlayerKingdom, revocableFiefs.Count > 0);
            bool npcCanBeAppointedGovernor = GovernorAppointmentPolicy.CanOfferAppointment(
               playerLeadsClan: Clan.PlayerClan?.Leader == Hero.MainHero,
               appointeeInPlayerClan: npc.Clan != null && npc.Clan == Clan.PlayerClan,
               appointeeAvailable: npc.IsAlive && !npc.IsPrisoner && npc != Hero.MainHero,
               hasVacantGovernableFief: appointableFiefs.Count > 0,
               appointeeNotGoverningElsewhere: npc.GovernorOf == null,
               appointeeNotAway: npcAvailableForErrand);

            bool npcHasActiveStipend = CouncilStipendBehavior.Instance?.HasActive(npc.StringId) == true;
            bool playerCanAffordMinTranche = Hero.MainHero != null && Hero.MainHero.Gold >= StipendPolicy.MinTrancheCost;
            bool npcCanBeGrantedStipend = StipendPolicy.CanOfferStipend(npcIsPlayerCompanion, npcHasActiveStipend, playerCanAffordMinTranche);

            var verbCtx = new VerbContext(npc, profile, bridge, null, null);
            bool canGiveTroops = new GiveTroopsVerb().IsEligible(verbCtx);
            bool canSpendInfluence = new SpendInfluenceVerb().IsEligible(verbCtx);

            bool npcIsFactionLeader = npc.IsFactionLeader;
            List<string> mediatableFactions = npcIsFactionLeader ? MediatableFactionsOf(npc) : new List<string>();
            int playerRegardWithLeader = profile?.ReputationWithPlayer ?? npc.GetRelation(Hero.MainHero);
            int playerStatureTier = Clan.PlayerClan?.Tier ?? 0;
            bool playerCanMediatePeace = PeaceMediationPolicy.CanMediate(
               npcIsFactionLeader, mediatableFactions.Count > 0, playerRegardWithLeader, playerStatureTier);

            bool hasNotableSuccessor = npc.IsNotable && BannerlordGameStateBridge.FindNotableSuccessor(npc) != null;
            bool canRecruitNotable = npc.IsNotable && NotableRecruitmentPolicy.CanRecruit(
               npc.IsNotable, npc.IsAlive, npc.IsPrisoner, npc.Clan == Clan.PlayerClan, regard, hasNotableSuccessor);

            int itemCount = MobileParty.MainParty?.ItemRoster?.Count ?? 0;

            return new ManualActionFacts {
               NpcIsPlayerPrisoner = npcIsPlayerPrisoner,
               CanRecruitPrisoner = canRecruitPrisoner,
               NpcIsPlayerCompanion = npcIsPlayerCompanion,
               NpcIsAwayOnErrand = !npcAvailableForErrand,
               CompanionIsDiscontented = IsDiscontented(npc),
               NpcCanBeDispatchedOnMission = PartyCommandOfferPolicy.CanOfferDispatchMission(npcCanRideForPlayer, npcInMainParty, npcAvailableForErrand),
               NpcCanBeAssignedPartyRole = PartyCommandOfferPolicy.CanOfferAssignRole(npcIsPlayerCompanion, npcInMainParty),
               NpcCanRejoinParty = PartyCommandOfferPolicy.CanOfferRejoinParty(npcIsPlayerCompanion, npcAlive, npcIsPrisoner, npcInMainParty, npcLeadsOwnCompany),
               NpcCanEscortPlayer = npcCanEscortPlayer,
               NpcIsEscortingPlayer = npcIsEscortingPlayer,
               NpcCanRideWithPlayer = npcCanRideWithPlayer,
               NpcIsRidingWithPlayer = npcIsRidingWithPlayer,
               NpcCanBeChallengedToDuel = DuelEligibilityResolver.CanDuelNow(npc, profile, out _),
               NpcCanBeGrantedFief = npcCanBeGrantedFief,
               GrantableFiefs = grantableFiefs,
               NpcCanHaveFiefRevoked = npcCanHaveFiefRevoked,
               RevocableFiefs = revocableFiefs,
               NpcCanBeAppointedGovernor = npcCanBeAppointedGovernor,
               AppointableFiefs = appointableFiefs,
               NpcCanBeGrantedStipend = npcCanBeGrantedStipend,
               CanGiveTroops = canGiveTroops,
               CanSpendInfluence = canSpendInfluence,
               PlayerCanMediatePeace = playerCanMediatePeace,
               MediatablePeaceFactions = mediatableFactions,
               CanRecruitNotable = canRecruitNotable,
               CompanionAskingPrice = RecruitmentPricing.GetHiringPrice(npc),
               PlayerHasGiveableItems = itemCount > 0
            };
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION_MENU] fact resolution failed: {ex.GetType().Name}: {ex.Message}");

            return new ManualActionFacts();
         }
      }

      /// <summary>True when a companion's happiness reads Restless or Disgruntled, a live grievance worth hearing out.</summary>
      private static bool IsDiscontented(Hero npc)
      {
         HappinessBand band = CompanionHappiness.Band(CompanionHappinessService.Get(npc));

         return band == HappinessBand.Restless || band == HappinessBand.Disgruntled;
      }

      /// <summary>Every town/castle this clan owns: the crown-fief grounding both grant_fief (player's own) and revoke_fief (the vassal's own).</summary>
      private static List<string> CrownFiefsOf(Clan clan)
      {
         if (clan?.Settlements == null) return new List<string>();

         return clan.Settlements
            .Where(s => s.IsTown || s.IsCastle)
            .Select(s => s.Name?.ToString())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
      }

      /// <summary>This clan's towns/castles with no seated governor: the appoint_governor grounding.</summary>
      private static List<string> VacantCrownFiefsOf(Clan clan)
      {
         if (clan?.Settlements == null) return new List<string>();

         return clan.Settlements
            .Where(s => (s.IsTown || s.IsCastle) && s.Town?.Governor == null)
            .Select(s => s.Name?.ToString())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
      }

      /// <summary>The enemy realms this faction leader's own faction is at war with: the mediate_peace grounding.</summary>
      private static List<string> MediatableFactionsOf(Hero hero)
      {
         var names = new List<string>();

         try
         {
            IFaction leaderFaction = hero?.MapFaction;

            if (leaderFaction == null) return names;

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
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION_MENU] MediatableFactionsOf failed: {ex.GetType().Name}: {ex.Message}");
         }

         return names;
      }

      /// <summary>
      ///   True when <paramref name="faction" /> holds a live war against ANY other faction, not only the
      ///   player's own (the author's C-guard concern: a lord whose realm is at war must stay home for his
      ///   liege's muster call, not ride off with the player and quietly weaken his faction). Walks the same
      ///   Kingdoms + independent-Clans pool <see cref="MediatableFactionsOf" /> already draws from, so this
      ///   answer can never disagree with that list. Guarded so a missing engine snapshot never throws.
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
   }
}
