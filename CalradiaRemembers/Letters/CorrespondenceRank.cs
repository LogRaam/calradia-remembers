// Code written by Gabriel Mailhot, 28/09/2026.
// Reads rank from the game for CorrespondenceRankPolicy (pure, tested): who writes to the player in their own hand, who
// has the letter written for them, and who rides out in person (Gabriel, 28/09/2026: the Empress Rhagaea wrote in her
// own hand to a landless newcomer, and had ridden out herself to find him). Call on the game thread.

#region

using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>The rule of rank, read from the live game.</summary>
   public static class CorrespondenceRank
   {
      /// <summary>Whether <paramref name="sender" /> deals with the player in person; <paramref name="reason" /> null for a meeting on the road.</summary>
      public static bool DealsInPerson(Hero sender, NpcProfile profile, LetterReason? reason)
         => sender == null || CorrespondenceRankPolicy.DealsInPerson(Facts(sender, profile, reason));

      /// <summary>The title a letter from <paramref name="sender" /> is written under, or null when they write it themselves.</summary>
      public static string WrittenByFor(Hero sender, NpcProfile profile, LetterReason reason)
      {
         if (DealsInPerson(sender, profile, reason)) return null;

         return CorrespondenceRankPolicy.ScribeTitle(sender.Name?.ToString(), Mapping.StatureReader.Of(sender).IsRuler, sender.IsFemale);
      }

      /// <summary>The facts the rule reads, for the self-test seam.</summary>
      internal static CorrespondenceRankFacts Facts(Hero sender, NpcProfile profile, LetterReason? reason)
      {
         Hero player = Hero.MainHero;

         return new CorrespondenceRankFacts {
            Sender = Mapping.StatureReader.Of(sender),
            Player = Mapping.StatureReader.Player,
            PersonalBond = PersonalBond(sender, player, profile),
            Regard = profile?.ReputationWithPlayer ?? 0,
            Reason = reason
         };
      }

      /// <summary>A romance, a spouse, the player's own house, or close blood.</summary>
      private static bool PersonalBond(Hero sender, Hero player, NpcProfile profile)
      {
         if (sender == null || player == null) return false;
         if (sender.Spouse == player || (sender.Clan != null && sender.Clan == player.Clan)) return true;
         if (sender.Father == player || sender.Mother == player || player.Father == sender || player.Mother == sender) return true;
         if (sender.Father != null && sender.Father == player.Father || sender.Mother != null && sender.Mother == player.Mother) return true;

         return profile?.Romantic != null && profile.Romantic.Status.IsCuriousOrDeeper();
      }
   }
}
