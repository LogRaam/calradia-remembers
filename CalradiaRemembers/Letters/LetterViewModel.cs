// Code written by Gabriel Mailhot, 10/07/2026.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GUI;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Portraits;
using CalradiaRemembers.Verbs;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>View model for the dedicated courier window.</summary>
   public sealed class LetterViewModel : ViewModel, IManagedPortrait
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

      // Correspondent portrait (the recipient when composing, the sender when reading/replying). A player-dropped
      // custom chat portrait (ChatPortraitResolver) wins when it exists; otherwise the vanilla 3D render is used.
      private string _customPortraitPath = "";
      private string _portraitTextureProviderName = "";
      private NpcProfile _profile; // sender (read / reply) or recipient (compose)
      private string _receivedBody = "";
      private string _senderHeading = "";
      private bool _showCustomPortrait;
      private bool _showPortrait;
      private LetterComposerState _state = new(LetterWindowMode.Read);

      private string _title = "";

      // ── Correspondence left page (always shown; reuses the encyclopedia archive's thread + row template) ──
      private readonly MBBindingList<LetterArchiveEntryViewModel> _historyEntries = new MBBindingList<LetterArchiveEntryViewModel>();
      private string _historyEmptyText = "";
      private bool _historyIsEmpty;
      private string _historyTitle = "";

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

      /// <summary>The letter-body brush name: CR.Letter.Body, or its plain Cyrillic-safe sibling for a Cyrillic game language.</summary>
      [DataSourceProperty]
      public string BodyBrush => LetterFontResolver.Resolve("CR.Letter.Body", "CR.Letter.Body.Plain");

      /// <summary>The small "From X" heading brush name (also used by the courier-note line), same Galahad/plain choice as <see cref="BodyBrush" />.</summary>
      [DataSourceProperty]
      public string HeadingBrush => LetterFontResolver.Resolve("CR.Letter.Heading", "CR.Letter.Heading.Plain");

      /// <summary>The compose/reply writing-field brush name, same Galahad/plain choice as <see cref="BodyBrush" />.</summary>
      [DataSourceProperty]
      public string InputBrush => LetterFontResolver.Resolve("CR.Letter.Input", "CR.Letter.Input.Plain");

      /// <summary>True while the correspondent portrait still holds a rendered tableau (a GPU render-target).</summary>
      public bool HasLivePortrait => _showPortrait && !string.IsNullOrEmpty(_portraitId);

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

      /// <summary>
      ///   The correspondent's identity. The letter shows a single portrait, so this mainly satisfies the guard's
      ///   contract.
      /// </summary>
      public string PortraitKey => _profile?.Id ?? "";

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
      public bool ShowAcknowledge => _state.ShowsAcknowledge && !IsOfferLetter;

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

      /// <summary>The player-dropped custom portrait file path for this correspondent, or "" when none exists.</summary>
      [DataSourceProperty]
      public string CustomPortraitPath
      {
         get => _customPortraitPath;
         set
         {
            if (_customPortraitPath == value) return;
            _customPortraitPath = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>True when a custom portrait exists: it is shown instead of the vanilla 3D render.</summary>
      [DataSourceProperty]
      public bool ShowCustomPortrait
      {
         get => _showCustomPortrait;
         set
         {
            if (_showCustomPortrait == value) return;
            _showCustomPortrait = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>The vanilla 3D portrait shows only when there IS one and no custom portrait overrides it.</summary>
      [DataSourceProperty]
      public bool ShowVanillaPortrait => _showPortrait && !_showCustomPortrait;

      /// <summary>Whether the portrait frame has anything to show at all (custom or vanilla).</summary>
      [DataSourceProperty]
      public bool HasAnyPortrait => _showCustomPortrait || _showPortrait;

      [DataSourceProperty]
      public bool ShowReceivedBody => _state.ShowsReceivedBody;

      [DataSourceProperty]
      public bool ShowReplyButton => _state.ShowsReplyButton && !IsOfferLetter;

      [DataSourceProperty]
      public bool ShowSend => _state.ShowsSend;

      // ── Offer letters (ServiceOffer / PrisonerOffer): Accept / Decline instead of Acknowledge / Reply ──

      /// <summary>True when the letter being read is an actionable lord's offer, so it shows Accept / Decline.</summary>
      private bool IsOfferLetter
         => _state.Mode == LetterWindowMode.Read && _letter != null
            && (_letter.Reason == LetterReason.ServiceOffer || _letter.Reason == LetterReason.PrisonerOffer);

      /// <summary>
      ///   Whether the offer can actually be accepted right now. A ServiceOffer always can (a light courtesy);
      ///   a PrisonerOffer only while the named captive is still in the player's prison (they may have escaped,
      ///   died, or been sold since the letter was written).
      /// </summary>
      [DataSourceProperty]
      public bool ShowAcceptOffer
      {
         get
         {
            if (!IsOfferLetter) return false;
            if (_letter.Reason == LetterReason.PrisonerOffer) return OfferPrisonerStillHeld();

            return true;
         }
      }

      /// <summary>Decline is always available on an offer letter (a free refusal, the way out when Accept has lapsed).</summary>
      [DataSourceProperty]
      public bool ShowDeclineOffer => IsOfferLetter;

      /// <summary>
      ///   The line explaining why a prisoner offer cannot be taken, shown in place of nothing at all. A player
      ///   who reads a good sum and finds only a Decline button cannot tell a lapsed offer from a broken mod, and
      ///   said so almost in those words (2026-09-08).
      /// </summary>
      [DataSourceProperty]
      public string OfferLapsedNote
         => IsOfferLetter && _letter.Reason == LetterReason.PrisonerOffer
            ? PrisonerOfferAvailabilityPolicy.Describe(PrisonerOfferBlocker(), OfferCaptiveName())
            : "";

      /// <summary>True when there is a reason to show, so the note's row stays out of the way otherwise.</summary>
      [DataSourceProperty]
      public bool ShowOfferLapsedNote => !string.IsNullOrEmpty(OfferLapsedNote);

      [DataSourceProperty]
      public string AcceptOfferButtonText => _letter?.Reason == LetterReason.PrisonerOffer
         ? "Accept the trade"
         : "Accept the offer";

      [DataSourceProperty]
      public string DeclineOfferButtonText => "Decline";

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

      // ── Correspondence left page bindings ───────────────────────────────

      /// <summary>The absolute file path to the leather two-page book art (cr_letters.jpg), painted behind the whole window.</summary>
      [DataSourceProperty]
      public string LetterBookBackgroundPath => System.IO.Path.Combine(
         CalradiaRemembers.Paths.ModulePaths.Root, "GUI", "Images", "cr_letters.jpg");

      /// <summary>The dated correspondence thread with this contact, shown on the left page (whole history, unbounded).</summary>
      [DataSourceProperty]
      public MBBindingList<LetterArchiveEntryViewModel> HistoryEntries => _historyEntries;

      /// <summary>True when the left page has no letters to show (a fresh contact): a quiet empty line stands in for the thread.</summary>
      [DataSourceProperty]
      public bool HistoryIsEmpty
      {
         get => _historyIsEmpty;
         set
         {
            if (_historyIsEmpty == value) return;
            _historyIsEmpty = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>The empty-state line for the left page when no letters have yet passed with this contact.</summary>
      [DataSourceProperty]
      public string HistoryEmptyText
      {
         get => _historyEmptyText;
         set
         {
            if (_historyEmptyText == value) return;
            _historyEmptyText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>The left-page heading ("Letters with X").</summary>
      [DataSourceProperty]
      public string HistoryTitle
      {
         get => _historyTitle;
         set
         {
            if (_historyTitle == value) return;
            _historyTitle = value;
            OnPropertyChangedWithValue(value);
         }
      }

      // ── Commands ────────────────────────────────────────────────────────

      /// <summary>Read mode: dismiss the letter (it was already marked delivered on arrival).</summary>
      public void ExecuteAcknowledge()
      {
         ApplyInterestDelta(CorrespondenceInterestPolicy.OnAcknowledge());
         OnCloseRequested?.Invoke();
      }

      public void ExecuteClose() => OnCloseRequested?.Invoke();

      /// <summary>Offer letter: accept the lord's proposition. A PrisonerOffer settles the ransom; a ServiceOffer warms the bond (light).</summary>
      public void ExecuteAcceptOffer()
      {
         if (!IsOfferLetter) return;

         if (_letter.Reason == LetterReason.PrisonerOffer) AcceptPrisonerOffer();
         else if (_letter.Reason == LetterReason.ServiceOffer) AcceptServiceOffer();
         else OnCloseRequested?.Invoke();
      }

      /// <summary>Offer letter: refuse the proposition. Costs nothing beyond setting the letter aside.</summary>
      public void ExecuteDeclineOffer()
      {
         if (_profile != null)
            MBInformationManager.AddQuickInformation(new TextObject($"You set {_profile.Name}'s offer aside."));

         OnCloseRequested?.Invoke();
      }

      /// <summary>True while the PrisonerOffer's named captive is still in the player's prison (Accept is otherwise hidden).</summary>
      private bool OfferPrisonerStillHeld() => PrisonerOfferBlocker() == PrisonerOfferBlock.None;

      /// <summary>
      ///   Why this prisoner offer cannot be settled, or None when it can. Reads the live campaign and lets the
      ///   pure policy judge, so the window's note and the delivery log give the player and us the SAME reason
      ///   (player report 2026-09-08: a generous offer with only a Decline button and no word as to why).
      /// </summary>
      private PrisonerOfferBlock PrisonerOfferBlocker()
      {
         Hero captive = string.IsNullOrEmpty(_letter?.OfferTargetId)
            ? null
            : Hero.FindFirst(h => h.StringId == _letter.OfferTargetId);

         return PrisonerOfferAvailabilityPolicy.Evaluate(new PrisonerOfferAvailabilityPolicy.PrisonerOfferFacts {HasTargetId = !string.IsNullOrEmpty(_letter?.OfferTargetId), HasAmount = _letter?.OfferAmount > 0, CaptiveResolved = captive != null, StillHeld = captive != null && BuyPrisonerVerb.PlayerHeldHeroPrisoners().Contains(captive)});
      }

      /// <summary>The captive's own name for the note, falling back to nothing rather than to an id.</summary>
      private string OfferCaptiveName()
      {
         Hero captive = string.IsNullOrEmpty(_letter?.OfferTargetId)
            ? null
            : Hero.FindFirst(h => h.StringId == _letter.OfferTargetId);

         return captive?.Name?.ToString();
      }

      /// <summary>Settles the prisoner ransom the letter named, through the shared executor, then closes.</summary>
      private void AcceptPrisonerOffer()
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;
         Hero buyer = _profile != null ? Hero.FindFirst(h => h.StringId == _profile.Id) : null;
         Hero captive = string.IsNullOrEmpty(_letter?.OfferTargetId)
            ? null
            : Hero.FindFirst(h => h.StringId == _letter.OfferTargetId);

         if (buyer == null || captive == null)
         {
            MBInformationManager.AddQuickInformation(new TextObject("(The offer can no longer be honoured.)"));
            OnCloseRequested?.Invoke();

            return;
         }

         NpcProfile buyerProfile = behavior?.Store?.Get(_profile.Id) ?? _profile;
         bool ok = PrisonerRansomExecutor.Execute(buyer, captive, _letter.OfferAmount, buyerProfile, out string message);
         if (ok) behavior?.Store?.Set(buyerProfile);

         MBInformationManager.AddQuickInformation(new TextObject(message));
         OnCloseRequested?.Invoke();
      }

      /// <summary>
      ///   Accepts a service offer: a LIGHT hook. It warms the lord's regard for the player and records that the
      ///   courtship was received, but it does NOT change the player's allegiance or grant the enticement, which
      ///   stays a promise the player realises only by actually entering that lord's service.
      /// </summary>
      private void AcceptServiceOffer()
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile p = behavior?.Store?.Get(_profile?.Id) ?? _profile;

         if (p != null)
         {
            NpcMemoryService.Core.Services.ProfileMutator.ApplyReputationDelta(p, ServiceOfferPolicy.AcceptRelationWarmup, NpcMemoryService.Core.Standing.RegardCause.Letter);
            p.Events?.Add(new NotableEvent((int) CampaignTime.Now.ToDays, NotableEventType.Other,
               "The player received my offer of service warmly, though they have not yet left their lord."));
            behavior?.Store?.Set(p);
         }

         MBInformationManager.AddQuickInformation(new TextObject(
            $"You signal your interest to {_profile?.Name}. To truly enter their service, seek them out and change your allegiance."));
         OnCloseRequested?.Invoke();
      }

      /// <summary>
      ///   Cancel button. In reply mode it drops the draft and returns to READING this same letter (restoring
      ///   Acknowledge / Reply) rather than closing the window, so the player can back out of writing without
      ///   losing the letter and still leave via Acknowledge. In compose mode there is no letter to return to,
      ///   so it closes.
      /// </summary>
      public void ExecuteCancel()
      {
         if (_state.Mode == LetterWindowMode.Reply && _letter != null && _profile != null)
         {
            SetupRead(_letter, _profile);

            return;
         }

         OnCloseRequested?.Invoke();
      }

      /// <summary>Read mode: turn this into a reply — flip the same window to reply mode in place.</summary>
      public void ExecuteReply()
      {
         if (_state.Mode != LetterWindowMode.Read || _letter == null || _profile == null) return;

         Hero npcHero = Hero.FindFirst(h => h.StringId == _profile.Id);
         SetupReply(_letter, _profile, npcHero);
      }

      /// <summary>4.1: an upper bound on a composed/replied letter, so a pathological paste cannot bloat the save blob or the generation prompt. Generous for real prose.</summary>
      private const int MaxLetterLength = 2000;

      public void ExecuteSend()
      {
         string text = (InputText ?? "").Trim();

         if (string.IsNullOrEmpty(text) || !IsSendEnabled) return;

         // 4.1: clamp before it is stored or sent. Rare in practice (real letters are short); guards against a
         // pathological paste that would otherwise be serialised in full on the profile and re-injected each turn.
         if (text.Length > MaxLetterLength) text = text.Substring(0, MaxLetterLength);

         if (_state.Mode == LetterWindowMode.Compose)
         {
            SealAndSendCompose(text);

            return;
         }

         if (_state.Mode != LetterWindowMode.Reply) return;

         SealAndSendReply(text);
      }

      /// <summary>Frees the correspondent portrait's render-target on close by clearing the bound image id.</summary>
      public void ReleasePortrait()
      {
         if (!_showPortrait && string.IsNullOrEmpty(_portraitId)) return;
         PortraitId = "";
         PortraitTextureProviderName = "";
         PortraitAdditionalArgs = "";
         ShowPortrait = false;
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
         // A letter written on a greater lord's behalf says so (Gabriel, 28/09/2026): "A LETTER FROM THE CHANCELLERY OF ...".
         _title = $"A LETTER FROM {(letter?.WrittenBy ?? letter?.SenderName ?? senderProfile?.Name ?? "?").ToUpperInvariant()}";
         _senderHeading = ComposeSenderHeading(letter, senderProfile?.Name);
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
         _senderHeading = ComposeSenderHeading(letter, profile?.Name);
         _panelHeight = ReplyHeight;
         ResolvePortrait(npcHero ?? Hero.FindFirst(h => h.StringId == profile?.Id));
         RefreshFromState();
      }

      #region private

      /// <summary>
      ///   "From X" plus how long ago the letter was written ("From X, sent 3 days ago"), so a note penned
      ///   before current events reads as the dated dispatch it is (player report). No age line when the
      ///   writing day is unknown (older saves).
      /// </summary>
      private static string ComposeSenderHeading(PendingLetter letter, string fallbackName)
      {
         var from = $"From {letter?.WrittenBy ?? letter?.SenderName ?? fallbackName}";
         string age = letter == null
            ? null
            : LetterAgeDescriber.Describe(letter.SentOnDay, (int) CampaignTime.Now.ToDays);

         return string.IsNullOrEmpty(age)
            ? from
            : $"{from}, {age}";
      }

      private void RecomputeSendEnabled()
         => IsSendEnabled = _state.CanSend(_inputText, Hero.MainHero?.Gold ?? 0);

      // ── Refresh ─────────────────────────────────────────────────────────

      /// <summary>Rebuilds every binding from the current mode + data.</summary>
      private void RefreshFromState()
      {
         Title = _title;
         SenderHeading = _senderHeading;
         ReceivedBody = _state.ShowsReceivedBody
            ? Mapping.ShownText.Of(_letter?.Content ?? "(The letter is blank.)")
            : "";

         CourierNote = _state.Mode == LetterWindowMode.Compose && _courierCost > 0
            ? $"A courier will carry this for {_courierCost} denars."
            : "";

         InputText = "";

         RefreshHistoryState();

         OnPropertyChanged(nameof(ShowReceivedBody));
         OnPropertyChanged(nameof(ShowInput));
         OnPropertyChanged(nameof(ShowSend));
         OnPropertyChanged(nameof(ShowAcknowledge));
         OnPropertyChanged(nameof(ShowReplyButton));
         OnPropertyChanged(nameof(ShowAcceptOffer));
         OnPropertyChanged(nameof(ShowOfferLapsedNote));
         OnPropertyChanged(nameof(OfferLapsedNote));
         OnPropertyChanged(nameof(ShowDeclineOffer));
         OnPropertyChanged(nameof(AcceptOfferButtonText));
         OnPropertyChanged(nameof(DeclineOfferButtonText));
         OnPropertyChanged(nameof(HasCourierNote));
         OnPropertyChanged(nameof(BodyBrush));
         OnPropertyChanged(nameof(HeadingBrush));
         OnPropertyChanged(nameof(InputBrush));
         OnPropertyChanged(nameof(CloseButtonText));
         OnPropertyChanged(nameof(PanelHeight));
         OnPropertyChanged(nameof(HistoryIsEmpty));
         OnPropertyChanged(nameof(HistoryEmptyText));
         OnPropertyChanged(nameof(HistoryTitle));
         OnPropertyChanged(nameof(ShowPortrait));
         OnPropertyChanged(nameof(ShowCustomPortrait));
         OnPropertyChanged(nameof(ShowVanillaPortrait));
         OnPropertyChanged(nameof(HasAnyPortrait));
         OnPropertyChanged(nameof(CustomPortraitPath));
         OnPropertyChanged(nameof(PortraitTextureProviderName));
         OnPropertyChanged(nameof(PortraitId));
         OnPropertyChanged(nameof(PortraitAdditionalArgs));
         RecomputeSendEnabled();
      }

      /// <summary>
      ///   Resolves the correspondent's portrait: a player-dropped custom chat portrait
      ///   (<see cref="ChatPortraitResolver" />) wins when one exists, so no vanilla render-target is even created;
      ///   otherwise the vanilla 3D render (same as chat / encyclopedia) is used.
      /// </summary>
      private void ResolvePortrait(Hero hero)
      {
         string custom = hero == null
            ? null
            : ChatPortraitResolver.ResolveForHero(hero.StringId, hero.Name?.ToString());

         if (!string.IsNullOrEmpty(custom))
         {
            _customPortraitPath = custom;
            _showCustomPortrait = true;
            _portraitId = "";
            _portraitTextureProviderName = "";
            _portraitAdditionalArgs = "";
            _showPortrait = false;

            return;
         }

         _customPortraitPath = "";
         _showCustomPortrait = false;

         PortraitData portrait = PortraitTextureGuard.Create(hero?.CharacterObject);
         _portraitId = portrait.Id;
         _portraitTextureProviderName = portrait.TextureProviderName;
         _portraitAdditionalArgs = portrait.AdditionalArgs;
         _showPortrait = portrait.HasImage;
      }

      // ── Correspondence left page ────────────────────────────────────────

      /// <summary>
      ///   Recomputes the left page for the current contact: the heading and the thread itself (the page is
      ///   always shown). A fresh contact with no letters yet shows a quiet empty line instead of a blank page.
      /// </summary>
      private void RefreshHistoryState()
      {
         _historyTitle = string.IsNullOrEmpty(_profile?.Name)
            ? "Correspondence"
            : $"Letters with {_profile.Name}";

         BuildHistory();
      }

      /// <summary>
      ///   Fills the left page with the whole dated thread for this contact, oldest-first and every letter kept
      ///   (the archive is unbounded by design, so cap 0). Reads the freshest persisted profile so a letter
      ///   delivered this very session is included; falls back to the setup profile if the store is absent.
      /// </summary>
      private void BuildHistory()
      {
         _historyEntries.Clear();
         if (_profile == null)
         {
            _historyIsEmpty = true;
            _historyEmptyText = "No letters yet.";

            return;
         }

         NpcProfile source = NpcMemoryCampaignBehavior.Instance?.Store.Get(_profile.Id) ?? _profile;
         string name = source.Name ?? _profile.Name ?? "?";

         foreach (LetterThreadEntry entry in
            LetterThreadAssembler.Build(name, source.SentLetters, source.ReceivedPlayerLetters, cap: 0))
            _historyEntries.Add(new LetterArchiveEntryViewModel(entry.Header, entry.Body, entry.IsIncoming));

         _historyIsEmpty = _historyEntries.Count == 0;
         _historyEmptyText = _historyIsEmpty
            ? $"No letters have yet passed between you and {name}."
            : "";
      }

      // ── B1: correspondence interest (persisted; plumbing only, see B2 for the mailbox reading it) ────────

      /// <summary>
      ///   Applies a correspondence-interest delta (<see cref="CorrespondenceInterestPolicy.OnAcknowledge" /> or
      ///   <see cref="CorrespondenceInterestPolicy.OnReply" />) to the sender's persisted profile and to this
      ///   letter's subject, both run on the main/UI thread so a direct <c>Store.Set</c> is safe here. Skipped
      ///   for a <see cref="LetterMailboxPolicy.BypassesBudget" /> reason: a divorce demand, a pension due date,
      ///   a companion's farewell, or the player's own reply carries no signal about the player's taste in
      ///   unprompted correspondence.
      /// </summary>
      private void ApplyInterestDelta(InterestDeltas deltas)
      {
         if (_letter == null || _profile == null) return;
         if (LetterMailboxPolicy.BypassesBudget(_letter.Reason)) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile p = behavior?.Store.Get(_profile.Id);
         if (p == null) return;

         p.CorrespondenceInterest = CorrespondenceInterestPolicy.ClampSender(p.CorrespondenceInterest + deltas.SenderDelta);
         behavior.Store.Set(p);
         behavior.LetterEvaluator.NudgeSubject(_letter.Reason, deltas.SubjectDelta);
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
         if (_courierCost > 0 && Hero.MainHero.Gold < _courierCost)
         {
            MBInformationManager.AddQuickInformation(new TextObject(
               $"You cannot afford the courier ({_courierCost} denars)."));

            return;
         }

         int today = behavior.GameStateBridge.GetCurrentGameDay();

         // 1.7 (courier audit): queue FIRST, then charge only if the letter is actually on its way. A recipient who
         // is dead, captured, or absent cannot receive it, so the player is told honestly and no coin is taken.
         (bool deliverable, int deliveryDays) = PlayerLetterService.CreateAndQueue(_profile, content, today, behavior.Store);

         if (!deliverable)
         {
            MBInformationManager.AddQuickInformation(new TextObject(
               $"{_profile.Name} cannot receive a letter right now; your courier stands down and no fee is charged."));
            OnCloseRequested?.Invoke();

            return;
         }

         if (_courierCost > 0)
         {
            Hero.MainHero.ChangeHeroGold(-_courierCost);
            ModLog.Info($"[PLAYER LETTER] courier fee charged: {_courierCost} denars.");
         }

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

         ApplyInterestDelta(CorrespondenceInterestPolicy.OnReply());

         MBInformationManager.AddQuickInformation(new TextObject(
            $"Your reply to {_profile.Name} has been sealed and sent."));
         StoryLog.WriteLetterSent(_profile.Name, content, today);
         ModLog.Info($"[LETTER] reply to {_profile.Name} sealed; NPC response queued as a new letter.");

         OnCloseRequested?.Invoke();
      }

      #endregion
   }
}