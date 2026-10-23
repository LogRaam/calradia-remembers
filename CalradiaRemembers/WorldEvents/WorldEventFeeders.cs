// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Battles;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Battles;
using CalradiaRemembers.Logic.RealmTidings;
using CalradiaRemembers.Stance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>
   ///   Subscribes to the campaign events that matter for rumor and chronicle, and records a
   ///   <see cref="WorldEvent" /> for each. Owned by <see cref="NpcMemoryCampaignBehavior" /> and
   ///   wired on session launch (mirrors <c>LetterTriggerEvaluator.Subscribe</c>).
   /// </summary>
   public sealed class WorldEventFeeders
   {
      private const int MaxLordsPerKingdomScan = 40; // bound the O(n^2) intra-kingdom pass
      private const int MaxRivalriesPerWeek = 2; // ambient, not a flood

      private const int RivalryCooldownDays = 40; // don't re-gossip the same feud too soon

      // ── Rivalry gossip (feeder #3) ────────────────────────────────────────
      private const int RivalryThreshold = -30; // mutual relation at/below = bitter rivals

      private readonly Dictionary<string, int> _rivalryGossipDay = new();

      // The party that held the player when a battle set them loose. Buffered because the engine reports the
      // release BEFORE the map event that caused it, so the liberator is not knowable yet at release time
      // (the same ordering RescueBehavior relies on). Cleared by the next map event either way: resolved
      // into a named liberator, or flushed as the anonymous wording, because losing the chronicle entry
      // outright would be worse than keeping it nameless.
      private PartyBase _pendingPlayerCaptor;
      private bool _playerReleaseAwaitingBattle;

      // The player got out DURING the fight rather than being cut loose by its winner, which is what the
      // engine reports for "you slipped away in the confusion". Changes the wording, not the credit.
      private bool _playerBrokeFree;

      // The settlement the player just helped storm (fought on the winning attacking side of a siege
      // assault) and the day it happened. OnSettlementOwnerChanged consumes this to recognise a mere
      // besieging PARTICIPANT, as opposed to the officially credited capturer (the only case the old code
      // recognised), so the conquest is never relayed back to the player as unbidden "news". Player report:
      // "the monarch notified me per letter of a successful siege ... when I was part of the force that
      // took it." See SiegeParticipationMarker for the pure match rule (settlement + recency).
      private string _pendingPlayerSiegeSettlementId;
      private int _pendingPlayerSiegeDay;

      private static string PlayerName => Hero.MainHero?.Name?.ToString() ?? "the player";

      private static WorldEventStore Store => WorldEventStore.Instance;
      private static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>
      ///   Marks every event recording that the player's clan left a realm, whatever the way out, so "how long ago
      ///   did the player leave this realm" is one query on the persisted store (<see cref="DaysSincePlayerLeft" />).
      /// </summary>
      public const string PlayerLeftRealmStatus = "player_left_realm";

      /// <summary>
      ///   Days since the player's clan last left <paramref name="kingdom" />, or null when it never has (as far as
      ///   the store knows: departures before 2.5.16 carry no marker, so an old save reads "never", the permissive
      ///   answer).
      /// </summary>
      public static int? DaysSincePlayerLeft(Kingdom kingdom, int today)
      {
         if (kingdom == null || Store == null) return null;

         string id = kingdom.StringId;
         WorldEvent left = Store.MostRecentMatching(WorldEventType.PlayerFeat,
            e => e.Status == PlayerLeftRealmStatus && e.FactionIds != null && e.FactionIds.Contains(id));

         return left == null ? (int?) null : Math.Max(0, today - left.Day);
      }

      public void Subscribe()
      {
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
         CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnHeroesMarried);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
         CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
         CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
         CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
         CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
         CampaignEvents.RulingClanChanged.AddNonSerializedListener(this, OnRulingClanChanged);
         CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
         CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
         ModLog.Info("WorldEventFeeders subscribed.");
      }

      #region private

      private static List<string> FactionIds(params Hero[] heroes)
         => heroes.Where(h => h?.MapFaction != null).Select(h => h.MapFaction.StringId).Distinct().ToList();

      private static List<string> Ids(params Hero[] heroes)
         => heroes.Where(h => h != null).Select(h => h.StringId).ToList();

      private static bool IsBandit(Hero h) => h?.MapFaction?.IsBanditFaction == true;

      private static string LeaderName(Hero h) => h?.Name == null ? "a host" : HeroLabel.Describe(new HeroLabel.HeroNaming {Name = h.Name.ToString(), ClanName = h.Clan?.Name?.ToString()});

      // Names a hero actor for a Summary line with the clan disambiguator (rumor/news phrasing must
      // never let a namesake, such as a lord and the player's own young son, be read as the same
      // person). Empty when the hero itself is null, so callers keep their own sentence-start fallback.
      private static string Named(Hero h) => HeroLabel.Describe(new HeroLabel.HeroNaming {Name = h?.Name?.ToString(), ClanName = h?.Clan?.Name?.ToString()});

      private static string PairKey(Hero a, Hero b)
         => string.CompareOrdinal(a.StringId, b.StringId) < 0
            ? a.StringId + "|" + b.StringId
            : b.StringId + "|" + a.StringId;

      /// <summary>
      ///   Leaves a memory of the player's battlefield in the profiles of those who were present: the lords
      ///   who led their own party (either side), and the companions who rode inside the player's own party.
      /// </summary>
      private static void RecordBattlePerformanceIntoProfiles(MapEvent mapEvent, bool playerAttacker, bool playerWon, string placeClause, bool sharedEffort, string engagement)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null) return;

         BattleSideEnum playerSide = playerAttacker
            ? BattleSideEnum.Attacker
            : BattleSideEnum.Defender;
         int day = Today;

         // Inc 2 (battle-memory enrichment): the deeds the player actually did in this fight, distilled to a few
         // first-hand lines. Consumed ONCE here and appended to everyone who witnessed the battle, so the generic
         // "we fought together" line above is joined by what actually happened. Empty for an auto-resolved battle
         // (no mission, so no per-combatant truth to report) and for a fight that left nothing of note.
         // Rendered PER RECIPIENT: the man a deed is ABOUT lived it, and must not read "I saw you best Lucon" in
         // Lucon's own memory.
         // tashmetu (Nexus, 13/10/2026): the feat reporter's own "at " contract (BattleFeatText) wants the NOUN
         // PHRASE from ChronicleEngagement ("the siege of X"), never placeClause (already shaped as " at the
         // siege of X"); passing placeClause here doubled the preposition into "cut down 168 of them at at the
         // siege of Thorios Castle".
         BattleFeatReport deeds = BattleFeatReporter.ConsumeReport(engagement);

         foreach (BattleSideEnum side in new[] {BattleSideEnum.Attacker, BattleSideEnum.Defender})
         {
            MBReadOnlyList<MapEventParty> parties = mapEvent.GetMapEventSide(side)?.Parties;

            if (parties == null) continue;
            bool ally = side == playerSide;

            foreach (MapEventParty mp in parties)
            {
               Hero h = mp.Party?.LeaderHero;

               if (h == null || h == Hero.MainHero || IsBandit(h)) continue;

               // Enemy lords faced, and allied lords who led their own party, are recorded. On the
               // player's OWN side a companion or kin leading a DETACHED party is a wanderer (not IsLord)
               // yet one of the player's own people, so they earn the ally memory too; everywhere else a
               // real lord is still required. (Companions riding inside the main party are handled below.)
               bool playerKin = ally && h.Clan != null && h.Clan == Hero.MainHero?.Clan;

               if (!h.IsLord && !playerKin) continue;
               NpcProfile profile = behavior.Store.Get(h.StringId);

               if (profile == null) continue; // only those the player has actually met

               // NB: the enemy-lost phrase "put their host to flight" is also read by
               // EncounterContextBuilder.LostBattleToPlayerRecently to feed the POWER BALANCE
               // note — keep the two in sync if this wording changes.
               string summary = ally
                  ? BattleMemoryText.AllySummary(playerWon, placeClause, sharedEffort, PlayerName)
                  : $"Faced {PlayerName} in battle and {(playerWon ? "saw them put my host to flight" : "held the field against them")}{placeClause}.";
               profile.Events.Add(new NotableEvent(day, NotableEventType.Other, summary));

               // They were there, so they speak of the deeds first-hand (the LIVED register).
               foreach (string deed in deeds.LinesFor(h.StringId))
                  profile.Events.Add(new NotableEvent(day, NotableEventType.Other, deed));

               // Stance: a victory the player won earns respect (and, from a beaten foe, fear).
               if (playerWon)
               {
                  if (ally) StanceService.OnFoughtBesideYou(profile);
                  else StanceService.OnDefeatedInBattle(profile);
               }

               behavior.Store.Set(profile);
            }
         }

         // Companions ride INSIDE the player's own party, so they are never party LEADERS and the loop
         // above (which reads each party's leader) never visits them. A player asked for the "fought
         // together" memory to reach them too, so this is its own seam a self-test can drive directly
         // (see DebugRecordCompanionBattleMemories) without staging a real battle.
         RecordCompanionBattleMemories(day, playerWon, placeClause, sharedEffort, deeds);
      }

      /// <summary>
      ///   Leaves the "fought at your side" ally memory on the profiles of the player's own companions, who ride
      ///   INSIDE the main party and so are never the party LEADERS the leader loop in
      ///   <see cref="RecordBattlePerformanceIntoProfiles" /> visits. Family riding with the party qualifies the
      ///   same way; the notable-battle gate upstream already keeps this from firing on every skirmish. Split out
      ///   (behaviour unchanged) so the self-test seam <see cref="DebugRecordCompanionBattleMemories" /> can drive
      ///   exactly this path against a real party without a staged <see cref="MapEvent" />.
      /// </summary>
      private static void RecordCompanionBattleMemories(int day, bool playerWon, string placeClause, bool sharedEffort,
         BattleFeatReport deeds = null)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null) return;

         var roster = MobileParty.MainParty?.MemberRoster?.GetTroopRoster();

         if (roster == null) return;

         foreach (var el in roster)
         {
            Hero h = el.Character?.HeroObject;

            if (h == null || h == Hero.MainHero || IsBandit(h)) continue;
            NpcProfile companion = OwnPeopleProfile(behavior, h);

            if (companion == null) continue; // only companions the player has actually met

            companion.Events.Add(new NotableEvent(day, NotableEventType.Other, BattleMemoryText.AllySummary(playerWon, placeClause, sharedEffort, PlayerName)));

            // A companion rode at the player's shoulder, so they witnessed the deeds too.
            if (deeds != null)
               foreach (string deed in deeds.LinesFor(h.StringId))
                  companion.Events.Add(new NotableEvent(day, NotableEventType.Other, deed));

            // A victory earns the same respect a lord who fought beside the player would feel.
            if (playerWon) StanceService.OnFoughtBesideYou(companion);

            behavior.Store.Set(companion);
         }
      }

      /// <summary>
      ///   A fight the player fought that the chronicle does not record (a bandit band, a patrol, a lord's lone party):
      ///   the companions riding in the main party fold it into their running "small fights" memory. No stance moves
      ///   and no deed lines are written; a skirmish is remembered, not celebrated.
      /// </summary>
      private static void RecordCompanionSkirmish(MapEvent mapEvent, bool playerAttacker)
      {
         BattleSideEnum playerSide = playerAttacker
            ? BattleSideEnum.Attacker
            : BattleSideEnum.Defender;
         BattleSideEnum foeSide = playerAttacker
            ? BattleSideEnum.Defender
            : BattleSideEnum.Attacker;

         MapEventSide other = mapEvent.GetMapEventSide(foeSide);
         PartyBase foeParty = other?.LeaderParty;
         Hero foeLord = SideLeader(mapEvent, foeSide);
         bool lordLed = foeLord is {IsLord: true} && !IsBandit(foeLord);
         string foe = SkirmishMemoryText.Foe(new SkirmishFoe {
            LordName = lordLed ? LeaderName(foeLord) : null,
            PartyName = foeParty?.Name?.ToString(),
            IsBandit = foeParty?.MapFaction?.IsBanditFaction == true,
            FactionName = foeParty?.MapFaction?.Name?.ToString()
         });

         Settlement place = mapEvent.MapEventSettlement;
         BattleSetting setting = mapEvent.EventType == MapEvent.BattleTypes.Raid
            ? BattleSetting.Raid
            : BattleSetting.OpenField;

         RecordCompanionSkirmishMemories(Today, new SkirmishFacts {
            PlayerName = PlayerName,
            Foe = foe,
            PlayerWon = mapEvent.WinningSide == playerSide,
            PlaceClause = BattleMemoryText.PlaceClause(setting, place?.Name?.ToString())
         });
      }

      /// <summary>
      ///   Folds one small fight into the "small fights" memory of every companion riding in the main party (the
      ///   same companions <see cref="RecordCompanionBattleMemories" /> visits), or starts that memory. Returns how
      ///   many companions it reached. Also the cr.testall seam (companion_skirmish_memory).
      /// </summary>
      internal static int RecordCompanionSkirmishMemories(int day, SkirmishFacts fight)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null || fight == null) return 0;

         var roster = MobileParty.MainParty?.MemberRoster?.GetTroopRoster();

         if (roster == null) return 0;

         var reached = 0;

         foreach (var el in roster)
         {
            Hero h = el.Character?.HeroObject;

            if (h == null || h == Hero.MainHero || IsBandit(h)) continue;
            NpcProfile companion = OwnPeopleProfile(behavior, h);

            if (companion?.Events == null) continue; // only companions the player has actually met

            int at = companion.Events.FindLastIndex(e => SkirmishMemoryText.CountIn(e?.summary, fight.PlayerName) > 0);
            var count = 1;

            if (at >= 0 && SkirmishMemoryText.FoldsInto(new SkirmishLine {Day = companion.Events[at].gameDay, Summary = companion.Events[at].summary, PlayerName = fight.PlayerName}, day))
            {
               count = SkirmishMemoryText.CountIn(companion.Events[at].summary, fight.PlayerName) + 1;
               companion.Events.RemoveAt(at);
            }

            companion.Events.Add(new NotableEvent(day, NotableEventType.Other, SkirmishMemoryText.Line(count, fight)));
            behavior.Store.Set(companion);
            reached++;
         }

         return reached;
      }

      /// <summary>
      ///   The profile of someone riding in the main party, created for the player's own clan (companions, kin) when
      ///   none exists yet. A profile used to be made only by a first Calradia Remembers conversation, so a companion
      ///   hired through the game's own dialogue and never spoken to here remembered no battle at all (Thoragoros1,
      ///   15/10/2026). Riding in the player's party is having been met. Anyone else still needs a profile already.
      /// </summary>
      private static NpcProfile OwnPeopleProfile(NpcMemoryCampaignBehavior behavior, Hero h)
      {
         NpcProfile profile = behavior.Store.Get(h.StringId);

         if (profile != null || h.Clan == null || h.Clan != Clan.PlayerClan) return profile;

         profile = Mapping.HeroProfileMapper.FromHero(h);
         behavior.Store.Set(profile);
         ModLog.Info($"[WORLD] {h.Name} rides in the player's party with no profile yet; one is made so the fight is remembered.");

         return profile;
      }

      /// <summary>
      ///   Self-test seam (cr.testall companion_battle_memory): records the companion "fought at your side" memory
      ///   for the current main party as if a notable battle had just ended, and returns how many main-party
      ///   companion profiles gained a fresh memory this call. Drives the exact
      ///   <see cref="RecordCompanionBattleMemories" /> path the battle feeder uses; no <see cref="MapEvent" /> is
      ///   staged, and <c>near</c> is empty (no settlement).
      /// </summary>
      public static int DebugRecordCompanionBattleMemories(bool playerWon)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null) return 0;

         int day = Today;
         string expected = BattleMemoryText.AllySummary(playerWon, "", false, PlayerName);
         int before = CountCompanionAllyMemories(behavior, day, expected);
         RecordCompanionBattleMemories(day, playerWon, "", false);

         return CountCompanionAllyMemories(behavior, day, expected) - before;
      }

      /// <summary>Counts, across the main party's own companion profiles, the ally-memory events dated <paramref name="day" /> whose summary equals <paramref name="summary" /> (so a delta isolates the ones this pass added).</summary>
      private static int CountCompanionAllyMemories(NpcMemoryCampaignBehavior behavior, int day, string summary)
      {
         var roster = MobileParty.MainParty?.MemberRoster?.GetTroopRoster();

         if (roster == null) return 0;

         var count = 0;

         foreach (var el in roster)
         {
            Hero h = el.Character?.HeroObject;

            if (h == null || h == Hero.MainHero || IsBandit(h)) continue;
            NpcProfile companion = behavior.Store.Get(h.StringId);

            if (companion?.Events == null) continue;
            count += companion.Events.Count(e => e.gameDay == day && e.summary == summary);
         }

         return count;
      }

      private static void RecordDiplomacy(IFaction f1, IFaction f2, WorldEventType type, int magnitude, string verb)
      {
         if (f1 == null || f2 == null || Store == null) return;
         try
         {
            Store.Record(new WorldEvent {
               Type = type,
               Day = Today,
               Magnitude = magnitude,
               Summary = $"{f1.Name} {verb} {f2.Name}.",
               FactionIds = new List<string> {f1.StringId, f2.StringId}.Where(s => s != null).ToList()
            });
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] diplomacy feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // ── Helpers ───────────────────────────────────────────────────────────

      /// <summary>True when any party on this side of the battle belongs to an army.</summary>
      private static bool SideHasArmy(MapEvent mapEvent, BattleSideEnum side)
      {
         MBReadOnlyList<MapEventParty> parties = mapEvent.GetMapEventSide(side)?.Parties;

         if (parties == null) return false;

         foreach (MapEventParty p in parties)
            if (p.Party?.MobileParty?.Army != null)
               return true;

         return false;
      }

      /// <summary>Total combatants across both sides — feeds the player-battle size bypass in <see cref="BattleNotability" />.</summary>
      private static int TotalCombatants(MapEvent mapEvent)
         => (mapEvent.GetMapEventSide(BattleSideEnum.Attacker)?.TroopCount ?? 0) +
            (mapEvent.GetMapEventSide(BattleSideEnum.Defender)?.TroopCount ?? 0);

      /// <summary>Names a side for the world's record: its hero leader, else what the game knows of its party (BattleRecordText).</summary>
      private static string SideName(MapEvent mapEvent, BattleSideEnum side, Hero leader)
      {
         PartyBase party = mapEvent.GetMapEventSide(side)?.LeaderParty;

         return BattleRecordText.Side(new BattleSideFacts {
            LeaderName = leader?.Name == null ? null : LeaderName(leader),
            PartyName = party?.Name?.ToString(),
            IsBandit = party?.MapFaction?.IsBanditFaction == true,
            FactionName = party?.MapFaction?.IsBanditFaction == true ? null : party?.MapFaction?.Name?.ToString()
         });
      }

      /// <summary>The healthy men both sides brought to the field, read at the start (the end counts only survivors).</summary>
      private static int MenAtStart(MapEvent mapEvent)
         => (mapEvent.GetMapEventSide(BattleSideEnum.Attacker)?.HealthyTroopCountAtMapEventStart ?? 0) +
            (mapEvent.GetMapEventSide(BattleSideEnum.Defender)?.HealthyTroopCountAtMapEventStart ?? 0);

      /// <summary>The realms of both sides (bandits aside), so a leaderless side's own people hear of its fall.</summary>
      private static IEnumerable<string> SideFactionIds(MapEvent mapEvent)
         => new[] {BattleSideEnum.Attacker, BattleSideEnum.Defender}
            .Select(s => mapEvent.GetMapEventSide(s)?.LeaderParty?.MapFaction)
            .Where(f => f != null && !f.IsBanditFaction)
            .Select(f => f.StringId);

      /// <summary>The settlement closest to where the battle was fought (hideouts aside), if it is near enough to name.</summary>
      private static Settlement NearestSettlementTo(MapEvent mapEvent)
      {
         try
         {
            Vec2 at = mapEvent.Position.ToVec2();
            Settlement best = null;
            float bestDistance = BattleRecordText.NearPlaceMaxDistance;

            foreach (Settlement candidate in Settlement.All)
            {
               if (candidate == null || candidate.IsHideout) continue;

               float d = candidate.GetPosition2D.Distance(at);
               if (d >= bestDistance) continue;

               bestDistance = d;
               best = candidate;
            }

            return best;
         }
         catch { return null; }
      }

      private static Hero SideLeader(MapEvent mapEvent, BattleSideEnum side)
      {
         MapEventSide s = mapEvent.GetMapEventSide(side);
         Hero leader = s?.LeaderParty?.LeaderHero;

         if (leader != null) return leader;

         return s?.Parties?.Select(p => p.Party?.LeaderHero).FirstOrDefault(h => h is {IsLord: true});
      }

      // ── Marriages ─────────────────────────────────────────────────────────

      private void OnHeroesMarried(Hero a, Hero b, bool showNotification)
      {
         if (a == null || b == null || Store == null) return;
         try
         {
            bool playerIn = a == Hero.MainHero || b == Hero.MainHero;
            Hero other = a == Hero.MainHero
               ? b
               : a;
            var e = new WorldEvent {
               Type = playerIn
                  ? WorldEventType.PlayerFeat
                  : WorldEventType.Marriage,
               Day = Today,
               Magnitude = playerIn
                  ? 45
                  : 35,
               InvolvesPlayer = playerIn,
               Summary = $"{Named(a)} wed {Named(b)}.",
               ActorIds = Ids(a, b),
               FactionIds = FactionIds(a, b)
            };
            if (playerIn)
               e.ChronicleLine = $"{PlayerName} was joined in marriage to {other.Name}, that two houses might be made one";
            Store.Record(e);
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] marriage feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // ── Executions ────────────────────────────────────────────────────────

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         if (victim == null || Store == null) return;
         if (detail == KillCharacterAction.KillCharacterActionDetail.DiedInBattle)
         {
            RecordFellInBattle(victim);

            return;
         }
         if (!Mapping.ExecutionDetail.IsExecution(detail)) return; // only deliberate executions are news, after a battle too
         try
         {
            bool playerIn = killer == Hero.MainHero;
            var e = new WorldEvent {
               Type = playerIn
                  ? WorldEventType.PlayerFeat
                  : WorldEventType.Execution,
               Day = Today,
               Magnitude = 65,
               InvolvesPlayer = playerIn,
               Summary = killer != null
                  ? $"{Named(killer)} had {Named(victim)} executed."
                  : $"{Named(victim)} was executed.",
               ActorIds = Ids(victim, killer),
               FactionIds = FactionIds(victim, killer),
               // Influence loop: an execution by the player wounds the victim's own faction deeply.
               AggrievedFactionIds = playerIn
                  ? FactionIds(victim)
                  : new List<string>(),
               AggrievedActorIds = playerIn
                  ? Ids(victim)
                  : new List<string>()
            };
            if (playerIn)
               e.ChronicleLine = $"by the judgement of {PlayerName}, {victim.Name} was put to death, and men spoke of it long after";
            Store.Record(e);
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] execution feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   A lord killed in battle (Gabriel, 17/10/2026: lords killed in battle are history). The engine passes no killer
      ///   for a death in the field (the agent origins call ApplyByBattle with null), so the line names no slayer; the
      ///   place is the last settlement he was near. The player's own house is left to the battle memory, which knows
      ///   the fight. Recorded as news, so it reaches the long-term memory after 42 days.
      /// </summary>
      private void RecordFellInBattle(Hero victim)
      {
         try
         {
            if (victim == Hero.MainHero || !victim.IsLord || IsBandit(victim) || victim.Clan == null || victim.Clan == Clan.PlayerClan) return;

            Settlement near = victim.LastKnownClosestSettlement;
            Store.Record(new WorldEvent {
               Type = WorldEventType.Other, Day = Today, Magnitude = LongTermHistoryPolicy.FellInBattleMagnitude,
               Status = LongTermHistoryPolicy.FellInBattleStatus,
               Summary = near != null ? $"{Named(victim)} fell in battle near {near.Name}." : $"{Named(victim)} fell in battle.",
               ActorIds = Ids(victim),
               FactionIds = FactionIds(victim),
               PlaceId = near?.StringId
            });
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] fallen lord not recorded for {victim?.Name}: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnMakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
         => RecordDiplomacy(faction1, faction2, WorldEventType.PeaceDeclared, 60, "made peace with");

      // ── Battles ───────────────────────────────────────────────────────────

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         if (mapEvent == null || Store == null) return;
         try
         {
            if (mapEvent.WinningSide == BattleSideEnum.None) return;

            // Before the notability gate: a lord riding down a bandit band never clears it, and that is
            // exactly the battle that frees a captive player.
            ResolvePendingPlayerRelease(mapEvent);

            bool playerAttacker = mapEvent.AttackerSide?.IsMainPartyAmongParties() == true;
            bool playerDefender = mapEvent.DefenderSide?.IsMainPartyAmongParties() == true;
            bool playerIn = playerAttacker || playerDefender;

            bool isSiege = mapEvent.EventType == MapEvent.BattleTypes.Siege;
            bool attackerHasArmy = SideHasArmy(mapEvent, BattleSideEnum.Attacker);
            bool defenderHasArmy = SideHasArmy(mapEvent, BattleSideEnum.Defender);

            // Before the notability gate on purpose (mirrors ResolvePendingPlayerRelease above): the
            // settlement-owner-changed event that follows a captured siege fires regardless of whether THIS
            // battle clears the chronicle-worthy bar below, so the marker must be stamped here rather than
            // after the `if (!notable) return;` a few lines down.
            StashPlayerSiegeMarker(mapEvent, isSiege, playerAttacker);

            // Only a siege or a clash of two armies is worth recording (into world-events, the
            // chronicle, and lords' profiles) — a field skirmish or a bandit chase is noise that
            // would bloat the save. This also skips the per-lord profile memory + stance bumps below.
            // A PLAYER battle earns a further bypass on raw size alone (BattleNotability.IsNotableForPlayer):
            // a great scrap the player led is chronicle-worthy even without armies on both sides.
            bool notable = playerIn
               ? BattleNotability.IsNotableForPlayer(new BattleNotability.BattleFacts {IsSiege = isSiege, AttackerHasArmy = attackerHasArmy, DefenderHasArmy = defenderHasArmy, TotalCombatants = TotalCombatants(mapEvent)})
               : BattleNotability.IsNotable(new BattleNotability.BattleFacts {IsSiege = isSiege, AttackerHasArmy = attackerHasArmy, DefenderHasArmy = defenderHasArmy});

            // Thoragoros1 (Nexus, 15/10/2026): companions remembered none of the early fights. A fight the
            // chronicle does not record still happened to the companions who rode in it: they keep one folded
            // "small fights" memory (SkirmishMemoryText), and nothing else is written.
            if (!notable)
            {
               if (playerIn) RecordCompanionSkirmish(mapEvent, playerAttacker);

               return;
            }

            BattleSideEnum loseSide = mapEvent.WinningSide == BattleSideEnum.Attacker
               ? BattleSideEnum.Defender
               : BattleSideEnum.Attacker;

            Hero winLeader = SideLeader(mapEvent, mapEvent.WinningSide);
            Hero loseLeader = SideLeader(mapEvent, loseSide);

            // Need at least one real lord, and skip bandit-only affairs.
            bool winLord = winLeader is {IsLord: true} && !IsBandit(winLeader);
            bool loseLord = loseLeader is {IsLord: true} && !IsBandit(loseLeader);

            if (!winLord && !loseLord)
            {
               if (playerIn) RecordCompanionSkirmish(mapEvent, playerAttacker);

               return;
            }

            bool playerWon = playerIn &&
                             mapEvent.WinningSide ==
                             (playerAttacker
                                ? BattleSideEnum.Attacker
                                : BattleSideEnum.Defender);

            BattleSetting setting = isSiege
               ? BattleSetting.Siege
               : mapEvent.EventType == MapEvent.BattleTypes.Raid
                  ? BattleSetting.Raid
                  : BattleSetting.OpenField;
            // fkasad (16/10/2026): a field battle the game tied to no settlement read "in battle", nowhere. It is now
            // "near" the closest settlement, when one is close enough to be the place people would name.
            Settlement place = mapEvent.MapEventSettlement
                               ?? (setting == BattleSetting.OpenField ? NearestSettlementTo(mapEvent) : null);
            string placeClause = BattleMemoryText.PlaceClause(setting, place?.Name?.ToString());
            // The noun-phrase form (no leading preposition): the chronicle line below wants it, and so does the
            // battle-feat reporter, which already adds its own " at " (BattleFeatText's contract).
            string engagement = BattleMemoryText.ChronicleEngagement(setting, place?.Name?.ToString());
            // The player fought within a larger host when their own side fielded an army (more than one party), so a
            // siege or pitched battle won that way is a shared triumph, never the solo one lords used to congratulate.
            bool sharedEffort = playerAttacker ? attackerHasArmy : defenderHasArmy;
            // fkasad (16/10/2026): a side no lord led was "a host" whatever it was, and the size was never said.
            string winName = SideName(mapEvent, mapEvent.WinningSide, winLeader);
            string loseName = SideName(mapEvent, loseSide, loseLeader);
            int menAtStart = MenAtStart(mapEvent);
            int magnitude = BattleRecordText.Magnitude(mapEvent.MapEventSettlement != null, menAtStart);

            var e = new WorldEvent {
               Type = playerIn
                  ? WorldEventType.PlayerFeat
                  : WorldEventType.Battle,
               Day = Today,
               PlaceId = place?.StringId,
               Magnitude = playerIn
                  ? Math.Max(40, magnitude)
                  : magnitude,
               InvolvesPlayer = playerIn,
               Summary = BattleRecordText.Summary(winName, loseName, placeClause, menAtStart),
               // The long-term memory keeps a battle of several hundred men, or a siege (Gabriel, 17/10/2026, 7.7a).
               MenAtStart = menAtStart,
               IsSiege = isSiege,
               ActorIds = Ids(winLeader, loseLeader),
               FactionIds = FactionIds(winLeader, loseLeader).Union(SideFactionIds(mapEvent)).Distinct().ToList(),
               // Influence loop: a battle the player WON harms the beaten side; a loss harms no one
               // by the player's hand, so it aggrieves nobody (the foe who won is not the player's deed).
               AggrievedFactionIds = playerIn && playerWon
                  ? FactionIds(loseLeader)
                  : new List<string>(),
               AggrievedActorIds = playerIn && playerWon
                  ? Ids(loseLeader)
                  : new List<string>()
            };
            if (playerIn)
            {
               e.ChronicleLine = playerWon
                  ? $"the lord {PlayerName} met {loseName} in {engagement}, and by sword and valour put the foe to flight"
                  : $"the lord {PlayerName} gave battle to {winName} in {engagement}, but fortune turned against him and the field was lost";
            }

            Store.Record(e);

            if (playerIn) RecordBattlePerformanceIntoProfiles(mapEvent, playerAttacker, playerWon, placeClause, sharedEffort, engagement);
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] battle feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // ── Sieges / conquests ────────────────────────────────────────────────

      private void OnSettlementOwnerChanged(
         Settlement settlement,
         bool openToClaim,
         Hero newOwner,
         Hero oldOwner,
         Hero capturerHero,
         ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
      {
         if (settlement == null || Store == null) return;

         bool isSiege = detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege;
         // A fief handed to the player outside of conquest (a king's reward, a clan gift) is a
         // second chronicle-worthy path through this same feeder — everything else (barter,
         // rebellion, clan destruction) is left alone.
         bool isGrantToPlayer = !isSiege && newOwner == Hero.MainHero &&
                                (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByGift ||
                                 detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision);

         if (!isSiege && !isGrantToPlayer) return; // only conquests and grants are chronicle-worthy

         try
         {
            if (isSiege)
            {
               Hero taker = capturerHero ?? newOwner;
               RecordSiegeCapture(settlement, taker, newOwner, oldOwner);
            }
            else
            {
               // The liege who bestowed it is the settlement's own kingdom leader once the grant has
               // landed (a king's decision or a clan gift both flow from the crown in practice).
               Hero liege = (newOwner.MapFaction as Kingdom)?.Leader;
               string liegeName = liege?.Name?.ToString() ?? "the crown";
               Store.Record(new WorldEvent {
                  Type = WorldEventType.PlayerFeat,
                  Day = Today,
                  PlaceId = settlement.StringId,
                  Magnitude = settlement.IsTown ? 65 : settlement.IsCastle ? 50 : 35,
                  InvolvesPlayer = true,
                  Summary = $"{PlayerName} was granted {settlement.Name}.",
                  ActorIds = Ids(Hero.MainHero, liege),
                  FactionIds = FactionIds(Hero.MainHero, liege),
                  ChronicleLine = $"{liegeName} bestowed {settlement.Name} upon {PlayerName}, in reward for faithful service"
               });
            }
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] siege feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Records the SettlementCaptured/PlayerFeat WorldEvent for a siege capture. Splits TWO separate
      ///   facts that the old code conflated into one <c>playerIn</c> flag: <c>playerIsTaker</c> (the
      ///   player is the officially credited capturer, drives Type, ChronicleLine, and Aggrieved*, exactly
      ///   as before this fix) versus <c>playerParticipated</c> (the player fought for this conquest even
      ///   when someone else, an army leader, got the fief, which drives ONLY InvolvesPlayer, so the
      ///   realm-tidings feature never relays the player's own siege back to them as unbidden news, without
      ///   stealing the conquest's attribution from its real taker). Participation is proven by the fresh
      ///   "just stormed this settlement" marker OnMapEventEnded stamps for the assault case, or, failing
      ///   that, a cheap fallback for a starvation/surrender capture with no assault MapEvent: the player's
      ///   own party riding in the taker's army under a different leader. Driven directly by the self-test
      ///   seam <see cref="DebugRecordSiegeCapture" /> so it can be exercised without a real MapEvent.
      /// </summary>
      private WorldEvent RecordSiegeCapture(Settlement settlement, Hero taker, Hero newOwner, Hero oldOwner)
      {
         bool playerIsTaker = taker == Hero.MainHero;

         bool markerMatches = SiegeParticipationMarker.Matches(_pendingPlayerSiegeSettlementId, _pendingPlayerSiegeDay, settlement.StringId, Today);
         // Consumed on a match so a later, unrelated recapture of the same settlement can never
         // false-positive against a marker that already did its job here.
         if (markerMatches) _pendingPlayerSiegeSettlementId = null;

         bool playerParticipated = playerIsTaker || markerMatches || PlayerInTakersArmy(taker, newOwner);

         var e = new WorldEvent {
            Type = playerIsTaker
               ? WorldEventType.PlayerFeat
               : WorldEventType.SettlementCaptured,
            Day = Today,
            PlaceId = settlement.StringId,
            Magnitude = settlement.IsTown ? 70 : settlement.IsCastle ? 55 : 35,
            InvolvesPlayer = playerParticipated,
            Summary = $"{(taker == null ? "A host" : Named(taker))} took {settlement.Name}" +
                      (oldOwner != null
                         ? $" from {Named(oldOwner)}."
                         : "."),
            ActorIds = Ids(taker, oldOwner),
            FactionIds = FactionIds(taker, oldOwner),
            // Influence loop: a conquest by the player wounds the dispossessed lord's faction. Keyed on
            // playerIsTaker (not playerParticipated): a mere participant did not personally dispossess
            // anyone, so attribution must stay with the real taker.
            AggrievedFactionIds = playerIsTaker
               ? FactionIds(oldOwner)
               : new List<string>(),
            AggrievedActorIds = playerIsTaker
               ? Ids(oldOwner)
               : new List<string>()
         };
         if (playerIsTaker)
            e.ChronicleLine = $"{PlayerName} laid siege to {settlement.Name} and wrested it from {oldOwner?.Name?.ToString() ?? "its lord"}, and the banners were changed upon its walls";
         Store.Record(e);

         return e;
      }

      /// <summary>
      ///   Buffers "the player just stormed this settlement" when a siege assault's winning side was the
      ///   ATTACKER and the player fought on it: the signal <see cref="RecordSiegeCapture" /> later matches
      ///   against the settlement-owner-changed event the engine fires for the same conquest.
      /// </summary>
      private void StashPlayerSiegeMarker(MapEvent mapEvent, bool isSiege, bool playerAttacker)
      {
         if (!isSiege || !playerAttacker || mapEvent.WinningSide != BattleSideEnum.Attacker) return;

         Settlement stormed = mapEvent.MapEventSettlement;
         if (stormed?.StringId == null) return;

         _pendingPlayerSiegeSettlementId = stormed.StringId;
         _pendingPlayerSiegeDay = Today;
      }

      /// <summary>
      ///   Cheap fallback participation signal for a starvation/surrender capture, which has no assault
      ///   MapEvent to stamp <see cref="StashPlayerSiegeMarker" />'s marker: true when the player's own
      ///   party is riding inside the taker's army under a DIFFERENT leader (the player did not personally
      ///   take the fief, but marched with the force that did).
      /// </summary>
      private static bool PlayerInTakersArmy(Hero taker, Hero newOwner)
      {
         Hero armyLeader = MobileParty.MainParty?.Army?.LeaderParty?.LeaderHero;

         return armyLeader != null && (armyLeader == taker || armyLeader == newOwner);
      }

      /// <summary>
      ///   Self-test seam (cr.testall siege_tidings_participation): stamps the "player just stormed this
      ///   settlement" marker as <see cref="StashPlayerSiegeMarker" /> would from a real siege assault,
      ///   without needing a real <see cref="MapEvent" />.
      /// </summary>
      public void DebugSetPlayerSiegeMarker(string settlementId, int day)
      {
         _pendingPlayerSiegeSettlementId = settlementId;
         _pendingPlayerSiegeDay = day;
      }

      /// <summary>
      ///   Self-test seam (cr.testall siege_tidings_participation): runs the exact
      ///   <see cref="RecordSiegeCapture" /> construction <see cref="OnSettlementOwnerChanged" /> uses for a
      ///   siege, without a real MapEvent/ChangeOwnerOfSettlementAction, and returns the recorded event so
      ///   the caller can assert on it (and remove it from the store afterwards).
      /// </summary>
      public WorldEvent DebugRecordSiegeCapture(Settlement settlement, Hero taker, Hero oldOwner)
      {
         if (settlement == null || Store == null) return null;

         return RecordSiegeCapture(settlement, taker, taker, oldOwner);
      }

      // ── Political ascent (vassalage, defection, rulership) — player clan only ───

      /// <summary>
      ///   The player clan's standing with a kingdom changed. CreateKingdom is a rise to rulership
      ///   (chronicled via <see cref="OnRulingClanChanged" /> instead, which fires for it too); a leave
      ///   forced by clan/kingdom destruction carries no agency of the player's to chronicle, so those
      ///   details are left alone.
      /// </summary>
      private void OnClanChangedKingdom(
         Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
      {
         // The GAME's own companies (Zivkyn: "why does nobody resent the Skolderbrotva?"), judged by fkasad's grid.
         if (clan != null && clan != Clan.PlayerClan)
         {
            bool left = detail == ChangeKingdomAction.ChangeKingdomActionDetail.LeaveAsMercenary;
            bool joined = detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinAsMercenary;
            if (left || joined) MercenaryReputationBehavior.Instance?.OnCompanyChangedKingdom(clan, oldKingdom, newKingdom, left, joined);

            // A lord's house rising, or received back (FloatingMammal, 02/10/2026: a former rebel spoke as a loyalist).
            RecordHouseRebellion(clan, oldKingdom, newKingdom, detail);

            return;
         }

         if (clan == null || Store == null) return;
         try
         {
            switch (detail)
            {
               case ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom:
                  RecordEntryIntoService(newKingdom, asMercenary: false);
                  MercenaryReputationBehavior.Instance?.OnJoined(newKingdom); // fkasad: a recent departure may become a betrayal

                  break;
               case ChangeKingdomAction.ChangeKingdomActionDetail.JoinAsMercenary:
                  // A hired company swears nothing (tashmetu, 25/09/2026: a king told his sellsword "I've heard word
                  // you swore fealty"). The matching start of RecordMercenaryContractEnd below.
                  RecordEntryIntoService(newKingdom, asMercenary: true);
                  MercenaryReputationBehavior.Instance?.OnJoined(newKingdom);

                  break;
               case ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdomByDefection:
                  RecordDefection(oldKingdom, newKingdom);
                  Mapping.MisdeedLedgerBehavior.Record(oldKingdom?.StringId, Logic.Trauma.MisdeedKind.ForsookRealm); // Gabriel 23/10: a betrayal
                  Mapping.DeedRegardApplier.ApplyToRealm(oldKingdom, Logic.PlayerDeed.ForsookRealm);
                  MercenaryReputationBehavior.Instance?.OnJoined(newKingdom);

                  break;
               case ChangeKingdomAction.ChangeKingdomActionDetail.LeaveAsMercenary:
                  ModLog.Info($"[WORLD] player clan left {oldKingdom?.Name} (detail={detail}); CR observed this vanilla event, it did not initiate it.");
                  // fkasad's grid (25/09/2026): the realm's situation judges the departure; the plain contract end
                  // only when nothing could be judged.
                  if (MercenaryReputationBehavior.Instance?.OnLeft(oldKingdom) != true) RecordMercenaryContractEnd(oldKingdom);

                  break;
               case ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom:
                  ModLog.Info($"[WORLD] player clan left {oldKingdom?.Name} (detail={detail}); CR observed this vanilla event, it did not initiate it.");
                  // fkasad (24/09/2026): a vassal holding no fief is judged by the mercenary grid, the oath counted;
                  // the plain departure when it holds fiefs, the realm is at peace, or nothing could be judged.
                  if (MercenaryReputationBehavior.Instance?.OnLeftAsVassal(oldKingdom) != true) RecordDeparture(oldKingdom);
                  Mapping.MisdeedLedgerBehavior.Record(oldKingdom?.StringId, Logic.Trauma.MisdeedKind.ForsookRealm); // a sworn oath, not a contract

                  break;
               case ChangeKingdomAction.ChangeKingdomActionDetail.LeaveWithRebellion:
                  ModLog.Info($"[WORLD] player clan left {oldKingdom?.Name} (detail={detail}); CR observed this vanilla event, it did not initiate it.");
                  RecordDeparture(oldKingdom);
                  Mapping.MisdeedLedgerBehavior.Record(oldKingdom?.StringId, Logic.Trauma.MisdeedKind.ForsookRealm);
                  Mapping.DeedRegardApplier.ApplyToRealm(oldKingdom, Logic.PlayerDeed.ForsookRealm); // a rebellion

                  break;
               default:
                  // Any OTHER way out of a realm, which vanilla does not use for a living realm but another mod
                  // may: tashmetu's Bellum Civile expelled him after a lost war, and nothing here noticed, so a
                  // letter from his former king read as though he still served. Neutral wording, since CR cannot
                  // tell an expulsion from a departure.
                  if (oldKingdom != null && newKingdom == null && !oldKingdom.IsEliminated)
                  {
                     ModLog.Info($"[WORLD] player clan left {oldKingdom.Name} (detail={detail}); CR observed this event, it did not initiate it.");
                     RecordParting(oldKingdom);
                  }

                  break;
            }
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] kingdom-change feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   A house that rises against its realm, and one received back into the realm it rose against: recorded as a world
      ///   event and remembered by every adult member, so a former rebel never again speaks as though she had stood with
      ///   the crown (FloatingMammal, Nexus, 02/10/2026). Until now only the PLAYER's house changing sides was recorded.
      /// </summary>
      private void RecordHouseRebellion(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail)
      {
         try
         {
            if (Store == null || clan == null || clan.IsBanditFaction || clan.IsMinorFaction) return;

            List<Hero> members = MembersWhoRemember(clan);

            if (detail == ChangeKingdomAction.ChangeKingdomActionDetail.LeaveWithRebellion && oldKingdom != null)
            {
               var rose = new Logic.RebellionFacts {
                  House = clan.Name?.ToString(), Realm = oldKingdom.Name?.ToString(), Ruler = oldKingdom.Leader?.Name?.ToString(),
                  Banner = newKingdom != null && newKingdom != oldKingdom ? newKingdom.Name?.ToString() : ""
               };
               Store.Record(new WorldEvent {
                  Type = WorldEventType.Rebellion, Day = Today, Magnitude = 50, Summary = Logic.RebellionWording.RoseSummary(rose),
                  ActorIds = members.Select(h => h.StringId).ToList(),
                  FactionIds = new List<string> {oldKingdom.StringId, newKingdom?.StringId}.Where(s => s != null).ToList()
               });
               RememberHouse(members, Logic.RebellionWording.RoseMemory(rose));
               ModLog.Info($"[WORLD] house {clan.Name} rose against {oldKingdom.Name}; {members.Count} member(s) remember it.");

               return;
            }

            bool joinedRealm = detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom
                               || detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdomByDefection;
            if (!joinedRealm || newKingdom == null) return;

            var ids = new HashSet<string>(members.Select(h => h.StringId));
            Func<WorldEvent, bool> roseAgainstIt = e => e.Type == WorldEventType.Rebellion && e.FactionIds != null && e.FactionIds.FirstOrDefault() == newKingdom.StringId
                                                       && e.ActorIds != null && e.ActorIds.Any(ids.Contains);
            // The rising may be older than the news: since 17/10/2026 it is kept in the long-term memory.
            WorldEvent rising = Store.MostRecentMatching(WorldEventType.Rebellion, roseAgainstIt) ?? Store.AllHistory().LastOrDefault(roseAgainstIt);
            if (rising == null) return;

            var back = new Logic.RebellionFacts {House = clan.Name?.ToString(), Realm = newKingdom.Name?.ToString()};
            Store.Record(new WorldEvent {
               Type = WorldEventType.Other, Day = Today, Magnitude = 40, Summary = Logic.RebellionWording.ReturnedSummary(back),
               Status = LongTermHistoryPolicy.RebelReturnedStatus,
               ActorIds = members.Select(h => h.StringId).ToList(), FactionIds = new List<string> {newKingdom.StringId}
            });
            RememberHouse(members, Logic.RebellionWording.ReturnedMemory(back));
            ModLog.Info($"[WORLD] house {clan.Name} received back into {newKingdom.Name} after its rebellion; {members.Count} member(s) remember it.");
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] house rebellion not recorded for {clan?.Name}: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>The members of a house who remember its rising: adults in the world, never the player.</summary>
      internal static List<Hero> MembersWhoRemember(Clan clan)
         => (clan?.Heroes ?? Enumerable.Empty<Hero>())
            .Where(h => h != null && h != Hero.MainHero && !h.IsChild && Mapping.HeroPresence.IsInTheWorld(h)).ToList();

      /// <summary>Each member keeps the line, through CR's public memory door (which makes a profile properly when there is none).</summary>
      private static void RememberHouse(IEnumerable<Hero> members, string line)
      {
         foreach (Hero h in members)
            CrNpcMemory.AddMemory(new CrMemoryEntry {NpcId = h.StringId, Summary = line, Kind = "Other", DaysAgo = 0, SourceMod = "CalradiaRemembers"});
      }

      /// <summary>The player's clan entered a realm's service, as a sworn vassal or a hired company; RealmServiceWording words it.</summary>
      private void RecordEntryIntoService(Kingdom kingdom, bool asMercenary)
      {
         if (kingdom == null) return;
         Hero ruler = kingdom.Leader;
         RealmServiceLine line = RealmServiceWording.Joined(asMercenary, PlayerName, kingdom.Name?.ToString(), ruler?.Name?.ToString());
         Store.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = line.Magnitude,
            InvolvesPlayer = true,
            Summary = line.Summary,
            ActorIds = Ids(Hero.MainHero, ruler),
            FactionIds = new List<string> {kingdom.StringId},
            ChronicleLine = line.ChronicleLine
         });
      }

      private void RecordDefection(Kingdom oldKingdom, Kingdom newKingdom)
      {
         if (newKingdom == null) return;
         Hero ruler = newKingdom.Leader;
         string rulerName = ruler?.Name?.ToString() ?? "the crown";
         Store.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = 60,
            InvolvesPlayer = true,
            Summary = $"{PlayerName} defected to {newKingdom.Name}.",
            ActorIds = Ids(Hero.MainHero, ruler),
            FactionIds = new List<string> {newKingdom.StringId},
            ChronicleLine = oldKingdom != null
               ? $"{PlayerName} forsook {oldKingdom.Name} and swore fealty instead to {rulerName} of {newKingdom.Name}"
               : $"{PlayerName} bent the knee and swore fealty to {rulerName} of {newKingdom.Name}"
         });
      }

      private void RecordDeparture(Kingdom kingdom)
      {
         if (kingdom == null) return;
         Store.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = MercenaryDeparturePolicy.PlainVassalDepartureMagnitude,
            InvolvesPlayer = true,
            Summary = $"{PlayerName} left {kingdom.Name}.",
            ActorIds = Ids(Hero.MainHero),
            FactionIds = new List<string> {kingdom.StringId},
            ChronicleLine = $"{PlayerName} forsook {kingdom.Name}, and the oath was broken",
            Status = PlayerLeftRealmStatus
         });
      }

      /// <summary>The player's clan is out of a realm by a way CR does not recognise; see the default branch above.</summary>
      private void RecordParting(Kingdom kingdom)
      {
         Store.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = 45,
            InvolvesPlayer = true,
            Summary = $"{PlayerName} is no longer part of {kingdom.Name}.",
            ActorIds = Ids(Hero.MainHero),
            FactionIds = new List<string> {kingdom.StringId},
            ChronicleLine = $"{PlayerName} and {kingdom.Name} parted ways",
            Status = PlayerLeftRealmStatus
         });
      }

      /// <summary>
      ///   A mercenary contract simply ran its course and ended (LeaveAsMercenary), as opposed to the player
      ///   breaking an oath of fealty: no "forsook"/"oath broken" language, and a lower magnitude, since CR
      ///   only OBSERVES this vanilla event and did not cause the contract to end.
      /// </summary>
      private void RecordMercenaryContractEnd(Kingdom kingdom)
      {
         if (kingdom == null) return;
         Store.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = 30,
            InvolvesPlayer = true,
            Summary = $"{PlayerName}'s mercenary contract with {kingdom.Name} came to an end.",
            ActorIds = Ids(Hero.MainHero),
            FactionIds = new List<string> {kingdom.StringId},
            ChronicleLine = $"{PlayerName}'s hired service to {kingdom.Name} ran its course and ended"
         });
      }

      /// <summary>The player clan was raised to rule a kingdom (by election, succession, or founding one).</summary>
      private void OnRulingClanChanged(Kingdom kingdom, Clan clan)
      {
         if (kingdom == null || clan == null || clan != Clan.PlayerClan || Store == null) return;
         try
         {
            string pronoun = Hero.MainHero?.IsFemale == true ? "her" : "his";
            Store.Record(new WorldEvent {
               Type = WorldEventType.PlayerFeat,
               Day = Today,
               Magnitude = 85,
               InvolvesPlayer = true,
               Summary = $"{PlayerName} was raised to rule {kingdom.Name}.",
               ActorIds = Ids(Hero.MainHero),
               FactionIds = new List<string> {kingdom.StringId},
               ChronicleLine = $"{PlayerName} was raised to rule {kingdom.Name}, and a crown sat new upon {pronoun} brow"
            });
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] ruler feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // ── Childbirth (dynastic) ────────────────────────────────────────────────

      /// <summary>
      ///   A child was born. Chronicled only when the player is the mother (always visible), or the
      ///   father by the player's own wedded spouse (a legitimate, open birth) — a child born of the
      ///   player elsewhere is a HIDDEN bastard, whose own discovery is the Bastards pillar's separate
      ///   Scandal channel, and must not be spoiled here.
      /// </summary>
      private void OnGivenBirth(Hero mother, List<Hero> children, int newBornCount)
      {
         if (mother == null || children == null || Store == null) return;
         try
         {
            bool playerMother = mother == Hero.MainHero;
            bool motherIsPlayerSpouse = mother == Hero.MainHero?.Spouse;

            foreach (Hero child in children)
            {
               if (child == null) continue;

               // Player-mother births are always visible; a player-fathered child is only OPENLY
               // chronicled when born of the player's own wedded spouse — a child fathered elsewhere is
               // a HIDDEN bastard, whose own discovery is the Bastards pillar's separate Scandal channel.
               bool playerIsFather = child.Father == Hero.MainHero;
               if (!playerMother && !(playerIsFather && motherIsPlayerSpouse)) continue;

               string sex = child.IsFemale ? "daughter" : "son";
               Store.Record(new WorldEvent {
                  Type = WorldEventType.PlayerFeat,
                  Day = Today,
                  Magnitude = 50,
                  InvolvesPlayer = true,
                  Summary = $"A child was born to {PlayerName}.",
                  ActorIds = Ids(Hero.MainHero, child),
                  FactionIds = FactionIds(Hero.MainHero),
                  ChronicleLine = $"there was born to {PlayerName} a {sex}, and the babe was named {child.Name}"
               });
            }
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] childbirth feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // ── Captivity (the player, and the lords the player takes) ─────────────

      /// <summary>
      ///   A hero was taken prisoner. Two chronicle-worthy cases share this native event: the player
      ///   themself being captured, and the player capturing an enemy lord.
      /// </summary>
      private void OnHeroPrisonerTaken(PartyBase capturingParty, Hero prisoner)
      {
         if (prisoner == null || Store == null) return;
         try
         {
            if (prisoner == Hero.MainHero)
            {
               RecordPlayerCaptured(capturingParty);

               return;
            }

            if (capturingParty?.LeaderHero == Hero.MainHero && prisoner.IsLord && !IsBandit(prisoner))
            {
               RecordLordCaptured(prisoner);

               return;
            }

            // Lordfadooboo (03/10/2026): an empress taken by the Aserai, the player nowhere near, left no trace, so
            // nobody could speak of it. A ruler's or a landed house head's capture is news; a lesser lord's is not.
            // Gabriel (17/10/2026): a lord taking another lord prisoner is still remembered, by the long-term memory,
            // so the regard between two lords can stand on it. Not news, so it goes straight into that tier.
            if (AlreadyRecordedCaptureToday(prisoner)) return;

            int magnitude = CalradiaRemembers.Logic.Captivity.CaptiveNotoriety.NewsMagnitude(Mapping.StatureReader.Of(prisoner), prisoner.IsClanLeader);
            if (magnitude > 0) RecordGreatCapture(prisoner, capturingParty, magnitude);
            else RecordLesserCapture(prisoner, capturingParty?.LeaderHero);
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] prisoner-taken feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Skips a fresh chronicle entry when the player is already mid-captivity (the last chronicle
      ///   entry is still an open "player_captured" with no "player_freed" since) — a custody handoff
      ///   between captors (bandits to the lord who paid them, say) is one captivity, not two.
      /// </summary>
      private void RecordPlayerCaptured(PartyBase capturingParty)
      {
         WorldEvent last = LastChronicleEntry();
         if (last?.Status == "player_captured") return;

         Hero captor = capturingParty?.LeaderHero;
         string captorName = captor?.Name?.ToString() ?? capturingParty?.Name?.ToString() ?? "unknown captors";
         Store.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = 55,
            InvolvesPlayer = true,
            Status = "player_captured",
            Summary = $"{PlayerName} was taken captive by {captorName}.",
            ActorIds = Ids(Hero.MainHero, captor),
            FactionIds = FactionIds(Hero.MainHero, captor),
            ChronicleLine = $"{PlayerName} fell into the hands of {captorName} and was carried off captive"
         });
      }

      /// <summary>A ruler or a landed house head taken by someone other than the player: news that travels.</summary>
      private void RecordGreatCapture(Hero prisoner, PartyBase capturingParty, int magnitude)
      {
         Hero captor = capturingParty?.LeaderHero;
         string by = captor != null ? Named(captor) : capturingParty?.MapFaction?.Name?.ToString() ?? capturingParty?.Name?.ToString();
         Store.Record(new WorldEvent {
            Type = WorldEventType.Other, Day = Today, Magnitude = magnitude, Status = LongTermHistoryPolicy.CaptureStatus,
            Summary = string.IsNullOrEmpty(by) ? $"{Named(prisoner)} was taken captive." : $"{Named(prisoner)} was taken captive by {by}.",
            ActorIds = captor != null ? Ids(prisoner, captor) : Ids(prisoner),
            FactionIds = captor != null ? FactionIds(prisoner, captor) : FactionIds(prisoner)
         });
         ModLog.Info($"[WORLD] {prisoner.Name} taken captive (magnitude {magnitude}): news.");
      }

      /// <summary>
      ///   A lord taken by a lord, neither of them the player nor a bandit, and the prisoner too minor for news: written
      ///   straight into the long-term memory (Gabriel, 17/10/2026), where only those tied to it know it.
      /// </summary>
      private void RecordLesserCapture(Hero prisoner, Hero captor)
      {
         if (!prisoner.IsLord || IsBandit(prisoner)) return;
         if (captor == null || captor == Hero.MainHero || !captor.IsLord || IsBandit(captor)) return;

         Store.RecordHistory(new WorldEvent {
            Type = WorldEventType.Other, Day = Today, Magnitude = LongTermHistoryPolicy.LesserCaptureMagnitude,
            Status = LongTermHistoryPolicy.CaptureStatus,
            Summary = $"{Named(prisoner)} was taken captive by {Named(captor)}.",
            ActorIds = Ids(prisoner, captor),
            FactionIds = FactionIds(prisoner, captor)
         });
      }

      // TakePrisonerAction raises the event once per capture (decompiled 1.4.8, TakePrisonerAction.cs:42), and no NPC
      // handoff between captors was found to raise it again. This only keeps a repeated raise, should a mod cause one,
      // from writing the same line twice: one capture per prisoner per day.
      private static bool AlreadyRecordedCaptureToday(Hero prisoner)
      {
         int today = Today;
         string id = prisoner.StringId;

         return Store.AnyMatching(e => e.Status == LongTermHistoryPolicy.CaptureStatus && e.Day == today && e.ActorIds != null
                                       && e.ActorIds.Count > 0 && e.ActorIds[0] == id);
      }

      /// <summary>Once per unique lord — a capture already chronicled never repeats for the same lord.</summary>
      private void RecordLordCaptured(Hero lord)
      {
         bool already = Store.GetPlayerChronicle()
                              .Any(e => e.Status == "lord_captured" && e.ActorIds != null && e.ActorIds.Contains(lord.StringId));
         if (already) return;

         Store.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = 55,
            InvolvesPlayer = true,
            Status = CalradiaRemembers.Logic.Captivity.CaptureNorm.Status,
            Summary = $"{PlayerName} took {lord.Name} captive.",
            ActorIds = Ids(Hero.MainHero, lord),
            FactionIds = FactionIds(Hero.MainHero, lord),
            // Fakade (08/10/2026): the captive's realm and kin are the ones it touches, as with an execution; how it is
            // framed (an ordinary act of war) is CaptureNorm's, where the news is told.
            AggrievedFactionIds = FactionIds(lord),
            AggrievedActorIds = Ids(lord),
            ChronicleLine = $"{PlayerName} took {lord.Name} captive upon the field"
         });
      }

      private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction faction, EndCaptivityDetail detail, bool showNotification)
      {
         if (prisoner != Hero.MainHero || Store == null) return;
         if (detail == EndCaptivityDetail.Death) return; // no chronicle for this — nothing to come back to

         // Somebody fell upon the captors, so the chronicle must be able to say who. Gating this on
         // ReleasedAfterBattle alone was WRONG and missed the common case (Gabriel, 2026-07-18): a lord
         // engaged the band holding him, the game offered him the confusion of the fight, and the engine
         // reported a plain ESCAPE. The attacker still caused it. So the real signal is that the captors
         // are IN a battle at the moment the player gets out, whatever label the engine puts on it.
         bool captorInBattle = party?.MapEvent != null;

         if (detail == EndCaptivityDetail.ReleasedAfterBattle || captorInBattle)
         {
            _pendingPlayerCaptor = party;
            _playerReleaseAwaitingBattle = true;
            // An escape during the fight is not the winner cutting the player loose, and the wording says so.
            _playerBrokeFree = detail != EndCaptivityDetail.ReleasedAfterBattle;

            return;
         }

         RecordPlayerFreed(null);
      }

      /// <summary>
      ///   Writes the end of the player's captivity. <paramref name="liberator" /> null keeps the original
      ///   "won free" wording, which is right for an escape or a ransom and is the honest fallback when a
      ///   battle freed the player but no leader could be named.
      /// </summary>
      private void RecordPlayerFreed(Hero liberator, bool brokeFree = false)
      {
         try
         {
            string pronoun = Hero.MainHero.IsFemale ? "her" : "his";
            string liberatorName = liberator?.Name?.ToString();
            Store.Record(new WorldEvent {
               Type = WorldEventType.PlayerFeat,
               Day = Today,
               Magnitude = 45,
               InvolvesPlayer = true,
               Status = "player_freed",
               Summary = CaptivityReleaseNarrator.Summary(PlayerName, liberatorName, brokeFree),
               // The liberator joins the actors so the deed reaches THEM: this is the whole point of naming
               // them, and it is what stops a lord denying a rescue the player is thanking him for.
               ActorIds = liberator != null
                  ? Ids(Hero.MainHero, liberator)
                  : Ids(Hero.MainHero),
               FactionIds = liberator != null
                  ? FactionIds(Hero.MainHero, liberator)
                  : FactionIds(Hero.MainHero),
               ChronicleLine = CaptivityReleaseNarrator.ChronicleLine(new CaptivityReleaseNarrator.ReleaseLineFacts {PlayerName = PlayerName, PossessivePronoun = pronoun, LiberatorName = liberatorName, BrokeFree = brokeFree})
            });
         }
         catch (Exception ex) { ModLog.Error($"[WORLD] prisoner-released feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Resolves a buffered "freed by battle" against the map event that caused it. Runs BEFORE the
      ///   notability gate below on purpose: a lord riding down a bandit band is never a notable battle, and
      ///   that is precisely the case this exists for.
      /// </summary>
      private void ResolvePendingPlayerRelease(MapEvent mapEvent)
      {
         if (!_playerReleaseAwaitingBattle) return;

         try
         {
            BattleSideEnum losing = mapEvent.WinningSide == BattleSideEnum.Attacker
               ? BattleSideEnum.Defender
               : BattleSideEnum.Attacker;

            // The captors merely have to be IN this battle, on either side: the player slipping away in the
            // confusion is caused by the attack itself, not by who eventually won it.
            Hero winner = SideLeader(mapEvent, mapEvent.WinningSide);
            bool captorEngaged = _pendingPlayerCaptor != null
                              && (mapEvent.PartiesOnSide(losing).Any(p => p.Party == _pendingPlayerCaptor)
                               || mapEvent.PartiesOnSide(mapEvent.WinningSide).Any(p => p.Party == _pendingPlayerCaptor));

            RecordPlayerFreed(CaptivityReleaseNarrator.CreditsLiberator(new CaptivityReleaseNarrator.LiberationFacts {CaptorWasEngaged = captorEngaged, LiberatorKnown = winner != null, LiberatorIsCaptor = winner != null && winner == _pendingPlayerCaptor?.LeaderHero})
                                 ? winner
                                 : null,
                              _playerBrokeFree);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WORLD] player-release resolve failed: {ex.GetType().Name}: {ex.Message}");
            RecordPlayerFreed(null); // never lose the entry
         }
         finally
         {
            _playerReleaseAwaitingBattle = false;
            _pendingPlayerCaptor = null;
            _playerBrokeFree = false;
         }
      }

      private static WorldEvent LastChronicleEntry()
      {
         IReadOnlyList<WorldEvent> chronicle = Store.GetPlayerChronicle();

         return chronicle.Count > 0
            ? chronicle[chronicle.Count - 1]
            : null;
      }

      // ── Wars & peace (realm-level rumor, not personal deeds) ───────────────

      private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
         => RecordDiplomacy(faction1, faction2, WorldEventType.WarDeclared, 75, "declared war on");

      /// <summary>
      ///   Once a week, surface a couple of bitter intra-kingdom rivalries as ambient gossip —
      ///   feuding vassals of the same crown, inferred from their native standing. At most one
      ///   per kingdom per pass, each pairing on a cooldown so it does not repeat too soon.
      /// </summary>
      private void OnWeeklyTick()
      {
         if (Store == null) return;
         int today = Today;
         var emitted = 0;

         foreach (Kingdom kingdom in Kingdom.All)
         {
            if (emitted >= MaxRivalriesPerWeek) break;

            Kingdom k = kingdom;
            // One kingdom's scan throwing must not stop the gossip pass for the rest of the realms.
            SafeRun.Step($"WorldEventFeeders.OnWeeklyTick({k?.Name})", () =>
            {
               var lords = new List<Hero>();
               foreach (Clan clan in k.Clans)
               {
                  foreach (Hero h in clan.Heroes)
                     if (h is {IsAlive: true, IsLord: true} && h != Hero.MainHero)
                        lords.Add(h);

                  if (lords.Count >= MaxLordsPerKingdomScan) break;
               }

               if (TryEmitKingdomRivalry(lords, k, today)) emitted++;
            });
         }
      }

      private bool TryEmitKingdomRivalry(List<Hero> lords, Kingdom kingdom, int today)
      {
         for (var i = 0; i < lords.Count; i++)
            for (int j = i + 1; j < lords.Count; j++)
            {
               if (lords[i].GetRelation(lords[j]) > RivalryThreshold) continue;

               string key = PairKey(lords[i], lords[j]);

               if (_rivalryGossipDay.TryGetValue(key, out int last) && today - last < RivalryCooldownDays) continue;

               _rivalryGossipDay[key] = today;
               Store.Record(new WorldEvent {
                  Type = WorldEventType.Rivalry,
                  Day = today,
                  Magnitude = 22,
                  Summary = $"{Named(lords[i])} and {Named(lords[j])} are at bitter odds.",
                  ActorIds = Ids(lords[i], lords[j]),
                  FactionIds = kingdom?.StringId != null
                     ? new List<string> {kingdom.StringId}
                     : new List<string>()
               });

               return true;
            }

         return false;
      }

      #endregion
   }
}