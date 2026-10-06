// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales: one line of the numbered list under a scene's narration ("1. Take the left path", "3. Something
// else..."). What it shows and what a click sends are decided by Logic.AuthoredQuests.SceneChoiceMenu; this only
// carries it to the prefab and hands the click back to the chat.

#region

using System;
using CalradiaRemembers.Logic.AuthoredQuests;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>One clickable line of a scene's way-out list.</summary>
   public sealed class SceneChoiceItemViewModel : ViewModel
   {
      private readonly Action<SceneChoiceMenuItem> _chosen;

      public SceneChoiceItemViewModel(SceneChoiceMenuItem item, Action<SceneChoiceMenuItem> chosen)
      {
         Item = item;
         _chosen = chosen;
      }

      public SceneChoiceMenuItem Item { get; }

      [DataSourceProperty]
      public string Text => Item.Text;

      /// <summary>The Classic skin's brush, on the same "Chat Text Size (%)" step as the dialogue lines.</summary>
      [DataSourceProperty]
      public string ClassicChoiceBrush => "CR.SceneChoice.Classic." + ModSettings.ChatTextSizeStep;

      /// <summary>The Dark Leather (Manuscript) skin's twin.</summary>
      [DataSourceProperty]
      public string ManuscriptChoiceBrush => "CR.SceneChoice.Manuscript." + ModSettings.ChatTextSizeStep;

      public void ExecuteChoose() => _chosen?.Invoke(Item);
   }
}
