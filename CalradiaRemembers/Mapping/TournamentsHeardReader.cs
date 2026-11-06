// Code written by Gabriel Mailhot, 05/11/2026.
// The host side of TournamentsHeard. 3chmidt (Nexus, 05/11/2026): "lords know in what town currently tournaments happen,
// at least in the same kingdom or a neighbouring one". Reads the tournaments the game holds now (TournamentManager, one per
// town at most), from where the conversation takes place (the player's party), and hands the pure policy who holds each and
// how far it is.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Tournaments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The tournaments under way a character would know of.</summary>
   public static class TournamentsHeardReader
   {
      /// <summary>The lines for the prompt; null when none is known.</summary>
      public static IReadOnlyList<string> Read(Hero speaker)
      {
         try
         {
            List<string> lines = TournamentsHeard.Lines(Held(speaker));

            return lines.Count == 0 ? null : lines;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[TOURNAMENT] tournaments heard failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Every tournament the game holds now, seen from where the speaker talks.</summary>
      internal static List<HeldTournament> Held(Hero speaker)
      {
         var held = new List<HeldTournament>();
         ITournamentManager manager = Campaign.Current?.TournamentManager;
         if (manager == null) return held;

         IFaction own = speaker?.MapFaction ?? Hero.MainHero?.MapFaction;
         Vec2 here = MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero;
         foreach (Town town in Town.AllTowns)
         {
            TournamentGame game = town == null ? null : manager.GetTournamentGame(town);
            if (game == null || town.Settlement == null) continue;

            IFaction realm = town.Settlement.MapFaction;
            held.Add(new HeldTournament {
               Town = town.Name?.ToString() ?? "",
               Realm = realm?.Name?.ToString() ?? "",
               InOwnRealm = own != null && realm == own,
               Distance = town.Settlement.GetPosition2D.Distance(here),
               Prize = game.Prize?.Name?.ToString() ?? ""
            });
         }

         return held;
      }
   }
}
