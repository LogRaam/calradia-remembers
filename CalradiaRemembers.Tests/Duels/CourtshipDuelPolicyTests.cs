// Code written by Gabriel Mailhot, 16/07/2026.
// Duels pillar, Inc A: a courtship duel's romance-axis consequence. The through-line is that fighting for her
// (win or lose) is what a suitor is FOR, while refusing to fight for her at all is judged worse than losing.
// If refusal cost less than a loss, a suitor could always duck a risky duel with no penalty beyond winning it,
// which would teach the opposite of what this feature means: dare or be doubted.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class CourtshipDuelPolicyTests
   {
      // The marquee win: fighting for her and winning must raise her attraction. This is the whole point of a
      // suitor risking the field.
      [Test]
      public void GIVEN_the_player_won_the_courtship_duel_WHEN_resolving_THEN_attraction_rises_by_admiration_on_win()
      {
         CourtshipDuelPolicy.Resolve(DuelResolution.PlayerWon)
            .CourtedAttractionDelta.Should().Be(CourtshipDuelPolicy.AdmirationOnWin);
      }

      // Losing a duel fought for her must sting, but only modestly: he dared the field, and daring counts for
      // something even in defeat.
      [Test]
      public void GIVEN_the_player_lost_the_courtship_duel_WHEN_resolving_THEN_attraction_falls_by_doubt_on_loss()
      {
         CourtshipDuelPolicy.Resolve(DuelResolution.NpcWon)
            .CourtedAttractionDelta.Should().Be(CourtshipDuelPolicy.DoubtOnLoss);
      }

      // Refusing to fight for her must cost MORE than losing would have. This is the sign that most defines
      // the pillar: without it, ducking a winnable-looking duel would always be the safe play.
      [Test]
      public void GIVEN_the_player_refused_the_courtship_duel_WHEN_resolving_THEN_attraction_falls_by_cowardice_on_refuse()
      {
         CourtshipDuelPolicy.Resolve(DuelResolution.PlayerForfeited)
            .CourtedAttractionDelta.Should().Be(CourtshipDuelPolicy.CowardiceOnRefuse);
      }

      // The rival backing out is not a real contest, so the gain must be modest, not the full win bonus: the
      // player's own worth was never actually proven, only the rival's lack of nerve.
      [Test]
      public void GIVEN_the_rival_balked_WHEN_resolving_THEN_attraction_rises_by_admiration_on_rival_balked()
      {
         CourtshipDuelPolicy.Resolve(DuelResolution.NpcForfeited)
            .CourtedAttractionDelta.Should().Be(CourtshipDuelPolicy.AdmirationOnRivalBalked);
      }

      // A resolution outside the known enum must be a defensive no-op, never a guess: an unrecognized
      // resolution should never silently move a hero's heart.
      [Test]
      public void GIVEN_an_unrecognized_resolution_WHEN_resolving_THEN_attraction_does_not_move()
      {
         CourtshipDuelPolicy.Resolve((DuelResolution) 999)
            .CourtedAttractionDelta.Should().Be(0);
      }

      // The ordering guarantee that carries the whole design: winning must beat losing must beat refusing, as
      // signed values. If this ever inverted, refusing to fight for her would read as safer than daring and
      // losing, which is backwards.
      [Test]
      public void GIVEN_the_four_resolutions_WHEN_compared_THEN_winning_beats_losing_beats_refusing()
      {
         CourtshipDuelPolicy.AdmirationOnWin.Should().BeGreaterThan(CourtshipDuelPolicy.DoubtOnLoss);
         CourtshipDuelPolicy.DoubtOnLoss.Should().BeGreaterThan(CourtshipDuelPolicy.CowardiceOnRefuse);
      }
   }
}
