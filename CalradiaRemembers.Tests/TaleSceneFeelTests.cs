// Code written by Gabriel Mailhot, 07/10/2026.
// fkasad (Nexus, 07/10/2026), on written tales, ratified by Gabriel ("on fait aussi les idees de fkasad"):
// - a scene takes the player away from town, so the narration should show where they are: "an Aserai character scouting
//   a forest at night compared to a Battanian doing the same", "a Nord crawling through the sand", "a guy from Danustica
//   lost in the snow";
// - "last encounter difficulty should scale with rank/army size. Otherwise, there's no climax scene to end the quest";
// - war prisoners can be invited into a tale's scene; that should be the tale's choice, off unless a stage allows it.
// If these break, every scene reads the same whoever walks into it, a lord with two hundred men sweeps the climax aside,
// or a captive in chains strolls through the wood beside the player.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleSceneFeelTests
   {
      // fkasad's Aserai in a forest at night: the narrator is told the ground, the season, the hour, and who the player is.
      [Test]
      public void GIVEN_an_Aserai_in_a_forest_on_a_winter_night_WHEN_the_narrator_is_told_THEN_it_knows_all_of_it()
      {
         string block = SceneSurroundingsText.Compose(new SceneSurroundingsFacts {
            EngineTerrain = "Forest", Season = "winter", Hour = 23, PlayerCulture = "Aserai"
         });

         block.Should().Contain("forest").And.Contain("winter").And.Contain("night");
         block.Should().Contain("Aserai").And.Contain("strange to their people");
      }

      // The engine's own names are turned into words a narrator can use; sand is sand, a dune is too.
      [Test]
      public void GIVEN_the_engine_names_of_the_ground_WHEN_worded_THEN_they_read_as_places()
      {
         SceneSurroundingsText.TerrainWord("Dune").Should().Be("desert sand");
         SceneSurroundingsText.TerrainWord("Desert").Should().Be("desert sand");
         SceneSurroundingsText.TerrainWord("Snow").Should().Be("snow-covered country");
         SceneSurroundingsText.TerrainWord("RuralArea").Should().Be("open farmland");
         SceneSurroundingsText.TerrainWord("OpenSea").Should().BeNull("a scene is never narrated on the waves");
      }

      // Nothing known, nothing said: a scene opened where the game cannot tell keeps its own canvas alone.
      [Test]
      public void GIVEN_nothing_known_of_the_surroundings_WHEN_composed_THEN_nothing_is_added()
      {
         SceneSurroundingsText.Compose(new SceneSurroundingsFacts()).Should().BeNull();
      }

      // The wolf tale's narrator carries the block when the game supplies it.
      [Test]
      public void GIVEN_the_surroundings_WHEN_the_scene_narrator_is_told_THEN_they_are_in_its_prompt()
      {
         AuthoredQuest q = AuthoredTales.Wolf();
         AuthoredQuestRun run = AuthoredTales.At(q, "the_tracks");

         string prompt = AuthoredQuestPrompt.Narrator(run, q, new SceneSurroundingsFacts {EngineTerrain = "Snow", PlayerCulture = "Empire"});

         prompt.Should().Contain("THE SURROUNDINGS").And.Contain("snow-covered country").And.Contain("Empire");
      }

      // A band that does not scale is the file's size, as before.
      [Test]
      public void GIVEN_a_band_that_does_not_scale_WHEN_sized_THEN_it_is_the_files_roll()
      {
         AuthoredBandSizePolicy.Size(new AuthoredBandSizeFacts {Rolled = 15, ScalesWithPlayer = false, PlayerFighters = 300}).Should().Be(15);
      }

      // fkasad: a climax for a lord with an army. The band grows with the player's men, never shrinks below the file's
      // roll for a lone rider, and stops at a ceiling so a great host does not raise a horde from one wood.
      [Test]
      public void GIVEN_a_band_that_scales_WHEN_the_player_rides_alone_or_with_an_army_THEN_it_meets_them_at_their_size()
      {
         int alone = AuthoredBandSizePolicy.Size(new AuthoredBandSizeFacts {Rolled = 15, ScalesWithPlayer = true, PlayerFighters = 4});
         int army = AuthoredBandSizePolicy.Size(new AuthoredBandSizeFacts {Rolled = 15, ScalesWithPlayer = true, PlayerFighters = 120});
         int host = AuthoredBandSizePolicy.Size(new AuthoredBandSizeFacts {Rolled = 15, ScalesWithPlayer = true, PlayerFighters = 1000});

         alone.Should().Be(15);
         army.Should().BeGreaterThan(60);
         host.Should().Be(AuthoredBandSizePolicy.MaxScaledBand);
      }

      // A tale's scene keeps prisoners out of the wood unless the tale says otherwise; companions are always welcome.
      [Test]
      public void GIVEN_a_scene_that_does_not_welcome_prisoners_WHEN_the_player_invites_THEN_only_their_free_company_is_offered()
      {
         var company = new[] {
            new CompanyMember {Id = "ira", Name = "Ira", Role = CompanyRole.Companion},
            new CompanyMember {Id = "derthert", Name = "Derthert", Role = CompanyRole.Prisoner}
         };

         PlayerCompanyPolicy.InviteCandidates(company, null, new string[0], prisonersWelcome: false).Select(c => c.Id).Should().Equal("ira");
         PlayerCompanyPolicy.InviteCandidates(company, null, new string[0]).Select(c => c.Id).Should().Equal("ira", "derthert");
      }

      // The flag is the tale's, and off unless written.
      [Test]
      public void GIVEN_a_scene_in_a_tale_file_WHEN_loaded_THEN_prisoners_are_welcome_only_when_it_says_so()
      {
         JsonConvert.DeserializeObject<AuthoredScene>("{\"title\":\"t\"}").InvitePrisoners.Should().BeFalse();
         JsonConvert.DeserializeObject<AuthoredScene>("{\"title\":\"t\",\"invite_prisoners\":true}").InvitePrisoners.Should().BeTrue();
         JsonConvert.DeserializeObject<AuthoredSpawn>("{\"id\":\"b\",\"scale_with_player\":true}").ScaleWithPlayer.Should().BeTrue();
      }

      // The Wolf of the Wood ends on its slavers: their band now meets the player at their size.
      [Test]
      public void GIVEN_the_wolf_tale_WHEN_loaded_THEN_its_slavers_scale_with_the_player()
      {
         AuthoredQuest q = AuthoredTales.Wolf();

         q.Stages.SelectMany(s => new[] {s.Objective}.Concat(s.Choices.Select(c => c.Objective)))
          .Where(o => o?.Spawn != null).Should().NotBeEmpty().And.OnlyContain(o => o.Spawn.ScaleWithPlayer);
      }
   }
}
