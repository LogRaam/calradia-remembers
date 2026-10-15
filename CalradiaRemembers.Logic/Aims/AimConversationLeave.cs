// Code written by Gabriel Mailhot, 14/10/2026.
// The leave to raise an aim in a conversation is decided ONCE, when the conversation opens, and held until it ends
// (tashmetu, 14/10/2026: "once in a conversation at most"). The prompt is rebuilt on every reply; asking
// AimRaisePolicy again on each would answer TooSoon from the second reply on, since the ledger was marked at the
// opening, and the lord would lose his leave mid-conversation with one reply to use it in. "Raised" is the floor
// given once (Claude's call on Gabriel's delegation, 14/10/2026, to review): the opening marks the ledger whether or
// not the lord takes it up, the way a recent matter is spent when its opener is used.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Aims
{
   /// <summary>What the opening of a conversation needs to decide the leave.</summary>
   public sealed class AimOpening
   {
      /// <summary>The conversation's id (CrConversation's SessionId).</summary>
      public string SessionId { get; init; }

      /// <summary>The aim the lord holds, or null.</summary>
      public StandingAim Aim { get; init; }

      /// <summary>The live facts about the lord and the player, minus the ledger (taken from the aim).</summary>
      public AimRaiseFacts Facts { get; init; }

      /// <summary>The game day.</summary>
      public int Today { get; init; }
   }

   /// <summary>Holds, per open conversation, whether the lord was given leave to raise his aim.</summary>
   public sealed class AimConversationLeave
   {
      private readonly Dictionary<string, StandingAim> _granted = new Dictionary<string, StandingAim>(StringComparer.Ordinal);

      /// <summary>
      ///   Decides the leave at the opening, marks the ledger when it is given, and returns the verdict. A second
      ///   opening of the same session changes nothing.
      /// </summary>
      public AimRaiseVerdict Open(AimOpening opening, AimBook book)
      {
         if (opening == null || string.IsNullOrWhiteSpace(opening.SessionId) || opening.Aim == null || book == null)
            return AimRaiseVerdict.Spent;
         if (_granted.ContainsKey(opening.SessionId)) return AimRaiseVerdict.MayRaise;

         var live = opening.Facts ?? new AimRaiseFacts();
         var verdict = AimRaisePolicy.Decide(new AimRaiseFacts
         {
            TimesRaised = opening.Aim.TimesRaised,
            DaysSinceLastRaised = opening.Aim.LastRaisedDay.HasValue ? opening.Today - opening.Aim.LastRaisedDay.Value : int.MaxValue,
            IsEnemyOfPlayer = live.IsEnemyOfPlayer,
            IsCaptivity = live.IsCaptivity,
            IsExpired = opening.Aim.ExpiresOnDay.HasValue && opening.Aim.ExpiresOnDay.Value < opening.Today,
            ChannelOpen = (opening.Aim.Channels & AimChannels.Conversation) != 0
         });

         if (verdict != AimRaiseVerdict.MayRaise) return verdict;

         book.MarkRaised(opening.Aim, opening.Today, AimChannels.Conversation);
         _granted[opening.SessionId] = opening.Aim;
         return verdict;
      }

      /// <summary>True for every reply of a conversation whose opening gave the lord leave to raise his aim.</summary>
      public bool MayRaise(string sessionId)
         => !string.IsNullOrWhiteSpace(sessionId) && _granted.ContainsKey(sessionId);

      /// <summary>Forgets the conversation once it ends.</summary>
      public void Close(string sessionId)
      {
         if (!string.IsNullOrWhiteSpace(sessionId)) _granted.Remove(sessionId);
      }
   }
}
