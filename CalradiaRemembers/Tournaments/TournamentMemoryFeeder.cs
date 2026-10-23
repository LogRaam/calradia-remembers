// Code written by Gabriel Mailhot, 06/09/2026.
// Tournament memory pillar (player suggestion 2026-09-06), Inc 2 (feeder). Turns a read TournamentResult into what
// the lords actually carry: a first-hand LIVED memory on each hero the player faced (or the champion who took the
// crown over them), so the existing recent-matter interception has them seek the player out afterward to speak of
// it. The FACT is stored first-person and named (town, round, who bested whom); the TONE (brag vs congratulate) is
// left to the lord's own character when the LLM speaks the approach, exactly as the recent-matter LIVED register
// intends. A champion who beat the player also seeds a world-gossip event so lords who were NOT there can ask about
// it. The appreciation/grudge aftermath and rivalries are a later increment.
// chmidt (Nexus, 23/10/2026): the lady of Hargard forgot the tournament the player won in her town. The lords and ladies
// in town and the notables the player knows now keep a spectator's memory (TournamentAudiencePolicy), and each fighter's
// memory ends with their own part in the lists, read from the combat journal of the tournament.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Battles;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Battles;
using CalradiaRemembers.Logic.Tournaments;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Tournaments
{
   /// <summary>Writes the LIVED tournament memories (and a champion gossip event) that make lords approach the player about the arena.</summary>
   public static class TournamentMemoryFeeder
   {
      private const int ChampionGossipMagnitude = 55; // clears the recent-matter WorldGossip threshold (55)
      private const int MaxTournamentLetters = 2;      // a couple of grounded notes at most, so a tourney never floods the mailbox

      /// <summary>Records a first-hand tournament memory on every hero the player had a decisive result against, queues up to a couple of grounded letters (brag / gracious), and seeds a gossip event for a champion who beat the player.</summary>
      public static void Apply(TournamentResult result, Settlement town = null)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (result == null || behavior?.Store == null) return;

         // The tournament's own combat journal: the mission is still running when the final resolves.
         BattleFeatJournal journal = BattleFeatJournal.Current;
         var seen = new HashSet<string>();
         var lettersLeft = MaxTournamentLetters;

         foreach (TournamentBout bout in result.Bouts)
            foreach (string heroId in bout.OpponentHeroIds)
               if (!string.IsNullOrEmpty(heroId) && seen.Add(heroId) && RecordMemoryFor(behavior, result, heroId, lettersLeft > 0, journal))
                  lettersLeft--;

         if (!string.IsNullOrEmpty(result.ChampionHeroId) && seen.Add(result.ChampionHeroId) && RecordMemoryFor(behavior, result, result.ChampionHeroId, lettersLeft > 0, journal))
            lettersLeft--;

         RecordChampionGossip(result);
         RecordAudience(behavior, result, town, seen);
      }

      /// <summary>
      ///   The town's onlookers keep a spectator's memory (chmidt, 23/10/2026): the lords and ladies of the holding house
      ///   in town, even never spoken to, and the lords and notables the player knows. Returns how many were written.
      /// </summary>
      internal static int RecordAudience(NpcMemoryCampaignBehavior behavior, TournamentResult result, Settlement town, ISet<string> fought)
      {
         if (town == null || behavior?.Store == null || result == null) return 0;

         var inTown = new List<Hero>();
         if (town.HeroesWithoutParty != null) inTown.AddRange(town.HeroesWithoutParty);
         if (town.Parties != null) inTown.AddRange(town.Parties.Select(p => p?.LeaderHero));
         if (town.Notables != null) inTown.AddRange(town.Notables);
         Clan holder = town.OwnerClan;

         var onlookers = inTown.Where(h => h != null && h != Hero.MainHero && !h.IsChild && HeroPresence.IsInTheWorld(h))
                               .Distinct()
                               .Select(h => new TournamentOnlooker {
                                  Id = h.StringId, IsLord = h.IsLord, OfTheHoldingHouse = holder != null && h.Clan == holder,
                                  KnownToThePlayer = behavior.Store.Get(h.StringId) != null, FoughtInIt = fought != null && fought.Contains(h.StringId)
                               })
                               .ToList();

         string line = TournamentAudiencePolicy.SpectatorMemory(result);
         var written = 0;
         foreach (string id in TournamentAudiencePolicy.Who(onlookers))
         {
            Hero h = inTown.First(x => x != null && x.StringId == id);
            NpcProfile profile = behavior.Store.Get(id) ?? HeroProfileMapper.FromHero(h);
            if (profile == null) continue;

            profile.Events.Add(new NotableEvent(result.Day, NotableEventType.Other, line));
            behavior.Store.Set(profile);
            written++;
         }

         if (written > 0) Foundation.ModLog.Info($"[TOURNEY] {written} onlooker(s) at {result.TownName} remember the tourney.");

         return written;
      }

      // Writes the LIVED memory and, when allowed, may queue a grounded letter (brag/gracious). Returns true only
      // when a letter was actually queued, so the caller can hold the tournament to a small letter budget.
      private static bool RecordMemoryFor(NpcMemoryCampaignBehavior behavior, TournamentResult result, string heroId, bool mayQueueLetter, BattleFeatJournal journal = null)
      {
         TournamentStanding standing = TournamentMemoryPolicy.Classify(result, heroId);
         if (standing.Relation == TournamentRelation.None) return false;

         Hero hero = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h.StringId == heroId);
         if (hero == null) return false;

         NpcProfile profile = behavior.Store.Get(heroId) ?? HeroProfileMapper.FromHero(hero);
         if (profile == null) return false;

         string ownPart = BattleFeatReporter.OwnPartOf(journal, heroId, arena: true);
         string memory = ComposeMemory(result, standing);
         profile.Events.Add(new NotableEvent(result.Day, NotableEventType.Other, string.IsNullOrEmpty(ownPart) ? memory : $"{memory} {ownPart}"));

         // A face-to-face result (not a champion who never met the player) is a head-to-head that builds a rivalry.
         if (standing.Relation == TournamentRelation.BeatThePlayer || standing.Relation == TournamentRelation.LostToThePlayer)
            TournamentRivalryBehavior.Instance?.RecordEncounter(hero, standing.Relation == TournamentRelation.LostToThePlayer);

         // Battle-memory Inc 3: beating a lord in the lists who was well above the player, or a name the realm
         // knows, is an EXPLOIT, so word of it leaves the arena and lords who never attended may raise it. Nobody
         // is "slain" in a tournament, hence Bested.
         if (standing.Relation == TournamentRelation.LostToThePlayer)
            BattleFeatReporter.TryRecordExploit(hero, CombatFeatOutcome.Bested, TourneyEngagement(result));

         bool letterQueued = mayQueueLetter && TryQueueLetter(profile, hero, result, standing);
         behavior.Store.Set(profile);

         return letterQueued;
      }

      // A lord who came away with something to SAY writes a grounded note: a braggart crows, a gracious soul
      // congratulates or is magnanimous. A measured or sore lord says nothing by letter (in person, or a rivalry,
      // carries that). A lord travelling with the player never writes; they would just speak.
      private static bool TryQueueLetter(NpcProfile profile, Hero hero, TournamentResult result, TournamentStanding standing)
      {
         if (hero.PartyBelongedTo == MobileParty.MainParty) return false;

         TournamentMemoryTone tone = TournamentMemoryPolicy.Tone(standing, TraitsOf(hero));
         if (tone != TournamentMemoryTone.Brag && tone != TournamentMemoryTone.Gracious) return false;

         profile.LetterCandidates.Add(new LetterCandidateIntent {
            Reason = LetterReason.TournamentResult,
            Context = ComposeLetterContext(result, standing, tone),
            EligibleSinceDay = result.Day,
            BaseWeightOverride = 0 // derive from LetterWeights.BaseWeight(TournamentResult)
         });

         return true;
      }

      private static TournamentTraitFacts TraitsOf(Hero hero)
         => new TournamentTraitFacts {Valor = hero.GetTraitLevel(DefaultTraits.Valor), Honor = hero.GetTraitLevel(DefaultTraits.Honor), Mercy = hero.GetTraitLevel(DefaultTraits.Mercy)};

      // A concise, first-person FACT the recent-matter opener will name; the lord's own character supplies the tone.
      internal static string ComposeMemory(TournamentResult result, TournamentStanding standing)
      {
         string atTown = string.IsNullOrEmpty(result.TownName) ? "the tourney" : $"the tourney at {result.TownName}";
         string round = RoundName(standing.Round);

         switch (standing.Relation)
         {
            case TournamentRelation.BeatThePlayer:
               return standing.IsChampion
                  ? $"I bested the player in {round} of {atTown} and went on to take the crown."
                  : $"I bested the player in {round} of {atTown}.";
            case TournamentRelation.LostToThePlayer:
               return $"The player bested me in {round} of {atTown}.";
            default: // CoCompetitor champion (never faced the player directly)
               return $"I won the crown at {atTown}, where the player also competed.";
         }
      }

      // The letter's instruction to the writer: the bracket fact in second person, plus the spirit their tone gives.
      internal static string ComposeLetterContext(TournamentResult result, TournamentStanding standing, TournamentMemoryTone tone)
      {
         string atTown = string.IsNullOrEmpty(result.TownName) ? "the tourney" : $"the tourney at {result.TownName}";
         string round = RoundName(standing.Round);

         string fact;
         switch (standing.Relation)
         {
            case TournamentRelation.BeatThePlayer:
               fact = standing.IsChampion
                  ? $"You bested the player in {round} of {atTown} and went on to take the crown."
                  : $"You bested the player in {round} of {atTown}.";
               break;
            case TournamentRelation.LostToThePlayer:
               fact = $"The player bested you in {round} of {atTown}.";
               break;
            default:
               fact = $"You took the crown at {atTown}, where the player also competed.";
               break;
         }

         string spirit = tone == TournamentMemoryTone.Brag
            ? "You write to crow over it, rubbing the victory in as your nature dictates."
            : standing.Relation == TournamentRelation.LostToThePlayer
               ? "You write to congratulate them, gracious in defeat."
               : "You write a magnanimous note, praising how they fought.";

         return fact + " " + spirit;
      }

      private static string TourneyEngagement(TournamentResult result)
         => string.IsNullOrEmpty(result.TownName) ? "the tourney" : $"the tourney at {result.TownName}";

      private static string RoundName(int round)
      {
         switch (round)
         {
            case 4: return "the final";
            case 3: return "the semi-final";
            case 2: return "the second round";
            case 1: return "the first round";
            default: return "a bout";
         }
      }

      // A champion who took the crown over a competing player becomes an anonymous-to-the-absent world-gossip event,
      // so lords who were NOT there may ask the player about it. Deterministic id = one standing fact per tournament.
      private static void RecordChampionGossip(TournamentResult result)
      {
         if (string.IsNullOrEmpty(result.ChampionHeroId) || WorldEventStore.Instance == null) return;

         Hero champion = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h.StringId == result.ChampionHeroId);
         if (champion == null) return;

         string name = HeroLabel.Describe(new HeroLabel.HeroNaming {Name = champion.Name?.ToString(), ClanName = champion.Clan?.Name?.ToString(), CultureName = champion.Culture?.Name?.ToString()});
         string atTown = string.IsNullOrEmpty(result.TownName) ? "a tournament" : $"the tournament at {result.TownName}";

         WorldEventStore.Instance.Record(new WorldEvent
         {
            Id = $"cr_tourney_{result.TownName}_{result.Day}",
            Type = WorldEventType.Other,
            Day = result.Day,
            Magnitude = ChampionGossipMagnitude,
            InvolvesPlayer = false,
            ActorIds = new List<string> {champion.StringId},
            FactionIds = champion.MapFaction != null ? new List<string> {champion.MapFaction.StringId} : new List<string>(),
            Summary = $"{name} won {atTown}.",
            ChronicleLine = $"{name} took the crown at {atTown}."
         });
      }
   }
}
