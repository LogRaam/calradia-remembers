// Code written by Gabriel Mailhot, 14/10/2026.
// HOW a lord goes after his aim, from his traits (tashmetu, 14/10/2026: "an honest lord asks openly, a calculating or
// dishonest one works around the player or uses them"). The sentence is given to the lord in his prompt. If it is
// wrong: an honest lord schemes, a schemer blurts, or worst, a devious lord is told to deceive, against Gabriel's ruling
// that NPCs never lie (20/09/2026); or a lord asks for a service the game will never verify, against gating before
// declaration (09/09/2026).

#region

using CalradiaRemembers.Logic.Aims;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Aims
{
   [TestFixture]
   public sealed class AimApproachWordingTests
   {
      // tashmetu: "an honest lord asks openly".
      [Test]
      public void GIVEN_an_honourable_lord_WHEN_choosing_how_he_pursues_his_aim_THEN_he_asks_openly()
      {
         AimApproachWording.Choose(new AimApproachFacts { Honor = 1 }).Should().Be(AimApproach.Open);
      }

      // tashmetu: "a calculating or dishonest one works around the player". An honourable schemer still schemes.
      [Test]
      public void GIVEN_a_calculating_or_dishonest_lord_WHEN_choosing_THEN_he_works_around_the_player()
      {
         AimApproachWording.Choose(new AimApproachFacts { Calculating = 1, Honor = 1 }).Should().Be(AimApproach.Guarded);
         AimApproachWording.Choose(new AimApproachFacts { Honor = -1 }).Should().Be(AimApproach.Guarded);
      }

      // NPCs never lie (Gabriel, 20/09/2026): a guarded lord conceals, and he is told so in the same breath as he is
      // told to be guarded.
      [Test]
      public void GIVEN_a_guarded_lord_WHEN_his_approach_is_worded_THEN_he_is_told_he_never_states_a_falsehood()
      {
         string text = AimApproachWording.Describe(new AimApproachFacts { Calculating = 2 });

         text.Should().Contain("never tell them a falsehood");
         text.Should().NotContainEquivalentOf("lie to");
         text.Should().NotContainEquivalentOf("deceive");
      }

      // Gating before declaration: without a verified deed behind it the lord must not promise or demand as if the
      // world will hold the player to it.
      [Test]
      public void GIVEN_an_aim_without_a_verified_deed_WHEN_worded_THEN_the_lord_sounds_out_and_promises_nothing_unmade()
      {
         string text = AimApproachWording.Describe(new AimApproachFacts { Honor = 1 });

         text.Should().Contain("promise nothing");
         text.Should().NotContain("ask them outright for the service");
      }

      // With a MissionBoard deed behind the aim the service is real, so an honest lord may simply ask for it.
      [Test]
      public void GIVEN_an_aim_backed_by_a_verified_deed_WHEN_worded_THEN_the_lord_may_ask_for_the_service_outright()
      {
         AimApproachWording.Describe(new AimApproachFacts { Honor = 1, HasVerifiedService = true })
            .Should().Contain("ask them outright for the service");
      }
   }
}
