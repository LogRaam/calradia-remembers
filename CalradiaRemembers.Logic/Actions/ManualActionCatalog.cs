// Code written by Gabriel Mailhot, 21/08/2026.
// Manual action-injection menu: the catalog half. Lists every action verb the player may EMIT BY HAND from
// the chat's action-menu button, for when the Action Interpreter missed a deed or is turned off entirely.
// Deliberately narrower than BannerlordGameStateBridge.HandledActionTypes: only the player-DRIVEN deeds
// belong here (a click is the player's own initiative), never an NPC-JUDGMENT verb (change_relation,
// sway_opinion, give_gold, scheme_assist/heed, grant_blessing, turn_nemesis, execute_player...) or a
// two-sided negotiation (marry, join_as_vassal, swear_oath...) a single click must not unilaterally settle.
// See ManualActionAvailability for which of these the player may pick RIGHT NOW.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>How a manual action's entries are grouped in the menu, in display order.</summary>
   public enum ManualActionGroup
   {
      CoinAndGoods,
      Prisoner,
      Companion,
      Governance,
      Diplomacy
   }

   /// <summary>
   ///   What follow-up the menu must raise before the action can be built: a plain amount, an item from the
   ///   player's roster, a fief name, a party role, a companion-mission kind, an enemy faction, or nothing at
   ///   all (the action carries no parameter and is built and injected the instant it is picked).
   /// </summary>
   public enum ManualActionParameterKind
   {
      None,
      Amount,
      ItemPick,
      FiefPick,
      RolePick,
      MissionPick,
      FactionPick
   }

   /// <summary>
   ///   One action the manual menu can inject: its bridge <see cref="Type" />, the label shown in the menu, the
   ///   <see cref="Group" /> it is listed under, what parameter (if any) the menu must collect before building
   ///   the <c>GameAction</c>, the exact dictionary key that parameter is written under (null when
   ///   <see cref="ParameterKind" /> is <see cref="ManualActionParameterKind.None" /> or
   ///   <see cref="ManualActionParameterKind.ItemPick" />, which is carried instead as
   ///   <c>ActionContext.OfferedItemId</c>, mirroring the existing "give" bag), and whether it must be routed
   ///   through the in-chat Y/n confirmation flow (<see cref="ActionConfirmationPolicy.RequiresConfirmation" />)
   ///   rather than injected straight into the bridge.
   /// </summary>
   public sealed class ManualActionEntry
   {
      public ManualActionEntry(string type,
                                string displayLabel,
                                ManualActionGroup group,
                                ManualActionParameterKind parameterKind,
                                string parameterName,
                                bool requiresConfirmation)
      {
         Type = type;
         DisplayLabel = displayLabel;
         Group = group;
         ParameterKind = parameterKind;
         ParameterName = parameterName;
         RequiresConfirmation = requiresConfirmation;
      }

      /// <summary>The bridge's own <c>GameAction.Type</c> key, exactly as BannerlordGameStateBridge.ExecuteAction matches it.</summary>
      public string Type { get; }

      /// <summary>The plain-language label shown for this entry in the menu.</summary>
      public string DisplayLabel { get; }

      /// <summary>Which section of the menu this entry is listed under.</summary>
      public ManualActionGroup Group { get; }

      /// <summary>What follow-up (if any) the menu must raise before building the action.</summary>
      public ManualActionParameterKind ParameterKind { get; }

      /// <summary>
      ///   The GameAction.Parameters dictionary key the collected value is written under, or null when this
      ///   action needs none (<see cref="ManualActionParameterKind.None" />) or carries it instead as
      ///   <c>ActionContext.OfferedItemId</c> (<see cref="ManualActionParameterKind.ItemPick" />).
      /// </summary>
      public string ParameterName { get; }

      /// <summary>True when this action must be queued for the player's in-chat Y/n confirmation before it lands.</summary>
      public bool RequiresConfirmation { get; }
   }

   /// <summary>
   ///   The fixed set of every action the manual menu may offer, in the stable order the menu lists them (grouped
   ///   Coin &amp; Goods, Prisoner, Companion, Governance, Diplomacy). <see cref="ManualActionAvailability" /> is
   ///   the gate deciding which of these apply to THIS conversation right now.
   /// </summary>
   public static class ManualActionCatalog
   {
      public static readonly ManualActionEntry TakeGold = new(
         "take_gold", "Give them gold", ManualActionGroup.CoinAndGoods, ManualActionParameterKind.Amount, "amount", true);

      public static readonly ManualActionEntry GiveItem = new(
         "give_item", "Give them an item", ManualActionGroup.CoinAndGoods, ManualActionParameterKind.ItemPick, null, false);

      public static readonly ManualActionEntry SpendInfluence = new(
         "spend_influence", "Spend your influence to back their clan", ManualActionGroup.CoinAndGoods, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry ExecutePrisoner = new(
         "execute_prisoner", "Execute this prisoner", ManualActionGroup.Prisoner, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry FreePrisoner = new(
         "free_prisoner", "Set this prisoner free", ManualActionGroup.Prisoner, ManualActionParameterKind.None, null, true);

      public static readonly ManualActionEntry RecruitPrisoner = new(
         "recruit_prisoner", "Recruit this prisoner (turn their coat)", ManualActionGroup.Prisoner, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry DispatchMission = new(
         "dispatch_mission", "Send them on an errand", ManualActionGroup.Companion, ManualActionParameterKind.MissionPick, "target_mission", false);

      public static readonly ManualActionEntry AssignPartyRole = new(
         "assign_party_role", "Assign them a party role", ManualActionGroup.Companion, ManualActionParameterKind.RolePick, "target_role", false);

      public static readonly ManualActionEntry RecallCompanion = new(
         "recall_companion", "Recall them from their errand", ManualActionGroup.Companion, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry RejoinParty = new(
         "rejoin_party", "Order them to rejoin your party", ManualActionGroup.Companion, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry FollowMe = new(
         "follow_me", "Ask them to escort you (follow_me)", ManualActionGroup.Companion, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry DismissEscort = new(
         "dismiss_escort", "Dismiss their escort", ManualActionGroup.Companion, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry RideWithMe = new(
         "ride_with_me", "Ask them to ride in your party", ManualActionGroup.Companion, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry PartWays = new(
         "part_ways", "Part ways with them", ManualActionGroup.Companion, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry ChallengeDuel = new(
         "challenge_duel", "Call them out to a duel", ManualActionGroup.Companion, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry ReassureCompanion = new(
         "reassure_companion", "Reassure them", ManualActionGroup.Companion, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry GrantFief = new(
         "grant_fief", "Grant them a fief", ManualActionGroup.Governance, ManualActionParameterKind.FiefPick, "target_fief", false);

      public static readonly ManualActionEntry RevokeFief = new(
         "revoke_fief", "Revoke one of their fiefs", ManualActionGroup.Governance, ManualActionParameterKind.FiefPick, "target_fief", false);

      public static readonly ManualActionEntry AppointGovernor = new(
         "appoint_governor", "Appoint them governor", ManualActionGroup.Governance, ManualActionParameterKind.FiefPick, "target_fief", false);

      public static readonly ManualActionEntry GrantStipend = new(
         "grant_stipend", "Grant them a stipend", ManualActionGroup.Governance, ManualActionParameterKind.Amount, "target_amount", false);

      public static readonly ManualActionEntry GiveTroops = new(
         "give_troops", "Give them troops", ManualActionGroup.Governance, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry MediatePeace = new(
         "mediate_peace", "Mediate peace with a realm", ManualActionGroup.Diplomacy, ManualActionParameterKind.FactionPick, "target_faction", false);

      public static readonly ManualActionEntry RecruitNotable = new(
         "recruit_notable", "Recruit this notable", ManualActionGroup.Diplomacy, ManualActionParameterKind.None, null, false);

      public static readonly ManualActionEntry JoinParty = new(
         "join_party", "Hire them into your party", ManualActionGroup.Diplomacy, ManualActionParameterKind.Amount, "price", true);

      /// <summary>Every catalog entry, in the menu's stable display order.</summary>
      public static readonly IReadOnlyList<ManualActionEntry> All = new List<ManualActionEntry> {
         TakeGold, GiveItem, SpendInfluence,
         ExecutePrisoner, FreePrisoner, RecruitPrisoner,
         DispatchMission, AssignPartyRole, RecallCompanion, RejoinParty, FollowMe, DismissEscort, RideWithMe, PartWays, ChallengeDuel, ReassureCompanion,
         GrantFief, RevokeFief, AppointGovernor, GrantStipend, GiveTroops,
         MediatePeace, RecruitNotable, JoinParty
      };
   }
}
