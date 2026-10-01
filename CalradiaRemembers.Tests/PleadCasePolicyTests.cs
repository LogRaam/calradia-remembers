// Code written by Gabriel Mailhot, 01/10/2026.
// Lordfadooboo (Nexus, 01/10/2026): "a lord asking you to argue on their behalf to their family leader or faction
// leader". The plea is done when the person named is truly won over toward the giver, in their own conversation. If
// these rules are wrong, a plea completes when the player turned someone AGAINST the giver, or when he persuaded the
// wrong person, or about the wrong lord; or a lord sends the player to plead before a child or a dead man.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PleadCasePolicyTests
    {
        private static readonly EarnRegardTargetFacts LivingAdult = new EarnRegardTargetFacts {IsInTheWorld = true, IsAdult = true};

        // ── Before whom ───────────────────────────────────────────────────────────────────────────

        // "Their family leader or faction leader": a lord, or a notable, holds a court a case can be made at.
        [Test]
        public void GIVEN_a_living_adult_who_holds_a_court_WHEN_named_THEN_the_plea_may_be_asked()
        {
            PleadCasePolicy.MayBePleadedTo(LivingAdult, holdsCourt: true).Should().BeTrue();
        }

        // A wanderer or a soldier is never taught to be swayed, so a plea before them could never complete.
        [Test]
        public void GIVEN_someone_who_holds_no_court_WHEN_named_THEN_no_such_plea_exists()
        {
            PleadCasePolicy.MayBePleadedTo(LivingAdult, holdsCourt: false).Should().BeFalse();
        }

        // The same third-person rule as earn_regard: not the dead, not a child, not the giver, not the player.
        [Test]
        public void GIVEN_the_dead_a_child_or_the_giver_WHEN_named_THEN_no_such_plea_exists()
        {
            PleadCasePolicy.MayBePleadedTo(new EarnRegardTargetFacts {IsInTheWorld = false, IsAdult = true}, true).Should().BeFalse();
            PleadCasePolicy.MayBePleadedTo(new EarnRegardTargetFacts {IsInTheWorld = true, IsAdult = false}, true).Should().BeFalse();
            PleadCasePolicy.MayBePleadedTo(new EarnRegardTargetFacts {IsInTheWorld = true, IsAdult = true, IsTheGiver = true}, true).Should().BeFalse();
        }

        // ── When it is done ───────────────────────────────────────────────────────────────────────

        // The khan, persuaded, warms toward the lord who sent the player: the plea is answered.
        [Test]
        public void GIVEN_the_named_person_won_over_toward_the_giver_WHEN_the_sway_lands_THEN_the_plea_is_answered()
        {
            PleadCasePolicy.AnswersThePlea(new SwayFacts {PersuadedId = "khan", AboutId = "giver", Warmer = true}, "khan", "giver").Should().BeTrue();
        }

        // Turning the khan AGAINST the giver is the opposite of the errand, however it was argued.
        [Test]
        public void GIVEN_the_named_person_turned_against_the_giver_WHEN_the_sway_lands_THEN_the_plea_is_not_answered()
        {
            PleadCasePolicy.AnswersThePlea(new SwayFacts {PersuadedId = "khan", AboutId = "giver", Warmer = false}, "khan", "giver").Should().BeFalse();
        }

        // Persuading someone else, or warming the khan toward somebody else, is not the plea that was asked.
        [Test]
        public void GIVEN_the_wrong_person_or_the_wrong_subject_WHEN_the_sway_lands_THEN_the_plea_is_not_answered()
        {
            PleadCasePolicy.AnswersThePlea(new SwayFacts {PersuadedId = "cousin", AboutId = "giver", Warmer = true}, "khan", "giver").Should().BeFalse();
            PleadCasePolicy.AnswersThePlea(new SwayFacts {PersuadedId = "khan", AboutId = "rival", Warmer = true}, "khan", "giver").Should().BeFalse();
        }
    }
}
