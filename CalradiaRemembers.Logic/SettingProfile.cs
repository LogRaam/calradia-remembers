// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>The editable per-setting overrides, as parsed from ModuleData/setting.json (any field may be absent).</summary>
   public sealed class SettingProfile
   {
      /// <summary>Culture / faction name fragments whose men are skeptical of female authority.</summary>
      public List<string> PatriarchalCultures { get; set; }

      /// <summary>The world's adjective ("Calradian" → "of the Old World").</summary>
      public string WorldAdjective { get; set; }

      /// <summary>The world's name in prose ("Calradia" → "the Old World").</summary>
      public string WorldName { get; set; }
   }

   /// <summary>
   ///   The Calradia defaults and the rule that resolves an override against them: each field is taken from
   ///   the override when present and non-blank, else the default. A null or empty override yields the full
   ///   Calradia defaults — so an unmodified install behaves exactly as before.
   /// </summary>
   public static class SettingProfileDefaults
   {
      public const string WorldAdjective = "Calradian";
      public const string WorldName = "Calradia";

      public static readonly IReadOnlyList<string> PatriarchalCultures =
         new[] {"Vlandia", "Northern Empire", "Western Empire", "Aserai"};

      /// <summary>The effective setting values: the override where given, the Calradia default otherwise.</summary>
      public static (string WorldName, string WorldAdjective, IReadOnlyList<string> PatriarchalCultures) Resolve(SettingProfile profile)
      {
         string name = !string.IsNullOrWhiteSpace(profile?.WorldName)
            ? profile.WorldName.Trim()
            : WorldName;
         string adj = !string.IsNullOrWhiteSpace(profile?.WorldAdjective)
            ? profile.WorldAdjective.Trim()
            : WorldAdjective;

         List<string> cultures = profile?.PatriarchalCultures?
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .Select(s => s.Trim())
                                        .ToList();

         return (name, adj, cultures is {Count: > 0}
            ? cultures
            : PatriarchalCultures);
      }
   }
}