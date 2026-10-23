// Code written by Gabriel Mailhot, 23/10/2026.
// CombatTallyText words a hero's own part in a fight for their memory. chmidt (Nexus, 23/10/2026): companions "can't
// remember the last battle you fought with them, how many kills they had or if they were downed." If this is wrong, a
// companion says nothing of their own deeds, a foe the campaign spared is called slain, a tournament reads as a
// slaughter, or a line names every man of rank a hero ever unhorsed.

#region

using CalradiaRemembers.Logic.Battles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CombatTallyTextTests
   {
      // STAKE: chmidt: how many kills, and whether they were downed, both in one sentence of their own.
      [Test]
      public void GIVEN_a_companion_who_felled_men_and_was_struck_down_WHEN_worded_THEN_both_are_told()
      {
         string line = CombatTallyText.OwnPart(new CombatTally {TroopsFelled = 7, WasDowned = true, DownedBy = "Ira of the fen Uvain"});

         line.Should().Be("My own part: I cut down seven of their men, before Ira of the fen Uvain struck me down.");
      }

      // STAKE: a foe of name is told by name; one the campaign spared is "bested", never slain.
      [Test]
      public void GIVEN_a_foe_of_name_the_campaign_spared_WHEN_worded_THEN_he_is_bested()
      {
         string line = CombatTallyText.OwnPart(new CombatTally {TroopsFelled = 3, HeroesDowned = new[] {new TalliedFoe {Name = "Lucon"}}});

         line.Should().Be("My own part: I cut down three of their men and bested Lucon.");
      }

      // STAKE: a foe the campaign says died of it was slain.
      [Test]
      public void GIVEN_a_foe_who_died_of_it_WHEN_worded_THEN_he_was_slain()
      {
         CombatTallyText.OwnPart(new CombatTally {HeroesDowned = new[] {new TalliedFoe {Name = "Lucon", Died = true}}})
                        .Should().Be("My own part: I slew Lucon.");
      }

      // STAKE: a line names two foes at most and counts the rest, or a hero's memory becomes a roll call.
      [Test]
      public void GIVEN_many_foes_of_name_WHEN_worded_THEN_two_are_named_and_the_rest_counted()
      {
         CombatTallyText.OwnPart(new CombatTally {HeroesDowned = new[] {new TalliedFoe {Name = "A"}, new TalliedFoe {Name = "B"}, new TalliedFoe {Name = "C"}, new TalliedFoe {Name = "D"}}})
                        .Should().Be("My own part: I bested A and B and two more of name besides.");
      }

      // STAKE: downed by no one of name still says so.
      [Test]
      public void GIVEN_a_hero_only_struck_down_WHEN_worded_THEN_the_fall_alone_is_told()
      {
         CombatTallyText.OwnPart(new CombatTally {WasDowned = true}).Should().Be("My own part: I was struck down.");
      }

      // STAKE: a hero who did nothing of note and stayed standing adds nothing to their memory.
      [Test]
      public void GIVEN_nothing_of_note_WHEN_worded_THEN_nothing_is_added()
      {
         CombatTallyText.OwnPart(new CombatTally()).Should().BeEmpty();
         CombatTallyText.OwnPart(null).Should().BeEmpty();
      }

      // STAKE: an auto-resolved battle has no mission; only the wound the campaign knows of is told, never invented kills.
      [Test]
      public void GIVEN_an_auto_resolved_battle_WHEN_a_wounded_companion_remembers_THEN_only_the_wound_is_told()
      {
         CombatTallyText.CameOutWounded().Should().Be("My own part: I came out of it wounded.");
      }

      // STAKE: no one is cut down or slain in the lists; a tournament is told as one.
      [Test]
      public void GIVEN_a_tournament_WHEN_worded_THEN_it_speaks_of_the_lists()
      {
         CombatTallyText.OwnPart(new CombatTally {TroopsFelled = 2, HeroesDowned = new[] {new TalliedFoe {Name = "Lucon", Died = true}}, WasDowned = true, DownedBy = "Ira"}, arena: true)
                        .Should().Be("My own part: I put two opponents out of the lists and bested Lucon, before Ira put me out of the lists.");
      }
   }
}
