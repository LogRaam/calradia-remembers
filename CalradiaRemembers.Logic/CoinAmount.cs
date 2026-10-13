// Code written by Gabriel Mailhot, 13/10/2026.
// A coin amount read tolerantly from what a model writes. Moved here from ActionConfirmationController (unchanged) so the
// bridge's sell_item reads a price exactly as the confirmation reads it: "9,000" and "9 000 denars" are both 9000.

#region

using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Reads a coin amount tolerantly. Pure.</summary>
   public static class CoinAmount
   {
      /// <summary>
      ///   A model (a small local one especially) may write "10,000", "10 000", or "10000 denars"; a bare int.TryParse
      ///   rejects the first two outright and reads a partial, wrong value from the third (player report: giving 10,000
      ///   registered as a far smaller sum). Keep only the digit run, so "10,000 denars" and "10 000" both parse to
      ///   10000. A leading minus is dropped: a transfer amount is always non-negative here. 0 when there is none.
      /// </summary>
      public static int Parse(string raw)
      {
         if (string.IsNullOrWhiteSpace(raw)) return 0;

         var digits = new StringBuilder(raw.Length);
         foreach (char c in raw)
            if (char.IsDigit(c)) digits.Append(c);

         return digits.Length > 0 && int.TryParse(digits.ToString(), out int parsed) ? parsed : 0;
      }
   }
}
