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

      [Test]
      public void GIVEN_a_monogamous_culture_WHEN_asked_at_any_tier_THEN_it_never_tolerates_sharing()
      {
         //Arrange
         JealousyCulture culture = JealousyCulture.Monogamous;

         //Act/Assert
         culture.ToleratesSharedPartners(0).Should().BeFalse();
         culture.ToleratesSharedPartners(6).Should().BeFalse("a high-outrage culture treats a new partner as betrayal at every rank");
      }

      [Test]
      public void GIVEN_a_permissive_culture_at_or_above_the_elite_tier_WHEN_asked_THEN_it_tolerates_sharing()
      {
         //Arrange
         JealousyCulture culture = JealousyCulture.FromRaw("Low", polygamyEliteTier: 2, "Low", false, "");

         //Act/Assert
         culture.ToleratesSharedPartners(2).Should().BeTrue("the elite tier gate is inclusive at its edge");
         culture.ToleratesSharedPartners(4).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_permissive_culture_below_the_elite_tier_WHEN_asked_THEN_it_behaves_monogamously()
      {
         //Arrange — historically polygamy was concentrated among the elite.
         JealousyCulture culture = JealousyCulture.FromRaw("Low", polygamyEliteTier: 2, "Low", false, "");

         //Act/Assert
         culture.ToleratesSharedPartners(1).Should().BeFalse("a commoner of a permissive culture still expects exclusivity");
      }

      [Test]
      public void GIVEN_a_high_outrage_culture_above_the_tier_gate_WHEN_asked_THEN_it_still_refuses_to_share()
      {
         //Arrange — both conditions are required; outrage alone vetoes tolerance.
         JealousyCulture culture = JealousyCulture.FromRaw("High", polygamyEliteTier: 0, "Low", false, "");

         //Act/Assert
         culture.ToleratesSharedPartners(6).Should().BeFalse();
      }

      // ── Raw-value parsing (safe degradation) ───────────────────────────────

      [Test]
      public void GIVEN_outrage_strings_in_any_casing_WHEN_parsed_THEN_they_map_to_the_right_level()
      {
         //Arrange/Act/Assert
         JealousyCulture.FromRaw("low", 0, "Low", false, "").Outrage.Should().Be(JealousyOutrage.Low);
         JealousyCulture.FromRaw("MEDIUM", 0, "Low", false, "").Outrage.Should().Be(JealousyOutrage.Medium);
         JealousyCulture.FromRaw("High", 0, "Low", false, "").Outrage.Should().Be(JealousyOutrage.High);
      }

      [Test]
      public void GIVEN_an_unrecognised_or_null_outrage_WHEN_parsed_THEN_it_degrades_to_monogamous()
      {
         //Arrange/Act/Assert
         JealousyCulture.FromRaw("nonsense", 0, "Low", false, "").Outrage.Should().Be(JealousyOutrage.High);
         JealousyCulture.FromRaw(null, 0, "Low", false, "").Outrage.Should().Be(JealousyOutrage.High,
            "a mistyped or missing culture must fail safe toward exclusivity, never toward permissiveness");
      }

      [Test]
      public void GIVEN_hierarchy_strings_WHEN_parsed_THEN_only_high_and_medium_are_recognised()
      {
         //Arrange/Act/Assert
         JealousyCulture.FromRaw("Low", 0, "High", false, "").Hierarchy.Should().Be(JealousyHierarchy.High);
         JealousyCulture.FromRaw("Low", 0, "medium", false, "").Hierarchy.Should().Be(JealousyHierarchy.Medium);
         JealousyCulture.FromRaw("Low", 0, "anything-else", false, "").Hierarchy.Should().Be(JealousyHierarchy.Low);
      }

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
