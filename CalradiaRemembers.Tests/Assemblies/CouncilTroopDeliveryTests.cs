// Code written by Gabriel Mailhot, 04/08/2026.
// The mod's CouncilTroopDeliveryBehavior persists a List<CouncilTroopDelivery> as byte[] JSON via Newtonsoft.Json,
// mirroring CompanionMissionBehavior's and CouncilStipendBehavior's own SyncData shape exactly (idempotent load,
// graceful on missing/corrupt, see CouncilStipendTests.cs's own header for why the mod project itself is
// integration-only here). What IS unit-testable, and IS the real save contract the behavior depends on, is the
// DTO's own round trip through the same serializer: a delegation is a real party physically travelling the map
// with someone else's troops already loaded aboard it (Gabriel's condition B, a PERMANENT gift once it departs)
// - a save made mid-journey corrupting any one of these fields would either strand the escort forever (lost
// PartyId) or misattribute/miscount the troops on arrival (lost tier counts or donor id).
#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilTroopDeliveryTests
    {
        // Baseline: a single in-flight delegation, led by a real minor hero, must survive a full
        // serialize/deserialize cycle with every field intact.
        [Test]
        public void GIVEN_an_in_flight_delivery_with_an_escort_leader_WHEN_round_tripped_through_the_same_serializer_the_behavior_uses_THEN_every_field_survives()
        {
            var original = new List<CouncilTroopDelivery> {
                new CouncilTroopDelivery {
                    Id = "delivery_1", DonorHeroId = "hero_donor", EscortLeaderId = "hero_escort",
                    PartyId = "party_escort_1", LowCount = 5, MediumCount = 2, HighCount = 1
                }
            };

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilTroopDelivery> restored = JsonConvert.DeserializeObject<List<CouncilTroopDelivery>>(json)!;

            restored.Should().HaveCount(1);
            restored[0].Id.Should().Be("delivery_1");
            restored[0].DonorHeroId.Should().Be("hero_donor");
            restored[0].EscortLeaderId.Should().Be("hero_escort");
            restored[0].PartyId.Should().Be("party_escort_1");
            restored[0].LowCount.Should().Be(5);
            restored[0].MediumCount.Should().Be(2);
            restored[0].HighCount.Should().Be(1);
        }

        // The FALLBACK path (this build's own documented deviation, CouncilTroopDeliveryBehavior's header): a
        // delivery started with no escort leader available (instant-delivery-at-the-lift case) is never actually
        // tracked as an in-flight entry by that fallback branch, but the DTO's own EscortLeaderId field must still
        // round-trip a null cleanly (defensive: a future caller, or a corrupt hand-edited save, could leave it
        // unset) rather than throwing or coercing it to an empty string that would then fail a StringId lookup.
        [Test]
        public void GIVEN_a_delivery_with_no_escort_leader_recorded_WHEN_round_tripped_THEN_the_null_survives()
        {
            var original = new List<CouncilTroopDelivery> {
                new CouncilTroopDelivery {
                    Id = "delivery_2", DonorHeroId = "hero_donor", EscortLeaderId = null,
                    PartyId = null, LowCount = 3, MediumCount = 0, HighCount = 0
                }
            };

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilTroopDelivery> restored = JsonConvert.DeserializeObject<List<CouncilTroopDelivery>>(json)!;

            restored[0].EscortLeaderId.Should().BeNull();
            restored[0].PartyId.Should().BeNull();
        }

        // Mirrors CompanionMissionBehavior's and CouncilStipendBehavior's own empty-save contract: no in-flight
        // deliveries must round-trip to an empty list, never null, since the behavior's own load path only
        // degrades gracefully when it gets one.
        [Test]
        public void GIVEN_no_active_deliveries_WHEN_round_tripped_THEN_it_is_an_empty_list_not_null()
        {
            var original = new List<CouncilTroopDelivery>();

            string json = JsonConvert.SerializeObject(original, Formatting.None);
            List<CouncilTroopDelivery> restored = JsonConvert.DeserializeObject<List<CouncilTroopDelivery>>(json)!;

            restored.Should().NotBeNull();
            restored.Should().BeEmpty();
        }

        // The corrupt-save graceful-degradation contract (mirrors CompanionMissionBehavior's and
        // CouncilStipendBehavior's own catch path): malformed JSON must throw so the behavior's own try/catch can
        // fall back to an empty list and let the campaign open, never strand a half-populated delegation silently.
        [Test]
        public void GIVEN_corrupt_json_WHEN_deserialized_THEN_it_throws_so_the_behaviors_own_catch_path_can_fall_back()
        {
            FluentActions.Invoking(() => JsonConvert.DeserializeObject<List<CouncilTroopDelivery>>("{not valid json"))
                .Should().Throw<JsonException>();
        }
    }
}
