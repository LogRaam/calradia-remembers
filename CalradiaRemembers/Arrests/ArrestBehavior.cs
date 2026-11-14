// Code written by Gabriel Mailhot, 26/09/2026.
// Arrests Inc 2: the player's case, kept across saves, and the grave acts that open it (Gabriel's rulings, 26/09/2026;
// the rules are ArrestPolicy's and the case is ArrestCase, both pure). Three kinds of act bring a warrant in the liege's
// name, and only while the player is a vassal or a mercenary:
//
//   - a treasonous letter of theirs, read by a lord of their own realm (the courier secrets, CourierSecretBehavior);
//   - violence in conversation against a member of their own realm (ViolenceConsequenceService);
//   - the game's own crime rating in their realm turning SEVERE. Charged on the day it turns severe, not every day it
//     stays so: an acquittal wipes the charge, and a rating left severe must not bring the same warrant straight back.
//     Only a new grave act can (Gabriel).
//
// Inc 3, the sergeants: while a warrant is out, a party of the liege's own troops rides from the realm's town nearest
// the player and pursues them. When it reaches them (the same "one party has reached another" range the interception
// uses, checked every frame since a pursuer can cross it within an hour), a popup reads the warrant: surrender, and be
// taken to the liege for trial; refuse, and be named an outlaw. Fighting them off is refusing. Like the couriers, the
// sergeants are never saved: they are despawned before a save and ride out again after a load. What surrender and
// refusal then set in motion (the trial, the outlaw) is Inc 4 and 5. Additive save key.
//
// Inc 4, the trial (Gabriel: captive, then judged; "le joueur doit sentir qu'il confronte une assemblee"): once the
// player is in the liege's cells, the TRIAL council is convened over the captivity (CouncilScope.Trial, the one sitting
// captivity does not block), seating the liege and the vassals who hate the player most. Its verdict is applied at the
// lift (CouncilLift.SettleVerdict -> ApplyVerdict): acquitted, the charges are wiped and the player walks free; convicted,
// the player serves ArrestPolicy.PrisonDays in those cells and is released when they run out. A council that rose
// without a verdict (the window closed, a save interrupted it) is judged by the bench's regard
// (TrialVerdictPolicy.DefaultConviction), so closing the window is no escape. Breaking out is: an escape is a flight.
//
// Inc 5, the outlaw (OutlawPolicy): a flight (refusing the warrant, beating off the sergeants, breaking out) puts the
// realm's price on the player's head. A vassal leaves the kingdom, a mercenary its contract; the player then leads an
// independent clan, and the realm's crime rating is raised past the game's own war threshold, so the game itself makes
// war on them: the realm's lords and troops are real enemies who can beat and take them. Each week, each realm with a
// bounty may declare an amnesty (a low chance): the bounty is lifted, the crime rating cleared, the war ended, and a
// fled outlaw pardoned.
//
// Hunters: each realm with a bounty sends a band of bounty hunters (OutlawPolicy.HunterStrength) that rides to ENGAGE
// the player, a real fight. A player leading an independent clan is at war with the realm, so the band rides in the
// realm's own colours; a player still sworn to a kingdom cannot be warred on alone, so the band rides under an outlaw
// clan instead (OutlawPolicy.ModeFor). Beaten, the player is taken; whoever takes them (the hunters, or any party of a
// realm that wants them), the bounty is collected and the player handed to that realm's sovereign, to be tried by
// that realm's council, the bounty's cause charged. Like the sergeants, hunters are never saved.
//
// froggyluv (Nexus, 22/10/2026): hunters of two realms on him again and again, charging him inside a 2000-strong army,
// "I've disabled that in options but they're still showing up", and no way to settle. Gabriel (23/10/2026): with the
// option off every bounty is lifted as an amnesty lifts it (unless the law already holds the player); no hunters ride
// against a player marching in another lord's army; a bounty can be paid to a lord of the realm (pay_bounty). The day
// each realm last sent hunters is now saved: a load no longer sends a fresh band at once.

#region

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Travel;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Arrests
{
   /// <summary>Keeps the player's case and opens it when they do something grave.</summary>
   public sealed class ArrestBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CR_ArrestCase_v1bytes";

      public static ArrestBehavior Instance { get; private set; }

      /// <summary>TUNE: the sergeants' number, enough to look like the law, not an army.</summary>
      private const int SergeantCount = 12;

      /// <summary>TUNE: their base speed, above a courier's, so a player on the road cannot simply outride the law.</summary>
      private const float SergeantSpeed = 8f;

      /// <summary>The range at which one party has reached another (InterceptionBehavior.ArrivalRange, CourierService.ArrivalDistance).</summary>
      private const float ArrivalRange = 2f;

      private SavedState _state = new();

      // The sergeants and bounty hunters on the road are TransientPartyRegistry's (26/09/2026): it despawns them before a
      // save and tells this behavior when one is truly destroyed. Never saved; sent again after a load.
      private const string SergeantsRole = "arrest.sergeants";
      private const string HuntersRole = "arrest.hunters";

      // Set while the law itself frees the player, so the release is not mistaken for an escape.
      private bool _freeing;

      // The realm whose bounty the player was just taken on, handed over on the next hour (never inside the battle's
      // own capture call, which is still running when the capture event fires).
      private string _pendingHandoverRealmId;

      public ArrestBehavior()
      {
         Instance = this;
      }

      /// <summary>The player's case as it stands.</summary>
      internal ArrestCase Case => _state.Case;

      /// <summary>Test seam (cr.test all): the whole saved state, as JSON, so a self-test can put it back exactly.</summary>
      internal string DebugSnapshot() => JsonConvert.SerializeObject(_state, Formatting.None);

      /// <summary>Test seam (cr.test all): restores a state taken by <see cref="DebugSnapshot" />.</summary>
      internal void DebugRestore(string json)
      {
         _state = JsonConvert.DeserializeObject<SavedState>(json ?? "{}") ?? new SavedState();
         _state.Case ??= new ArrestCase();
         _state.Bounties ??= new List<Bounty>();
         _state.HuntersSentDay ??= new Dictionary<string, int>();
      }

      /// <summary>The realm's price on the player's head, or null when it has none.</summary>
      internal Bounty BountyOf(IFaction realm)
         => realm == null ? null : _state.Bounties.FirstOrDefault(b => b.RealmId == realm.StringId);

      /// <summary>
      ///   The player pays a lord of the realm to have its price lifted (pay_bounty, Gabriel 23/10/2026): the gold goes to
      ///   the lord, and the bounty is lifted as an amnesty lifts it. Null on success, else why it cannot be done.
      /// </summary>
      internal string PayBounty(Hero lord)
      {
         IFaction realm = lord?.MapFaction;
         Bounty bounty = BountyOf(realm);
         if (bounty == null) return $"{realm?.Name?.ToString() ?? "This realm"} has no price on your head";
         if (HoldsPlayer) return "the law already holds you; your case is for the court now";

         int price = OutlawPolicy.BountyPrice(bounty.Reason);
         if (Hero.MainHero.Gold < price) return $"you cannot cover the {price} denars it costs";

         GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, lord, price);
         LiftBounty(bounty, $"You pay {price} denars to {lord.Name}. The price on your head in {realm.Name} is lifted.", $"paid {price} to {lord.Name}");

         return null;
      }

      /// <summary>True while the law holds the player: awaiting trial, or serving the sentence.</summary>
      internal bool HoldsPlayer => Case.Stage == ArrestStage.OnTrial || Case.Stage == ArrestStage.Imprisoned;

      /// <summary>
      ///   The trial's bench: the liege first (who presides), then the vassals of the realm who regard the player worst
      ///   on the mod's own ledger (ArrestPolicy.CouncilSeats), wherever they are. Empty with no liege.
      /// </summary>
      internal List<Hero> TrialBench()
      {
         var bench = new List<Hero>();
         Hero liege = Liege();
         if (liege == null) return bench;

         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         IEnumerable<CouncilCandidate> vassals = (liege.Clan?.Kingdom?.Clans ?? Enumerable.Empty<Clan>())
                                                 .Where(c => c != null && c != liege.Clan && c != Clan.PlayerClan && !c.IsEliminated)
                                                 .Select(c => c.Leader)
                                                 .Where(h => Mapping.ActionGate.Allows(h, Logic.ActionKind.TrialSeat)) // a clan leader may be under age
                                                 .Select(h => new CouncilCandidate {HeroId = h.StringId, RegardForPlayer = store?.Get(h.StringId)?.ReputationWithPlayer ?? 0});

         foreach (string id in ArrestPolicy.CouncilSeats(liege.StringId, vassals, Hero.MainHero?.StringId))
         {
            Hero h = Hero.FindFirst(x => x != null && x.StringId == id);
            if (h != null) bench.Add(h);
         }

         return bench;
      }

      /// <summary>
      ///   The council's verdict, applied at its lift. Acquitted: the charges are wiped and the player walks free.
      ///   Convicted: they serve the sentence in the same cells. False when no trial awaits a verdict.
      /// </summary>
      internal bool ApplyVerdict(bool convicted)
      {
         if (Case.Stage != ArrestStage.OnTrial) return false;

         Hero liege = Liege();
         Settlement prison = Prison();
         Case.Verdict(convicted, Today);

         if (!convicted)
         {
            FreePlayer(liege);
            MBInformationManager.AddQuickInformation(new TextObject("The council acquits you. The charges are dropped, and you walk free."));
            ModLog.Info("[ARREST] the council acquits the player; the charges are wiped.");

            return true;
         }

         int days = Case.ReleaseDay - Today;
         MBInformationManager.AddQuickInformation(new TextObject($"The council finds you guilty. You are to serve {days} days in the cells of {prison?.Name?.ToString() ?? "your liege"}."));
         ModLog.Info($"[ARREST] the council convicts the player: {days} days, release on day {Case.ReleaseDay}.");

         return true;
      }

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
         CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
      }

      /// <summary>
      ///   A grave act. Opens a warrant in the liege's name, or adds to the case already open. Nothing happens for a
      ///   player who answers to no liege (a faction leader, or outside any realm). <paramref name="what" /> says it in
      ///   words, for the player and the log.
      /// </summary>
      public void Charge(ArrestCharge charge, string what, bool notify = true)
      {
         try
         {
            // MCM, off by default: nothing new is opened; a case already open still takes its charges.
            if (!ModSettings.EnableArrests && Case.Stage == ArrestStage.None) return;

            Hero liege = Clan.PlayerClan?.Kingdom?.Leader;
            // The warrant is the liege's act (30/09/2026): no warrant from a dead, absent, child or captive ruler. The
            // trial bench already asked the gate; the warrant that leads to it did not ask anything.
            bool canBeArrested = ArrestPolicy.CanBeArrested(Standing()) && Mapping.ActionGate.Allows(liege, Logic.ActionKind.Warrant, log: true);
            bool wasOpen = Case.Stage != ArrestStage.None;

            Case.Charge(charge, liege?.StringId, canBeArrested);

            if (!canBeArrested) return;

            if (!wasOpen && Case.Stage == ArrestStage.WarrantOut)
            {
               if (notify)
                  MBInformationManager.AddQuickInformation(new TextObject($"Word reaches you: {liege.Name} has issued a warrant for your arrest, for {what}."));
               ModLog.Info($"[ARREST] warrant in {liege.Name}'s name: {charge} ({what}).");
            }
            else
            {
               ModLog.Info($"[ARREST] charge added to the open case: {charge} ({what}).");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] charging failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Where the player stands: a faction leader, a vassal, a mercenary, or none of these.</summary>
      internal static ArrestStandingFacts Standing()
      {
         Clan clan = Clan.PlayerClan;
         var kingdom = clan?.Kingdom;

         return new ArrestStandingFacts {
            IsFactionLeader = kingdom != null && kingdom.RulingClan == clan,
            IsMercenary = kingdom != null && clan.IsUnderMercenaryService,
            IsVassal = kingdom != null && !clan.IsUnderMercenaryService
         };
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_state, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[ARREST] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[ARREST] save failed: {ex.GetType().Name}: {ex.Message}"); }

            return;
         }

         byte[] loaded = null;
         try { dataStore.SyncData(SaveKey, ref loaded); }
         catch
         {
            /* key absent in older saves */
         }

         if (loaded is not {Length: > 0}) return;

         try
         {
            _state = JsonConvert.DeserializeObject<SavedState>(Encoding.UTF8.GetString(loaded)) ?? new SavedState();
            _state.Case ??= new ArrestCase();
            _state.Bounties ??= new List<Bounty>();
            _state.HuntersSentDay ??= new Dictionary<string, int>();
         }
         catch (Exception ex)
         {
            _state = new SavedState();
            ModLog.Error($"[ARREST] load failed, the case is dropped for this load. {ex.GetType().Name}: {ex.Message}");
         }
      }

      #region private

      /// <summary>A severe crime rating brings a warrant on the day it turns severe, once.</summary>
      private void OnDailyTick()
      {
         try
         {
            var kingdom = Clan.PlayerClan?.Kingdom;
            bool severe = kingdom != null && Campaign.Current?.Models?.CrimeModel?.IsPlayerCrimeRatingSevere(kingdom) == true;

            if (severe && !_state.CrimeWasSevere) Charge(ArrestCharge.SevereCrimeRating, "your crimes in the realm");
            _state.CrimeWasSevere = severe;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] daily tick failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   While a warrant is out, the sergeants ride. While the law holds the player, the trial is convened, the
      ///   sentence runs out, or an escape is noticed.
      /// </summary>
      private void OnHourlyTick()
      {
         try
         {
            if (_pendingHandoverRealmId != null)
            {
               HandOverToSovereign();

               return;
            }

            if (HoldsPlayer)
            {
               // A band is taken off only once it no longer holds the player (the handover moved them to the cells).
               if (PlayerCaptivity.CaptorParty?.IsSettlement == true) DespawnHunters();
               TickHeld();

               return;
            }

            LiftBountiesWhenOff(ModSettings.EnableArrests);
            TickHunters();

            if (Case.Stage != ArrestStage.WarrantOut)
            {
               DespawnSergeants();
               Initiative.WorldInitiativeBehavior.Instance?.Withdraw(WarrantKey);

               return;
            }

            MobileParty main = MobileParty.MainParty;
            if (main == null || Hero.MainHero.IsPrisoner || main.IsCurrentlyAtSea) return;

            MobileParty sergeants = Sergeants() ?? SendSergeants();
            if (sergeants != null) TravelingParty.DirectToMovingPoint(sergeants, main.GetPosition2D);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] hourly tick failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private const string WarrantKey = "arrest:warrant";
      private const string TrialKey = "arrest:trial";
      private double _lastWarrantKnockHour = double.MinValue;

      /// <summary>The sergeants reach the player: the warrant is read, once they stand free on dry land.</summary>
      private void OnTick(float dt)
      {
         try
         {
            if (Case.Stage != ArrestStage.WarrantOut) return;

            MobileParty sergeants = Sergeants();
            MobileParty main = MobileParty.MainParty;
            if (sergeants == null || main == null) return;
            if (sergeants.GetPosition2D.Distance(main.GetPosition2D) > ArrivalRange) return;
            if (!InterceptionBehavior.PlayerIsFreeOnMap() || main.IsCurrentlyAtSea) return;
            if (main.Army != null && main.Army.LeaderParty != main) return; // never force a scene on an army subordinate

            // The world's initiative (29/10/2026): the arrest is one of Gabriel's four that always pass, yet it waits out a
            // battle, a chat or the settling delay. Asked once an in-game hour at most (this runs every frame); kept
            // waiting, the sergeants keep shadowing the player.
            double hour = CampaignTime.Now.ToHours;
            if (hour - _lastWarrantKnockHour < 1) return;
            _lastWarrantKnockHour = hour;
            if (!(Initiative.WorldInitiativeBehavior.Instance?.MayKnock(WarrantKey, Logic.Initiative.InitiativeFamily.WarAndLaw, Logic.Initiative.InitiativeRank.Imposed, Liege()) ?? true))
               return;

            DespawnSergeants();
            ReadTheWarrant();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] arrival check failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The warrant, read by the sergeants' captain: surrender, or refuse and be an outlaw.</summary>
      private void ReadTheWarrant()
      {
         Hero liege = Hero.FindFirst(h => h != null && h.StringId == Case.LiegeId);
         string lord = liege?.Name?.ToString() ?? "your liege";
         string charges = ArrestPolicy.Describe(Case.Charges);

         InformationManager.ShowInquiry(new InquiryData(
            $"In {lord}'s name",
            $"Sergeants of {lord} bar your road. Their captain unrolls a warrant: you are to answer before {lord} for {charges}.\n\n" +
            $"Surrender, and you will be taken to {lord} for judgement. Refuse, and you will be named an outlaw, with a price on your head.",
            true, true, "Surrender", "Refuse",
            Surrender, Refuse), true);
         ModLog.Info($"[ARREST] the sergeants reach the player and read the warrant ({charges}).");
      }

      /// <summary>
      ///   The player gives themselves up and is taken, alone (the party waits, as the engine keeps it for any captive
      ///   player), to the cells of the liege's town or castle nearest them, there to be tried (Gabriel, 26/09/2026:
      ///   captive, then judged).
      /// </summary>
      private void Surrender()
      {
         Case.Taken();
         ModLog.Info("[ARREST] the player surrenders to the sergeants.");
         TakeToPrison();
      }

      /// <summary>
      ///   Puts the player in the cells of the liege's own town or castle nearest them (else any of the realm's). The
      ///   engine handles a settlement captor natively: the player waits in its dungeon, the party frozen until release.
      /// </summary>
      private void TakeToPrison()
      {
         try
         {
            Hero liege = Hero.FindFirst(h => h != null && h.StringId == Case.LiegeId);
            MobileParty main = MobileParty.MainParty;
            if (liege == null || main == null || Hero.MainHero.IsPrisoner) return;

            Vec2 at = main.GetPosition2D;
            Settlement prison = Settlement.All
                                          .Where(s => (s.IsTown || s.IsCastle) && s.Party != null && s.OwnerClan == liege.Clan)
                                          .OrderBy(s => s.GetPosition2D.Distance(at))
                                          .FirstOrDefault()
                                ?? Settlement.All
                                             .Where(s => (s.IsTown || s.IsCastle) && s.Party != null && s.MapFaction == liege.MapFaction)
                                             .OrderBy(s => s.GetPosition2D.Distance(at))
                                             .FirstOrDefault();
            if (prison == null)
            {
               ModLog.Error("[ARREST] no town or castle of the realm to hold the player in; the arrest cannot proceed.");

               return;
            }

            Case.PrisonSettlementId = prison.StringId;
            TakePrisonerAction.Apply(prison.Party, Hero.MainHero);
            MBInformationManager.AddQuickInformation(new TextObject($"The sergeants take you to {prison.Name}, to answer before {liege.Name}."));
            ModLog.Info($"[ARREST] the player is held in {prison.Name} to await trial (captive={Hero.MainHero.IsPrisoner}).");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] taking the player to prison failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The player refuses the warrant, or fights the sergeants off: an outlaw. What follows is Inc 5.</summary>
      private void Refuse()
      {
         IFaction realm = Liege()?.MapFaction;
         Case.Flee();
         MBInformationManager.AddQuickInformation(new TextObject("You refuse the warrant. From this day you are an outlaw, and there is a price on your head."));
         ModLog.Info("[ARREST] the player refuses the warrant: outlaw, with a bounty.");
         BecomeOutlaw(realm, BountyReason.Flight);
      }

      /// <summary>
      ///   The realm puts a price on the player's head. A player sworn to it leaves it (a vassal the kingdom, a
      ///   mercenary the contract), which leaves them leading an independent clan; the realm's crime rating is then
      ///   raised past the game's own war threshold, and the game makes war on that clan. Never throws.
      /// </summary>
      internal void BecomeOutlaw(IFaction realm, BountyReason reason)
      {
         try
         {
            if (realm == null || Clan.PlayerClan == null) return;

            OutlawPolicy.Place(_state.Bounties, realm.StringId, reason, Today);

            if (Clan.PlayerClan.Kingdom != null && Clan.PlayerClan.Kingdom == realm)
            {
               bool mercenary = Clan.PlayerClan.IsUnderMercenaryService;
               var kingdom = Clan.PlayerClan.Kingdom;
               GameState.DiplomacyOutcome door = GameState.DiplomacyCommit.Realm(mercenary ? Logic.Diplomacy.DiplomacyChange.ClanLeavesMercenaryService : Logic.Diplomacy.DiplomacyChange.ClanLeavesKingdom,
                  "arrest", Clan.PlayerClan, kingdom, null, () =>
                  {
                     if (mercenary) ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(Clan.PlayerClan);
                     else ChangeKingdomAction.ApplyByLeaveKingdom(Clan.PlayerClan);
                  });
               ModLog.Info($"[ARREST] the outlaw leaves {realm.Name}: {(door.Happened ? "left" : door.Verdict.ToString())}.");
            }

            MakeWanted(realm);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] outlawing the player failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Raises the realm's crime rating past the game's war threshold. For a player leading an independent clan the
      ///   game then declares war itself (ChangeCrimeRatingAction's own rule); a sworn vassal only becomes wanted.
      /// </summary>
      private static void MakeWanted(IFaction realm)
      {
         float threshold = Campaign.Current?.Models?.CrimeModel?.DeclareWarCrimeRatingThreshold ?? 60f;
         float needed = threshold + 5f - realm.MainHeroCrimeRating;
         if (needed > 0f) ChangeCrimeRatingAction.Apply(realm, needed);
      }

      /// <summary>
      ///   Each week, each realm with a price on the player's head may declare an amnesty (OutlawPolicy): the bounty
      ///   is lifted, the crime rating cleared, the war it brought ended, and an outlaw who fled that realm pardoned.
      /// </summary>
      private void OnWeeklyTick()
      {
         try
         {
            foreach (Bounty bounty in _state.Bounties.ToList())
               if (OutlawPolicy.AmnestyDeclared(MBRandom.RandomFloat))
                  LiftBounty(bounty, $"{RealmName(bounty.RealmId)} has declared an amnesty. The price on your head there is lifted.", "amnesty");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] weekly amnesty failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Lifts a realm's price on the player's head, whatever lifted it (an amnesty, a payment, the option turned off):
      ///   the bounty, the crime rating it raised, the war it brought on an independent clan, a fled outlaw's case, and
      ///   the realm's hunters. One road, so a bounty can never be half lifted.
      /// </summary>
      private void LiftBounty(Bounty bounty, string notice, string why)
      {
         _state.Bounties.Remove(bounty);
         TransientPartyRegistry.Instance?.Release(TransientPartyRegistry.Instance.WithTag(HuntersRole, bounty.RealmId));
         IFaction realm = Campaign.Current?.Factions?.FirstOrDefault(f => f.StringId == bounty.RealmId);
         if (realm == null) return;

         if (realm.MainHeroCrimeRating > 0f) ChangeCrimeRatingAction.Apply(realm, -realm.MainHeroCrimeRating);
         IFaction mine = Hero.MainHero?.MapFaction;
         if (mine != null && mine != realm && realm.IsAtWarWith(mine) && mine.Leader == Hero.MainHero && Clan.PlayerClan.Kingdom == null)
            GameState.DiplomacyCommit.Factions(Logic.Diplomacy.DiplomacyChange.MakePeace, "arrest", realm, mine, null, () => MakePeaceAction.Apply(realm, mine));

         if (Case.Stage == ArrestStage.Outlaw && Liege()?.MapFaction == realm) Case.Pardon();

         MBInformationManager.AddQuickInformation(new TextObject(notice));
         ModLog.Info($"[ARREST] {realm.Name}'s bounty lifted ({why}).");
      }

      /// <summary>
      ///   froggyluv (22/10/2026): the option off and the hunters still came. Off, no bounty stands, unless the law already
      ///   holds the player. Called each hour with the option's value (cr.test all passes it).
      /// </summary>
      internal void LiftBountiesWhenOff(bool arrestsOn)
      {
         if (_state.Bounties.Count == 0 || !OutlawPolicy.BountiesLiftedWhenOff(arrestsOn, HoldsPlayer)) return;

         foreach (Bounty bounty in _state.Bounties.ToList())
            LiftBounty(bounty, $"With arrests and outlaws turned off, the price on your head in {RealmName(bounty.RealmId)} is lifted.", "the option is off");
      }

      private static string RealmName(string realmId)
         => Campaign.Current?.Factions?.FirstOrDefault(f => f.StringId == realmId)?.Name?.ToString() ?? realmId;

      /// <summary>
      ///   One hour of the law's hold: an escape is a flight; the trial is convened once, when the player is in the
      ///   cells and nothing else owns the screen; a council that rose without a verdict is judged by its bench's
      ///   regard; a served sentence ends in release.
      /// </summary>
      private void TickHeld()
      {
         if (_freeing) return;

         if (!Hero.MainHero.IsPrisoner)
         {
            // Never put in the cells at all (no town or castle could hold them): no one to escape from, so the case
            // is dropped rather than turned into an outlawry the player never earned.
            if (string.IsNullOrEmpty(Case.PrisonSettlementId))
            {
               Case.Verdict(false, Today);
               ModLog.Info("[ARREST] the player was never held; the case is dropped.");

               return;
            }

            IFaction fledRealm = Liege()?.MapFaction;
            Case.Escape();
            MBInformationManager.AddQuickInformation(new TextObject("You have broken out of the law's hold. From this day you are an outlaw, and there is a price on your head."));
            ModLog.Info("[ARREST] the player escaped the law's hold: outlaw, with a bounty.");
            BecomeOutlaw(fledRealm, BountyReason.Flight);

            return;
         }

         if (Case.Stage == ArrestStage.Imprisoned)
         {
            if (!Case.SentenceServed(Today)) return;

            Hero liege = Liege();
            Case.Release();
            FreePlayer(liege);
            MBInformationManager.AddQuickInformation(new TextObject("Your sentence is served. The gaolers open the door, and you are free."));
            ModLog.Info("[ARREST] sentence served; the player is released.");

            return;
         }

         // On trial. Nothing may be forced over another screen.
         if (ChatManager.IsOpen || CouncilConversationStager.IsStaging || ChatSceneStager.IsStaging || Mission.Current != null) return;

         if (Case.TrialOpened)
         {
            // The council was convened and has risen, and no verdict reached the case: the bench decides in the
            // accused's absence, on its own regard. Closing the window never buys an acquittal.
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            List<int> regards = TrialBench().Select(h => store?.Get(h.StringId)?.ReputationWithPlayer ?? 0).ToList();
            bool convicted = TrialVerdictPolicy.DefaultConviction(regards);
            ModLog.Info($"[ARREST] the trial rose without a verdict; the bench decides in the player's absence: {(convicted ? "guilty" : "acquitted")}.");
            ApplyVerdict(convicted);

            return;
         }

         if (TrialBench().Count == 0)
         {
            // No liege to answer to any more (dead, gone): the case cannot be tried, and the player is let go.
            ModLog.Info("[ARREST] no bench can be seated (no liege); the case is dropped.");
            ApplyVerdict(false);

            return;
         }

         // The world's initiative (29/10/2026): the trial is part of the arrest, imposed; it waits out the settling delay.
         if (!(Initiative.WorldInitiativeBehavior.Instance?.MayKnock(TrialKey, Logic.Initiative.InitiativeFamily.WarAndLaw, Logic.Initiative.InitiativeRank.Imposed, Liege()) ?? true))
            return;

         Case.TrialOpened = true;
         string result = CouncilConversationStager.StageCouncil(CouncilScope.Trial);
         if (result == null || !result.StartsWith("Convening", StringComparison.Ordinal))
         {
            Case.TrialOpened = false; // refused for now (another screen, a staging in flight): try again next hour
            ModLog.Info($"[ARREST] the trial could not be convened yet: {result}");

            return;
         }

         MBInformationManager.AddQuickInformation(new TextObject($"You are brought in chains before {Liege()?.Name?.ToString() ?? "your liege"}'s council."));
         ModLog.Info("[ARREST] the trial is convened.");
      }

      /// <summary>
      ///   Each realm with a bounty keeps one band of hunters on the player's trail, sent again only after a cooldown
      ///   once a band is lost, and steered each hour to engage the player. Nothing while the player is at sea or a
      ///   prisoner.
      /// </summary>
      private void TickHunters()
      {
         MobileParty main = MobileParty.MainParty;
         if (main == null || Hero.MainHero.IsPrisoner || main.IsCurrentlyAtSea || _state.Bounties.Count == 0) return;

         // A band riding when the player joins another lord's army is called off, its cooldown untouched: no hunt was had.
         if (!OutlawPolicy.HuntersMayRide(new HuntFacts {
                ArrestsOn = ModSettings.EnableArrests, InAnotherLeadersArmy = main.Army != null && main.Army.LeaderParty != main
             }))
         {
            DespawnHunters();

            return;
         }

         foreach (Bounty bounty in _state.Bounties.ToList())
         {
            MobileParty hunters = TransientPartyRegistry.Instance?.WithTag(HuntersRole, bounty.RealmId);

            if (hunters == null)
            {
               int? last = _state.HuntersSentDay.TryGetValue(bounty.RealmId, out int d) ? d : (int?) null;
               if (!OutlawPolicy.MaySendHunters(last, Today)) continue;

               hunters = SendHunters(bounty.RealmId);
               if (hunters == null) continue;
            }

            hunters.Ai.SetDoNotMakeNewDecisions(false);
            hunters.SetMoveEngageParty(main, MobileParty.NavigationType.Default);
            hunters.Ai.SetDoNotMakeNewDecisions(true);
         }
      }

      /// <summary>
      ///   Sends a realm's bounty hunters from its town nearest the player: in the realm's own colours against an
      ///   independent clan it is at war with, under an outlaw clan against a player still sworn to a kingdom.
      /// </summary>
      private MobileParty SendHunters(string realmId)
      {
         IFaction realm = Campaign.Current?.Factions?.FirstOrDefault(f => f.StringId == realmId);
         MobileParty main = MobileParty.MainParty;
         Hero sovereign = realm?.Leader;
         if (realm == null || main == null || sovereign == null) return null;

         bool independent = Clan.PlayerClan?.Kingdom == null;
         Clan colours = OutlawPolicy.ModeFor(independent) == HuntMode.War
            ? sovereign.Clan
            : Clan.BanditFactions?.FirstOrDefault(c => c.StringId == "looters") ?? Clan.BanditFactions?.FirstOrDefault();
         if (colours == null) return null;

         Vec2 player = main.GetPosition2D;
         Settlement origin = Settlement.All
                                       .Where(s => s.IsTown && s.MapFaction == realm)
                                       .OrderBy(s => s.GetPosition2D.Distance(player))
                                       .FirstOrDefault();
         if (origin == null) return null;

         int strength = OutlawPolicy.HunterStrength(main.MemberRoster?.TotalManCount ?? 0);
         MobileParty hunters = SpawnHunters(realm, origin.GetPosition2D, origin, strength, colours);
         if (hunters == null) return null;

         // Restored where they stood once a save is over, so a save never sends them back to their town.
         TransientPartyRegistry.Instance?.Track(hunters, HuntersRole, realmId,
            onRestore: at => SpawnHunters(realm, at, origin, strength, colours));
         _state.HuntersSentDay[realmId] = Today;
         MBInformationManager.AddQuickInformation(new TextObject($"Word reaches you: {realm.Name} has sent bounty hunters after you."));
         ModLog.Info($"[ARREST] {realm.Name} sends {strength} bounty hunters from {origin.Name} (under {colours.Name}).");

         return hunters;
      }

      /// <summary>A band of the realm's bounty hunters, spawned at <paramref name="at" /> (the registry tracks it).</summary>
      private static MobileParty SpawnHunters(IFaction realm, Vec2 at, Settlement home, int strength, Clan colours)
      {
         Hero sovereign = realm?.Leader;
         if (sovereign == null || colours == null) return null;

         return TravelingParty.Spawn(at, home, new TextObject($"Bounty hunters of {realm.Name}"),
            SergeantSpeed, sovereign.Culture, false, strength, colours, sovereign);
      }

      /// <summary>
      ///   The player is taken. If by a realm that wants them (its hunters, or any of its parties), the bounty is
      ///   collected and the handover to that realm's sovereign is armed for the next hour.
      /// </summary>
      private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
      {
         try
         {
            if (prisoner != Hero.MainHero || capturer == null || HoldsPlayer) return;

            string realmId = null;
            TransientPartyEntry hunterEntry = TransientPartyRegistry.Instance?.EntryOf(capturer.MobileParty);
            if (hunterEntry?.Role == HuntersRole) realmId = hunterEntry.Tag;
            else if (capturer.MapFaction != null && _state.Bounties.Any(b => b.RealmId == capturer.MapFaction.StringId)) realmId = capturer.MapFaction.StringId;

            if (realmId == null) return;

            _pendingHandoverRealmId = realmId;
            ModLog.Info($"[ARREST] the player is taken on {realmId}'s bounty by {capturer.Name}.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] reading the capture failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Hands a player taken on a bounty to the realm's sovereign: the bounty is collected, the case is opened in
      ///   that sovereign's name with the bounty's cause charged, and the player is moved to the cells of the
      ///   sovereign's nearest town or castle, where TickHeld convenes the trial.
      /// </summary>
      private void HandOverToSovereign()
      {
         string realmId = _pendingHandoverRealmId;
         _pendingHandoverRealmId = null;

         try
         {
            if (!Hero.MainHero.IsPrisoner) return;

            IFaction realm = Campaign.Current?.Factions?.FirstOrDefault(f => f.StringId == realmId);
            Bounty bounty = _state.Bounties.FirstOrDefault(b => b.RealmId == realmId);
            Hero sovereign = realm?.Leader;
            if (realm == null || bounty == null || sovereign == null) return;

            _state.Bounties.Remove(bounty);
            Case.CaughtOnBounty(sovereign.StringId, new[] {OutlawPolicy.ChargeFor(bounty.Reason)});

            Vec2 at = MobileParty.MainParty?.GetPosition2D ?? sovereign.GetMapPoint()?.Position.ToVec2() ?? Vec2.Zero;
            Settlement prison = Settlement.All
                                          .Where(s => (s.IsTown || s.IsCastle) && s.Party != null && s.OwnerClan == sovereign.Clan)
                                          .OrderBy(s => s.GetPosition2D.Distance(at))
                                          .FirstOrDefault()
                                ?? Settlement.All
                                             .Where(s => (s.IsTown || s.IsCastle) && s.Party != null && s.MapFaction == realm)
                                             .OrderBy(s => s.GetPosition2D.Distance(at))
                                             .FirstOrDefault();
            if (prison != null)
            {
               Case.PrisonSettlementId = prison.StringId;
               PlayerCaptivity.CaptorParty = prison.Party; // the engine's own custody transfer
            }

            // Only now, the player out of their hands: a band still holding its captive is never taken off the map.
            if (prison != null) DespawnHunters();

            MBInformationManager.AddQuickInformation(new TextObject(
               $"The price on your head is collected. You are handed over to {sovereign.Name}, to be tried in {prison?.Name?.ToString() ?? "their hall"}."));
            ModLog.Info($"[ARREST] handed over to {sovereign.Name} for trial in {prison?.Name?.ToString() ?? "(no prison found)"}.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] handing the player over failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Takes every band of hunters off the map (once the player is held; a save is the registry's own).</summary>
      private static void DespawnHunters() => TransientPartyRegistry.Instance?.ReleaseRole(HuntersRole);

      /// <summary>Ends the player's captivity at the law's own hand (acquittal, or the sentence served).</summary>
      private void FreePlayer(Hero liege)
      {
         if (!Hero.MainHero.IsPrisoner) return;

         _freeing = true;
         try { EndCaptivityAction.ApplyByReleasedByChoice(Hero.MainHero, liege); }
         catch (Exception ex) { ModLog.Error($"[ARREST] releasing the player failed: {ex.GetType().Name}: {ex.Message}"); }
         finally { _freeing = false; }
      }

      private Hero Liege() => string.IsNullOrEmpty(Case.LiegeId) ? null : Hero.FindFirst(h => h != null && h.StringId == Case.LiegeId);

      private Settlement Prison() => string.IsNullOrEmpty(Case.PrisonSettlementId) ? null : Settlement.Find(Case.PrisonSettlementId);

      private static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>
      ///   Sends the sergeants from the liege's realm's town nearest the player (else from the liege's own position), in
      ///   the liege's colours and culture.
      /// </summary>
      private MobileParty SendSergeants()
      {
         Hero liege = Hero.FindFirst(h => h != null && h.StringId == Case.LiegeId);
         MobileParty main = MobileParty.MainParty;
         if (liege == null || main == null) return null;

         Vec2 player = main.GetPosition2D;
         Settlement origin = Settlement.All
                                       .Where(s => s.IsTown && s.MapFaction != null && s.MapFaction == liege.MapFaction)
                                       .OrderBy(s => s.GetPosition2D.Distance(player))
                                       .FirstOrDefault();
         Vec2 from = origin?.GetPosition2D ?? liege.GetMapPoint()?.Position.ToVec2() ?? player;

         MobileParty sergeants = SpawnSergeants(liege, from, origin ?? liege.HomeSettlement);
         if (sergeants == null) return null;

         // Restored where they stood once a save is over (26/09/2026: before, a save sent them back to their town on
         // the next hour, so with frequent autosaves sergeants riding from far off might never reach the player).
         Settlement home = origin ?? liege.HomeSettlement;
         TransientPartyRegistry.Instance?.Track(sergeants, SergeantsRole, onDestroyed: OnSergeantsDestroyed,
            onRestore: at => SpawnSergeants(liege, at, home));
         ModLog.Info($"[ARREST] sergeants of {liege.Name} ride from {origin?.Name?.ToString() ?? "the liege's side"} after the player.");

         return sergeants;
      }

      /// <summary>The liege's sergeants, spawned at <paramref name="at" /> (the registry tracks them).</summary>
      private static MobileParty SpawnSergeants(Hero liege, Vec2 at, Settlement home)
         => liege == null
            ? null
            : TravelingParty.Spawn(at, home, new TextObject($"Sergeants of {liege.Name}"),
               SergeantSpeed, liege.Culture, false, SergeantCount, liege.Clan, liege);

      private static MobileParty Sergeants() => TransientPartyRegistry.Instance?.OfRole(SergeantsRole).FirstOrDefault();

      /// <summary>Takes the sergeants off the map (on arrival, or once no warrant is out; a save is the registry's own).</summary>
      private static void DespawnSergeants() => TransientPartyRegistry.Instance?.ReleaseRole(SergeantsRole);

      /// <summary>Beating the sergeants off is refusing the warrant (the registry reports only a TRUE loss).</summary>
      private void OnSergeantsDestroyed(MobileParty destroyed, PartyBase destroyer)
      {
         bool byPlayer = destroyer == PartyBase.MainParty || destroyer?.MobileParty?.ActualClan == Clan.PlayerClan;
         if (byPlayer && Case.Stage == ArrestStage.WarrantOut) Refuse();
      }

      /// <summary>What is saved: the case, and whether the crime rating was already severe yesterday.</summary>
      private sealed class SavedState
      {
         public ArrestCase Case { get; set; } = new();
         public bool CrimeWasSevere { get; set; }

         /// <summary>Every realm's price on the player's head.</summary>
         public List<Bounty> Bounties { get; set; } = new();

         /// <summary>
         ///   Realm id -> the day its last band was sent, so a lost band is not replaced the next morning. Saved since
         ///   23/10/2026 (froggyluv): unsaved, every load sent a fresh band at once. Absent (null) on an older save.
         /// </summary>
         public Dictionary<string, int> HuntersSentDay { get; set; } = new();
      }

      #endregion
   }
}
