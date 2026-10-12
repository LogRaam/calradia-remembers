// Code written by Gabriel Mailhot, 12/10/2026.
// The tale-scenario bridge (docs/design/tale-scenario-bridge; Gabriel, 12/10/2026). A rule answering a tale (ON_TALE)
// names the tale's people by role, {husband} or {giver}, since they are chosen when the tale begins; its message names
// them too, filled in before the realm hears it. And "rancune et pousse son roi vers la guerre": a husband who does not
// rule presses his king, and the king decides as a leader does (Gabriel, 02/07/2026: war is the leader's decision,
// taken when it serves him), on his regard for the husband, the wars he already fights and the strength he would face.

#region

using System.Collections.Generic;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>A reference written as a tale role, "{husband}". Pure.</summary>
   public static class ScenarioRoleRef
   {
      private static readonly Regex Role = new(@"^\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}$", RegexOptions.Compiled);

      /// <summary>True when <paramref name="reference" /> is a role in braces; <paramref name="role" /> is its name, lower case.</summary>
      public static bool TryRole(string reference, out string role)
      {
         Match m = Role.Match(reference?.Trim() ?? "");
         role = m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;

         return m.Success;
      }
   }

   /// <summary>Fills a rule's message with the names of the tale's people. Pure.</summary>
   public static class ScenarioRoleText
   {
      private static readonly Regex Placeholder = new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

      /// <summary>Each {role} with a name becomes that name; a role nobody holds stays as written.</summary>
      public static string Fill(string text, IReadOnlyDictionary<string, string> names)
      {
         if (string.IsNullOrEmpty(text) || names == null || names.Count == 0) return text;

         return Placeholder.Replace(text, m => names.TryGetValue(m.Groups[1].Value.ToLowerInvariant(), out string name) && !string.IsNullOrEmpty(name) ? name : m.Value);
      }
   }

   /// <summary>What a king weighs when a lord of his realm presses him toward war.</summary>
   public sealed class WarPressFacts
   {
      /// <summary>The one pressing rules the realm himself: the decision is already his.</summary>
      public bool PresserRules { get; init; }

      /// <summary>The king's relation with the one pressing him (the game's own, -100 to 100).</summary>
      public int KingRegardForPresser { get; init; }

      /// <summary>Wars the king's realm already fights.</summary>
      public int KingOpenWars { get; init; }

      /// <summary>The king's realm's strength over the target's (1.0: even).</summary>
      public double StrengthRatio { get; init; }
   }

   /// <summary>Whether a king goes to war when a lord presses him (PRESS_FOR_WAR). Pure.</summary>
   public static class WarPressPolicy
   {
      /// <summary>TUNE: the least relation with the one pressing that makes the king heed him: he counts him a friend.</summary>
      public const int RegardToHeed = NpcMemoryService.Core.Standing.VanillaRelation.FriendTier;

      /// <summary>TUNE: the most wars the realm may already fight for the king to open another.</summary>
      public const int MostOpenWars = 1;

      /// <summary>TUNE: the least strength over the target's the king accepts to face (0.8: a little weaker will do).</summary>
      public const double LeastStrengthRatio = 0.8;

      /// <summary>True when war is declared: the presser rules, or the king heeds him and can afford the war.</summary>
      public static bool KingDeclares(WarPressFacts f)
      {
         if (f == null) return false;
         if (f.PresserRules) return true;

         return f.KingRegardForPresser >= RegardToHeed && f.KingOpenWars <= MostOpenWars && f.StrengthRatio >= LeastStrengthRatio;
      }
   }
}
