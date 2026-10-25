// Code written by Gabriel Mailhot, 25/10/2026.
// The actions the prose model is taught by name in the GAME ACTIONS list, when the same call reads its own actions
// (Standard composition). Moved out of LlmServices (25/10/2026) so the composition-parity map renders the prose
// prompt with the very list the game builds, rather than a copy (Gabriel, 25/10/2026: "il ne devrait pas avoir de
// differences entre [Enhanced et le mode regulier], car ce sont juste deux facons differentes d'interpreter les
// actions dans une conversation"). The wording is unchanged: five verbs written by hand (change_relation tuned by
// the Relationship Warmth setting), then any externally registered action, so a third-party verb reaches this path too.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Actions;
using NpcMemoryService.Core.Prompts;

#endregion

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>The action vocabulary of the prose prompt (Standard composition): what its GAME ACTIONS list may name.</summary>
   public static class ProseActionVocabulary
   {
      /// <summary>
      ///   The player's violence in words (rob, assault, kill), taught from its catalogue spec. Gabriel (25/10/2026), once
      ///   the composition-parity map showed these taught to the interpreter alone: "enseigne la violence aux deux modes".
      ///   They had been kept out of the prose prompt so an ordinary conversation was never invited to violence, which
      ///   meant a player in Standard composition could never rob, beat or kill anyone in words. The bridge still re-checks
      ///   every one (ViolencePolicy, the killing option), exactly as it does for the interpreter's.
      /// </summary>
      public static readonly IReadOnlyList<string> PlayerViolenceVerbs = new[] {"rob_npc", "assault_npc", "kill_npc"};

      /// <summary>
      ///   The five hand-written verbs, change_relation worded for <paramref name="warmth" />, the player's violence
      ///   (<see cref="PlayerViolenceVerbs" />) from its catalogue spec, then every externally registered catalogue action
      ///   (other built-ins are taught by their own sections, so none is taught twice).
      /// </summary>
      public static IReadOnlyList<GameActionDefinition> Build(RelationWarmth warmth)
      {
         var vocabulary = new List<GameActionDefinition> {
            new() {
               Type = "change_relation",
               Description = ChangeRelationDescription(warmth),
               Parameters = new List<string> {"delta"}
            },
            new() {
               Type = "end_conversation",
               Description = "End this conversation now. Emit when you, in character, would genuinely refuse to continue: " +
                             "after a grave insult, a crude or unwelcome advance you will not tolerate, " +
                             "an advance you already refused that the player presses again, repeated disrespect, an open threat \u2014 " +
                             "or when the PLAYER winds the talk down (a goodbye, a parting word) and you answer in kind. " +
                             "NEVER end on a turn where your own words still engage the player: if you ask a question you want answered, " +
                             "make a request of them, or otherwise await a reply, the conversation MUST stay open, so do NOT emit this action. " +
                             "By default the talk stays open and the PLAYER decides when they are done. A rhetorical outburst as you dismiss them " +
                             "('How dare you?') is still a dismissal and DOES end; a genuine question you expect them to answer does not. " +
                             "CRUCIAL \u2014 your words and this action must AGREE: if your dialogue this turn dismisses the player or " +
                             "declares the talk over ('I have duties that demand my attention', 'I will hear no more of this today', " +
                             "'we are done here', a firm farewell, or sending them on their way), you MUST emit this action so the " +
                             "conversation actually ends. Do not say goodbye or refuse to hear more while leaving the conversation open. " +
                             "When you end because the player wronged or disrespected you, you MUST also emit change_relation with a " +
                             "negative delta matching the offense. Do not end over a single clumsy remark or mere awkwardness; reserve " +
                             "it for a real breach, a clear dismissal, or a true farewell.",
               Parameters = new List<string>()
            },
            new() {
               Type = "witness_leaves",
               Description = "One of the PLAYER'S OWN companions leaves the conversation. Emit ONLY when the player " +
                             "explicitly asks one of their companions to step away or leave the two of you alone \u2014 " +
                             "never on your own initiative. This applies only to witnesses marked as the player's " +
                             "companion; your own people do not answer to the player (a private audience is the way " +
                             "to clear the room). Set 'name' to the companion's name exactly as listed under " +
                             "WITNESSES PRESENT, and acknowledge the departure naturally in [DIALOGUE].",
               Parameters = new List<string> {"name"}
            },
            new() {
               Type = "give_gold",
               Description = "Transfer gold from you to the player (payment, reward, gift, bribe). " +
                             "'amount' is a whole number of denars. " +
                             "The amount must reflect your actual willingness: do not give what you would not give. " +
                             "DIRECTION CHECK: use this ONLY when YOU hand YOUR OWN coin to the player. When the " +
                             "PLAYER is the one paying, including in a stage direction such as *hands you 1000 " +
                             "denars* or *he gives her the coin* (the player writing about themselves in the third " +
                             "person), that is take_gold, never give_gold.",
               Parameters = new List<string> {"amount"}
            },
            new() {
               Type = "take_gold",
               Description = "Transfer gold from the player to you (tribute, fee, debt repaid). " +
                             "'amount' is a whole number of denars. " +
                             "Only emit if the player has agreed or you have the authority to demand it. " +
                             "This includes when the player mimes the payment in a stage direction (*hands you " +
                             "500 denars*, *he gives her the money*): the player is the one paying, so it is " +
                             "take_gold, not give_gold.",
               Parameters = new List<string> {"amount"}
            }
         };

         // Extension Surface (CrActions.Register): teach the 1:1 prose prompt any externally-registered
         // action too, so a third-party mod's verb is not only visible to the Action Interpreter path.
         // Built-ins are skipped (already hand-authored above, with tuned wording) so none is taught twice.
         // In normal play nothing is registered, so this adds nothing and the vocabulary stays unchanged.
         foreach (GameActionSpec spec in GameActionCatalog.All)
         {
            if (GameActionCatalog.BuiltInTypes.Contains(spec.Type) && !PlayerViolenceVerbs.Contains(spec.Type)) continue;

            vocabulary.Add(new GameActionDefinition {
               Type = spec.Type,
               Description = spec.Description,
               Parameters = spec.Parameters.Select(p => p.Name).ToList()
            });
         }

         return vocabulary;
      }

      /// <summary>
      ///   Assembles the change_relation instruction. The opening, the negative scale,
      ///   and the closing are constant at every warmth level; only the guidance on
      ///   WHEN a positive gain is warranted changes, so insults stay equally costly
      ///   while the player's chosen warmth controls how readily goodwill is earned.
      /// </summary>
      private static string ChangeRelationDescription(RelationWarmth warmth)
      {
         const string opening =
            "Reflect a genuine shift in how you regard the player \u2014 this is the ONLY way your relationship with them changes. ";

         string positive;
         switch (warmth)
         {
            case RelationWarmth.Easier:
               positive =
                  "Emit +1 whenever the player genuinely pleases you this exchange \u2014 a well-turned compliment, shared humor, " +
                  "an engaging story, real sympathy or support, a pledge of loyalty or service you welcome, sincere praise of " +
                  "your deeds or leadership, or simply good company. Good company earns goodwill here: do not " +
                  "withhold a small gain merely because no grand deed was done. A larger positive is reserved for a truly " +
                  "significant, self-evident moment such as physical intimacy. ";

               break;

            case RelationWarmth.VeryEasy:
               positive =
                  "Emit +1 \u2014 occasionally +2 \u2014 for almost any sincerely positive exchange: pleasant talk, a kind word, a shared " +
                  "laugh, a pledge of support, praise of your deeds or your leadership, warm words after a shared victory, " +
                  "a moment of real connection. You warm to people readily and give them the benefit of the doubt; you need " +
                  "no grand deed to feel goodwill. When in doubt whether an exchange earned the +1, emit it rather than " +
                  "withhold it. Reserve larger gains for a truly significant, self-evident moment such as physical intimacy. ";

               break;

            default: // Normal \u2014 the balanced baseline
               positive =
                  "Emit when the player earns it through something real \u2014 never for idle flattery or empty promises. A +1 is right " +
                  "when this exchange genuinely warms you: a service rendered or a promise kept, a real step forward between you " +
                  "(courtship advancing, trust shown, respect earned through deeds), sincere support in a moment that mattered, or a " +
                  "connection that rings true. A larger positive is reserved for a truly significant, self-evident moment \u2014 completing " +
                  "a real task you set them, or physical intimacy. Trust is slow to grow and quick to break \u2014 but do not stay frozen at " +
                  "neutral when the player has actually earned your regard; a genuinely earned moment should move it. ";

               break;
         }

         const string negative =
            "For a wrong done to you, make 'delta' negative and scale it to the gravity \u2014 " +
            "a cold rebuff (-2), a crude or unwelcome advance (-5), a pointed insult (-5), " +
            "a lewd proposition made in front of others (-8), the betrayal of a real agreement (-15); " +
            "if you are the player's captive and they treat you with cruelty, your regard may collapse entirely. " +
            "Do NOT stay neutral when the player is disrespectful, vulgar, or presses an advance you have already refused \u2014 your regard must fall. ";

         const string closing =
            "CONSISTENCY: the sign of 'delta' must match the tone of your OWN reply this turn. If you answer the player " +
            "warmly, with approval or affection, 'delta' is 0 or positive, never negative; make it negative ONLY when " +
            "your reply itself is cold, hurt, angry, or displeased. A friendly reply paired with a negative delta is a " +
            "contradiction. The game enforces pacing and limits, so state what you feel honestly and let it decide what lands.";

         return opening + positive + negative + closing;
      }
   }
}
