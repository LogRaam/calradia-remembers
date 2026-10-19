// Code written by Gabriel Mailhot, 25/09/2026.
// Documents what a proposed capture becomes. FloatingMammal (Nexus, 25/09/2026): a lord asked him to capture a rival his
// realm was not at war with; no task stuck, and handing the prisoner over was refused as "no such bargain". Gabriel's
// ruling the same day: in war a capture of an enemy is a war errand, set aside if the war ends; in peace a capture of a
// rival (blackmail, vengeance, a scheme) is a different task that no war touches. The type decides both how the task is
// completed and whether a peace can void it.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptureQuestPolicyTests
   {
      // The war errand is unchanged: its deed is the capture, and peace sets it aside.
      [Test]
      public void GIVEN_a_capture_of_an_enemy_in_war_WHEN_issued_THEN_it_stays_a_capture()
      {
         CaptureQuestPolicy.TypeToIssue(QuestType.CapturePrisoner, giverAtWarWithTarget: true).Should().Be(QuestType.CapturePrisoner);
      }

      // The report: a rival taken in peace is wanted in the giver's own hands. As a delivery it is completed by handing
      // him over, and no peace ever voids it, since it never depended on a war.
      [Test]
      public void GIVEN_a_capture_of_a_rival_in_peace_WHEN_issued_THEN_it_is_a_delivery_into_the_givers_hands()
      {
         CaptureQuestPolicy.TypeToIssue(QuestType.CapturePrisoner, giverAtWarWithTarget: false).Should().Be(QuestType.DeliverPrisoner);
      }

      // Only a capture is reread; every other task is issued exactly as proposed, war or not.
      [Test]
      public void GIVEN_any_other_task_WHEN_issued_THEN_it_is_issued_as_proposed()
      {
         CaptureQuestPolicy.TypeToIssue(QuestType.AttackLord, giverAtWarWithTarget: false).Should().Be(QuestType.AttackLord);
      }

      // The delivery that replaces it is exactly the kind a treaty never sets aside, which is the whole ruling.
      [Test]
      public void GIVEN_the_delivery_a_rival_capture_becomes_WHEN_peace_holds_THEN_it_is_never_voided()
      {
         QuestTargetVoidPolicy.ShouldVoidOnPeace(QuestType.DeliverPrisoner, hasTargetHero: true, giverAtWarWithTargetFaction: false).Should().BeFalse();
      }
   }
}
