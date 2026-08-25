// Code written by Gabriel Mailhot, 07/08/2026.
// The letter archive viewer: a player's whole correspondence with one contact, shown as a dated,
// scrollable thread (oldest-first). Mirrors ChronicleViewModel's shape; the thread itself is assembled
// by the pure, tested CalradiaRemembers.Logic.LetterThreadAssembler so this view only binds what that
// produces and never re-implements the ordering, dating, or cap.

#region

using System;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>View model for the per-contact letter archive (thread) window.</summary>
   public sealed class LetterArchiveViewModel : ViewModel
   {
      public Action OnCloseRequested;
      private readonly NpcProfile _profile;
      private MBBindingList<LetterArchiveEntryViewModel> _entries;
      private string _emptyText = "";
      private bool _isEmpty;
      private string _title = "";

      public LetterArchiveViewModel(NpcProfile profile)
      {
         _profile = profile;
         _entries = new MBBindingList<LetterArchiveEntryViewModel>();
         RefreshFromState();
      }

      [DataSourceProperty]
      public string CloseButtonText => "Close";

      /// <summary>The heading brush name (window title and the empty-state line), swapped to its plain Cyrillic-safe sibling for a Cyrillic game language, same rule as the compose/read letter view.</summary>
      [DataSourceProperty]
      public string HeadingBrush => LetterFontResolver.Resolve("CR.Letter.Heading", "CR.Letter.Heading.Plain");

      [DataSourceProperty]
      public MBBindingList<LetterArchiveEntryViewModel> Entries
      {
         get => _entries;
         set
         {
            if (_entries == value) return;
            _entries = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public string EmptyText
      {
         get => _emptyText;
         set
         {
            if (_emptyText == value) return;
            _emptyText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public bool IsEmpty
      {
         get => _isEmpty;
         set
         {
            if (_isEmpty == value) return;
            _isEmpty = value;
            OnPropertyChangedWithValue(value);
         }
      }

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

      public void ExecuteClose() => OnCloseRequested?.Invoke();

      /// <summary>Rebuilds the window from the contact's persisted correspondence.</summary>
      public void RefreshFromState()
      {
         string name = _profile?.Name ?? "?";
         Title = $"LETTERS WITH {name.ToUpperInvariant()}";

         Entries.Clear();

         // cap 0 = unbounded: the correspondence history is kept whole by design, so the whole thread is shown.
         System.Collections.Generic.IReadOnlyList<LetterThreadEntry> thread =
            LetterThreadAssembler.Build(name, _profile?.SentLetters, _profile?.ReceivedPlayerLetters, cap: 0);

         if (thread == null || thread.Count == 0)
         {
            IsEmpty = true;
            EmptyText = $"No letters have yet passed between you and {name}.";

            return;
         }

         IsEmpty = false;
         foreach (LetterThreadEntry entry in thread)
            Entries.Add(new LetterArchiveEntryViewModel(entry.Header, entry.Body, entry.IsIncoming));
      }
   }

   /// <summary>One letter in the thread: its dated attribution header, its body, and which side wrote it.</summary>
   public sealed class LetterArchiveEntryViewModel : ViewModel
   {
      private string _body;
      private string _header;
      private bool _isIncoming;

      public LetterArchiveEntryViewModel(string header, string body, bool isIncoming)
      {
         _header = header;
         _body = body;
         _isIncoming = isIncoming;
      }

      [DataSourceProperty]
      public string Body
      {
         get => _body;
         set
         {
            if (_body == value) return;
            _body = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public string Header
      {
         get => _header;
         set
         {
            if (_header == value) return;
            _header = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>A letter the NPC sent to the player (drives the incoming-vs-outgoing styling in the prefab).</summary>
      [DataSourceProperty]
      public bool IsIncoming
      {
         get => _isIncoming;
         set
         {
            if (_isIncoming == value) return;
            _isIncoming = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>A letter (or reply) the player sent to the NPC.</summary>
      [DataSourceProperty]
      public bool IsOutgoing => !_isIncoming;

      /// <summary>The body brush name for this thread entry, swapped to its plain Cyrillic-safe sibling for a Cyrillic game language so archived Russian letters stay legible (Galahad has no proper Cyrillic glyphs).</summary>
      [DataSourceProperty]
      public string BodyBrush => LetterFontResolver.Resolve("CR.Letter.Body", "CR.Letter.Body.Plain");
   }
}
