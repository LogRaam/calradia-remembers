// Code written by Gabriel Mailhot, 09/11/2026.
// What these pin: how CR's JSON goes into a save (SaveBlobCodec), and the size it must never reach.
//
// tashmetu (Nexus, 09/11/2026): "long campaigns with CR stop loading". The game numbers a byte[]'s entries on 3 bytes, so
// a blob past 16,777,215 bytes is saved without error and never loads again; CR's relation ledger held 18.2 million
// bytes and its memory store 15.9 million. If these fail, a long campaign bricks again, an older save loses its memories
// on load, or a name with an accent comes back garbled.

#region

using System.Linq;
using System.Text;
using CalradiaRemembers.Logic.Saving;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SaveBlobCodecTests
   {
      // STAKE: what is saved is what comes back, names with accents and other scripts included (Zoë, Ærlaug, Хаган).
      [Test]
      public void GIVEN_json_with_accented_names_WHEN_encoded_and_decoded_THEN_it_comes_back_unchanged()
      {
         const string json = "{\"lord_1\":{\"Name\":\"Zoë of Ærlaug\",\"Note\":\"Хаган - été\"}}";

         SaveBlobCodec.Decode(SaveBlobCodec.Encode(json)).Should().Be(json);
      }

      // STAKE: a save written by an older CR holds plain JSON; it must still load, or every memory of the campaign is lost.
      [Test]
      public void GIVEN_plain_json_from_an_older_save_WHEN_decoded_THEN_it_is_read_as_it_was()
      {
         byte[] old = Encoding.UTF8.GetBytes("{\"a\":1}");

         SaveBlobCodec.IsCompressed(old).Should().BeFalse();
         SaveBlobCodec.Decode(old).Should().Be("{\"a\":1}");
      }

      // STAKE: a fresh campaign or an empty store saves nothing and loads nothing, without throwing.
      [Test]
      public void GIVEN_no_json_WHEN_encoded_or_decoded_THEN_nothing_is_stored_and_nothing_read()
      {
         SaveBlobCodec.Encode(null).Should().BeEmpty();
         SaveBlobCodec.Encode("").Should().BeEmpty();
         SaveBlobCodec.Decode(null).Should().BeNull();
         SaveBlobCodec.Decode(new byte[0]).Should().BeNull();
      }

      // STAKE (tashmetu): JSON of the size that broke his save (18.2 million bytes) lands far under the ceiling once
      // compressed, and is written.
      [Test]
      public void GIVEN_json_larger_than_the_engine_ceiling_WHEN_encoded_THEN_it_is_stored_far_under_it()
      {
         var sb = new StringBuilder("{");
         for (int i = 0; sb.Length < 18_200_000; i++)
            sb.Append($"\"lord_{i}|lord_{i * 7 % 9973}\":{{\"Day\":{i % 4000},\"Baseline\":{i % 200 - 100}}},");
         sb.Append("\"end\":0}");
         string json = sb.ToString();

         byte[] stored = SaveBlobCodec.Encode(json);

         Encoding.UTF8.GetByteCount(json).Should().BeGreaterThan(SaveBlobCodec.EngineCeiling);
         stored.Length.Should().BeLessThan(SaveBlobCodec.WarnAt);
         SaveBlobCodec.Judge(stored.Length).Should().Be(SaveBlobVerdict.Fine);
         SaveBlobCodec.Decode(stored).Should().Be(json);
      }

      // STAKE (tashmetu): a blob near the size past which the save never loads is not written; one store lost is far better
      // than a campaign that cannot be opened.
      [Test]
      public void GIVEN_a_blob_near_the_ceiling_WHEN_judged_THEN_it_is_refused_and_a_growing_one_warned()
      {
         SaveBlobCodec.RefuseAt.Should().BeLessThan(SaveBlobCodec.EngineCeiling);
         SaveBlobCodec.Judge(SaveBlobCodec.EngineCeiling + 1).Should().Be(SaveBlobVerdict.Refuse);
         SaveBlobCodec.Judge(SaveBlobCodec.RefuseAt).Should().Be(SaveBlobVerdict.Refuse);
         SaveBlobCodec.Judge(SaveBlobCodec.WarnAt).Should().Be(SaveBlobVerdict.Warn);
         SaveBlobCodec.Judge(1000).Should().Be(SaveBlobVerdict.Fine);
      }

      // STAKE: the codec's own output is recognised as compressed, so a new save is never read as plain text.
      [Test]
      public void GIVEN_encoded_json_WHEN_checked_THEN_it_is_known_as_compressed()
      {
         SaveBlobCodec.IsCompressed(SaveBlobCodec.Encode("{}")).Should().BeTrue();
         SaveBlobCodec.IsCompressed(Enumerable.Repeat((byte) '{', 4).ToArray()).Should().BeFalse();
      }
   }
}
