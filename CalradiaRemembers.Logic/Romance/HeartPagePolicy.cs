// Code written by Gabriel Mailhot, 20/10/2026.
// The encyclopedia's Heart section. Gabriel (20/10/2026), asked whether love stories were documented in CR: they were
// kept (courtship, lovers, consorts, marriages, open terms, her own lover) and shown nowhere on the page. His rulings:
// "seulement les npc engages avec le joueur", "oui pour un recap sur la page du joueur", "le ton pourrait etre plus
// personnel", all of it "sous une section COEUR". So: on the page of a character the player is engaged with, the story of
// the bond in the second person ("She has been your secret lover since spring 1091"); on the player's own page, one line
// per such person. Nothing is shown the player does not already know: her own lover only once the player knows of him,
// and no date the game did not record (an old save's bond is told without one).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Romance
{
   /// <summary>A child the player and this character have together, as the page names it.</summary>
   public sealed class HeartChild
   {
      /// <summary>The child's name.</summary>
      public string Name { get; init; }

      /// <summary>The calendar year the child was born.</summary>
      public int BornYear { get; init; }
   }

   /// <summary>What the player knows of one character's bond with them. Days are in-game days; null when never recorded.</summary>
   public sealed class HeartFacts
   {
      /// <summary>The character's name, for the recap on the player's page.</summary>
      public string Name { get; init; }

      /// <summary>She, rather than he: the pronouns and wife rather than husband.</summary>
      public bool IsFemale { get; init; }

      /// <summary>The romantic state of the bond, as the conversations and the game's actions left it.</summary>
      public RomanticStatus Status { get; init; }

      /// <summary>The day the state last changed.</summary>
      public int? StatusSinceDay { get; init; }

      /// <summary>The day they first became lovers.</summary>
      public int? LoversSinceDay { get; init; }

      /// <summary>She is the player's spouse now. Outranks the romantic state, which a divorce does not reset.</summary>
      public bool IsSpouse { get; init; }

      /// <summary>She was the player's spouse and is no longer. Outranks the romantic state.</summary>
      public bool IsExSpouse { get; init; }

      /// <summary>The day she wed the player, when the game recorded it.</summary>
      public int? WeddedOnDay { get; init; }

      /// <summary>She is the player's consort: a committed bond the law does not recognise.</summary>
      public bool IsConsort { get; init; }

      /// <summary>She is the player's secret lover, a bond kept hidden by choice.</summary>
      public bool IsSecretLover { get; init; }

      /// <summary>The day the secret bond began.</summary>
      public int? SecretLoverSinceDay { get; init; }

      /// <summary>She and the player hold each other free to love others.</summary>
      public bool IsOpenArrangement { get; init; }

      /// <summary>She accepts the player's other partners and keeps to the player alone.</summary>
      public bool AcceptsPlayersOtherPartners { get; init; }

      /// <summary>Her own lover's name, ONLY when the player knows of him; null otherwise.</summary>
      public string KnownLoverName { get; init; }

      /// <summary>Her own lover was a hidden affair the player discovered, rather than one held openly.</summary>
      public bool AffairDiscovered { get; init; }

      /// <summary>The children she and the player have together, never a hidden one.</summary>
      public IReadOnlyList<HeartChild> Children { get; init; } = Array.Empty<HeartChild>();
   }

   /// <summary>Tells a character's bond with the player for the Heart section, and the recap line on the player's page.</summary>
   public static class HeartPagePolicy
   {
      /// <summary>
      ///   True when the character is engaged with the player and has a Heart section: a spouse now or before, a consort,
      ///   a lover, a courtship, or a bond that soured or broke (a story that ended badly is still a story). A passing
      ///   curiosity is not.
      /// </summary>
      public static bool IsEngaged(HeartFacts f)
         => f != null && (f.IsSpouse || f.IsExSpouse || f.IsConsort || f.IsSecretLover
                          || f.Status is RomanticStatus.Courting or RomanticStatus.Intimate or RomanticStatus.SecretLover
                                or RomanticStatus.Committed or RomanticStatus.Estranged or RomanticStatus.Broken);

      /// <summary>The Heart section of the character's page, in the player's second person. Empty when not engaged.</summary>
      public static IReadOnlyList<string> Lines(HeartFacts f, Func<int, string> when)
      {
         if (!IsEngaged(f)) return Array.Empty<string>();

         var p = new Pronouns(f.IsFemale);
         var lines = new List<string> {Bond(f, p, when)};

         // When they became lovers, if it is not already what the first line dates.
         if (f.LoversSinceDay is int lovers && Dates(f) != lovers && (f.IsSpouse || f.IsExSpouse || f.IsConsort || f.Status is RomanticStatus.Estranged or RomanticStatus.Broken))
            lines.Add($"You first became lovers in {when(lovers)}.");

         if (f.Children.Count > 0) lines.Add($"Your {(f.Children.Count == 1 ? "child" : "children")} together: {string.Join(", ", f.Children.Select(c => $"{c.Name} ({c.BornYear})"))}.");

         if (f.IsOpenArrangement) lines.Add("You hold each other free to love others.");
         else if (f.AcceptsPlayersOtherPartners) lines.Add($"{p.She} accepts that you have others, and keeps to you alone.");

         if (!string.IsNullOrWhiteSpace(f.KnownLoverName))
            lines.Add(f.AffairDiscovered ? $"{p.She} took {f.KnownLoverName} as a lover behind your back." : $"{p.She} has a lover of {p.His} own, {f.KnownLoverName}, and you know it.");

         return lines;
      }

      /// <summary>One line on the player's page: who, what to the player, since when, and the children.</summary>
      public static string RecapLine(HeartFacts f, Func<int, string> when)
      {
         if (!IsEngaged(f)) return null;

         var p = new Pronouns(f.IsFemale);
         int? day = Dates(f);
         string date = day is int d ? when(d) : null;
         string line = $"{f.Name}, {Role(f, p)}";
         if (date != null)
            line += f.IsExSpouse ? $" (wed in {date})"
                  : !f.IsSpouse && !f.IsConsort && !f.IsSecretLover && f.Status == RomanticStatus.Broken ? $" (it ended in {date})"
                  : $" since {date}";
         if (f.Children.Count > 0) line += $"; your {(f.Children.Count == 1 ? "child" : "children")}: {string.Join(", ", f.Children.Select(c => c.Name))}";

         return line;
      }

      // The day the first line tells, so the lovers' day is not said twice.
      private static int? Dates(HeartFacts f)
      {
         if (f.IsSpouse || f.IsExSpouse) return f.WeddedOnDay;
         if (f.IsConsort) return f.StatusSinceDay;
         if (f.IsSecretLover) return f.SecretLoverSinceDay ?? f.StatusSinceDay;
         if (f.Status is RomanticStatus.Intimate or RomanticStatus.SecretLover) return f.LoversSinceDay ?? f.StatusSinceDay;

         return f.StatusSinceDay;
      }

      private static string Bond(HeartFacts f, Pronouns p, Func<int, string> when)
      {
         int? day = Dates(f);
         string At(string lead) => day is int d ? $"{lead} {when(d)}" : "";

         if (f.IsSpouse) return $"{p.She} is your {p.Wife}{At(", wed in")}.";
         if (f.IsExSpouse) return $"{p.She} was your {p.Wife}{At(", wed in")}. The marriage was dissolved.";
         if (f.IsConsort) return $"{p.She} is your consort{At(" since")}.";
         if (f.IsSecretLover) return $"{p.She} has been your secret lover{At(" since")}.";

         return f.Status switch
         {
            RomanticStatus.SecretLover => $"{p.She} has been your lover in secret{At(" since")}.",
            RomanticStatus.Intimate => $"{p.She} has been your lover{At(" since")}.",
            RomanticStatus.Committed => $"{p.She} is bound to you{At(" since")}.",
            RomanticStatus.Courting => $"You and {p.Her} have been courting{At(" since")}.",
            RomanticStatus.Estranged => $"{p.She} has grown distant from you{At(" since")}.",
            _ => $"What was between you is over{At(". It ended in")}."
         };
      }

      private static string Role(HeartFacts f, Pronouns p)
      {
         if (f.IsSpouse) return $"your {p.Wife}";
         if (f.IsExSpouse) return $"once your {p.Wife}";
         if (f.IsConsort) return "your consort";
         if (f.IsSecretLover) return "your secret lover";

         return f.Status switch
         {
            RomanticStatus.SecretLover => "your lover in secret",
            RomanticStatus.Intimate => "your lover",
            RomanticStatus.Committed => "bound to you",
            RomanticStatus.Courting => "courting you",
            RomanticStatus.Estranged => "grown distant from you",
            _ => "your former love"
         };
      }

      private readonly struct Pronouns
      {
         public Pronouns(bool female)
         {
            She = female ? "She" : "He";
            Her = female ? "her" : "him";
            His = female ? "her" : "his";
            Wife = female ? "wife" : "husband";
         }

         public string She { get; }
         public string Her { get; }
         public string His { get; }
         public string Wife { get; }
      }
   }
}
