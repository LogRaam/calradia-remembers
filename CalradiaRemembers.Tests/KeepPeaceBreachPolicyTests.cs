// Code written by Gabriel Mailhot, 06/08/2026.
// Guards the v1.34.1 hotfix for swear_oath's "keep the peace" kind. The shipped v1.34.0 breach check marked
// the oath broken whenever the promisor's faction and the named house ended up at war, without ever reading
// WHO DECLARED it, so a lord who swore peace and was then INVADED was branded an oathbreaker for the
// aggression committed against him. KeepPeaceBreachPolicy decides the breach purely: a keep-peace oath breaks
// ONLY when the promisor's own faction was the aggressor (the declarer). If this rule drifts, the exploit
// returns (a victim of invasion loses relation, eats a BrokenOath grudge and a per-witness cost for a war
// they never chose) or, the opposite failure, a genuine aggressor escapes their broken oath entirely.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class KeepPeaceBreachPolicyTests
    {
        // The baseline the oath exists to punish: the promisor's OWN faction marched on the very house they
        // swore to keep the peace with. This must breach, or a lord could swear peace at the council table,
        // then invade with total impunity.
        [Test]
        public void GIVEN_the_promisor_declared_war_on_the_oath_house_WHEN_judging_THEN_it_breaches()
        {
            KeepPeaceBreachPolicy.ShouldBreach(new WarSides("sturgia", "vlandia"), promisorFactionId: "sturgia", oathFactionId: "vlandia").Should().BeTrue();
        }

        // THE EXPLOIT THIS HOTFIX FIXES: the oath house invaded the promisor. The promisor is the victim, not
        // an oathbreaker; branding him one (relation crater + BrokenOath grudge + witness cost) for being
        // attacked is the exact wrong-party punishment the v1.34.0 check inflicted. Must NOT breach.
        [Test]
        public void GIVEN_the_oath_house_invaded_the_promisor_WHEN_judging_THEN_it_does_NOT_breach()
        {
            // Vlandia (the oath house) is the declarer; Sturgia (the promisor) is the defender/victim.
            KeepPeaceBreachPolicy.ShouldBreach(new WarSides("vlandia", "sturgia"), promisorFactionId: "sturgia", oathFactionId: "vlandia").Should().BeFalse();
        }

        // The oath named ONE specific house. The promisor declaring war on a DIFFERENT faction is not a breach
        // of a peace sworn with Vlandia; punishing it would fabricate an oathbreaking that never happened.
        [Test]
        public void GIVEN_the_promisor_declared_war_on_a_different_house_WHEN_judging_THEN_it_does_NOT_breach()
        {
            KeepPeaceBreachPolicy.ShouldBreach(new WarSides("sturgia", "battania"), promisorFactionId: "sturgia", oathFactionId: "vlandia").Should().BeFalse();
        }

        // WarDeclared fires for EVERY war in the world. A war between two houses the promisor is not part of
        // must leave his oath untouched; a stray breach here would punish a lord for a war on the far side of
        // Calradia he had no hand in.
        [Test]
        public void GIVEN_a_war_between_two_unrelated_houses_WHEN_judging_THEN_it_does_NOT_breach()
        {
            KeepPeaceBreachPolicy.ShouldBreach(new WarSides("battania", "khuzait"), promisorFactionId: "sturgia", oathFactionId: "vlandia").Should().BeFalse();
        }

        // Faction ids surface from different sources (the saved oath's TargetId vs. live WarDeclared factions)
        // and their casing is not guaranteed to match; a case-sensitive bug would silently let a real
        // aggressor's broken oath slip through.
        [Test]
        public void GIVEN_case_differences_WHEN_judging_THEN_matching_is_case_insensitive()
        {
            KeepPeaceBreachPolicy.ShouldBreach(new WarSides("STURGIA", "vlandia"), "Sturgia", "Vlandia").Should().BeTrue();
        }

        // A blank or unresolved id (a malformed oath, or a faction lookup that came back empty) must fail
        // closed: on ambiguity the policy never breaches, because a false "closed ranks" oathbreaker
        // accusation is worse than a missed breach.
        [Test]
        public void GIVEN_a_missing_id_WHEN_judging_THEN_it_does_NOT_breach()
        {
            KeepPeaceBreachPolicy.ShouldBreach(new WarSides("sturgia", "vlandia"), null, "vlandia").Should().BeFalse();
            KeepPeaceBreachPolicy.ShouldBreach(new WarSides("sturgia", "  "), "sturgia", "vlandia").Should().BeFalse();
        }
    }
}
