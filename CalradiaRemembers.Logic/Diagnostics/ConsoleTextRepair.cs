// Code written by Gabriel Mailhot, 03/10/2026.
// Shinelegacy (Nexus, 03/10/2026, on 2.6.10): accents typed into cr.memory_add / cr.memory_set came out as mojibake
// ("é" -> "Ã©", "œ" -> "Å“"). The game's console hands a command its arguments as UTF-8 bytes read back as
// Windows-1252, before any CR code runs. This undoes exactly that, and only when the result is valid UTF-8: text that
// arrived intact cannot be read as a UTF-8 byte sequence (a lone "é" is an invalid one), so it is left alone.

#region

using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>Undoes the console's UTF-8-read-as-Windows-1252 mangling of a command's text. Pure.</summary>
   public static class ConsoleTextRepair
   {
      // Windows-1252 0x80-0x9F. Written out: .NET Core ships no code page 1252 without a provider.
      private const string Cp1252High = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008DŽ\u008F\u0090‘’“”•–\u2014˜™š›œ\u009DžŸ";

      private static readonly IReadOnlyDictionary<char, byte> HighToByte = BuildHighToByte();
      private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

      /// <summary>The text as typed, when it arrived mangled; otherwise the text unchanged.</summary>
      public static string Repair(string text)
      {
         if (string.IsNullOrEmpty(text)) return text;

         var bytes = new byte[text.Length];
         var anyHigh = false;
         for (var i = 0; i < text.Length; i++)
         {
            char c = text[i];
            if (c < 0x80) bytes[i] = (byte) c;
            else if (HighToByte.TryGetValue(c, out byte b)) { bytes[i] = b; anyHigh = true; }
            else if (c <= 0xFF) { bytes[i] = (byte) c; anyHigh = true; }
            else return text; // a character no single Windows-1252 byte reads as: not this mangling
         }

         if (!anyHigh) return text;

         try { return StrictUtf8.GetString(bytes); }
         catch (DecoderFallbackException) { return text; }
      }

      /// <summary>
      ///   The console's mangling, reproduced (UTF-8 bytes read back one by one as Windows-1252), so cr.test all and the
      ///   unit tests can feed CR's commands exactly what the console delivers.
      /// </summary>
      public static string Mangle(string text)
      {
         if (string.IsNullOrEmpty(text)) return text;

         var sb = new StringBuilder();
         foreach (byte b in Encoding.UTF8.GetBytes(text))
            sb.Append(b >= 0x80 && b <= 0x9F ? Cp1252High[b - 0x80] : (char) b);

         return sb.ToString();
      }

      private static Dictionary<char, byte> BuildHighToByte()
      {
         var map = new Dictionary<char, byte>();
         for (var i = 0; i < Cp1252High.Length; i++) map[Cp1252High[i]] = (byte) (0x80 + i);

         return map;
      }
   }
}
