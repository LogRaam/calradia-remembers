// Code written by Gabriel Mailhot, 18/10/2026.
// A tale may ask for its giver's realm. Gabriel (18/10/2026): the queens' tale (The Guest of the King's Hall) is offered
// only where the realm's queen has her face and her pictures, until he sends the two still missing (the north and the
// Western Empire). The three empires share one culture, so the culture requirement cannot tell them apart; only the
// realm can. If this is wrong, a Western Empire queen offers the tale with no face and no picture, or a realm the tale
// names never offers it.

#region

using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredRealmRequirementTests
   {
      private static AuthoredQuest WithRealm(string realm)
         => AuthoredTales.Load(AuthoredTales.Fork.Replace("\"binds\":", "\"requires\": { \"giver\": { \"realm\": " + realm + " } },\n        \"binds\":"));

      // STAKE: the author writes one realm or a list, as for a culture; both read the same way.
      [Test]
      public void GIVEN_one_realm_or_a_list_WHEN_the_tale_is_read_THEN_both_name_the_realms_it_asks_for()
      {
         WithRealm("\"vlandia\"").Requires.Giver.Realm.Should().Equal("vlandia");
         WithRealm("[\"empire\", \"aserai\"]").Requires.Giver.Realm.Should().Equal("empire", "aserai");
      }

      // STAKE: the northern empire has its queen, the western one not yet: naming "empire" must never let a Western
      // Empire queen offer the tale, though both are of the same culture.
      [Test]
      public void GIVEN_a_tale_for_the_northern_empire_WHEN_a_western_empire_giver_is_weighed_THEN_she_does_not_match()
      {
         var asked = new[] {"empire", "vlandia"}.ToList();

         AuthoredCulturePolicy.Matches(asked, "empire", "Northern Empire").Should().BeTrue();
         AuthoredCulturePolicy.Matches(asked, "empire_w", "Western Empire").Should().BeFalse();
      }

      // STAKE: a tale that names no realm is offered in every realm, as every tale written before this field was.
      [Test]
      public void GIVEN_a_tale_naming_no_realm_WHEN_any_giver_is_weighed_THEN_every_realm_matches()
      {
         AuthoredTales.Load().Requires?.Giver?.Realm.Should().BeNullOrEmpty();
         AuthoredCulturePolicy.Matches(new System.Collections.Generic.List<string>(), "empire_w", "Western Empire").Should().BeTrue();
      }
   }
}
