// Code written by Gabriel Mailhot, 03/10/2026.
// The journal entry of an authored quest (docs/design/AUTHORED_QUESTS.md). Gabriel (03/10/2026): a quest should look
// different from a task in the journal, so the player sees at a glance that this is a story; its title reads "Tale:".
// A mirror only: the run (AuthoredQuestBehavior) is the authority, and closes it. Saveable id 4 in
// CalradiaRemembersQuestTypeDefiner, additive.

#region

using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>The journal mirror of an authored quest.</summary>
   public sealed class AuthoredStoryQuest : QuestBase
   {
      [SaveableField(1)] private readonly string _storyId;
      [SaveableField(2)] private readonly string _title;

      public AuthoredStoryQuest(string storyId, string title, Hero giver, string firstLine)
         : base($"cr_story_{storyId}_{(int) CampaignTime.Now.ToDays}", giver, CampaignTime.Never, 0)
      {
         _storyId = storyId;
         _title = title;
         if (!string.IsNullOrWhiteSpace(firstLine)) AddLog(new TextObject(Mapping.ShownText.Of(firstLine)));
         SetDialogs();
         StartQuest();
      }

      /// <summary>The authored quest id this entry mirrors.</summary>
      public string StoryId => _storyId;

      public override TextObject Title => new($"Tale: {_title}");
      public override bool IsRemainingTimeHidden => true;

      /// <summary>Issue-less quests survive a load only as "special" (see CalradiaRemembersQuest).</summary>
      public override string SpecialQuestType => "CalradiaRemembersTale";

      /// <summary>Marks a place on the map as part of the tale, as a vanilla quest marks its destination (fkasad, 06/10/2026).</summary>
      public void Track(TaleWorlds.CampaignSystem.ITrackableCampaignObject target)
      {
         try
         {
            if (target != null) AddTrackedObject(target);
         }
         catch (Exception ex) { ModLog.Error($"[TALE] tracking failed: {ex.Message}"); }
      }

      public void Log(string line)
      {
         try { AddLog(new TextObject(Mapping.ShownText.Of(line))); }
         catch (Exception ex) { ModLog.Error($"[TALE] journal log failed: {ex.Message}"); }
      }

      public void CloseAsCompleted()
      {
         try { CompleteQuestWithSuccess(); }
         catch (Exception ex) { ModLog.Error($"[TALE] close (completed) failed: {ex.Message}"); }
      }

      public void CloseAsFailed(string reason)
      {
         try { CompleteQuestWithFail(new TextObject(reason)); }
         catch (Exception ex) { ModLog.Error($"[TALE] close (failed) failed: {ex.Message}"); }
      }

      public void CloseAsCancelled(string reason)
      {
         try { CompleteQuestWithCancel(new TextObject(reason)); }
         catch (Exception ex) { ModLog.Error($"[TALE] close (cancelled) failed: {ex.Message}"); }
      }

      protected override void InitializeQuestOnGameLoad() => SetDialogs();

      protected override void SetDialogs()
      {
         // No vanilla dialogue: the story plays in CR's chat.
      }
   }
}
