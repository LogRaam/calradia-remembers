// Code written by Gabriel Mailhot, 17/10/2026.
// The conversation temperature the menu setting sends (fkasad, 17/10/2026; Gabriel's go the same day). Wrong, the
// player's pick is silently replaced, or a hand-edited config sends a value no model stays coherent at.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ChatTemperaturePolicyTests
    {
        // STAKE: an existing player who never opens the menu keeps exactly the 0.7 the mod always sent.
        [Test]
        public void GIVEN_no_change_WHEN_the_temperature_is_read_THEN_it_is_the_old_value()
        {
            ChatTemperaturePolicy.Default.Should().Be(0.7f);
            ChatTemperaturePolicy.Clamp(ChatTemperaturePolicy.Default).Should().Be(0.7f);
        }

        // STAKE: the value the player picked inside the menu's range is the value the request carries.
        [Test]
        public void GIVEN_a_pick_inside_the_range_WHEN_the_temperature_is_read_THEN_it_is_honoured()
        {
            ChatTemperaturePolicy.Clamp(0.35f).Should().Be(0.35f);
            ChatTemperaturePolicy.Clamp(ChatTemperaturePolicy.Min).Should().Be(ChatTemperaturePolicy.Min);
            ChatTemperaturePolicy.Clamp(ChatTemperaturePolicy.Max).Should().Be(ChatTemperaturePolicy.Max);
        }

        // STAKE: a hand-edited config cannot send 0 (frozen repetition) or 2 (incoherence), nor a broken number.
        [Test]
        public void GIVEN_a_value_outside_the_range_or_unreadable_WHEN_the_temperature_is_read_THEN_it_is_held_in_range()
        {
            ChatTemperaturePolicy.Clamp(0f).Should().Be(ChatTemperaturePolicy.Min);
            ChatTemperaturePolicy.Clamp(2f).Should().Be(ChatTemperaturePolicy.Max);
            ChatTemperaturePolicy.Clamp(float.NaN).Should().Be(ChatTemperaturePolicy.Default);
        }
    }
}
