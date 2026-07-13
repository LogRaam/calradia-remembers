// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (ProudSeal 2026-07-10): NPCs opened too neutral and too reasonable, so a rivalry required the
// player to offend someone first. This policy decides the OPENING TONE a barely-known lord takes toward the
// player, from the lord's traits and the player's standing: Cordial, Neutral, Cool, Arrogant, Suspicious, or
// Demeaning. The answer only colours prompt tone (EncounterContext.OpeningDisposition); it must NEVER touch
// relation numbers, or deeds would stop being what governs regard. Two hard exemptions matter as much as the
// roll itself: a lord in the player's OWN clan never opens prejudiced (Loyal), and an already-known lord keeps
// their earned stance instead of a cold first impression. Get either exemption wrong and a loyal companion or
// an established friend would suddenly read as hostile for no in-fiction reason.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class FirstImpressionPolicyTests
    {
        // Fail safe, never fail prejudiced: a missing input must never resolve to Arrogant/Demeaning/Suspicious,
        // which would colour an NPC's tone hostile for a reason the player could never trace back to anything.
        [Test]
        public void GIVEN_a_null_input_WHEN_resolving_THEN_it_is_neutral()
        {
            FirstImpressionPolicy.Resolve(null).Should().Be(FirstImpressionDisposition.Neutral);
        }

        // The hard gate, checked first (ROADMAP, Gabriel 2026-07-10): a lord in the player's OWN clan "has cast
        // his lot with the player" and opens loyal or grateful, never arrogant or contemptuous, NO MATTER how
        // hostile their traits are. Hostile Mercy/Honor here would fire Demeaning/Suspicious for a stranger;
        // this proves clan membership overrides the trait roll entirely rather than merely softening it.
        [Test]
        public void GIVEN_an_own_clan_member_with_hostile_traits_WHEN_resolving_THEN_they_open_loyal()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = -2,
                Honor = -2,
                IsOwnClanMember = true
            };

            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Loyal);
        }

        // "First impressions are only for the barely-known" (FirstImpressionResolver): an already-known lord's
        // EARNED stance must govern, not a fresh trait roll. Without this gate, a friend the player spent real
        // effort winning over could suddenly render as Demeaning at the next chat purely from their traits,
        // contradicting the relationship the player actually built.
        [Test]
        public void GIVEN_an_already_known_noble_with_hostile_traits_WHEN_resolving_THEN_no_first_impression_colour()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = -2,
                Honor = -2,
                IsAlreadyKnown = true
            };

            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.None);
        }

        // The floor the whole pressure scale calibrates against: with nothing pushing the noble either warm or
        // hostile, the opening must be the ordinary default. Every other disposition test below is read as a
        // deviation FROM this baseline, so if the baseline itself drifted off Neutral the whole scale would be
        // silently miscalibrated.
        [Test]
        public void GIVEN_all_neutral_inputs_WHEN_resolving_THEN_the_opening_is_neutral()
        {
            var i = new FirstImpressionInputs { Roll = 0 };

            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Neutral);
        }

        // High Mercy/Generosity must be able to push the OTHER way too, past plain Neutral into an actively
        // warm Cordial opening, not just fail to trigger the hostile bands. Pins the negative-pressure cutoff
        // (<= -3) that separates "no prejudice" from "genuinely welcoming"; the exact threshold is a TUNE
        // value with no further documented rationale beyond the arithmetic itself.
        [Test]
        public void GIVEN_a_warm_noble_meeting_a_stranger_WHEN_resolving_THEN_they_open_cordial()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = 2,
                Generosity = 2,
                Roll = 0
            };

            // pressure = -2 (mercy) - 2 (generosity) = -4, which is <= -3.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Cordial);
        }

        // This is ProudSeal's own example scenario (Belgir deriding a landless upstart whose father was
        // "arrow-fodder"): a cruel, low-Honor noble facing a player with no tier/renown/fiefs to disarm them.
        // Pins that when both Mercy and Honor are pushing hostile, Mercy < 0 wins the tie (Demeaning over
        // Suspicious), matching the resolver's own priority order.
        [Test]
        public void GIVEN_a_cruel_low_honor_noble_vs_a_landless_upstart_WHEN_the_roll_fires_THEN_they_are_demeaning()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = -2,
                Honor = -2,
                PlayerClanTier = 0,
                PlayerRenownBand = 0,
                PlayerFiefCount = 0,
                Roll = 0
            };

            // pressure = 4 (mercy) + 2 (honor) = 6, chance = min(85, 90) = 85, roll 0 < 85 fires, Mercy<0 wins.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Demeaning);
        }

        // Same hostile pressure as above, but the stable per-NPC roll misses the show-it chance: prejudice
        // existing is not the same as a noble ACTING on it every time. Without this, every noble with hostile
        // traits would show it 100% of the time, defeating the point of the roll (a personality trait, not a
        // guaranteed insult) and making the mod feel mechanical rather than characterful.
        [Test]
        public void GIVEN_the_same_cruel_noble_but_a_high_roll_WHEN_resolving_THEN_they_do_not_show_it()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = -2,
                Honor = -2,
                Roll = 90
            };

            // pressure 6, chance 85, roll 90 >= 85 so this particular noble keeps it to themselves.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Neutral);
        }

        // ROADMAP's "ambition sub-trigger": a low-Generosity lord SHARING the player's faction resents "a new
        // clan crowding the faction's shared rewards" without the player ever having wronged them. This is the
        // core of ProudSeal's ask (rivalry without the player offending first) and pins that a same-faction
        // rival reads as Arrogant specifically, not Suspicious or Demeaning.
        [Test]
        public void GIVEN_an_ambitious_faction_rival_WHEN_the_roll_fires_THEN_they_are_arrogant()
        {
            var i = new FirstImpressionInputs
            {
                Generosity = -2,
                SameFactionRival = true,
                PlayerClanTier = 0,
                Roll = 0
            };

            // pressure = 2 (generosity) + 2 (rival) = 4, chance = 60, roll 0 < 60, stingy rival wins.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Arrogant);
        }

        // Isolates the Honor-only path: "low Honor projects suspicion" (ROADMAP trait-mapping sketch), with no
        // Mercy driver present to win the priority order instead. Without this case on its own, Suspicious
        // could go untested and silently become unreachable behind the Demeaning/Arrogant checks above it.
        [Test]
        public void GIVEN_a_low_honor_noble_only_WHEN_the_roll_fires_THEN_they_are_suspicious()
        {
            var i = new FirstImpressionInputs
            {
                Honor = -2,
                PlayerClanTier = 0,
                Roll = 0
            };

            // pressure = 2 (honor), chance = 30, roll 0 < 30, no mercy driver so honor wins.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Suspicious);
        }

        // ROADMAP's "softener": high player renown or a strong record disarms prejudice, so a famous, titled
        // player should read as a peer rather than a target. Pins that clan tier alone can pull hostile
        // pressure back to plain Neutral, not merely reduce it, once it crosses back above the Cordial cutoff.
        [Test]
        public void GIVEN_high_player_standing_WHEN_resolving_THEN_it_disarms_a_cruel_noble()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = -2,
                PlayerClanTier = 6,
                Roll = 0
            };

            // pressure = 4 (mercy) - 6 (tier) = -2, above 0 but not <= -3, so plainly neutral.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Neutral);
        }

        // The design point of the whole feature (ROADMAP): a noble is "consistently arrogant (a personality),
        // not random each talk". If StableRoll varied per call, the same lord could open Demeaning one
        // conversation and Cordial the next with nothing in the fiction to explain the change.
        [Test]
        public void GIVEN_the_same_seed_twice_WHEN_taking_a_stable_roll_THEN_the_value_is_identical()
        {
            FirstImpressionPolicy.StableRoll("lord_derthert")
                                 .Should().Be(FirstImpressionPolicy.StableRoll("lord_derthert"));
        }

        // Resolve() compares Roll against a 0..85 show-it chance (i.Roll >= chance). A roll that ever escaped
        // 0..99, or came back negative, would either always fail that comparison (prejudice can never show) or
        // behave inconsistently depending on the seed's hash. Includes the empty-string edge case explicitly.
        [Test]
        public void GIVEN_any_seed_WHEN_taking_a_stable_roll_THEN_it_stays_in_the_zero_to_ninety_nine_range()
        {
            FirstImpressionPolicy.StableRoll("Caladog").Should().BeInRange(0, 99);
            FirstImpressionPolicy.StableRoll("seed").Should().BeInRange(0, 99);
            FirstImpressionPolicy.StableRoll("").Should().BeInRange(0, 99);
        }

        // Guards the algorithm itself: StableRoll's own XML doc calls out that it deliberately uses FNV-1a, NOT
        // string.GetHashCode (which is randomized per PROCESS in .NET). A regression back to GetHashCode would
        // pass every other test here (still deterministic within one run, still in range) but would silently
        // break the "same noble, same disposition" promise across game restarts, the exact bug this exists to
        // prevent, so this test pins the precomputed cross-process value the real algorithm must reproduce.
        [Test]
        public void GIVEN_a_known_seed_WHEN_taking_a_stable_roll_THEN_it_yields_the_precomputed_value()
        {
            // FNV-1a of "lord_derthert" modulo 100 is a fixed, cross-process value.
            FirstImpressionPolicy.StableRoll("lord_derthert").Should().Be(19);
        }
    }
}
