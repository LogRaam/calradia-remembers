// Code written by Gabriel Mailhot, 02/10/2026.
// tashmetu (Nexus, 02/10/2026, Bridges to Remember on 2.6.10): "CR records marriages as world news, but not divorces
// made by other mods (Hero.Spouse set to null). Could CR notice them?" The game raises no event when a mod clears a
// spouse, so CR compares the marriages it saw yesterday with today's. If this rule is wrong, a divorce another mod made
// goes unremembered (the former spouses speak as though still wed), a death is mistaken for a divorce, or a divorce CR
// itself made is recorded twice.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ForeignDivorceDetectionTests
   {
      private static Dictionary<string, string> Wed(params string[] pairs)
      {
         var map = new Dictionary<string, string>();
         for (var i = 0; i + 1 < pairs.Length; i += 2)
         {
            map[pairs[i]] = pairs[i + 1];
            map[pairs[i + 1]] = pairs[i];
         }

         return map;
      }

      private static HashSet<string> Present(params string[] ids) => new(ids);

      // tashmetu's case: another mod clears the spouse, both still live; CR must see the marriage is over.
      [Test]
      public void GIVEN_a_couple_yesterday_and_both_unwed_today_WHEN_compared_THEN_their_divorce_is_found()
      {
         var found = ForeignDivorceDetection.Dissolved(new DivorceDetectionFacts {
            Before = Wed("ira", "mesui"), Now = Wed(), InTheWorld = Present("ira", "mesui")
         });

         found.Should().ContainSingle(p => p.Key == ForeignDivorceDetection.PairKey("mesui", "ira"));
      }

      // A death ends a marriage too, but it is not a divorce: the widow must never remember a divorce.
      [Test]
      public void GIVEN_one_spouse_gone_from_the_world_WHEN_compared_THEN_no_divorce_is_found()
      {
         ForeignDivorceDetection.Dissolved(new DivorceDetectionFacts {
            Before = Wed("ira", "mesui"), Now = Wed(), InTheWorld = Present("ira")
         }).Should().BeEmpty();
      }

      // CR's own divorces already carry their consequences (grudges, memories); recording them again would double them.
      [Test]
      public void GIVEN_a_divorce_CR_made_itself_WHEN_compared_THEN_it_is_left_to_CR()
      {
         ForeignDivorceDetection.Dissolved(new DivorceDetectionFacts {
            Before = Wed("ira", "mesui"), Now = Wed(), InTheWorld = Present("ira", "mesui"),
            DissolvedByCr = new HashSet<string> {ForeignDivorceDetection.PairKey("ira", "mesui")}
         }).Should().BeEmpty();
      }

      // A couple still wed is no news, and the pair is reported once, not once per spouse.
      [Test]
      public void GIVEN_one_couple_unchanged_and_one_parted_WHEN_compared_THEN_only_the_parted_one_is_found_once()
      {
         var found = ForeignDivorceDetection.Dissolved(new DivorceDetectionFacts {
            Before = Wed("ira", "mesui", "caladog", "alys"), Now = Wed("caladog", "alys"),
            InTheWorld = Present("ira", "mesui", "caladog", "alys")
         });

         found.Should().HaveCount(1);
      }

      // The former spouse remembers a parting, never a death, and names whom she parted from.
      [Test]
      public void GIVEN_a_divorce_found_WHEN_a_former_spouse_remembers_THEN_she_knows_the_marriage_ended()
      {
         ForeignDivorceDetection.MemoryLine("Mesui of the Tigers").Should().Contain("My marriage to Mesui of the Tigers has ended").And.Contain("no longer wed");
      }

      // Divorced then wed to someone else by the same mod: the first marriage still ended, and is remembered as ended.
      [Test]
      public void GIVEN_a_spouse_now_wed_to_another_WHEN_compared_THEN_the_first_marriage_is_found_ended()
      {
         ForeignDivorceDetection.Dissolved(new DivorceDetectionFacts {
            Before = Wed("ira", "mesui"), Now = Wed("ira", "alys"), InTheWorld = Present("ira", "mesui", "alys")
         }).Should().ContainSingle(p => p.Key == ForeignDivorceDetection.PairKey("ira", "mesui"));
      }
   }
}
