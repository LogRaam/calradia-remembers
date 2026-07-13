// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the heart of the jealousy design — whether a culture reads a new partner as BETRAYAL or
// merely COMPETITION — and the raw-value parsing that decides it. A silent regression here would
// turn every faction monogamous (or every faction permissive), unnoticed in game.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class JealousyCultureTests
   {
      // ── Tolerance (monogamy vs polygamy) ───────────────────────────────────

      // ToleratesSharedPartners feeds JealousyBehavior's competition-vs-betrayal branch and
      // CompetitionSelector's ToleratesSharing gate. If the default (monogamous) culture ever
      // tolerated sharing, betrayal-tier partners would misroute into the softer competition track,
      // undercutting exclusivity for the default and most factions (Vlandia, Empire, Sturgia).
      [Test]
      public void GIVEN_a_monogamous_culture_WHEN_asked_at_any_tier_THEN_it_never_tolerates_sharing()
      {
         //Arrange
         JealousyCulture culture = JealousyCulture.Monogamous;

         //Act/Assert
         culture.ToleratesSharedPartners(0).Should().BeFalse();
         culture.ToleratesSharedPartners(6).Should().BeFalse("a high-outrage culture treats a new partner as betrayal at every rank");
      }

      // Pins the PolygamyEliteTier gate as INCLUSIVE at its edge (the XML doc notes "historically
      // polygamy was concentrated among the elite"). An off-by-one here would deny tolerance to the
      // exact clan tier the culture was configured to allow it at.
      [Test]
      public void GIVEN_a_permissive_culture_at_or_above_the_elite_tier_WHEN_asked_THEN_it_tolerates_sharing()
      {
         //Arrange
         JealousyCulture culture = JealousyCulture.FromRaw("Low", polygamyEliteTier: 2, "Low", false, "");

         //Act/Assert
         culture.ToleratesSharedPartners(2).Should().BeTrue("the elite tier gate is inclusive at its edge");
         culture.ToleratesSharedPartners(4).Should().BeTrue();
      }

      // The far side of that same boundary: a commoner of a nominally permissive culture must still
      // read as exclusive, confirming the elite-only design intent isn't accidentally applied
      // mod-wide by outrage alone.
      [Test]
      public void GIVEN_a_permissive_culture_below_the_elite_tier_WHEN_asked_THEN_it_behaves_monogamously()
      {
         //Arrange — historically polygamy was concentrated among the elite.
         JealousyCulture culture = JealousyCulture.FromRaw("Low", polygamyEliteTier: 2, "Low", false, "");

         //Act/Assert
         culture.ToleratesSharedPartners(1).Should().BeFalse("a commoner of a permissive culture still expects exclusivity");
      }

      // ToleratesSharedPartners requires BOTH a non-high outrage AND the tier gate. Pins that tier
      // alone can never override a High-outrage stance: losing this AND would let a high-tier clan
      // in a strictly monogamous culture read as tolerant, wrongly softening what should be betrayal.
      [Test]
      public void GIVEN_a_high_outrage_culture_above_the_tier_gate_WHEN_asked_THEN_it_still_refuses_to_share()
      {
         //Arrange — both conditions are required; outrage alone vetoes tolerance.
         JealousyCulture culture = JealousyCulture.FromRaw("High", polygamyEliteTier: 0, "Low", false, "");

         //Act/Assert
         culture.ToleratesSharedPartners(6).Should().BeFalse();
      }

      // ── Raw-value parsing (safe degradation) ───────────────────────────────

      // FromRaw parses the moddable `ModuleData/jealousy_cultures.json` (per CHANGELOG, so faction
      // mods can define their own peoples' customs). Case-insensitivity keeps an author's casing
      // choice from silently degrading their culture's outrage level.
      [Test]
      public void GIVEN_outrage_strings_in_any_casing_WHEN_parsed_THEN_they_map_to_the_right_level()
      {
         //Arrange/Act/Assert
         JealousyCulture.FromRaw("low", 0, "Low", false, "").Outrage.Should().Be(JealousyOutrage.Low);
         JealousyCulture.FromRaw("MEDIUM", 0, "Low", false, "").Outrage.Should().Be(JealousyOutrage.Medium);
         JealousyCulture.FromRaw("High", 0, "Low", false, "").Outrage.Should().Be(JealousyOutrage.High);
      }

      // The documented fail-safe (JealousyCulture's own doc: "any culture not configured...
      // degrades safely to monogamy"). A modder's typo or an unlisted faction must never silently
      // become permissive, or spouses/rivals would stay unable to react to what the game clearly
      // modeled as a betrayal.
      [Test]
      public void GIVEN_an_unrecognised_or_null_outrage_WHEN_parsed_THEN_it_degrades_to_monogamous()
      {
         //Arrange/Act/Assert
         JealousyCulture.FromRaw("nonsense", 0, "Low", false, "").Outrage.Should().Be(JealousyOutrage.High);
         JealousyCulture.FromRaw(null, 0, "Low", false, "").Outrage.Should().Be(JealousyOutrage.High,
            "a mistyped or missing culture must fail safe toward exclusivity, never toward permissiveness");
      }

      // Hierarchy drives the v1.5 competition loop's senior-withdrawal consequence
      // (JealousyBehavior.FireCompetition checks Hierarchy == High). An unrecognised string must
      // degrade to Low (no estrangement) rather than accidentally triggering a senior partner
      // walking away from the bond.
      [Test]
      public void GIVEN_hierarchy_strings_WHEN_parsed_THEN_only_high_and_medium_are_recognised()
      {
         //Arrange/Act/Assert
         JealousyCulture.FromRaw("Low", 0, "High", false, "").Hierarchy.Should().Be(JealousyHierarchy.High);
         JealousyCulture.FromRaw("Low", 0, "medium", false, "").Hierarchy.Should().Be(JealousyHierarchy.Medium);
         JealousyCulture.FromRaw("Low", 0, "anything-else", false, "").Hierarchy.Should().Be(JealousyHierarchy.Low);
      }

      // PromptHint is concatenated directly into the LLM letter-context string
      // (JealousyReaction.BuildLetterContext appends culture.PromptHint). A null here would inject
      // a literal null reference or "null" text into the generated jealous letter's prompt.
      [Test]
      public void GIVEN_a_null_prompt_hint_WHEN_built_THEN_it_is_an_empty_string()
      {
         //Arrange/Act
         JealousyCulture culture = JealousyCulture.FromRaw("Low", 0, "Low", false, null);

         //Assert
         culture.PromptHint.Should().BeEmpty("the reaction prompt concatenates the hint and must never see null");
      }
   }
}
