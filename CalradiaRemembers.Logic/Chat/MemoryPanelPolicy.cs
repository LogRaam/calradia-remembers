// Code written by Gabriel Mailhot, 20/10/2026.
// tashmetu (20/10/2026): "the Memories window lists everything a character remembers, private memories included, so a
// player read about their character's wife's secret affair in her lover's memories before anything had come to light.
// Could the window leave private memories out, or mark them as secret [...]? A setting would do as well, for players who
// like to see everything." Gabriel approved (20/10/2026). A private memory another mod writes (CrMemoryEntry.Private) is a
// secret the player has not learned; the private memories CR writes itself are the player's own word to a companion
// (the briefing), which the player said and may read again.

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>How one memory appears in the chat's Memories panel.</summary>
   public enum MemoryPanelShow
   {
      /// <summary>Listed as it is.</summary>
      Plain,

      /// <summary>Listed, marked as a secret.</summary>
      MarkedSecret,

      /// <summary>Left out of the panel. The character still remembers it.</summary>
      Hidden
   }

   /// <summary>What decides whether one memory is listed.</summary>
   public sealed class MemoryPanelFacts
   {
      /// <summary>The memory is the holder's alone (NotableEvent.IsPrivate): never spoken of in front of others.</summary>
      public bool IsPrivate { get; init; }

      /// <summary>
      ///   Another mod wrote it (its SourceMod, CR excluded). A private memory of CR's own is the player's word to a companion,
      ///   which the player knows; one from another mod is a secret the player may not have learned.
      /// </summary>
      public bool FromAnotherMod { get; init; }

      /// <summary>The player asked, in the mod options, to see the secrets too.</summary>
      public bool ShowSecrets { get; init; }
   }

   /// <summary>Decides how a memory appears in the Memories panel.</summary>
   public static class MemoryPanelPolicy
   {
      /// <summary>The SourceMod CR gives the memories it writes itself through its own door.</summary>
      public const string OwnSource = "CalradiaRemembers";

      /// <summary>How the memory appears: a secret from another mod is left out unless the player asked to see them.</summary>
      public static MemoryPanelShow Show(MemoryPanelFacts f)
      {
         if (f == null || !f.IsPrivate || !f.FromAnotherMod) return MemoryPanelShow.Plain;

         return f.ShowSecrets ? MemoryPanelShow.MarkedSecret : MemoryPanelShow.Hidden;
      }

      /// <summary>Whether a SourceMod names another mod than CR (blank is CR's own memory).</summary>
      public static bool IsAnotherMod(string sourceMod)
         => !string.IsNullOrWhiteSpace(sourceMod) && !string.Equals(sourceMod.Trim(), OwnSource, System.StringComparison.OrdinalIgnoreCase);
   }
}
