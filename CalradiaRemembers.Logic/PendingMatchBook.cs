// Code written by Gabriel Mailhot, 16/10/2026.
// Matches another mod asks CR to let the player broker. tashmetu (Nexus, 16/10/2026): a tester asked a head of house, in
// the chat, for his kinswoman's hand for a lord of the player's house (Regard and Standing's matchmaker had proposed
// her); the lord agreed, "bound them together now", and nothing happened, because arrange_match is only taught for a
// match a character asked of the player by letter. He asked that another mod may register a pending match (suitor,
// bride, the head who decides) so the verb is taught for it and CR's own law settles it. Gabriel approved it the same
// day.
//
// CR declares before the model speaks: a registration the law would refuse outright (unknown people, one person, a
// couple the game will not wed, a decider with no say over her) is turned down at the door, with the reason, so the
// head of house is never taught a match the game would then refute. What may change later (her heart, her family's
// regard) is weighed when the head consents, as for any brokered match.
//
// The book is kept in memory only: a mod registers again when a game is loaded. Nothing is written to the save, so a
// mod that is removed leaves nothing behind.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Why a match was or was not taken into the book.</summary>
   public enum MatchRegistrationResult
   {
      /// <summary>Taken: the decider will be taught arrange_match for it.</summary>
      Registered,

      /// <summary>An id is missing, or names no one in the world.</summary>
      RejectedUnknown,

      /// <summary>The suitor and the bride are the same person.</summary>
      RejectedSamePerson,

      /// <summary>The player is the suitor or the bride: the player's own marriage goes through CR's own rules, not this door.</summary>
      RejectedPlayer,

      /// <summary>The game would not wed this couple (age, kin, a marriage already, the same sex).</summary>
      RejectedNotLawful,

      /// <summary>The decider has no say over the bride's hand (neither she herself, nor her house's head or parent).</summary>
      RejectedNoAuthority
   }

   /// <summary>What the door reads about a match offered for registration.</summary>
   public sealed class MatchRegistrationFacts
   {
      /// <summary>The suitor, the bride and the decider all name someone in the world.</summary>
      public bool AllKnown { get; init; }

      /// <summary>The suitor and the bride are one person.</summary>
      public bool SamePerson { get; init; }

      /// <summary>The player is the suitor or the bride.</summary>
      public bool PlayerIsOneOfThem { get; init; }

      /// <summary>The game's own marriage model would wed them.</summary>
      public bool LawfulCouple { get; init; }

      /// <summary>The decider is the bride, or has the say over her hand.</summary>
      public bool DeciderHasSay { get; init; }
   }

   /// <summary>One match waiting for its decider's consent.</summary>
   public sealed class PendingMatchEntry
   {
      /// <summary>The suitor's hero id.</summary>
      public string SuitorId { get; init; }

      /// <summary>The bride's hero id.</summary>
      public string BrideId { get; init; }

      /// <summary>The id of the one who decides: the bride herself, or the head of her house or a parent.</summary>
      public string DeciderId { get; init; }

      /// <summary>The mod that registered it.</summary>
      public string Source { get; init; }
   }

   /// <summary>The matches other mods have registered, and the door they pass. Pure.</summary>
   public sealed class PendingMatchBook
   {
      private readonly List<PendingMatchEntry> _entries = new();

      /// <summary>Every match waiting, in the order registered.</summary>
      public IReadOnlyList<PendingMatchEntry> Entries => _entries;

      /// <summary>The door: whether a match may be registered, read before anything is kept.</summary>
      public static MatchRegistrationResult Judge(MatchRegistrationFacts f)
      {
         if (f == null || !f.AllKnown) return MatchRegistrationResult.RejectedUnknown;
         if (f.SamePerson) return MatchRegistrationResult.RejectedSamePerson;
         if (f.PlayerIsOneOfThem) return MatchRegistrationResult.RejectedPlayer;
         if (!f.LawfulCouple) return MatchRegistrationResult.RejectedNotLawful;
         if (!f.DeciderHasSay) return MatchRegistrationResult.RejectedNoAuthority;

         return MatchRegistrationResult.Registered;
      }

      /// <summary>Keeps the match; one registered again for the same couple replaces the earlier one.</summary>
      public void Add(PendingMatchEntry entry)
      {
         if (entry == null) return;
         Remove(entry.SuitorId, entry.BrideId);
         _entries.Add(entry);
      }

      /// <summary>Drops the match of this couple; false when there was none.</summary>
      public bool Remove(string suitorId, string brideId)
         => _entries.RemoveAll(e => string.Equals(e.SuitorId, suitorId, StringComparison.Ordinal) && string.Equals(e.BrideId, brideId, StringComparison.Ordinal)) > 0;

      /// <summary>The first match this decider has the say over, among those still standing; null when none.</summary>
      public PendingMatchEntry ForDecider(string deciderId)
         => _entries.FirstOrDefault(e => string.Equals(e.DeciderId, deciderId, StringComparison.Ordinal));

      /// <summary>Drops every match <paramref name="stillStands" /> says is over (one of them dead or wed, the decider's say lost).</summary>
      public int Prune(Func<PendingMatchEntry, bool> stillStands)
         => stillStands == null ? 0 : _entries.RemoveAll(e => !stillStands(e));

      /// <summary>Forgets every match (a new game, or a game loaded).</summary>
      public void Clear() => _entries.Clear();
   }
}
