// Code written by Gabriel Mailhot, 22/06/2026.
// Guards dynastic succession's two pure rules: the predecessor's private history does NOT cross to the
// heir (but their political/relational history does), and the kinship word is chosen correctly.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SuccessionRulesTests
    {
        // ── Event inheritance ───────────────────────────────────────────────

        [TestCase(NotableEventType.Flirt)]
        [TestCase(NotableEventType.Intimacy)]
        [TestCase(NotableEventType.Captivity)]
        [TestCase(NotableEventType.Jealousy)]
        public void GIVEN_a_private_event_WHEN_succeeding_THEN_it_does_not_cross_to_the_heir(NotableEventType type)
        {
            SuccessionMemoryPolicy.SurvivesSuccession(type).Should().BeFalse();
        }

        [TestCase(NotableEventType.FirstMeeting)]
        [TestCase(NotableEventType.Conflict)]
        [TestCase(NotableEventType.Collaboration)]
        [TestCase(NotableEventType.Agreement)]
        [TestCase(NotableEventType.Betrayal)]
        [TestCase(NotableEventType.Confrontation)]
        [TestCase(NotableEventType.Other)]
        public void GIVEN_a_political_or_relational_event_WHEN_succeeding_THEN_the_heir_inherits_it(NotableEventType type)
        {
            SuccessionMemoryPolicy.SurvivesSuccession(type).Should().BeTrue();
        }

        // ── Kinship label ───────────────────────────────────────────────────

        [Test]
        public void GIVEN_the_predecessor_was_a_parent_WHEN_labelling_THEN_it_is_father_or_mother_by_sex()
        {
            SuccessionKinship.Label(predecessorWasParent: true, false, false, predecessorIsFemale: false).Should().Be("father");
            SuccessionKinship.Label(predecessorWasParent: true, false, false, predecessorIsFemale: true).Should().Be("mother");
        }

        [Test]
        public void GIVEN_a_sibling_or_spouse_WHEN_labelling_THEN_the_right_word_is_used()
        {
            SuccessionKinship.Label(false, predecessorWasSibling: true, false, predecessorIsFemale: true).Should().Be("sister");
            SuccessionKinship.Label(false, predecessorWasSibling: true, false, predecessorIsFemale: false).Should().Be("brother");
            SuccessionKinship.Label(false, false, predecessorWasSpouse: true, false).Should().Be("spouse");
        }

        [Test]
        public void GIVEN_no_known_relation_WHEN_labelling_THEN_it_falls_back_to_kin()
        {
            SuccessionKinship.Label(false, false, false, false).Should().Be("kin");
        }

        [Test]
        public void GIVEN_several_relations_WHEN_labelling_THEN_parent_takes_precedence()
        {
            SuccessionKinship.Label(predecessorWasParent: true, predecessorWasSibling: true, predecessorWasSpouse: true, predecessorIsFemale: false)
                .Should().Be("father");
        }
    }
}
