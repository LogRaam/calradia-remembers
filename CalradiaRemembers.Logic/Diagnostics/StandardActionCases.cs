// Code written by Gabriel Mailhot, 18/11/2026.
// The deeds a [standard] row of cr.cost_bench must tag in its own reply. Gabriel (18/11/2026) asked for one benchmark,
// offered to players and modders as a console command, that gives action accuracy, speed, cost AND cache. INTERPRET rows
// were scored on the action catalog, but a Standard model writes the reply AND its [ACTION] blocks in one call, and
// nothing scored those blocks. The catalog cannot be fed to it as it is: a catalog case is the character's finished
// reply, which an interpreter reads; a Standard model writes that reply itself. So each case here is STAGED: earlier
// turns settle the deed (the price agreed, the lesson promised), then a last player line calls for it, or, in a false
// lead, only speaks of it for later. Gabriel (18/11/2026, option B): the bench opens the gate that teaches the verb
// itself (a lord who could ride with the player, a skill he could teach), as the cr.test fixtures do, because a lord
// as he stands is taught only gold and the end of a talk, and a score over four verbs is not the benchmark asked for.
// Nothing is executed: the call is only read for its tags.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Actions;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>The context gate a staged case opens so its verb is taught, as the game would for a character who could do it.</summary>
   public enum StandardGate
   {
      /// <summary>Nothing to open: the verb is taught to every character (gold, the end of a talk).</summary>
      None,

      /// <summary>The character could ride in the player's party (<c>ride_with_me</c>).</summary>
      CanRideWithPlayer,

      /// <summary>The character rides in the player's party now (<c>part_ways</c>).</summary>
      RidesWithPlayer,

      /// <summary>The character's own party could escort the player's (<c>follow_me</c>).</summary>
      CanEscortPlayer,

      /// <summary>The character's own party escorts the player's now (<c>dismiss_escort</c>).</summary>
      EscortsPlayer,

      /// <summary>The character could teach the player One Handed (<c>teach_skill</c>).</summary>
      CanTeachOneHanded
   }

   /// <summary>One staged case: the turns that settle the deed, the line that calls for it, and what must be tagged.</summary>
   public sealed class StandardActionCase
   {
      /// <summary>Its name in the misses the report lists.</summary>
      public string Id { get; init; } = "";

      /// <summary>How hard it is, in the INTERPRET table's terms (no action, plain, oblique, false lead).</summary>
      public InterpretDifficulty Difficulty { get; init; }

      /// <summary>The gate opened for it.</summary>
      public StandardGate Gate { get; init; }

      /// <summary>The verb the prompt must teach for the case to count; a case whose verb is not taught is skipped, never scored.</summary>
      public string Verb { get; init; } = "";

      /// <summary>The earlier turns, player then character, that settle the matter.</summary>
      public IReadOnlyList<(string Player, string Character)> Earlier { get; init; } = new List<(string, string)>();

      /// <summary>The player's last line, the one the model answers.</summary>
      public string Line { get; init; } = "";

      /// <summary>What must, or must not, be tagged (the catalog's own scorer reads it); empty Expected for a NoAction case.</summary>
      public ActionBenchCase Scoring { get; init; }
   }

   /// <summary>The staged cases, their gates and their scoring. Pure.</summary>
   public static class StandardActionCases
   {
      private static readonly (string, string)[] FordBargain = {
         ("Will you pay a hundred denars to the one who clears the bandit camp by the ford?", "A hundred, when it is done. You have my word on it.")
      };

      private static readonly (string, string)[] MarePrice = {
         ("That grey mare in your stable, would you part with her?", "For two hundred denars, she is yours.")
      };

      private static readonly (string, string)[] RideOffer = {
         ("Ride with me this season. I would have you at my side on the road.", "Ask me again tomorrow, plainly, and I will come.")
      };

      private static readonly (string, string)[] EscortOffer = {
         ("The road to Ortysia is thick with raiders. Would your men ride with mine?", "Ask it plainly and my men will follow your banner there.")
      };

      private static readonly (string, string)[] LessonOffer = {
         ("Will you teach me your swordwork?", "One lesson, in the yard, today. Fetch your blade.")
      };

      private static readonly (string, string)[] Riding = {
         ("How do you find life in my column?", "Dusty, and the cook is a menace. But I am glad to ride with you.")
      };

      private static readonly (string, string)[] Escorting = {
         ("Your men have kept good order on the road.", "They are used to it. No raider has come near us since we set out together.")
      };

      /// <summary>Every staged case, grouped by gate so the cacheable prefix changes only when the gate does.</summary>
      public static IReadOnlyList<StandardActionCase> All { get; } = new List<StandardActionCase> {
         // Nothing is done: an ordinary line must carry no deed (a change of regard is allowed).
         None("ordinary_roads", "The roads from Diatma were muddy, but the inns were warm. How have things been here?"),
         None("ordinary_mare", "I saw a fine grey mare at the market. Do you think she would suit the long ride north?"),
         None("ordinary_tax", "Tell me, what do you make of the new tax the council speaks of?"),
         None("ordinary_table", "I will stay a few days, if you will have me at your table."),

         // Gold and the end of a talk: taught to everyone.
         Positive("give_gold_agreed", InterpretDifficulty.Simple, StandardGate.None, "give_gold", FordBargain,
            "It is done: the camp is ash and their leader hangs at the ford. I have come for the hundred we agreed.", "give_gold", ("amount", "100")),
         Positive("give_gold_oblique", InterpretDifficulty.Oblique, StandardGate.None, "give_gold", FordBargain,
            "The ford is quiet now, and its bandits will trouble no one again. You said something would be waiting for the one who made it so.", "give_gold", ("amount", "100")),
         FalseLead("give_gold_later", StandardGate.None, "give_gold", FordBargain,
            "Good. I ride for the ford next week; when the camp is cleared, you will pay me the hundred, yes?"),
         Positive("take_gold_paid", InterpretDifficulty.Simple, StandardGate.None, "take_gold", MarePrice,
            "*I count two hundred denars into your hand.* There, the price you named.", "take_gold", ("amount", "200")),
         Positive("take_gold_oblique", InterpretDifficulty.Oblique, StandardGate.None, "take_gold", MarePrice,
            "*I set a small, heavy pouch on the table and slide it across to you.* Counted twice. She is mine, then.", "take_gold", ("amount", "200")),
         FalseLead("take_gold_later", StandardGate.None, "take_gold", MarePrice,
            "Agreed. I will bring you the two hundred when I am back from the market tomorrow."),
         Positive("end_farewell", InterpretDifficulty.Simple, StandardGate.None, "end_conversation", new (string, string)[0],
            "I must ride before dark. Farewell, and keep well until I return.", "end_conversation"),
         Positive("end_oblique", InterpretDifficulty.Oblique, StandardGate.None, "end_conversation", new (string, string)[0],
            "The hour is late and my horse is saddled at the gate. May your gods keep you.", "end_conversation"),
         FalseLead("end_one_more_question", StandardGate.None, "end_conversation", new (string, string)[0],
            "Before I go, one more question: who holds the bridge at Epicrotea now?"),

         // Riding in the player's party.
         Positive("ride_with_me_asked", InterpretDifficulty.Simple, StandardGate.CanRideWithPlayer, "ride_with_me", RideOffer,
            "I ask it again, plainly: ride with me. We leave at dawn.", "ride_with_me"),
         FalseLead("ride_with_me_someday", StandardGate.CanRideWithPlayer, "ride_with_me", RideOffer,
            "Not yet, though. When the war is over, perhaps I will ask you again."),
         Positive("part_ways_sent_home", InterpretDifficulty.Simple, StandardGate.RidesWithPlayer, "part_ways", Riding,
            "You have ridden with me long enough, and your house needs you. Go home with my thanks; we part here.", "part_ways"),
         Positive("part_ways_oblique", InterpretDifficulty.Oblique, StandardGate.RidesWithPlayer, "part_ways", Riding,
            "Your own banners are needed at home more than in my column. I will not keep you from them any longer.", "part_ways"),

         // The character's own party escorting the player's.
         Positive("follow_me_asked", InterpretDifficulty.Simple, StandardGate.CanEscortPlayer, "follow_me", EscortOffer,
            "Then I ask it plainly: bring your men and follow my party to Ortysia.", "follow_me"),
         FalseLead("follow_me_next_month", StandardGate.CanEscortPlayer, "follow_me", EscortOffer,
            "I may ask that of you next month. Keep your men ready until then."),
         Positive("dismiss_escort_done", InterpretDifficulty.Simple, StandardGate.EscortsPlayer, "dismiss_escort", Escorting,
            "The road ahead is safe now. Take your men home; your escort is done, with my thanks.", "dismiss_escort"),

         // A lesson.
         Positive("teach_skill_lesson", InterpretDifficulty.Simple, StandardGate.CanTeachOneHanded, "teach_skill", LessonOffer,
            "*I draw my sword in the yard.* I am ready for the lesson you promised.", "teach_skill", ("skill", "One Handed")),
         FalseLead("teach_skill_someday", StandardGate.CanTeachOneHanded, "teach_skill", LessonOffer,
            "Not today, my arm still aches from the ride. Some other day you must show me.")
      };

      /// <summary>
      ///   The <see cref="EncounterContext" /> facts each gate sets, by property name, as the game sets them for a character who
      ///   could do the deed. The two halves of a pair are set both ways, so a character is never told both at once.
      /// </summary>
      public static IReadOnlyDictionary<string, object> GateFacts(StandardGate gate)
      {
         switch (gate)
         {
            case StandardGate.CanRideWithPlayer:
               return new Dictionary<string, object> {
                  {nameof(EncounterContext.NpcCanRideWithPlayer), true}, {nameof(EncounterContext.NpcIsRidingWithPlayer), false},
                  {nameof(EncounterContext.NpcCanEscortPlayer), false}
               };
            case StandardGate.RidesWithPlayer:
               return new Dictionary<string, object> {
                  {nameof(EncounterContext.NpcIsRidingWithPlayer), true}, {nameof(EncounterContext.NpcCanRideWithPlayer), false}
               };
            case StandardGate.CanEscortPlayer:
               return new Dictionary<string, object> {
                  {nameof(EncounterContext.NpcCanEscortPlayer), true}, {nameof(EncounterContext.NpcIsEscortingPlayer), false},
                  {nameof(EncounterContext.NpcCanRideWithPlayer), false}
               };
            case StandardGate.EscortsPlayer:
               return new Dictionary<string, object> {
                  {nameof(EncounterContext.NpcIsEscortingPlayer), true}, {nameof(EncounterContext.NpcCanEscortPlayer), false}
               };
            case StandardGate.CanTeachOneHanded:
               return new Dictionary<string, object> {{nameof(EncounterContext.TeachableSkills), (IReadOnlyList<string>) new List<string> {"One Handed"}}};
            default:
               return new Dictionary<string, object>();
         }
      }

      /// <summary>Whether the prompt the model read teaches the case's verb; an untaught verb cannot be scored fairly.</summary>
      public static bool Taught(StandardActionCase test, string prompt)
         => string.IsNullOrEmpty(test?.Verb) || (prompt ?? "").Contains(test.Verb);

      /// <summary>
      ///   A reply's tags scored: an ordinary line by what it emitted (only a change of regard allowed), a staged deed by the
      ///   catalog's own scorer, exactly as an INTERPRET reading is.
      /// </summary>
      public static (bool Right, bool Invented) Score(StandardActionCase test, IReadOnlyList<GameAction> emitted)
      {
         IReadOnlyList<GameAction> tags = emitted ?? new List<GameAction>();
         if (test.Difficulty == InterpretDifficulty.NoAction)
         {
            bool right = InterpretCases.NoActionReadRight(tags.Select(a => a.Type));

            return (right, !right);
         }

         return InterpretCases.Read(ActionBenchScorer.Score(tags, test.Scoring));
      }

      #region private

      private static StandardActionCase None(string id, string line)
         => new StandardActionCase {
            Id = id, Difficulty = InterpretDifficulty.NoAction, Gate = StandardGate.None, Line = line,
            Scoring = ActionBenchCase.ExpectNone(id, "none", InterpretCases.OrdinaryContext, line, "none")
         };

      private static StandardActionCase Positive(string id, InterpretDifficulty difficulty, StandardGate gate, string verb, (string, string)[] earlier, string line,
                                                 string expected, (string Key, string Value)? param = null)
         => new StandardActionCase {
            Id = id, Difficulty = difficulty, Gate = gate, Verb = verb, Earlier = earlier, Line = line,
            Scoring = ActionBenchCase.Expect(id, verb, "", line, expected,
               param == null ? null : new Dictionary<string, string> {{param.Value.Key, param.Value.Value}})
         };

      private static StandardActionCase FalseLead(string id, StandardGate gate, string verb, (string, string)[] earlier, string line)
         => new StandardActionCase {
            Id = id, Difficulty = InterpretDifficulty.FalseLead, Gate = gate, Verb = verb, Earlier = earlier, Line = line,
            Scoring = ActionBenchCase.ExpectNone(id, verb, "", line, verb)
         };

      #endregion
   }
}
