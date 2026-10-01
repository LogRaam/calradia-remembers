// Code written by Gabriel Mailhot, 01/10/2026.
// A completed rescue task records what happened, and the giver reads it back. If this is wrong, a lord whose kinsman
// the player broke out of a town's dungeon thanks him for "freeing him from captivity", as if a battle had done it.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RescueEvidenceTests
    {
        // A prison break names the prison: the deed the giver will speak of.
        [Test]
        public void GIVEN_the_player_broke_him_out_of_a_named_prison_WHEN_recorded_THEN_the_prison_is_named()
        {
            RescueEvidence.Describe("Derthert", "Pravend", 120).Should().Be("Broke Derthert out of the prison of Pravend on day 120");
        }

        // Any other freeing (a won battle, a captive let go) keeps the plain line it always had.
        [Test]
        public void GIVEN_any_other_freeing_WHEN_recorded_THEN_the_plain_line_stands()
        {
            RescueEvidence.Describe("Derthert", null, 120).Should().Be("Freed Derthert from captivity on day 120");
        }
    }
}
