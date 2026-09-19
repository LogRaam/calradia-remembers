// Code written by Gabriel Mailhot, 19/09/2026.
// Which outstanding "bring me a prisoner" bargain a captive settles, asked in ONE place.
//
// Player report (Lordfadooboo, Nexus, 18/09/2026): "a khuzite lord asked me to deliver a nord lord to them as a
// prisoner. i was able to buy a nord prisoner off someone else... when i tried to give the prisoner i didnt see
// the button to do that action, so i decided to type out what i meant to do... the character spoke as if i
// completed the quest, i even got paid, but the quest still remained on my board, unable to be completed."
//
// Two verbs hand a captive from the player to an NPC, and only one of them ever looked at the quest:
//
//   give_prisoner   the delivery. Stamps the bargain, pays its reward, closes the journal.
//   buy_prisoner    the sale. The NPC pays a price and takes the captive. No quest is touched.
//
// So the same act, the lord who asked for a Nord receiving a Nord, closed the bargain or left it open forever
// depending on which of the two words the model reached for. And the only prisoner option in the offer bag
// pre-filled "Would you buy this prisoner from me?", which pushes it towards the one that does nothing for the
// quest. "Paid, and the quest still open" is the sale path exactly.
//
// This is the duel crash's shape again: two answers to one question. The question is "does handing THIS
// captive to THIS person settle something he asked for", and both verbs, and the bag, now ask it here.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>A captive the player holds, as far as a bargain cares: who they are and whose realm they serve.</summary>
   public sealed class HeldCaptive
   {
      public string Id { get; init; } = "";

      /// <summary>The realm they serve (MapFaction), which is what a faction-keyed bargain is matched on.</summary>
      public string FactionId { get; init; }
   }

   /// <summary>A delivery the player can make right now: the bargain it settles and the captive who settles it.</summary>
   public sealed class DeliveryMatch
   {
      public InformalQuest Quest { get; init; }

      public string CaptiveId { get; init; } = "";
   }

   /// <summary>Decides which outstanding prisoner-delivery bargain a held captive settles.</summary>
   public static class DeliveryQuestMatcher
   {
      /// <summary>
      ///   The outstanding delivery bargain that handing over <paramref name="captive" /> would settle, or null.
      ///   A bargain naming a hero is settled only by that hero; one naming a realm, by any lord serving it.
      /// </summary>
      public static InformalQuest QuestSettledBy(IEnumerable<InformalQuest> quests, HeldCaptive captive)
      {
         if (quests == null || captive == null || string.IsNullOrEmpty(captive.Id)) return null;

         return quests.FirstOrDefault(q =>
            q != null
            && q.Type == QuestType.DeliverPrisoner
            && q.IsOutstanding
            && new PrisonerDeedTarget(q.TargetHeroId, q.TargetFactionId).Matches(captive.Id, captive.FactionId));
      }

      /// <summary>
      ///   The delivery to make: the first held captive who settles an outstanding bargain. When
      ///   <paramref name="preferredCaptiveId" /> names one who does, it is chosen over any other, because with a
      ///   realm-keyed bargain and two of that realm's lords in the cells, "the one the player named" and "the
      ///   first one found" are different men, and handing over the wrong one is its own small betrayal.
      /// </summary>
      public static DeliveryMatch FirstDelivery(
         IEnumerable<InformalQuest> quests, IEnumerable<HeldCaptive> held, string preferredCaptiveId = null)
      {
         if (quests == null || held == null) return null;

         List<InformalQuest> open = quests.ToList();
         List<HeldCaptive> captives = held.Where(c => c != null && !string.IsNullOrEmpty(c.Id)).ToList();

         if (!string.IsNullOrEmpty(preferredCaptiveId))
         {
            HeldCaptive named = captives.FirstOrDefault(c => c.Id == preferredCaptiveId);
            InformalQuest forNamed = QuestSettledBy(open, named);

            if (forNamed != null) return new DeliveryMatch {Quest = forNamed, CaptiveId = named.Id};
         }

         foreach (HeldCaptive captive in captives)
         {
            InformalQuest quest = QuestSettledBy(open, captive);

            if (quest != null) return new DeliveryMatch {Quest = quest, CaptiveId = captive.Id};
         }

         return null;
      }
   }
}
