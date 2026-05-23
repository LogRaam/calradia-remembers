// Code written by Gabriel Mailhot, 19/05/2026.

#region

using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
    public sealed class ChatMessageViewModel : ViewModel
    {
        private const string NpcColor = "#e8d8b8FF"; // warm cream
        private const string PlayerColor = "#a8c8e0FF"; // cool blue

        private string _authorColor;
        private string _avatarColor;
        private string _avatarInitial;
        private string _displayText;

        public ChatMessageViewModel(string authorName, string messageText, bool isPlayer)
        {
            _displayText = BuildDisplayText(authorName, messageText);

            _avatarInitial = string.IsNullOrEmpty(authorName)
                ? "?"
                : authorName.Substring(0, 1).ToUpperInvariant();

            var color = isPlayer
                ? PlayerColor
                : NpcColor;
            _authorColor = color;
            _avatarColor = color;
        }

        [DataSourceProperty]
        public string AuthorColor
        {
            get => _authorColor;
            set
            {
                if (_authorColor == value) return;
                _authorColor = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string AvatarColor
        {
            get => _avatarColor;
            set
            {
                if (_avatarColor == value) return;
                _avatarColor = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string AvatarInitial
        {
            get => _avatarInitial;
            set
            {
                if (_avatarInitial == value) return;
                _avatarInitial = value;
                OnPropertyChangedWithValue(value);
            }
        }

        /// <summary>
        ///   Texte complet affiché : "<b>Nom:</b> message"
        /// </summary>
        [DataSourceProperty]
        public string DisplayText
        {
            get => _displayText;
            set
            {
                if (_displayText == value) return;
                _displayText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        #region private

        private static string BuildDisplayText(string authorName, string messageText)
        {
            if (string.IsNullOrEmpty(authorName))
                authorName = "Inconnu";

            // Nom en gras doré + retour à la ligne + message en dessous
            //return $"<b><color=#ffd700>{authorName}:</color></b>\n {messageText}";
            return $"{authorName}\n— {messageText}";
        }

        #endregion
    }
}