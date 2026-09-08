// Code written by Gabriel Mailhot, 07/09/2026.
// Companion-briefing pillar: holds each companion's standing brief FOR THE LIFE OF THE SESSION ONLY, keyed by
// hero StringId. Deliberately NOT SyncData: a brief expires with the conversation it was carried into (see
// CalradiaRemembers.Logic.CompanionBriefPolicy.HeroesToExpireOnClose), so it must never touch the save format
// or outlive the very short window it exists for. Cleared on session launch (RumorStore's own pattern) so a
// stale entry from a prior campaign in the same process can never leak into a fresh one.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers.Companions
{
   /// <summary>
   ///   In-memory ledger of standing companion briefs, keyed by hero StringId. Nothing here is persisted:
   ///   see the file header for why a brief must never touch the save format.
   /// </summary>
   public static class CompanionBriefingStore
   {
      private static readonly Dictionary<string, string> _briefs = new();

      /// <summary>The hero ids of every companion currently carrying a brief, for the expiry sweep on chat close.</summary>
      public static IReadOnlyCollection<string> BriefedHeroIds => _briefs.Keys;

      /// <summary>
      ///   Stacks <paramref name="instruction" /> onto whatever <paramref name="heroId" /> was already told
      ///   (ruling #3: several briefings stack rather than replace), bounded per
      ///   <see cref="CompanionBriefPolicy.Stack" />. No-op for a blank hero id or a blank instruction.
      /// </summary>
      public static void AddInstruction(string heroId, string instruction)
      {
         if (string.IsNullOrEmpty(heroId)) return;

         _briefs.TryGetValue(heroId, out string existing);
         string stacked = CompanionBriefPolicy.Stack(existing, instruction);

         if (stacked == null) return;
         _briefs[heroId] = stacked;
      }

      /// <summary>Drops every outstanding brief. Call on session launch, so a prior campaign's briefs never leak into a fresh one.</summary>
      public static void Clear() => _briefs.Clear();

      /// <summary>Drops <paramref name="heroId" />'s brief, if any. No-op when they carry none.</summary>
      public static void Clear(string heroId)
      {
         if (!string.IsNullOrEmpty(heroId)) _briefs.Remove(heroId);
      }

      /// <summary>
      ///   The single expiry sweep, run on EVERY chat close (<see cref="Chat.ChatViewModel.FinalizeCompanionBriefs" />
      ///   calls this so the rule lives in exactly one place, and a self-test can drive the identical production
      ///   path). Delegates the "which companions" decision to
      ///   <see cref="CompanionBriefPolicy.HeroesToExpireOnClose" />; see that method's own doc for the ruling it
      ///   encodes. A cheap no-op whenever nothing is outstanding.
      /// </summary>
      public static void ExpireForConversationClose(IEnumerable<string> conversationParticipantHeroIds, bool isBriefingChat)
      {
         if (_briefs.Count == 0) return;

         List<string> toExpire = CompanionBriefPolicy.HeroesToExpireOnClose(BriefedHeroIds, conversationParticipantHeroIds, isBriefingChat);

         foreach (string heroId in toExpire)
            Clear(heroId);
      }

      /// <summary>
      ///   The current standing brief for <paramref name="heroId" />, or null when they carry none. A
      ///   non-destructive read: unlike a one-shot directive (e.g. <c>CompanionMissionBehavior.ConsumeNewsReport</c>),
      ///   a brief must be re-read on EVERY turn of the conversation it rides into, not swallowed by the first.
      /// </summary>
      public static string Peek(string heroId)
         => !string.IsNullOrEmpty(heroId) && _briefs.TryGetValue(heroId, out string brief)
            ? brief
            : null;
   }
}
