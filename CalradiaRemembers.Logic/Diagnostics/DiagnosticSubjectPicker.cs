// Code written by Gabriel Mailhot, 02/10/2026.
// tashmetu (Nexus, 02/10/2026): "cr.prompt_dump <name> picks the first match when names repeat. Could it also accept
// a hero id?" Which character a diagnostic command means, chosen from what the modder typed: an exact hero id, else
// an exact name, else a fragment of a name; and everyone else who answered, so a namesake is never dumped in silence.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>A character a diagnostic command may pick: its hero id and its display name.</summary>
   public sealed class DiagnosticSubject
   {
      /// <summary>The hero's StringId.</summary>
      public string Id { get; init; } = "";

      /// <summary>The hero's display name.</summary>
      public string Name { get; init; } = "";
   }

   /// <summary>The character picked, and the others who answered equally well.</summary>
   public sealed class DiagnosticPick
   {
      /// <summary>The character the command acts on; null when nobody answered.</summary>
      public DiagnosticSubject Chosen { get; init; }

      /// <summary>Everyone else who answered at the same rank, to be named with their ids.</summary>
      public IReadOnlyList<DiagnosticSubject> Others { get; init; } = Array.Empty<DiagnosticSubject>();
   }

   /// <summary>Picks a diagnostic command's subject from what was typed. Pure.</summary>
   public static class DiagnosticSubjectPicker
   {
      /// <summary>An exact hero id first, then an exact name, then a name containing the text, all ignoring case.</summary>
      public static DiagnosticPick Pick(IEnumerable<DiagnosticSubject> candidates, string typed)
      {
         List<DiagnosticSubject> all = (candidates ?? Enumerable.Empty<DiagnosticSubject>()).Where(c => c != null).ToList();
         string t = typed?.Trim() ?? "";
         if (t.Length == 0) return new DiagnosticPick();

         DiagnosticSubject byId = all.FirstOrDefault(c => string.Equals(c.Id, t, StringComparison.OrdinalIgnoreCase));
         if (byId != null) return new DiagnosticPick {Chosen = byId};

         List<DiagnosticSubject> matches = all.Where(c => string.Equals(c.Name?.Trim(), t, StringComparison.OrdinalIgnoreCase)).ToList();
         if (matches.Count == 0)
            matches = all.Where(c => c.Name != null && c.Name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

         return matches.Count == 0
            ? new DiagnosticPick()
            : new DiagnosticPick {Chosen = matches[0], Others = matches.Skip(1).ToList()};
      }
   }
}
