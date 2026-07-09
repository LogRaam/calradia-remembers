// Code written by Gabriel Mailhot, 08/07/2026.

#region

using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.give_prisoner [name]
      ///   Captures a live lord straight into the player's party as a hero prisoner, so the prisoner-trade UI
      ///   (the bag's "A prisoner" offer) and the buy/sell verbs can be tested without hunting one down in game.
      ///   With a name fragment it takes that lord; without, the first free non-player-clan lord it finds.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("give_prisoner", "cr")]
      public static string GivePrisoner(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (Hero.MainHero == null || MobileParty.MainParty == null) return "No player party (load a campaign first).";

         Hero target = ResolveGiveablePrisoner(args);
         if (target == null)
            return "No capturable lord found. Pass a name fragment, or make sure a free (unheld) lord exists.";

         TakePrisonerAction.Apply(PartyBase.MainParty, target);
         if (!target.IsPrisoner)
            return $"Failed to capture {target.Name}.";

         return $"{target.Name} is now your prisoner. In a conversation, click the bag and choose 'A prisoner' to test the offer.";
      }

      /// <summary>The named lord (when a fragment is given), else the first free, alive, non-player-clan lord not already held.</summary>
      private static Hero ResolveGiveablePrisoner(List<string> args)
      {
         bool Eligible(Hero h) => h != null && h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner;

         if (args != null && args.Count > 0)
         {
            Hero named = ResolveHero(args);

            return named != null && Eligible(named) ? named : null;
         }

         return Hero.AllAliveHeroes.FirstOrDefault(h => Eligible(h) && h.Clan != Clan.PlayerClan);
      }
   }
}
