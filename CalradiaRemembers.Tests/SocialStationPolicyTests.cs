// Code written by Gabriel Mailhot, 28/10/2026.
// SocialStationPolicy resolves which station definition voices a character and reads the definition files. Gabriel
// (28/10/2026): one definition per social status, composed into the prompt, each adjusting to the player's rung ("Un
// paysan qui parle au joueur niveau 1 sera plus rude que si il parle au joueur niveau 20 Clan tier 3"). avalanche24
// (Nexus, 28/10/2026): companions and a landowner spoke "way above their station". If this is wrong, a hired companion is
// voiced as a lord, a landowner as a noble, or a player's edit of a definition is silently lost.

#region

using System.IO;
using CalradiaRemembers.Logic.Social;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SocialStationPolicyTests
   {
      // STAKE: avalanche24's companion reasoned she was a lady of the realm; a hired wanderer is voiced as a companion.
      [Test]
      public void GIVEN_a_hired_wanderer_WHEN_resolved_THEN_she_is_a_companion()
      {
         SocialStationPolicy.Resolve(new SocialStationFacts {IsPlayerCompanion = true, Occupation = "Wanderer"}).Should().Be(SocialStationPolicy.Companion);
      }

      // STAKE: avalanche24's landowner is a village notable, a commoner of means, never a noble.
      [Test]
      public void GIVEN_a_landowner_WHEN_resolved_THEN_he_is_a_rural_notable()
      {
         SocialStationPolicy.Resolve(new SocialStationFacts {Occupation = "RuralNotable"}).Should().Be("ruralnotable");
      }

      // STAKE: a lord heading a landed house, a lady of a house who commands nothing, and a crown are three stations.
      [Test]
      public void GIVEN_nobles_WHEN_resolved_THEN_ruler_landed_lord_and_noble_are_apart()
      {
         SocialStationPolicy.Resolve(new SocialStationFacts {IsRuler = true, IsNoble = true, HeadsLandedHouse = true}).Should().Be(SocialStationPolicy.Ruler);
         SocialStationPolicy.Resolve(new SocialStationFacts {IsNoble = true, HeadsLandedHouse = true}).Should().Be(SocialStationPolicy.Lord);
         SocialStationPolicy.Resolve(new SocialStationFacts {IsNoble = true}).Should().Be(SocialStationPolicy.Noble);
      }

      // STAKE: Gabriel named the bandit: a bandit hero is voiced as the street, with the gang leaders.
      [Test]
      public void GIVEN_a_bandit_or_a_gang_leader_WHEN_resolved_THEN_both_are_of_the_street()
      {
         SocialStationPolicy.Resolve(new SocialStationFacts {Occupation = "Bandit"}).Should().Be(SocialStationPolicy.GangLeader);
         SocialStationPolicy.Resolve(new SocialStationFacts {Occupation = "GangLeader"}).Should().Be(SocialStationPolicy.GangLeader);
      }

      // STAKE: a definition is read as written: who they are, and how they meet each rung; comments never reach the prompt.
      [Test]
      public void GIVEN_a_definition_file_WHEN_read_THEN_each_section_is_kept_and_comments_are_dropped()
      {
         var definition = SocialStationPolicy.Parse("# a comment\n[who]\nYou hold land\nin the village.\n\n[nobody]\nYou are rude to them.\n[risinghouse]\nYou take your hat off.");

         SocialStationPolicy.Who(definition).Should().Be("You hold land in the village.");
         SocialStationPolicy.Toward(definition, PlayerRung.Nobody).Should().Be("You are rude to them.");
         SocialStationPolicy.Toward(definition, PlayerRung.RisingHouse).Should().Be("You take your hat off.");
      }

      // STAKE: a player who deletes a section loses only that line, never the conversation.
      [Test]
      public void GIVEN_a_definition_missing_a_rung_WHEN_read_THEN_that_rung_says_nothing()
      {
         SocialStationPolicy.Toward(SocialStationPolicy.Parse("[who]\nA merchant."), PlayerRung.Sovereign).Should().BeNull();
         SocialStationPolicy.Who(SocialStationPolicy.Parse("")).Should().BeNull();
      }

      // STAKE: every station CR ships says who the character is and meets each of the five rungs; a missing file or
      // section would leave that station voiced as before, above or below its place.
      [Test]
      public void GIVEN_the_shipped_station_files_WHEN_read_THEN_each_says_who_and_meets_every_rung()
      {
         foreach (string key in SocialStationPolicy.Keys)
         {
            string file = Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "ModuleData", "stations", key + ".txt");
            var definition = SocialStationPolicy.Parse(File.ReadAllText(file));

            SocialStationPolicy.Who(definition).Should().NotBeNull(key);
            foreach (PlayerRung rung in System.Enum.GetValues(typeof(PlayerRung)))
               SocialStationPolicy.Toward(definition, rung).Should().NotBeNull($"{key} {rung}");
         }
      }
   }
}
