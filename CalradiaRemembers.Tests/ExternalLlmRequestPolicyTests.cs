// Code written by Gabriel Mailhot, 15/11/2026.
// What ExternalLlmRequestPolicy decides: whether a request another mod sends through CrLlm (the player's own CR connection)
// goes, and how long its answer may be. VonLemberg (RFC, 15/11/2026): "Support short, bounded outputs ... Return a clear
// failure when a required route is unavailable." If this is wrong, another mod spends the player's credit after the
// player said no, with no name on the bill, or asks for an unbounded answer on the player's key.

#region

using CalradiaRemembers.Logic.Extension;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ExternalLlmRequestPolicyTests
   {
      private static ExternalLlmRequestFacts Good(int maxTokens = 0) => new() {
         SourceMod = "BellumDiplomacy", HasSomethingToAnswer = true, AskedMaxOutputTokens = maxTokens, PlayerAllowsOtherMods = true, ConnectionReady = true
      };

      // STAKE: the ordinary case: a named mod with something to answer, on a ready connection the player lends, is sent.
      [Test]
      public void GIVEN_a_signed_request_on_a_ready_connection_the_player_lends_WHEN_ruled_THEN_it_goes()
      {
         ExternalLlmRequestPolicy.Rule(Good()).Allowed.Should().BeTrue();
      }

      // STAKE: the player's credit is the player's: once they turn the door off, no other mod spends a cent through CR,
      // and the caller is told why.
      [Test]
      public void GIVEN_the_player_turned_the_door_off_WHEN_ruled_THEN_nothing_is_sent_and_the_caller_is_told_why()
      {
         ExternalLlmRuling ruling = ExternalLlmRequestPolicy.Rule(new ExternalLlmRequestFacts {
            SourceMod = "BellumDiplomacy", HasSomethingToAnswer = true, PlayerAllowsOtherMods = false, ConnectionReady = true
         });

         ruling.Allowed.Should().BeFalse();
         ruling.Refusal.Should().Be(ExternalLlmRefusal.PlayerTurnedTheDoorOff);
         ExternalLlmRequestPolicy.Explain(ruling.Refusal).Should().Contain("Other mods may use my connection");
      }

      // STAKE: the RFC's "clear failure when a route is unavailable": no key, no model, or no campaign is said plainly.
      [Test]
      public void GIVEN_no_connection_ready_WHEN_ruled_THEN_the_refusal_says_so()
      {
         ExternalLlmRequestPolicy.Rule(new ExternalLlmRequestFacts {
            SourceMod = "BellumDiplomacy", HasSomethingToAnswer = true, PlayerAllowsOtherMods = true, ConnectionReady = false
         }).Refusal.Should().Be(ExternalLlmRefusal.ConnectionNotReady);
      }

      // STAKE: every request spent on the player's key carries a name, so the log and the player can tell who spent it.
      [Test]
      public void GIVEN_an_unsigned_request_WHEN_ruled_THEN_it_is_refused()
      {
         ExternalLlmRequestPolicy.Rule(new ExternalLlmRequestFacts {
            SourceMod = " ", HasSomethingToAnswer = true, PlayerAllowsOtherMods = true, ConnectionReady = true
         }).Refusal.Should().Be(ExternalLlmRefusal.NoSourceMod);
      }

      // STAKE: an empty request would bill the player for an answer to nothing.
      [Test]
      public void GIVEN_nothing_to_answer_WHEN_ruled_THEN_it_is_refused()
      {
         ExternalLlmRequestPolicy.Rule(new ExternalLlmRequestFacts {
            SourceMod = "BellumDiplomacy", HasSomethingToAnswer = false, PlayerAllowsOtherMods = true, ConnectionReady = true
         }).Refusal.Should().Be(ExternalLlmRefusal.NothingToAnswer);
      }

      // STAKE: "short, bounded outputs": no length asked gives a short reply, a huge length is held to a long letter.
      [TestCase(0, ExternalLlmRequestPolicy.DefaultMaxOutputTokens)]
      [TestCase(-5, ExternalLlmRequestPolicy.DefaultMaxOutputTokens)]
      [TestCase(250, 250)]
      [TestCase(50000, ExternalLlmRequestPolicy.CeilingMaxOutputTokens)]
      public void GIVEN_an_asked_answer_length_WHEN_ruled_THEN_it_is_bounded(int asked, int sent)
      {
         ExternalLlmRequestPolicy.Rule(Good(asked)).MaxOutputTokens.Should().Be(sent);
      }

      // STAKE: VonLemberg's diplomacy calls need a model of their own; the id they name is the one sent, trimmed.
      [Test]
      public void GIVEN_a_named_model_WHEN_ruled_THEN_it_goes_and_that_model_is_sent()
      {
         ExternalLlmRuling ruling = ExternalLlmRequestPolicy.Rule(new ExternalLlmRequestFacts {
            SourceMod = "BellumDiplomacy", HasSomethingToAnswer = true, PlayerAllowsOtherMods = true, ConnectionReady = true, AskedModel = " diplomacy "
         });

         ruling.Allowed.Should().BeTrue();
         ruling.Model.Should().Be("diplomacy");
      }

      // STAKE: a request that names no model behaves exactly as before: the player's own prose model.
      [Test]
      public void GIVEN_no_named_model_WHEN_ruled_THEN_the_players_own_model_is_used()
      {
         ExternalLlmRequestPolicy.Rule(Good()).Model.Should().BeNull();
      }

      // STAKE: "a clear failure, not silently another model": a malformed id is refused before a cent is spent.
      [TestCase("glm 5.2")]
      [TestCase("z-ai/\tglm")]
      public void GIVEN_a_malformed_model_id_WHEN_ruled_THEN_it_is_refused_and_says_why(string asked)
      {
         ExternalLlmRuling ruling = ExternalLlmRequestPolicy.Rule(new ExternalLlmRequestFacts {
            SourceMod = "BellumDiplomacy", HasSomethingToAnswer = true, PlayerAllowsOtherMods = true, ConnectionReady = true, AskedModel = asked
         });

         ruling.Refusal.Should().Be(ExternalLlmRefusal.MalformedModel);
         ExternalLlmRequestPolicy.Explain(ruling.Refusal).Should().Contain("Model");
      }
   }
}
