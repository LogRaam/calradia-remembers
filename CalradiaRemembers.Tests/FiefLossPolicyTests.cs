// Code written by Gabriel Mailhot, 15/11/2026.
// What FiefLossPolicy decides: whether a fief changing hands is a loss to the one who held it, the blow SelfEsteemService
// records and the day's mood names ("What set your day this way: the loss of Phycaon"). superdeeplearningaibot
// (Discord, 15/11/2026): he and Queen Rhagaea took Phycaon, the realm's vote then gave it to her vassal Strategos
// Turiados, and the queen spent her day mourning "the loss of Phycaon". If this widens, a lord stripped of his fief or a
// town lost to a siege no longer weighs on anyone; if it narrows again, a ruler grieves every town she awards.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class FiefLossPolicyTests
   {
      // THE REPORT: the queen awarded the town she took to her own vassal, and mourned it as lost.
      [Test]
      public void GIVEN_a_ruler_placing_a_town_with_her_own_vassal_WHEN_weighed_THEN_it_is_no_loss_to_her()
      {
         FiefLossPolicy.IsLoss(new FiefHandover {ByTheCrownsHand = true, HolderRulesTheNewHoldersRealm = true}).Should().BeFalse();
      }

      // STAKE: a lord whose fief the crown takes back has lost it, by the very same decision.
      [Test]
      public void GIVEN_a_lord_whose_fief_the_crown_takes_back_WHEN_weighed_THEN_it_is_a_loss_to_him()
      {
         FiefLossPolicy.IsLoss(new FiefHandover {ByTheCrownsHand = true, HolderRulesTheNewHoldersRealm = false}).Should().BeTrue();
      }

      // STAKE: a ruler whose town falls to a siege, her own rebels' included, has lost it.
      [Test]
      public void GIVEN_a_ruler_whose_town_falls_to_a_siege_WHEN_weighed_THEN_it_is_a_loss_to_her()
      {
         FiefLossPolicy.IsLoss(new FiefHandover {ByTheCrownsHand = false, HolderRulesTheNewHoldersRealm = true}).Should().BeTrue();
      }
   }
}
