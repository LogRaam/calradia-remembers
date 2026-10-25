// Code written by Gabriel Mailhot, 25/10/2026.
// What these pin: that the two readers of a conversation's actions are taught the same verbs.
//
// Gabriel (25/10/2026): "il ne devrait pas avoir de differences entre [Enhanced et le mode regulier] car ce sont juste
// deux facons differentes d'interpreter les actions dans une conversation." A verb taught to the interpreter and not to
// the prose model is a deed a player in Standard composition can never do, and the reverse is one a player in Enhanced
// can never do; neither shows anywhere until a player notices. A tale's choices by words were the first found that way.
//
// The comparison is CompositionParityMap's own rendering (both prompts built by the real builders from one context), so
// what the map shows and what this refuses cannot disagree. Known asymmetries sit on CompositionParityMap.Debts, each
// with its reason; the list may only shrink.

#region

using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   [TestFixture]
   public sealed class CompositionParityTests
   {
      [SetUp]
      public void Reachable()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}; run `dotnet test`.");
      }

      // STAKE: a new verb, or a moved teaching, that reaches one reader only is a deed that silently cannot happen in one
      // of the two compositions; it is refused here unless it is written down with its reason.
      [Test]
      public void GIVEN_both_readers_WHEN_their_teaching_is_compared_THEN_no_verb_reaches_one_only_unless_it_is_an_allowed_debt()
      {
         List<string> unexplained = CompositionParityMap.Rows(SourceTree.Production)
                                                        .Where(r => r.Asymmetric && !CompositionParityMap.Debts.ContainsKey(r.Verb))
                                                        .Select(r => $"{r.Verb} ({(r.InterpreterFull ? "interpreter" : "prose")} only)")
                                                        .ToList();

         unexplained.Should().BeEmpty("Enhanced and Standard must teach the same actions (Gabriel, 25/10/2026)");
      }

      // STAKE: a debt that has been paid must leave the list, or the list stops saying what is still wrong and a later
      // regression on the same verb would pass as already allowed.
      [Test]
      public void GIVEN_the_allowed_debts_WHEN_compared_THEN_each_is_still_an_asymmetry()
      {
         Dictionary<string, CompositionParityMap.Row> rows = CompositionParityMap.Rows(SourceTree.Production).ToDictionary(r => r.Verb);

         List<string> paid = CompositionParityMap.Debts.Keys.Where(v => !rows.TryGetValue(v, out CompositionParityMap.Row? r) || !r.Asymmetric).ToList();

         paid.Should().BeEmpty("a debt no longer owed comes off the list");
      }
   }
}
