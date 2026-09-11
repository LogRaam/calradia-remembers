// Code written by Gabriel Mailhot, 10/08/2026.
// Guards LoverChildClanPolicy (Inc 3c): which line a child conceived by a partner's LOVER joins at birth. The
// author's hard requirement is that such a child belongs to its genetic father (the lover) and its mother, and
// NEVER the player-husband. The trap is a wife married INTO the player's clan: the ordinary CR rule places a
// CR-conceived child in the MOTHER's clan, which for that wife is the player's own house, wrongly adopting her
// lover's child. These tests pin the "never the player's clan" invariant at every branch, so a future edit that
// re-lets a lover-child into the player's line fails here instead of shipping. Get it wrong and the player's
// dynasty silently inherits another man's child.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LoverChildClanPolicyTests
    {
        // The normal, preferred outcome: the lover is a lord with his own house, so his child joins his line.
        // This is patrilineal attribution to the GENETIC father, exactly what the author asked for.
        [Test]
        public void GIVEN_the_lover_has_his_own_clan_WHEN_resolved_THEN_the_child_joins_the_lovers_line()
        {
            LoverChildClanPolicy.Resolve(new LoverChildClanPolicy.LoverChildFacts {LoverHasClan = true, LoverClanIsPlayer = false, MotherHasClan = true, MotherClanIsPlayer = true})
                                .Should().Be(LoverChildClanTarget.LoverClan,
                                    "a lover-child belongs to its genetic father's house, even when the mother lives in the player's clan");
        }

        // The critical case: a wife married INTO the player's clan whose lover is (implausibly) clanless. The
        // mother's clan IS the player's, so the fallback must REFUSE it. This is the exact regression the whole
        // increment guards: the child must not land in the player's house just because its mother lives there.
        [Test]
        public void GIVEN_a_clanless_lover_and_a_mother_in_the_player_clan_WHEN_resolved_THEN_neither_line_is_used()
        {
            LoverChildClanPolicy.Resolve(new LoverChildClanPolicy.LoverChildFacts {LoverHasClan = false, LoverClanIsPlayer = false, MotherHasClan = true, MotherClanIsPlayer = true})
                                .Should().Be(LoverChildClanTarget.LeaveAsIs,
                                    "a lover-child must NEVER join the player's clan, not even via the mother's-clan fallback");
        }

        // The fallback proper: the lover has no usable clan but the mother has her OWN house (not the player's),
        // so the child follows her line rather than being left clanless. Confirms the fallback fires only for a
        // non-player maternal clan.
        [Test]
        public void GIVEN_a_clanless_lover_and_a_mother_in_her_own_non_player_clan_WHEN_resolved_THEN_the_child_joins_the_mothers_line()
        {
            LoverChildClanPolicy.Resolve(new LoverChildClanPolicy.LoverChildFacts {LoverHasClan = false, LoverClanIsPlayer = false, MotherHasClan = true, MotherClanIsPlayer = false})
                                .Should().Be(LoverChildClanTarget.MotherClan,
                                    "with no lover-clan, a non-player maternal clan is the safe fallback line");
        }

        // Defensive: even if the lover's clan were somehow read as the player's clan (it should not be, the lover
        // is selected outside the player's household), the lover-clan branch must not fire and adopt the child.
        // Falls through to the mother, and here she too is in the player's clan, so neither line is used.
        [Test]
        public void GIVEN_the_lovers_clan_reads_as_the_player_clan_WHEN_resolved_THEN_it_is_not_used()
        {
            LoverChildClanPolicy.Resolve(new LoverChildClanPolicy.LoverChildFacts {LoverHasClan = true, LoverClanIsPlayer = true, MotherHasClan = true, MotherClanIsPlayer = true})
                                .Should().Be(LoverChildClanTarget.LeaveAsIs,
                                    "the player's clan is never a valid destination for a lover-child, from either parent");
        }

        // The bare corner: neither parent has any clan at all. There is no line to assign, so the child is left
        // where the engine placed it (the host still guarantees that is never the player's clan).
        [Test]
        public void GIVEN_neither_parent_has_a_clan_WHEN_resolved_THEN_the_child_is_left_as_is()
        {
            LoverChildClanPolicy.Resolve(new LoverChildClanPolicy.LoverChildFacts {LoverHasClan = false, LoverClanIsPlayer = false, MotherHasClan = false, MotherClanIsPlayer = false})
                                .Should().Be(LoverChildClanTarget.LeaveAsIs,
                                    "with no clan on either parent there is no line to join");
        }
    }
}
