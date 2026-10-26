// Code written by Gabriel Mailhot, 26/10/2026.
// The player leaves their house for a ruler who heads hers (Gabriel, 26/10/2026: "Jamais elle laissera son trone [...]
// le joueur aura le choix soit d'abandonner son personnage et continuer avec l'heritier"). The heir takes up the house
// and becomes the played character; the old character, now one of the world's lords, weds her and joins her house.
// RaveN017Mod (Nexus, 26/10/2026) asked to marry Rhagaea and hit the game's silent refusal of two heads of houses.
// The steps and their order are LeaveHouseForRulerPolicy's; the engine calls are decompiled 1.4.8 (vanilla's own
// retirement, ApplyHeirSelectionAction, without its DisableHeroAction). It never runs inside the open chat: the played
// character changing under a live conversation is untested ground, so it waits for the map, like a staged duel.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Dynasty;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Dynasty;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Scenarios;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>Hands the player's house to the heir and weds the old character to the ruler he leaves it for.</summary>
   public sealed class LeaveHouseForRulerBehavior : CampaignBehaviorBase
   {
      // Frames the map must be settled after the chat closes before the hand-over runs (the duel's own lesson: a scene
      // opened in the second the chat closed crashed the engine while the game's conversation was still unwinding).
      private const int SettleFrames = 30;

      private string _pendingRulerId;
      private LeaveHouseRoad _pendingRoad;
      private int _framesSettled;

      public static LeaveHouseForRulerBehavior Instance { get; private set; }

      public LeaveHouseForRulerBehavior() => Instance = this;

      // Pumped from the submodule's OnApplicationTick, not CampaignEvents.TickEvent: the campaign tick is paused inside
      // a settlement, so a hand-over agreed in a town would wait for the player to ride out (the duel's lesson).
      public override void RegisterEvents() { }

      // A pending hand-over lives for the frames between the player's yes and the map: nothing to save.
      public override void SyncData(IDataStore dataStore) { }

      /// <summary>The heir the game itself would name first (its own heir apparents, highest score), or null.</summary>
      public static Hero BestHeir()
         => Clan.PlayerClan?.GetHeirApparents()?.OrderByDescending(kv => kv.Value).Select(kv => kv.Key)
               .FirstOrDefault(h => h != null && h != Hero.MainHero && HeroPresence.IsInTheWorld(h) && !h.IsPrisoner);

      /// <summary>The live facts behind leaving the house for <paramref name="ruler" /> by <paramref name="road" />.</summary>
      public static LeaveHouseFacts FactsFor(Hero ruler, LeaveHouseRoad road)
      {
         Hero player = Hero.MainHero;
         if (player == null || ruler == null) return null;

         Hero heir = BestHeir();
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(ruler.StringId);
         MobileParty herParty = ruler.PartyBelongedTo;

         return new LeaveHouseFacts {
            Road = road,
            PlayerIsWed = player.Spouse != null,
            TwoHeadsOfHouses = MarriageProspectResolver.HeadsOfTwoHouses(player, ruler),
            WarOrCaptivity = !MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {
               CandidatePrisoner = ruler.IsPrisoner, PlayerPrisoner = player.IsPrisoner, AtWarWithCandidate = BannerlordGameStateBridge.IsAtWarWithPlayer(ruler)
            }),
            HerHeartIsWon = EncounterContextBuilder.RegardMeetsMarriageBar(ruler, profile),
            HasAnHeir = heir != null,
            HeirRidesWithThePlayer = heir != null && heir.PartyBelongedTo == MobileParty.MainParty,
            ArmyOrBattle = MobileParty.MainParty?.MapEvent != null || herParty?.MapEvent != null || herParty?.Army != null,
            TheGameWouldWedThemAfter = TheGameWouldWedThemAfter(player, ruler)
         };
      }

      /// <summary>What stands in the way of leaving the house for <paramref name="ruler" /> by <paramref name="road" />, or None.</summary>
      public static LeaveHouseBar WhyNot(Hero ruler, LeaveHouseRoad road) => LeaveHouseForRulerPolicy.WhyNot(FactsFor(ruler, road));

      /// <summary>The roads open to the player toward <paramref name="ruler" />, the heir's first.</summary>
      public static LeaveHouseRoad[] OpenRoads(Hero ruler)
         => new[] {LeaveHouseRoad.ThroughTheHeir, LeaveHouseRoad.GiveHerAll}.Where(r => WhyNot(ruler, r) == LeaveHouseBar.None).ToArray();

      /// <summary>
      ///   Every clause of DefaultMarriageModel.IsCoupleSuitableForMarriage the hand-over does not itself lift (it lifts
      ///   only the two-heads-of-houses bar). Kinship within three generations is the model's private test: a refusal
      ///   there is caught after the wedding and the hand-over is undone.
      /// </summary>
      private static bool TheGameWouldWedThemAfter(Hero player, Hero ruler)
      {
         MarriageModel model = Campaign.Current?.Models?.MarriageModel;
         if (model == null) return false;

         bool courtedElsewhere = (Romance.GetCourtedHeroInOtherClan(player, ruler) is { } a && a != ruler)
                                 || (Romance.GetCourtedHeroInOtherClan(ruler, player) is { } b && b != player);

         return ruler.CanMarry() && player.Spouse == null && player.IsFemale != ruler.IsFemale && !AreKin(model, player, ruler)
                && model.IsClanSuitableForMarriage(player.Clan) && model.IsClanSuitableForMarriage(ruler.Clan) && !courtedElsewhere;
      }

      /// <summary>
      ///   The marriage model's own kinship test (three generations), private in DefaultMarriageModel, read by reflection
      ///   so a refused wedding is caught BEFORE the hand-over. Another mod's model, or a renamed method: not known, and
      ///   the hand-back after the wedding stays the net.
      /// </summary>
      private static bool AreKin(MarriageModel model, Hero a, Hero b)
      {
         if (!(model is DefaultMarriageModel)) return false;

         MethodInfo related = AccessTools.Method(typeof(DefaultMarriageModel), "AreHeroesRelated");

         return related?.Invoke(model, new object[] {a, b, 3}) is true;
      }

      /// <summary>The player said yes: the hand-over runs once the chat has closed and the map has settled.</summary>
      public void Request(Hero ruler, LeaveHouseRoad road)
      {
         if (ruler == null) return;

         _pendingRulerId = ruler.StringId;
         _pendingRoad = road;
         _framesSettled = 0;
         ModLog.Info($"[LEAVE_HOUSE] the player will leave the house for {ruler.Name} ({road}); waiting for the map.");
      }

      /// <summary>One frame: runs the pending hand-over once the chat, the mission and the game's conversation are gone.</summary>
      public void Tick()
      {
         if (_pendingRulerId == null) return;

         bool settled = !ChatManager.IsOpen && Mission.Current == null && Campaign.Current != null
                        && Campaign.Current.ConversationManager?.IsConversationInProgress != true
                        && Game.Current?.GameStateManager?.ActiveState is MapState;
         _framesSettled = settled ? _framesSettled + 1 : 0;
         if (_framesSettled < SettleFrames) return;

         string id = _pendingRulerId;
         _pendingRulerId = null;
         Hero ruler = Hero.FindFirst(h => h.StringId == id);
         string refusal = _pendingRoad == LeaveHouseRoad.GiveHerAll ? GiveHerAllService.Give(ruler) : HandOver(ruler);
         if (refusal != null) InformationManager.DisplayMessage(new InformationMessage(refusal, Colors.Red));
      }

      /// <summary>
      ///   The hand-over itself, in <see cref="LeaveHouseForRulerPolicy.Steps" /> order. Null when sealed, else why not,
      ///   with everything handed back. Every bar is asked again first: the player's yes may be some frames old.
      /// </summary>
      public static string HandOver(Hero ruler)
      {
         try
         {
            LeaveHouseBar bar = WhyNot(ruler, LeaveHouseRoad.ThroughTheHeir);
            if (bar != LeaveHouseBar.None)
            {
               ModLog.Info($"[LEAVE_HOUSE] refused before anything was handed over: {bar}.");

               return $"You cannot leave your house for {ruler?.Name} now ({bar}).";
            }

            Hero old = Hero.MainHero;
            Hero heir = BestHeir();
            var handedWorkshops = new List<Workshop>();

            foreach (LeaveHouseStep step in LeaveHouseForRulerPolicy.Steps)
            {
               switch (step)
               {
                  case LeaveHouseStep.HandOverWorkshops:
                     for (int i = old.OwnedWorkshops.Count - 1; i >= 0; i--)
                     {
                        Workshop w = old.OwnedWorkshops[i];
                        ChangeOwnerOfWorkshopAction.ApplyByDeath(w, heir);
                        handedWorkshops.Add(w);
                     }

                     break;
                  case LeaveHouseStep.HandOverCaravans:
                     foreach (Hero h in Clan.PlayerClan.Heroes.ToList())
                        if (h.PartyBelongedTo is {IsCaravan: true} caravan)
                           CaravanPartyComponent.TransferCaravanOwnership(caravan, heir, caravan.HomeSettlement);

                     break;
                  case LeaveHouseStep.DisbandTheArmy:
                     if (MobileParty.MainParty?.Army != null) DisbandArmyAction.ApplyByUnknownReason(MobileParty.MainParty.Army);

                     break;
                  case LeaveHouseStep.HandOverTheHouse:
                     ChangeClanLeaderAction.ApplyWithSelectedNewLeader(Clan.PlayerClan, heir);

                     break;
                  case LeaveHouseStep.HandOverThePlayer:
                     SwitchPlayerTo(heir);

                     break;
                  case LeaveHouseStep.Wed:
                     // rhagaea_marriage.json asks IF_PLAYER_INVOLVED: for this wedding, the one who was the player is.
                     ScenarioContext.StandsInForThePlayer = old;
                     try { MarriageAction.Apply(old, ruler); }
                     finally { ScenarioContext.StandsInForThePlayer = null; }

                     if (old.Spouse != ruler)
                     {
                        ModLog.Error($"[LEAVE_HOUSE] the game refused the wedding of {old.Name} and {ruler.Name} after the hand-over; handing everything back.");
                        HandBack(old, heir, handedWorkshops);

                        return $"The game would not wed {old.Name} to {ruler.Name}; your house and your character were handed back.";
                     }

                     break;
                  case LeaveHouseStep.JoinHer:
                     if (ruler.PartyBelongedTo != null) TeleportHeroAction.ApplyImmediateTeleportToParty(old, ruler.PartyBelongedTo);
                     else if (ruler.HomeSettlement != null) TeleportHeroAction.ApplyImmediateTeleportToSettlement(old, ruler.HomeSettlement);

                     break;
               }
            }

            Seal(old, heir, ruler);

            return null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[LEAVE_HOUSE] the hand-over failed: {ex.GetType().Name}: {ex.Message}");

            return "Leaving your house failed; see the log.";
         }
      }

      // CR's own word that this change of player is an abdication, so the succession waits for the sealed wedding.
      private static void SwitchPlayerTo(Hero hero)
      {
         SuccessionBehavior.AbdicationUnderWay = true;
         try { ChangePlayerCharacterAction.Apply(hero); }
         finally { SuccessionBehavior.AbdicationUnderWay = false; }
      }

      // The wedding was refused after the hand-over: the old character takes the house and the player back.
      private static void HandBack(Hero old, Hero heir, List<Workshop> workshops)
      {
         ChangeClanLeaderAction.ApplyWithSelectedNewLeader(Clan.PlayerClan, old);
         SwitchPlayerTo(old);
         foreach (Workshop w in workshops)
            if (w.Owner == heir) ChangeOwnerOfWorkshopAction.ApplyByDeath(w, old);
      }

      // Sealed: the heir inherits standing, not romance; she remembers why he came to her; the realm hears of it.
      private static void Seal(Hero old, Hero heir, Hero ruler)
      {
         SuccessionBehavior.Instance?.Apply(old, heir, PlayerChangeKind.Abdication, ruler);

         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile hers = store?.Get(ruler.StringId);
         if (hers != null)
         {
            hers.Events.Add(new NotableEvent((int) CampaignTime.Now.ToDays, NotableEventType.OwnLife,
               $"Wed {old.Name}, who gave up the head of {(old.IsFemale ? "her" : "his")} house to stand at my side; I kept my own. "
               + $"{heir.Name} now leads the house {(old.IsFemale ? "she" : "he")} left."));
            store.Set(hers);
         }

         string msg = $"{old.Name} has left {(old.IsFemale ? "her" : "his")} house to wed {ruler.Name}. {heir.Name} now leads it, and you play on as {heir.Name}.";
         MBInformationManager.AddQuickInformation(new TextObject(msg));
         StoryLog.WriteNarration(msg);
         Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
         ModLog.Info($"[LEAVE_HOUSE] sealed: {old.Name} wed {ruler.Name} and joined {ruler.Clan?.Name}; {heir.Name} leads {Clan.PlayerClan?.Name}.");
      }
   }
}
