// Code written by Gabriel Mailhot, 24/10/2026.

// PngHeader tells the portrait loader whether the image it is about to hand the game engine is the RGBA encoding that
// renders true (Lordfadooboo, 23/10/2026: PNGs with alpha showed blue faces while a JPG did not, because the loader
// swapped red and blue on every image). The loader normalises every portrait to RGBA and checks the result with this;
// a wrong reading would either refuse a good portrait (falling back to the raw file) or let a wrong encoding through.

#region

using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PngHeaderTests
   {
      // Ira.png, the blue portrait, is colour type 6; Arwa.png, a portrait that rendered true after the old swap, is 2.
      [TestCase(6, 6)]
      [TestCase(2, 2)]
      [TestCase(3, 3)]
      public void GIVEN_a_png_WHEN_its_header_is_read_THEN_its_colour_type_is_known(byte colourType, int expected)
      {
         PngHeader.ColorType(Png(colourType)).Should().Be(expected);
      }

      // A JPG, or a file cut short, is not a PNG of any kind: the loader must not mistake it for the encoding it wants.
      [Test]
      public void GIVEN_bytes_that_are_not_a_png_WHEN_read_THEN_no_colour_type_is_claimed()
      {
         byte[] jpg = {0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte) 'J', (byte) 'F', (byte) 'I', (byte) 'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0};

         PngHeader.ColorType(jpg).Should().Be(-1);
         PngHeader.ColorType(new byte[10]).Should().Be(-1);
         PngHeader.ColorType(null).Should().Be(-1);
      }

      private static byte[] Png(byte colourType)
      {
         var b = new byte[33];
         new byte[] {0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte) 'I', (byte) 'H', (byte) 'D', (byte) 'R'}.CopyTo(b, 0);
         b[19] = 8;  // width 8
         b[23] = 8;  // height 8
         b[24] = 8;  // bit depth
         b[25] = colourType;

         return b;
      }
   }
}
