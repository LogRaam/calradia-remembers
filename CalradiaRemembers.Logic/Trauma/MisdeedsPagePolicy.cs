// Code written by Gabriel Mailhot, 23/10/2026.
// The encyclopedia's Misdeeds section, on the player's own page. Gabriel (23/10/2026), after the prisoners the player took
// by force were first told under Heart: "Heart doit etre une section sur l'amour et le desir. L'evenement 'take by force'
// devrait etre une autre section avec des actions reprehensibles." His earlier ruling still holds: "les actes que le joueur
// a fait subir peuvent apparaitre", "les scenes forcees subies par le joueur ne sont pas affichees sur la page du joueur".
// Then: "oui [beatings and robbery below Hardcore too], et on pourrait aussi ajouter des trahisons, extortions, et tout
// autres gestes qui pourraient etres classes comme misdeeds"; "divorce est un choix de vie. adultere est un misdeed."
// So the section tells what the player did of their own will, each kind from ONE source so a deed is never told twice:
// the Trauma ledger for the captive scenes, beatings and robberies (the same records each victim's Trauma section reads),
// the game's own death log for murders and executions, the quests the player let lapse or gave up, and the misdeeds ledger
// for adultery, a captive handed to his enemies, a realm forsaken, and villages raided and places sacked. Never what a captor did, nor what a captor forced
// the player's hands to do. The captive scenes stay Hardcore only, as the Trauma section; the rest shows at every level.
// Sober: who, what, how many times, the first and the last.

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CalradiaRemembers.Logic.Quests;
using CalradiaRemembers.Logic.Romance;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Trauma
{
   /// <summary>What the player did. Stored by NAME in the misdeeds ledger, so the order may change.</summary>
   public enum MisdeedKind
   {
      /// <summary>Took a prisoner by force (a captive scene).</summary>
      TookByForce,

      /// <summary>Tortured a prisoner.</summary>
      Tortured,

      /// <summary>Interrogated a prisoner under duress.</summary>
      Interrogated,

      /// <summary>Tormented a prisoner in a way no closer word fits.</summary>
      Tormented,

      /// <summary>Beat someone down in a conversation turned violent.</summary>
      Beat,

      /// <summary>Robbed someone by force.</summary>
      Robbed,

      /// <summary>Killed someone outside battle (the game's "murdered").</summary>
      Murdered,

      /// <summary>Had someone put to death.</summary>
      Executed,

      /// <summary>Gave up or let lapse a task given in trust (not a bandit lair that could not be found).</summary>
      BrokeWord,

      /// <summary>Handed a prisoner to his own enemies.</summary>
      HandedToEnemy,

      /// <summary>Was unfaithful to their own spouse or partner; the victim is that spouse.</summary>
      Unfaithful,

      /// <summary>Lay with someone else's spouse; the victim is the wronged spouse.</summary>
      LayWithSpouseOf,

      /// <summary>Took someone's spouse from them; the victim is the one left.</summary>
      StoleSpouse,

      /// <summary>Forsook the oath sworn to a realm; the victim is the realm.</summary>
      ForsookRealm,

      /// <summary>Raided a village in a war already under way; the victim is the village.</summary>
      RaidedInWar,

      /// <summary>Fell on a village with no war before it, the raid being the war's first act; the victim is the village.</summary>
      RaidedAtPeace,

      /// <summary>Devastated a place after taking it; the victim is the place.</summary>
      Devastated,

      /// <summary>Gave a place to the troops to pillage after taking it; the victim is the place.</summary>
      Pillaged
   }

   /// <summary>One deed of the player, as a source gives it.</summary>
   public sealed class MisdeedRecord
   {
      /// <summary>The hero the deed was done to, or the kingdom for a realm forsaken, or the place for a raid or a sack.</summary>
      public string VictimId { get; init; }

      /// <summary>What the player did.</summary>
      public MisdeedKind Kind { get; init; }

      /// <summary>The in-game day.</summary>
      public int Day { get; init; }
   }

   /// <summary>One kind of deed the player did to one victim, every time it happened.</summary>
   public sealed class MisdeedFacts
   {
      /// <summary>The victim's id.</summary>
      public string VictimId { get; init; }

      /// <summary>What the player did.</summary>
      public MisdeedKind Kind { get; init; }

      /// <summary>The in-game days, oldest first, each once.</summary>
      public IReadOnlyList<int> Days { get; init; } = Array.Empty<int>();
   }

   /// <summary>The Misdeeds section's gate, its gathering, its words and its ledger's save form.</summary>
   public static class MisdeedsPagePolicy
   {
      private static readonly string[] Numbers = {"", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"};

      /// <summary>The days a war must have run for a raid to be an act of that war: a hostile act opens the war the same day.</summary>
      public const int DaysOfAWarUnderWay = 1;

      /// <summary>Only on the player's own page (a character's page has its Trauma section).</summary>
      public static bool Shows(bool isThePlayersPage) => isThePlayersPage;

      /// <summary>The captive scenes are Hardcore only, as the Trauma section; every other deed shows at any level.</summary>
      public static bool NeedsHardcore(MisdeedKind kind)
         => kind is MisdeedKind.TookByForce or MisdeedKind.Tortured or MisdeedKind.Interrogated or MisdeedKind.Tormented;

      /// <summary>
      ///   A Trauma record as the player's deed; null when the player did not do it of their own will (a captor, a
      ///   captor's men, or the player's hands forced by a captor).
      /// </summary>
      public static MisdeedRecord FromTrauma(TraumaRecord r)
      {
         if (r == null || r.Author != TraumaAuthor.Player) return null;
         MisdeedKind kind = r.Kind switch
         {
            TraumaKind.Assaulted => MisdeedKind.TookByForce,
            TraumaKind.Tortured => MisdeedKind.Tortured,
            TraumaKind.Interrogated => MisdeedKind.Interrogated,
            TraumaKind.Beaten => MisdeedKind.Beat,
            TraumaKind.Robbed => MisdeedKind.Robbed,
            _ => MisdeedKind.Tormented
         };

         return new MisdeedRecord {VictimId = r.VictimId, Kind = kind, Day = r.Day};
      }

      /// <summary>
      ///   A task the player gave up or let lapse as a broken word to its giver, dated the day it was given (an abandoned
      ///   task keeps no day of its own); null for a task done, set aside without fault, or a bandit lair that could not
      ///   be found, which the game already forgives (QuestFailureLeniencyPolicy).
      /// </summary>
      public static MisdeedRecord FromQuest(string giverId, InformalQuest quest)
      {
         if (quest == null || string.IsNullOrEmpty(giverId)) return null;
         if (quest.Status != QuestStatus.Abandoned && quest.Status != QuestStatus.Expired) return null;
         if (!QuestFailureLeniencyPolicy.Resolve(quest.Type, 1).RecordBrokenWordMemory) return null;

         return new MisdeedRecord {VictimId = giverId, Kind = MisdeedKind.BrokeWord, Day = quest.IssuedOnDay};
      }

      /// <summary>
      ///   The adultery a jealous party found out, or null when it is none (Gabriel, 23/10/2026: "adultere est un
      ///   misdeed"): the player's own partner wronged by a night or a marriage elsewhere, unless their people share partners
      ///   or they accepted the arrangement; the spouse of the one the player lay with (or married). A flirt is doubt, not
      ///   adultery; a rival suitor, a spurned admirer or a secret lover was no one's spouse.
      /// </summary>
      public static MisdeedKind? AdulteryOf(JealousStake stake, RomanticActTier tier, bool sharedByCustomOrAccepted)
      {
         if (tier < RomanticActTier.Intimacy) return null;
         if (stake == JealousStake.PlayerPartner) return sharedByCustomOrAccepted ? null : MisdeedKind.Unfaithful;
         if (stake == JealousStake.SpouseOfCourted) return tier == RomanticActTier.Marriage ? MisdeedKind.StoleSpouse : MisdeedKind.LayWithSpouseOf;

         return null;
      }

      /// <summary>
      ///   A raid as the page tells it (Gabriel, 01/11/2026, "go" on Claude's proposal): a misdeed either way, the softer line
      ///   for a village of a war under way. <paramref name="daysAtWar" /> is how long the war had run, null with no war.
      /// </summary>
      public static MisdeedKind RaidKind(int? daysAtWar)
         => daysAtWar >= DaysOfAWarUnderWay ? MisdeedKind.RaidedInWar : MisdeedKind.RaidedAtPeace;

      /// <summary>
      ///   The deeds shown at <paramref name="level" />, one entry per victim and kind, the oldest first; a deed two sources
      ///   both hold (same victim, kind and day) is told once.
      /// </summary>
      public static IReadOnlyList<MisdeedFacts> Of(IEnumerable<MisdeedRecord> records, AdultContentLevel level)
         => (records ?? Enumerable.Empty<MisdeedRecord>())
            .Where(r => r != null && !string.IsNullOrEmpty(r.VictimId) && (level == AdultContentLevel.Hardcore || !NeedsHardcore(r.Kind)))
            .GroupBy(r => (r.VictimId, r.Kind))
            .Select(g => new MisdeedFacts {VictimId = g.Key.VictimId, Kind = g.Key.Kind, Days = g.Select(r => r.Day).Distinct().OrderBy(d => d).ToList()})
            .OrderBy(m => m.Days[0])
            .ToList();

      /// <summary>
      ///   One line, in the player's second person: "You took Phenoria by force while she was your prisoner, twice, the first
      ///   in spring 1091, the last in summer 1092." <paramref name="victimIsFemale" /> is ignored for a realm.
      /// </summary>
      public static string Line(MisdeedFacts deed, string victimName, bool victimIsFemale, Func<int, string> when)
      {
         string he = victimIsFemale ? "she" : "he";
         string his = victimIsFemale ? "her" : "his";
         string prisoner = $"while {he} was your prisoner";
         string lead = deed?.Kind switch
         {
            MisdeedKind.TookByForce => $"You took {victimName} by force {prisoner}",
            MisdeedKind.Tortured => $"You tortured {victimName} {prisoner}",
            MisdeedKind.Interrogated => $"You interrogated {victimName} under duress {prisoner}",
            MisdeedKind.Beat => $"You beat {victimName}",
            MisdeedKind.Robbed => $"You robbed {victimName} by force",
            MisdeedKind.Murdered => $"You killed {victimName} outside of battle",
            MisdeedKind.Executed => $"You had {victimName} put to death",
            MisdeedKind.BrokeWord => $"You broke your word to {victimName} on a task {he} gave you",
            MisdeedKind.HandedToEnemy => $"You handed {victimName} over to {his} enemies {prisoner}",
            MisdeedKind.Unfaithful => $"You were unfaithful to {victimName}",
            MisdeedKind.LayWithSpouseOf => $"You lay with {victimName}'s {(victimIsFemale ? "husband" : "wife")}",
            MisdeedKind.StoleSpouse => $"You took {victimName}'s {(victimIsFemale ? "husband" : "wife")} from {(victimIsFemale ? "her" : "him")}",
            MisdeedKind.ForsookRealm => $"You forsook your oath to {victimName}",
            MisdeedKind.RaidedInWar => $"You raided {victimName} in a war against its realm",
            MisdeedKind.RaidedAtPeace => $"You fell on {victimName} when it was at peace with you",
            MisdeedKind.Devastated => $"You devastated {victimName} after taking it",
            MisdeedKind.Pillaged => $"You gave {victimName} to your men to pillage after taking it",
            _ => $"You tormented {victimName} {prisoner}"
         };
         IReadOnlyList<int> days = deed?.Days ?? Array.Empty<int>();
         if (days.Count == 0) return $"{lead}.";
         if (days.Count == 1) return $"{lead}, in {when(days[0])}.";

         return $"{lead}, {(days.Count == 2 ? "twice" : $"{Count(days.Count)} times")}, {Span(days, when)}.";
      }

      /// <summary>True when the ledger already holds this deed (same victim, kind and day): a deed noticed twice is kept once.</summary>
      public static bool Holds(IEnumerable<MisdeedRecord> ledger, MisdeedRecord r)
         => (ledger ?? Enumerable.Empty<MisdeedRecord>()).Any(x => x.VictimId == r.VictimId && x.Kind == r.Kind && x.Day == r.Day);

      /// <summary>The ledger as one save string: a line per record, "victim|kind|day", the kind by name.</summary>
      public static string Write(IEnumerable<MisdeedRecord> ledger)
         => string.Join("\n", (ledger ?? Enumerable.Empty<MisdeedRecord>()).Select(r => string.Join("|", r.VictimId, r.Kind, r.Day.ToString(CultureInfo.InvariantCulture))));

      /// <summary>The ledger read back; a line it cannot read (a kind from a later version) is skipped, never thrown.</summary>
      public static List<MisdeedRecord> Read(string saved)
      {
         var ledger = new List<MisdeedRecord>();
         foreach (string line in (saved ?? "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
         {
            string[] f = line.Split('|');
            if (f.Length != 3 || string.IsNullOrEmpty(f[0])) continue;
            if (!Enum.TryParse(f[1], out MisdeedKind kind)) continue;
            if (!int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)) continue;
            ledger.Add(new MisdeedRecord {VictimId = f[0], Kind = kind, Day = day});
         }

         return ledger;
      }

      // "the first in spring 1091, the last in summer 1092", or "in spring 1091" when both fall in one season.
      private static string Span(IReadOnlyList<int> days, Func<int, string> when)
      {
         string first = when(days[0]);
         string last = when(days[days.Count - 1]);

         return first == last ? $"in {first}" : $"the first in {first}, the last in {last}";
      }

      private static string Count(int n) => n < Numbers.Length ? Numbers[n] : n.ToString();
   }
}
