// Code written by Gabriel Mailhot, 30/10/2026.
// The letter box window's view model (Gabriel, 30/10/2026): one row per waiting letter, by order of arrival, with the
// urgent mark, the ribbon, an Open and a Throw away; one row per silenced subject, with Accept again. Throwing an urgent
// letter asks twice and says why: what it announces is unchanged.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Letters;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>View model for the letter box window.</summary>
   public sealed class LetterBoxViewModel : ViewModel
   {
      public Action OnCloseRequested;

      private MBBindingList<LetterBoxRowViewModel> _letters = new();
      private MBBindingList<SilencedSubjectRowViewModel> _silenced = new();
      private bool _isEmpty;
      private bool _hasSilenced;
      private string _title = "";

      public LetterBoxViewModel() => Refresh();

      [DataSourceProperty] public string CloseButtonText => "Close";

      [DataSourceProperty] public string EmptyText => "No letter waits. When a courier brings one, you will hear it, and it will wait here.";

      [DataSourceProperty] public string SilencedTitle => "Subjects you asked not to hear of again";

      /// <summary>The heading brush, swapped to its Cyrillic-safe sibling as in the letter window.</summary>
      [DataSourceProperty] public string HeadingBrush => LetterFontResolver.Resolve("CR.Letter.Heading", "CR.Letter.Heading.Plain");

      /// <summary>The body brush, the same rule.</summary>
      [DataSourceProperty] public string BodyBrush => LetterFontResolver.Resolve("CR.Letter.Body", "CR.Letter.Body.Plain");

      [DataSourceProperty]
      public MBBindingList<LetterBoxRowViewModel> Letters
      {
         get => _letters;
         set { if (_letters == value) return; _letters = value; OnPropertyChangedWithValue(value); }
      }

      [DataSourceProperty]
      public MBBindingList<SilencedSubjectRowViewModel> Silenced
      {
         get => _silenced;
         set { if (_silenced == value) return; _silenced = value; OnPropertyChangedWithValue(value); }
      }

      [DataSourceProperty]
      public bool IsEmpty
      {
         get => _isEmpty;
         set { if (_isEmpty == value) return; _isEmpty = value; OnPropertyChangedWithValue(value); }
      }

      [DataSourceProperty]
      public bool HasSilenced
      {
         get => _hasSilenced;
         set { if (_hasSilenced == value) return; _hasSilenced = value; OnPropertyChangedWithValue(value); }
      }

      [DataSourceProperty]
      public string Title
      {
         get => _title;
         set { if (_title == value) return; _title = value; OnPropertyChangedWithValue(value); }
      }

      /// <summary>Re-reads what waits and what is silenced (after an arrival, an open, a throw, an undo).</summary>
      public void Refresh()
      {
         int today = (int)CampaignTime.Now.ToDays;
         var letters = new MBBindingList<LetterBoxRowViewModel>();
         foreach (BoxedLetter b in LetterBox.Waiting()) letters.Add(new LetterBoxRowViewModel(b, today, this));

         var silenced = new MBBindingList<SilencedSubjectRowViewModel>();
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store != null)
            foreach (string id in store.ListIds())
            {
               NpcProfile p = store.Get(id);
               if (p?.SilencedLetterSubjects == null) continue;
               foreach (string subject in new List<string>(p.SilencedLetterSubjects)) silenced.Add(new SilencedSubjectRowViewModel(p, subject, this));
            }

         Letters = letters;
         Silenced = silenced;
         IsEmpty = letters.Count == 0;
         HasSilenced = silenced.Count > 0;
         Title = letters.Count == 0 ? "Letter box" : letters.Count == 1 ? "Letter box: one letter waits" : $"Letter box: {letters.Count} letters wait";
      }

      public void ExecuteClose() => OnCloseRequested?.Invoke();
   }

   /// <summary>One waiting letter in the box.</summary>
   public sealed class LetterBoxRowViewModel : ViewModel
   {
      private readonly BoxedLetter _boxed;
      private readonly LetterBoxViewModel _owner;
      private bool _confirmingThrow;

      public LetterBoxRowViewModel(BoxedLetter boxed, int today, LetterBoxViewModel owner)
      {
         _boxed = boxed;
         _owner = owner;
         int days = Math.Max(0, today - boxed.Letter.EffectiveDeliveredDay);
         string when = days == 0 ? "arrived today" : days == 1 ? "arrived yesterday" : $"arrived {days} days ago";
         Line = $"{LetterBoxPolicy.SubjectLabel(LetterBoxPolicy.SubjectOf(boxed.Letter.Reason) ?? "a reply")}, {when}";
      }

      /// <summary>The heading and body brushes, as on the box itself (an item template binds its own row).</summary>
      [DataSourceProperty] public string HeadingBrush => LetterFontResolver.Resolve("CR.Letter.Heading", "CR.Letter.Heading.Plain");

      [DataSourceProperty] public string BodyBrush => LetterFontResolver.Resolve("CR.Letter.Body", "CR.Letter.Body.Plain");

      [DataSourceProperty] public string SenderName => _boxed.Sender?.Name ?? _boxed.Letter.SenderName ?? "";

      [DataSourceProperty] public string Line { get; }

      [DataSourceProperty] public bool IsUrgent => _boxed.Urgent;

      [DataSourceProperty] public bool IsAwaited => _boxed.Awaited;

      [DataSourceProperty] public string UrgentText => "URGENT";

      [DataSourceProperty] public string RibbonImagePath => System.IO.Path.Combine(CalradiaRemembers.Paths.ModulePaths.Root, "GUI", "Images", "cr_ribbon.png");

      [DataSourceProperty] public string OpenText => "Open";

      [DataSourceProperty] public string ThrowText => _confirmingThrow ? "Throw anyway" : "Throw away";

      /// <summary>Shown on the first click on an urgent letter's Throw away (Gabriel: urgent letters may be thrown, with a warning).</summary>
      [DataSourceProperty] public bool ShowThrowWarning => _confirmingThrow;

      [DataSourceProperty] public string ThrowWarning => "Throwing this letter away changes nothing of what it announces.";

      public void ExecuteOpen() => LetterBoxWindow.Read(_boxed);

      public void ExecuteThrow()
      {
         if (_boxed.Urgent && !_confirmingThrow)
         {
            _confirmingThrow = true;
            OnPropertyChanged(nameof(ThrowText));
            OnPropertyChanged(nameof(ShowThrowWarning));

            return;
         }

         LetterBox.Throw(_boxed); // the box refreshes on LetterBox.Changed
      }
   }

   /// <summary>One subject the player silenced from one correspondent.</summary>
   public sealed class SilencedSubjectRowViewModel : ViewModel
   {
      private readonly NpcProfile _sender;
      private readonly string _subject;
      private readonly LetterBoxViewModel _owner;

      public SilencedSubjectRowViewModel(NpcProfile sender, string subject, LetterBoxViewModel owner)
      {
         _sender = sender;
         _subject = subject;
         _owner = owner;
      }

      /// <summary>The heading and body brushes, as on the box itself (an item template binds its own row).</summary>
      [DataSourceProperty] public string HeadingBrush => LetterFontResolver.Resolve("CR.Letter.Heading", "CR.Letter.Heading.Plain");

      [DataSourceProperty] public string BodyBrush => LetterFontResolver.Resolve("CR.Letter.Body", "CR.Letter.Body.Plain");

      [DataSourceProperty] public string Line => $"{_sender?.Name}: {LetterBoxPolicy.SubjectLabel(_subject)}";

      [DataSourceProperty] public string UndoText => "Accept again";

      public void ExecuteUndo()
      {
         LetterBox.Unsilence(_sender, _subject);
         _owner?.Refresh();
      }
   }
}
