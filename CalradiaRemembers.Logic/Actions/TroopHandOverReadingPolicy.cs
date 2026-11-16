// Code written by Gabriel Mailhot, 09/11/2026.
// Men a lord asked for are his errand, whatever the reader called them.
//
// deliver_recruits closes a lord's recruit task (Lordfadooboo, Nexus, 01/10/2026: "recruitment quests"); give_troops
// reinforces an under-strength party with the player's lowest-tier soldiers and closes nothing. On the cost bench
// (09/11/2026; Gabriel: "vas-y pour l'interprete, les refus et give_troops"), a lord taking the fresh men he had asked
// for ("Fresh men, as I asked. You have my thanks.") was read as give_troops by both measured providers. In the game the
// reader knows even less than on the bench: its facts never carry the player's open tasks. Read as give_troops, the
// men move (or are refused for want of need) and the task stays open.
//
// So the game decides from what it knows: when this lord has an open recruit task and the player holds every recruit
// it asks for, a give_troops toward him is the hand-over of that task. Without enough recruits it stays give_troops,
// so a player who offers other soldiers to a thin lord is not refused because of an errand they have not finished.
//
// The same for the walls (Aporo, Discord, 16/11/2026; Gabriel: "go"): Queen Rhagaea asked for men for the garrison of
// Onira; the reader wrote give_troops, which reinforces a thin FIELD party, so it was refused "no room or need for more
// soldiers" and the task stayed open. When the character's garrison task waits in the very place the player stands, a
// give_troops toward them is that hand-over, whatever the count: reinforce_garrison then says how many men are missing,
// where give_troops could only say none were needed.

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>The facts about a give_troops toward a lord, as far as his recruit task is concerned.</summary>
   public sealed class TroopHandOverFacts
   {
      /// <summary>This lord has an outstanding task asking the player for fresh recruits of his people.</summary>
      public bool RecruitTaskOutstanding { get; init; }

      /// <summary>The player's party holds, fit to march, at least as many matching recruits as the task asks.</summary>
      public bool PlayerHoldsTheRecruitsAsked { get; init; }
   }

   /// <summary>The facts about a give_troops, as far as the character's garrison task is concerned.</summary>
   public sealed class GarrisonHandOverFacts
   {
      /// <summary>The character has an outstanding garrison task for the settlement the player stands in now.</summary>
      public bool GarrisonTaskWaitingHere { get; init; }
   }

   /// <summary>Whether a give_troops is the hand-over of a lord's recruit task, or of a garrison task.</summary>
   public static class TroopHandOverReadingPolicy
   {
      /// <summary>True when the give_troops should be carried out as deliver_recruits, closing the task.</summary>
      public static bool IsTheRecruitTask(TroopHandOverFacts facts)
         => facts != null && facts.RecruitTaskOutstanding && facts.PlayerHoldsTheRecruitsAsked;

      /// <summary>True when the give_troops should be carried out as reinforce_garrison (which counts the men itself).</summary>
      public static bool IsTheGarrisonTask(GarrisonHandOverFacts facts)
         => facts != null && facts.GarrisonTaskWaitingHere;
   }
}
