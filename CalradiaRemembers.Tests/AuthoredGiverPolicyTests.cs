// Code written by Gabriel Mailhot, 11/10/2026.
// Who may give a written tale. Gabriel (10/10/2026): a tale of intimacy with the player's own wife, whatever the
// player's sex, while she travels with him and is not already with child. If these rules are wrong, a stranger, a
// husband or a wife left at home is offered the scene, a wife already expecting is offered a tale that ends on a chance
// of pregnancy, or every tale written before the fields stops finding a giver.

#region

using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredGiverPolicyTests
   {
      private static GiverFacts Wife(bool spouse = true, bool female = true, bool withPlayer = true, bool pregnant = false)
         => new() {IsLord = true, IsPlayersSpouse = spouse, IsFemale = female, InPlayersParty = withPlayer, IsPregnant = pregnant};

      private static GiverRequirement WifeWithPlayer() => new() {Role = "spouse", Gender = "female", WithPlayer = true, NotPregnant = true};

      // The tale's whole point: the player's wife, travelling with him, not expecting, may give it.
      [Test]
      public void GIVEN_the_players_wife_in_his_party_WHEN_a_spouse_tale_asks_THEN_she_may_give_it()
      {
         AuthoredGiverPolicy.WhyNot(WifeWithPlayer(), Wife()).Should().BeNull();
      }

      // A lady of the court is not the player's wife: the scene is not hers to offer.
      [Test]
      public void GIVEN_a_lady_who_is_not_the_players_spouse_WHEN_a_spouse_tale_asks_THEN_she_may_not()
      {
         GiverFacts lady = Wife(spouse: false);

         AuthoredGiverPolicy.WhyNot(WifeWithPlayer(), lady).Should().Contain("spouse");
      }

      // A woman player married to a man: the tale written for a wife does not go to her husband.
      [Test]
      public void GIVEN_the_player_is_married_to_a_man_WHEN_the_tale_wants_a_wife_THEN_he_may_not_give_it()
      {
         GiverFacts husband = Wife(female: false);

         AuthoredGiverPolicy.WhyNot(WifeWithPlayer(), husband).Should().Contain("woman");
      }

      // A woman player married to a woman gets the same tale (Gabriel: "peu importe si le joueur est masculin ou feminin").
      [Test]
      public void GIVEN_a_woman_player_married_to_a_woman_WHEN_the_tale_wants_a_wife_THEN_the_wife_may_give_it()
      {
         // The player's own sex is no fact the rule reads: only the wife's.
         AuthoredGiverPolicy.WhyNot(WifeWithPlayer(), Wife()).Should().BeNull();
      }

      // Gabriel: "l'epouse doit voyager avec le joueur". A wife at home in her castle is not met by a waterfall.
      [Test]
      public void GIVEN_the_wife_is_not_in_the_players_party_WHEN_the_tale_wants_her_with_him_THEN_she_may_not()
      {
         GiverFacts athome = Wife(withPlayer: false);

         AuthoredGiverPolicy.WhyNot(WifeWithPlayer(), athome).Should().Contain("party");
      }

      // Gabriel: "il faut aussi que la femme ne soit pas deja enceinte". The tale ends on a chance of a child.
      [Test]
      public void GIVEN_the_wife_is_already_with_child_WHEN_the_tale_asks_her_not_to_be_THEN_she_may_not()
      {
         GiverFacts expecting = Wife(pregnant: true);

         AuthoredGiverPolicy.WhyNot(WifeWithPlayer(), expecting).Should().Contain("child");
      }

      // Every tale written before these fields (a lord, a notable, a gang leader, anyone) finds its giver as before.
      [Test]
      public void GIVEN_a_tale_that_asks_none_of_the_new_fields_WHEN_checked_THEN_the_old_roles_hold()
      {
         var stranger = new GiverFacts {IsLord = true, IsPregnant = true};

         AuthoredGiverPolicy.WhyNot(new GiverRequirement {Role = "lord"}, stranger).Should().BeNull();
         AuthoredGiverPolicy.WhyNot(new GiverRequirement {Role = "any"}, new GiverFacts()).Should().BeNull();
         AuthoredGiverPolicy.WhyNot(new GiverRequirement {Role = "notable"}, stranger).Should().Contain("notable");
         AuthoredGiverPolicy.WhyNot(new GiverRequirement {Role = "gang_leader"}, new GiverFacts {IsGangLeader = true}).Should().BeNull();
      }

      // A gender left empty is either: a tale for "the player's spouse" whoever they are.
      [Test]
      public void GIVEN_a_spouse_tale_with_no_gender_WHEN_the_spouse_is_a_man_THEN_he_may_give_it()
      {
         var husband = new GiverFacts {IsLord = true, IsPlayersSpouse = true, InPlayersParty = true};

         AuthoredGiverPolicy.WhyNot(new GiverRequirement {Role = "spouse"}, husband).Should().BeNull();
      }

      // STAKE: tashmetu (18/10/2026): a road tale told by "someone who rides with the player" ("I was born on the farm down
      // there") used role "any" with with_player, which also let in the player's spouse or a noble child riding along.
      // The companion role takes only a companion of the player's house.
      [Test]
      public void GIVEN_a_companion_tale_WHEN_a_companion_or_the_spouse_rides_along_THEN_only_the_companion_may_give_it()
      {
         var wants = new GiverRequirement {Role = "companion", WithPlayer = true};

         AuthoredGiverPolicy.WhyNot(wants, new GiverFacts {IsPlayersCompanion = true, InPlayersParty = true}).Should().BeNull();
         AuthoredGiverPolicy.WhyNot(wants, new GiverFacts {IsLord = true, IsPlayersSpouse = true, InPlayersParty = true}).Should().Contain("companions");
         AuthoredGiverPolicy.WhyNot(wants, new GiverFacts {IsLord = true, InPlayersParty = true}).Should().Contain("companions");
      }

      // ── A married giver (the Troy-inspired tale, Gabriel 12/10/2026: docs/design/tale-scenario-bridge) ──

      private static GiverRequirement Queen() => new() {Role = "lord", Gender = "female", Married = true, NotClanLeader = true, NotOfPlayersRealm = true};

      private static GiverFacts Lady(bool married = true, bool leadsHouse = false, bool ofPlayersRealm = false)
         => new() {IsLord = true, IsFemale = true, IsMarriedToAnother = married, IsClanLeader = leadsHouse, IsOfPlayersRealm = ofPlayersRealm};

      // The tale is about leaving a husband: a married lady of another realm, who does not lead her house, may give it.
      [Test]
      public void GIVEN_a_married_lady_of_another_realm_WHEN_the_queens_tale_asks_THEN_she_may_give_it()
      {
         AuthoredGiverPolicy.WhyNot(Queen(), Lady()).Should().BeNull();
      }

      // No husband, no tale: the elopement and the war it brings need someone she leaves.
      [Test]
      public void GIVEN_an_unmarried_lady_WHEN_the_queens_tale_asks_THEN_she_may_not()
      {
         AuthoredGiverPolicy.WhyNot(Queen(), Lady(married: false)).Should().Contain("married");
      }

      // A woman who leads her house cannot leave it (Stolen Love's own limit: such a divorce is never completed).
      [Test]
      public void GIVEN_a_lady_who_leads_her_house_WHEN_the_queens_tale_asks_THEN_she_may_not()
      {
         AuthoredGiverPolicy.WhyNot(Queen(), Lady(leadsHouse: true)).Should().Contain("house");
      }

      // Gabriel (12/10/2026): "jamais par la Reine de mon royaume". A war against one's own realm cannot be.
      [Test]
      public void GIVEN_a_lady_of_the_players_own_realm_WHEN_the_queens_tale_asks_THEN_she_may_not()
      {
         AuthoredGiverPolicy.WhyNot(Queen(), Lady(ofPlayersRealm: true)).Should().Contain("your own realm");
      }

      // ── Her husband ──

      // The tale's war is declared by a husband who rules; when the tale asks for one, a husband who rules no realm fails it.
      [Test]
      public void GIVEN_a_tale_wanting_a_ruling_husband_WHEN_he_rules_no_realm_THEN_the_tale_is_not_offered()
      {
         var wants = new HusbandRequirement {RulesARealm = true};

         AuthoredHusbandPolicy.WhyNot(wants, new HusbandFacts {Exists = true, InTheWorld = true, RulesARealm = false}).Should().Contain("rules no realm");
         AuthoredHusbandPolicy.WhyNot(wants, new HusbandFacts {Exists = true, InTheWorld = true, RulesARealm = true}).Should().BeNull();
      }

      // Gabriel (12/10/2026): a husband who does not rule still holds the grudge and presses his king, so by default any
      // husband will do; but he must be alive and in the world, or nobody is left to answer the insult.
      [Test]
      public void GIVEN_a_husband_out_of_the_world_WHEN_the_tale_asks_THEN_it_is_not_offered()
      {
         AuthoredHusbandPolicy.WhyNot(new HusbandRequirement(), new HusbandFacts {Exists = true, InTheWorld = true}).Should().BeNull();
         AuthoredHusbandPolicy.WhyNot(new HusbandRequirement(), new HusbandFacts {Exists = true, InTheWorld = false}).Should().NotBeNull();
         AuthoredHusbandPolicy.WhyNot(new HusbandRequirement(), new HusbandFacts()).Should().NotBeNull();
      }
   }
}
