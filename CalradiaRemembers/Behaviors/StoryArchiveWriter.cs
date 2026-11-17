// Code written by Gabriel Mailhot, 17/11/2026.
// The story archive's door for what happens OUTSIDE a conversation (Gabriel, 17/11/2026: the archive holds the player's
// whole story, and "certains evenements produits en dehors d'une conversation ... pourrait aussi etre recompose dans cette
// source"). Letters sent and received, and the notices the mod writes about the player's life (a marriage settled, a house
// left), which the Story Log carried before. A conversation writes through ChatViewModel, which knows the room.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.StoryArchive;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Behaviors
{
   /// <summary>Writes letters and the mod's notices into the campaign's story archive. Never throws.</summary>
   public static class StoryArchiveWriter
   {
      /// <summary>A letter the player sent (<paramref name="sent" />) to, or received from, <paramref name="otherName" />.</summary>
      public static void Letter(bool sent, string otherName, string otherId, string content)
         => Write(new StoryArchiveRecord {
            Kind = StoryArchiveKind.Letter, SceneKind = "letter", Time = Now(), IsPlayer = sent, SpeakerId = otherId, SpeakerName = otherName,
            Text = string.IsNullOrWhiteSpace(content) ? "(The letter is blank.)" : content, Audience = Ids(otherId), Tags = Ids(otherId)
         });

      /// <summary>Something the mod told the player about their life, with the heroes it concerns.</summary>
      public static void Notice(string text, params string[] heroIds)
         => Write(new StoryArchiveRecord {
            Kind = StoryArchiveKind.Notice, SceneKind = "world", Time = Now(), Text = text, Audience = Ids(heroIds), Tags = Ids(heroIds)
         });

      private static double Now() => Campaign.Current != null ? CampaignTime.Now.ToDays : 0;

      private static List<string> Ids(params string[] ids) => (ids ?? new string[0]).Where(i => !string.IsNullOrEmpty(i)).Distinct().ToList();

      private static void Write(StoryArchiveRecord draft)
      {
         try
         {
            if (Campaign.Current == null || string.IsNullOrWhiteSpace(draft?.Text)) return;

            StoryArchiveStore store = NpcMemoryCampaignBehavior.Instance?.StoryArchive;
            store?.Append(draft);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[STORY ARCHIVE] {draft?.Kind} not recorded: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
