// Code written by Gabriel Mailhot, 13/06/2026.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>
   ///   A thin <see cref="QuestBase" /> wrapper around an <see cref="InformalQuest" />
   ///   that makes it visible in Bannerlord's native quest journal. The quest journal
   ///   entry is a MIRROR of the data owned by our <see cref="InformalQuest" /> — it
   ///   is never the authoritative source. All lifecycle decisions (completion, expiry,
   ///   cancellation, abandonment) happen in <see cref="QuestService" /> which then
   ///   calls the appropriate <c>CompleteQuestWith…</c> here to close the mirror.
   ///   Anti-stuck guarantee: <see cref="QuestJournalBridge.ReconcileOnSessionLaunch" />
   ///   force-closes any mirror whose <see cref="InformalQuest" /> is no longer
   ///   outstanding on every session launch. A stuck entry cannot outlive one session.
   ///   Saveable fields: only <see cref="_giverId" /> and <see cref="_questKey" />.
   ///   Everything else is reconstituted from the live <see cref="InformalQuest" /> on
   ///   load via <see cref="InitializeQuestOnGameLoad" />.
   /// </summary>
   public sealed class CalradiaRemembersQuest : QuestBase
   {
      // Saveable field IDs — do not reuse.
      [SaveableField(1)]
      private readonly string _giverId;

      [SaveableField(2)]
      private readonly string _questKey;

      /// <summary>
      ///   Creates a new mirror for <paramref name="quest" />.
      /// </summary>
      public CalradiaRemembersQuest(InformalQuest quest, Hero giver)
         : base(
            BuildQuestId(giver, quest),
            giver,
            // Stored directly as the engine due time (no arithmetic): never times out. WE own
            // deadline handling (QuestService.RunDailyMaintenance) and hide the engine
            // countdown (IsRemainingTimeHidden).
            CampaignTime.Never,
            quest.RewardGold)
      {
         _giverId = giver.StringId;
         _questKey = BuildQuestKey(giver, quest);

         AddLog(BuildDescriptionText(quest));
         // A factual bearing the LLM's own description may not carry, so the player is pointed to the region of
         // a lair that stays hidden until scouted (the map marker sits on the anchor town, not the hidden lair).
         if (!string.IsNullOrWhiteSpace(quest.DirectionHint))
            AddLog(new TextObject($"The lair lies to the {quest.DirectionHint}."));
         SetDialogs();
         StartQuest();
         TrackTarget(quest);
      }

      // We manage deadline ourselves in QuestService.RunDailyMaintenance —
      // hide the engine's countdown to avoid double-expiry display.
      public override bool IsRemainingTimeHidden => true;

      /// <summary>
      ///   LOAD-BEARING (root cause of the "failed then re-accepted on reload" noise): at load,
      ///   <c>QuestManager.OnGameLoaded</c> CANCELS every non-finalized quest that is neither attached to a live
      ///   Issue nor special ("There is not active issue for quest ... Quest will be canceled"). Our mirrors have
      ///   no Issue, so a non-empty special type is the engine's own path for issue-less custom quests to survive
      ///   a reload. Side effects verified in the decompiled engine: exempt from the clan-leader-change auto-cancel
      ///   (our reconciler owns that lifecycle) and a cosmetic quest-marker icon variant; the journal UI ignores it.
      /// </summary>
      public override string SpecialQuestType => "CalradiaRemembers";

      /// <summary>
      ///   The STABLE match key (<see cref="BuildQuestId" /> at creation, saved verbatim). Match against THIS,
      ///   never <see cref="MBObjectBase.StringId" />: the engine can rewrite a quest's StringId on registration
      ///   (a numeric suffix on collision), which silently broke both the close path and the session-launch
      ///   reconciler and made completed quests read as cancelled on reload. Built from append-only, stable parts
      ///   (giver id, quest type ordinal, issue day), so it survives version upgrades and reloads intact.
      /// </summary>
      public string QuestKey => _questKey;

      public override TextObject Title => new(Mapping.ShownText.Of(ResolveLiveQuest()?.Description ?? _questKey));

      /// <summary>
      ///   Closes the mirror as cancelled (war ended, giver died, etc.) — no penalty,
      ///   no reward.
      /// </summary>
      public void CloseAsCancelled(string reason)
      {
         try { CompleteQuestWithCancel(new TextObject(reason)); }
         catch (Exception ex) { ModLog.Error($"[QUEST-JOURNAL] CloseAsCancelled failed: {ex.Message}"); }
      }

      /// <summary>
      ///   Closes the mirror with success and pays the gold reward.
      /// </summary>
      public void CloseAsCompleted()
      {
         try { CompleteQuestWithSuccess(); }
         catch (Exception ex) { ModLog.Error($"[QUEST-JOURNAL] CloseAsCompleted failed: {ex.Message}"); }
      }

      /// <summary>
      ///   Closes the mirror as failed (deadline missed or abandoned).
      /// </summary>
      public void CloseAsFailed(string reason)
      {
         try { CompleteQuestWithFail(new TextObject(reason)); }
         catch (Exception ex) { ModLog.Error($"[QUEST-JOURNAL] CloseAsFailed failed: {ex.Message}"); }
      }

      /// <summary>
      ///   Called when the deed is verified. Updates the journal log so the player
      ///   sees « Done — report back to [giver] » in the quest detail.
      /// </summary>
      public void MarkDeedDone(string evidence)
      {
         AddLog(new TextObject($"Done: {evidence} — return to {QuestGiver?.Name} to collect your reward."));
      }

      internal static string BuildQuestId(Hero giver, InformalQuest quest)
         => $"cr_quest_{giver.StringId}_{(int) quest.Type}_{quest.IssuedOnDay}";

      protected override void InitializeQuestOnGameLoad()
      {
         // Reconnect the live quest object. If it is gone or terminal the
         // reconciler (QuestJournalBridge.ReconcileOnSessionLaunch) will
         // call CloseAsCancelled, so we don't need to act here.
         SetDialogs();
      }

      // ── QuestBase contract ────────────────────────────────────────────────

      protected override void SetDialogs()
      {
         // No conversation flows needed — we use our own chat window.
      }

      #region private

      /// <summary>
      ///   Places a map marker on the deed's target so the player can actually find where to go: the hero to
      ///   capture / kill / rescue / deliver to / scout (preferred when the quest is hero-bound), the specific
      ///   hidden lair of a bandit-hideout deed (so the player is led to the fight, not the anchor town), or
      ///   otherwise the settlement to clear bandits around, besiege, or raid. Faction-bound deeds (attack a
      ///   kingdom, a caravan) have no single point and simply show no marker. Best-effort and never throws: an
      ///   unresolvable target just leaves the quest markerless, as before.
      /// </summary>
      private void TrackTarget(InformalQuest quest)
      {
         try
         {
            switch (ResolveMapMarkerTarget(quest))
            {
               case Hero hero:
                  AddTrackedObject(hero);

                  break;
               case Settlement settlement:
                  AddTrackedObject(settlement);

                  break;
            }
         }
         catch (Exception ex) { ModLog.Error($"[QUEST-JOURNAL] TrackTarget failed: {ex.Message}"); }
      }

      /// <summary>
      ///   The single map object this quest should mark, or null for a markerless (faction-bound) deed. A
      ///   BanditHideout deed points at its bound lair (<see cref="InformalQuest.TargetHideoutId" />) rather than
      ///   the anchor town, so the marker sits on the actual base the player must clear (player report: told to
      ///   clear a base "south of the village" but the marker sat on the village). A lair that no longer resolves
      ///   (already cleared, or a save from before hideouts were bound) falls back to the anchor town. Split out so
      ///   the marker choice is drivable by the self-test without opening a live journal.
      /// </summary>
      internal static MBObjectBase ResolveMapMarkerTarget(InformalQuest quest)
      {
         if (quest == null) return null;

         if (!string.IsNullOrWhiteSpace(quest.TargetHeroId))
         {
            Hero hero = Hero.FindFirst(h => h.StringId == quest.TargetHeroId);
            if (hero != null) return hero;
         }

         if (quest.Type == QuestType.BanditHideout && !string.IsNullOrWhiteSpace(quest.TargetHideoutId))
         {
            Settlement hideout = Settlement.Find(quest.TargetHideoutId);
            if (hideout != null) return hideout;
         }

         if (string.IsNullOrWhiteSpace(quest.TargetSettlement)) return null;

         return ResolveSettlementByName(quest.TargetSettlement);
      }

      /// <summary>Resolves a settlement by display name (exact preferred), matching how QuestFactory bound it.</summary>
      private static Settlement ResolveSettlementByName(string name)
      {
         Settlement match = null;
         foreach (Settlement s in Settlement.All)
         {
            string sn = s?.Name?.ToString();
            if (string.IsNullOrEmpty(sn)) continue;
            if (string.Equals(sn, name, StringComparison.OrdinalIgnoreCase)) return s;
            if (match == null && sn.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) match = s;
         }

         return match;
      }

      private static TextObject BuildDescriptionText(InformalQuest quest) => new(Mapping.ShownText.Of(quest.Description));

      private static string BuildQuestKey(Hero giver, InformalQuest quest)
         => BuildQuestId(giver, quest);

      // ── Helpers ───────────────────────────────────────────────────────────

      private InformalQuest ResolveLiveQuest()
      {
         if (_giverId == null) return null;
         Hero giver = Hero.FindFirst(h => h.StringId == _giverId);

         if (giver == null) return null;

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(_giverId);

         return profile?.ActiveQuests.Find(q => BuildQuestKey(giver, q) == _questKey);
      }

      #endregion
   }
}