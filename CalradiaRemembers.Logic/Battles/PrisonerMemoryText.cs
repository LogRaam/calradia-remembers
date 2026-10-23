// Code written by Gabriel Mailhot, 23/10/2026.
// What a captive, and the lord whose dungeon holds him, keep of the player's prisoners. chmidt (Nexus, 23/10/2026):
// "Lords and ladies should also know who I put in their dungeons, which they refuse to acknowledge if I tell them that I
// put an enemy lord in their own town dungeon." Gabriel (23/10/2026): a listener dedicated to prisoners, to bring what
// happens in the game into the characters' memory. Until then a lord the player took kept nothing of it (only the realm's
// news did), and nothing at all recorded a prisoner left in a dungeon or how a captivity ended.

namespace CalradiaRemembers.Logic.Battles
{
   /// <summary>How a captivity the player held ended.</summary>
   public enum CaptivityEnd
   {
      /// <summary>A ransom was paid.</summary>
      Ransomed,

      /// <summary>Peace was made and the prisoners went home.</summary>
      PeaceMade,

      /// <summary>The captive's own side won a battle and freed him.</summary>
      FreedInBattle,

      /// <summary>He escaped.</summary>
      Escaped,

      /// <summary>He was set free by choice.</summary>
      SetFree,

      /// <summary>A price was paid for him another way.</summary>
      Compensated
   }

   /// <summary>Pure: the memory lines of the player's prisoners.</summary>
   public static class PrisonerMemoryText
   {
      /// <summary>The captive's memory of being taken: "Taken prisoner by Arwa after the battle near Pravend."</summary>
      public static string Taken(string playerName, bool afterBattle, string place)
      {
         string where = string.IsNullOrWhiteSpace(place) ? string.Empty : afterBattle ? $" after the battle near {place.Trim()}" : $" near {place.Trim()}";
         if (afterBattle && where.Length == 0) where = " after a battle";

         return $"Taken prisoner by {Name(playerName)}{where}.";
      }

      /// <summary>The captive's memory of the dungeon: "Arwa left me in the dungeon of Hargard."</summary>
      public static string LeftInDungeon(string playerName, string place) => $"{Name(playerName)} left me in the dungeon of {Place(place)}.";

      /// <summary>The memory of the lord whose dungeon now holds him: "Arwa left Lucon of the dey Meroc in my dungeon at Hargard."</summary>
      public static string DepositedWithMe(string playerName, string captive, string place)
         => $"{Name(playerName)} left {(string.IsNullOrWhiteSpace(captive) ? "a prisoner" : captive.Trim())} in my dungeon at {Place(place)}.";

      /// <summary>
      ///   The captive's memory of how it ended. <paramref name="dungeon" /> names the dungeon the player left him in,
      ///   when he was not in the player's own hands; empty when the player held him.
      /// </summary>
      public static string Released(CaptivityEnd how, string playerName, string dungeon)
      {
         string who = Name(playerName);
         string held = string.IsNullOrWhiteSpace(dungeon) ? $"{who}'s captivity" : $"the dungeon of {dungeon.Trim()}, where {who} had left me";

         return how switch
         {
            CaptivityEnd.Ransomed => $"Ransomed out of {held}.",
            CaptivityEnd.PeaceMade => $"Went home from {held} when peace was made.",
            CaptivityEnd.FreedInBattle => $"Freed from {held} when my own side won the field.",
            CaptivityEnd.Escaped => $"Escaped from {held}.",
            CaptivityEnd.Compensated => $"Released from {held} for a price paid.",
            _ => string.IsNullOrWhiteSpace(dungeon) ? $"Set free by {who}." : $"Set free from {held}."
         };
      }

      private static string Name(string playerName) => string.IsNullOrWhiteSpace(playerName) ? "the player" : playerName.Trim();

      private static string Place(string place) => string.IsNullOrWhiteSpace(place) ? "a stronghold" : place.Trim();
   }
}
