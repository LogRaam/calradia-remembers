// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 Phase 2/3: the chronicle viewer's data. Shows the plain templated deeds or the LLM-illuminated
// manuscript (with the not-yet-illuminated recent deeds as a "rough tail"), with Illuminate / Rewrite /
// toggle controls. Refreshed live when an async illumination pass completes.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chronicle
{
    /// <summary>View model for the chronicle window.</summary>
    public sealed class ChronicleViewModel : ViewModel
    {
        private string _title = "";
        private string _emptyText = "";
        private string _statusText = "";
        private bool _isEmpty;
        private bool _isBusy;
        private bool _canIlluminate;
        private bool _canRewrite;
        private bool _canToggle;
        private string _illuminateButtonText = "Illuminate";
        private string _toggleButtonText = "Show plain";
        private bool _illuminatedView;
        private MBBindingList<ChronicleEntryViewModel> _entries;

        public Action OnCloseRequested;

        public ChronicleViewModel()
        {
            _entries = new MBBindingList<ChronicleEntryViewModel>();
            _illuminatedView = ChronicleIlluminationService.HasIllumination;
            RefreshFromState();
        }

        // ── Bindings ──────────────────────────────────────────────────────────

        [DataSourceProperty] public string Title { get => _title; set { if (_title != value) { _title = value; OnPropertyChangedWithValue(value, nameof(Title)); } } }
        [DataSourceProperty] public string EmptyText { get => _emptyText; set { if (_emptyText != value) { _emptyText = value; OnPropertyChangedWithValue(value, nameof(EmptyText)); } } }
        [DataSourceProperty] public string StatusText { get => _statusText; set { if (_statusText != value) { _statusText = value; OnPropertyChangedWithValue(value, nameof(StatusText)); } } }
        [DataSourceProperty] public bool IsEmpty { get => _isEmpty; set { if (_isEmpty != value) { _isEmpty = value; OnPropertyChangedWithValue(value, nameof(IsEmpty)); } } }
        [DataSourceProperty] public bool HasStatus { get => !string.IsNullOrEmpty(_statusText); }
        [DataSourceProperty] public bool CanIlluminate { get => _canIlluminate; set { if (_canIlluminate != value) { _canIlluminate = value; OnPropertyChangedWithValue(value, nameof(CanIlluminate)); } } }
        [DataSourceProperty] public bool CanRewrite { get => _canRewrite; set { if (_canRewrite != value) { _canRewrite = value; OnPropertyChangedWithValue(value, nameof(CanRewrite)); } } }
        [DataSourceProperty] public bool CanToggle { get => _canToggle; set { if (_canToggle != value) { _canToggle = value; OnPropertyChangedWithValue(value, nameof(CanToggle)); } } }
        [DataSourceProperty] public string IlluminateButtonText { get => _illuminateButtonText; set { if (_illuminateButtonText != value) { _illuminateButtonText = value; OnPropertyChangedWithValue(value, nameof(IlluminateButtonText)); } } }
        [DataSourceProperty] public string ToggleButtonText { get => _toggleButtonText; set { if (_toggleButtonText != value) { _toggleButtonText = value; OnPropertyChangedWithValue(value, nameof(ToggleButtonText)); } } }
        [DataSourceProperty] public string CloseButtonText => "Close";

        [DataSourceProperty]
        public MBBindingList<ChronicleEntryViewModel> Entries
        {
            get => _entries;
            set { if (_entries != value) { _entries = value; OnPropertyChangedWithValue(value, nameof(Entries)); } }
        }

        // ── Commands ──────────────────────────────────────────────────────────

        public void ExecuteIlluminate()
        {
            StatusText = ChronicleIlluminationService.Extend();
            OnPropertyChanged(nameof(HasStatus));
            RefreshFromState();
        }

        public void ExecuteRewrite()
        {
            StatusText = ChronicleIlluminationService.RewriteAll();
            OnPropertyChanged(nameof(HasStatus));
            RefreshFromState();
        }

        public void ExecuteToggleView()
        {
            _illuminatedView = !_illuminatedView;
            RefreshFromState();
        }

        public void ExecuteClose() => OnCloseRequested?.Invoke();

        // ── Refresh ───────────────────────────────────────────────────────────

        /// <summary>Rebuilds the window from current state — also called when an async pass completes.</summary>
        public void RefreshFromState()
        {
            string player = Hero.MainHero?.Name?.ToString() ?? "the player";
            Title = $"THE CHRONICLE OF {player.ToUpperInvariant()}";

            _isBusy = ChronicleIlluminationService.IsBusy;
            bool hasIllum = ChronicleIlluminationService.HasIllumination;
            int pending = ChronicleIlluminationService.PendingDeedCount();

            if (_isBusy) StatusText = "The chronicler sets quill to vellum.. (this may take a moment)";
            OnPropertyChanged(nameof(HasStatus));

            CanIlluminate = !_isBusy && pending > 0;
            CanRewrite    = !_isBusy && hasIllum;
            CanToggle     = hasIllum;
            IlluminateButtonText = hasIllum ? "Illuminate new deeds" : "Illuminate";
            ToggleButtonText = _illuminatedView ? "Show plain" : "Show illuminated";

            BuildEntries(hasIllum);
        }

        private void BuildEntries(bool hasIllum)
        {
            Entries.Clear();
            IsEmpty = false;

            IReadOnlyList<WorldEvent> deeds = WorldEventStore.Instance?.GetPlayerChronicle();
            if (deeds == null || deeds.Count == 0)
            {
                IsEmpty = true;
                EmptyText = "The chronicle of your deeds is yet unwritten.";
                return;
            }

            if (_illuminatedView && hasIllum)
            {
                Entries.Add(new ChronicleEntryViewModel("", ChronicleIlluminationService.Illuminated));

                int through = ChronicleIlluminationService.ThroughCount;
                if (through < deeds.Count)
                {
                    Entries.Add(new ChronicleEntryViewModel("", "— and since, not yet set to fine hand: —"));
                    for (int i = through; i < deeds.Count; i++)
                        Entries.Add(MakeTemplatedEntry(deeds[i]));
                }
                return;
            }

            foreach (WorldEvent e in deeds)
                Entries.Add(MakeTemplatedEntry(e));
        }

        private static ChronicleEntryViewModel MakeTemplatedEntry(WorldEvent e)
        {
            string body = string.IsNullOrWhiteSpace(e.ChronicleLine) ? e.Summary : e.ChronicleLine;
            return new ChronicleEntryViewModel(ChronicleWriter.DatePhrase(e.Day), Capitalize(body));
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

        [DataSourceProperty]
        public bool HasDate => !string.IsNullOrEmpty(_dateLine);
    }
}
