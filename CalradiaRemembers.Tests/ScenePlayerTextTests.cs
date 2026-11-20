// Code written by Gabriel Mailhot, 19/11/2026.
// ScenePlayerText: what a tale's narrator is told of the player's kit and of the people of the world near the scene.
// Schmidt (Discord, 18/11/2026): he drew a two-handed axe he did not own, a character who held a weapon could not hand it
// over, and "lady Gunjadrid, daughter of Orvi" in the nearest town was unknown to the tale, which made up an old man by that
// name. Gabriel (19/11/2026) took three answers: the player uses only what they carry; a gift in a scene is the scene's only;
// a name on the list is that real person, a name on none is never given to an invented one. If this is wrong, the player
// conjures gear, a gift in a tale fills the real inventory, or a real lady becomes a stranger of the wood.
// Gabriel (20/11/2026), "aussi party_may_join": a tale may let the player send for their party, in the telling only. If that
// is wrong, a lone hero's scene fills with soldiers, or a story kills men the player really loses.

#region

using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ScenePlayerTextTests
   {
      // STAKE: the narrator knows what the player has, and that anything else is not there.
      [Test]
      public void GIVEN_the_player_carries_an_axe_and_a_cloak_WHEN_composed_THEN_they_are_listed_and_anything_else_is_not_there()
      {
         string text = ScenePlayerText.Compose(new ScenePlayerFacts {Carried = new[] {"Sturgian Axe", "Wolf Cloak", "Sturgian Axe", " "}});

         text.Should().Contain("WHAT THE PLAYER CARRIES").And.Contain("Sturgian Axe, Wolf Cloak.");
         text.Should().Contain("If the player reaches for anything else, it is not there");
      }

      // STAKE: a character may lend the player a weapon in the tale, and it stays in the tale: no free gear from narration.
      [Test]
      public void GIVEN_any_scene_WHEN_composed_THEN_a_gift_made_in_it_belongs_to_the_scene_only()
      {
         string text = ScenePlayerText.Compose(new ScenePlayerFacts());

         text.Should().Contain("may lend or give the player something").And.Contain("does not leave the tale with them");
      }

      // STAKE: Gunjadrid, in the nearest town, is that very lady to the people of the scene, never an old man of the wood.
      [Test]
      public void GIVEN_a_lady_in_the_nearest_town_WHEN_composed_THEN_she_is_named_as_herself_and_no_one_is_made_up_for_an_unknown_name()
      {
         string text = ScenePlayerText.Compose(new ScenePlayerFacts {
            Nearby = new[] {new ScenePersonFact {Name = "Gunjadrid of the Gundarid clan", Note = "daughter of Orvi, in Varcheg now"}}
         });

         text.Should().Contain("- Gunjadrid of the Gundarid clan: daughter of Orvi, in Varcheg now");
         text.Should().Contain("that very person").And.Contain("never make up a person to bear it");
      }

      // STAKE: companions come first, a person named twice is listed once, and the list stops at its cap.
      [Test]
      public void GIVEN_companions_and_many_lords_WHEN_composed_THEN_companions_lead_duplicates_drop_and_the_list_is_capped()
      {
         var nearby = new ScenePersonFact[30];
         for (var i = 0; i < nearby.Length; i++) nearby[i] = new ScenePersonFact {Name = "Lord " + i, Note = "in Varcheg now"};
         nearby[1] = new ScenePersonFact {Name = "Ira", Note = "in Varcheg now"};

         string text = ScenePlayerText.Compose(new ScenePlayerFacts {
            Company = new[] {new ScenePersonFact {Name = "Ira", Note = "of your company, with your party"}},
            Nearby = nearby
         });

         text.Should().Contain("- Ira: of your company");
         text.Should().NotContain("- Ira: in Varcheg now");
         System.Text.RegularExpressions.Regex.Matches(text, "(?m)^- ").Count.Should().Be(ScenePlayerText.MaxPeople);
      }

      // STAKE: a tale written for a lone hero (the wolf's forest) stays one: the party is away and nobody comes when called.
      [Test]
      public void GIVEN_a_tale_without_party_may_join_WHEN_composed_THEN_companions_are_not_in_the_scene_and_no_party_comes()
      {
         string text = ScenePlayerText.Compose(new ScenePlayerFacts {
            Company = new[] {new ScenePersonFact {Name = "Ira", Note = "of your company, with your party"}}, MenWithYou = 80
         });

         text.Should().Contain("- Ira: of your company, with your party, not in this scene");
         text.Should().NotContain("MAY JOIN THIS SCENE");
      }

      // STAKE: Gabriel (20/11/2026) let a tale bring the party in, in the telling only: no man is lost to a story, and the
      // scene never becomes a battle the map did not fight (battles stay the "defeat a band" stage).
      [Test]
      public void GIVEN_a_tale_with_party_may_join_and_men_WHEN_composed_THEN_they_may_come_when_sent_for_and_nothing_that_happens_to_them_is_real()
      {
         string text = ScenePlayerText.Compose(new ScenePlayerFacts {
            Company = new[] {new ScenePersonFact {Name = "Ira", Note = "of your company, with your party"}}, MenWithYou = 80, PartyMayJoin = true
         });

         text.Should().Contain("your companions and 80 fit soldiers").And.Contain("only if the player sends for them");
         text.Should().Contain("none of them is ever wounded, killed, captured or lost").And.Contain("never turn the scene into a battle");
         text.Should().Contain("- Ira: of your company, with your party, comes if the player sends for them");
      }

      // STAKE: a player riding alone is never told an army waits for them because the tale allows one.
      [Test]
      public void GIVEN_party_may_join_but_the_player_rides_alone_WHEN_composed_THEN_no_party_is_offered()
      {
         ScenePlayerText.Compose(new ScenePlayerFacts {PartyMayJoin = true}).Should().NotContain("MAY JOIN THIS SCENE");
      }

      // STAKE: no facts, no block (a tale read without a campaign, the tale editor's preview).
      [Test]
      public void GIVEN_no_facts_WHEN_composed_THEN_nothing_is_added()
      {
         ScenePlayerText.Compose(null).Should().BeNull();
      }
   }
}
