// Code written by Gabriel Mailhot, 05/08/2026.
// Focused unit tests for the pure decision core of the engine-registry initialization guardrail
// (EngineRegistryGuard). The whole-assembly IL scan in EngineRegistryInitializationGuardTests only reports the
// truth if this classifier is right, so these pin its two edges directly: a blacklisted registry among safe
// references IS flagged, and engine-agnostic references are NOT. See EngineRegistryGuard's header for the full
// 2026-08-05 CouncilWardBehavior / DefaultSkills save-load crash this whole guardrail exists to prevent.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Guardrails
{
    [TestFixture]
    public sealed class EngineRegistryGuardTests
    {
        // THE STAKE: this is the exact shape of the crash bug. A ctor that references DefaultSkills alongside
        // ordinary framework types must be flagged; if the classifier missed it because safe names surround the
        // dangerous one, the scan would wave the real landmine through.
        [Test]
        public void GIVEN_a_constructor_referencing_DefaultSkills_amid_safe_types_WHEN_classified_THEN_DefaultSkills_is_flagged()
        {
            var referenced = new[] { "String", "List`1", "DefaultSkills", "Object" };

            IReadOnlyList<string> hits = EngineRegistryGuard.FindReferencedUnsafeRegistries(referenced);

            hits.Should().ContainSingle().Which.Should().Be("DefaultSkills");
        }

        // THE STAKE: the guardrail must not cry wolf. A constructor touching only engine-agnostic types is safe by
        // definition, and a false positive there would train Gabriel to ignore the guardrail, which is worse than
        // not having it.
        [Test]
        public void GIVEN_a_constructor_referencing_only_engine_agnostic_types_WHEN_classified_THEN_nothing_is_flagged()
        {
            var referenced = new[] { "String", "List`1", "JsonConvert", "Hero", "NpcProfile" };

            IReadOnlyList<string> hits = EngineRegistryGuard.FindReferencedUnsafeRegistries(referenced);

            hits.Should().BeEmpty();
        }

        // THE STAKE: the crash family is not only the Default* registries. Reading Campaign.Current / Game.Current
        // / MBObjectManager.Instance from a ctor is the same load-order landmine (null or half-built until the
        // game is up), so each declaring type must be recognised on its own.
        [TestCase("Campaign")]
        [TestCase("Game")]
        [TestCase("MBObjectManager")]
        [TestCase("DefaultTraits")]
        [TestCase("DefaultItems")]
        public void GIVEN_a_process_global_or_registry_type_name_WHEN_asked_THEN_it_is_unsafe(string typeName)
        {
            EngineRegistryGuard.IsUnsafeRegistry(typeName).Should().BeTrue();
        }

        // THE STAKE: matching is by exact simple name, so ordinary types (and empty / null operands the scanner
        // will hand over for non-member opcodes) must read as safe, or the scan would flag noise.
        [TestCase("Campaigner")]
        [TestCase("GameState")]
        [TestCase("")]
        [TestCase(null)]
        public void GIVEN_a_non_blacklisted_or_empty_name_WHEN_asked_THEN_it_is_safe(string? typeName)
        {
            EngineRegistryGuard.IsUnsafeRegistry(typeName).Should().BeFalse();
        }
    }
}
