// Code written by Gabriel Mailhot, 30/10/2026.
// The letter box (Gabriel, 30/10/2026): "toutes les lettres sont envoyees et vont dans la boite aux lettres [...] Une lettre
// lue est enlevee de la boite aux lettres [...] c'est le joueur qui classe, elimine, abonne et desabonne les conversations
// par lettres." A letter that arrives no longer opens a window: the sender's side of the arrival happens as before
// (MarkReceived: the sender's memory, a child's pension quest), the letter waits InBox, the courier's sound plays, and the
// map bar's letter icon counts it. Opening it reads it in the usual window; throwing it away silences its subject from that
// sender (LetterBoxPolicy). The rules are the pure LetterBoxPolicy; this host holds the store, the window and the sound.

#region

using System;
using System.Collections.Generic;
using System.Linq;
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

      /// <summary>Opens a waiting letter in the reading window; it leaves the box. False when the window is busy or it no longer waits.</summary>
      public static bool Open(BoxedLetter boxed)
      {
         if (boxed?.Letter == null || boxed.Sender == null || !boxed.Letter.InBox) return false;
         if (LetterWindowManager.IsOpen) return false;

         boxed.Letter.InBox = false;
         Store?.Set(boxed.Sender);
         ModLog.Info($"[LETTERBOX] opened {boxed.Sender.Name}'s letter ({boxed.Letter.Reason}).");
         RaiseChanged();
         LetterWindowManager.OpenRead(boxed.Letter, boxed.Sender);

         return true;
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
      internal static void ResetForSession() => _lastSoundTick = int.MinValue;

      private static void RaiseChanged()
      {
         try { Changed?.Invoke(); }
         catch (Exception ex) { ModLog.Error($"[LETTERBOX] a box listener failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
