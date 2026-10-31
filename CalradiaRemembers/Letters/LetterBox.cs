// Code written by Gabriel Mailhot, 30/10/2026.
// The letter box (Gabriel, 30/10/2026): "toutes les lettres sont envoyees et vont dans la boite aux lettres [...] Une lettre
// lue est enlevee de la boite aux lettres [...] c'est le joueur qui classe, elimine, abonne et desabonne les conversations
// par lettres." A letter that arrives no longer opens a window: the sender's side of the arrival happens as before
// (MarkReceived: the sender's memory, a child's pension quest), the letter waits InBox, the courier's sound plays, and the
// map bar's letter icon counts it. Opening it reads it in the usual window; throwing it away silences its subject from that
// sender (LetterBoxPolicy). The rules are the pure LetterBoxPolicy; this host holds the store, the window and the sound.

#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Letters;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>One letter waiting in the box, with its sender's profile (the same instances the store holds).</summary>
   public sealed class BoxedLetter
   {
      /// <summary>The letter.</summary>
      public PendingLetter Letter { get; set; }

      /// <summary>Its sender's profile.</summary>
      public NpcProfile Sender { get; set; }

      /// <summary>It wears the urgent mark.</summary>
      public bool Urgent => LetterBoxPolicy.IsUrgent(Letter.Reason);

      /// <summary>The player put the ribbon on this correspondent.</summary>
      public bool Awaited => Sender?.LettersAwaited == true;
   }

   /// <summary>The player's letter box: what waits in it, a letter arriving, opened, or thrown away.</summary>
   public static class LetterBox
   {
      /// <summary>The courier's sound, as named in ModuleData/module_sounds.xml (Gabriel chose a letterbox's clap, 30/10/2026).</summary>
      public const string ArrivalSoundName = "cr_letter_arrival";

      /// <summary>The same sound's file, for the engine's other road if the module sound does not play.</summary>
      private const string ArrivalSoundFile = "ModuleSounds/cr_letter_arrival.ogg";

      /// <summary>Several letters arriving on one tick make one sound, not a volley (milliseconds).</summary>
      private const int SoundSpacingMs = 1500;

      private static int _lastSoundTick = int.MinValue;

      /// <summary>Raised when what waits in the box changes (a letter arrived, opened, thrown): the box and the map bar refresh.</summary>
      public static event Action Changed;

      private static BannerlordNpcMemoryStore Store => NpcMemoryCampaignBehavior.Instance?.Store;

      /// <summary>The letters waiting, in the box's order (oldest arrival first).</summary>
      public static IReadOnlyList<BoxedLetter> Waiting()
      {
         BannerlordNpcMemoryStore store = Store;
         if (store == null) return new List<BoxedLetter>();

         var byEntry = new Dictionary<LetterBoxEntry, BoxedLetter>();
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);
            if (profile?.SentLetters == null) continue;

            for (var i = 0; i < profile.SentLetters.Count; i++)
            {
               PendingLetter l = profile.SentLetters[i];
               if (l == null || !l.InBox) continue;

               byEntry[new LetterBoxEntry {SenderId = profile.Id, Reason = l.Reason, ArrivedOnDay = l.EffectiveDeliveredDay, Sequence = i, Awaited = profile.LettersAwaited}]
                  = new BoxedLetter {Letter = l, Sender = profile};
            }
         }

         return LetterBoxPolicy.Ordered(byEntry.Keys).Select(e => byEntry[e]).ToList();
      }

      /// <summary>How many letters wait unopened.</summary>
      public static int WaitingCount() => Waiting().Count;

      /// <summary>True when an urgent letter waits.</summary>
      public static bool UrgentWaits() => Waiting().Any(b => b.Urgent);

      // The map bar asks every frame; the box is read again only after it changed.
      private static bool _dirty = true;
      private static int _cachedCount;
      private static bool _cachedUrgent;

      /// <summary>The count, read again only after the box changed (the map bar's badge asks every frame).</summary>
      public static int WaitingCountCached()
      {
         Recount();

         return _cachedCount;
      }

      /// <summary>Whether an urgent letter waits, with the same cache.</summary>
      public static bool UrgentWaitsCached()
      {
         Recount();

         return _cachedUrgent;
      }

      /// <summary>Marks the box as changed without telling the listeners (a test that removed its own letters).</summary>
      public static void Invalidate() => _dirty = true;

      private static void Recount()
      {
         if (!_dirty) return;

         IReadOnlyList<BoxedLetter> waiting = Waiting();
         _cachedCount = waiting.Count;
         _cachedUrgent = waiting.Any(b => b.Urgent);
         _dirty = false;
      }

      /// <summary>True when the player asked not to receive this kind of letter from this sender.</summary>
      public static bool IsSilenced(NpcProfile sender, LetterReason reason) => LetterBoxPolicy.IsSilenced(sender?.SilencedLetterSubjects, reason);

      /// <summary>
      ///   A letter that has just reached the player (the sender's side already marked by the caller): it waits in the box,
      ///   the courier's sound plays, and a short notice names its sender.
      /// </summary>
      internal static void Receive(PendingLetter letter, NpcProfile sender)
      {
         if (letter == null || sender == null) return;

         letter.InBox = true;
         Store?.Set(sender);

         string urgent = LetterBoxPolicy.IsUrgent(letter.Reason) ? " It looks urgent." : "";
         MBInformationManager.AddQuickInformation(new TextObject($"A letter from {sender.Name} has arrived.{urgent}"));
         PlayArrivalSound();
         ModLog.Info($"[LETTERBOX] {sender.Name}'s letter ({letter.Reason}) waits in the box.");
         RaiseChanged();
      }

      /// <summary>
      ///   A letter that has just reached the player about a subject they silenced from this sender: it is set aside unseen,
      ///   as if thrown on arrival. The sender's side of the arrival has happened (it silences the letter, not the person).
      /// </summary>
      internal static void SetAsideSilenced(PendingLetter letter, NpcProfile sender)
      {
         if (letter == null || sender == null) return;

         letter.InBox = false;
         letter.ThrownAway = true;
         Store?.Set(sender);
         ModLog.Info($"[LETTERBOX] {sender.Name}'s letter ({letter.Reason}) set aside unseen: the player silenced '{LetterBoxPolicy.SubjectOf(letter.Reason)}' from them.");
      }

      // The envelope being written now (one at a time), and the texts that came back, applied on the game thread.
      private static BoxedLetter _writing;
      private static readonly ConcurrentQueue<KeyValuePair<BoxedLetter, string>> Written = new();

      // A text written for a campaign the player has since left must never land in the next one (31/10/2026).
      private static int _session;
      private static int _writingSession;

      /// <summary>True while an envelope's text is being written for the player who opened it.</summary>
      public static bool IsWriting => _writing != null;

      /// <summary>
      ///   Opens a waiting letter in the reading window; it leaves the box. An envelope is written first (the letter box,
      ///   step 3, Gabriel 31/10/2026): the seal breaks now and the letter opens once its text comes back (Tick). False when
      ///   the window is busy, another envelope is being written, or the letter no longer waits.
      /// </summary>
      public static bool Open(BoxedLetter boxed)
      {
         if (boxed?.Letter == null || boxed.Sender == null || !boxed.Letter.InBox) return false;
         if (LetterWindowManager.IsOpen || _writing != null) return false;

         if (LetterEnvelopePolicy.IsUnwritten(boxed.Letter))
         {
            _writing = boxed;
            _writingSession = _session;
            MBInformationManager.AddQuickInformation(new TextObject($"You break the seal of {boxed.Sender.Name}'s letter."));
            ModLog.Info($"[LETTERBOX] writing {boxed.Sender.Name}'s envelope ({boxed.Letter.Reason}) as of day {boxed.Letter.SentOnDay}.");
            LetterGenerationService.WriteEnvelopeAsync(boxed.Letter, boxed.Sender)
               .ContinueWith(t => Written.Enqueue(new KeyValuePair<BoxedLetter, string>(boxed, t.Status == TaskStatus.RanToCompletion ? t.Result : null)));

            return true;
         }

         Show(boxed);

         return true;
      }

      /// <summary>
      ///   Called from the application tick: an envelope's text that came back is put in the letter, its sender's memory gets
      ///   its substance, and the letter opens. When the model did not answer, the letter stays in the box to try again.
      /// </summary>
      public static void Tick()
      {
         if (!Written.TryPeek(out KeyValuePair<BoxedLetter, string> done)) return;
         if (_writing == null || _writingSession != _session)
         {
            Written.TryDequeue(out _); // written for a campaign since left

            return;
         }

         // Only on the campaign map: the application tick also runs inside a conversation or a battle (a mission), where a
         // pushed letter window has no place. Another panel first, too; the letter waits.
         if (!OnTheMap() || LetterWindowManager.IsOpen || UI.CrPanelHost.AnyPanelOwnsScreen) return;

         Written.TryDequeue(out done);
         _writing = null;
         BoxedLetter boxed = done.Key;
         string text = done.Value;

         if (string.IsNullOrWhiteSpace(text))
         {
            MBInformationManager.AddQuickInformation(new TextObject($"{boxed.Sender?.Name}'s letter could not be read just now. It waits in the box."));
            ModLog.Info($"[LETTERBOX] {boxed.Sender?.Name}'s envelope was not written (the model did not answer); it stays in the box.");
            RaiseChanged();

            return;
         }

         if (string.IsNullOrWhiteSpace(boxed.Letter.Content)) boxed.Letter.Content = text;
         StoryLog.WriteLetterReceived(boxed.Letter.SenderName, boxed.Letter.Content, (int)CampaignTime.Now.ToDays);
         if (boxed.Letter.InBox) Show(boxed);
      }

      /// <summary>True when the campaign map is the active screen (not a mission, not a menu over it).</summary>
      internal static bool OnTheMap() => Game.Current?.GameStateManager?.ActiveState is TaleWorlds.CampaignSystem.GameState.MapState;

      private static void Show(BoxedLetter boxed)
      {
         // A letter written earlier (a courier taken on the road) gets its sender's memory enriched now, once.
         LetterMemoryFinalizer.EnrichFromText(boxed.Sender, boxed.Letter, NpcMemoryCampaignBehavior.Instance);
         boxed.Letter.InBox = false;
         Store?.Set(boxed.Sender);
         ModLog.Info($"[LETTERBOX] opened {boxed.Sender.Name}'s letter ({boxed.Letter.Reason}).");
         RaiseChanged();
         LetterWindowManager.OpenRead(boxed.Letter, boxed.Sender);
      }

      /// <summary>
      ///   Throws a waiting letter away unread: it leaves the box, its subject is silenced from that sender (unless it is one
      ///   a throw never silences), another mod hears it was set aside, and the sender's interest cools as for Acknowledge.
      /// </summary>
      public static void Throw(BoxedLetter boxed)
      {
         if (boxed?.Letter == null || boxed.Sender == null || !boxed.Letter.InBox) return;

         PendingLetter letter = boxed.Letter;
         NpcProfile sender = boxed.Sender;
         letter.InBox = false;
         letter.ThrownAway = true;
         sender.SilencedLetterSubjects = LetterBoxPolicy.AfterThrow(sender.SilencedLetterSubjects, letter.Reason);
         ModLetterNotices.OnSetAside(letter);

         if (!LetterMailboxPolicy.BypassesBudget(letter.Reason))
         {
            InterestDeltas deltas = CorrespondenceInterestPolicy.OnAcknowledge();
            sender.CorrespondenceInterest = CorrespondenceInterestPolicy.ClampSender(sender.CorrespondenceInterest + deltas.SenderDelta);
            NpcMemoryCampaignBehavior.Instance?.LetterEvaluator?.NudgeSubject(letter.Reason, deltas.SubjectDelta);
         }

         Store?.Set(sender);
         ModLog.Info($"[LETTERBOX] threw {sender.Name}'s letter ({letter.Reason}) away; silenced: {(LetterBoxPolicy.ThrowSilences(letter.Reason) ? LetterBoxPolicy.SubjectOf(letter.Reason) : "nothing")}.");
         RaiseChanged();
      }

      /// <summary>Lets this correspondent write about a silenced subject again (the letter book's undo).</summary>
      public static void Unsilence(NpcProfile sender, string subject)
      {
         if (sender?.SilencedLetterSubjects == null || !sender.SilencedLetterSubjects.Remove(subject)) return;

         Store?.Set(sender);
         ModLog.Info($"[LETTERBOX] {sender.Name} may write about '{subject}' again.");
      }

      /// <summary>
      ///   Plays the courier's sound: the module's own sound first, the file through the engine's external-file road if that
      ///   does not play. Logged either way, so the first play test says which road works.
      /// </summary>
      public static bool PlayArrivalSound()
      {
         int now = Environment.TickCount;
         if (_lastSoundTick != int.MinValue && unchecked(now - _lastSoundTick) < SoundSpacingMs) return true;
         _lastSoundTick = now;

         try
         {
            if (SoundEvent.PlaySound2D(ArrivalSoundName))
            {
               ModLog.Info("[SOUND] letter arrival: module sound played.");

               return true;
            }

            string path = System.IO.Path.Combine(CalradiaRemembers.Paths.ModulePaths.Root, ArrivalSoundFile);
            if (!System.IO.File.Exists(path))
            {
               ModLog.Info($"[SOUND] letter arrival: the module sound did not play and {path} is missing.");

               return false;
            }

            SoundEvent ev = SoundEvent.CreateEventFromExternalFile("event:/ui/default", path, null, false, false);
            bool played = ev != null && ev.IsValid && ev.Play();
            ModLog.Info($"[SOUND] letter arrival: module sound did not play; external file {(played ? "played" : "did not play")}.");

            return played;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SOUND] letter arrival failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>For a session start: forget the sound's spacing.</summary>
      internal static void ResetForSession()
      {
         _lastSoundTick = int.MinValue;
         _writing = null;
         _session++;
         while (Written.TryDequeue(out _)) { }
         _dirty = true;
      }

      private static void RaiseChanged()
      {
         _dirty = true;
         try { Changed?.Invoke(); }
         catch (Exception ex) { ModLog.Error($"[LETTERBOX] a box listener failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
