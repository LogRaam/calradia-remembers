// Code written by Gabriel Mailhot, 16/10/2026.
// How the world's record of a battle names its sides, its scale, and how far word of it carries. fkasad (Nexus,
// 16/10/2026): "defeated a host" said nothing of who lost, a band of looters read like an army, and a field battle
// had no place. Wrong one way, rumour calls a police action against looters a battle and every lord around repeats
// it; wrong the other way, a great battle carries no further than a skirmish, or the loser stays nameless when the
// game knew who it was.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BattleRecordTextTests
    {
        // STAKE: the reported case; beaten looters are named for what they were, not "a host".
        [Test]
        public void GIVEN_a_bandit_band_with_no_hero_WHEN_the_side_is_named_THEN_it_is_a_band_of_that_kind()
        {
            BattleRecordText.Side(new BattleSideFacts {PartyName = "Looters", IsBandit = true}).Should().Be("a band of Looters");
            BattleRecordText.Side(new BattleSideFacts {IsBandit = true}).Should().Be("a band of outlaws");
        }

        // STAKE: a leaderless side of a realm is named by its realm, so its own people know whose men fell.
        [Test]
        public void GIVEN_a_side_of_a_realm_with_no_hero_WHEN_the_side_is_named_THEN_it_is_men_of_that_realm()
        {
            BattleRecordText.Side(new BattleSideFacts {PartyName = "Garrison", FactionName = "Vlandia"}).Should().Be("men of Vlandia");
        }

        // STAKE: a side a hero led keeps his name; "a host" stays only for a side nothing is known of.
        [Test]
        public void GIVEN_a_side_led_by_a_hero_or_unknown_WHEN_the_side_is_named_THEN_the_hero_is_named_and_only_nothing_reads_a_host()
        {
            BattleRecordText.Side(new BattleSideFacts {LeaderName = "Roland of Montmirail", FactionName = "Vlandia"}).Should().Be("Roland of Montmirail");
            BattleRecordText.Side(new BattleSideFacts()).Should().Be("a host");
            BattleRecordText.Side(null).Should().Be("a host");
        }

        // STAKE: the model cannot tell a skirmish from a great battle unless the line says so.
        [Test]
        public void GIVEN_the_men_on_the_field_WHEN_the_scale_is_said_THEN_it_runs_from_a_skirmish_to_a_great_battle()
        {
            BattleRecordText.Scale(40).Should().Be("a skirmish of a few dozen men");
            BattleRecordText.Scale(150).Should().Be("a clash of a hundred or two");
            BattleRecordText.Scale(500).Should().Be("a battle of several hundred men");
            BattleRecordText.Scale(1200).Should().Be("a great battle of over a thousand men");
            BattleRecordText.Scale(3000).Should().Be("a great battle of thousands");
            BattleRecordText.Scale(0).Should().BeEmpty();
        }

        // STAKE: the full line names who, where and how big, and still reads when the place or the size is unknown.
        [Test]
        public void GIVEN_a_battle_WHEN_the_line_is_written_THEN_it_names_the_sides_the_place_and_the_scale()
        {
            BattleRecordText.Summary(new BattleRecordNames {Winner = "Roland of Montmirail", Loser = "a band of Looters", PlaceClause = " near Ortysia", MenAtStart = 45})
                            .Should().Be("Roland of Montmirail defeated a band of Looters near Ortysia, a skirmish of a few dozen men.");
            BattleRecordText.Summary(new BattleRecordNames {Winner = "Roland of Montmirail", Loser = "men of Vlandia", PlaceClause = "", MenAtStart = 0})
                            .Should().Be("Roland of Montmirail defeated men of Vlandia in battle.");
        }

        // STAKE: a police action carries less far than a field battle, a great battle further; a fight over a
        // settlement still carries further than one in the field.
        [Test]
        public void GIVEN_the_size_and_the_place_WHEN_the_magnitude_is_set_THEN_size_moves_it_both_ways()
        {
            BattleRecordText.Magnitude(false, 300).Should().Be(35);
            BattleRecordText.Magnitude(true, 300).Should().Be(45);
            BattleRecordText.Magnitude(false, 40).Should().Be(25);
            BattleRecordText.Magnitude(true, 1500).Should().Be(55);
            BattleRecordText.Magnitude(false, 0).Should().Be(35);
        }
    }
}
