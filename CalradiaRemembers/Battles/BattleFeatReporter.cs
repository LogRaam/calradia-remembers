// Code written by Gabriel Mailhot, 06/09/2026.
// Battle-memory enrichment, Inc 2: turns the mission's ledger into the few first-hand lines a battle leaves in the
// memory of those who were THERE, beside (never replacing) the existing "fought at your side" line.
//
// This is where the truth rule is enforced. The mission reports a hero as Unconscious or Killed, but the CAMPAIGN
// has the last word: Bannerlord routinely spares a hero the mission called Killed. So each downed hero's outcome is
// read from the hero himself once the dust settles: truly dead, taken captive, or simply bested. That is what stops
// an NPC mourning a lord the player can visit in his own dungeon (Gabriel ratified 2026-09-06).

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Battles;
using CalradiaRemembers.Tournaments;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

#endregion

namespace CalradiaRemembers.Battles
{
   /// <summary>Builds the distilled, first-hand deed lines for the battle that just ended.</summary>
   public static class BattleFeatReporter
   {
      /// <summary>How loudly an exploit travels. Above a tournament win (40), below a realm-shaking scandal. Tuning.</summary>
      private const int ExploitMagnitude = 55;

      /// <summary>How a battlefield rivalry is described when it crystallizes, as against the arena wording.</summary>
      private const string RivalryVenue = "the field of battle";

      /// <summary>
      ///   What this battle leaves in the memory of those who saw it, rendered PER RECIPIENT (see
      ///   <see cref="BattleFeatReport.LinesFor" />). Consumes the finished journal of the mission fought for
      ///   <paramref name="mapEvent" /> (never another fight's: a tournament's deeds are not this battle's), so a battle
      ///   yields its deeds exactly once; never null.
      /// </summary>
      public static BattleFeatReport ConsumeReport(MapEvent mapEvent, string engagement)
      {
         BattleFeatJournal journal = BattleFeatJournal.ConsumeFor(mapEvent);

         return journal == null ? BattleFeatReport.Empty : BuildReport(journal, engagement);
      }

      /// <summary>cr.testall seam: the per-recipient report for a journal, without consuming the live one.</summary>
      internal static BattleFeatReport BuildReport(BattleFeatJournal journal, string engagement)
      {
         if (journal == null) return BattleFeatReport.Empty;

         List<CombatFeat> feats = journal.HeroesDowned.Select(ResolveFeat).Where(f => f != null).ToList();
         string downedById = journal.DownedThePlayer.FirstOrDefault();

         BattleFeatDigest digest = BattleFeatDistiller.Distil(feats, NameOf(downedById), journal.TroopsFelled);
         if (digest.IsEmpty) return new BattleFeatReport(null, null, engagement, journal); // every hero's own part remains

         // Only an EXPLOIT leaves the field (Gabriel's rule); everything else stays with those who saw it.
         if (digest.BestFeat != null && CombatProwessPolicy.TravelsAsGossip(digest.BestFeat.Tier))
            RecordExploitEvent(digest.BestFeat, engagement);

         // A named head-to-head is what a rivalry is made of, and at most two per battle keeps it a trickle.
         if (digest.BestFeat != null) RecordRivalryEncounter(digest.BestFeat.FoeId, playerWon: true);
         if (!string.IsNullOrEmpty(downedById)) RecordRivalryEncounter(downedById, playerWon: false);

         return new BattleFeatReport(digest, downedById, engagement, journal);
      }

      /// <summary>
      ///   One hero's own part in the fight a journal recorded, worded for their memory (CombatTallyText), each foe named
      ///   and judged by the campaign's verdict afterwards; empty when they did and suffered nothing.
      /// </summary>
      public static string OwnPartOf(BattleFeatJournal journal, string heroId, bool arena)
      {
         HeroTally t = journal?.TallyOf(heroId);
         if (t == null) return string.Empty;

         return CombatTallyText.OwnPart(new CombatTally {
            TroopsFelled = t.TroopsFelled,
            HeroesDowned = t.HeroesDowned.Select(FindHero).Where(h => h != null)
                            .Select(h => new TalliedFoe {Name = Label(h), Died = h.IsDead}).ToList(),
            WasDowned = t.WasDowned,
            DownedBy = t.DownedById == null ? null : Label(FindHero(t.DownedById))
         }, arena);
      }

      private static Hero FindHero(string heroId)
         => string.IsNullOrEmpty(heroId) ? null : Hero.Find(heroId) ?? Hero.FindFirst(h => h.StringId == heroId);

      private static string Label(Hero h)
         => h == null ? null : HeroLabel.Describe(new HeroLabel.HeroNaming {Name = h.Name?.ToString(), ClanName = h.Clan?.Name?.ToString(), CultureName = h.Culture?.Name?.ToString()});

      // A battlefield head-to-head feeds the same standing-rivalry ledger the arena uses, so a lord the player keeps
      // meeting sword to sword becomes a worthy rival or a bitter one by his own character.
      private static void RecordRivalryEncounter(string heroId, bool playerWon)
      {
         if (string.IsNullOrEmpty(heroId)) return;

         Hero hero = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h.StringId == heroId);
         if (hero == null) return;

         TournamentRivalryBehavior.Instance?.RecordEncounter(hero, playerWon, RivalryVenue);
      }

      /// <summary>
      ///   Rates one victory and, when it is an EXPLOIT, lets word of it leave the field. Shared with the arena so
      ///   a tournament win travels the same way. Returns true when an event was actually recorded.
      /// </summary>
      public static bool TryRecordExploit(Hero foe, CombatFeatOutcome outcome, string engagement)
      {
         if (foe == null || Hero.MainHero == null) return false;

         CombatFeatTier tier = CombatProwessPolicy.Rate(BattleFeatFacts.For(Hero.MainHero), BattleFeatFacts.For(foe));
         if (!CombatProwessPolicy.TravelsAsGossip(tier)) return false;

         string name = HeroLabel.Describe(new HeroLabel.HeroNaming {Name = foe.Name?.ToString(), ClanName = foe.Clan?.Name?.ToString(), CultureName = foe.Culture?.Name?.ToString()});
         RecordExploitEvent(new CombatFeat(foe.StringId, name, outcome, tier), engagement);

         return true;
      }

      /// <summary>
      ///   Records the exploit as a PLAYER FEAT the realm may speak of. Deliberately shaped so the recent-matter
      ///   registers classify it correctly: <c>InvolvesPlayer</c> is true (it is the player's own deed, so a lord
      ///   raises it as a RUMOUR about them, not as impersonal gossip), and only the two who were actually in it
      ///   are named as actors. No faction is tagged on purpose: tagging one would let every lord of that faction
      ///   read it as something they took part in, and claim first-hand knowledge of a fight they never saw.
      /// </summary>
      private static void RecordExploitEvent(CombatFeat feat, string engagement)
      {
         if (WorldEventStore.Instance == null || Hero.MainHero == null) return;

         var day = (int) CampaignTime.Now.ToDays;
         string playerName = HeroLabel.Describe(new HeroLabel.HeroNaming {Name = Hero.MainHero.Name?.ToString(), ClanName = Hero.MainHero.Clan?.Name?.ToString(), CultureName = Hero.MainHero.Culture?.Name?.ToString()});

         WorldEventStore.Instance.Record(new WorldEvent {
            Id = $"cr_feat_{feat.FoeId}_{day}",
            Type = WorldEventType.PlayerFeat,
            Day = day,
            Magnitude = ExploitMagnitude,
            InvolvesPlayer = true,
            ActorIds = new List<string> {Hero.MainHero.StringId, feat.FoeId},
            Summary = BattleFeatText.GossipedExploit(playerName, feat.FoeName, feat.Outcome, engagement),
            ChronicleLine = BattleFeatText.GossipedExploit(playerName, feat.FoeName, feat.Outcome, engagement)
         });

         ModLog.Info($"[BATTLEFEAT] word spreads: the player bested {feat.FoeName}.");
      }

      /// <summary>
      ///   One downed hero, rated and resolved. The OUTCOME comes from the hero as the campaign left them, never
      ///   from the mission's own verdict; the TIER comes from how the two of them actually compared.
      /// </summary>
      private static CombatFeat ResolveFeat(string heroId)
      {
         Hero foe = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h.StringId == heroId)
                    ?? Hero.DeadOrDisabledHeroes?.FirstOrDefault(h => h != null && h.StringId == heroId);
         if (foe == null || Hero.MainHero == null) return null;

         CombatFeatTier tier = CombatProwessPolicy.Rate(BattleFeatFacts.For(Hero.MainHero), BattleFeatFacts.For(foe));
         if (tier == CombatFeatTier.Unremarkable) return null;

         string name = HeroLabel.Describe(new HeroLabel.HeroNaming {Name = foe.Name?.ToString(), ClanName = foe.Clan?.Name?.ToString(), CultureName = foe.Culture?.Name?.ToString()});

         return new CombatFeat(heroId, name, ResolveOutcome(foe), tier);
      }

      // The campaign's verdict, not the mission's: dead only when truly dead, captive when the player holds them.
      private static CombatFeatOutcome ResolveOutcome(Hero foe)
      {
         if (foe.IsDead) return CombatFeatOutcome.Slain;

         return foe.IsPrisoner && foe.PartyBelongedToAsPrisoner?.MapFaction == Hero.MainHero.MapFaction
            ? CombatFeatOutcome.TakenAlive
            : CombatFeatOutcome.Bested;
      }

      private static string NameOf(string heroId)
      {
         if (string.IsNullOrEmpty(heroId)) return null;

         Hero hero = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h.StringId == heroId);

         return hero == null
            ? null
            : HeroLabel.Describe(new HeroLabel.HeroNaming {Name = hero.Name?.ToString(), ClanName = hero.Clan?.Name?.ToString(), CultureName = hero.Culture?.Name?.ToString()});
      }
   }

   /// <summary>
   ///   What one battle left behind, rendered PER RECIPIENT. A witness saw the deeds; the man a deed is ABOUT
   ///   lived them, and must speak of them in his own voice, or his memory would read "I saw you best Lucon" when
   ///   he IS Lucon.
   /// </summary>
   public sealed class BattleFeatReport
   {
      /// <summary>A battle that left nothing worth adding beyond the ordinary "we fought together" line.</summary>
      public static readonly BattleFeatReport Empty = new BattleFeatReport(null, null, null);

      private readonly BattleFeatDigest _digest;
      private readonly string _downedThePlayerId;
      private readonly string _engagement;
      private readonly BattleFeatJournal _journal;

      internal BattleFeatReport(BattleFeatDigest digest, string downedThePlayerId, string engagement, BattleFeatJournal journal = null)
      {
         _digest = digest;
         _downedThePlayerId = downedThePlayerId;
         _engagement = engagement;
         _journal = journal;
      }

      /// <summary>
      ///   The recipient's OWN part in the fight (chmidt, 23/10/2026: what they felled, whether they were downed), to
      ///   append to their memory of it; empty for an auto-resolved battle, which has no mission to tell it.
      /// </summary>
      public string OwnPartFor(string recipientHeroId) => BattleFeatReporter.OwnPartOf(_journal, recipientHeroId, arena: false);

      /// <summary>The battle was fought in a mission (false for an auto-resolved one, which has no one's deeds to tell).</summary>
      public bool HadAMission => _journal != null;

      /// <summary>A report for a fight with a mission journal but no deed of the player's own (a small fight's).</summary>
      internal static BattleFeatReport ForOwnParts(BattleFeatJournal journal) => new BattleFeatReport(null, null, null, journal);

      /// <summary>The player as these memory lines name them, never "you": see BattleMemoryText.NameOrPlayer.</summary>
      private static string PlayerName => Hero.MainHero?.Name?.ToString() ?? "the player";

      /// <summary>True when there is nothing to append to anyone.</summary>
      public bool IsEmpty => _digest == null || _digest.IsEmpty;

      /// <summary>
      ///   The lines this particular hero would remember. A bystander witnessed them; the foe the player bested
      ///   speaks of his own defeat; the man who put the player down says so himself rather than reporting it.
      /// </summary>
      public IReadOnlyList<string> LinesFor(string recipientHeroId)
      {
         var lines = new List<string>();
         if (IsEmpty) return lines;

         bool isTheBestedFoe = _digest.BestFeat != null && !string.IsNullOrEmpty(recipientHeroId)
                                                        && _digest.BestFeat.FoeId == recipientHeroId;
         bool isTheOneWhoDownedThePlayer = !string.IsNullOrEmpty(_downedThePlayerId) && _downedThePlayerId == recipientHeroId;

         if (_digest.BestFeat != null)
            lines.Add(isTheBestedFoe
               ? BattleFeatText.SubjectBestedByPlayer(_digest.BestFeat.Outcome, _engagement, PlayerName)
               : BattleFeatText.WitnessedFeat(_digest.BestFeat.FoeName, _digest.BestFeat.Outcome, _engagement, PlayerName));

         if (!string.IsNullOrEmpty(_digest.DownedThePlayer))
            lines.Add(isTheOneWhoDownedThePlayer
               ? BattleFeatText.SubjectDownedThePlayer(_engagement, PlayerName)
               : BattleFeatText.WitnessedPlayerDowned(_digest.DownedThePlayer, _engagement, PlayerName));

         if (_digest.TollToMention > 0)
            lines.Add(BattleFeatText.WitnessedToll(_digest.TollToMention, _engagement, PlayerName));

         return lines;
      }
   }
}
