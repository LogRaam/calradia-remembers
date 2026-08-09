// Code written by Gabriel Mailhot, 09/08/2026.
// Guards OpenRelationshipPolicy.SuppressesJealousy, the single pure rule both jealousy paths consult before
// letting a partner react: the daily competition loop (JealousyBehavior.EvaluateCompetition, which excludes
// an open partner from the aggrieved selection) and the event-triggered reaction (JealousyBehavior.FireReaction,
// which never fires for an open partner). Player report: a player whose several wives agreed, in conversation,
// to a harmonious no-jealousy arrangement still got jealousy pop-ups. The fix models an OPEN RELATIONSHIP: a
// partner who holds reciprocal open terms with the player is not jealous of the player's other loves. Get this
// wrong and either the open pact is ignored (the reported bug returns) or an ordinary partner's genuine
// grievance is silently swallowed.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class OpenRelationshipPolicyTests
    {
        // The whole point of Inc 2: a partner who and the player hold reciprocal open terms is not jealous of
        // the player's other loves. Without this the reported no-jealousy pact is never honoured and the open
        // partner keeps generating competition and event-triggered jealousy she has explicitly renounced.
        [Test]
        public void GIVEN_an_open_arrangement_WHEN_asking_if_jealousy_is_suppressed_THEN_it_is()
        {
            OpenRelationshipPolicy.SuppressesJealousy(isOpenArrangement: true)
                                  .Should().BeTrue("a partner on reciprocal open terms accepts the player's other loves");
        }

        // The mirror case, and the more dangerous one to get wrong: a partner who has NOT agreed to open terms
        // must keep her full jealousy. If suppression leaked to the default (non-open) partner, every partner
        // would silently stop reacting and the whole jealousy system would go dark for players who never opened
        // their bond at all.
        [Test]
        public void GIVEN_no_open_arrangement_WHEN_asking_if_jealousy_is_suppressed_THEN_it_is_not()
        {
            OpenRelationshipPolicy.SuppressesJealousy(isOpenArrangement: false)
                                  .Should().BeFalse("an ordinary partner still feels favoured or slighted");
        }
    }
}
