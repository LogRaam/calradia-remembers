// Code written by Gabriel Mailhot, 11/08/2026.
// recruit_notable: the pure gate deciding whether a settlement NOTABLE (gang leader, headman, merchant,
// artisan, rural notable, preacher) can be persuaded in conversation to leave their post and swear into the
// player's clan as a COMPANION. It closes the same class of broken promise recruit_prisoner closed: the LLM
// narrating a recruitment the game never carried out.
//
// WHY IT MATTERS: the policy is consulted at BOTH surfaces around the LLM ("the prompt is advice, the bridge
// is law"): whether recruit_notable is taught at all (EncounterContextBuilder), and whether it actually
// executes (BannerlordGameStateBridge.ExecuteRecruitNotable, which converts the notable to a companion, hands
// their gang/role to a chosen successor so the settlement keeps a notable, and applies a starting relation
// bonus). Get a gate wrong and either a persuaded notable STILL does not join (the broken promise), or a
// notable is talked out of a town that has no one to inherit the role (a settlement left with a vacant post),
// or a low-regard notable is trivially poached.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NotableRecruitmentPolicyTests
    {
        // The core positive case: a living, free notable who is not already the player's, has an eligible
        // successor to inherit the gang/role, and whose regard has reached the floor MUST be recruitable. If
        // this refuses, the whole verb ships a broken promise (LLM narrates the notable joining, nothing moves).
        [Test]
        public void GIVEN_a_free_notable_with_a_successor_at_the_regard_floor_WHEN_evaluating_THEN_they_can_be_recruited()
        {
            NotableRecruitmentPolicy.Evaluate(true, true, false, false, NotableRecruitmentPolicy.RegardFloor, true)
                .Should().Be(NotableRecruitmentBlock.None);
            NotableRecruitmentPolicy.CanRecruit(true, true, false, false, NotableRecruitmentPolicy.RegardFloor, true)
                .Should().BeTrue();
        }

        // The floor is inclusive ("boundary belongs to the rule"): a notable who has earned exactly the
        // deep-bond mark counts as won over, not needing one further point before the switch is ever possible.
        [Test]
        public void GIVEN_regard_exactly_at_the_floor_WHEN_evaluating_THEN_it_passes()
        {
            NotableRecruitmentPolicy.CanRecruit(true, true, false, false, 60, true).Should().BeTrue();
        }

        // The strong regard bar is what stops the verb being a trivial "chat up any town's gang leader and keep
        // him" exploit. Just below the floor must refuse, so a notable who does not deeply esteem the player
        // stays at their post.
        [Test]
        public void GIVEN_regard_just_below_the_floor_WHEN_evaluating_THEN_regard_is_too_low()
        {
            NotableRecruitmentPolicy.Evaluate(true, true, false, false, NotableRecruitmentPolicy.RegardFloor - 1, true)
                .Should().Be(NotableRecruitmentBlock.RegardTooLow);
            NotableRecruitmentPolicy.CanRecruit(true, true, false, false, NotableRecruitmentPolicy.RegardFloor - 1, true)
                .Should().BeFalse();
        }

        // recruit_notable is only ever taught to a genuine settlement notable, never a lord, wanderer or
        // stranger (those have their own recruitment paths: join_clan, join_party). If the hero is not a
        // notable, refuse even at maxed regard so the verb never fires on the wrong kind of hero.
        [Test]
        public void GIVEN_a_hero_who_is_not_a_notable_WHEN_evaluating_THEN_they_are_not_a_notable_even_at_full_regard()
        {
            NotableRecruitmentPolicy.Evaluate(false, true, false, false, 100, true)
                .Should().Be(NotableRecruitmentBlock.NotANotable);
            NotableRecruitmentPolicy.CanRecruit(false, true, false, false, 100, true).Should().BeFalse();
        }

        // A dead notable cannot be recruited. The live executor re-derives IsAlive from the engine, but if it
        // ever reads a dead hero (a stale reference), refuse rather than swear a corpse into the clan.
        [Test]
        public void GIVEN_a_dead_notable_WHEN_evaluating_THEN_they_are_not_alive()
        {
            NotableRecruitmentPolicy.Evaluate(true, false, false, false, 100, true)
                .Should().Be(NotableRecruitmentBlock.NotAlive);
            NotableRecruitmentPolicy.CanRecruit(true, false, false, false, 100, true).Should().BeFalse();
        }

        // A notable who is currently a prisoner belongs to the recruit_prisoner path (freed-from-the-cell
        // defection), not this one (leaving a post they still hold). Refuse so the two verbs never overlap on
        // the same hero and double-recruit.
        [Test]
        public void GIVEN_a_notable_who_is_a_prisoner_WHEN_evaluating_THEN_it_refuses_as_a_prisoner()
        {
            NotableRecruitmentPolicy.Evaluate(true, true, true, false, 100, true)
                .Should().Be(NotableRecruitmentBlock.IsPrisoner);
            NotableRecruitmentPolicy.CanRecruit(true, true, true, false, 100, true).Should().BeFalse();
        }

        // A notable already in the player's clan has nothing to recruit. Refuse so a stray emission on an
        // already-recruited hero is a harmless no-op, never a double-join or a spurious relation bonus.
        [Test]
        public void GIVEN_a_notable_already_in_the_player_clan_WHEN_evaluating_THEN_it_refuses_as_already_player_clan()
        {
            NotableRecruitmentPolicy.Evaluate(true, true, false, true, 100, true)
                .Should().Be(NotableRecruitmentBlock.AlreadyPlayerClan);
            NotableRecruitmentPolicy.CanRecruit(true, true, false, true, 100, true).Should().BeFalse();
        }

        // The author's first call: the notable must bequeath their gang/role to one of their own people so the
        // settlement keeps a notable of that kind. When no supporter or eligible commoner can inherit it, the
        // recruitment must refuse rather than leave the town's post vacant, even at maxed regard.
        [Test]
        public void GIVEN_no_eligible_successor_WHEN_evaluating_THEN_it_refuses_to_leave_the_post_vacant()
        {
            NotableRecruitmentPolicy.Evaluate(true, true, false, false, 100, false)
                .Should().Be(NotableRecruitmentBlock.NoEligibleSuccessor);
            NotableRecruitmentPolicy.CanRecruit(true, true, false, false, 100, false).Should().BeFalse();
        }

        // Which reason surfaces when several gates fail is real, player-facing behaviour (it grounds the
        // refusal line and any diagnostic). Pinning identity ahead of the successor and regard checks keeps the
        // report stable and honest: a hero who is not even a notable should never be told their regard, or a
        // missing successor, is the problem.
        [Test]
        public void GIVEN_a_non_notable_who_also_lacks_a_successor_with_low_regard_WHEN_evaluating_THEN_identity_is_reported_first()
        {
            NotableRecruitmentPolicy.Evaluate(false, true, false, false, 0, false)
                .Should().Be(NotableRecruitmentBlock.NotANotable);
        }

        // The successor gate is checked before regard, so a well-loved notable with nowhere to hand the gang is
        // told the honest reason (no successor), not "earn more trust" they have already earned. Pins that order.
        [Test]
        public void GIVEN_a_beloved_notable_with_no_successor_WHEN_evaluating_THEN_the_missing_successor_is_reported_over_regard()
        {
            NotableRecruitmentPolicy.Evaluate(true, true, false, false, 100, false)
                .Should().Be(NotableRecruitmentBlock.NoEligibleSuccessor);
        }

        // The starting relation bonus represents the standing the notable brings from their gang: they arrive
        // an ally, not a stranger. It is pinned so the value is a deliberate design choice (kept modest, below
        // the deep-bond mark) and a silent edit that zeroed or inflated it would fail here.
        [Test]
        public void GIVEN_the_starting_relation_bonus_WHEN_read_THEN_it_is_a_modest_positive_below_the_deep_bond_mark()
        {
            NotableRecruitmentPolicy.StartingRelationBonus.Should().Be(20);
            NotableRecruitmentPolicy.StartingRelationBonus.Should().BeGreaterThan(0);
            NotableRecruitmentPolicy.StartingRelationBonus.Should().BeLessThan(NotableRecruitmentPolicy.RegardFloor);
        }
    }
}
