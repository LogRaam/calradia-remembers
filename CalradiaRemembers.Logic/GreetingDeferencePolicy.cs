// Code written by Gabriel Mailhot, 16/10/2026.
// Whether a character may open a conversation with a deferential greeting. tashmetu (Nexus, 16/10/2026): in a new
// campaign, landless and with no troops, a princess opened with "I am at your disposal, for the moment. What is it?";
// the rest of her reply sized the player up as a landless stranger, but the greeting comes from a fixed list chosen by
// regard and traits, and a few of its lines read as service offered to a superior. Gabriel approved the fix the same
// day: those lines are left out when the speaker outranks the player.
//
// Rank is the rule of rank in correspondence (CorrespondenceRankPolicy, Gabriel 28/09/2026), read the same way: a
// romance, a spouse, the player's own house or close blood, and a character who has come to hold the player dear may
// speak warmly whatever the station; so may anyone to a player who rules a realm.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Which greeting lines offer service, and who may say them to the player. Pure.</summary>
   public static class GreetingDeferencePolicy
   {
      /// <summary>"You honor me with your company. Say what is on your mind."</summary>
      public const string HonorMeWithYourCompany = "You honor me with your company. Say what is on your mind.";

      /// <summary>"I am glad you came. Whatever you need, you have but to ask."</summary>
      public const string WhateverYouNeed = "I am glad you came. Whatever you need, you have but to ask.";

      /// <summary>"Friend! Set your worries down a moment and tell me how I can help."</summary>
      public const string TellMeHowICanHelp = "Friend! Set your worries down a moment and tell me how I can help.";

      /// <summary>"Ah, a familiar face in a hard season. What can I do for you?"</summary>
      public const string FamiliarFaceWhatCanIDo = "Ah, a familiar face in a hard season. What can I do for you?";

      /// <summary>"What can I do for you?"</summary>
      public const string WhatCanIDoForYou = "What can I do for you?";

      /// <summary>"I am at your disposal, for the moment. What is it?"</summary>
      public const string AtYourDisposal = "I am at your disposal, for the moment. What is it?";

      /// <summary>"Whatever I can spare is yours to ask for. Go on."</summary>
      public const string WhateverICanSpare = "Whatever I can spare is yours to ask for. Go on.";

      /// <summary>The lines that offer the player service, as one would to an equal or a superior.</summary>
      public static readonly IReadOnlyCollection<string> DeferentialLines = new HashSet<string> {
         HonorMeWithYourCompany, WhateverYouNeed, TellMeHowICanHelp, FamiliarFaceWhatCanIDo, WhatCanIDoForYou, AtYourDisposal, WhateverICanSpare
      };

      /// <summary>Whether the speaker may offer the player service in a greeting: not when they outrank the player.</summary>
      public static bool MayBeDeferential(CorrespondenceRankFacts f)
      {
         if (f == null) return true;
         if (f.PersonalBond || f.Regard >= Regard.ConfidingTier || f.Player.IsRuler) return true;
         if (f.Sender.IsRuler) return false;

         return f.Sender.TiersAbove(f.Player) <= 0;
      }

      /// <summary>The candidates the speaker may say: every line, or every line but the deferential ones.</summary>
      public static List<string> Filter(IEnumerable<string> candidates, bool mayBeDeferential)
         => (candidates ?? Enumerable.Empty<string>()).Where(c => mayBeDeferential || !DeferentialLines.Contains(c)).ToList();
   }
}
