// Code written by Gabriel Mailhot, 17/07/2026.
// Bastards (romance audit M-G2): whether a player-parented child is a concealed bastard. The bug this locks: the
// old test read the player's spouse LIVE at birth, so a spouse who died DURING the pregnancy (Spouse already
// null) turned a child conceived inside the marriage into a "concealed bastard", triggering seasonal discovery
// rolls and a scandal that cost the child's own legitimate line renown. The line between a posthumous marital
// heir and a true bastard sits on ONE fact, whether the former spouse is now dead (widowhood) or alive (divorce),
// so it is pinned here in both directions.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class BastardLegitimacyPolicyTests
   {
      // A child by the player's CURRENT spouse is legitimate: the baseline the whole feature is built around.
      [Test]
      public void GIVEN_a_child_by_the_current_spouse_WHEN_deciding_THEN_it_is_not_a_bastard()
      {
         BastardLegitimacyPolicy.IsPlayersBastard(
               playerIsParent: true, otherParentExists: true, otherParentIsCurrentSpouse: true,
               otherParentIsFormerSpouse: false, otherParentAlive: true)
            .Should().BeFalse();
      }

      // The M-G2 core: the other parent is a former spouse who is now DEAD. That is widowhood during the
      // pregnancy, a posthumous heir conceived inside the marriage, NOT a bastard, even though Spouse now reads
      // null. This is the exact case the live-Spouse test mislabeled.
      [Test]
      public void GIVEN_a_posthumous_child_by_a_deceased_former_spouse_WHEN_deciding_THEN_it_is_not_a_bastard()
      {
         BastardLegitimacyPolicy.IsPlayersBastard(
               playerIsParent: true, otherParentExists: true, otherParentIsCurrentSpouse: false,
               otherParentIsFormerSpouse: true, otherParentAlive: false)
            .Should().BeFalse();
      }

      // A child by a LIVING former spouse is a bastard: they are divorced, so the child was conceived outside any
      // marriage. The dead-vs-alive distinction is the whole point, so this must NOT be swept up by the M-G2 fix.
      [Test]
      public void GIVEN_a_child_by_a_living_former_spouse_WHEN_deciding_THEN_it_is_a_bastard()
      {
         BastardLegitimacyPolicy.IsPlayersBastard(
               playerIsParent: true, otherParentExists: true, otherParentIsCurrentSpouse: false,
               otherParentIsFormerSpouse: true, otherParentAlive: true)
            .Should().BeTrue();
      }

      // A child by someone the player was never married to (a lover, a stranger) is a bastard, alive or dead.
      [Test]
      public void GIVEN_a_child_by_a_never_married_partner_WHEN_deciding_THEN_it_is_a_bastard()
      {
         BastardLegitimacyPolicy.IsPlayersBastard(
               playerIsParent: true, otherParentExists: true, otherParentIsCurrentSpouse: false,
               otherParentIsFormerSpouse: false, otherParentAlive: true)
            .Should().BeTrue();
      }

      // Not the player's child at all, or no second parent recorded: never the player's bastard, and never throws.
      [Test]
      public void GIVEN_no_player_parentage_or_no_other_parent_WHEN_deciding_THEN_it_is_not_a_bastard()
      {
         BastardLegitimacyPolicy.IsPlayersBastard(false, true, false, false, true).Should().BeFalse();
         BastardLegitimacyPolicy.IsPlayersBastard(true, false, false, false, true).Should().BeFalse();
      }
   }
}
