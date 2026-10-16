// Code written by Gabriel Mailhot, 16/10/2026.
// When the manual action menu offers "You lay together (a child may come of it)", the hand-sent impregnation_risk.
// tashmetu (Nexus, 16/10/2026): the menu had no such entry, so a scene the interpreter missed (a local model now and
// then "produced no tags") could not be recorded in any mode. Gabriel approved it the same day, between lovers or
// spouses, at a content level that permits a conception; the dice still decide. Wrong one way, a player on Off or
// Mature is shown a conception option at all; wrong the other way, lovers whose scene was missed have no way to say it
// happened, or a stranger's conversation offers it.

#region

using System.Linq;
using CalradiaRemembers.Logic.Actions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Actions
{
   [TestFixture]
   public sealed class ManualConceptionRiskTests
   {
      private static ManualActionStatus Find(ManualActionFacts facts)
         => ManualActionAvailability.Describe(facts).SingleOrDefault(s => s.Entry.Type == ManualActionCatalog.ConceptionRisk.Type);

      // STAKE: below Explicit the option is not even shown as blocked; the content level the player chose hides it.
      [Test]
      public void GIVEN_a_content_level_below_explicit_WHEN_the_panel_is_built_THEN_the_scene_is_not_listed_at_all()
      {
         Find(new ManualActionFacts {ContentPermitsConception = false, PairingCanConceive = true, NpcIsIntimatePartner = true}).Should().BeNull();
      }

      // STAKE: lovers or spouses whose scene the interpreter missed can record it by hand.
      [Test]
      public void GIVEN_lovers_who_could_conceive_at_explicit_WHEN_the_panel_is_built_THEN_the_scene_is_open()
      {
         Find(new ManualActionFacts {ContentPermitsConception = true, PairingCanConceive = true, NpcIsIntimatePartner = true})
            .State.Should().Be(ManualActionState.Open);
      }

      // STAKE: with someone who is not their lover, a click must not stand in for a scene the two never shared.
      [Test]
      public void GIVEN_a_character_who_is_not_a_lover_WHEN_the_panel_is_built_THEN_the_scene_is_blocked_and_says_why()
      {
         ManualActionStatus s = Find(new ManualActionFacts {ContentPermitsConception = true, PairingCanConceive = true, NpcIsIntimatePartner = false});

         s.State.Should().Be(ManualActionState.Blocked);
         s.Reason.Should().Be("you are not lovers");
      }

      // STAKE: a pairing that cannot conceive is not offered a conception, whoever they are.
      [Test]
      public void GIVEN_a_pairing_that_cannot_conceive_WHEN_the_panel_is_built_THEN_it_does_not_apply_to_them()
      {
         Find(new ManualActionFacts {ContentPermitsConception = true, PairingCanConceive = false, NpcIsIntimatePartner = true})
            .State.Should().Be(ManualActionState.NotForThisPerson);
      }

      // STAKE: the entry sends the very verb the interpreter would have sent, so the bridge's own guards and dice apply.
      [Test]
      public void GIVEN_the_entry_WHEN_read_THEN_it_sends_impregnation_risk_without_a_parameter()
      {
         ManualActionCatalog.ConceptionRisk.Type.Should().Be("impregnation_risk");
         ManualActionCatalog.ConceptionRisk.ParameterKind.Should().Be(ManualActionParameterKind.None);
         ManualActionCatalog.All.Should().NotContain(ManualActionCatalog.ConceptionRisk);
      }
   }
}
