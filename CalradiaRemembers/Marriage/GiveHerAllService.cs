// Code written by Gabriel Mailhot, 26/10/2026.
// The second road to a ruler's hand (Gabriel, 26/10/2026): "le Clan du joueur est transfere a Rhagaea. C'est aussi
// l'option si aucun heritier. Mais ca pourrait declencher la guerre", then: his family stays with him ("le lien de sang
// est tres fort"), a realm he rules is absorbed into hers, the AI makes peace when it will, and his influence is given
// back. The player keeps their character and family; she receives his towns, castles and gold; his house joins her
// realm; they wed, each keeping their own house; she pays a season of his house's wages; her people's rivals may answer
// with war. Order and odds are LeaveHouseForRulerPolicy's; engine calls are decompiled 1.4.8 (the 2c spike, 26/10/2026).

#region

using System;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Marriage;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>Gives a ruler the player's fiefs, gold and realm, and weds them, each keeping their own house.</summary>
   public static class GiveHerAllService
   {
      // Days the houses of an absorbed realm must stay in hers before they may leave it again.
      private const int AbsorbedHousesStayDays = 365;

      /// <summary>What the player would bring her, for the war odds.</summary>
      public static UnionFacts UnionFor(Hero ruler)
      {
         Clan house = Clan.PlayerClan;
         Kingdom herRealm = ruler?.Clan?.Kingdom;
         Kingdom ownRealm = house?.Kingdom;
         bool rules = ownRealm != null && ownRealm.RulingClan == house;

         return new UnionFacts {
            BringsARealm = rules && ownRealm != herRealm,
            DefectsFromARival = !rules && ownRealm != null && herRealm != null && ownRealm != herRealm && ownRealm.Culture == herRealm.Culture,
            FiefsBrought = house?.Fiefs.Count ?? 0
         };
      }

      /// <summary>
      ///   Gives her all, in <see cref="LeaveHouseForRulerPolicy.GiveHerAllSteps" /> order. Null when sealed, else why not.
      ///   Every bar is asked again first, so a refusal hands nothing over; past that, there is no undo.
      /// </summary>
      public static string Give(Hero ruler)
      {
         try
         {
            LeaveHouseBar bar = LeaveHouseForRulerBehavior.WhyNot(ruler, LeaveHouseRoad.GiveHerAll);
            if (bar != LeaveHouseBar.None)
            {
               ModLog.Info($"[LEAVE_HOUSE] giving all to {ruler?.Name} refused before anything was given: {bar}.");

               return $"You cannot give your house to {ruler?.Name} now ({bar}).";
            }

            Hero player = Hero.MainHero;
            Clan house = Clan.PlayerClan;
            Kingdom herRealm = ruler.Clan?.Kingdom;
            Kingdom ownRealm = house.Kingdom;
            UnionFacts union = UnionFor(ruler);
            float influence = house.Influence;
            int fiefs = house.Fiefs.Count;

            foreach (GiveHerAllStep step in LeaveHouseForRulerPolicy.GiveHerAllSteps)
            {
               switch (step)
               {
                  case GiveHerAllStep.DisbandTheArmy:
                     if (MobileParty.MainParty?.Army != null) DisbandArmyAction.ApplyByUnknownReason(MobileParty.MainParty.Army);

                     break;
                  case GiveHerAllStep.AbsorbTheRealm:
                     if (union.BringsARealm && herRealm != null) Absorb(ownRealm, herRealm, house);

                     break;
                  case GiveHerAllStep.GiveTheFiefs:
                     foreach (Town town in house.Fiefs.ToList())
                        ChangeOwnerOfSettlementAction.ApplyByGift(town.Settlement, ruler);

                     break;
                  case GiveHerAllStep.GiveTheGold:
                     if (player.Gold > 0) GiveGoldAction.ApplyBetweenCharacters(player, ruler, player.Gold, true);

                     break;
                  case GiveHerAllStep.JoinHerRealm:
                     if (herRealm != null && house.Kingdom != herRealm) ChangeKingdomAction.ApplyByJoinToKingdom(house, herRealm);
                     if (union.BringsARealm && ownRealm != null && !ownRealm.IsEliminated && ownRealm.Clans.Count == 0)
                     {
                        ownRealm.RulingClan = null;
                        DestroyKingdomAction.Apply(ownRealm);
                     }

                     break;
                  case GiveHerAllStep.RestoreTheInfluence:
                     ChangeClanInfluenceAction.Apply(house, influence - house.Influence);

                     break;
                  case GiveHerAllStep.Wed:
                     Wed(player, ruler);
                     if (player.Spouse != ruler)
                     {
                        ModLog.Error($"[LEAVE_HOUSE] the wedding of {player.Name} and {ruler.Name} did not hold after the gift.");

                        return $"Your gift was given, but the wedding with {ruler.Name} did not hold; see the log.";
                     }

                     break;
                  case GiveHerAllStep.SeasonOfWages:
                     int daily = house.WarPartyComponents.Sum(w => w?.MobileParty?.TotalWage ?? 0);
                     int season = LeaveHouseForRulerPolicy.SeasonOfWages(daily);
                     if (season > 0) GiveGoldAction.ApplyBetweenCharacters(ruler, player, season, false);

                     break;
                  case GiveHerAllStep.TheRivalsAnswer:
                     RivalsAnswer(ruler, herRealm, union);

                     break;
               }
            }

            string msg = $"{player.Name} has given {ruler.Name} {fiefs} fief(s), {(union.BringsARealm ? "a realm, " : "")}and all {(player.IsFemale ? "her" : "his")} gold, "
                         + $"and wed {(ruler.IsFemale ? "her" : "him")}. {house.Name} now serves {herRealm?.Name?.ToString() ?? ruler.Clan?.Name?.ToString()}.";
            MBInformationManager.AddQuickInformation(new TextObject(msg));
            StoryArchiveWriter.Notice(msg, ruler.StringId);
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            ModLog.Info($"[LEAVE_HOUSE] gave all: {fiefs} fief(s), realm={union.BringsARealm}, wed {ruler.Name}; influence restored to {house.Influence:0}.");

            return null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[LEAVE_HOUSE] giving all failed: {ex.GetType().Name}: {ex.Message}");

            return "Giving your house failed; see the log.";
         }
      }

      // A realm the player rules: its armies disband and every other house joins hers, held there a year; a mercenary
      // house simply leaves. Never ApplyByLeaveKingdom: it hands the leaving house's fiefs to the old realm's ruler.
      private static void Absorb(Kingdom ownRealm, Kingdom herRealm, Clan house)
      {
         foreach (Army army in ownRealm.Armies.ToList()) DisbandArmyAction.ApplyByUnknownReason(army);

         foreach (Clan c in ownRealm.Clans.ToList())
         {
            if (c == house || c.IsEliminated) continue;
            if (c.IsUnderMercenaryService) ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(c, false);
            else ChangeKingdomAction.ApplyByJoinToKingdom(c, herRealm, CampaignTime.DaysFromNow(AbsorbedHousesStayDays), false);
         }
      }

      // MarriageAction.ApplyInternal without its clan change: each keeps their own house, so she keeps her throne.
      // Not through MarryThroughCr: she heads her own house, so the game's path (the family consents) is the right one.
      private static void Wed(Hero player, Hero ruler)
      {
         MarriageModel model = Campaign.Current.Models.MarriageModel;
         player.Spouse = ruler;
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(player, ruler, model.GetEffectiveRelationIncrease(player, ruler), false);
         CampaignEventDispatcher.Instance.OnBeforeHeroesMarried(player, ruler, true);
         foreach (Romance.RomanticState state in Romance.RomanticStateList)
            if (state.Person1 == player || state.Person2 == player || state.Person1 == ruler || state.Person2 == ruler)
               state.Level = Romance.RomanceLevelEnum.Ended;
         ChangeRomanticStateAction.Apply(player, ruler, Romance.RomanceLevelEnum.Marriage);
      }

      // Her people's rival realms (the same culture, a claim to the same throne) may declare war, as the gift warrants.
      private static void RivalsAnswer(Hero ruler, Kingdom herRealm, UnionFacts union)
      {
         if (herRealm == null) return;

         int chance = LeaveHouseForRulerPolicy.WarChance(union);
         int roll = MBRandom.RandomInt(100);
         ModLog.Info($"[LEAVE_HOUSE] the rivals of {herRealm.Name}: war chance {chance}%, roll {roll}.");
         if (roll >= chance) return;

         foreach (Kingdom rival in Kingdom.All.Where(k => k != herRealm && !k.IsEliminated && k.Culture == herRealm.Culture).ToList())
         {
            if (rival.IsAtWarWith(herRealm)) continue;

            GameState.DiplomacyOutcome door = GameState.DiplomacyCommit.Factions(Logic.Diplomacy.DiplomacyChange.DeclareWar, "union_rivals", rival, herRealm, rival.Leader,
               () => DeclareWarAction.ApplyByDefault(rival, herRealm));
            if (!door.Happened) continue;

            MBInformationManager.AddQuickInformation(new TextObject($"{rival.Name} will not suffer the union of {ruler.Name}'s house: it declares war on {herRealm.Name}."));
         }
      }
   }
}
