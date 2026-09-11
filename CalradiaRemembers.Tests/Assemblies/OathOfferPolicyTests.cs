// Code written by Gabriel Mailhot, 05/08/2026.
// OathOfferPolicy is the 1:1 conversation TEACHING gate for swear_oath, brought to the 1:1 pipeline alongside the
// council on 2026-08-05. What it decides: whether the LLM is even SHOWN the swear_oath action this turn, from the
// plain engine facts the mod's EncounterContextBuilder reads about the ONE conversation NPC. What it feeds:
// PromptBuilder.AppendSwearOathOffer's gate. What breaks if it is wrong: it guards the empty-promise class the whole
// conditional-teaching contract exists to prevent. It mirrors the council's own swearer eligibility
// (ChatViewModel.AnySeatedMemberCanSwearOath): alive, free, not the player, no oath already outstanding (one vow at a
// time), and plausibly qualifying for at least one whitelisted oath kind. Every fact is re-checked at the bridge
// (CourtActionResolver.TrySwearOath), so a true answer here only ever OFFERS a teaching, never records an oath. Each
// fact below removes one way an NPC could otherwise "agree" to an oath the game then silently declines.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class OathOfferPolicyTests
    {
        // The all-clear case: a living, free NPC who is not the player, holds no oath yet, and qualifies for some
        // kind. If this did not offer, swear_oath would never be taught in 1:1 at all and the verb would stay
        // council-only, the exact regression this slice exists to fix.
        [Test]
        public void GIVEN_every_teaching_fact_holds_WHEN_offering_an_oath_THEN_the_action_is_taught()
        {
            OathOfferPolicy.CanOfferOath(new OathOfferPolicy.OathOfferFacts {NpcAlive = true, NpcIsPrisoner = false, NpcIsPlayer = false, NpcHasActiveCommitment = false, NpcQualifiesForSomeKind = true})
                .Should().BeTrue();
        }

        // A dead hero cannot swear anything: the bridge refuses (SwearerUnavailable). Guards the degenerate case so a
        // stale reference can never surface an oath offer.
        [Test]
        public void GIVEN_the_npc_is_dead_WHEN_offering_an_oath_THEN_it_is_withheld()
        {
            OathOfferPolicy.CanOfferOath(new OathOfferPolicy.OathOfferFacts {NpcAlive = false, NpcIsPrisoner = false, NpcIsPlayer = false, NpcHasActiveCommitment = false, NpcQualifiesForSomeKind = true})
                .Should().BeFalse();
        }

        // A prisoner is in no position to swear a binding vow, and the bridge refuses one (SwearerUnavailable).
        // Teaching it here would let a captive "agree" to an oath the game declines.
        [Test]
        public void GIVEN_the_npc_is_imprisoned_WHEN_offering_an_oath_THEN_it_is_withheld()
        {
            OathOfferPolicy.CanOfferOath(new OathOfferPolicy.OathOfferFacts {NpcAlive = true, NpcIsPrisoner = true, NpcIsPlayer = false, NpcHasActiveCommitment = false, NpcQualifiesForSomeKind = true})
                .Should().BeFalse();
        }

        // The oath is sworn TO the player; the player cannot swear one to themselves. The bridge excludes the player
        // (SwearerUnavailable), so the offer must withhold to match rather than teach a nonsensical self-oath.
        [Test]
        public void GIVEN_the_npc_is_the_player_WHEN_offering_an_oath_THEN_it_is_withheld()
        {
            OathOfferPolicy.CanOfferOath(new OathOfferPolicy.OathOfferFacts {NpcAlive = true, NpcIsPrisoner = false, NpcIsPlayer = true, NpcHasActiveCommitment = false, NpcQualifiesForSomeKind = true})
                .Should().BeFalse();
        }

        // One vow at a time: an NPC already carrying a commitment cannot swear a second (the same CommitmentBehavior
        // .HasActive gate pledge_against shares, re-checked at the bridge as AlreadyCommitted). Teaching it here would
        // let them "agree" to an oath the game then declines.
        [Test]
        public void GIVEN_the_npc_already_carries_an_oath_WHEN_offering_an_oath_THEN_it_is_withheld()
        {
            OathOfferPolicy.CanOfferOath(new OathOfferPolicy.OathOfferFacts {NpcAlive = true, NpcIsPrisoner = false, NpcIsPlayer = false, NpcHasActiveCommitment = true, NpcQualifiesForSomeKind = true})
                .Should().BeFalse();
        }

        // No whitelisted oath kind fits this NPC (no gold to pay, no party to fight with, no faction to keep peace
        // for): every kind the bridge could record would fail, so offering the verb is a guaranteed empty promise.
        // This mirrors AnySeatedMemberCanSwearOath's own "plausibly qualifies for at least one kind" disjunction.
        [Test]
        public void GIVEN_the_npc_qualifies_for_no_oath_kind_WHEN_offering_an_oath_THEN_it_is_withheld()
        {
            OathOfferPolicy.CanOfferOath(new OathOfferPolicy.OathOfferFacts {NpcAlive = true, NpcIsPrisoner = false, NpcIsPlayer = false, NpcHasActiveCommitment = false, NpcQualifiesForSomeKind = false})
                .Should().BeFalse();
        }
    }
}
