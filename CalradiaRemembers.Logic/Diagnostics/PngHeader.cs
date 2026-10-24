// Code written by Gabriel Mailhot, 24/10/2026.
// Which kind of PNG an image is, read from its IHDR chunk. Lordfadooboo (Nexus, 23/10/2026): his own chat portraits
// showed blue while Rhagaea's did not; his were PNGs with an alpha channel (colour type 6), hers a JPG. The PNG norm
// fixes the byte order (always R, G, B, A); what changes the game engine's handling is the colour type. The portrait
// loader now hands the engine one encoding only, RGBA (type 6, the encoding every CR icon and the political map
// already use and that renders true), and checks it did.

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>Reads a PNG's colour type. Pure.</summary>
   public static class PngHeader
   {
      /// <summary>PNG colour type 6: red, green, blue and alpha, eight bits each in the images CR writes.</summary>
      public const int Rgba = 6;

      /// <summary>PNG colour type 2: red, green and blue, no alpha.</summary>
      public const int Rgb = 2;

      private static readonly byte[] Signature = {0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A};

      /// <summary>
      ///   The colour type in the IHDR chunk (0 grey, 2 RGB, 3 palette, 4 grey and alpha, 6 RGBA), or -1 when the bytes are
      ///   not a PNG (a JPG, a truncated file).
      /// </summary>
      public static int ColorType(byte[] bytes)
      {
         if (bytes == null || bytes.Length < 26) return -1;

         for (var i = 0; i < Signature.Length; i++)
            if (bytes[i] != Signature[i]) return -1;

         // Signature (8), then the IHDR chunk: length (4), "IHDR" (4), width (4), height (4), bit depth (1), colour type.
         return bytes[12] == 'I' && bytes[13] == 'H' && bytes[14] == 'D' && bytes[15] == 'R' ? bytes[25] : -1;
      }
   }
}
