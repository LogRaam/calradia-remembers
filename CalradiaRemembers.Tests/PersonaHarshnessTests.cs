// Code written by Gabriel Mailhot, 02/10/2026.
// tashmetu (Nexus, 02/10/2026): "A tester saw a companion's sister come out very cruel, beyond what her traits
// suggest. Is that the model, or something in how the persona is built?" It was the persona. HeroProfileMapper picks
// an archetype from the COMBINATION of a hero's traits, and three ordinary -1 traits (Generosity, Honor, Mercy)
// drew "Sadistic. Takes pleasure in the suffering of others... A predator wearing a lord's title", which the prompt
// then tells the model not to soften. A -1 trait is a leaning: on its own, Mercy -1 already reads "Not needlessly
// cruel; simply unmoved". If these guards fail, an ordinary hard character is played as a monster again, or a save
// keeps the old wording for good.

#region

using System.Linq;
using System.Text.RegularExpressions;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PersonaHarshnessTests
   {
      // No archetype tells the model its character relishes cruelty: that goes beyond what any trait combination says.
      [Test]
      public void GIVEN_the_persona_catalog_WHEN_read_THEN_no_archetype_says_the_character_enjoys_suffering()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         string catalog = SourceTree.Production.Single(f => f.RelativePath.EndsWith("HeroProfileMapper.cs")).Text;
         var spoken = Regex.Matches(catalog, "return \"([^\"]*)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();

         spoken.Should().NotContain(s => Regex.IsMatch(s, @"pleasure|Sadistic|Predator|Butcher|Savage|predator", RegexOptions.None),
                                    "a trait combination is a leaning, never an appetite for suffering");
      }

      // A retired archetype must never be handed out again, or every load would "upgrade" a profile to it in a loop.
      [Test]
      public void GIVEN_the_retired_archetypes_WHEN_the_catalog_is_read_THEN_none_of_them_is_still_handed_out()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         string catalog = SourceTree.Production.Single(f => f.RelativePath.EndsWith("HeroProfileMapper.cs")).Text;

         RetiredArchetypes.Names.Should().NotContain(name => catalog.Contains($"return \"{name}"));
      }

      // A save made before the rewording keeps the persona it stored: it is recognised, so the next load re-derives it.
      [Test]
      public void GIVEN_a_stored_persona_with_a_retired_archetype_WHEN_checked_THEN_it_is_due_for_the_new_wording()
      {
         RetiredArchetypes.IsRetired("Sadistic. Takes pleasure in the suffering of others, breaks oaths without guilt.").Should().BeTrue();
      }

      // A current archetype, or an authored persona, is left alone.
      [TestCase("The Small Tyrant. Dishonest, cruel, and tight-fisted.")]
      [TestCase("Hard. Shows no sentiment for those who suffer.")]
      [TestCase("")]
      [TestCase(null)]
      public void GIVEN_a_current_persona_WHEN_checked_THEN_it_is_left_alone(string trait)
      {
         RetiredArchetypes.IsRetired(trait).Should().BeFalse();
      }
   }
}
