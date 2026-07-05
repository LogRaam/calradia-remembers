// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar: NemesisIdentity is a plain carrier with no behaviour — this just protects that every
// property round-trips, so a typo in a property name fails loudly instead of silently at engine-wiring time.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisIdentityTests
    {
        [Test]
        public void GIVEN_a_freshly_minted_nemesis_WHEN_setting_every_property_THEN_all_values_round_trip()
        {
            var identity = new NemesisIdentity
            {
                Id = "nemesis-1",
                Name = "Ranulf",
                Epithet = "the Faceless Fox",
                BandCultureKey = "forest_bandits",
                RegionId = "region_calradia_north",
                IsAlive = true,
                DebtOwed = 250,
                TimesEncountered = 3,
                EncountersSinceReveal = 1,
                MintedDay = 100,
                DeathDay = null
            };

            identity.Id.Should().Be("nemesis-1");
            identity.Name.Should().Be("Ranulf");
            identity.Epithet.Should().Be("the Faceless Fox");
            identity.BandCultureKey.Should().Be("forest_bandits");
            identity.RegionId.Should().Be("region_calradia_north");
            identity.IsAlive.Should().BeTrue();
            identity.DebtOwed.Should().Be(250);
            identity.TimesEncountered.Should().Be(3);
            identity.EncountersSinceReveal.Should().Be(1);
            identity.MintedDay.Should().Be(100);
            identity.DeathDay.Should().BeNull();
        }

        [Test]
        public void GIVEN_a_slain_nemesis_WHEN_setting_DeathDay_THEN_it_carries_a_value_and_IsAlive_can_be_false()
        {
            var identity = new NemesisIdentity { IsAlive = false, DeathDay = 142 };

            identity.IsAlive.Should().BeFalse();
            identity.DeathDay.Should().Be(142);
        }
    }
}
