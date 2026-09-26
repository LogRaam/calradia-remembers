// Code written by Gabriel Mailhot, 25/09/2026.
// Two different tasks hide behind "capture that lord" (Gabriel's ruling, 25/09/2026, on FloatingMammal's report):
//
//   - In WAR, a lord asks the player to take an ENEMY: a war errand. Its deed is the capture, and if the war ends the
//     task is set aside (QuestTargetVoidPolicy.ShouldVoidOnPeace), since carrying it out would break the peace.
//   - In PEACE, a lord asks the player to take a RIVAL, for blackmail, vengeance, a political scheme. The point is to
//     have the rival in his own hands, and no war has anything to do with it.
//
// FloatingMammal's case was the second, run as the first: the task was created as a capture, the daily sweep saw no war
// and voided it the next day in silence, and when he brought the prisoner, handing him over settled nothing ("There is no
// such bargain between us"). A capture made in peace is therefore issued as the DELIVERY it is: take him, and bring him
// to me. DeliverPrisoner is completed by handing the captive over, is not a hostile deed, and no peace ever voids it.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Decides what a proposed capture really is, from whether the giver is at war with the target. Pure.</summary>
   public static class CaptureQuestPolicy
   {
      /// <summary>
      ///   The quest type to issue for <paramref name="proposed" />: a capture proposed while the giver is NOT at war
      ///   with the target's realm becomes a delivery; everything else is issued as proposed.
      /// </summary>
      public static QuestType TypeToIssue(QuestType proposed, bool giverAtWarWithTarget)
         => proposed == QuestType.CapturePrisoner && !giverAtWarWithTarget ? QuestType.DeliverPrisoner : proposed;
   }
}
