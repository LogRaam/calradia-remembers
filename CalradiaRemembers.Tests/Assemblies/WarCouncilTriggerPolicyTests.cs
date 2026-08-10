// Code written by Gabriel Mailhot, 10/08/2026.
// Documents the war-council OFFER gate. What breaks without it: the player suggestion is that after an army forms
// or wins a considerable action, the mod OFFERS to convene the war council. This policy is the single tested
// authority WarCouncilOfferBehavior defers to before it shows that inquiry. Get it wrong and the offer either
// never appears (no captains ever take counsel) or spams on every back-to-back battle (the anti-nag cooldown is
// exactly what the DefaultCooldownDays boundary pins), or surfaces to a lone player who commands no army at all.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class WarCouncilTriggerPolicyTests
    {
        // The core case the whole feature exists for: army led, action was notable, never offered before, so the
        // captains-take-counsel offer must fire. If this returned false the trigger would be dead on arrival.
        [Test]
        public void GIVEN_in_an_army_and_a_notable_action_never_offered_WHEN_evaluated_THEN_it_offers()
            => WarCouncilTriggerPolicy.ShouldOffer(playerInOrLeadsArmy: true, actionIsNotable: true,
                    daysSinceLastOffer: null, cooldownDays: WarCouncilTriggerPolicy.DefaultCooldownDays)
                .Should().BeTrue();

        // A never-offered player (null days) is the first-ever offer and must be allowed: the cooldown only guards
        // REPEAT offers, and treating "no prior offer" as "still in cooldown" would suppress the very first one.
        [Test]
        public void GIVEN_no_prior_offer_WHEN_evaluated_THEN_the_null_days_case_is_allowed()
            => WarCouncilTriggerPolicy.ShouldOffer(playerInOrLeadsArmy: true, actionIsNotable: true,
                    daysSinceLastOffer: null, cooldownDays: WarCouncilTriggerPolicy.DefaultCooldownDays)
                .Should().BeTrue();

        // The offer is about an ARMY taking counsel. A lord acting alone (no army) has no captains to convene, so
        // however notable the deed, there is no war council to offer and this must refuse.
        [Test]
        public void GIVEN_not_in_an_army_WHEN_evaluated_THEN_it_refuses()
            => WarCouncilTriggerPolicy.ShouldOffer(playerInOrLeadsArmy: false, actionIsNotable: true,
                    daysSinceLastOffer: null, cooldownDays: WarCouncilTriggerPolicy.DefaultCooldownDays)
                .Should().BeFalse();

        // A routine, non-notable action (a skirmish, a minor owner change) must not summon the captains: only a
        // considerable action (a capture, a large battle) earns the offer, or it would nag on trivial events.
        [Test]
        public void GIVEN_a_non_notable_action_WHEN_evaluated_THEN_it_refuses()
            => WarCouncilTriggerPolicy.ShouldOffer(playerInOrLeadsArmy: true, actionIsNotable: false,
                    daysSinceLastOffer: null, cooldownDays: WarCouncilTriggerPolicy.DefaultCooldownDays)
                .Should().BeFalse();

        // Anti-spam: a second notable action ONE day after an offer, well inside the 3-day cooldown, must be
        // refused. This is the "back-to-back battles do not spam offers" guarantee the player would feel as nagging.
        [Test]
        public void GIVEN_a_notable_action_within_the_cooldown_WHEN_evaluated_THEN_it_refuses()
            => WarCouncilTriggerPolicy.ShouldOffer(playerInOrLeadsArmy: true, actionIsNotable: true,
                    daysSinceLastOffer: 1, cooldownDays: WarCouncilTriggerPolicy.DefaultCooldownDays)
                .Should().BeFalse();

        // The far side of the cooldown: once at least cooldownDays have passed the offer is allowed again, so a
        // genuinely fresh campaign beat still earns its council. The boundary is inclusive (exactly cooldownDays
        // fires) so a 3-day cooldown does not silently become a 4-day one.
        [Test]
        public void GIVEN_a_notable_action_after_the_cooldown_WHEN_evaluated_THEN_it_offers_again()
        {
            WarCouncilTriggerPolicy.ShouldOffer(playerInOrLeadsArmy: true, actionIsNotable: true,
                    daysSinceLastOffer: WarCouncilTriggerPolicy.DefaultCooldownDays, cooldownDays: WarCouncilTriggerPolicy.DefaultCooldownDays)
                .Should().BeTrue();

            WarCouncilTriggerPolicy.ShouldOffer(playerInOrLeadsArmy: true, actionIsNotable: true,
                    daysSinceLastOffer: WarCouncilTriggerPolicy.DefaultCooldownDays + 2, cooldownDays: WarCouncilTriggerPolicy.DefaultCooldownDays)
                .Should().BeTrue();
        }
    }
}
