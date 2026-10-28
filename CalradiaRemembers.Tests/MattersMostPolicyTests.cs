// Code written by Gabriel Mailhot, 27/10/2026.
// MattersMostPolicy chooses what matters most to a character among what is true of them, keeps it, and moves it only
// when the world takes its ground away (Gabriel, 27/10/2026: "qu'est-ce qui compte le plus pour un NPC. Ses enfants? Sa
// richesse? Son amoureux?"). If it is wrong, a childless lord cherishes his children, what someone cherishes changes
// from one reading to the next, a mother whose child was killed goes on cherishing a dead child, or the encyclopedia
// tells the player of a lover they were never meant to know about.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Character;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MattersMostPolicyTests
   {
      private static IEnumerable<string> Heroes => Enumerable.Range(0, 1000).Select(i => "lord_" + i);

      private static MattersMostFacts Lord(string id, bool children = true, bool spouse = true, int valor = 0, int generosity = 0, int mercy = 0,
                                           bool scoreToSettle = false)
         => new() {
            HeroId = id, Traits = new RomanticTraits {Valor = valor, Generosity = generosity, Mercy = mercy}, HasChildren = children, HasSpouse = spouse,
            HasKin = true, IsNoble = true, HasScoreToSettle = scoreToSettle
         };

      // STAKE: Gabriel's own examples: "Ses enfants? Sa richesse? Son amoureux?": one who cherishes their children has
      // children; one with no lover never cherishes a lover; a wanderer's freedom is never a lord's.
      [Test]
      public void GIVEN_many_characters_WHEN_chosen_THEN_what_matters_most_is_always_true_of_them()
      {
         foreach (string id in Heroes)
         {
            MattersMost m = MattersMostPolicy.Resolve(MattersMost.None, Lord(id, children: false, spouse: false));
            m.Should().NotBe(MattersMost.Children).And.NotBe(MattersMost.Spouse).And.NotBe(MattersMost.Lover).And.NotBe(MattersMost.ThePlayer)
             .And.NotBe(MattersMost.Freedom).And.NotBe(MattersMost.Vengeance).And.NotBe(MattersMost.None);
         }
      }

      // STAKE: the same character cherishes the same thing on every reading and every run.
      [Test]
      public void GIVEN_the_same_character_WHEN_chosen_twice_THEN_it_is_the_same()
      {
         foreach (string id in Heroes.Take(100))
            MattersMostPolicy.Resolve(MattersMost.None, Lord(id)).Should().Be(MattersMostPolicy.Resolve(MattersMost.None, Lord(id)));
      }

      // STAKE: "les traits influencent": the generous and merciful cherish their children more often, the valorous glory.
      [Test]
      public void GIVEN_traits_WHEN_chosen_THEN_they_weigh_the_choice()
      {
         double Share(MattersMost m, int valor, int generosity, int mercy)
            => Heroes.Count(id => MattersMostPolicy.Resolve(MattersMost.None, Lord(id, valor: valor, generosity: generosity, mercy: mercy)) == m) / 1000.0;

         Share(MattersMost.Children, 0, 2, 2).Should().BeGreaterThan(Share(MattersMost.Children, 0, -2, -2) * 2);
         Share(MattersMost.Glory, 2, 0, 0).Should().BeGreaterThan(Share(MattersMost.Glory, -2, 0, 0) * 2);
      }

      // STAKE: what they cherish holds while its ground stands: a reading never redraws it.
      [Test]
      public void GIVEN_an_anchor_still_grounded_WHEN_read_again_THEN_it_stays()
      {
         MattersMostPolicy.Resolve(MattersMost.Glory, Lord("lord_3", valor: -2)).Should().Be(MattersMost.Glory);
         MattersMostPolicy.Resolve(MattersMost.Children, Lord("lord_3", scoreToSettle: true)).Should().Be(MattersMost.Children);
      }

      // STAKE: a mother whose child was killed, holding a grudge, now lives for a score to settle; without one, she finds
      // another anchor, never the lost one.
      [Test]
      public void GIVEN_the_cherished_one_lost_WHEN_read_THEN_vengeance_if_they_hold_a_grudge_else_another_anchor()
      {
         MattersMostPolicy.Resolve(MattersMost.Children, Lord("lord_9", children: false, scoreToSettle: true)).Should().Be(MattersMost.Vengeance);

         foreach (string id in Heroes.Take(200))
         {
            MattersMost after = MattersMostPolicy.Resolve(MattersMost.Children, Lord(id, children: false));
            after.Should().NotBe(MattersMost.Children).And.NotBe(MattersMost.None).And.NotBe(MattersMost.Vengeance);
         }
      }

      // STAKE: nothing on the page until the player learned it from them.
      [Test]
      public void GIVEN_it_not_yet_learned_WHEN_her_page_opens_THEN_nothing_is_said()
      {
         MattersMostPolicy.PageLine(new MattersMostPageFacts {MattersMost = MattersMost.Children, Known = false, IsFemale = true}).Should().BeNull();
         MattersMostPolicy.PageLine(new MattersMostPageFacts {MattersMost = MattersMost.Children, Known = true, IsFemale = true})
                          .Should().Be("What matters most to her: her children.");
      }

      // STAKE: the encyclopedia never leaks a secret lover: unknown to the player, it reads "someone she loves".
      [Test]
      public void GIVEN_a_lover_the_player_does_not_know_of_WHEN_her_page_opens_THEN_the_lover_is_not_named_as_such()
      {
         MattersMostPolicy.PageLine(new MattersMostPageFacts {MattersMost = MattersMost.Lover, Known = true, IsFemale = true})
                          .Should().Be("What matters most to her: someone she loves.");
         MattersMostPolicy.PageLine(new MattersMostPageFacts {MattersMost = MattersMost.Lover, Known = true, IsFemale = true, LoverKnownToPlayer = true})
                          .Should().Be("What matters most to her: her lover.");
      }

      // STAKE: every anchor has its words on the page, and the player who is loved reads "you".
      [Test]
      public void GIVEN_every_anchor_WHEN_learned_THEN_his_page_names_it()
      {
         foreach (MattersMost m in System.Enum.GetValues(typeof(MattersMost)).Cast<MattersMost>().Where(m => m != MattersMost.None))
            MattersMostPolicy.PageLine(new MattersMostPageFacts {MattersMost = m, Known = true}).Should().StartWith("What matters most to him: ", m.ToString());

         MattersMostPolicy.PageLine(new MattersMostPageFacts {MattersMost = MattersMost.ThePlayer, Known = true}).Should().Be("What matters most to him: you.");
      }
   }
}
