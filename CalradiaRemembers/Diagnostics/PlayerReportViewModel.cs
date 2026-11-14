// Code written by Gabriel Mailhot, 14/11/2026.
// The "report a mistake" window (Gabriel, 14/11/2026): "On clique sur le bouton et on voit tout le contexte, les logs, la
// conversation et le joueur peut ajouter un texte explicatif du problème." Shows the report as it will be written, live, so
// the player sees exactly what they share; a switch leaves out what was said (on by default in Hardcore).

#region

using System;
using CalradiaRemembers.Logic.Diagnostics;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>View model of the player report window.</summary>
   public sealed class PlayerReportViewModel : ViewModel
   {
      /// <summary>The most characters of the report the window shows; the file holds all of it.</summary>
      internal const int PreviewChars = 12000;

      internal Action OnCloseRequested;
      internal Action<string> OnCreated;

      private readonly Func<string, bool, PlayerReportFacts> _gather;
      private PlayerReportFacts _facts;
      private string _playerText = "";
      private bool _removeConversation;
      private string _previewText = "";

      internal PlayerReportViewModel(Func<string, bool, PlayerReportFacts> gather, bool removeConversationByDefault)
      {
         _gather = gather;
         _removeConversation = removeConversationByDefault;
         _facts = _gather?.Invoke("", _removeConversation);
         RefreshPreview();
      }

      [DataSourceProperty]
      public string Title => "Report a problem";

      [DataSourceProperty]
      public string Explanation =>
         "Say what went wrong and what you expected. The report below holds this conversation, what the character was given, the model's replies and this session's log. "
         + "Keys are masked. It is saved to a file you join on Discord, and written into CalradiaRemembers.log too. Nothing is sent by itself.";

      [DataSourceProperty]
      public string InputLabel => "What went wrong:";

      [DataSourceProperty]
      public string CreateButtonText => "Create the report";

      [DataSourceProperty]
      public string CancelButtonText => "Cancel";

      [DataSourceProperty]
      public string RemoveButtonText => _removeConversation ? "What was said: left out" : "What was said: included";

      [DataSourceProperty]
      public string PlayerText
      {
         get => _playerText;
         set
         {
            if (_playerText == value) return;
            _playerText = value ?? "";
            OnPropertyChangedWithValue(value);
            RefreshPreview();
         }
      }

      [DataSourceProperty]
      public string PreviewText
      {
         get => _previewText;
         set
         {
            if (_previewText == value) return;
            _previewText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>The report as it will be written, with the player's words and choice.</summary>
      internal string Report => PlayerReportComposer.Compose(WithChoices());

      public void ExecuteToggleRemove()
      {
         _removeConversation = !_removeConversation;
         OnPropertyChanged(nameof(RemoveButtonText));
         RefreshPreview();
      }

      public void ExecuteCreate()
      {
         OnCreated?.Invoke(Report);
         OnCloseRequested?.Invoke();
      }

      public void ExecuteCancel() => OnCloseRequested?.Invoke();

      private PlayerReportFacts WithChoices()
         => new() {
            PlayerText = _playerText, RemoveConversationText = _removeConversation, Header = _facts?.Header, Transcript = _facts?.Transcript,
            Exchanges = _facts?.Exchanges, LogLines = _facts?.LogLines
         };

      private void RefreshPreview() => PreviewText = PlayerReportComposer.Preview(Report, PreviewChars);
   }
}
