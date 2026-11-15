// Code written by Gabriel Mailhot, 17/10/2026.
// PairRegardPolicy decides what one character thinks of another, and why, from what really happened between them
// (docs/design/NPC_REGARD.md, sections 5 and 9). Gabriel (17/10/2026): a friend by the vanilla number was the mod's own
// bitter enemy, and every friend or enemy shown must carry its reason. If this is wrong, a lord named by the player is
// called a friend of the man who had his brother executed, kin fills the friend lists with no story, or a secret reason
// leaks as a claim the speaker cannot stand behind.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PairRegardPolicyTests
   {
      private static PairRegardTrace Trace(int weight, string line, bool known = true) => new() {Weight = weight, Line = line, KnownToPlayer = known};

      // STAKE: Gabriel's ruling that every entry carries its reason; the case vanilla gets wrong.
      [Test]
      public void GIVEN_a_lord_whose_brother_another_lord_executed_WHEN_his_regard_is_read_THEN_the_executioner_is_his_enemy_and_he_says_why()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {
            Traces = new[] {Trace(PairRegardPolicy.KinExecuted(), "had your brother Garios executed (9 years ago)")}
         });

         r.Verdict.Should().Be(PairRegardVerdict.Enemy);
         PairRegardPolicy.Describe(r).Should().Contain("an enemy").And.Contain("had your brother Garios executed (9 years ago)");
      }

      // STAKE: kin belongs in the family; a brother with nothing between them is not a "friend".
      [Test]
      public void GIVEN_two_brothers_with_nothing_between_them_WHEN_regard_is_read_THEN_neither_is_a_friend()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {IsSibling = true, SameClan = true});

         r.Verdict.Should().Be(PairRegardVerdict.Neither);
         PairRegardPolicy.Describe(r).Should().BeNull();
      }

      // STAKE: the speaker knows his own secret grudge, but a reason the player could not know is marked as one he may
      // keep to himself: discretion, never a denial (NPCs never lie).
      [Test]
      public void GIVEN_an_enemy_whose_only_reason_is_secret_WHEN_described_THEN_the_speaker_may_keep_it_to_himself()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {Traces = new[] {Trace(-45, "betrayed you at the parley", known: false)}});

         PairRegardPolicy.Describe(r).Should().Contain("you may keep it to yourself");
      }

      // STAKE: the exact report of 17/10/2026 transposed to two NPCs: vanilla is not a source, so a bitter grudge makes
      // an enemy whatever the vanilla number says (the caller never passes it).
      [Test]
      public void GIVEN_a_bitter_grudge_WHEN_regard_is_read_THEN_he_is_an_enemy()
      {
         PairRegardPolicy.Assess(new PairRegardFacts {Traces = new[] {Trace(-60, "your grudge: he slandered your house")}}).Verdict
                         .Should().Be(PairRegardVerdict.Enemy);
      }

      // STAKE: a rescue makes a friend, with its reason.
      [Test]
      public void GIVEN_the_other_once_rescued_the_speaker_WHEN_regard_is_read_THEN_he_is_a_friend()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {Traces = new[] {Trace(PairRegardPolicy.HelpedInThePast(2), "rescued you from the Khuzaits (2 years ago)")}});

         r.Verdict.Should().Be(PairRegardVerdict.Friend);
         PairRegardPolicy.Describe(r).Should().StartWith("a friend");
      }

      // STAKE: a strong reason the other way is said too, so the speaker is not told a simple story that is not his.
      [Test]
      public void GIVEN_an_execution_and_a_rescue_WHEN_described_THEN_both_are_said()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {
            Traces = new[] {Trace(-70, "had your brother executed"), Trace(40, "once rescued you")}
         });

         PairRegardPolicy.Describe(r).Should().Contain("had your brother executed; though once rescued you");
      }

      // STAKE: old wrongs weigh less: a betrayal twenty years gone is a quarter of a fresh one; an execution never fades.
      [Test]
      public void GIVEN_years_passed_WHEN_a_wrong_is_aged_THEN_it_halves_each_half_life_but_an_execution_does_not()
      {
         PairRegardPolicy.WrongedInThePast(0).Should().Be(PairRegardPolicy.WrongedInThePastWeight);
         PairRegardPolicy.WrongedInThePast(20).Should().Be(PairRegardPolicy.WrongedInThePastWeight / 4);
         PairRegardPolicy.KinExecuted().Should().Be(PairRegardPolicy.KinExecutedWeight);
      }

      // STAKE: the encyclopedia never lists an enemy whose only reason the player could not know (NPC_REGARD.md 5.3).
      [Test]
      public void GIVEN_an_enemy_whose_only_reason_is_secret_WHEN_his_page_is_built_THEN_he_is_not_listed()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {Traces = new[] {Trace(-60, "betrayed you", known: false)}});

         PairRegardPolicy.PageReason(r).Should().BeNull();
      }

      // STAKE: the page is read by the player: it shows the line told about the lord, never one that says "you" of him.
      [Test]
      public void GIVEN_a_known_reason_WHEN_his_page_is_built_THEN_the_page_line_is_shown()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {
            Traces = new[] {new PairRegardTrace {Weight = -70, Line = "had your brother Garios executed", PageLine = "had his brother Garios executed", KnownToPlayer = true}}
         });

         PairRegardPolicy.PageReason(r).Should().Be("had his brother Garios executed");
      }

      // STAKE: a small trace with ties tips nothing on its own: no verdict, the caller keeps the old label.
      [Test]
      public void GIVEN_a_small_trace_WHEN_regard_is_read_THEN_there_is_no_verdict()
      {
         PairRegardPolicy.Assess(new PairRegardFacts {Traces = new[] {Trace(PairRegardPolicy.BeatenInBattle(6), "beat you in battle (6 years ago)")}}).Verdict
                         .Should().Be(PairRegardVerdict.Neither);
      }

      // STAKE: tashmetu (18/10/2026): two lords R&S made close through years of battles side by side showed as "neither"
      // on the page, beside R&S's own tooltip saying the opposite. What another mod says counts, with its reason.
      [Test]
      public void GIVEN_another_mod_saying_two_lords_are_close_WHEN_nothing_here_says_otherwise_THEN_they_are_friends_for_its_reason()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {OutsideTraces = new[] {Trace(30, "fought side by side for years")}});

         r.Verdict.Should().Be(PairRegardVerdict.Friend);
         PairRegardPolicy.PageReason(r).Should().Be("fought side by side for years");
      }

      // STAKE: another mod may tip a pair, never drown what happened here: however much it claims, a brother executed
      // keeps the man who ordered it an enemy (OutsideCap).
      [Test]
      public void GIVEN_another_mod_claiming_great_friendship_WHEN_the_other_had_his_brother_executed_THEN_he_stays_an_enemy()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {
            Traces = new[] {Trace(PairRegardPolicy.KinExecuted(), "had your brother executed")},
            OutsideTraces = new[] {Trace(100, "a debt of honour"), Trace(100, "a great rapport")}
         });

         r.Verdict.Should().Be(PairRegardVerdict.Enemy);
         r.Points.Should().Be(PairRegardPolicy.KinExecuted() + PairRegardPolicy.OutsideCap);
         r.Reasons.Where(t => t.Line != "had your brother executed").Should().OnlyContain(t => t.Weight == PairRegardPolicy.OutsideCap);
      }

      // STAKE: when another mod's reason weighs as much as one of ours, ours is said first.
      [Test]
      public void GIVEN_an_outside_reason_as_strong_as_ours_WHEN_the_reasons_are_ordered_THEN_ours_comes_first()
      {
         PairRegard r = PairRegardPolicy.Assess(new PairRegardFacts {
            Traces = new[] {Trace(30, "saved your life")}, OutsideTraces = new[] {Trace(30, "an old rapport")}
         });

         r.Reasons[0].Line.Should().Be("saved your life");
      }

      // STAKE: tashmetu (18/10/2026, Halthdar's page): "Hertogi Gornlautir, friend: Halthdar and Hertogi Gornlautir rode
      // together" names the page's lord again; the pair opening is cut, and only that, so who wronged whom still reads.
      [Test]
      public void GIVEN_a_reason_opening_on_both_names_WHEN_shown_on_the_page_THEN_the_pair_is_not_named_again_but_a_deed_keeps_its_doer()
      {
         PairRegardPolicy.WithoutThePair(new PairReasonNames {Reason = "Halthdar and Hertogi rode together against raiders near Thronderlag (8 years ago)", OneName = "Halthdar", OtherName = "Hertogi"})
                         .Should().Be("rode together against raiders near Thronderlag (8 years ago)");
         PairRegardPolicy.WithoutThePair(new PairReasonNames {Reason = "Hertogi and Halthdar have been friends since they were young", OneName = "Halthdar", OtherName = "Hertogi"})
                         .Should().Be("have been friends since they were young");
         PairRegardPolicy.WithoutThePair(new PairReasonNames {Reason = "Hertogi insulted Halthdar at a feast, and Halthdar never forgot it", OneName = "Halthdar", OtherName = "Hertogi"})
                         .Should().Be("Hertogi insulted Halthdar at a feast, and Halthdar never forgot it");
      }
   }
}
