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

        private readonly System.Action<MemoryEntryViewModel> _onSelect;
        private bool _isSelected;

        public MemoryEntryViewModel(string meta, string summary, string actionLabel = null,
                                    NpcMemoryService.Core.Models.NotableEvent memory = null, System.Action<MemoryEntryViewModel> onSelect = null)
        {
            _meta = meta ?? "";
            _actionLabel = actionLabel ?? "";
            _summary = Mapping.ShownText.Of(summary ?? "");
            Memory = memory;
            _onSelect = onSelect;
        }

        /// <summary>The memory this row shows (null for the "no memories yet" placeholder).</summary>
        internal NpcMemoryService.Core.Models.NotableEvent Memory { get; set; }

        /// <summary>
        ///   A click on the row selects the memory, and its picture shows beside the panel (Bhelogan, Gabriel, 20/11/2026).
        ///   The parent is called back through the row: a binding to the ChatViewModel from inside the template resolves to
        ///   nothing (see <see cref="SeparatorPath" />).
        /// </summary>
        public void ExecuteSelect() => _onSelect?.Invoke(this);

        /// <summary>The row the player clicked last, drawn with a faint mark.</summary>
        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChangedWithValue(value);
            }
        }

        /// <summary>A small mark beside the date when the memory carries a picture.</summary>
        [DataSourceProperty]
        public bool HasPicture => !string.IsNullOrEmpty(Memory?.ImageFile);

        internal void PictureChanged() => OnPropertyChanged(nameof(HasPicture));

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

        /// <summary>
        ///   Full path to the ornamental divider PNG shown under each entry. It lives HERE, not on the parent
        ///   ChatViewModel, because an ItemTemplate binds against the ROW's data source (this view-model): a
        ///   binding to a ChatViewModel property from inside the template resolves to nothing, so the separator
        ///   never appeared (each row needs its own path property to bind).
        /// </summary>
        [DataSourceProperty]
        public string SeparatorPath => System.IO.Path.Combine(
            CalradiaRemembers.Paths.ModulePaths.Root, "GUI", "Images", "cr_memories_separator.png");

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
