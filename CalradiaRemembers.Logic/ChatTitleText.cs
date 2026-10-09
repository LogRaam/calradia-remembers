// Code written by Gabriel Mailhot, 08/10/2026.
// The chat window's title (Gabriel, 08/10/2026: drop "Calradia Remembers -" from it). Whom or where, alone; the mod's
// name only when there is nothing else to name.

namespace CalradiaRemembers.Logic
{
   /// <summary>The chat window's title. Pure.</summary>
   public static class ChatTitleText
   {
      /// <summary>The title when there is nothing to name.</summary>
      public const string Fallback = "Calradia Remembers";

      public static string For(string name) => string.IsNullOrWhiteSpace(name) ? Fallback : name.Trim();
   }
}
