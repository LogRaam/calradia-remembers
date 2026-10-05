// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales (docs/design/quest-editor/goal.md): the small tale the branching tests walk, read by the real
// loader from JSON exactly as a file in ModuleData/Quests would be. It holds one of each new thing: a stage left by
// whichever act comes first (riding to the village, going back to the lord, or letting days pass), a scene left by
// the player's choice (a path open only once a light was seen, another always, a default if they just walk away),
// flag-gated lines, and three endings with their own regard, memory and consequences.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;

#endregion

namespace CalradiaRemembers.Tests
{
   internal static class AuthoredTales
   {
      public const string Giver = "lord_ortongard";
      public const string Village = "village_ortongard";

      public const string Fork = """
      {
        "id": "cr_test_fork",
        "title": "The Fork in the Wood",
        "binds": { "giver": "a lord", "village": "a village by a wood" },
        "stages": [
          { "id": "ask", "journal": "{giver} asks you to find the shepherd boy of {village}.",
            "objective": { "type": "talk_to", "hero": "{giver}" },
            "canvas": "The lord is worried for the boy.",
            "facts": [ "The boy went into the wood three days ago." ],
            "on_complete": { "next": "the_road" } },
          { "id": "the_road", "journal": "The boy is lost somewhere near {village}.",
            "knowers": { "{giver}": [ "You sent the player after the boy and wait for news." ] },
            "choices": [
              { "id": "ride_out", "label": "Ride to {village}", "journal": "Ride to {village} and search the wood.",
                "objective": { "type": "be_at", "place": "{village}" }, "next": "the_wood" },
              { "id": "give_up", "label": "Tell {giver} you will not go", "journal": "Or tell {giver} it is not your affair.",
                "objective": { "type": "talk_to", "hero": "{giver}" }, "sets": "refused", "next": "end:abandoned",
                "canvas": "The lord hides his disappointment badly.",
                "knowers": { "{giver}": [ "The player has just refused to look for the boy." ] } },
              { "id": "let_it_be", "label": "Let the days pass",
                "objective": { "type": "wait", "days": 3 }, "next": "end:abandoned" }
            ] },
          { "id": "the_wood", "journal": "Search the wood by {village}.",
            "objective": { "type": "explore", "scene": {
              "opening": { "canvas": "Dusk in the wood. The path splits.", "facts": [ "The path splits left and right." ] },
              "findings": [ { "id": "lantern", "required": false, "text": "A light moving between the trees to the left.", "sets": "saw_light" } ],
              "closing": "You leave the fork behind."
            } },
            "choices": [
              { "id": "left", "label": "Follow the light to the left", "if": "saw_light", "next": "the_mill" },
              { "id": "right", "label": "Take the right path down to the river", "next": "the_river" },
              { "id": "turn_back", "label": "Turn back", "default": true, "sets": "turned_back", "next": "end:abandoned" }
            ] },
          { "id": "the_mill", "journal": "Bring the boy home to {giver}.",
            "objective": { "type": "talk_to", "hero": "{giver}" },
            "canvas": [ "The lord's relief is plain.", { "text": "He asks about the light in the wood.", "if": "saw_light" } ],
            "knowers": {
              "{giver}": [ "The player found the boy at the old mill.", { "text": "The boy says smugglers keep a lantern there.", "if": "saw_light" },
                           { "text": "Nobody has told you of any light.", "if": "!saw_light" } ],
              "locals_of:{village}": [ "The shepherd boy is said to be found." ] },
            "on_complete": { "next": "end:rescued" } },
          { "id": "the_river", "journal": "Tell {giver} what the river showed you.",
            "objective": { "type": "talk_to", "hero": "{giver}" },
            "knowers": { "{giver}": [ "The player found only the boy's cap by the river." ] },
            "on_complete": { "next": "end:drowned" } }
        ],
        "endings": {
          "rescued": { "regard": 10, "gold": 200, "memory": "The player brought the boy of {village} home.",
                       "journal": "The boy is home.",
                       "consequences": [ { "type": "memory", "who": "locals_of:{village}", "text": "A stranger brought our boy home." },
                                         { "type": "world_event", "summary": "Smugglers keep a lantern at the old mill of {village}.", "magnitude": 20, "place": "{village}", "if": "saw_light" } ] },
          "drowned": { "regard": 2, "memory": "The player found the boy's cap by the river." },
          "abandoned": { "regard": -4, "memory": "I asked the player to find the boy, and nothing came of it.",
                         "consequences": [ { "type": "memory", "who": "{giver}", "text": "The player would not even go.", "if": "refused" } ] }
        },
        "failure": { "days": 30, "regard": -3, "memory": "The boy was never found." }
      }
      """;

      /// <summary>The tale through the real loader; it must load clean, or every test built on it is meaningless.</summary>
      public static AuthoredQuest Load(string json = Fork)
      {
         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse(json, "test.json");
         r.Problems.Should().BeEmpty();

         return r.Quests.Single();
      }

      /// <summary>What the loader says of a file, problems and all.</summary>
      public static AuthoredQuestLoadResult Read(string json) => AuthoredQuestLoader.Parse(json, "test.json");

      /// <summary>A run bound as the game binds one: the giver and the village, by id and by name.</summary>
      public static AuthoredQuestRun Start(AuthoredQuest q, int today = 100)
         => AuthoredQuestEngine.Start(q,
            new Dictionary<string, string> {["giver"] = Giver, ["village"] = Village},
            new Dictionary<string, string> {["giver"] = "Lord Aldric", ["village"] = "Ortongard"}, today);
   }
}
