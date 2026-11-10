// Code written by Gabriel Mailhot, 29/10/2026.
// Several goods handed over at once, each in the amount the player chose. fearente (Nexus, 29/10/2026): he wanted to
// give grain, fish and meat in one gesture, in different amounts, and had to give the fish and the meat one by one, one
// unit each, with an out-of-character note to say how much. This decides how the amount typed in the bag is read, how
// the bundle is named in the chat and the memory, and how a character who loves one thing in it and not another
// receives the whole. Pure; the bag asks and the bridge moves the goods.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>One kind of good in a bundle: its name and how many.</summary>
   public sealed class GiftLine
   {
      /// <summary>The good's display name.</summary>
      public string Name { get; init; } = "";

      /// <summary>How many of it.</summary>
      public int Count { get; init; }
   }

   /// <summary>One good laid out in the bag for a sale: how many were set out, and how many the player holds now.</summary>
   public sealed class LaidOutGood
   {
      /// <summary>The amount set out in the bag.</summary>
      public int Count { get; init; }

      /// <summary>The amount the player still holds when the sale is struck; 0 when the good is gone.</summary>
      public int Held { get; init; }
   }

   /// <summary>Reads the amount typed for each good, names the bundle, and weighs how welcome it is. Pure.</summary>
   public static class GiftBundlePolicy
   {
      /// <summary>
      ///   The amount the player typed for a good they hold <paramref name="held" /> of: a blank answer means all of it,
      ///   a number from 1 to <paramref name="held" /> means that many, anything else is not an amount (null), so the
      ///   bag can refuse it before anything leaves.
      /// </summary>
      public static int? Amount(string typed, int held)
      {
         if (held <= 0) return null;
         if (string.IsNullOrWhiteSpace(typed)) return held;

         return int.TryParse(typed.Trim(), out int n) && n >= 1 && n <= held ? n : (int?) null;
      }

      /// <summary>
      ///   True when the player sent the handover line the bag wrote, as written: only then do the amounts chosen in the
      ///   bag decide what leaves. A line he edited ("a sack of grain" over "300 Grain") is his word instead, so the bag
      ///   must not hand over what he no longer says.
      /// </summary>
      public static bool BagStillDecides(string sent, string written)
         => !string.IsNullOrWhiteSpace(written) && string.Equals(sent?.Trim(), written.Trim(), System.StringComparison.Ordinal);

      /// <summary>"20 Fish, 10 Meat and Grain": each good with its count when more than one, joined as speech joins them.</summary>
      public static string Describe(IReadOnlyList<GiftLine> lines)
      {
         List<string> parts = (lines ?? new List<GiftLine>())
                              .Where(l => l != null && l.Count > 0 && !string.IsNullOrWhiteSpace(l.Name))
                              .Select(l => l.Count > 1 ? $"{l.Count} {l.Name.Trim()}" : l.Name.Trim())
                              .ToList();

         if (parts.Count <= 1) return parts.FirstOrDefault() ?? "";

         return $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[parts.Count - 1]}";
      }

      /// <summary>
      ///   Whether a lot laid out for one price can still be sold: every good in it, each in the amount set out (froggyluv,
      ///   Nexus, 04/11/2026: food chosen in the bag to sell; Gabriel 09/11/2026: several kinds at once). The price was named
      ///   for the whole lot, so a lot the player no longer holds in full is not sold short for the same price: nothing
      ///   leaves, and the bridge says why.
      /// </summary>
      public static bool WholeLotHeld(IReadOnlyList<LaidOutGood> lot)
         => lot != null && lot.Count > 0 && lot.All(g => g != null && g.Count > 0 && g.Held >= g.Count);

      /// <summary>
      ///   How welcome a bundle is: loved when it holds something they love (the gesture carries it), cold only when
      ///   every good in it leaves them cold, ordinary otherwise.
      /// </summary>
      public static GiftWelcome Welcome(IEnumerable<GiftWelcome> each)
      {
         List<GiftWelcome> all = each?.ToList() ?? new List<GiftWelcome>();
         if (all.Count == 0) return GiftWelcome.Ordinary;
         if (all.Contains(GiftWelcome.Loved)) return GiftWelcome.Loved;

         return all.All(w => w == GiftWelcome.Cold) ? GiftWelcome.Cold : GiftWelcome.Ordinary;
      }
   }
}
