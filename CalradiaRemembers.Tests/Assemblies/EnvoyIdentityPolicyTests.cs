// Code written by Gabriel Mailhot, 29/08/2026.
// Documents the wording rule for a synthetic ambassador's identity: the display name and persona line the player
// and the LLM actually see. The player report this whole convergence traces back to is a council that "felt
// magic" (a stand-in appearing as if it were the real lord); if the display name or persona ever failed to name
// the represented faction/leader, or leaked a raw "null"/empty placeholder from a missing name, the seat would
// either read as the wrong person or as visibly broken. Both DisplayName and Persona are pure string composition,
// so every boundary (blank, null, both present) is exercised here rather than left to be noticed in a live scene.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class EnvoyIdentityPolicyTests
    {
        // ── DisplayName ──────────────────────────────────────────────────────

        // The whole point of the "Herald of {Faction}" format: the player must be able to tell at a glance that
        // this is a stand-in for another realm, not a real lord of their own or anyone else's house. Losing the
        // faction name from the seat's label is exactly the "felt magic" failure this convergence exists to close.
        [Test]
        public void GIVEN_a_faction_name_WHEN_building_the_display_name_THEN_it_reads_herald_of_that_faction()
        {
            string name = EnvoyIdentityPolicy.DisplayName("Vlandia");

            name.Should().Be("Herald of Vlandia");
        }

        // A council seat is rendered from live text; a name with incidental leading/trailing whitespace (a
        // faction display string assembled elsewhere) must not leak into the player-facing label as a stray gap.
        [Test]
        public void GIVEN_a_faction_name_with_surrounding_whitespace_WHEN_building_the_display_name_THEN_it_is_trimmed()
        {
            string name = EnvoyIdentityPolicy.DisplayName("  Sturgia  ");

            name.Should().Be("Herald of Sturgia");
        }

        // STAKE: a null faction (an unresolvable IFaction, or a projection that lost the name) must never produce
        // "Herald of " with nothing after it, or a literal "Herald of null" (either reads as a broken seat,
        // rather than an ambassador), and the player cannot tell who is supposed to be at the table.
        [Test]
        public void GIVEN_a_null_faction_name_WHEN_building_the_display_name_THEN_it_degrades_to_the_generic_fallback_without_emitting_null()
        {
            string name = EnvoyIdentityPolicy.DisplayName(null);

            name.Should().Be("A foreign herald");
            name.Should().NotContain("null");
        }

        // Same failure mode as null, reached via an empty/whitespace-only string instead (e.g. a faction whose
        // Name resolved to "" rather than null): the seat must still read as unmistakably a herald, not blank.
        [Test]
        public void GIVEN_a_blank_faction_name_WHEN_building_the_display_name_THEN_it_degrades_to_the_generic_fallback()
        {
            string name = EnvoyIdentityPolicy.DisplayName("   ");

            name.Should().Be("A foreign herald");
        }

        // ── Persona ──────────────────────────────────────────────────────────

        // The persona line is what the LLM reads to play the herald's constrained role instead of the lord's own
        // voice. If it does not name BOTH the faction and the leader, the model has nothing to ground "their name"
        // and "their words" in, and risks answering as the lord himself rather than a stand-in who binds nothing.
        [Test]
        public void GIVEN_a_faction_and_leader_name_WHEN_building_the_persona_THEN_it_names_both_and_states_it_binds_nothing()
        {
            string persona = EnvoyIdentityPolicy.Persona("Vlandia", "Derthert");

            persona.Should().Contain("Vlandia");
            persona.Should().Contain("Derthert");
            persona.Should().Contain("bind nothing by your own authority");
        }

        // STAKE: a leader who could not be resolved (e.g. an interregnum, or a projection that only had the
        // faction) must not produce "sent by  of Vlandia" (a leaked double space) or a literal "null" in the
        // leader's place: the sentence must still read as a complete, in-character line.
        [Test]
        public void GIVEN_a_faction_name_but_no_leader_name_WHEN_building_the_persona_THEN_it_degrades_without_emitting_null_or_a_double_space()
        {
            string persona = EnvoyIdentityPolicy.Persona("Vlandia", null);

            persona.Should().Contain("Vlandia");
            persona.Should().NotContain("null");
            persona.Should().NotContain("  ");
        }

        // The mirror case: a leader name resolved (perhaps from the represented Hero directly) but the faction
        // name did not. The persona must still read as a complete sentence naming who sent the herald.
        [Test]
        public void GIVEN_a_leader_name_but_no_faction_name_WHEN_building_the_persona_THEN_it_degrades_without_emitting_null()
        {
            string persona = EnvoyIdentityPolicy.Persona(null, "Derthert");

            persona.Should().Contain("Derthert");
            persona.Should().NotContain("null");
        }

        // STAKE: both names missing at once (the worst-case projection failure) must still produce a coherent,
        // in-character sentence rather than a crash or a visibly templated gap, so an ambassador seat can never
        // surface broken text to the player even when every upstream lookup failed.
        [Test]
        public void GIVEN_both_faction_and_leader_name_missing_WHEN_building_the_persona_THEN_it_degrades_to_a_generic_but_coherent_sentence()
        {
            string persona = EnvoyIdentityPolicy.Persona(null, null);

            persona.Should().NotContain("null");
            persona.Should().Contain("bind nothing by your own authority");
            persona.Should().StartWith("A rankless herald");
        }
    }
}
