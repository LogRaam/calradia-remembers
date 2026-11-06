// Code written by Gabriel Mailhot, 05/11/2026.
// Which tournaments under way a character knows of. 3chmidt (Nexus, 05/11/2026): "lords know in what town currently
// tournaments happen, at least in the same kingdom or a neighbouring one". If this is wrong, a lord is blind to the games
// in his own realm, claims news of a tournament at the far end of Calradia, or recites every list in the land.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Tournaments;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TournamentsHeardTests
   {
      // STAKE: a tournament in his own realm is known, however far, and named with its prize.
      [Test]
      public void GIVEN_a_tournament_far_off_in_his_own_realm_WHEN_told_THEN_he_knows_of_it()
      {
         List<string> lines = TournamentsHeard.Lines(new[] {
            new HeldTournament {Town = "Pravend", Realm = "Vlandia", InOwnRealm = true, Distance = 600f, Prize = "Noble Bow"}
         });

         lines.Should().Equal("at Pravend, in your own realm, the prize a Noble Bow");
      }

      // STAKE: a neighbouring realm's tournament is talked of on the roads; one across the map is not his news.
      [Test]
      public void GIVEN_tournaments_of_other_realms_near_and_far_WHEN_told_THEN_only_the_near_one_is_known()
      {
         List<string> lines = TournamentsHeard.Lines(new[] {
            new HeldTournament {Town = "Sargot", Realm = "Vlandia", Distance = TournamentsHeard.HeardWithin - 1},
            new HeldTournament {Town = "Quyaz", Realm = "the Aserai Sultanate", Distance = TournamentsHeard.HeardWithin + 200}
         });

         lines.Should().Equal("at Sargot, in Vlandia");
      }

      // STAKE: his own realm's games come before a nearer foreign one, and the list never runs past a few.
      [Test]
      public void GIVEN_many_tournaments_WHEN_told_THEN_his_own_realms_come_first_and_only_a_few_are_named()
      {
         var held = new List<HeldTournament> {new HeldTournament {Town = "Near Foreign", Realm = "Sturgia", Distance = 5f}};
         for (var i = 0; i < 6; i++) held.Add(new HeldTournament {Town = $"Own {i}", InOwnRealm = true, Distance = 100f + i});

         List<string> lines = TournamentsHeard.Lines(held);

         lines.Should().HaveCount(TournamentsHeard.MaxNamed);
         lines[0].Should().StartWith("at Own 0");
         lines.Should().NotContain(l => l.StartsWith("at Near Foreign"));
      }

      // STAKE: no tournament held, nothing said.
      [Test]
      public void GIVEN_no_tournament_WHEN_told_THEN_nothing_is_said()
      {
         TournamentsHeard.Lines(new HeldTournament[0]).Should().BeEmpty();
      }

      // STAKE: a prize starting with a vowel reads as English does.
      [Test]
      public void GIVEN_a_prize_starting_with_a_vowel_WHEN_described_THEN_it_takes_an()
      {
         TournamentsHeard.Describe(new HeldTournament {Town = "Ortysia", Realm = "the Empire", Prize = "Imperial Sword"})
                         .Should().Be("at Ortysia, in the Empire, the prize an Imperial Sword");
      }
   }
}
