// Code written by Gabriel Mailhot, 01/11/2026.
// The words of a call for help (DepaHN, Nexus, 27/10/2026): the facts the writer is given, the letter's buttons and note, the
// journal entry, and what the writer remembers. The context holds only what the game proved, so the letter invents no distress.

namespace CalradiaRemembers.Logic.HelpPleas
{
   /// <summary>The words of a plea.</summary>
   public static class HelpPleaWording
   {
      /// <summary>What the writer is told to write about.</summary>
      public static string LetterContext(HelpCrisis crisis, HelpAsk ask, string place, string enemy, int amount)
      {
         string what;
         switch (crisis)
         {
            case HelpCrisis.Besieged:
               what = string.IsNullOrEmpty(enemy) ? $"{place}, a seat of your house, is under siege." : $"{place}, a seat of your house, is under siege by {enemy}.";
               break;
            case HelpCrisis.Ruined: what = "Your house has run out of money: its coffers are all but empty."; break;
            case HelpCrisis.BattleLost: what = string.IsNullOrEmpty(enemy) ? "You were just beaten in the field and got away with little." : $"You were just beaten in the field by {enemy} and got away with little."; break;
            case HelpCrisis.FiefLost: what = $"Your house has just lost {place} to the enemy."; break;
            default: what = $"{place}, a seat of your house, is running out of food."; break;
         }

         switch (ask)
         {
            case HelpAsk.Relief: return $"{what} You ask the player to ride to {place} with their men.";
            case HelpAsk.Coin: return $"{what} You ask the player for {amount} denars, and you name that sum.";
            default: return $"{what} You ask the player to bring you {amount} loads of food, by their own hand.";
         }
      }

      /// <summary>The button that says yes.</summary>
      public static string AcceptButton(HelpAsk ask, int amount)
      {
         switch (ask)
         {
            case HelpAsk.Relief: return "I will ride to you";
            case HelpAsk.Coin: return $"Send {amount} denars";
            default: return "I will bring the food";
         }
      }

      /// <summary>The button that says no: "I cannot" costs nothing and only shows when the game sees it is so.</summary>
      public static string DeclineButton(HelpIncapacity incapacity) => incapacity == HelpIncapacity.None ? "I refuse" : "I cannot";

      /// <summary>The mod speaking to the player beside the buttons, so the price of each answer is known before it is given.</summary>
      public static string Note(HelpAsk ask, HelpIncapacity incapacity, int? daysLeft)
      {
         switch (incapacity)
         {
            case HelpIncapacity.Captive: return "You are a prisoner. Nobody will hold it against you.";
            case HelpIncapacity.OwnFiefBesieged: return "Your own walls are under siege. Nobody will hold it against you.";
            case HelpIncapacity.TooFewMen: return $"You have fewer than {HelpPleaPolicy.MinMenToRide} men. Nobody will hold it against you.";
            case HelpIncapacity.TooPoor: return "You do not have the sum. Nobody will hold it against you.";
         }

         if (ask != HelpAsk.Relief) return "Refusing will disappoint them. Leaving this letter unanswered will be remembered.";

         string left = daysLeft.HasValue && daysLeft.Value > 0 ? $"You have {daysLeft.Value} days to reach them. " : "";

         return $"{left}A promise you do not keep, or a letter left unanswered, will be remembered.";
      }

      /// <summary>The journal entry of a promised relief.</summary>
      public static string JournalLine(string sender, string place, int days)
         => $"Ride to {place}, under siege, as you promised {sender}. You have {days} days from the day their letter came.";

      /// <summary>What the writer remembers, or null when nothing happened that they would.</summary>
      public static string Memory(HelpVerdict verdict, HelpAsk ask, string place, bool disaster)
      {
         string aid = ask == HelpAsk.Relief ? $"when {place} was under siege" : "when I wrote for help";
         string fell = disaster ? $" {place} fell." : "";

         switch (verdict?.Feeling)
         {
            case HelpFeeling.Thankful:
               return ask == HelpAsk.Relief ? $"The player came to {place} when I called for help." : "The player sent what I asked when I wrote for help.";
            case HelpFeeling.TooLate: return $"The player promised to come to {place} and set out, but arrived too late.{fell}";
            case HelpFeeling.Disappointed: return $"I asked the player for help {aid} and they refused.{fell}";
            case HelpFeeling.Abandoned: return $"I asked the player for help {aid} and had no answer.{fell}";
            case HelpFeeling.Betrayed: return $"The player promised to help me {aid} and never came.{fell}";
            default: return null;
         }
      }

      /// <summary>The cause on a grievance or a kindness, as the player reads it.</summary>
      public static string Cause(HelpVerdict verdict, HelpAsk ask, string place)
      {
         string aid = ask == HelpAsk.Relief ? $"when {place} was besieged" : "in their time of need";

         switch (verdict?.Feeling)
         {
            case HelpFeeling.Thankful: return $"You answered their call for help {aid}.";
            case HelpFeeling.Abandoned: return $"You left their call for help unanswered {aid}.";
            case HelpFeeling.Betrayed: return $"You promised to help {aid} and never came.";
            default: return null;
         }
      }
   }
}
