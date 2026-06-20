// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 Phase 2: the data behind the illuminated chronicle viewer — the player's deeds, in order,
// each a dated chronicler-voice entry. Bound to CalradiaRemembersChronicle.xml.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chronicle
{
    /// <summary>View model for the chronicle window: a title and an ordered list of dated entries.</summary>
    public sealed class ChronicleViewModel : ViewModel
    {
        private string _title = "";
        private string _emptyText = "";
        private bool _isEmpty;
        private MBBindingList<ChronicleEntryViewModel> _entries;

        public Action OnCloseRequested;

        public ChronicleViewModel()
        {
            _entries = new MBBindingList<ChronicleEntryViewModel>();
            Load();
        }

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set { if (_title != value) { _title = value; OnPropertyChangedWithValue(value, nameof(Title)); } }
        }

        [DataSourceProperty]
        public string EmptyText
        {
            get => _emptyText;
            set { if (_emptyText != value) { _emptyText = value; OnPropertyChangedWithValue(value, nameof(EmptyText)); } }
        }

        [DataSourceProperty]
        public bool IsEmpty
        {
            get => _isEmpty;
            set { if (_isEmpty != value) { _isEmpty = value; OnPropertyChangedWithValue(value, nameof(IsEmpty)); } }
        }

        [DataSourceProperty]
        public MBBindingList<ChronicleEntryViewModel> Entries
        {
            get => _entries;
            set { if (_entries != value) { _entries = value; OnPropertyChangedWithValue(value, nameof(Entries)); } }
        }

        [DataSourceProperty]
        public string CloseButtonText => "Close";

        public void ExecuteClose() => OnCloseRequested?.Invoke();

        private void Load()
        {
            string player = Hero.MainHero?.Name?.ToString() ?? "the player";
            Title = $"THE CHRONICLE OF {player.ToUpperInvariant()}";

            IReadOnlyList<WorldEvent> deeds = WorldEventStore.Instance?.GetPlayerChronicle();
            if (deeds == null || deeds.Count == 0)
            {
                IsEmpty = true;
                EmptyText = "The chronicle of your deeds is yet unwritten.";
                return;
            }

            foreach (WorldEvent e in deeds)
            {
                string body = string.IsNullOrWhiteSpace(e.ChronicleLine) ? e.Summary : e.ChronicleLine;
                if (string.IsNullOrWhiteSpace(body)) continue;
                Entries.Add(new ChronicleEntryViewModel(ChronicleWriter.DatePhrase(e.Day), Capitalize(body)));
            }
        }

        private static string Capitalize(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1) + (s.EndsWith(".") ? "" : ".");
    }

    /// <summary>One chronicle entry: a chronicler's date heading and the deed itself.</summary>
    public sealed class ChronicleEntryViewModel : ViewModel
    {
        private string _dateLine;
        private string _body;

        public ChronicleEntryViewModel(string dateLine, string body)
        {
            _dateLine = dateLine;
            _body = body;
        }

        [DataSourceProperty]
        public string DateLine
        {
            get => _dateLine;
            set { if (_dateLine != value) { _dateLine = value; OnPropertyChangedWithValue(value, nameof(DateLine)); } }
        }

        [DataSourceProperty]
        public string Body
        {
            get => _body;
            set { if (_body != value) { _body = value; OnPropertyChangedWithValue(value, nameof(Body)); } }
        }
    }
}
