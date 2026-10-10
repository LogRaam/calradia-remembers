// Code written by Gabriel Mailhot, 10/10/2026.
// philip0105 (Nexus, 10/10/2026), Vivid World: what a character knows in his mod keeps changing (forgotten rumors,
// denials, an older save rolling them back), and memories copied into CR with AddMemory could never be taken back, so
// the two drifted apart. A mod may now remove what it added: the memory's exact text, and only its own.

namespace CalradiaRemembers.Logic
{
   /// <summary>A stored memory, and the removal a mod asks for.</summary>
   public sealed class MemoryRemovalFacts
   {
      /// <summary>The memory's text as CR stores it.</summary>
      public string StoredSummary { get; init; }

      /// <summary>The mod that added it, or null (CR's own memory, or an injected one stored before mods were recorded).</summary>
      public string StoredSourceMod { get; init; }

      /// <summary>The text the mod passed to AddMemory.</summary>
      public string RequestedSummary { get; init; }

      /// <summary>The mod asking.</summary>
      public string RequestedSourceMod { get; init; }
   }

   /// <summary>Whether a mod's removal takes this memory. Pure.</summary>
   public static class InjectedMemoryRemovalPolicy
   {
      /// <summary>
      ///   The text matches as AddMemory stored it (bounded and trimmed), and the memory is this mod's: added by it, or
      ///   recorded before mods were kept with memories. A memory another mod added is never taken.
      /// </summary>
      public static bool Removes(MemoryRemovalFacts f)
      {
         if (f == null || string.IsNullOrWhiteSpace(f.StoredSummary)) return false;
         string requested = ExternalEventRules.BoundSummary(f.RequestedSummary);
         if (requested == null || !string.Equals(f.StoredSummary.Trim(), requested.Trim(), System.StringComparison.Ordinal)) return false;
         if (string.IsNullOrWhiteSpace(f.StoredSourceMod)) return true;

         return string.Equals(f.StoredSourceMod.Trim(), (f.RequestedSourceMod ?? "").Trim(), System.StringComparison.OrdinalIgnoreCase);
      }
   }
}
