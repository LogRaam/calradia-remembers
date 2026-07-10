// Code written by Gabriel Mailhot, 23/05/2026.

#region

using System;
using System.IO;
using CalradiaRemembers.GUI;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
    public sealed class ChatMessageViewModel : ViewModel, IManagedPortrait
    {
        private const string NpcColor = "#e8d8b8FF"; // warm cream
        private const string PlayerColor = "#a8c8e0FF"; // cool blue

        private string _authorColor;
        private string _displayText;
        private bool _showAvatar = true;
        private string _portraitAdditionalArgs = "";

        // PORTRAIT SPIKE: when this is a player line and a custom portrait PNG is present, show it (larger)
        // instead of the vanilla 3D render, so the player's custom face sits beside the NPCs' vanilla ones.
        private string _playerPortraitPath = "";
        private bool _showCustomPortrait;
        private float _avatarWidth = 96f;
        private float _avatarHeight = 88f;
        private float _textMarginLeft = 112f;

        // The encyclopedia link of the hero who spoke this line, for the right-click on their portrait.
        // Empty for a heroless speaker (a bandit troop) or a system line, whose right-click is a no-op.
        private string _encyclopediaLink = "";

        // Flattened portrait properties.
        // This avoids nested Gauntlet bindings such as @Portrait.Id inside an ItemTemplate.
        private string _portraitId = "";
        private string _portraitTextureProviderName = "";

        // A stable identity for this line's speaker (Hero StringId, else the portrait troop's StringId, else the
        // author name). The chat keeps at most ONE live portrait tableau per distinct speaker key, so a long
        // transcript never holds more GPU render-targets than there are participants present in the room.
        private string _speakerKey = "";

        public ChatMessageViewModel(Hero speakerHero, string authorName, string messageText, bool isPlayer)
        {
            _displayText = BuildDisplayText(authorName, messageText);
            ResolvePortrait(speakerHero?.CharacterObject, authorName);
            _speakerKey = speakerHero?.StringId ?? authorName ?? "";
            _encyclopediaLink = speakerHero?.EncyclopediaLink ?? "";
            _authorColor = isPlayer ? PlayerColor : NpcColor;
            ApplyCustomPortrait(speakerHero?.StringId, speakerHero?.Name?.ToString());
        }

        /// <summary>
        ///   Heroless-speaker variant: renders the portrait from a bare <see cref="CharacterObject" />
        ///   (e.g. a bandit troop standing in for a synthesized brigand captor that has no real Hero).
        /// </summary>
        public ChatMessageViewModel(CharacterObject portraitCharacter, string authorName, string messageText, bool isPlayer)
        {
            _displayText = BuildDisplayText(authorName, messageText);
            ResolvePortrait(portraitCharacter, authorName);
            _speakerKey = portraitCharacter?.StringId ?? authorName ?? "";
            _authorColor = isPlayer ? PlayerColor : NpcColor;
            ApplyCustomPortrait(portraitCharacter?.StringId, portraitCharacter?.Name?.ToString());
        }

        /// <summary>
        ///   Witness variant: uses an explicitly provided color so each witness
        ///   gets their own distinct hue in the chat window.
        /// </summary>
        public ChatMessageViewModel(Hero speakerHero, string authorName, string messageText, string colorHex)
        {
            _displayText = BuildDisplayText(authorName, messageText);
            ResolvePortrait(speakerHero?.CharacterObject, authorName);
            _speakerKey = speakerHero?.StringId ?? authorName ?? "";
            _encyclopediaLink = speakerHero?.EncyclopediaLink ?? "";
            _authorColor = colorHex ?? NpcColor;
            ApplyCustomPortrait(speakerHero?.StringId, speakerHero?.Name?.ToString());
        }

        /// <summary>
        ///   Heroless witness variant: renders the portrait from a bare <see cref="CharacterObject" />
        ///   (a bandit troop standing in for one of the band) with an explicit witness color.
        /// </summary>
        public ChatMessageViewModel(CharacterObject portraitCharacter, string authorName, string messageText, string colorHex)
        {
            _displayText = BuildDisplayText(authorName, messageText);
            ResolvePortrait(portraitCharacter, authorName);
            _speakerKey = portraitCharacter?.StringId ?? authorName ?? "";
            _authorColor = colorHex ?? NpcColor;
            ApplyCustomPortrait(portraitCharacter?.StringId, portraitCharacter?.Name?.ToString());
        }

        /// <summary>
        ///   System notification line (relation change, gold transfer): no portrait,
        ///   no author prefix. The color encodes whether the outcome is favorable.
        /// </summary>
        public ChatMessageViewModel(string systemText, string colorHex)
        {
            _displayText = systemText ?? "";
            _authorColor = colorHex;
            _showAvatar  = false; // no portrait/frame for a system line
        }

        // ── Portrait spike bindings (player custom vs NPC vanilla) ──
        [DataSourceProperty]
        public string PlayerPortraitPath => _playerPortraitPath;

        [DataSourceProperty]
        public bool ShowCustomPortrait => _showCustomPortrait;

        [DataSourceProperty]
        public bool ShowVanillaPortrait => _showAvatar && !_showCustomPortrait;

        [DataSourceProperty]
        public float AvatarWidth => _avatarWidth;

        [DataSourceProperty]
        public float AvatarHeight => _avatarHeight;

        [DataSourceProperty]
        public float TextMarginLeft => _textMarginLeft;

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

        /// <summary>
        ///   Texte complet affiché : "Nom" puis le message à la ligne suivante.
        /// </summary>
        [DataSourceProperty]
        public bool ShowAvatar
        {
            get => _showAvatar;
            set
            {
                if (_showAvatar == value) return;
                _showAvatar = value;
                OnPropertyChangedWithValue(value);
            }
        }

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

        /// <summary>
        ///   The stable identity of this line's speaker, used to keep at most one live portrait per participant.
        /// </summary>
        public string PortraitKey => _speakerKey;

        /// <summary>
        ///   True while this line still carries a rendered character tableau (a GPU render-target). A system
        ///   line, a narration line, or an already-released older duplicate has none.
        /// </summary>
        public bool HasLivePortrait => _showAvatar && !string.IsNullOrEmpty(_portraitId);

        /// <summary>
        ///   Frees this line's character tableau by clearing the ImageId the widget renders from. Merely hiding
        ///   the avatar does NOT release the GPU render-target; only an empty ImageId makes the
        ///   ImageIdentifierWidget drop its texture provider. The text stays, so scrollback survives. The chat
        ///   calls this on the older lines of a speaker once that speaker talks again, bounding VRAM to the
        ///   number of distinct participants present rather than the transcript length.
        /// </summary>
        public void ReleasePortrait()
        {
            // A custom portrait is a cached sprite (no GPU render-target), so there is nothing to free and it
            // must stay visible on every player line, not just the latest, unlike the vanilla tableaus below.
            if (_showCustomPortrait) return;

            if (!_showAvatar && string.IsNullOrEmpty(_portraitId)) return;
            PortraitId = "";
            PortraitTextureProviderName = "";
            PortraitAdditionalArgs = "";
            ShowAvatar = false;
        }

        /// <summary>
        ///   Right-click on this speaker's portrait opens their encyclopedia page (bound via
        ///   Command.AlternateClick on the avatar). A no-op for a heroless speaker (a bandit troop) or a
        ///   system line, which have no page. The chat's focus arbiter yields to the encyclopedia while it
        ///   is open and reclaims focus when it closes, so the chat stays usable underneath.
        /// </summary>
        public void ExecuteOpenEncyclopedia()
        {
            try
            {
                if (string.IsNullOrEmpty(_encyclopediaLink) || Campaign.Current?.EncyclopediaManager == null) return;

                Campaign.Current.EncyclopediaManager.GoToLink(_encyclopediaLink);
                ModLog.Info("[ENCYCLOPEDIA] opened from a chat portrait (right-click).");
                ChatManager.LogScreenState("encyclopedia right-click"); // where did it actually land?
            }
            catch (Exception ex)
            {
                ModLog.Error($"ChatMessageViewModel.ExecuteOpenEncyclopedia failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        #region private

        /// <summary>
        ///   Custom chat portraits: if the player has dropped an image for this speaker in
        ///   ModuleData/ChatPortraits/ (by StringId or name), show it instead of the vanilla 3D render. Any
        ///   speaker (the player or an NPC) with an image gets it; everyone else keeps the small vanilla avatar.
        /// </summary>
        private void ApplyCustomPortrait(string stringId, string displayName)
        {
            if (!_showAvatar) return;

            string path = Portraits.ChatPortraitResolver.ResolveForHero(stringId, displayName);
            if (string.IsNullOrEmpty(path)) return;

            _playerPortraitPath = path;
            _showCustomPortrait = true;

            // The portrait FILLS the avatar column width and grows only in HEIGHT to keep the PNG's aspect
            // (no stretch), capped so a very tall image cannot make a giant row. Width never exceeds the column,
            // so the message text stays at its fixed margin and the whole conversation stays aligned.
            const float colWidth = 96f;
            const float maxHeight = 220f;
            if (Portraits.RuntimePortraitFactory.TryGetDimensions(path, out int w, out int h) && w > 0 && h > 0)
            {
                float scale = Math.Min(colWidth / w, maxHeight / h);
                _avatarWidth = w * scale;
                _avatarHeight = h * scale;
            }
            else
            {
                _avatarWidth = colWidth;
                _avatarHeight = 174f;
            }

            _textMarginLeft = 112f; // fixed: the text never moves, whatever the portrait's height

            // A custom line uses a cached sprite, not a 3D render-target: clear the vanilla ids so it is never
            // counted as a live tableau nor released by the one-per-speaker VRAM management (it would vanish).
            _portraitId = "";
            _portraitTextureProviderName = "";
            _portraitAdditionalArgs = "";
        }

        private void ResolvePortrait(CharacterObject character, string authorName)
        {
            PortraitData portrait        = PortraitTextureGuard.Create(character);
            _portraitId                  = portrait.Id;
            _portraitTextureProviderName = portrait.TextureProviderName;
            _portraitAdditionalArgs      = portrait.AdditionalArgs;
            ModLog.Info(
                $"Portrait for {authorName}: " +
                $"CharacterObjectNull={character == null}, " +
                $"IdEmpty={string.IsNullOrEmpty(_portraitId)}, " +
                $"TextureProvider='{_portraitTextureProviderName}', " +
                $"Args='{_portraitAdditionalArgs}'");
        }

        private string BuildDisplayText(string authorName, string messageText)
        {
            if (string.IsNullOrEmpty(authorName))
                authorName = "Inconnu";

            return $"{authorName}\n{messageText}";
        }

        #endregion
    }
}