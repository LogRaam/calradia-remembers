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
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Battles
{
   /// <summary>Builds the distilled, first-hand deed lines for the battle that just ended.</summary>
   public static class BattleFeatReporter
   {
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

         return lines;
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
