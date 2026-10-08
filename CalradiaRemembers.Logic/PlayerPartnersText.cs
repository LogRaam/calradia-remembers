// Code written by Gabriel Mailhot, 08/10/2026.
// The player's partners know of each other. Windmolen (Nexus, 08/10/2026): the player took as a second wife his first
// wife's mother, and the first wife wrote of "my mother caring for the children" for a year without knowing; her family
// line said "Mother: X" and nothing more, and no partner was ever told who the others were. Gabriel: such a match stays
// possible, but everyone knows.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>One other partner of the player, as a partner sees them.</summary>
   public sealed class PartnerFacts
   {
      public string Name { get; init; }

      /// <summary>The player's spouse (else a consort).</summary>
      public bool IsSpouse { get; init; }

      /// <summary>What they are to the character told ("your mother"), or null.</summary>
      public string KinToSubject { get; init; }
   }

   /// <summary>Lines about the player's partners. Pure.</summary>
   public static class PlayerPartnersText
   {
      /// <summary>Appended to a relative in a family line who is also one of the player's partners.</summary>
      public static string KinNote(bool isSpouse) => isSpouse ? " (also the player's spouse)" : " (also the player's consort)";

      /// <summary>"The player's other partners: Rhagaea, their consort, who is your mother; Ira, their spouse.", or null.</summary>
      public static string OtherPartners(IReadOnlyList<PartnerFacts> others)
      {
         List<PartnerFacts> named = others?.Where(o => !string.IsNullOrWhiteSpace(o?.Name)).ToList() ?? new List<PartnerFacts>();
         if (named.Count == 0) return null;

         IEnumerable<string> parts = named.Select(o =>
            $"{o.Name.Trim()}, their {(o.IsSpouse ? "spouse" : "consort")}{(string.IsNullOrWhiteSpace(o.KinToSubject) ? "" : $", who is {o.KinToSubject.Trim()}")}");

         return "The player's other partners (you know them all): " + string.Join("; ", parts) + ".";
      }
   }
}
