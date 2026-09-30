// Code written by Gabriel Mailhot, 29/09/2026.
// Windmolen (Nexus, 29/09/2026): "Since about one day I am unable to talk to characters... I have tested my connection in
// game and there appears nothing wrong with it." His log: OpenRouter 404, "No endpoints found for deepseek/deepseek-v3.2.
// Every candidate endpoint was removed during routing: Filter by Fallback". He had pinned providers in MCM with
// fallbacks off (the default), and none of them served the model any more. The chat said only that the AI did not
// respond, which sent the author and another player toward timeouts and token limits. This names the real cause and
// the switch that fixes it; if it misfires, a player is told to change a setting that has nothing to do with the fault.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ProviderPinFailurePolicyTests
   {
      private const string WindmolensError =
         "HTTP 404: {\"error\":{\"message\":\"No endpoints found for deepseek/deepseek-v3.2. Every candidate endpoint was removed during routing: " +
         "Filter by Fallback removed gmicloud/fp8, deepinfra/fp4, venice.\",\"code\":404}}";

      // Windmolen's case, word for word: the pin, not the model or the network, is what failed.
      [Test]
      public void GIVEN_a_pin_with_no_fallbacks_and_no_endpoint_left_WHEN_explained_THEN_the_player_is_told_to_clear_the_pin_and_which_model()
      {
         string why = ProviderPinFailurePolicy.Explain(WindmolensError, pinActive: true, fallbacksAllowed: false);

         why.Should().Contain("deepseek/deepseek-v3.2");
         why.Should().Contain("Pin Providers");
         why.Should().Contain("Allow provider fallbacks");
      }

      // With no pin, or with fallbacks on, "no endpoints" means something else (a retired model, an account filter):
      // blaming the pin would send the player to a setting that is not the cause.
      [TestCase(false, false)]
      [TestCase(true, true)]
      public void GIVEN_no_pin_or_fallbacks_allowed_WHEN_explained_THEN_the_pin_is_not_blamed(bool pinActive, bool fallbacksAllowed)
      {
         ProviderPinFailurePolicy.Explain(WindmolensError, pinActive, fallbacksAllowed).Should().BeNull();
      }

      // Any other failure (a timeout, a bad key) keeps its own message.
      [Test]
      public void GIVEN_another_failure_WHEN_explained_THEN_it_is_left_to_its_own_message()
      {
         ProviderPinFailurePolicy.Explain("The request timed out after 120 s.", pinActive: true, fallbacksAllowed: false).Should().BeNull();
      }

      // The in-game connection test must send the same routing as the chat, or it passes while every chat fails.
      [Test]
      public void GIVEN_pinned_providers_WHEN_the_test_request_is_built_THEN_it_carries_the_same_routing_as_the_chat()
      {
         ProviderPinFailurePolicy.RoutingJson("deepinfra, gmicloud", fallbacksAllowed: false)
                                 .Should().Be(",\"provider\":{\"order\":[\"deepinfra\",\"gmicloud\"],\"allow_fallbacks\":false}");
         ProviderPinFailurePolicy.RoutingJson("  ", fallbacksAllowed: false).Should().BeEmpty();
      }
   }
}
