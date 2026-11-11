// Code written by Gabriel Mailhot, 10/11/2026.
// What these pin: that CR's PPMd (Ppmd7, a port of 7-Zip's PPMd var.H) writes exactly the stream 7-Zip and pyppmd write,
// and reads it back. Gabriel (10/11/2026): "je crois que ppmd est le bon choix", then "go" for the port. Every store of a
// 2.8.8 save goes through it: one bit wrong and a campaign's memories come back garbled or not at all, and a save written
// by one CR could not be read by the next. The vectors were made with pyppmd 1.3.1 (Ppmd7Encoder, flush without end mark):
// Tools/CodecBench/ppmd_vectors.py writes them again.

#region

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CalradiaRemembers.Logic.Saving;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PpmdTests
   {
      private static readonly string[] Words = {
         "the", "lord", "of", "Calradia", "remembers", "a", "debt", "owed", "déjà", "vu", "Ærin", "rode", "to", "Pravend", "and", "spoke",
         "with", "her", "sister", "about", "the", "war", ",", ".", "\n", "gold", "denars", "400", "5", "oath", "sworn", "Þór", "naïve",
         "château", "!"
      };

      // name, input length, order, memory, compressed length, SHA-256 of the compressed stream (pyppmd 1.3.1)
      private static readonly object[] Vectors = {
         new object[] {"empty", 0, 6, 1048576, 5, "8855508aade16ec573d21e6a485dfd0a7624085c1a14b5ecdd6485de0c6839a4"},
         new object[] {"one", 1, 6, 1048576, 6, "3a438fe907e7548c47b353d575f29833e23380aa688c46d5893224d19228f7cb"},
         new object[] {"abra", 550, 4, 1048576, 29, "6f3236dbe684bb3dcae7022b5079dd5dea4725e16b5d4a6ea9978fc8e4f71832"},
         new object[] {"lcg_o2", 50000, 2, 1048576, 7780, "3c8b21b382979275a1a5bd2b0680f076e8f08ed9c612aac952815e1eb96eab40"},
         new object[] {"lcg_o8", 200000, 8, 16777216, 27551, "9a8153b12b649e8dc473fb014b5b11e0467ea476901f46ea2b782be12a615192"},
         new object[] {"lcg_o64", 100000, 64, 16777216, 14116, "f2155d6591a998a34100c28eb4b90d1919a53b6b10950df537ca0a9cdde26d18"},
         new object[] {"lcg_tiny_mem", 300000, 16, 65536, 61855, "8fb768e4d7589afc5af6940d53f85e02c1d195d923fe79bd8f6b3dfe779821df"},
         new object[] {"lcg_small_mem", 400000, 8, 262144, 61418, "9b0e6b8eabef6e5f63b7654460dce1ab5e3db126d051032b31774028fe5a9631"},
         new object[] {"bytes_all", 10240, 6, 1048576, 353, "5e17ece9e9e3f76e9f92df78f94049b77dd948fd8ef418fa5fe2e61f20b29237"}
      };

      // STAKE: the stream is 7-Zip's to the bit, at every order and through the model running out of memory (the tiny and
      // small memory cases restart the model and glue its free blocks); a slip there garbles every store after it.
      [TestCaseSource(nameof(Vectors))]
      public void GIVEN_a_reference_input_WHEN_encoded_THEN_the_stream_is_the_one_7zip_writes_and_it_reads_back(
         string name, int length, int order, int memory, int packedLength, string sha256)
      {
         byte[] data = Input(name, length);

         byte[] packed = Ppmd7.Encode(data, order, memory);

         packed.Length.Should().Be(packedLength, name);
         Hex(packed).Should().Be(sha256, name);
         Ppmd7.Decode(packed, data.Length, order, memory).Should().Equal(data, name);
      }

      // STAKE: a damaged store fails loudly, so the load falls back to an older form instead of loading garbage as memories.
      [Test]
      public void GIVEN_a_stream_cut_short_WHEN_decoded_for_its_full_length_THEN_it_does_not_come_back_as_the_input()
      {
         byte[] data = Input("lcg_o8", 200000);
         byte[] packed = Ppmd7.Encode(data, 8, 16 << 20);
         byte[] cut = packed.Take(packed.Length / 2).ToArray();

         byte[] back = null;
         Action read = () => back = Ppmd7.Decode(cut, data.Length, 8, 16 << 20);

         try { read(); }
         catch (InvalidOperationException) { return; }

         back.Should().NotEqual(data);
      }

      // STAKE: an order or memory PPMd var.H refuses is refused here too, never a model built on nonsense.
      [Test]
      public void GIVEN_an_order_or_memory_out_of_range_WHEN_asked_THEN_it_is_refused()
      {
         ((Action) (() => Ppmd7.Encode(new byte[1], 1, 1 << 20))).Should().Throw<ArgumentOutOfRangeException>();
         ((Action) (() => Ppmd7.Encode(new byte[1], 65, 1 << 20))).Should().Throw<ArgumentOutOfRangeException>();
         ((Action) (() => Ppmd7.Encode(new byte[1], 6, 100))).Should().Throw<ArgumentOutOfRangeException>();
      }

      #region private

      private static byte[] Input(string name, int length)
      {
         switch (name)
         {
            case "empty": return new byte[0];
            case "one": return Encoding.ASCII.GetBytes("a");
            case "abra": return Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("abracadabra", 50)));
            case "bytes_all": return Enumerable.Range(0, 40).SelectMany(_ => Enumerable.Range(0, 256).Select(b => (byte) b)).ToArray();
            default: return LcgText(length, Seed(name));
         }
      }

      private static long Seed(string name)
      {
         switch (name)
         {
            case "lcg_o2": return 1;
            case "lcg_o8": return 2;
            case "lcg_o64": return 3;
            case "lcg_tiny_mem": return 4;
            default: return 5;
         }
      }

      // The same words in the same order as the Python that made the vectors: x = (x * 1103515245 + 12345) mod 2^31.
      private static byte[] LcgText(int n, long seed)
      {
         var output = new System.Collections.Generic.List<byte>(n + 16);
         long x = seed;
         while (output.Count < n)
         {
            x = (x * 1103515245 + 12345) % 2147483648;
            output.AddRange(Encoding.UTF8.GetBytes(Words[(x >> 16) % Words.Length] + " "));
         }

         return output.Take(n).ToArray();
      }

      private static string Hex(byte[] bytes)
      {
         using (SHA256 sha = SHA256.Create())
            return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
      }

      #endregion
   }
}
