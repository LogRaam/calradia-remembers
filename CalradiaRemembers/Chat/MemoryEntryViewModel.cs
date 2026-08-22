// Code written by Gabriel Mailhot, 07/07/2026.

#region

using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
    /// <summary>
    ///   One row in the in-chat "shared memories" panel: a muted meta line (the dated day, and the event type
    ///   when it is not a plain remark) above the memory's summary. A read-only mirror of one <c>cr.memory</c>
    ///   entry; the panel rebuilds the whole list from the NPC's profile each time it opens, so these are never
    ///   mutated in place.
    /// </summary>
    public sealed class MemoryEntryViewModel : ViewModel
    {
        private string _meta;
        private string _actionLabel;
        private string _summary;

        public MemoryEntryViewModel(string meta, string summary, string actionLabel = null)
        {
            _meta = meta ?? "";
            _actionLabel = actionLabel ?? "";
            _summary = summary ?? "";
        }

        [DataSourceProperty]
        public string Meta
        {
            get => _meta;
            set
            {
                if (_meta == value) return;
                _meta = value;
                OnPropertyChangedWithValue(value);
            }
        }

        /// <summary>
        ///   The event's type shown as a coloured tag beside the date (e.g. "Collaboration") when a real deed or
        ///   interaction was recorded, empty for a plain remark. Rendered in blue by the panel, apart from the
        ///   muted date, so a memory that carries an emitted action reads at a glance.
        /// </summary>
        [DataSourceProperty]
        public string ActionLabel
        {
            get => _actionLabel;
            set
            {
                if (_actionLabel == value) return;
                _actionLabel = value;
                OnPropertyChangedWithValue(value);
                OnPropertyChanged(nameof(HasActionLabel));
            }
        }

        /// <summary>True when this memory carries a coloured action tag, so the panel shows the blue label only then.</summary>
        [DataSourceProperty]
        public bool HasActionLabel => !string.IsNullOrEmpty(_actionLabel);

        [DataSourceProperty]
        public string Summary
        {
            get => _summary;
            set
            {
                if (_summary == value) return;
                _summary = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}
