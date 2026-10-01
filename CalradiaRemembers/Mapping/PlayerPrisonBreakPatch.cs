// Code written by Gabriel Mailhot, 01/10/2026.
// The game's own prison break (SandBox PrisonBreakCampaignBehavior) ends the captivity with
// EndCaptivityAction.ApplyByEscape(prisoner, Hero.MainHero): the player is named as the one who made it possible,
// and the freed lord says "I will remember your help". But HeroPrisonerReleased, the event every listener hears, does
// not carry that name, so CR could not tell a player's prison break from a lord slipping out alone, and remembered
// none of it. This prefix notes, for the length of that one call, who is being broken out by the player and from
// which prison; the release listeners that fire inside the call (the rescue gratitude, the rescue task) ask here.

#region

using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Who the player is breaking out of prison right now, for the listeners of that release.</summary>
   [HarmonyPatch(typeof(EndCaptivityAction), nameof(EndCaptivityAction.ApplyByEscape))]
   public static class PlayerPrisonBreakPatch
   {
      private static Hero _freed;
      private static string _prison;

      /// <summary>Notes a player-facilitated escape before the engine raises the release.</summary>
      public static void Prefix(Hero character, Hero facilitator)
      {
         try
         {
            _freed = null;
            _prison = null;
            if (character == null || facilitator == null || facilitator != Hero.MainHero) return;

            _freed = character;
            _prison = character.PartyBelongedToAsPrisoner?.Settlement?.Name?.ToString();
         }
         catch (Exception ex) { ModLog.Error($"[RESCUE] prison break prefix failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Always clears, even when the engine throws, so a stale note never credits a later release.</summary>
      public static Exception Finalizer(Exception __exception)
      {
         _freed = null;
         _prison = null;

         return __exception;
      }

      /// <summary>True while <paramref name="hero" /> is being broken out by the player, with the prison's name when held in one.</summary>
      public static bool IsUnderway(Hero hero, out string prison)
      {
         prison = hero != null && hero == _freed ? _prison : null;

         return hero != null && hero == _freed;
      }

      /// <summary>cr.testall seam: marks a break as underway without freeing anyone; pass null to clear.</summary>
      internal static void DebugMark(Hero hero, string prison)
      {
         _freed = hero;
         _prison = hero == null ? null : prison;
      }
   }
}
