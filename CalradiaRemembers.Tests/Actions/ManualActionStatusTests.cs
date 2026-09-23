// Code written by Gabriel Mailhot, 22/09/2026.
// What these pin: that the panel can tell the player what he may ask, what is merely out of reach today, and
// what will never apply to this person, from ONE decision rather than three.
//
// fkasad, Nexus, 22/09/2026, asking for a way to find out what the mod can actually do: "What if we could
// trigger a meta discussion and ask the LLM (which knows exactly what actions are possible) if we can do this or
// that." His own experiment answered itself: his companion replied in character, with charm and no facts,
// because a model asked about the rules reasons about its own prompt rather than about the mod.
//
// The mod knows with certainty, and already did: ManualActionAvailability.GetAvailable has always returned
// exactly what may be offered right now. What it could not say is what is MISSING and why, which is the half
// fkasad actually wanted. Gabriel's shape (22/09/2026): a list in three parts, what is valid with its
// explanation, what is possible but lacks a condition, and what the character will never accept; only the first
// is injectable.
//
// THE DIVIDING LINE, because without a rule the third part becomes a dumping ground: a thing is merely BLOCKED
// when it fails on where things stand (no gold, no fief in hand, she is not discontented today), and NOT FOR
// THIS PERSON when it fails on who she is (she is not your prisoner, she is not your companion). The first can
// change this afternoon; the second cannot change without her becoming someone else.
//
// And GetAvailable is now a VIEW of this, not a second decision, so the panel cannot offer what the menu would
// refuse. Two surfaces over one decision is the drift this repository spent 21-22/09/2026 learning about.

#region

using System.Linq;
using CalradiaRemembers.Logic.Actions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Actions
{
   [TestFixture]
   public sealed class ManualActionStatusTests
   {
      private static ManualActionStatus StatusOf(ManualActionFacts facts, ManualActionEntry entry)
         => ManualActionAvailability.Describe(facts).Single(s => s.Entry.Type == entry.Type);

      // THE PANEL AND THE MENU CANNOT DISAGREE, because one is built from the other. Before this, a second
      // surface would have meant a second copy of the same conditions, and this codebase has just spent two days
      // on what that costs (a teaching gate and a bridge guard that drifted, a mirror with seventeen holes).
      [Test]
      public void GIVEN_any_conversation_WHEN_the_menu_asks_what_is_available_THEN_it_is_exactly_what_the_panel_calls_open()
      {
         var facts = new ManualActionFacts {
            NpcIsPlayerCompanion = true,
            NpcCanBeDispatchedOnMission = true,
            PlayerHasGiveableItems = true,
            CanSpendInfluence = true
         };

         var open = ManualActionAvailability.Describe(facts)
                                            .Where(s => s.State == ManualActionState.Open)
                                            .Select(s => s.Entry.Type);

         ManualActionAvailability.GetAvailable(facts).Select(e => e.Type).Should().Equal(open);
      }

      // WHAT HE MAY ASK FOR RIGHT NOW needs no explanation beyond itself, so an open entry carries no reason:
      // a panel that explained why something IS allowed would bury the short list that matters under prose.
      [Test]
      public void GIVEN_an_action_he_may_take_WHEN_the_panel_is_built_THEN_it_is_open_and_says_nothing_further()
      {
         ManualActionStatus gold = StatusOf(new ManualActionFacts(), ManualActionCatalog.TakeGold);

         gold.State.Should().Be(ManualActionState.Open);
         gold.Reason.Should().BeEmpty();
      }

      // THE MIDDLE RANK IS THE USEFUL ONE. "You are carrying nothing worth handing over" tells the player what
      // to go and change; "give an item is unavailable" tells him nothing, which is the state he was in before
      // asking his companion about the laws of physics.
      [Test]
      public void GIVEN_an_action_blocked_by_where_things_stand_WHEN_the_panel_is_built_THEN_it_names_what_is_missing()
      {
         ManualActionStatus item = StatusOf(new ManualActionFacts {PlayerHasGiveableItems = false}, ManualActionCatalog.GiveItem);

         item.State.Should().Be(ManualActionState.Blocked);
         item.Reason.Should().NotBeEmpty();
      }

      // AND THE BOTTOM RANK IS ABOUT WHO SHE IS, which is why it can be folded away: a commoner will never be
      // granted a fief and no amount of play this afternoon changes that, so it is context rather than a to-do
      // list. Executing a prisoner who is not your prisoner is the clearest case.
      [Test]
      public void GIVEN_an_action_that_does_not_apply_to_this_person_WHEN_the_panel_is_built_THEN_it_is_not_merely_blocked()
      {
         ManualActionStatus execute = StatusOf(new ManualActionFacts {NpcIsPlayerPrisoner = false}, ManualActionCatalog.ExecutePrisoner);

         execute.State.Should().Be(ManualActionState.NotForThisPerson);
         execute.Reason.Should().NotBeEmpty();
      }

      // THE TWO RANKS ARE NOT THE SAME QUESTION, and the same entry can be in either depending on her. She IS
      // your prisoner, so the verb applies to her; she simply will not take your colours yet, which is a thing
      // that can change. Without this the whole taxonomy collapses into "available or not".
      [Test]
      public void GIVEN_a_prisoner_who_will_not_yet_turn_WHEN_the_panel_is_built_THEN_it_is_blocked_rather_than_never()
      {
         var held = new ManualActionFacts {NpcIsPlayerPrisoner = true, CanRecruitPrisoner = false};

         StatusOf(held, ManualActionCatalog.RecruitPrisoner).State.Should().Be(ManualActionState.Blocked);
         StatusOf(new ManualActionFacts(), ManualActionCatalog.RecruitPrisoner).State.Should().Be(ManualActionState.NotForThisPerson,
            "she is not your prisoner at all, which is a fact about who she is");
      }

      // EVERY ENTRY IS ACCOUNTED FOR, always. A panel that silently omitted an entry would be telling the player
      // it does not exist, which is the exact false impression this whole feature is meant to end.
      [Test]
      public void GIVEN_any_facts_WHEN_the_panel_is_built_THEN_every_catalogue_entry_appears_exactly_once()
      {
         var described = ManualActionAvailability.Describe(new ManualActionFacts()).Select(s => s.Entry.Type).ToList();

         described.Should().OnlyHaveUniqueItems();
         described.Should().BeEquivalentTo(ManualActionCatalog.All.Select(e => e.Type));
      }

      // AND NOTHING CLOSED IS EVER SILENT. A row that says only "no" is the thing fkasad went to an LLM to get
      // around; if we cannot say why, we have not answered him.
      [Test]
      public void GIVEN_the_emptiest_possible_conversation_WHEN_the_panel_is_built_THEN_every_closed_entry_carries_a_reason()
      {
         var closed = ManualActionAvailability.Describe(new ManualActionFacts())
                                              .Where(s => s.State != ManualActionState.Open)
                                              .Where(s => string.IsNullOrWhiteSpace(s.Reason))
                                              .Select(s => s.Entry.Type)
                                              .ToList();

         closed.Should().BeEmpty("a closed row with no reason teaches the player nothing");
      }

      // A NULL FACTS OBJECT is what a half-built conversation hands in, and the panel must still render: every
      // entry closed, nothing thrown, nothing offered.
      [Test]
      public void GIVEN_no_facts_at_all_WHEN_the_panel_is_built_THEN_nothing_is_offered_and_nothing_throws()
      {
         var described = ManualActionAvailability.Describe(null);

         described.Should().NotBeEmpty();
         described.Should().OnlyContain(s => s.State != ManualActionState.Open);
      }
   }
}
