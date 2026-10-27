// Code written by Gabriel Mailhot, 27/10/2026.
// DepaHN (Nexus, 26/10/2026): "The relationship with my wife is excellent, but relationships with children and
// grandchildren feel very cold, as do those with brothers, sisters, and relatives [...] A strong family should have depth
// and permanence." A kinsman was told he is family only while he shared no memory with the player, and grandchildren,
// nephews, nieces and cousins were never counted as family at all; the wife alone had a lasting line (Eisendrache2,
// 25/10/2026). This names what a character is to the player, by blood, for a line that stays. Gabriel: "oui fait les 3
// correctifs".

namespace CalradiaRemembers.Logic
{
   /// <summary>How a character is related to the player by blood, the closest bond first wins.</summary>
   public sealed class PlayerKinFacts
   {
      /// <summary>The character is a woman (for the word).</summary>
      public bool IsFemale { get; init; }

      /// <summary>The player is the character's parent.</summary>
      public bool IsChild { get; init; }

      /// <summary>The character is the player's parent.</summary>
      public bool IsParent { get; init; }

      /// <summary>They share a parent.</summary>
      public bool IsSibling { get; init; }

      /// <summary>The player is a parent of the character's parent.</summary>
      public bool IsGrandchild { get; init; }

      /// <summary>The character is a parent of the player's parent.</summary>
      public bool IsGrandparent { get; init; }

      /// <summary>The character's parent is the player's sibling.</summary>
      public bool IsNephewOrNiece { get; init; }

      /// <summary>The character is a sibling of the player's parent.</summary>
      public bool IsUncleOrAunt { get; init; }

      /// <summary>Their parents are siblings.</summary>
      public bool IsCousin { get; init; }
   }

   /// <summary>The word for what a character is to the player by blood, or null when they are not kin.</summary>
   public static class PlayerKinshipPolicy
   {
      /// <summary>"son", "granddaughter", "aunt", "cousin"..., the closest bond first; null when no bond of blood is known.</summary>
      public static string Label(PlayerKinFacts f)
      {
         if (f == null) return null;
         bool w = f.IsFemale;
         if (f.IsChild) return w ? "daughter" : "son";
         if (f.IsParent) return w ? "mother" : "father";
         if (f.IsSibling) return w ? "sister" : "brother";
         if (f.IsGrandchild) return w ? "granddaughter" : "grandson";
         if (f.IsGrandparent) return w ? "grandmother" : "grandfather";
         if (f.IsNephewOrNiece) return w ? "niece" : "nephew";
         if (f.IsUncleOrAunt) return w ? "aunt" : "uncle";
         if (f.IsCousin) return "cousin";

         return null;
      }
   }
}
