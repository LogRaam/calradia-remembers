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
        private string _summary;

        public MemoryEntryViewModel(string meta, string summary)
        {
            _meta = meta ?? "";
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
