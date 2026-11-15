// Code written by Gabriel Mailhot, 23/09/2026.
// The modder API's blocking guard: a public member that has SHIPPED may not vanish from the surface.
//
// modder-api.md only INFORMS: regenerating it accepts whatever the code now says, so a removed member shows up as one
// line in a diff, the exact "regenerate and commit" reflex the ratchets were built against. Found on 23/09/2026 when
// ExternalWorldEvent.PlayerIsActor left the map without a sound. That was harmless, it had never been released; the same
// silence for a member tashmetu's bridge calls would be his mod failing on a player's machine.
//
// So the baseline is not the committed map (which carries unreleased additions that may still be withdrawn) but a
// snapshot of the surface AS RELEASED, recorded on purpose at each cut. Only what is in it is protected.

#region

using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   internal static class ModderApiGuard
   {
      /// <summary>The snapshot of the surface as last released. Rewritten only by <see cref="RecordVariable" />.</summary>
      public const string ReleasedPath = "docs/maps/modder-api-released.md";

      /// <summary>Set to 1 at a release cut to record the surface being shipped as the new baseline.</summary>
      public const string RecordVariable = "CR_RECORD_RELEASED_API";

      /// <summary>Set to 1 to let a released member go on purpose. Say why in the commit and in "For modders".</summary>
      public const string AllowRemovalVariable = "CR_ALLOW_API_REMOVAL";

      private static readonly Regex MemberLine = new(@"^- `(?<member>.+)`$");

      // A constant's VALUE is not part of what a caller binds to: a compiled bridge keeps the value it was built with and
      // finds the member still there. CrApi.Version is designed to rise by one at each release that adds a door, and the
      // guard called 1 -> 2 a vanished member (15/11/2026). The name and type of a constant stay protected.
      private static readonly Regex ConstValue = new(@"^(?<decl>(static )?const \S+ \S+) = .+$");
      private static readonly Regex VersionValue = new(@"<Version value=""(?<version>[^""]+)""");

      /// <summary>
      ///   Every member listed in a modder-api map, as "Type: member" so two types with a member of the same shape stay
      ///   apart. A changed signature is therefore a different line: the old one vanishes, which is exactly what breaks
      ///   a caller.
      /// </summary>
      public static HashSet<string> MembersListedIn(string mapText)
      {
         var members = new HashSet<string>(StringComparer.Ordinal);
         string type = "";

         foreach (string raw in mapText.Replace("\r\n", "\n").Split('\n'))
         {
            string line = raw.TrimEnd();

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
               type = line.Substring(4).Trim();

               continue;
            }

            Match m = MemberLine.Match(line);
            if (!m.Success || type.Length == 0) continue;

            string member = m.Groups["member"].Value;
            Match constant = ConstValue.Match(member);
            members.Add($"{type}: {(constant.Success ? constant.Groups["decl"].Value : member)}");
         }

         return members;
      }

      /// <summary>The released members the current surface no longer has, in a stable order.</summary>
      public static List<string> Vanished(string releasedText, string currentText)
      {
         HashSet<string> now = MembersListedIn(currentText);

         return MembersListedIn(releasedText).Where(m => !now.Contains(m)).OrderBy(m => m, StringComparer.Ordinal).ToList();
      }

      /// <summary>The version a released snapshot was recorded for, or "an earlier release" when it does not say.</summary>
      public static string ReleasedVersionOf(string releasedText)
      {
         Match m = Regex.Match(releasedText, @"^Released in (?<version>\S+)", RegexOptions.Multiline);

         return m.Success ? m.Groups["version"].Value : "an earlier release";
      }

      /// <summary>The snapshot file: the version line, then the map exactly as generated for that release.</summary>
      public static string Snapshot(string generatedMap, string version)
         => $"Released in {version}\n\n"
          + "The modder API as it SHIPPED, the baseline for the vanished-member guard (see docs/maps/README.md). Recorded on\n"
          + $"purpose at each release cut with Tools/Update-Maps.ps1 -RecordReleasedApi; never by a plain regeneration.\n\n"
          + generatedMap;

      /// <summary>The version SubModule.xml declares, i.e. the one being cut.</summary>
      public static string VersionIn(string subModuleXml)
      {
         Match m = VersionValue.Match(subModuleXml);

         return m.Success ? m.Groups["version"].Value : "an unknown version";
      }
   }
}
