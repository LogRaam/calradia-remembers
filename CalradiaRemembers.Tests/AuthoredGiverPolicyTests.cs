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
   }
}
