// Code written by Gabriel Mailhot, 09/07/2026.

#region

using System;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>View model for the portrait-spike test overlay: a runtime sprite plus a status line.</summary>
    public sealed class PortraitSpikeViewModel : ViewModel
    {
        public Action OnCloseRequested;

        private Sprite _portrait;
        private string _status = "";

        [DataSourceProperty]
        public Sprite Portrait
        {
            get => _portrait;
            set
            {
                if (_portrait == value) return;
                _portrait = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string CloseButtonText => "Close";

        public void ExecuteClose() => OnCloseRequested?.Invoke();
    }
}
