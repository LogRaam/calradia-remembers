// Code written by Gabriel Mailhot, 10/08/2026.
// Guards TrystPolicy (Inc 3b-a): whether a partner who has taken a lover MEETS him this week. A tryst may
// happen only when she has a lover, the player is not beside her (an unobserved meeting), and a modest weekly
// roll lands. This is the hook the wife-to-lover bond growth, the secret-affair discovery curve, and the later
// pregnancy-by-the-lover build on. Get it wrong and trysts fire while the player rides at her side (she meets
// her lover under the husband's nose, which the design forbids), or never fire at all (the whole 3b/3c line is
// dead), or fire for a partner with no lover (a tryst with no one).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class TrystPolicyTests
    {
        // The core precondition: a tryst is a meeting with a LOVER, so with no lover taken there is no one to
        // meet. Without this gate the weekly pass would try to bond a partner to a null lover and record a
        // meeting that never happened.
        [Test]
        public void GIVEN_a_partner_with_no_lover_WHEN_asked_THEN_no_tryst()
        {
            TrystPolicy.ShouldMeet(hasLover: false, playerIsAway: true, rollPercent: 0)
                       .Should().BeFalse("a tryst is a meeting with a lover she has not taken");
        }

        // The privacy rule that defines a tryst: it happens when the player is AWAY. If it fired while the
        // player was beside her, she would be meeting her lover under her husband's nose, contradicting the
        // whole "she meets him when the player is away" design and pre-empting the discovery mechanic.
        [Test]
        public void GIVEN_the_player_is_beside_her_WHEN_asked_THEN_no_tryst()
        {
            TrystPolicy.ShouldMeet(hasLover: true, playerIsAway: false, rollPercent: 0)
                       .Should().BeFalse("she does not meet her lover while the player is with her");
        }

        // The boundary the mod's MBRandom roll is compared against: a roll UNDER the chance means they meet, a
        // roll AT or ABOVE it means they do not. Pins the strictly-less comparison so the ChancePercent knob
        // actually governs the frequency rather than an off-by-one letting every week (or no week) through.
        [Test]
        public void GIVEN_a_lover_and_the_player_away_WHEN_the_roll_lands_under_the_chance_THEN_they_meet()
        {
            TrystPolicy.ShouldMeet(hasLover: true, playerIsAway: true, rollPercent: TrystPolicy.ChancePercent - 1)
                       .Should().BeTrue("a roll below the chance means a tryst happens this week");
            TrystPolicy.ShouldMeet(hasLover: true, playerIsAway: true, rollPercent: TrystPolicy.ChancePercent)
                       .Should().BeFalse("a roll at the chance is outside it (strictly-less comparison)");
        }
    }
}
