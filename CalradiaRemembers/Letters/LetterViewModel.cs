// Code written by Gabriel Mailhot, 21/06/2026.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>View model for the dedicated courier window.</summary>
   public sealed class LetterViewModel : ViewModel
   {
      private const float ComposeHeight = 440f;
      private const float ReadHeight = 680f;
      private const float ReplyHeight = 700f;

      public Action OnCloseRequested;
      private int _courierCost; // compose-only fee
      private string _courierNote = "";
      private string _inputText = "";
      private bool _isSendEnabled;

      private PendingLetter _letter; // the received letter (read / reply)
      private Hero _npcHero; // resolved NPC for a reply

      // The window resizes to its mode: compose has no letter to read, so it stays compact.
      // Float because the widget's SuggestedHeight setter is float — binding an int crashes the bind.
      private float _panelHeight = ReadHeight;
      private string _portraitAdditionalArgs = "";
      private string _portraitId = "";

      // Correspondent portrait (the recipient when composing, the sender when reading/replying).
      private string _portraitTextureProviderName = "";
      private NpcProfile _profile; // sender (read / reply) or recipient (compose)
      private string _receivedBody = "";
      private string _senderHeading = "";
      private bool _showPortrait;
      private LetterComposerState _state = new(LetterWindowMode.Read);

      private string _title = "";

      [DataSourceProperty]
      public string AcknowledgeButtonText => "Acknowledge";

      [DataSourceProperty]
      public string CloseButtonText => _state.Mode == LetterWindowMode.Read
         ? "Close"
         : "Cancel";

      [DataSourceProperty]
      public string CourierNote
      {
         get => _courierNote;
         set
         {
            if (_courierNote == value) return;
            _courierNote = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public bool HasCourierNote => !string.IsNullOrEmpty(_courierNote);

      [DataSourceProperty]
      public string InputText
      {
         get => _inputText;
         set
         {
            if (_inputText == value) return;
            _inputText = value;
            OnPropertyChangedWithValue(value);
            RecomputeSendEnabled();
         }
      }

      [DataSourceProperty]
      public bool IsSendEnabled
      {
         get => _isSendEnabled;
         set
         {
            if (_isSendEnabled == value) return;
            _isSendEnabled = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public float PanelHeight
      {
         get => _panelHeight;
         set
         {
            if (_panelHeight == value) return;
            _panelHeight = value;
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

      [DataSourceProperty]
      public string ReceivedBody
      {
         get => _receivedBody;
         set
         {
            if (_receivedBody == value) return;
            _receivedBody = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public string ReplyButtonText => "Reply by letter";

      [DataSourceProperty]
      public string SendButtonText => "Seal & Send";

      [DataSourceProperty]
      public string SenderHeading
      {
         get => _senderHeading;
         set
         {
            if (_senderHeading == value) return;
            _senderHeading = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public bool ShowAcknowledge => _state.ShowsAcknowledge;

      [DataSourceProperty]
      public bool ShowInput => _state.ShowsInput;

      [DataSourceProperty]
      public bool ShowPortrait
      {
         get => _showPortrait;
         set
         {
            if (_showPortrait == value) return;
            _showPortrait = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public bool ShowReceivedBody => _state.ShowsReceivedBody;

      [DataSourceProperty]
      public bool ShowReplyButton => _state.ShowsReplyButton;

      [DataSourceProperty]
      public bool ShowSend => _state.ShowsSend;

      // ── Bindings ────────────────────────────────────────────────────────

      [DataSourceProperty]
      public string Title
      {
         get => _title;
         set
         {
            if (_title == value) return;
            _title = value;
            OnPropertyChangedWithValue(value);
         }
      }

      // ── Commands ────────────────────────────────────────────────────────

      /// <summary>Read mode: dismiss the letter (it was already marked delivered on arrival).</summary>
      public void ExecuteAcknowledge() => OnCloseRequested?.Invoke();

      public void ExecuteClose() => OnCloseRequested?.Invoke();

      /// <summary>Read mode: turn this into a reply — flip the same window to reply mode in place.</summary>
      public void ExecuteReply()
      {
         if (_state.Mode != LetterWindowMode.Read || _letter == null || _profile == null) return;

         Hero npcHero = Hero.FindFirst(h => h.StringId == _profile.Id);
         SetupReply(_letter, _profile, npcHero);
      }

      public void ExecuteSend()
      {
         string text = (InputText ?? "").Trim();

         if (string.IsNullOrEmpty(text) || !IsSendEnabled) return;

         if (_state.Mode == LetterWindowMode.Compose)
         {
            SealAndSendCompose(text);

            return;
         }

         if (_state.Mode != LetterWindowMode.Reply) return;

         SealAndSendReply(text);
      }

      /// <summary>Write a fresh letter to a chosen recipient (a courier fee may apply).</summary>
      public void SetupCompose(NpcProfile recipientProfile, int courierCost)
      {
         _profile = recipientProfile;
         _courierCost = courierCost;
         _state = new LetterComposerState(LetterWindowMode.Compose, courierCost);
         _title = $"TO {(recipientProfile?.Name ?? "?").ToUpperInvariant()}";
         _senderHeading = $"A letter to {recipientProfile?.Name}";
         _panelHeight = ComposeHeight;
         ResolvePortrait(Hero.FindFirst(h => h.StringId == recipientProfile?.Id));
         RefreshFromState();
      }

      // ── Setup (one per open, chosen by the manager) ─────────────────────

      /// <summary>Read a freshly delivered letter — body shown, Acknowledge / Reply offered.</summary>
      public void SetupRead(PendingLetter letter, NpcProfile senderProfile)
      {
         _letter = letter;
         _profile = senderProfile;
         _state = new LetterComposerState(LetterWindowMode.Read);
         _title = $"A LETTER FROM {(letter?.SenderName ?? senderProfile?.Name ?? "?").ToUpperInvariant()}";
         _senderHeading = $"From {letter?.SenderName ?? senderProfile?.Name}";
         _panelHeight = ReadHeight;
         ResolvePortrait(Hero.FindFirst(h => h.StringId == senderProfile?.Id));
         RefreshFromState();
      }

      /// <summary>Reply to a received letter — its body sits above the player's answer.</summary>
      public void SetupReply(PendingLetter letter, NpcProfile profile, Hero npcHero)
      {
         _letter = letter;
         _profile = profile;
         _npcHero = npcHero;
         _state = new LetterComposerState(LetterWindowMode.Reply);
         _title = $"REPLY TO {(profile?.Name ?? letter?.SenderName ?? "?").ToUpperInvariant()}";
         _senderHeading = $"From {letter?.SenderName ?? profile?.Name}";
         _panelHeight = ReplyHeight;
         ResolvePortrait(npcHero ?? Hero.FindFirst(h => h.StringId == profile?.Id));
         RefreshFromState();
      }

      #region private

      private void RecomputeSendEnabled()
         => IsSendEnabled = _state.CanSend(_inputText, Hero.MainHero?.Gold ?? 0);

      // ── Refresh ─────────────────────────────────────────────────────────

      /// <summary>Rebuilds every binding from the current mode + data.</summary>
      private void RefreshFromState()
      {
         Title = _title;
         SenderHeading = _senderHeading;
         ReceivedBody = _state.ShowsReceivedBody
            ? _letter?.Content ?? "(The letter is blank.)"
            : "";

         CourierNote = _state.Mode == LetterWindowMode.Compose && _courierCost > 0
            ? $"A courier will carry this for {_courierCost} denars."
            : "";

         InputText = "";

         OnPropertyChanged(nameof(ShowReceivedBody));
         OnPropertyChanged(nameof(ShowInput));
         OnPropertyChanged(nameof(ShowSend));
         OnPropertyChanged(nameof(ShowAcknowledge));
         OnPropertyChanged(nameof(ShowReplyButton));
         OnPropertyChanged(nameof(HasCourierNote));
         OnPropertyChanged(nameof(CloseButtonText));
         OnPropertyChanged(nameof(PanelHeight));
         OnPropertyChanged(nameof(ShowPortrait));
         OnPropertyChanged(nameof(PortraitTextureProviderName));
         OnPropertyChanged(nameof(PortraitId));
         OnPropertyChanged(nameof(PortraitAdditionalArgs));
         RecomputeSendEnabled();
      }

      /// <summary>Resolves the correspondent's in-game portrait (same render as chat / encyclopedia).</summary>
      private void ResolvePortrait(Hero hero)
      {
         CharacterObject character = hero?.CharacterObject;
         if (character != null)
         {
            var portrait =
               new CharacterImageIdentifierVM(CharacterCode.CreateFrom(character));
            _portraitId = portrait.Id ?? "";
            _portraitTextureProviderName = portrait.TextureProviderName ?? "";
            _portraitAdditionalArgs = portrait.AdditionalArgs ?? "";
            _showPortrait = !string.IsNullOrEmpty(_portraitId);
         }
         else
         {
            _portraitId = _portraitTextureProviderName = _portraitAdditionalArgs = "";
            _showPortrait = false;
         }
      }

      // ── Sealing (no inline LLM call) ────────────────────────────────────

      private void SealAndSendCompose(string content)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior == null || _profile == null)
         {
            OnCloseRequested?.Invoke();

            return;
         }

         // Refuse if the player cannot pay the courier — keep the window open so the words aren't lost.
         if (_courierCost > 0)
         {
            if (Hero.MainHero.Gold < _courierCost)
            {
               MBInformationManager.AddQuickInformation(new TextObject(
                  $"You cannot afford the courier ({_courierCost} denars)."));

               return;
            }

            Hero.MainHero.ChangeHeroGold(-_courierCost);
            ModLog.Info($"[PLAYER LETTER] courier fee charged: {_courierCost} denars.");
         }

         int today = behavior.GameStateBridge.GetCurrentGameDay();
         int deliveryDays = PlayerLetterService.CreateAndQueue(_profile, content, today, behavior.Store);

         string days = deliveryDays == 1
            ? "1 day"
            : $"{deliveryDays} days";
         MBInformationManager.AddQuickInformation(new TextObject(
            $"Your courier departs. The letter will reach {_profile.Name} in approximately {days}."));
         StoryLog.WriteLetterSent(_profile.Name, content, today);
         ModLog.Info($"[PLAYER LETTER] sealed to {_profile.Name} ({deliveryDays}d delivery).");

         OnCloseRequested?.Invoke();
      }

      private void SealAndSendReply(string content)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior == null || _letter == null || _profile == null)
         {
            OnCloseRequested?.Invoke();

            return;
         }

         _letter.PlayerReply = content;
         Hero giverHero = _npcHero ?? Hero.FindFirst(h => h.StringId == _profile.Id);
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         LetterGenerationService.QueueReplyLetter(
            _profile, giverHero, _letter, behavior.LlmServices, behavior.Store, today);

         MBInformationManager.AddQuickInformation(new TextObject(
            $"Your reply to {_profile.Name} has been sealed and sent."));
         StoryLog.WriteLetterSent(_profile.Name, content, today);
         ModLog.Info($"[LETTER] reply to {_profile.Name} sealed; NPC response queued as a new letter.");

         OnCloseRequested?.Invoke();
      }

      #endregion
   }
}