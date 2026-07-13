// Code written by Gabriel Mailhot, 22/06/2026.
// Guards dynastic succession's two pure rules: the predecessor's private history does NOT cross to the
// heir (but their political/relational history does), and the kinship word is chosen correctly.
//
// WHY IT MATTERS: SuccessionBehavior.Apply calls SurvivesSuccession to prune every stored NPC profile the
// moment the player dies and an heir takes over, and calls SuccessionKinship.Label to word the line the prompt
// then narrates ("your father helped me at the siege..."). Per SuccessionMemoryPolicy's own header, a leak here
// would have a lord recall the late ruler's love affairs, captivity, or jealousy to their child, exactly the
// immersion break this guards against; a wrong kinship word reads as a plain narrative error to the heir.

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

        // These four event types are the predecessor's private history. Left uninherited on purpose: a leak
        // would have an NPC recall the late ruler's flirtations, intimacy, captivity, or jealousy to their
        // own child, which SuccessionMemoryPolicy's header calls out as exactly the immersion break to avoid.
        [TestCase(NotableEventType.Flirt)]
        [TestCase(NotableEventType.Intimacy)]
        [TestCase(NotableEventType.Captivity)]
        [TestCase(NotableEventType.Jealousy)]
        public void GIVEN_a_private_event_WHEN_succeeding_THEN_it_does_not_cross_to_the_heir(NotableEventType type)
        {
            SuccessionMemoryPolicy.SurvivesSuccession(type).Should().BeFalse();
        }

        // The opposite side of the same line: these must SURVIVE. If they didn't, the heir would start their
        // reign with none of the predecessor's standing, debts, or grudges, erasing the weight of everything
        // that happened in the previous reign the moment succession occurs.
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

        // The label feeds straight into the narrated line ("your father helped me at that siege..."). Getting
        // the predecessor's sex wrong here reads as a plain, jarring narrative error to the player, not a
        // subtle bug.
        [Test]
        public void GIVEN_the_predecessor_was_a_parent_WHEN_labelling_THEN_it_is_father_or_mother_by_sex()
        {
            SuccessionKinship.Label(predecessorWasParent: true, false, false, predecessorIsFemale: false).Should().Be("father");
            SuccessionKinship.Label(predecessorWasParent: true, false, false, predecessorIsFemale: true).Should().Be("mother");
        }

        // Not every succession is parent-to-child: a sibling or spouse can inherit the clan too, and each needs
        // its own correct word in the narrated line rather than falling back to a generic label.
        [Test]
        public void GIVEN_a_sibling_or_spouse_WHEN_labelling_THEN_the_right_word_is_used()
        {
            SuccessionKinship.Label(false, predecessorWasSibling: true, false, predecessorIsFemale: true).Should().Be("sister");
            SuccessionKinship.Label(false, predecessorWasSibling: true, false, predecessorIsFemale: false).Should().Be("brother");
            SuccessionKinship.Label(false, false, predecessorWasSpouse: true, false).Should().Be("spouse");
        }

        // An heir need not be a parent, sibling, or spouse (a cousin inheriting the clan, say). The label must
        // still resolve to a safe generic word instead of returning null or an empty string into the prompt.
        [Test]
        public void GIVEN_no_known_relation_WHEN_labelling_THEN_it_falls_back_to_kin()
        {
            SuccessionKinship.Label(false, false, false, false).Should().Be("kin");
        }

        // Pins the precedence order explicitly (parent, then sibling, then spouse) for the rare edge case where
        // more than one relation is true at once, so a future reorder of the if-chain can't silently flip which
        // word wins.
        [Test]
        public void GIVEN_several_relations_WHEN_labelling_THEN_parent_takes_precedence()
        {
            SuccessionKinship.Label(predecessorWasParent: true, predecessorWasSibling: true, predecessorWasSpouse: true, predecessorIsFemale: false)
                .Should().Be("father");
        }
    }
}
