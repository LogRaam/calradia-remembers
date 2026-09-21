// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

using NpcMemoryService.Core.Calendar;
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

      /// <summary>
      ///   In-game days in one season, when this world's calendar is not Calradia's. Absent or below 1 keeps 21.
      ///   Added 21/09/2026: a conversion could already rename the world, while its YEAR stayed 84 days long, so
      ///   every "a season ago" the mod wrote was wrong for it.
      /// </summary>
      public int? DaysPerSeason { get; set; }

      /// <summary>Seasons in one year, when this world does not have four. Absent or below 1 keeps 4.</summary>
      public int? SeasonsPerYear { get; set; }
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

      /// <summary>Calradia's own calendar, and what an absent or nonsense override falls back to.</summary>
      public const int DaysPerSeason = WorldCalendar.DefaultDaysPerSeason;

      /// <summary>Four, as in Calradia.</summary>
      public const int SeasonsPerYear = WorldCalendar.DefaultSeasonsPerYear;

      /// <summary>The effective calendar: each field taken from the override when it is present and sane, else Calradia's.</summary>
      public static (int DaysPerSeason, int SeasonsPerYear) ResolveCalendar(SettingProfile profile)
         => (profile?.DaysPerSeason is > 0 ? profile.DaysPerSeason.Value : DaysPerSeason,
             profile?.SeasonsPerYear is > 0 ? profile.SeasonsPerYear.Value : SeasonsPerYear);

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