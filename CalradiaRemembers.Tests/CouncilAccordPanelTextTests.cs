// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 3: these lines are what the PLAYER reads on a hero's Encyclopedia page ("Owes you a tribute..."),
// the third-person mirror of the first-person prompt clauses. What breaks if a line is wrong: the page tells the
// player the opposite of the truth. As with the prompt describer, DIRECTION is the load-bearing fact (a hero that
// OWES a tribute must not read as RECEIVING one; a ward's mentor and protege sides must not swap), so most tests
// pin it. The remaining-term wording is pinned too so a live accord is never shown as "(0 days remain)".

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CouncilAccordPanelTextTests
    {
        // The page is read by the player, so a tribute the hero PAYS must read "Owes you", never "Receives from
        // you" (that is the stipend line). Swapping them tells the player the coffers flow the wrong way.
        [Test]
        public void GIVEN_a_tribute_THEN_the_line_reads_that_the_hero_owes_the_player()
        {
            string line = CouncilAccordPanelText.TributeOwed(300, 12, 0);

            line.Should().StartWith("Owes you a tribute");
            line.Should().Contain("300");
            line.Should().Contain("(12 days remain)");
        }

        // The opposite direction: a stipend is the player paying the hero, so the line must read "Receives ... from
        // you". Sharing wording with the tribute line would blur who funds whom on the page.
        [Test]
        public void GIVEN_a_stipend_THEN_the_line_reads_that_the_hero_receives_it_from_the_player()
        {
            string line = CouncilAccordPanelText.StipendReceived(200, 20);

            line.Should().StartWith("Receives a stipend");
            line.Should().Contain("from you");
            line.Should().Contain("200");
        }

        // The MissedDays note must appear only when the hero actually has fallen behind, or the page slanders a
        // payer who is current on their tribute.
        [Test]
        public void GIVEN_a_tribute_with_missed_days_THEN_the_line_notes_falling_behind()
        {
            CouncilAccordPanelText.TributeOwed(300, 12, 4).Should().Contain("fallen behind");
        }

        [Test]
        public void GIVEN_a_tribute_with_no_missed_days_THEN_the_line_does_not_note_falling_behind()
        {
            CouncilAccordPanelText.TributeOwed(300, 12, 0).Should().NotContain("fallen behind");
        }

        // A pact is a mutual truce, framed as a bond, not a debt one side carries.
        [Test]
        public void GIVEN_a_pact_THEN_the_line_frames_a_non_aggression_pact()
        {
            CouncilAccordPanelText.Pact(30).Should().Contain("non-aggression pact");
        }

        // The two hostage directions must not swap: a hostage the player HOLDS versus a hostage this house GAVE.
        [Test]
        public void GIVEN_the_hero_is_held_hostage_THEN_the_line_reads_held_by_you()
        {
            CouncilAccordPanelText.HeldAsHostage(40).Should().StartWith("Held by you");
        }

        [Test]
        public void GIVEN_the_hero_gave_a_hostage_THEN_the_line_names_who_was_given()
        {
            CouncilAccordPanelText.HostageGiven("Ira", 40).Should().Contain("Gave Ira to you");
        }

        // The two ward sides must not swap: a protege is fostered (mentored BY someone); a mentor teaches (mentors
        // someone). Confusing them would show a child as the teacher of their own tutor.
        [Test]
        public void GIVEN_the_hero_is_a_ward_protege_THEN_the_line_names_their_mentor()
        {
            CouncilAccordPanelText.WardAsProtege("Rhagaea").Should().Contain("mentored by Rhagaea");
        }

        [Test]
        public void GIVEN_the_hero_is_a_ward_mentor_THEN_the_line_names_their_protege()
        {
            CouncilAccordPanelText.WardAsMentor("Ira").Should().Contain("Mentors Ira");
        }

        // Unresolved names (a stale StringId after a save) must degrade to readable phrases, never leave a gap.
        [Test]
        public void GIVEN_unresolved_names_THEN_the_lines_fall_back_to_readable_phrases()
        {
            CouncilAccordPanelText.HostageGiven("", 40).Should().Contain("one of their kin");
            CouncilAccordPanelText.WardAsProtege("").Should().Contain("one of your clan");
            CouncilAccordPanelText.WardAsMentor("").Should().Contain("a young companion");
        }

        // A ward has NO term (an indefinite bond), so its lines must never carry a "(N days remain)" note, and any
        // termed accord whose term is spent or unknown must drop the parenthetical rather than show "(0 days...)".
        [Test]
        public void GIVEN_a_ward_or_a_spent_term_THEN_no_days_parenthetical_is_shown()
        {
            CouncilAccordPanelText.WardAsMentor("Ira").Should().NotContain("remain");
            CouncilAccordPanelText.StipendReceived(200, 0).Should().NotContain("remain");
        }

        // The remaining-term note is singular-aware: "1 day remains" not "1 days remain".
        [TestCase(5, "(5 days remain)")]
        [TestCase(1, "(1 day remains)")]
        public void GIVEN_a_positive_term_THEN_the_line_states_it_singular_aware(int days, string expected)
        {
            CouncilAccordPanelText.StipendReceived(200, days).Should().Contain(expected);
        }
    }
}
