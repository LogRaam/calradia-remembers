// Code written by Gabriel Mailhot, 30/10/2026.
// The letter box's window (Gabriel, 30/10/2026): the letters waiting, by order of arrival, with their urgent and ribbon
// marks, each to open or throw away; below them, the subjects the player silenced, each to accept again. Hosted on the
// shared CrPanelHost (a pushed state, the one rule for a full-screen panel). Opening a letter closes the box, reads the
// letter in the usual window, and brings the box back when that window closes: the two panels are never stacked.

#region

using CalradiaRemembers.Foundation;
using CalradiaRemembers.UI;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>Show/hide lifecycle for the letter box window.</summary>
   public static class LetterBoxWindow
   {
      private static LetterBoxViewModel _vm;

      // Set when a letter is opened from the box: the box comes back once the letter window has closed, on a later tick.
      private static bool _returnToBox;

      private static readonly CrPanelHost Host = new CrPanelHost(
         "CalradiaRemembersLetterBox",
         close =>
         {
            _vm = new LetterBoxViewModel {OnCloseRequested = close};

            return _vm;
         });

      static LetterBoxWindow() => LetterBox.Changed += () => { if (Host.IsOpen) _vm?.Refresh(); };

      /// <summary>True while the box is open.</summary>
      public static bool IsOpen => Host.IsOpen;

      /// <summary>Opens the box (the map bar's letter icon, or cr.letterbox show).</summary>
      public static void Open()
      {
         if (Host.IsOpen || LetterWindowManager.IsOpen) return;

         // A letter is being opened: the box would only hide it. It opens with the letter's return instead.
         if (LetterBox.IsWriting)
         {
            TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage("A letter is being opened; the box follows it."));
            _returnToBox = true;

            return;
         }

         _returnToBox = false;
         Host.Open();
      }

      /// <summary>Closes the box.</summary>
      public static void Close()
      {
         Host.Close();
         _vm = null;
      }

      /// <summary>Reads one waiting letter: the box closes, the letter opens, and the box returns after.</summary>
      internal static void Read(BoxedLetter boxed)
      {
         if (LetterBox.IsWriting)
         {
            TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage("Another letter is being opened; one at a time."));

            return;
         }

         Close();
         _returnToBox = true;
         if (!LetterBox.Open(boxed)) ModLog.Info("[LETTERBOX] the letter could not be opened now; the box returns.");
      }

      /// <summary>Called from the application tick: brings the box back once a letter opened from it has closed.</summary>
      public static void Tick()
      {
         LetterBox.Tick(); // an envelope written for the player opens here
         if (!_returnToBox || LetterBox.IsWriting || !LetterBox.OnTheMap() || LetterWindowManager.IsOpen || CrPanelHost.AnyPanelOwnsScreen) return;

         _returnToBox = false;
         ModLog.Info("[LETTERBOX] the letter is closed; the box opens again.");
         Open();
      }

      /// <summary>At session launch: forget a window from a prior session.</summary>
      public static void ResetForSession()
      {
         Host.ResetForSession();
         _vm = null;
         _returnToBox = false;
      }
   }
}
