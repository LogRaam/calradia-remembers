// Code written by Gabriel Mailhot, 08/07/2026.
// Appearance pillar: the custom modal popup for authoring the player's appearance note. A real Gauntlet layer
// (not ShowTextInquiry) so it can be larger AND so its focus layer captures Backspace (the inquiry popup lets the
// encyclopedia steal Backspace as a BACK navigation). Shows a live preview of the full "how others see you" line
// as the player types, so they can see exactly how their note reads appended to the auto-derived description.

#region

using System;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Appearance
{
    /// <summary>View model for the appearance-note editor popup.</summary>
    public sealed class AppearanceEditorViewModel : ViewModel
    {
        public Action OnCloseRequested;
        public Action<string> OnConfirm;

        private readonly Func<string, string> _composePreview;
        private string _noteText;
        private string _previewText = "";

        public AppearanceEditorViewModel(string currentNote, Func<string, string> composePreview)
        {
            _composePreview = composePreview;
            _noteText = currentNote ?? "";
            RefreshPreview();
        }

        [DataSourceProperty]
        public string Title => "Describe Your Appearance";

        [DataSourceProperty]
        public string Explanation =>
            "Add a detail others can see: a scar, a limp, a certain air. Write it in the third person; it follows the description read from your build, age, and colouring.";

        [DataSourceProperty]
        public string PreviewLabel => "How others will see you:";

        [DataSourceProperty]
        public string ConfirmButtonText => "Confirm";

        [DataSourceProperty]
        public string CancelButtonText => "Cancel";

        [DataSourceProperty]
        public string NoteText
        {
            get => _noteText;
            set
            {
                if (_noteText == value) return;
                _noteText = value;
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

        public void ExecuteConfirm()
        {
            OnConfirm?.Invoke(_noteText ?? "");
            OnCloseRequested?.Invoke();
        }

        public void ExecuteCancel() => OnCloseRequested?.Invoke();

        private void RefreshPreview()
            => PreviewText = _composePreview?.Invoke(_noteText ?? "") ?? (_noteText ?? "");
    }
}
