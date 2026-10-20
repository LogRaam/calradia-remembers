// Code written by Gabriel Mailhot, 20/10/2026.
// Who a "locals_of:" memory leaves out. tashmetu (20/10/2026), playing The Maiden of the Moonlit Glade: the giver, Dafydd
// of Llanoc Hen, is a notable of the village the ending speaks to, so he remembered the same moment twice, once as the
// giver ("Basil went looking for the maiden...") and once as one of the locals ("The stranger found the maiden of the
// glade..."). His ask: "Could locals_of leave out anyone the same ending already gives a memory to? Then a tale can speak to
// the giver and to the village without the giver hearing it twice." If this is wrong, a tale's giver hears his own ending
// twice in the Memories window and in his prompt, or a character the author named loses the memory written for him.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredLocalsMemoryTests
   {
      private const string Giver = "notable_dafydd";
      private const string Husband = "lord_basil";

      private static AuthoredQuestRun Run()
         => new() {BoundIds = new Dictionary<string, string> {["giver"] = Giver, ["husband"] = Husband, ["village"] = "village_llanoc"}};

      private static AuthoredConsequence Memory(string who) => new() {Type = "memory", Who = who, Text = "something happened"};

      // STAKE: tashmetu's report itself. The ending's own memory goes to the giver; a giver who lives in the village the ending
      // also speaks to must not hear the village's version on top of his own.
      [Test]
      public void GIVEN_an_ending_with_a_memory_for_the_giver_WHEN_it_also_speaks_to_the_village_THEN_the_village_memory_skips_the_giver()
      {
         var outcome = new AuthoredOutcome {Memory = "Basil went looking for the maiden."};

         ISet<string> named = AuthoredQuestEngine.NamedMemoryHolders(Run(), outcome, new[] {Memory("locals_of:{village}")});

         named.Should().Contain(Giver);
      }

      // STAKE: an ending with no memory for the giver gives him nothing of his own; leaving him out of the village's memory
      // would leave him the one notable of his village who never heard of it.
      [Test]
      public void GIVEN_an_ending_with_no_memory_for_the_giver_WHEN_it_speaks_to_the_village_THEN_the_giver_hears_it_with_the_others()
      {
         var outcome = new AuthoredOutcome {Memory = ""};

         ISet<string> named = AuthoredQuestEngine.NamedMemoryHolders(Run(), outcome, new[] {Memory("locals_of:{village}")});

         named.Should().BeEmpty();
      }

      // STAKE: "anyone the same ending already gives a memory to", in tashmetu's words, not only the giver: a husband named by
      // a memory of his own must not also get the crowd's line when he happens to be a local.
      [Test]
      public void GIVEN_a_memory_written_for_a_named_character_WHEN_the_ending_speaks_to_the_village_THEN_that_character_is_left_out_of_it()
      {
         ISet<string> named = AuthoredQuestEngine.NamedMemoryHolders(Run(), null, new[] {Memory("locals_of:{village}"), Memory("{husband}")});

         named.Should().BeEquivalentTo(new[] {Husband});
      }

      // STAKE: the crowd yields to the named, never the reverse; the order an author lists the consequences in must not matter,
      // and a role the tale never bound names nobody.
      [Test]
      public void GIVEN_the_crowd_listed_first_and_an_unbound_role_WHEN_reading_who_is_named_THEN_only_the_bound_named_count()
      {
         ISet<string> named = AuthoredQuestEngine.NamedMemoryHolders(Run(), null,
            new[] {Memory("locals_of:{village}"), Memory("{husband}"), Memory("{rival}")});

         named.Should().BeEquivalentTo(new[] {Husband});
      }
   }
}
