// Code written by Gabriel Mailhot, 20/10/2026.
// The encyclopedia's Trauma section. Gabriel (20/10/2026): "si Hardcore, je verrais une autre section trauma ou les
// aggressions vecus sont documentees", then "uniquement ceux des PNJ, pas ceux du joueur car on ne decide pas a la place
// du joueur ce qui est traumatisant pour lui." So: on a character's page only, never the player's, and only at Hardcore,
// the aggressions that character lived through and the player knows of, because the player was there: tormented as the
// player's prisoner, a companion broken before the player's eyes, beaten by the player in a conversation turned violent.
// Sober, never graphic. The ledger is the only source: a grudge can be refused, decay or resolve, and the companion's
// grudge "let me be broken" targets a player who did not do it, so reading grudges would hide trauma or misname its author.

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Trauma
{
   /// <summary>What was done. Stored by NAME in the save, so the order may change.</summary>
   public enum TraumaKind
   {
      /// <summary>Tortured.</summary>
      Tortured,

      /// <summary>Taken by force in a captive scene (domination, training, a captor's desire).</summary>
      Assaulted,

      /// <summary>Interrogated as a captive.</summary>
      Interrogated,

      /// <summary>Made to suffer in a way no closer word fits.</summary>
      Tormented,

      /// <summary>Beaten down in a conversation turned violent.</summary>
      Beaten,

      /// <summary>Robbed by force in a conversation turned violent (Gabriel, 20/10/2026: "oui, ajoute le vol").</summary>
      Robbed
   }

   /// <summary>Who did it, as the player saw it. Stored by NAME in the save.</summary>
   public enum TraumaAuthor
   {
      /// <summary>The player, of their own will.</summary>
      Player,

      /// <summary>The player's own hands, forced on them by the captor who held them both.</summary>
      PlayerCoerced,

      /// <summary>The captor in person, before the player's eyes.</summary>
      Captor,

      /// <summary>The captor's men, before the player's eyes.</summary>
      CaptorsMen
   }

   /// <summary>One aggression a character lived through, as the ledger keeps it.</summary>
   public sealed class TraumaRecord
   {
      /// <summary>The character who suffered it.</summary>
      public string VictimId { get; init; }

      /// <summary>The captor's hero id when the captor is a named hero; null for the player or a faceless captor.</summary>
      public string CaptorId { get; init; }

      /// <summary>What was done.</summary>
      public TraumaKind Kind { get; init; }

      /// <summary>Who did it.</summary>
      public TraumaAuthor Author { get; init; }

      /// <summary>The in-game day.</summary>
      public int Day { get; init; }
   }

   /// <summary>The Trauma section's words, its gate, and the ledger's save form.</summary>
   public static class TraumaPagePolicy
   {
      /// <summary>
      ///   Shown only at Hardcore, and never on the player's own page. The ledger is written whatever the level, so a
      ///   player who turns Hardcore on later sees what already happened.
      /// </summary>
      public static bool Shows(AdultContentLevel level, bool isThePlayersPage) => level == AdultContentLevel.Hardcore && !isThePlayersPage;

      /// <summary>
      ///   What a captive scene did, from its intent. Torture is torture; the scenes of force (domination, training, a
      ///   captor's desire) are an assault; an interrogation is one. Anything else is "tormented".
      /// </summary>
      public static TraumaKind KindOf(CaptiveSceneIntent intent) => intent switch
      {
         CaptiveSceneIntent.Torture => TraumaKind.Tortured,
         CaptiveSceneIntent.Domination or CaptiveSceneIntent.Training or CaptiveSceneIntent.PersonalDesire => TraumaKind.Assaulted,
         CaptiveSceneIntent.Interrogation => TraumaKind.Interrogated,
         _ => TraumaKind.Tormented
      };

      /// <summary>
      ///   One line, in the player's second person. <paramref name="captorName" /> is the named captor, or null for a
      ///   faceless one ("her captors"); the captor's sex is never guessed ("held by X").
      /// </summary>
      public static string Line(TraumaRecord r, bool victimIsFemale, string captorName, Func<int, string> when)
      {
         string she = victimIsFemale ? "She" : "He";
         string her = victimIsFemale ? "her" : "his";
         string verb = Verb(r.Kind);
         string by = string.IsNullOrWhiteSpace(captorName) ? $"{her} captors" : captorName;
         string date = $", in {when(r.Day)}";

         return r.Author switch
         {
            TraumaAuthor.Player when r.Kind is TraumaKind.Beaten or TraumaKind.Robbed => $"{she} was {verb} by you{date}.",
            TraumaAuthor.Player => $"{she} was {verb} by you while your prisoner{date}.",
            TraumaAuthor.PlayerCoerced => $"{she} was {verb} by your own hands, forced on you by {by}, while you were both held captive{date}.",
            TraumaAuthor.Captor => $"{she} was {verb} by {by} while held captive, before your eyes{date}.",
            _ => $"{she} was {verb} by {(string.IsNullOrWhiteSpace(captorName) ? $"{her} captors' men" : $"the men of {captorName}")} while held captive, before your eyes{date}."
         };
      }

      /// <summary>The lines of one character's page, oldest first, each aggression once.</summary>
      public static IReadOnlyList<TraumaRecord> Of(IEnumerable<TraumaRecord> ledger, string victimId)
         => (ledger ?? Enumerable.Empty<TraumaRecord>()).Where(r => r != null && r.VictimId == victimId).OrderBy(r => r.Day).ToList();

      /// <summary>
      ///   True when the ledger already holds this aggression (same victim, day, kind and author): a scene closed twice
      ///   is written once.
      /// </summary>
      public static bool Holds(IEnumerable<TraumaRecord> ledger, TraumaRecord r)
         => (ledger ?? Enumerable.Empty<TraumaRecord>()).Any(x => x.VictimId == r.VictimId && x.Day == r.Day && x.Kind == r.Kind && x.Author == r.Author);

      /// <summary>The ledger as one save string: a line per record, "victim|captor|kind|author|day", kind and author by name.</summary>
      public static string Write(IEnumerable<TraumaRecord> ledger)
         => string.Join("\n", (ledger ?? Enumerable.Empty<TraumaRecord>()).Select(r => string.Join("|", r.VictimId, r.CaptorId ?? "", r.Kind, r.Author,
                                                                                                        r.Day.ToString(CultureInfo.InvariantCulture))));

      /// <summary>The ledger read back; a line it cannot read (a kind from a later version) is skipped, never thrown.</summary>
      public static List<TraumaRecord> Read(string saved)
      {
         var ledger = new List<TraumaRecord>();
         foreach (string line in (saved ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
         {
            string[] f = line.Split('|');
            if (f.Length != 5 || string.IsNullOrEmpty(f[0])) continue;
            if (!Enum.TryParse(f[2], out TraumaKind kind) || !Enum.TryParse(f[3], out TraumaAuthor author)) continue;
            if (!int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)) continue;
            ledger.Add(new TraumaRecord {VictimId = f[0], CaptorId = f[1].Length == 0 ? null : f[1], Kind = kind, Author = author, Day = day});
         }

         return ledger;
      }

      private static string Verb(TraumaKind kind) => kind switch
      {
         TraumaKind.Tortured => "tortured",
         TraumaKind.Assaulted => "assaulted",
         TraumaKind.Interrogated => "harshly interrogated",
         TraumaKind.Beaten => "beaten down",
         TraumaKind.Robbed => "robbed at swordpoint",
         _ => "tormented"
      };
   }
}
