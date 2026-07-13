// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (2026-07-10): after marrying the mother of his child, a player kept getting child-support
// demand letters from her while she rode in his own party, escalating all the way to a divorce for "unpaid
// support". Two rulings fixed here: the player's own spouse never duns him for their shared child (kept in the
// household, not a courier debt), and nobody riding beside the player writes ANY birth letter at all (the game
// already shows the birth in person). LetterTriggerEvaluator gates both the announcement and the support
// request on these two booleans; get either wrong and the divorce-for-unpaid-support spiral returns.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BirthLetterPolicyTests
    {
        // A courier letter announcing a birth the player just watched happen beside him (or the game's own
        // birth notice already covers) would be absurd; presence alone must suppress the letter.
        [Test]
        public void GIVEN_the_mother_rides_with_the_player_WHEN_a_child_is_born_THEN_no_announcement_letter_is_sent()
        {
            BirthLetterPolicy.ShouldSendAnnouncement(true).Should().BeFalse();
        }

        // The mirror case: a genuinely absent mother must still get her fond note out ("you have a son"), or the
        // presence guard above would over-fire and silently swallow every birth announcement, present or not.
        [Test]
        public void GIVEN_the_mother_is_far_from_the_player_WHEN_a_child_is_born_THEN_a_fond_announcement_is_sent()
        {
            BirthLetterPolicy.ShouldSendAnnouncement(false).Should().BeTrue();
        }

        // The exact bug from the player report: the player's own wife, mother of a shared legitimate child,
        // must never send a support demand even while she is away from him, or the divorce-for-unpaid-support
        // spiral this ruling closed reopens.
        [Test]
        public void GIVEN_the_players_own_spouse_gives_birth_apart_WHEN_deciding_support_THEN_she_never_dun_him()
        {
            BirthLetterPolicy.ShouldRequestChildSupport(false, true).Should().BeFalse();
        }

        // Pins the opposite side of the legitimacy boundary: an actual out-of-wedlock mother apart from the
        // player is the one real case this request exists for, so the fix above must not have swallowed her too.
        [Test]
        public void GIVEN_a_child_born_outside_marriage_and_the_mother_apart_WHEN_deciding_support_THEN_she_may_ask()
        {
            BirthLetterPolicy.ShouldRequestChildSupport(false, false).Should().BeTrue();
        }

        // No mother sends a courier to someone travelling beside her (production doc): presence must suppress
        // the support demand regardless of legitimacy, so this is checked independently of the marital-status
        // gate proven by the two tests above.
        [Test]
        public void GIVEN_the_mother_rides_with_the_player_WHEN_deciding_support_THEN_she_never_writes_for_it()
        {
            // Beside him, whether the child is legitimate or not, no support demand goes out by courier.
            BirthLetterPolicy.ShouldRequestChildSupport(true, false).Should().BeFalse();
            BirthLetterPolicy.ShouldRequestChildSupport(true, true).Should().BeFalse();
        }
    }
}
