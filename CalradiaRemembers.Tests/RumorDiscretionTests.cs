// Code written by Gabriel Mailhot, 03/10/2026.
// IdentityCrysys (Nexus, 03/10/2026): "is there a way to remove a world event from public memory and/or a specific
// NPC's awareness? For example, preventing a certain character from being aware of a marriage to a different
// character? Or preventing a certain event from coming up in conversation with anyone (except who it involves
// directly, maybe)?" A world event is heard by score (magnitude, realm, place, age), never by a stored list, so the
// player had no say. Now an event can be kept to those it names, or hidden from one character. If these rules are
// wrong, a secret wedding is gossiped in every hall, or one of the newlyweds forgets her own wedding.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RumorDiscretionTests
   {
      private static DiscretionFacts Wedding(string observer, bool secret = false, params string[] hiddenFrom)
         => new DiscretionFacts {ObserverId = observer, KnownOnlyToThoseInvolved = secret, InvolvedIds = new[] {"bride", "groom"}, HiddenFromIds = hiddenFrom};

      // An ordinary event is withheld from no one: the score alone decides, as before.
      [Test]
      public void GIVEN_an_ordinary_event_WHEN_anyone_asks_THEN_nothing_is_withheld()
      {
         RumorDiscretion.WithheldReason(Wedding("stranger")).Should().BeNull();
      }

      // IdentityCrysys's second ask: a secret is known to those it names, and to no one else.
      [Test]
      public void GIVEN_a_secret_event_WHEN_a_stranger_or_one_of_those_named_asks_THEN_only_the_named_know()
      {
         RumorDiscretion.WithheldReason(Wedding("stranger", true)).Should().NotBeNull();
         RumorDiscretion.WithheldReason(Wedding("bride", true)).Should().BeNull();
      }

      // A town's common talk has no one to be "involved": a secret never reaches the market.
      [Test]
      public void GIVEN_a_secret_event_WHEN_a_town_is_asked_THEN_it_is_withheld()
      {
         RumorDiscretion.WithheldReason(Wedding(null, true)).Should().NotBeNull();
      }

      // IdentityCrysys's first ask: one character kept unaware, everyone else as before.
      [Test]
      public void GIVEN_an_event_hidden_from_one_character_WHEN_she_and_another_ask_THEN_only_she_is_kept_unaware()
      {
         RumorDiscretion.WithheldReason(Wedding("rival", false, "rival")).Should().NotBeNull();
         RumorDiscretion.WithheldReason(Wedding("stranger", false, "rival")).Should().BeNull();
      }
   }
}
