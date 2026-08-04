// Code written by Gabriel Mailhot, 21/07/2026.
// Decides which seated council member an [ACTION]'s "actor" name refers to, so the action-dispatch loop can
// execute a non-anchor member's own promise against THEM instead of always the anchor who carries the turn's
// spoken reply. WHY IT MATTERS: a wrong guess here is worse than dropping the action outright (the wrong hero's
// gold, the wrong hero married off), so every ambiguous or unrecognized case must resolve to -1, never a guess.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilActorPolicyTests
    {
        // The straight case: the model names a seated member exactly. If this failed, an unambiguous
        // attribution would still be dropped, defeating the whole feature on its simplest input.
        [Test]
        public void GIVEN_an_exact_name_match_WHEN_resolving_THEN_that_seat_is_returned()
        {
            CouncilActorPolicy.ResolveActor("Sley", new List<string> {"Sley", "Aldric"}).Should().Be(0);
        }

        // LLM output casing is not reliable; a model that writes "sley" must resolve exactly as "Sley" would,
        // or a routine casing quirk would silently drop deeds that were never actually ambiguous.
        [Test]
        public void GIVEN_a_case_difference_WHEN_resolving_THEN_the_seat_still_matches()
        {
            CouncilActorPolicy.ResolveActor("sley", new List<string> {"Sley", "Aldric"}).Should().Be(0);
        }

        // The model sometimes echoes the parenthetical role the prompt printed next to a name (the same habit
        // DialogueHeuristics.StripWitnessRole guards against for witness reactions): the actor line must not
        // fail to resolve just because the model was verbose about it.
        [Test]
        public void GIVEN_a_parenthetical_role_suffix_WHEN_resolving_THEN_the_role_is_stripped_before_matching()
        {
            CouncilActorPolicy.ResolveActor("Sley (a companion in the player's service)",
                new List<string> {"Sley", "Aldric"}).Should().Be(0);
        }

        // A model that answers with only a first name must still resolve when it fits exactly one seat:
        // the seated names carried into the prompt may be full names ("Aldric Stone"), but nothing requires
        // the model to echo them verbatim.
        [Test]
        public void GIVEN_a_first_name_only_match_WHEN_resolving_THEN_the_single_fitting_seat_is_returned()
        {
            CouncilActorPolicy.ResolveActor("Aldric", new List<string> {"Aldric Stone", "Sley"}).Should().Be(0);
        }

        // The case this policy exists to refuse: a fragment that fits two seats equally. A guess here would
        // mean gold (or worse, a vow) landing on the wrong hero, silently dropping the action is the only
        // safe outcome.
        [Test]
        public void GIVEN_an_ambiguous_fragment_matching_two_members_WHEN_resolving_THEN_it_is_refused()
        {
            CouncilActorPolicy.ResolveActor("Aldric",
                new List<string> {"Aldric Stone", "Aldric Younger"}).Should().Be(-1);
        }

        // An exact full-name match must win outright even when the SAME text would also substring-match a
        // different seat (a shorter name that happens to sit inside a longer one): the model gave the whole
        // name, which is the strongest signal available, so it must not be second-guessed into a false
        // ambiguity.
        [Test]
        public void GIVEN_an_exact_match_that_is_also_a_substring_of_another_seat_WHEN_resolving_THEN_the_exact_seat_wins()
        {
            CouncilActorPolicy.ResolveActor("Mira", new List<string> {"Mira", "Miranda"}).Should().Be(0);
        }

        // Naming someone who is simply not at this table must never fall back to the anchor: the whole point
        // of attribution is that an unrecognized actor is a DROP signal, not a shrug back to "whoever is
        // speaking".
        [Test]
        public void GIVEN_an_actor_naming_nobody_seated_WHEN_resolving_THEN_it_is_refused()
        {
            CouncilActorPolicy.ResolveActor("Someone Else", new List<string> {"Sley", "Aldric"}).Should().Be(-1);
        }

        // Guards the caller: a null/blank actor (the ordinary, non-council case, where the model never emits
        // one at all) must resolve to -1 without throwing, so the dispatch loop's "no actor = today's
        // behaviour" branch has a clean signal to check against.
        [Test]
        public void GIVEN_a_null_or_blank_actor_WHEN_resolving_THEN_it_is_refused_without_throwing()
        {
            CouncilActorPolicy.ResolveActor(null, new List<string> {"Sley"}).Should().Be(-1);
            CouncilActorPolicy.ResolveActor("", new List<string> {"Sley"}).Should().Be(-1);
            CouncilActorPolicy.ResolveActor("   ", new List<string> {"Sley"}).Should().Be(-1);
        }

        // No seats at all (an empty or null roster) must also resolve cleanly to -1 rather than throw, since
        // the caller may pass this before the roster is fully built.
        [Test]
        public void GIVEN_no_seated_members_WHEN_resolving_THEN_it_is_refused_without_throwing()
        {
            CouncilActorPolicy.ResolveActor("Sley", new List<string>()).Should().Be(-1);
            CouncilActorPolicy.ResolveActor("Sley", null).Should().Be(-1);
        }

        // declare_war's own carve-out: the pledge is the table's decision about two FACTIONS, not one seated
        // member's promise, so nobody at the table is naturally "the actor". An omitted actor for THIS kind
        // must default to the presiding member (seat 0) rather than being dropped, or the war council could
        // never actually record a decision when the model (correctly) names nobody in particular.
        [Test]
        public void GIVEN_a_blank_actor_WHEN_resolving_a_declare_war_actor_THEN_the_presiding_seat_is_returned()
        {
            CouncilActorPolicy.ResolveDeclareWarActor(null, new List<string> {"Sley", "Aldric"}).Should().Be(0);
            CouncilActorPolicy.ResolveDeclareWarActor("", new List<string> {"Sley", "Aldric"}).Should().Be(0);
            CouncilActorPolicy.ResolveDeclareWarActor("   ", new List<string> {"Sley", "Aldric"}).Should().Be(0);
        }

        // A NAMED actor is resolved exactly like any other kind: this carve-out only widens what an OMITTED
        // actor means, it never loosens the "never guess" rule for a name that was actually given.
        [Test]
        public void GIVEN_a_named_actor_WHEN_resolving_a_declare_war_actor_THEN_it_resolves_like_any_other_kind()
        {
            CouncilActorPolicy.ResolveDeclareWarActor("Aldric", new List<string> {"Sley", "Aldric"}).Should().Be(1);
        }

        // An actor naming somebody not seated must still refuse: the omitted-actor default is narrowly for a
        // BLANK actor, never a signal to fall back on a hallucinated name.
        [Test]
        public void GIVEN_an_unmatched_named_actor_WHEN_resolving_a_declare_war_actor_THEN_it_is_still_refused()
        {
            CouncilActorPolicy.ResolveDeclareWarActor("Someone Else", new List<string> {"Sley", "Aldric"}).Should().Be(-1);
        }

        // No seats at all must still refuse cleanly rather than default to a nonexistent seat 0.
        [Test]
        public void GIVEN_no_seated_members_WHEN_resolving_a_declare_war_actor_THEN_it_is_refused_without_throwing()
        {
            CouncilActorPolicy.ResolveDeclareWarActor(null, new List<string>()).Should().Be(-1);
            CouncilActorPolicy.ResolveDeclareWarActor(null, null).Should().Be(-1);
        }
    }
}
