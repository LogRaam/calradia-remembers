// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac Inc 3, the show/hide lifecycle for the "Balance of Power" panel, mirroring ChronicleManager: it
// gathers the live per-kingdom facts, scores them, and opens a Gauntlet layer with the prefab over the top
// screen. Opened from cr.powerindex's sibling command cr.balanceofpower for now (quick visual iteration); the
// leak-free campaign-bar entry (the council staged-scene recipe) is a later step, since a layer left open over
// the LIVE map leaks VRAM (see project_council_hosting_leak). A read-and-close window opened briefly is fine.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.WorldAlmanac;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>Show/hide lifecycle for the Balance of Power window.</summary>
   public static class BalanceOfPowerManager
   {
      private const string PrefabName = "CalradiaRemembersBalanceOfPower";
      private static ScreenBase _host;

      private static GauntletLayer _layer;
      private static GauntletMovieIdentifier _movieId;
      private static BalanceOfPowerViewModel _vm;

      public static bool IsOpen => _layer != null;

      public static void Close()
      {
         if (!IsOpen) return;

         _layer.IsFocusLayer = false;
         _layer.InputRestrictions.ResetInputRestrictions();
         _layer.ReleaseMovie(_movieId);
         _host?.RemoveLayer(_layer);

         _layer = null;
         _movieId = null;
         _host = null;
         _vm = null;

         ModLog.Info("Balance of Power window closed.");
      }

      public static void Open()
      {
         if (IsOpen) return;

         _host = ScreenManager.TopScreen;
         if (_host == null)
         {
            ModLog.Error("BalanceOfPowerManager.Open: no top screen available.");

            return;
         }

         List<KingdomPowerFacts> facts = KingdomPowerFactsBuilder.Build();
         PowerIndexResult result = PowerIndexPolicy.Compute(facts);
         _vm = new BalanceOfPowerViewModel(result, facts) {OnCloseRequested = Close};

         _layer = new GauntletLayer("CalradiaRemembersBalanceOfPower", 11000);
         _layer.InputRestrictions.SetInputRestrictions();
         _movieId = _layer.LoadMovie(PrefabName, _vm);

         _host.AddLayer(_layer);
         _layer.IsFocusLayer = true;
         ScreenManager.TrySetFocus(_layer);

         ModLog.Info($"Balance of Power window opened on {_host.GetType().Name} ({result.Ranked.Count} kingdoms).");
      }
   }
}
