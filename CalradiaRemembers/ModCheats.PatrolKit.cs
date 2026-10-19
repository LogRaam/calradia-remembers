// Code written by Gabriel Mailhot, 16/10/2026.
// A test helper born with the companion patrol's engine spike (cr.patrol_spike, removed 17/10/2026 once the patrol was
// built). Gabriel's test save starts at the beginning of a game, with no companions and few men; this fills the party
// so a patrol (cr.patrol) can be ordered and watched.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.patrol_kit [companions] [men]: fills a fresh test party (Gabriel, 17/10/2026: his test save is at the start
      ///   of the game). Mints wanderer companions into the clan and party, adds recruits of the player's culture (half
      ///   basic, half one tier up) and grain to feed them.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("patrol_kit", "cr")]
      public static string PatrolKit(List<string> args)
      {
         if (Campaign.Current == null || MobileParty.MainParty == null || Clan.PlayerClan == null) return "No campaign is running.";

         int companions = 2, men = 60;
         if (args is {Count: > 0} && int.TryParse(args[0], out int c) && c >= 0) companions = Math.Min(c, 6);
         if (args is {Count: > 1} && int.TryParse(args[1], out int m) && m >= 0) men = Math.Min(m, 500);

         try
         {
            var names = new List<string>();
            List<CharacterObject> templates = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>()
                                                             .Where(t => t != null && t.IsTemplate && t.Occupation == Occupation.Wanderer)
                                                             .OrderBy(_ => MBRandom.RandomFloat)
                                                             .Take(companions)
                                                             .ToList();
            int age = (Campaign.Current.Models?.AgeModel?.HeroComesOfAge ?? 18) + 7;
            foreach (CharacterObject template in templates)
            {
               Hero minted = HeroCreator.CreateSpecialHero(template, null, null, null, age);
               minted.ChangeState(Hero.CharacterStates.Active);
               AddCompanionAction.Apply(Clan.PlayerClan, minted);
               AddHeroToPartyAction.Apply(minted, MobileParty.MainParty, false);
               names.Add(minted.Name.ToString());
            }

            CharacterObject basic = Hero.MainHero.Culture?.BasicTroop;
            CharacterObject veteran = basic?.UpgradeTargets?.FirstOrDefault() ?? basic;
            var added = 0;
            if (basic != null && men > 0)
            {
               int half = men / 2;
               MobileParty.MainParty.MemberRoster.AddToCounts(basic, men - half);
               MobileParty.MainParty.MemberRoster.AddToCounts(veteran, half);
               added = men;
            }

            ItemObject grain = Game.Current.ObjectManager.GetObject<ItemObject>("grain");
            if (grain != null) MobileParty.MainParty.ItemRoster.AddToCounts(grain, 20 + men / 4);

            ModLog.Info($"[PATROL] kit: companions {string.Join(", ", names)}; {added} men ({basic?.Name} / {veteran?.Name}).");

            return $"Added {names.Count} companion(s) ({string.Join(", ", names)}) and {added} men ({basic?.Name} / {veteran?.Name}), plus grain.";
         }
         catch (Exception ex)
         {
            return $"Kit failed: {ex.GetType().Name}: {ex.Message}";
         }
      }
   }
}
