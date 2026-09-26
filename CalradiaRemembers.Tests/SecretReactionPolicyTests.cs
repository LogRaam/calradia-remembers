// Code written by Gabriel Mailhot, 26/09/2026.
// Documents what a lord does with an intercepted letter that came into their hands (courier Inc 4b). Gabriel: the
// content provokes an action "in proportion to the secret". The model names in one word how the letter touches the
// reader; these tests pin how that word is read and what each severity sets in motion, so a harmless letter never
// costs the player anything and a plot read by its target always does.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SecretReactionPolicyTests
   {
      // ── Reading the model's word ───────────────────────────────────────────

      [TestCase("TREASON", SecretSeverity.Treason)]
      [TestCase("against", SecretSeverity.AgainstInterests)]
      [TestCase("Embarrassing.", SecretSeverity.Embarrassing)]
      [TestCase("NOTHING", SecretSeverity.Nothing)]
      public void GIVEN_the_models_word_WHEN_read_THEN_it_names_the_severity(string answer, SecretSeverity expected)
      {
         SecretReactionPolicy.Parse(answer).Should().Be(expected);
      }

      // A garbled or empty reply must never punish the player.
      [TestCase("")]
      [TestCase(null)]
      [TestCase("I cannot say.")]
      public void GIVEN_an_answer_that_names_nothing_WHEN_read_THEN_it_is_nothing(string answer)
      {
         SecretReactionPolicy.Parse(answer).Should().Be(SecretSeverity.Nothing);
      }

      // When the model hedges between two words, the graver one is what it saw.
      [Test]
      public void GIVEN_an_answer_naming_two_severities_WHEN_read_THEN_the_gravest_wins()
      {
         SecretReactionPolicy.Parse("Embarrassing, perhaps even treason").Should().Be(SecretSeverity.Treason);
      }

      // ── What each severity sets in motion ──────────────────────────────────

      // A letter that does not touch the reader, or only makes good gossip, costs the author nothing beyond the rumour.
      [TestCase(SecretSeverity.Nothing)]
      [TestCase(SecretSeverity.Embarrassing)]
      public void GIVEN_a_letter_that_does_not_harm_the_reader_WHEN_they_react_THEN_nothing_more_happens(SecretSeverity severity)
      {
         SecretReaction r = SecretReactionPolicy.For(severity, authorIsPlayer: true, readerAlreadyHeldGrudge: false);

         r.GrudgeMagnitude.Should().Be(0);
         r.PlayerRenownPenalty.Should().Be(0);
         r.ConfrontsPlayerByLetter.Should().BeFalse();
      }

      // Against their interests: the reader resents the author, as the old leak did, sharper if they already did.
      [Test]
      public void GIVEN_a_letter_against_the_readers_interests_WHEN_they_react_THEN_they_resent_its_author()
      {
         SecretReaction r = SecretReactionPolicy.For(SecretSeverity.AgainstInterests, authorIsPlayer: true, readerAlreadyHeldGrudge: false);

         r.GrudgeMagnitude.Should().Be(LetterLeakConsequencePolicy.GrudgeMagnitudeHostile);
         r.PlayerRenownPenalty.Should().Be(LetterLeakConsequencePolicy.RenownPenaltyHostile);
         r.ConfrontsPlayerByLetter.Should().BeFalse();
      }

      // A plot read by its target: the sharpest grudge, and the reader writes to confront the player who wrote it.
      [Test]
      public void GIVEN_the_player_plotted_against_the_reader_WHEN_they_react_THEN_they_confront_the_player()
      {
         SecretReaction r = SecretReactionPolicy.For(SecretSeverity.Treason, authorIsPlayer: true, readerAlreadyHeldGrudge: false);

         r.GrudgeMagnitude.Should().Be(SecretReactionPolicy.TreasonGrudgeMagnitude);
         r.PlayerRenownPenalty.Should().Be(SecretReactionPolicy.TreasonRenownPenalty);
         r.ConfrontsPlayerByLetter.Should().BeTrue();
      }

      // Another lord's letter to the player: the reader resents THAT lord, and the player, who wrote nothing, pays nothing.
      [Test]
      public void GIVEN_another_lord_wrote_the_plot_WHEN_the_reader_reacts_THEN_the_player_pays_nothing()
      {
         SecretReaction r = SecretReactionPolicy.For(SecretSeverity.Treason, authorIsPlayer: false, readerAlreadyHeldGrudge: false);

         r.GrudgeMagnitude.Should().Be(SecretReactionPolicy.TreasonGrudgeMagnitude);
         r.PlayerRenownPenalty.Should().Be(0);
         r.ConfrontsPlayerByLetter.Should().BeFalse();
      }
   }
}
