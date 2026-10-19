// Code written by Gabriel Mailhot, 08/08/2026.
// Pins the "fought beside you" line and the place/engagement phrasing WorldEventFeeders writes into profiles and
// the chronicle. A player reported (08/08/2026) that party companions never got the memory allied lords did; the
// fix routes both through the single AllySummary string. A second player report (17/08/2026): a SIEGE was recorded
// as an "open battle near" the settlement, and a battle fought as part of an army read as a solo win. So the place
// clause is now setting-aware and the ally line reflects a shared effort. These pin all of that wording.
// tashmetu (Nexus, 13/10/2026, via cr.prompt_dump): "Fought at Can's side when they broke the enemy" left "they"
// reading as either the player or the army. The solo branches (sharedEffort: false) now name the one who broke
// the enemy explicitly instead of "they", so the existing pins below changed with it.

#region

using CalradiaRemembers.Logic.Battles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BattleMemoryTextTests
    {
        // WHOSE "YOU" (reminensce, Nexus, 19/09/2026). These lines are read under "YOUR HISTORY WITH THIS PLAYER",
        // in a prompt whose every other "you" is the character themselves, and a lord took the player's defeat for
        // his own. They name the player now. "We ... together" survives the change: a shared victory is still
        // shared, and "we" cannot be mistaken for either party alone.
        [Test]
        public void GIVEN_no_player_name_WHEN_writing_the_ally_line_THEN_it_falls_back_to_naming_them_the_player()
        {
            BattleMemoryText.AllySummary(playerWon: true, placeClause: "", sharedEffort: false)
                .Should().Be("Fought at the player's side when the player broke the enemy.")
                .And.NotContain("your");
        }

        // The victory wording is what earns the Respect stance bump alongside it; if this phrase drifts,
        // a companion's own memory of a won battle would read wrong even though the stance still moved.
        [Test]
        public void GIVEN_a_won_solo_battle_WHEN_writing_the_ally_line_THEN_it_credits_the_player()
        {
            BattleMemoryText.AllySummary(playerWon: true, placeClause: "", sharedEffort: false, playerName: "Adam")
                .Should().Be("Fought at Adam's side when Adam broke the enemy.");
        }

        // A loss must still leave a "stood with you" memory, not silence: fighting a losing battle
        // together is exactly the shared hardship a companion should remember, so the branch is pinned.
        [Test]
        public void GIVEN_a_lost_solo_battle_WHEN_writing_the_ally_line_THEN_it_reads_as_standing_together()
        {
            BattleMemoryText.AllySummary(playerWon: false, placeClause: "", sharedEffort: false, playerName: "Adam")
                .Should().Be("Fought at Adam's side when Adam stood against the enemy.");
        }

        // The place clause arrives pre-shaped (leading space, no trailing period) so it slots in before the
        // sentence's own period; this fixes that the two pieces join without a double space or misplaced full stop.
        [Test]
        public void GIVEN_a_named_place_WHEN_writing_the_ally_line_THEN_the_place_clause_is_appended_cleanly()
        {
            BattleMemoryText.AllySummary(playerWon: true, placeClause: " near Pravend", sharedEffort: false, playerName: "Adam")
                .Should().Be("Fought at Adam's side when Adam broke the enemy near Pravend.");
        }

        // Player report 17/08/2026: a siege won as part of an army had lords congratulate a SOLO triumph. When the
        // effort was shared, the memory must read "we ... together", not "you", so it does not overcredit the player.
        [Test]
        public void GIVEN_a_shared_effort_win_WHEN_writing_the_ally_line_THEN_it_reads_as_a_joint_victory()
        {
            BattleMemoryText.AllySummary(playerWon: true, placeClause: " at the siege of Redwall", sharedEffort: true, playerName: "Adam")
                .Should().Be("Fought at Adam's side when we broke the enemy together at the siege of Redwall.");
        }

        // tashmetu (Nexus, 13/10/2026, via cr.prompt_dump): "Fought at Can's side when they broke the enemy" left
        // "they" meaning either the player or the army. Not shared means it was the named player alone, so the
        // subject is now named explicitly rather than left to a pronoun that could be misread either way.
        [Test]
        public void GIVEN_a_solo_win_WHEN_writing_the_ally_line_THEN_the_subject_who_broke_the_enemy_is_named_not_they()
        {
            string line = BattleMemoryText.AllySummary(playerWon: true, placeClause: "", sharedEffort: false, playerName: "Can");

            line.Should().Be("Fought at Can's side when Can broke the enemy.");
            line.Should().NotContain("they");
        }

        // The core of the siege fix: an assault is named as a siege, a raid as a raid, and only an actual field
        // battle beside a settlement reads "near". This is what stopped a siege being logged as an "open battle near".
        [Test]
        public void GIVEN_a_setting_WHEN_building_the_place_clause_THEN_it_names_the_engagement()
        {
            BattleMemoryText.PlaceClause(BattleSetting.Siege, "Redwall").Should().Be(" at the siege of Redwall");
            BattleMemoryText.PlaceClause(BattleSetting.Raid, "Ostican").Should().Be(" in the raid on Ostican");
            BattleMemoryText.PlaceClause(BattleSetting.OpenField, "Pravend").Should().Be(" near Pravend");
        }

        // No settlement means no place clause at all, so a pure field battle never gets a dangling preposition.
        [Test]
        public void GIVEN_no_settlement_WHEN_building_the_place_clause_THEN_it_is_empty()
        {
            BattleMemoryText.PlaceClause(BattleSetting.Siege, "").Should().BeEmpty();
            BattleMemoryText.PlaceClause(BattleSetting.OpenField, null).Should().BeEmpty();
        }

        // The chronicle wants a noun phrase, not a trailing clause: "the siege of X", "the raid on X", or "open
        // battle near X", falling back to plain "open battle" when no settlement is known.
        [Test]
        public void GIVEN_a_setting_WHEN_naming_the_chronicle_engagement_THEN_it_reads_as_a_noun_phrase()
        {
            BattleMemoryText.ChronicleEngagement(BattleSetting.Siege, "Redwall").Should().Be("the siege of Redwall");
            BattleMemoryText.ChronicleEngagement(BattleSetting.Raid, "Ostican").Should().Be("the raid on Ostican");
            BattleMemoryText.ChronicleEngagement(BattleSetting.OpenField, "Pravend").Should().Be("open battle near Pravend");
            BattleMemoryText.ChronicleEngagement(BattleSetting.OpenField, "").Should().Be("open battle");
        }
    }
}
