// Code written by Gabriel Mailhot, 22/11/2026.
// Which house a character was born into, read from the engine's OriginClan and the parents. EvilPoptart1987 (Nexus,
// 21/11/2026): "Married people remember what house they are born into i.e. they married in from. Silly to hear marriage
// transplants talking about growing up in a house not their own." The engine stamps OriginClan with the first clan a hero
// is ever given, so a wife married before the campaign began has her husband's clan there. If this policy is wrong, such
// a wife either speaks of her husband's house as her childhood home, or a true child of the house is told they married in.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class BirthClanPolicyTests
   {
      // STAKE: a lady wed during the campaign keeps the house the engine recorded, as since 2.7.x (Maifailun's Lidia).
      [Test]
      public void GIVEN_a_wedding_the_engine_saw_WHEN_the_birth_house_is_read_THEN_the_origin_clan_names_it()
      {
         BirthClanPolicy.Resolve(new BirthClanFacts {CurrentClan = "takhi", OriginClan = "comnos", FatherClan = "comnos", IsMarried = true})
                        .Should().Be(BirthClanSource.Origin);
      }

      // STAKE: EvilPoptart1987's case. A wife married before the campaign began has her husband's clan as OriginClan; her
      // father's house is where she grew up.
      [Test]
      public void GIVEN_a_wife_married_before_the_campaign_with_a_father_elsewhere_WHEN_read_THEN_her_fathers_house_names_it()
      {
         BirthClanPolicy.Resolve(new BirthClanFacts {CurrentClan = "takhi", OriginClan = "takhi", FatherClan = "comnos", IsMarried = true})
                        .Should().Be(BirthClanSource.Father);
      }

      // STAKE: a son of the house, married, is never told he married into his own family.
      [Test]
      public void GIVEN_a_married_son_of_the_house_WHEN_read_THEN_nothing_is_said()
      {
         BirthClanPolicy.Resolve(new BirthClanFacts {CurrentClan = "takhi", OriginClan = "takhi", FatherClan = "takhi", MotherClan = "takhi", IsMarried = true, LeadsTheClan = true})
                        .Should().Be(BirthClanSource.None);
      }

      // STAKE: with no father known, the mother's house still tells where she came from.
      [Test]
      public void GIVEN_only_a_mother_known_in_another_house_WHEN_read_THEN_the_mothers_house_names_it()
      {
         BirthClanPolicy.Resolve(new BirthClanFacts {CurrentClan = "takhi", OriginClan = "takhi", MotherClan = "comnos", IsMarried = true})
                        .Should().Be(BirthClanSource.Mother);
      }

      // STAKE: most wives the game starts with have no parents at all. Wed to a man of the house by blood, she came in by
      // marriage, and must not speak of growing up there, even though her own house cannot be named.
      [Test]
      public void GIVEN_a_wife_with_no_parents_wed_to_a_man_of_the_house_WHEN_read_THEN_she_married_in_from_an_unnamed_house()
      {
         BirthClanPolicy.Resolve(new BirthClanFacts {CurrentClan = "takhi", OriginClan = "takhi", IsMarried = true, SpouseIsOfTheClanByBlood = true})
                        .Should().Be(BirthClanSource.MarriedInUnknownHouse);
      }

      // STAKE: the head of a house is never told they married into it, nor is someone whose spouse is no more of it than
      // they are, nor an unwed character: without a fact, nothing is invented.
      [Test]
      public void GIVEN_no_parents_and_no_sign_of_a_marriage_into_the_house_WHEN_read_THEN_nothing_is_said()
      {
         BirthClanPolicy.Resolve(new BirthClanFacts {CurrentClan = "takhi", OriginClan = "takhi", IsMarried = true, LeadsTheClan = true, SpouseIsOfTheClanByBlood = true})
                        .Should().Be(BirthClanSource.None);
         BirthClanPolicy.Resolve(new BirthClanFacts {CurrentClan = "takhi", OriginClan = "takhi", IsMarried = true})
                        .Should().Be(BirthClanSource.None);
         BirthClanPolicy.Resolve(new BirthClanFacts {CurrentClan = "takhi", OriginClan = "takhi", SpouseIsOfTheClanByBlood = true})
                        .Should().Be(BirthClanSource.None);
      }

      // STAKE: the line itself never names an invented house, and says where she did not grow up.
      [Test]
      public void GIVEN_a_marriage_from_an_unnamed_house_WHEN_the_line_is_told_THEN_it_denies_a_childhood_in_the_current_house()
      {
         BirthHouseText.Line(new BirthHouseFacts {CurrentClan = "Takhi", ByMarriage = true, HouseUnknown = true})
                       .Should().Be("Not born into the Takhi: joined it by marriage, from another house; never speak of growing up among the Takhi.");
      }
   }
}
