// Code written by Gabriel Mailhot, 30/10/2026.
// Which realm has grown too strong, and how the others build a counterweight against it. Gabriel (29/10/2026): a lord's
// ambition includes "faire des arrangements avec d'autres factions plus faibles pour attaquer une faction plus
// puissante"; on 30/10/2026 he agreed the mod may have a house put the game's own decisions to its realm's vote (war,
// peace, alliance, a call to war). The game already proposes these at random; this policy aims them. If it is wrong,
// the strategists either push against a realm that is merely the largest of equals, pile wars on a realm already
// fighting on every side, or declare war on the dominant realm before looking for a single ally.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Strategy;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Strategy
{
   [TestFixture]
   public sealed class RealmCounterweightPolicyTests
   {
      private static RealmPower Realm(string id, float strength) => new() {Id = id, Strength = strength};

      private static List<CounterweightMove> Moves(CounterweightFacts f) => RealmCounterweightPolicy.Steps(f, "empire").Select(s => s.Move).ToList();

      // A realm far ahead of the next and holding a large share of all the might is the one the others must check.
      [Test]
      public void GIVEN_one_realm_far_ahead_of_the_rest_WHEN_the_balance_is_read_THEN_it_dominates()
      {
         RealmCounterweightPolicy.Dominant(new[] {Realm("empire", 4000f), Realm("vlandia", 2000f), Realm("sturgia", 1500f), Realm("aserai", 1200f)})
                                 .Should().Be("empire");
      }

      // The largest of near equals is no threat to the balance; without this the strategists would always gang up on
      // whoever happens to lead by a hair.
      [Test]
      public void GIVEN_the_largest_realm_only_slightly_ahead_WHEN_the_balance_is_read_THEN_none_dominates()
      {
         RealmCounterweightPolicy.Dominant(new[] {Realm("empire", 2500f), Realm("vlandia", 2000f), Realm("sturgia", 1900f)}).Should().BeNull();
      }

      // With only two realms left there is no balance of the many against one to keep.
      [Test]
      public void GIVEN_only_two_realms_standing_WHEN_the_balance_is_read_THEN_none_dominates()
      {
         RealmCounterweightPolicy.Dominant(new[] {Realm("empire", 9000f), Realm("vlandia", 1000f)}).Should().BeNull();
      }

      // Already at war with the dominant realm, the first thing worth doing is to bring an ally in, then to free the armies
      // from a weaker enemy, then to win a new ally among the dominant realm's other enemies.
      [Test]
      public void GIVEN_a_realm_at_war_with_the_dominant_one_WHEN_its_steps_are_ordered_THEN_ally_first_then_peace_then_new_alliance()
      {
         var f = new CounterweightFacts {
            Strength = 1500f, AtWarWithDominant = true,
            Others = new[] {
               new OtherRealm {Id = "aserai", Strength = 1000f, AtWarWithDominant = true},
               new OtherRealm {Id = "khuzait", Strength = 900f, AtWarWithUs = true},
               new OtherRealm {Id = "battania", Strength = 800f, AlliedWithUs = true}
            }
         };

         Moves(f).Should().Equal(CounterweightMove.CallAllyToWar, CounterweightMove.PeaceWithWeaker, CounterweightMove.AllianceAgainstDominant);
      }

      // At peace with the dominant realm, a realm looks for allies and quiets its other fronts before it puts the war to the
      // vote: Gabriel's "arrangements with weaker realms", then the attack.
      [Test]
      public void GIVEN_a_realm_at_peace_with_the_dominant_one_WHEN_its_steps_are_ordered_THEN_the_war_comes_last()
      {
         var f = new CounterweightFacts {
            Strength = 1500f,
            Others = new[] {
               new OtherRealm {Id = "aserai", Strength = 1000f, AtWarWithDominant = true},
               new OtherRealm {Id = "khuzait", Strength = 900f, AtWarWithUs = true}
            }
         };

         Moves(f).Should().Equal(CounterweightMove.AllianceAgainstDominant, CounterweightMove.PeaceWithWeaker, CounterweightMove.WarOnDominant);
         RealmCounterweightPolicy.Steps(f, "empire").Last().RealmId.Should().Be("empire");
      }

      // Peace is sought only with a weaker enemy: making peace with a stronger one is a surrender, not a counterweight.
      [Test]
      public void GIVEN_an_enemy_stronger_than_the_realm_WHEN_its_steps_are_ordered_THEN_no_peace_is_sought_with_it()
      {
         var f = new CounterweightFacts {Strength = 1000f, AtWarWithDominant = true, Others = new[] {new OtherRealm {Id = "khuzait", Strength = 1200f, AtWarWithUs = true}}};

         Moves(f).Should().BeEmpty();
      }

      // An enemy of the realm is no ally, even if it also fights the dominant one; an ally already in the war needs no call.
      [Test]
      public void GIVEN_an_enemy_and_an_ally_already_fighting_WHEN_steps_are_ordered_THEN_neither_is_offered_an_alliance_or_a_call()
      {
         var f = new CounterweightFacts {
            Strength = 500f, AtWarWithDominant = true,
            Others = new[] {
               new OtherRealm {Id = "aserai", Strength = 1000f, AtWarWithDominant = true, AtWarWithUs = true},
               new OtherRealm {Id = "battania", Strength = 800f, AlliedWithUs = true, AtWarWithDominant = true}
            }
         };

         Moves(f).Should().BeEmpty();
      }

      // Among allies to call, the strongest first: it weighs most against the dominant realm.
      [Test]
      public void GIVEN_two_allies_to_call_WHEN_steps_are_ordered_THEN_the_stronger_ally_is_called_first()
      {
         var f = new CounterweightFacts {
            Strength = 1500f, AtWarWithDominant = true,
            Others = new[] {new OtherRealm {Id = "small", Strength = 300f, AlliedWithUs = true}, new OtherRealm {Id = "big", Strength = 1300f, AlliedWithUs = true}}
         };

         RealmCounterweightPolicy.Steps(f, "empire").First().RealmId.Should().Be("big");
      }

      // A house puts a war to the vote unless it is against it (the bar a lord pressed by the player answers to); a peace,
      // an alliance or a call only when it is for them at all.
      [Test]
      public void GIVEN_the_houses_support_WHEN_it_decides_whether_to_propose_THEN_a_torn_house_may_press_war_but_not_an_alliance()
      {
         RealmCounterweightPolicy.HouseWouldPropose(CounterweightMove.WarOnDominant, -20f).Should().BeTrue();
         RealmCounterweightPolicy.HouseWouldPropose(CounterweightMove.WarOnDominant, -60f).Should().BeFalse();
         RealmCounterweightPolicy.HouseWouldPropose(CounterweightMove.AllianceAgainstDominant, -20f).Should().BeFalse();
         RealmCounterweightPolicy.HouseWouldPropose(CounterweightMove.PeaceWithWeaker, 5f).Should().BeTrue();
      }

      // The player hears of the strategist's move as court news. NPCs never lie (Gabriel's standing rule): the news says the
      // lord URGED it and why, never that it was decided, since the vote and the ruler's choice are the game's.
      [Test]
      public void GIVEN_an_alliance_proposed_WHEN_the_news_is_told_THEN_it_says_who_urged_it_against_whom_and_claims_no_outcome()
      {
         string news = RealmCounterweightPolicy.News(new CounterweightNewsFacts {
            Move = CounterweightMove.AllianceAgainstDominant, Proposer = "Derthert of the dey Meroc", Realm = "Vlandia", Other = "the Aserai", Dominant = "the Empire"
         });

         news.Should().Be("Derthert of the dey Meroc urged Vlandia to seek an alliance with the Aserai against the Empire.");
      }

      // The pace put to Gabriel on 30/10/2026 (one strategist proposal a month per realm, on top of the game's own), which
      // his "go" let stand: without it a realm facing a dominant one would file a decision every week.
      [Test]
      public void GIVEN_a_realm_proposed_recently_WHEN_it_may_act_again_THEN_only_after_a_month()
      {
         RealmCounterweightPolicy.RealmMayAct(null, 10).Should().BeTrue();
         RealmCounterweightPolicy.RealmMayAct(10, 10 + RealmCounterweightPolicy.RealmCooldownDays - 1).Should().BeFalse();
         RealmCounterweightPolicy.RealmMayAct(10, 10 + RealmCounterweightPolicy.RealmCooldownDays).Should().BeTrue();
      }
   }
}
