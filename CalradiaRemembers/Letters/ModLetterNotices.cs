// Code written by Gabriel Mailhot, 20/10/2026.
// tashmetu (20/10/2026): "an event when the player seals a reply to a letter from another mod [...] Acknowledge could have
// its own event too." The letter window says what the player did with another mod's letter here; CrLetters, the public
// door, listens (the submodule connects the two at load, since Letters cannot name the door that reads it).

#region

using System;
using CalradiaRemembers.Foundation;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>What the player did with a letter another mod delivered.</summary>
   internal static class ModLetterNotices
   {
      /// <summary>The player sealed a reply to it (PlayerReply holds the text). Set by the submodule at load.</summary>
      internal static Action<PendingLetter> Replied;

      /// <summary>The player read it and set it aside without replying. Set by the submodule at load.</summary>
      internal static Action<PendingLetter> SetAside;

      /// <summary>Tells the door the player answered another mod's letter; nothing for CR's own letters.</summary>
      internal static void OnReplied(PendingLetter letter)
      {
         if (letter?.Reason != LetterReason.FromAnotherMod) return;
         Raise(Replied, letter, "reply");
      }

      /// <summary>Marks another mod's letter as set aside, once, unless it was answered, and tells the door.</summary>
      internal static void OnSetAside(PendingLetter letter)
      {
         if (letter?.Reason != LetterReason.FromAnotherMod || letter.SetAside || !string.IsNullOrWhiteSpace(letter.PlayerReply)) return;
         letter.SetAside = true;
         Raise(SetAside, letter, "set-aside");
      }

      private static void Raise(Action<PendingLetter> notice, PendingLetter letter, string what)
      {
         try { notice?.Invoke(letter); }
         catch (Exception ex) { ModLog.Error($"[LETTER] the {what} notice for a mod's letter failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
