// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 2: these clauses are injected verbatim into an NPC's 1:1 prompt so the character can reference a
// still-standing council accord ("the tribute I owe you"). What breaks if a clause is wrong: the NPC contradicts
// the real ledger to the player's face. The single most dangerous error is DIRECTION, an NPC told it RECEIVES a
// tribute it actually PAYS, or that it was GRANTED a stipend it in fact grants, so most tests here pin who owes
// whom. The remaining-term wording is pinned too, since "1 day remains" vs "5 days remain" is the one grammatical
// seam, and a stray "(0 days remain)" would read as a live accord that has actually lapsed.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CouncilAccordClauseTests
    {
        // Direction is the load-bearing fact: a tribute is the NPC paying the player, so the clause must read "You
        // owe", never "you receive". Getting this backwards has the NPC deny a debt it actually carries.
        [Test]
        public void GIVEN_a_tribute_THEN_the_clause_says_the_NPC_owes_the_player_the_daily_amount()
        {
            string clause = CouncilAccordClause.TributeOwedToPlayer(new CouncilAccordClause.TributeToPlayer {PlayerName = "Rhagaea", AmountPerDay = 300, DaysRemaining = 12, MissedDays = 0});

            clause.Should().Contain("You owe");
            clause.Should().Contain("Rhagaea");
            clause.Should().Contain("300");
            clause.Should().Contain("tribute");
        }

        // The MissedDays hook lets the character speak to their own default ("I have fallen behind"); it must appear
        // only when they actually have, or the NPC would apologise for a lapse that never happened.
        [Test]
        public void GIVEN_a_tribute_with_missed_days_THEN_the_clause_notes_falling_behind()
        {
            CouncilAccordClause.TributeOwedToPlayer(new CouncilAccordClause.TributeToPlayer {PlayerName = "Rhagaea", AmountPerDay = 300, DaysRemaining = 12, MissedDays = 3}).Should().Contain("fallen behind");
        }

        [Test]
        public void GIVEN_a_tribute_with_no_missed_days_THEN_the_clause_does_not_note_falling_behind()
        {
            CouncilAccordClause.TributeOwedToPlayer(new CouncilAccordClause.TributeToPlayer {PlayerName = "Rhagaea", AmountPerDay = 300, DaysRemaining = 12, MissedDays = 0}).Should().NotContain("fallen behind");
        }

        // The opposite direction: a stipend is the player paying the NPC, so it must read as a grant TO the NPC.
        // Sharing wording with the tribute clause would blur who funds whom.
        [Test]
        public void GIVEN_a_stipend_THEN_the_clause_says_the_player_grants_it_to_the_NPC()
        {
            string clause = CouncilAccordClause.StipendGrantedByPlayer("Rhagaea", 200, 20);

            clause.Should().Contain("grants you");
            clause.Should().Contain("Rhagaea");
            clause.Should().Contain("200");
        }

        // A pact is mutual and faction-level; the clause must frame it as standing between the two houses, not as a
        // debt one side owes, so the NPC treats it as a truce rather than an obligation to discharge.
        [Test]
        public void GIVEN_a_pact_THEN_the_clause_frames_a_non_aggression_pact_between_the_houses()
        {
            string clause = CouncilAccordClause.PactWithPlayer("Rhagaea", 30);

            clause.Should().Contain("non-aggression pact");
            clause.Should().Contain("your house");
            clause.Should().Contain("Rhagaea");
        }

        // The two hostage directions must not be confused: the held hostage speaks of being held; the pledger speaks
        // of having GIVEN someone. A swap would have a captive claim to be the captor's benefactor.
        [Test]
        public void GIVEN_the_NPC_is_the_hostage_THEN_the_clause_says_they_are_held_by_the_player()
        {
            CouncilAccordClause.HeldAsHostageByPlayer("Rhagaea", 40).Should().Contain("You are held by Rhagaea");
        }

        [Test]
        public void GIVEN_the_NPC_pledged_a_hostage_THEN_the_clause_names_who_their_house_gave()
        {
            string clause = CouncilAccordClause.HostageGivenToPlayer("Rhagaea", "Ira", 40);

            clause.Should().Contain("Your house has given Ira");
            clause.Should().Contain("Rhagaea");
        }

        // A blank hostage name (the hero could not be resolved) must degrade to a readable phrase, not an empty gap
        // that reads as "given  to Rhagaea".
        [Test]
        public void GIVEN_an_unresolved_hostage_name_THEN_the_clause_falls_back_to_a_readable_phrase()
        {
            CouncilAccordClause.HostageGivenToPlayer("Rhagaea", "", 40).Should().Contain("one of your kin");
        }

        // A blank player name (should never happen, but the clause is injected raw) must not leave a dangling gap.
        [Test]
        public void GIVEN_a_blank_player_name_THEN_the_clause_falls_back_to_the_player()
        {
            CouncilAccordClause.StipendGrantedByPlayer("", 200, 20).Should().Contain("the player");
        }

        // The remaining-term note is singular-aware, and a spent or unknown term drops the parenthetical entirely so
        // a live accord is never labelled "(0 days remain)" as if already over.
        [TestCase(5, "(5 days remain)")]
        [TestCase(1, "(1 day remains)")]
        public void GIVEN_a_positive_remaining_term_THEN_the_clause_states_it_singular_aware(int days, string expected)
        {
            CouncilAccordClause.StipendGrantedByPlayer("Rhagaea", 200, days).Should().Contain(expected);
        }

        [Test]
        public void GIVEN_a_spent_remaining_term_THEN_the_clause_carries_no_days_parenthetical()
        {
            CouncilAccordClause.StipendGrantedByPlayer("Rhagaea", 200, 0).Should().NotContain("remain");
        }
    }
}
