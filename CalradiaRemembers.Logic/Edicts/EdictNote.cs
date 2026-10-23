// Code written by Gabriel Mailhot, 22/10/2026.
// What a character knows of the edicts where they live (Gabriel, 21/10/2026: edicts on towns and realms, contestable).
// An edict the player can see in the town's tooltip must be one its people can speak of, or the world says one thing and
// its people another: the notables of a burdened town grumble, the lord who decreed it owns it, the realm remembers the
// edict a town forced off. One line per edict, worded here so the wording is tested.

namespace CalradiaRemembers.Logic.Edicts
{
   /// <summary>One edict as a character near it knows it.</summary>
   public sealed class EdictNoteFacts
   {
      /// <summary>The name it is known by.</summary>
      public string Name { get; init; }

      /// <summary>Who decreed it, by name (the player's character by name, never "the player").</summary>
      public string EnactorName { get; init; }

      /// <summary>The one speaking decreed it.</summary>
      public bool SpeakerDecreedIt { get; init; }

      /// <summary>Where it holds, in words ("Pravend", "all the towns of Vlandia").</summary>
      public string Where { get; init; }

      /// <summary>What it does, in words.</summary>
      public string Effects { get; init; }

      /// <summary>Days left in force; 0 when it no longer holds.</summary>
      public int DaysLeft { get; init; }

      /// <summary>The town's people have openly contested it.</summary>
      public bool Contested { get; init; }

      /// <summary>The town's people grieve under it, not yet openly.</summary>
      public bool Grumbling { get; init; }

      /// <summary>The people forced it off (it no longer holds where the speaker lives).</summary>
      public bool ForcedOff { get; init; }
   }

   /// <summary>The line a character is given about one edict where they live.</summary>
   public static class EdictNote
   {
      /// <summary>The line; null when there is nothing to know.</summary>
      public static string Line(EdictNoteFacts f)
      {
         if (f == null || string.IsNullOrWhiteSpace(f.Name)) return null;
         string by = f.SpeakerDecreedIt ? "you decreed it" : $"decreed by {f.EnactorName}";
         if (f.ForcedOff) return $"- {f.Name} ({by}) no longer holds in {f.Where}: its people forced it off.";

         string mood = f.Contested ? " Its people openly contest it." : f.Grumbling ? " The people grumble under it." : "";

         return $"- {f.Name}, {by}, holds in {f.Where}: {f.Effects}; {f.DaysLeft} days left.{mood}";
      }
   }
}
