// Code written by Gabriel Mailhot, 17/07/2026.
// CHRONICLE: whether a conception the player is party to is chronicled PUBLICLY (romance audit C5). This gate is
// the last line before a conception becomes a PlayerFeat that NPCs, tavern rumours, and the player's spouse all
// learn. It protects the whole bastard pillar: get it wrong in one direction and an adulterous conception leaks
// the day it happens (secret dead, "an heir" said of a bastard); get it wrong the other way and a legitimate
// heir's begetting is never recorded. Both are silent, hard-to-notice regressions, so they are pinned here.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ConceptionChroniclePolicyTests
   {
      // The C5 core: an adulterous conception (the other parent is NOT the player's spouse) is a hidden bastard
      // and must NEVER be chronicled publicly, whatever the partner's rank, or the secret leaks at conception.
      [Test]
      public void GIVEN_an_adulterous_conception_WHEN_deciding_THEN_it_is_not_a_public_heir()
      {
         ConceptionChroniclePolicy.IsPublicHeir(false, true).Should().BeFalse();
      }

      // A legitimate conception with a lord spouse IS a public heir: this is the one case the chronicle is for,
      // and it must keep firing so a married player's true heir is still recorded.
      [Test]
      public void GIVEN_a_legitimate_conception_with_a_lord_spouse_WHEN_deciding_THEN_it_is_a_public_heir()
      {
         ConceptionChroniclePolicy.IsPublicHeir(true, true).Should().BeTrue();
      }

      // Even a spouse who is not a lord of standing (a commoner) is not chronicle material: the feeder is about
      // heirs "got upon a hero of standing", so the rank gate must survive alongside the legitimacy gate.
      [Test]
      public void GIVEN_a_spouse_who_is_not_a_lord_WHEN_deciding_THEN_it_is_not_a_public_heir()
      {
         ConceptionChroniclePolicy.IsPublicHeir(true, false).Should().BeFalse();
      }
   }
}
