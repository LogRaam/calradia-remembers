// Code written by Gabriel Mailhot, 21/08/2026.
// Manual action-injection menu: the availability half. Pure and engine-free, so it is fully unit-testable:
// given a plain snapshot of facts about the CURRENT conversation NPC and the player's own state, decides
// exactly which ManualActionCatalog entries the menu may offer right now. The mod-side resolver
// (ManualActionMenuController) fills a ManualActionFacts from live engine state, reusing the SAME policies
// and EncounterContext gates the Action Interpreter's own 1:1 teaching already computed, never re-deriving a
// gate that exists; the bridge re-validates everything again at injection ("the prompt is advice, the
// bridge is law" applies here exactly as it does to an LLM-proposed action).

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>
   ///   Plain facts about the current conversation NPC and the player's own state, gathered from the SAME
   ///   policies and EncounterContext fields the Action Interpreter's teaching gates already compute. No engine
   ///   types: every field is a bool, an int, or a list of plain strings, so this can be constructed and
   ///   asserted against in a pure unit test.
   /// </summary>
   public sealed class ManualActionFacts
   {
      /// <summary>True when the conversation NPC is a hero prisoner the PLAYER themselves currently holds.</summary>
      public bool NpcIsPlayerPrisoner { get; set; }

      /// <summary>Only meaningful with <see cref="NpcIsPlayerPrisoner" />: PrisonerRecruitmentPolicy says this captive can be turned.</summary>
      public bool CanRecruitPrisoner { get; set; }

      /// <summary>True when the conversation NPC rides as a companion of the player's own clan.</summary>
      public bool NpcIsPlayerCompanion { get; set; }

      /// <summary>Only meaningful with <see cref="NpcIsPlayerCompanion" />: they are away on a dispatched errand right now.</summary>
      public bool NpcIsAwayOnErrand { get; set; }

      /// <summary>Only meaningful with <see cref="NpcIsPlayerCompanion" />: their happiness reads Restless or Disgruntled (a live grievance to hear out).</summary>
      public bool CompanionIsDiscontented { get; set; }

      /// <summary>PartyCommandOfferPolicy.CanOfferDispatchMission for this NPC.</summary>
      public bool NpcCanBeDispatchedOnMission { get; set; }

      /// <summary>PartyCommandOfferPolicy.CanOfferAssignRole for this NPC.</summary>
      public bool NpcCanBeAssignedPartyRole { get; set; }

      /// <summary>PartyCommandOfferPolicy.CanOfferRejoinParty for this NPC.</summary>
      public bool NpcCanRejoinParty { get; set; }

      /// <summary>EscortEligibilityPolicy.CanEscort: this lord's own party could begin escorting the player.</summary>
      public bool NpcCanEscortPlayer { get; set; }

      /// <summary>This lord's own party is genuinely escorting the player right now (follow_me already in force).</summary>
      public bool NpcIsEscortingPlayer { get; set; }

      /// <summary>RideWithMeEligibilityPolicy.CanRideWithPlayer: this lord could ride inside the player's own party.</summary>
      public bool NpcCanRideWithPlayer { get; set; }

      /// <summary>
      ///   DuelEligibilityResolver.CanDuelNow: there is a venue and this character may be called out right now.
      ///   Offered so a player who wants to fight a lord does not depend on the model hearing them say it
      ///   (FloatingMammal, 2026-09-10: "I just want to beat up some lords in single combat"). Whether the lord
      ///   ACCEPTS is still DuelAcceptancePolicy's answer, unchanged: this opens the door, it does not walk
      ///   anybody through it.
      /// </summary>
      public bool NpcCanBeChallengedToDuel { get; set; }

      /// <summary>This lord genuinely rides in the player's own party right now (ride_with_me already in force).</summary>
      public bool NpcIsRidingWithPlayer { get; set; }

      /// <summary>FiefTransferPolicy.CanOfferGrant: the player rules a kingdom, this NPC is a vassal of a separate house in it, and a grantable fief exists.</summary>
      public bool NpcCanBeGrantedFief { get; set; }

      /// <summary>Only meaningful with <see cref="NpcCanBeGrantedFief" />: the player-clan crown fiefs on offer.</summary>
      public IReadOnlyList<string> GrantableFiefs { get; set; }

      /// <summary>FiefTransferPolicy.CanOfferRevoke: the player rules a kingdom, this NPC is a vassal of a separate house in it, and holds a revocable fief.</summary>
      public bool NpcCanHaveFiefRevoked { get; set; }

      /// <summary>Only meaningful with <see cref="NpcCanHaveFiefRevoked" />: this NPC's own fiefs that could be stripped.</summary>
      public IReadOnlyList<string> RevocableFiefs { get; set; }

      /// <summary>GovernorAppointmentPolicy.CanOfferAppointment: this NPC (of the player's own clan) could be seated over a vacant fief.</summary>
      public bool NpcCanBeAppointedGovernor { get; set; }

      /// <summary>Only meaningful with <see cref="NpcCanBeAppointedGovernor" />: the player-clan fiefs with no governor.</summary>
      public IReadOnlyList<string> AppointableFiefs { get; set; }

      /// <summary>StipendPolicy.CanOfferStipend: this companion could be put on a recurring daily wage.</summary>
      public bool NpcCanBeGrantedStipend { get; set; }

      /// <summary>CourtActionResolver.TryGiveTroops: this NPC's own party is genuinely under-strength and could accept reinforcement.</summary>
      public bool CanGiveTroops { get; set; }

      /// <summary>CourtActionResolver.TrySpendInfluence: this NPC's own clan is needy enough to accept the player's backing at court.</summary>
      public bool CanSpendInfluence { get; set; }

      /// <summary>PeaceMediationPolicy.CanMediate: this NPC is a faction leader at war, and the player carries enough stature and trust to broker peace.</summary>
      public bool PlayerCanMediatePeace { get; set; }

      /// <summary>Only meaningful with <see cref="PlayerCanMediatePeace" />: the enemy realms this leader could be brought to peace with.</summary>
      public IReadOnlyList<string> MediatablePeaceFactions { get; set; }

      /// <summary>NotableRecruitmentPolicy.Evaluate: this settlement notable could bequeath their post and join the player's clan.</summary>
      public bool CanRecruitNotable { get; set; }

      /// <summary>
      ///   LordRecruitment.Evaluate for this lord: None when they would leave their house for the player's, else the
      ///   gate that holds them. Null when they are not a free lord of another house at all (LordRecruitmentFactsBuilder
      ///   gave no facts: a commoner, a wanderer, a prisoner, one of the player's own clan).
      /// </summary>
      public LordRecruitmentBlock? LordJoinBlock { get; set; }

      /// <summary>
      ///   Only meaningful when <see cref="LordJoinBlock" /> is None: StanceGatePolicy.CommandsRespectToJoinClan
      ///   passes for this lord (true when they have no stance profile, as at the teaching gate and the bridge).
      /// </summary>
      public bool LordRespectsPlayerEnough { get; set; }

      /// <summary>ResolveCompanionAskingPrice: this free wanderer could be hired, at this asking price (denars); null when not hireable.</summary>
      public int? CompanionAskingPrice { get; set; }

      /// <summary>The player's own party carries at least one item that could be handed over.</summary>
      public bool PlayerHasGiveableItems { get; set; }

      /// <summary>The content level permits a conception (Explicit or above); below it the scene is never listed.</summary>
      public bool ContentPermitsConception { get; set; }

      /// <summary>The player and this character could conceive a child together (a fertile woman and a man).</summary>
      public bool PairingCanConceive { get; set; }

      /// <summary>This character is the player's spouse, consort or lover.</summary>
      public bool NpcIsIntimatePartner { get; set; }
   }

   /// <summary>Where one manual action stands in this conversation, for the three ranks the panel shows.</summary>
   public enum ManualActionState
   {
      /// <summary>He may ask for it now, and the panel lets him inject it.</summary>
      Open,

      /// <summary>It applies to her, but not as things stand. The reason names what would have to change.</summary>
      Blocked,

      /// <summary>It does not apply to WHO SHE IS, and no play this afternoon changes that.</summary>
      NotForThisPerson
   }

   /// <summary>One catalogue entry with where it stands, and why when it is closed.</summary>
   public sealed class ManualActionStatus
   {
      public ManualActionStatus(ManualActionEntry entry, ManualActionState state, string reason)
      {
         Entry = entry;
         State = state;
         Reason = reason ?? "";
      }

      public ManualActionEntry Entry { get; }
      public ManualActionState State { get; }

      /// <summary>Plain player language, empty when <see cref="State" /> is <see cref="ManualActionState.Open" />.</summary>
      public string Reason { get; }
   }

   /// <summary>
   ///   Decides, from a <see cref="ManualActionFacts" /> snapshot, exactly which <see cref="ManualActionCatalog" />
   ///   entries the manual menu may offer for THIS conversation right now. Every entry returned still passes
   ///   through the bridge's own re-validation on injection ("the prompt is advice, the bridge is law"): a stale
   ///   or borderline fact can at worst offer an entry the bridge then politely refuses, never apply one it
   ///   should not have.
   /// </summary>
   public static class ManualActionAvailability
   {
      /// <summary>
      ///   What may be offered right now. A VIEW of <see cref="Describe" />, never a second decision: the panel
      ///   and the menu read one answer, so neither can offer what the other would refuse. Two surfaces over one
      ///   decision is the drift this repository spent 21-22/09/2026 learning about, twice.
      /// </summary>
      public static IReadOnlyList<ManualActionEntry> GetAvailable(ManualActionFacts facts)
         => Describe(facts).Where(s => s.State == ManualActionState.Open).Select(s => s.Entry).ToList();

      /// <summary>
      ///   Every catalogue entry, with where it stands and why when it is closed. The panel's whole content.
      ///   <para>
      ///     WHEN IN DOUBT, BLOCKED. Several facts here fold "who she is" and "where things stand" into one
      ///     boolean, and there is no honest way to split them without richer facts. Where that happens the
      ///     answer is Blocked, deliberately, because it is the weaker claim: calling something merely
      ///     out of reach when it will in fact never happen only leaves a row in the middle rank, while calling
      ///     it impossible when it was one errand away is a lie the player will act on. NotForThisPerson is used
      ///     only where an IDENTITY fact says so outright.
      ///   </para>
      /// </summary>
      public static IReadOnlyList<ManualActionStatus> Describe(ManualActionFacts facts)
      {
         var result = new List<ManualActionStatus>();

         // A NULL SNAPSHOT IS NOT AN EMPTY WORLD. It means the situation could not be read, and the shipped
         // contract is that nothing is offered then (GetAvailable(null) has always been empty). Substituting a
         // blank facts object would have turned "unknown" into "a world where the player owns nothing", and
         // offered take_gold on the strength of it. The existing null test caught exactly that.
         if (facts == null)
         {
            foreach (ManualActionEntry entry in ManualActionCatalog.All)
               result.Add(new ManualActionStatus(entry, ManualActionState.Blocked, "the moment cannot be read just now"));

            return result;
         }

         void Open(ManualActionEntry e) => result.Add(new ManualActionStatus(e, ManualActionState.Open, ""));
         void Blocked(ManualActionEntry e, string why) => result.Add(new ManualActionStatus(e, ManualActionState.Blocked, why));
         void NotHer(ManualActionEntry e, string why) => result.Add(new ManualActionStatus(e, ManualActionState.NotForThisPerson, why));

         void When(bool ok, ManualActionEntry e, string why)
         {
            if (ok) Open(e);
            else Blocked(e, why);
         }

         // Coin and goods. Taking gold needs nothing but a live conversation, which the panel only opens in.
         Open(ManualActionCatalog.TakeGold);
         When(facts.PlayerHasGiveableItems, ManualActionCatalog.GiveItem, "you are carrying nothing worth handing over");
         When(facts.CanSpendInfluence, ManualActionCatalog.SpendInfluence, "you have no influence to spend on them");

         // Prisoner. The gate is WHO SHE IS, so someone who is not your captive is in the bottom rank outright.
         if (!facts.NpcIsPlayerPrisoner)
         {
            const string notYours = "they are not your prisoner";
            NotHer(ManualActionCatalog.ExecutePrisoner, notYours);
            NotHer(ManualActionCatalog.FreePrisoner, notYours);
            NotHer(ManualActionCatalog.RecruitPrisoner, notYours);
         }
         else
         {
            Open(ManualActionCatalog.ExecutePrisoner);
            Open(ManualActionCatalog.FreePrisoner);
            When(facts.CanRecruitPrisoner, ManualActionCatalog.RecruitPrisoner, "they will not take your colours yet");
         }

         // Companion and party.
         When(facts.NpcCanBeDispatchedOnMission, ManualActionCatalog.DispatchMission, "there is nothing you could send them to do just now");
         When(facts.NpcCanBeAssignedPartyRole, ManualActionCatalog.AssignPartyRole, "they hold no place in your party that a role could be given to");

         if (!facts.NpcIsPlayerCompanion)
         {
            const string notYours = "they are not one of your companions";
            NotHer(ManualActionCatalog.RecallCompanion, notYours);
            NotHer(ManualActionCatalog.ReassureCompanion, notYours);
         }
         else
         {
            When(facts.NpcIsAwayOnErrand, ManualActionCatalog.RecallCompanion, "they are at your side already, not away on an errand");
            When(facts.CompanionIsDiscontented, ManualActionCatalog.ReassureCompanion, "they are content enough just now");
         }

         When(facts.NpcCanRejoinParty, ManualActionCatalog.RejoinParty, "there is no place waiting in your party for them to return to");
         When(facts.NpcCanEscortPlayer, ManualActionCatalog.FollowMe, "they are not free to walk at your side");
         When(facts.NpcIsEscortingPlayer, ManualActionCatalog.DismissEscort, "they are not escorting you");
         When(facts.NpcCanRideWithPlayer, ManualActionCatalog.RideWithMe, "they are not free to ride with your party");
         When(facts.NpcIsRidingWithPlayer, ManualActionCatalog.PartWays, "they are not riding with you");
         When(facts.NpcCanBeChallengedToDuel, ManualActionCatalog.ChallengeDuel, "there is nowhere here the two of you could cross blades");

         // Governance.
         When(facts.NpcCanBeGrantedFief, ManualActionCatalog.GrantFief, "you have no fief in your gift for them");
         When(facts.NpcCanHaveFiefRevoked, ManualActionCatalog.RevokeFief, "they hold no fief of yours to take back");
         When(facts.NpcCanBeAppointedGovernor, ManualActionCatalog.AppointGovernor, "you have no seat wanting a governor they could take");
         When(facts.NpcCanBeGrantedStipend, ManualActionCatalog.GrantStipend, "they are not someone you could set a stipend upon");
         When(facts.CanGiveTroops, ManualActionCatalog.GiveTroops, "you have no soldiers to spare them");

         // Diplomacy and recruitment.
         When(facts.PlayerCanMediatePeace, ManualActionCatalog.MediatePeace, "there is no war of theirs you could stand between");
         When(facts.CanRecruitNotable, ManualActionCatalog.RecruitNotable, "they are not a notable you could bring into your service");
         When(facts.CompanionAskingPrice.HasValue, ManualActionCatalog.JoinParty, "they are not for hire");
         DescribeLordJoin(facts, Open, Blocked, NotHer);

         // Intimacy (Gabriel, 16/10/2026): listed only for the player's spouse, consort or lover, and never below
         // Explicit. Shown to anyone else, even blocked, it would offer a scene with a stranger, a parent or a child.
         if (facts.ContentPermitsConception && facts.NpcIsIntimatePartner)
         {
            if (facts.PairingCanConceive) Open(ManualActionCatalog.ConceptionRisk);
            else NotHer(ManualActionCatalog.ConceptionRisk, "no child could come of the two of you");
         }

         return result;
      }

      /// <summary>
      ///   The Actions menu's rows: what may be taken now, then what is out of reach as things stand, each with its
      ///   reason (Fakade, Nexus, 12/11/2026: "Is it possible to see all possible Action/Options rather than only the
      ///   ones we have access to? Keep an option greyed out, perhaps with some hint as to why it is not available").
      ///   What will never apply to this person stays out: for most characters it is most of the catalogue, and a
      ///   menu of grey rows about fiefs for a tavern keeper would bury the few that matter.
      /// </summary>
      public static IReadOnlyList<ManualActionStatus> MenuRows(ManualActionFacts facts)
      {
         IReadOnlyList<ManualActionStatus> all = Describe(facts);

         return all.Where(s => s.State == ManualActionState.Open)
                   .Concat(all.Where(s => s.State == ManualActionState.Blocked))
                   .ToList();
      }

      /// <summary>
      ///   join_clan by hand (Fakade, 12/11/2026, who did not know a free lord could be recruited because the menu
      ///   never showed it). Reads the same LordRecruitment gate the teaching and the bridge read, and names the one
      ///   that holds. A clan leader is NotForThisPerson: leading the house is who they are, and the bridge refuses it
      ///   outright.
      /// </summary>
      private static void DescribeLordJoin(ManualActionFacts facts,
                                           System.Action<ManualActionEntry> open,
                                           System.Action<ManualActionEntry, string> blocked,
                                           System.Action<ManualActionEntry, string> notHer)
      {
         ManualActionEntry entry = ManualActionCatalog.JoinClan;

         switch (facts.LordJoinBlock)
         {
            case null:
               notHer(entry, "they are not a free lord of another house");

               break;

            case LordRecruitmentBlock.LordLeadsOwnClan:
               notHer(entry, "they lead their own house, and cannot fold it into yours");

               break;

            case LordRecruitmentBlock.TrustTooLow:
               blocked(entry, $"their regard for you must reach {LordRecruitment.MinRelation} before they would leave their house for yours");

               break;

            case LordRecruitmentBlock.PlayerRankTooLow:
               blocked(entry, "your clan must stand at least as high as theirs");

               break;

            case LordRecruitmentBlock.LordNotVulnerable:
               blocked(entry, "they are too settled to leave: their house holds land and is strong, and they are content with their liege");

               break;

            default:
               if (facts.LordRespectsPlayerEnough) open(entry);
               else blocked(entry, "they do not respect you enough yet to follow you");

               break;
         }
      }
   }
}
