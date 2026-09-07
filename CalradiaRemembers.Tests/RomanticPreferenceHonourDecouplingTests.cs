// Code written by Gabriel Mailhot, 07/09/2026.
// What this pins: which romantic preferences a character's traits and CULTURE make plausible, the pool a new NPC
// profile rolls from once and then keeps for the rest of the campaign.
//
// Why it exists. A player read the code and asked why polyamory was reachable only through a negative Honor trait,
// and why jealousy_cultures.json, which the mod already consults to decide whether a culture tolerates shared
// partners and which cultures keep harems, had no say in it at all. He was right on both counts. The mod declared
// in one file that some cultures hold multiple partners entirely normal, and decided in another that anyone from
// such a culture is strictly monogamous unless they are dishonourable.
//
// Gabriel's ruling, 2026-09-07: polyamory must NOT hang on honour. Only INFIDELITY should, and infidelity is a
// different thing entirely, the betrayal of an exclusivity that was actually promised. Honour already governs that
// where it genuinely happens (an honourable married lord holds firm against seduction in the Stolen Love path), so
// this only removes the place where the two ideas were confused.
//
// If these tests fail, the mod has gone back to treating a way of loving as a moral failing, or a culture that
// keeps harems is generating strictly monogamous nobles again.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RomanticPreferenceHonourDecouplingTests
    {
        private const int EliteTier = 4;

        private static RomanticTraits Traits(int honor) => new() {Honor = honor};

        // A culture that holds shared partners normal, at a tier that historically practised it.
        private static JealousyCulture Tolerant() =>
            JealousyCulture.FromRaw("Low", 0, "Low", false, null, true);

        private static JealousyCulture Strict() => JealousyCulture.Monogamous;

        // THE ruling. A character of a culture that holds multiple partners normal may be polyamorous whatever
        // their honour: an honourable man of such a culture is not thereby forced into exclusivity, and that was
        // exactly the case the mod could not represent.
        [Test]
        public void GIVEN_a_culture_that_tolerates_shared_partners_WHEN_the_character_is_honourable_THEN_polyamory_is_still_plausible()
        {
            RomanticProfilePolicy.BuildPreferencePool(Traits(2), Tolerant(), EliteTier)
                .Should().Contain(RomanticPreference.Polyamorous);
        }

        // The other half, and the point of the whole change: a dishonourable character of a strictly monogamous
        // culture is NOT thereby polyamorous. Low honour makes someone likely to betray a promise, which is
        // infidelity, and says nothing whatever about how many people they would openly commit to.
        [Test]
        public void GIVEN_a_strictly_monogamous_culture_WHEN_the_character_is_dishonourable_THEN_polyamory_is_not_offered()
        {
            RomanticProfilePolicy.BuildPreferencePool(Traits(-2), Strict(), EliteTier)
                .Should().NotContain(RomanticPreference.Polyamorous);
        }

        // The elite gate is the culture file's own rule (PolygamyEliteTier: historical polygamy was concentrated
        // among the elite), reused here rather than a second definition invented for this pool. A hedge knight of
        // a tolerant culture lives by the same norm as everyone else of his standing.
        [Test]
        public void GIVEN_a_tolerant_culture_WHEN_the_character_is_below_its_elite_tier_THEN_polyamory_is_not_offered()
        {
            JealousyCulture eliteOnly = JealousyCulture.FromRaw("Low", EliteTier, "Low", false, null, true);

            RomanticProfilePolicy.BuildPreferencePool(Traits(0), eliteOnly, clanTier: 0)
                .Should().NotContain(RomanticPreference.Polyamorous);
        }

        // Unknown culture keeps today's behaviour exactly. jealousy_cultures.json documents its own fallback ("any
        // culture absent from this file falls back to a strictly monogamous profile"), so a faction from a
        // conversion mod that never added an entry must not silently start producing polyamorous nobles.
        [Test]
        public void GIVEN_no_culture_is_known_WHEN_building_the_pool_THEN_it_falls_back_to_monogamy()
        {
            RomanticProfilePolicy.BuildPreferencePool(Traits(-2), null, EliteTier)
                .Should().NotContain(RomanticPreference.Polyamorous);
        }

        // What honour DOES still decide here is unchanged, and must stay: an honourable character is drawn toward
        // strict exclusivity, a dishonourable one toward tolerating discretion. Removing polyamory from the honour
        // branch must not quietly empty that branch of its real meaning.
        [Test]
        public void GIVEN_honour_WHEN_building_the_pool_THEN_it_still_decides_how_exclusive_the_character_is()
        {
            RomanticProfilePolicy.BuildPreferencePool(Traits(2), Strict(), EliteTier)
                .Should().Contain(RomanticPreference.MonogamousStrict);

            RomanticProfilePolicy.BuildPreferencePool(Traits(-2), Strict(), EliteTier)
                .Should().Contain(RomanticPreference.MonogamousFlexible);
        }
    }
}
