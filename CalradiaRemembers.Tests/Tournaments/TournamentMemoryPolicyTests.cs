// Code written by Gabriel Mailhot, 06/09/2026.
// TournamentMemoryPolicy turns a read arena bracket into what each lord carries away: their decisive relation to the
// player (they beat the player, the player beat them, or they merely co-competed / took the crown), the tone they
// speak of it in (brag / gracious / respect / sore), and whether it warms into an appreciation or curdles into a
// grudge. This is the whole point of the pillar: a lord must approach about the RIGHT result in a register true to
// his character. Get Classify wrong and a lord brags about beating a player he actually lost to; get Tone/Aftermath
// wrong and every result reads the same, so the sore loser and the worthy rival (Gabriel wanted both, by traits)
// both collapse into bland politeness. These pin the boundaries the LLM prompt and the appreciation/grudge feeders
// will trust.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Tournaments;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Tournaments
{
   [TestFixture]
   public sealed class TournamentMemoryPolicyTests
   {
      private static TournamentResult Result(
         IReadOnlyList<TournamentBout> bouts,
         bool playerWon = false,
         int eliminatedRound = 0,
         string championId = null)
         => new TournamentResult("Poros", 100, playerWon, eliminatedRound, championId, bouts);

      private static TournamentBout Bout(int round, bool playerAdvanced, params string[] opponents)
         => new TournamentBout(round, opponents, playerAdvanced);

      // A hero in a match the player WON did not advance: the player beat them. If this classified as anything else,
      // a lord the player defeated could end up bragging he won, inverting the fkasad-class bug the pillar exists to
      // prevent.
      [Test]
      public void GIVEN_a_hero_the_player_beat_WHEN_classified_THEN_they_lost_to_the_player()
      {
         var result = Result(new[] {Bout(1, playerAdvanced: true, "rival")});

         TournamentStanding s = TournamentMemoryPolicy.Classify(result, "rival");

         s.Relation.Should().Be(TournamentRelation.LostToThePlayer);
         s.Round.Should().Be(1);
      }

      // The hero in the match the player LOST advanced past the player: they beat the player and may brag. The round
      // is captured so the flavour can say "in the final" versus "in the first round".
      [Test]
      public void GIVEN_a_hero_who_put_the_player_out_WHEN_classified_THEN_they_beat_the_player()
      {
         var result = Result(new[] {Bout(4, playerAdvanced: false, "champion")}, eliminatedRound: 4, championId: "champion");

         TournamentStanding s = TournamentMemoryPolicy.Classify(result, "champion");

         s.Relation.Should().Be(TournamentRelation.BeatThePlayer);
         s.IsChampion.Should().BeTrue();
         s.Round.Should().Be(4);
      }

      // A hero who won the whole thing but never shared a match with the player is a champion co-competitor: worth
      // carrying (he can crow about the crown over a player who also competed), but not a face-to-face defeat.
      [Test]
      public void GIVEN_a_champion_who_never_faced_the_player_WHEN_classified_THEN_they_are_a_champion_co_competitor()
      {
         var result = Result(new[] {Bout(1, playerAdvanced: true, "someone_else")}, eliminatedRound: 2, championId: "champ");

         TournamentStanding s = TournamentMemoryPolicy.Classify(result, "champ");

         s.Relation.Should().Be(TournamentRelation.CoCompetitor);
         s.IsChampion.Should().BeTrue();
      }

      // A hero who neither faced the player nor took the crown has nothing to carry: it must NOT surface as a memory,
      // or lords who were barely in the same tournament would pester the player about nothing.
      [Test]
      public void GIVEN_an_unrelated_participant_WHEN_classified_THEN_there_is_no_relation()
      {
         var result = Result(new[] {Bout(1, playerAdvanced: true, "someone_else")}, championId: "champ");

         TournamentMemoryPolicy.Classify(result, "bystander").Relation.Should().Be(TournamentRelation.None);
      }

      // A proud, low-honour victor crows: this is the braggart the player asked for, distinct from a gracious winner.
      [Test]
      public void GIVEN_a_proud_low_honour_victor_WHEN_toned_THEN_they_brag()
      {
         var standing = new TournamentStanding(TournamentRelation.BeatThePlayer, false, 3);

         TournamentMemoryPolicy.Tone(standing, new TournamentTraitFacts(valor: 2, honor: -1, mercy: 0))
            .Should().Be(TournamentMemoryTone.Brag);
      }

      // An honourable victor is magnanimous, not a braggart: honour must win over valour when both are high, or every
      // skilled knight would gloat regardless of character.
      [Test]
      public void GIVEN_an_honourable_victor_WHEN_toned_THEN_they_are_gracious()
      {
         var standing = new TournamentStanding(TournamentRelation.BeatThePlayer, false, 3);

         TournamentMemoryPolicy.Tone(standing, new TournamentTraitFacts(valor: 2, honor: 2, mercy: 0))
            .Should().Be(TournamentMemoryTone.Gracious);
      }

      // A proud, low-honour hero the player beat sulks: this is the sore loser (the seed of a grudge), and it must be
      // reachable only through low honour, so an honourable loser never reads as bitter.
      [Test]
      public void GIVEN_a_proud_low_honour_loser_WHEN_toned_THEN_they_are_sore()
      {
         var standing = new TournamentStanding(TournamentRelation.LostToThePlayer, false, 2);

         TournamentMemoryPolicy.Tone(standing, new TournamentTraitFacts(valor: 2, honor: -2, mercy: 0))
            .Should().Be(TournamentMemoryTone.Sore);
      }

      // An honourable hero the player beat congratulates them: a defeat met graciously, which should warm into regard,
      // not resentment.
      [Test]
      public void GIVEN_an_honourable_loser_WHEN_toned_THEN_they_are_gracious()
      {
         var standing = new TournamentStanding(TournamentRelation.LostToThePlayer, false, 2);

         TournamentMemoryPolicy.Tone(standing, new TournamentTraitFacts(valor: 0, honor: 2, mercy: 0))
            .Should().Be(TournamentMemoryTone.Gracious);
      }

      // A champion who never faced the player still speaks as a victor over them (he took the crown the player was
      // chasing), so the won-over-player tone logic must apply to a champion co-competitor too.
      [Test]
      public void GIVEN_a_bragging_champion_co_competitor_WHEN_toned_THEN_they_brag()
      {
         var standing = new TournamentStanding(TournamentRelation.CoCompetitor, isChampion: true, 0);

         TournamentMemoryPolicy.Tone(standing, new TournamentTraitFacts(valor: 2, honor: -1, mercy: 0))
            .Should().Be(TournamentMemoryTone.Brag);
      }

      // Sulking curdles into a grudge; grace and respect warm into an appreciation; a brag leaves nothing lasting on
      // its own. This is what lets one match produce either a worthy rival or a bitter one, by character.
      [Test]
      public void GIVEN_a_tone_WHEN_resolving_aftermath_THEN_it_maps_to_the_right_lasting_feeling()
      {
         TournamentMemoryPolicy.Aftermath(TournamentMemoryTone.Sore).Should().Be(TournamentAftermath.Grudge);
         TournamentMemoryPolicy.Aftermath(TournamentMemoryTone.Gracious).Should().Be(TournamentAftermath.Appreciation);
         TournamentMemoryPolicy.Aftermath(TournamentMemoryTone.Respect).Should().Be(TournamentAftermath.Appreciation);
         TournamentMemoryPolicy.Aftermath(TournamentMemoryTone.Brag).Should().Be(TournamentAftermath.None);
      }

      // A null standing or traits must degrade to a plain measured note, never throw: the policy runs off live game
      // data and a throw here would break the whole tournament feeder.
      [Test]
      public void GIVEN_null_inputs_WHEN_toned_THEN_it_is_measured_and_does_not_throw()
      {
         TournamentMemoryPolicy.Tone(null, null).Should().Be(TournamentMemoryTone.Respect);
         TournamentMemoryPolicy.Classify(null, "x").Relation.Should().Be(TournamentRelation.None);
      }
   }
}
