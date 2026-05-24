// Code written by Gabriel Mailhot, 19/05/2026.

#region

using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
    public sealed class ChatMessageViewModel : ViewModel
    {
        private const string NpcColor = "#e8d8b8FF"; // warm cream
        private const string PlayerColor = "#a8c8e0FF"; // cool blue

        private string _authorColor;
        private string _displayText;

        // Flattened portrait properties.
        // This avoids nested Gauntlet bindings such as @Portrait.Id inside an ItemTemplate.
        private string _portraitId = "";
        private string _portraitTextureProviderName = "";
        private string _portraitAdditionalArgs = "";

        public ChatMessageViewModel(Hero speakerHero, string authorName, string messageText, bool isPlayer)
        {
            _displayText = BuildDisplayText(authorName, messageText);

            if (speakerHero?.CharacterObject != null)
            {
                CharacterImageIdentifierVM portrait =
                    new CharacterImageIdentifierVM(CharacterCode.CreateFrom(speakerHero.CharacterObject));

                _portraitId = portrait.Id ?? "";
                _portraitTextureProviderName = portrait.TextureProviderName ?? "";
                _portraitAdditionalArgs = portrait.AdditionalArgs ?? "";
            }

            ModLog.Info(
                $"Portrait for {authorName}: " +
                $"HeroNull={speakerHero == null}, " +
                $"CharacterObjectNull={speakerHero?.CharacterObject == null}, " +
                $"IdEmpty={string.IsNullOrEmpty(_portraitId)}, " +
                $"TextureProvider='{_portraitTextureProviderName}', " +
                $"Args='{_portraitAdditionalArgs}'");

            _authorColor = isPlayer ? PlayerColor : NpcColor;
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
        public string PortraitId
        {
            get => _portraitId;
            set
            {
                if (_portraitId == value) return;
                _portraitId = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string PortraitTextureProviderName
        {
            get => _portraitTextureProviderName;
            set
            {
                if (_portraitTextureProviderName == value) return;
                _portraitTextureProviderName = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string PortraitAdditionalArgs
        {
            get => _portraitAdditionalArgs;
            set
            {
                if (_portraitAdditionalArgs == value) return;
                _portraitAdditionalArgs = value;
                OnPropertyChangedWithValue(value);
            }
        }

        /// <summary>
        ///   Texte complet affiché : "Nom" puis le message à la ligne suivante.
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

            return $"{authorName}\n— {messageText}";
        }

        #endregion
    }
}
