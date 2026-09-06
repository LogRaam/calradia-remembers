// Code written by Gabriel Mailhot, 06/09/2026.
// ViolencePolicy is the anti-exploit spine of player-inflicted violence (player request 2026-09-06). It answers
// three things: may this be attempted at all, how likely is it, and how did this attempt end. Four failures would
// each break the feature in a different way. If the lethal gate leaked, a campaign with the MCM toggle OFF could
// still lose characters to a knife, which is the one thing Gabriel ruled must never happen by default. If the odds
// ignored the victim's company, the sandbox-breaking case the reporter himself worried about (walking up to the
// emperor and cutting his throat) would be as easy as mugging a lone villager. If certainty crept in at either end,
// the feature would become either a guaranteed power fantasy or a dead button. And if Resolve were not
// deterministic on its roll, none of it could be tested at all.

#region

using CalradiaRemembers.Logic.Violence;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Violence
{
    [TestFixture]
    public sealed class ViolencePolicyTests
    {
        private static ViolenceFacts Facts(int player, int target,
            ViolenceExposure exposure = ViolenceExposure.Alone, int roguery = 0)
            => new ViolenceFacts(player, target, exposure, roguery);

        // THE ruling Gabriel gave: the MCM toggle governs killing ONLY. With it off, no attempt on a life is even
        // made; robbery and assault are untouched by it, so the sandbox stays open without anyone dying by default.
        [Test]
        public void GIVEN_lethal_violence_is_disabled_WHEN_checking_permission_THEN_only_killing_is_barred()
        {
            ViolencePolicy.IsPermitted(ViolentAct.Kill, lethalViolenceEnabled: false).Should().BeFalse();
            ViolencePolicy.IsPermitted(ViolentAct.Rob, lethalViolenceEnabled: false).Should().BeTrue();
            ViolencePolicy.IsPermitted(ViolentAct.Assault, lethalViolenceEnabled: false).Should().BeTrue();
        }

        // A barred act never reaches the roll: even a roll that would otherwise succeed must not kill anyone while
        // the toggle is off. This is the guard that makes "default off" mean something.
        [Test]
        public void GIVEN_lethal_violence_is_disabled_WHEN_resolving_a_kill_THEN_it_is_forbidden_whatever_the_roll()
        {
            ViolencePolicy.Resolve(ViolentAct.Kill, Facts(1000, 1), lethalViolenceEnabled: false, rollPercent: 0)
                .Should().Be(ViolenceVerdict.Forbidden);
        }

        // Turning the toggle on does not make killing easy, it makes it possible: the same attempt now resolves on
        // its odds like any other.
        [Test]
        public void GIVEN_lethal_violence_is_enabled_WHEN_resolving_a_kill_THEN_it_resolves_on_the_roll()
        {
            ViolencePolicy.Resolve(ViolentAct.Kill, Facts(1000, 1), lethalViolenceEnabled: true, rollPercent: 0)
                .Should().Be(ViolenceVerdict.Succeeded);
        }

        // An even match comes off about half the time: the anchor every other number is read against.
        [Test]
        public void GIVEN_an_even_match_alone_WHEN_robbing_THEN_the_odds_are_even()
        {
            ViolencePolicy.SuccessChance(ViolentAct.Rob, Facts(200, 200))
                .Should().Be(ViolencePolicy.EvenMatchChancePercent);
        }

        // Gabriel's ruling on place: violence is possible EVERYWHERE, but a victim who is not isolated is HARDER to
        // reach. Company must lower the odds strictly, and guards must lower them further than mere onlookers.
        [Test]
        public void GIVEN_the_victim_has_company_WHEN_attacking_THEN_it_is_strictly_harder_and_guards_worst()
        {
            int alone = ViolencePolicy.SuccessChance(ViolentAct.Assault, Facts(200, 200));
            int onlookers = ViolencePolicy.SuccessChance(ViolentAct.Assault, Facts(200, 200, ViolenceExposure.Onlookers));
            int guarded = ViolencePolicy.SuccessChance(ViolentAct.Assault, Facts(200, 200, ViolenceExposure.Guarded));

            onlookers.Should().BeLessThan(alone);
            guarded.Should().BeLessThan(onlookers);
        }

        // The sandbox-breaking case, pinned: a weak player going after a guarded champion is left with the floor,
        // not with a coin flip. It stays POSSIBLE (Gabriel refused a hard bar), just desperate.
        [Test]
        public void GIVEN_a_weak_player_against_a_guarded_champion_WHEN_killing_THEN_the_odds_fall_to_the_floor_but_never_to_zero()
        {
            int chance = ViolencePolicy.SuccessChance(ViolentAct.Kill, Facts(40, 320, ViolenceExposure.Guarded));

            chance.Should().Be(ViolencePolicy.MinChancePercent);
            chance.Should().BeGreaterThan(0);
        }

        // Severity has its own price: the same victim in the same room is easier to frighten into paying than to
        // beat down, and easier to beat down than to kill.
        [Test]
        public void GIVEN_the_same_victim_WHEN_comparing_acts_THEN_robbery_beats_assault_beats_killing()
        {
            ViolenceFacts f = Facts(200, 200);

            int rob = ViolencePolicy.SuccessChance(ViolentAct.Rob, f);
            int assault = ViolencePolicy.SuccessChance(ViolentAct.Assault, f);
            int kill = ViolencePolicy.SuccessChance(ViolentAct.Kill, f);

            rob.Should().BeGreaterThan(assault);
            assault.Should().BeGreaterThan(kill);
        }

        // Roguery is the craft of the shakedown, so it helps a robbery and nothing else: a cutpurse is not thereby
        // a better murderer.
        [Test]
        public void GIVEN_a_practised_rogue_WHEN_robbing_THEN_the_craft_helps_but_not_when_killing()
        {
            ViolencePolicy.SuccessChance(ViolentAct.Rob, Facts(200, 200, roguery: 100))
                .Should().BeGreaterThan(ViolencePolicy.SuccessChance(ViolentAct.Rob, Facts(200, 200)));

            ViolencePolicy.SuccessChance(ViolentAct.Kill, Facts(200, 200, roguery: 100))
                .Should().Be(ViolencePolicy.SuccessChance(ViolentAct.Kill, Facts(200, 200)));
        }

        // Neither certainty exists: overwhelming odds still stop at the ceiling, so no attempt is ever a formality.
        [Test]
        public void GIVEN_overwhelming_odds_WHEN_attacking_THEN_it_is_capped_short_of_certainty()
        {
            ViolencePolicy.SuccessChance(ViolentAct.Rob, Facts(5000, 1, roguery: 300))
                .Should().Be(ViolencePolicy.MaxChancePercent);
        }

        // Resolve is deterministic on its roll, which is what makes every outcome above testable: the engine rolls,
        // the policy decides, and the boundary sits exactly at the chance value.
        [Test]
        public void GIVEN_a_roll_at_the_boundary_WHEN_resolving_THEN_below_succeeds_and_at_the_value_fails()
        {
            ViolenceFacts f = Facts(200, 200);
            int chance = ViolencePolicy.SuccessChance(ViolentAct.Rob, f);

            ViolencePolicy.Resolve(ViolentAct.Rob, f, true, chance - 1).Should().Be(ViolenceVerdict.Succeeded);
            ViolencePolicy.Resolve(ViolentAct.Rob, f, true, chance).Should().Be(ViolenceVerdict.Failed);
        }

        // Null facts must degrade to the floor rather than throw: this runs off live campaign data on every attempt.
        [Test]
        public void GIVEN_missing_facts_WHEN_asking_the_odds_THEN_it_floors_and_does_not_throw()
        {
            ViolencePolicy.SuccessChance(ViolentAct.Assault, null).Should().Be(ViolencePolicy.MinChancePercent);
        }
    }
}
