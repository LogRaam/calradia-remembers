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
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Battles;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Battles
{
   /// <summary>Builds the distilled, first-hand deed lines for the battle that just ended.</summary>
   public static class BattleFeatReporter
   {
      /// <summary>How loudly an exploit travels. Above a tournament win (40), below a realm-shaking scandal. Tuning.</summary>
      private const int ExploitMagnitude = 55;

      /// <summary>
      ///   The lines to append to every profile that witnessed this battle, or an empty list when the fight left
      ///   nothing worth adding. Consumes the finished journal, so it yields deeds exactly once per battle.
      /// </summary>
      public static IReadOnlyList<string> ConsumeWitnessLines(string engagement)
      {
         BattleFeatJournal journal = BattleFeatJournal.ConsumeCompleted();

         return journal == null ? new List<string>() : BuildLines(journal, engagement);
      }

      /// <summary>cr.testall seam: build the lines from a journal directly, without consuming the live one.</summary>
      internal static IReadOnlyList<string> BuildLines(BattleFeatJournal journal, string engagement)
      {
         var lines = new List<string>();
         if (journal == null) return lines;

         List<CombatFeat> feats = journal.HeroesDowned.Select(ResolveFeat).Where(f => f != null).ToList();
         string downedBy = NameOf(journal.DownedThePlayer.FirstOrDefault());

         BattleFeatDigest digest = BattleFeatDistiller.Distil(feats, downedBy, journal.TroopsFelled);
         if (digest.IsEmpty) return lines;

         if (digest.BestFeat != null)
            lines.Add(BattleFeatText.WitnessedFeat(digest.BestFeat.FoeName, digest.BestFeat.Outcome, engagement));

         if (!string.IsNullOrEmpty(digest.DownedThePlayer))
            lines.Add(BattleFeatText.WitnessedPlayerDowned(digest.DownedThePlayer, engagement));

         if (digest.TollToMention > 0)
            lines.Add(BattleFeatText.WitnessedToll(digest.TollToMention, engagement));

         // Inc 3: only an EXPLOIT leaves the field (Gabriel's rule). Everything else stays with those who saw it.
         if (digest.BestFeat != null && CombatProwessPolicy.TravelsAsGossip(digest.BestFeat.Tier))
            RecordExploitEvent(digest.BestFeat, engagement);

         return lines;
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

         string name = HeroLabel.Describe(foe.Name?.ToString(), foe.Clan?.Name?.ToString(), foe.Culture?.Name?.ToString());
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
         string playerName = HeroLabel.Describe(Hero.MainHero.Name?.ToString(), Hero.MainHero.Clan?.Name?.ToString(),
            Hero.MainHero.Culture?.Name?.ToString());

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

         string name = HeroLabel.Describe(foe.Name?.ToString(), foe.Clan?.Name?.ToString(), foe.Culture?.Name?.ToString());

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
            : HeroLabel.Describe(hero.Name?.ToString(), hero.Clan?.Name?.ToString(), hero.Culture?.Name?.ToString());
      }
   }
}
