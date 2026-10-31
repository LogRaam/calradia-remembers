// Code written by Gabriel Mailhot, 28/10/2026.
// Extension Surface: another mod forbids a conception Calradia Remembers would create. tashmetu (Nexus, 28/10/2026):
// Traits of Calradia's Sterile character is told "you have no child of your blood and know you will have none", while
// CR's own conception (an intimate act carried to completion, a tale's reward, a lover's child) never asked. Only the mod
// that owns the trait knows it, so it registers a veto here (Gabriel approved, 28/10/2026). The engine's own daily
// pregnancies are not CR's: a mod governs those itself.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>Lets another mod forbid the conceptions Calradia Remembers creates.</summary>
   public static class CrConception
   {
      /// <summary>
      ///   Registers a veto under <paramref name="source" /> (your mod's name, shown in the log). Before CR creates a
      ///   pregnancy it calls <paramref name="veto" /> with the mother and the father (null when the father is unknown);
      ///   return a few words saying why (e.g. "Sterile") to forbid it, or null to let it be. A veto that throws is logged
      ///   and never forbids. Registering the same source again replaces its veto. Not saved: register again when a game
      ///   is loaded. Since 2.8.0.
      /// </summary>
      public static void RegisterVeto(string source, Func<Hero, Hero, string> veto)
      {
         if (PregnancyService.Vetoes.Register(source, veto)) ModLog.Info($"[EXT CONCEPTION] veto registered by '{source}'");
      }

      /// <summary>Withdraws the veto of <paramref name="source" />. Returns whether one was there.</summary>
      public static bool UnregisterVeto(string source) => PregnancyService.Vetoes.Unregister(source);
   }
}
