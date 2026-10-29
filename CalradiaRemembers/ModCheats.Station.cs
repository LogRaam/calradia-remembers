// Code written by Gabriel Mailhot, 28/10/2026.
// A character's station and where the player stands beside them, as the prompt states them (Gabriel, 28/10/2026: one
// definition per social status, adjusting to the player's rung), for play-testing the station pillar.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Social;
using CalradiaRemembers.Mapping;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.station [name]: the station a character is voiced as, the player's rung, and the lines the prompt gives them.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("station", "cr")]
      public static string Station(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         PlayerRung rung = PlayerRungReader.Rung();

         return $"{hero.Name}: station = {SocialStationReader.StationOf(hero) ?? "(none, voiced by the guidelines alone)"}; "
                + $"player rung = {rung} (tier {StatureReader.Player.ClanTier}, level {Hero.MainHero?.Level}).\n"
                + $"Who: {SocialStationReader.Who(hero) ?? "(nothing)"}\n"
                + $"Beside the player: {PlayerRungReader.LineFor(hero) ?? "(nothing)"}";
      }
   }
}
