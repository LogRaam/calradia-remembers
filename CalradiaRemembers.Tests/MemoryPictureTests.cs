// Code written by Gabriel Mailhot, 20/11/2026.
// What MemoryWriteWatch and MemoryPicturePolicy decide for pictures another mod paints for the Memories panel (Bhelogan,
// Discord, 20/11/2026; Gabriel: "oui on y va. et on protège les IDS associés à des images"). The watch reports each memory
// written since it last looked, with an id and, at a conversation's close, the conversation it was born in; the policy
// takes or refuses a picture, names CR's copy, and says when the panel offers to paint. If this is wrong, a mod paints a
// memory twice or never, links it to the wrong conversation, a repainted memory keeps its old picture, or a secret's words
// reach another mod.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic.Extension;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MemoryPictureTests
   {
      private static WatchedProfile[] Ira(params NotableEvent[] memories)
         => new[] {new WatchedProfile {NpcId = "ira", Memories = new List<NotableEvent>(memories)}};

      // STAKE: loading a save is not "a hundred new memories": the first look only takes stock, or a painter would paint
      // the character's whole past at once.
      [Test]
      public void GIVEN_memories_already_there_WHEN_the_watch_first_looks_THEN_it_reports_nothing()
      {
         var watch = new MemoryWriteWatch();

         watch.Look(Ira(new NotableEvent(1, NotableEventType.Other, "Met at Pravend.")), null, true).Should().BeEmpty();
      }

      // STAKE: Bhelogan: "a 'new memory added' event ... would let us prepare the picture while the scene is still fresh".
      [Test]
      public void GIVEN_a_memory_written_after_the_first_look_WHEN_the_watch_looks_THEN_it_is_reported_once_with_an_id()
      {
         var watch = new MemoryWriteWatch();
         var old = new NotableEvent(1, NotableEventType.Other, "Met at Pravend.");
         watch.Look(Ira(old), null, true);
         var fresh = new NotableEvent(2, NotableEventType.Agreement, "Swore to ride with me.");

         IReadOnlyList<WatchedMemory> first = watch.Look(Ira(old, fresh), null, true);
         IReadOnlyList<WatchedMemory> second = watch.Look(Ira(old, fresh), null, true);

         first.Should().ContainSingle().Which.IsNew.Should().BeTrue();
         first[0].Memory.Id.Should().NotBeNullOrEmpty();
         first[0].NpcId.Should().Be("ira");
         second.Should().BeEmpty();
      }

      // STAKE: two "Spoke with X." on one day are two sittings, two memories, not one seen twice.
      [Test]
      public void GIVEN_two_memories_with_the_same_words_WHEN_the_watch_looks_THEN_both_are_reported()
      {
         var watch = new MemoryWriteWatch();
         watch.Look(Ira(), null, true);

         watch.Look(Ira(new NotableEvent(5, NotableEventType.Other, "Spoke with Gabriel."), new NotableEvent(5, NotableEventType.Other, "Spoke with Gabriel.")), null, true)
              .Should().HaveCount(2);
      }

      // STAKE: Bhelogan: "Memories are a single sentence, so without this we can only guess who was there". A memory first
      // seen as a conversation closes was written in it.
      [Test]
      public void GIVEN_a_memory_first_seen_as_a_conversation_closes_WHEN_the_watch_looks_THEN_it_carries_that_conversation()
      {
         var watch = new MemoryWriteWatch();
         watch.Look(Ira(), null, true);

         watch.Look(Ira(new NotableEvent(5, NotableEventType.Other, "Spoke with Gabriel.")), "session1", true)[0].Memory.SessionId.Should().Be("session1");
      }

      // STAKE: the conversation's summary later rewrites "Spoke with X." into what was said: the painter hears of the better
      // words (not as a new memory), and the memory keeps its id and its conversation.
      [Test]
      public void GIVEN_a_memory_rewritten_by_the_summary_WHEN_the_watch_looks_THEN_it_is_reported_as_a_rewrite_with_the_same_id()
      {
         var watch = new MemoryWriteWatch();
         watch.Look(Ira(), null, true);
         var anchor = new NotableEvent(5, NotableEventType.Other, "Spoke with Gabriel.");
         watch.Look(Ira(anchor), "session1", true);

         NotableEvent rich = anchor with {summary = "Promised to ride to Pravend at dawn."};
         IReadOnlyList<WatchedMemory> written = watch.Look(Ira(rich), "session2", true);

         written.Should().ContainSingle().Which.IsNew.Should().BeFalse();
         written[0].Memory.Id.Should().Be(anchor.Id);
         written[0].Memory.SessionId.Should().Be("session1");
      }

      // STAKE: attaching a picture copies the memory (same words): that is not news to report back to the painter.
      [Test]
      public void GIVEN_a_memory_copied_with_a_picture_WHEN_the_watch_looks_THEN_nothing_is_reported()
      {
         var watch = new MemoryWriteWatch();
         var memory = new NotableEvent(1, NotableEventType.Other, "Met at Pravend.");
         watch.Look(Ira(memory), null, true);
         watch.EnsureId(memory);

         watch.Look(Ira(memory with {ImageFile = "a.png"}), null, true).Should().BeEmpty();
      }

      // STAKE: a memory the panel or GetMemories gave an id keeps it; asking again never changes it (the picture hangs on it).
      [Test]
      public void GIVEN_a_memory_with_an_id_WHEN_its_id_is_asked_again_THEN_it_does_not_change()
      {
         var watch = new MemoryWriteWatch();
         var memory = new NotableEvent(1, NotableEventType.Other, "Met at Pravend.");

         string first = watch.EnsureId(memory);

         watch.EnsureId(memory).Should().Be(first);
      }

      // STAKE: another campaign loaded is a fresh start, not a flood of "new" memories.
      [Test]
      public void GIVEN_the_watch_reset_WHEN_it_looks_again_THEN_it_takes_stock_silently()
      {
         var watch = new MemoryWriteWatch();
         watch.Look(Ira(), null, true);
         watch.Reset();

         watch.Look(Ira(new NotableEvent(1, NotableEventType.Other, "Met at Pravend.")), null, true).Should().BeEmpty();
      }

      // STAKE: a picture on disk, of a kind CR draws, is taken; a video, a missing file or no memory named is refused.
      [Test]
      public void GIVEN_pictures_handed_to_SetImage_WHEN_judged_THEN_only_a_still_picture_on_disk_for_a_named_memory_is_taken()
      {
         MemoryPicturePolicy.WhyRefused(new MemoryPictureFacts {Named = true, CampaignLoaded = true, FileExists = true, Extension = ".JPG"}).Should().BeNull();
         MemoryPicturePolicy.WhyRefused(new MemoryPictureFacts {Named = true, CampaignLoaded = true, FileExists = true, Extension = ".crvid"}).Should().NotBeNull();
         MemoryPicturePolicy.WhyRefused(new MemoryPictureFacts {Named = true, CampaignLoaded = true, FileExists = false, Extension = ".png"}).Should().NotBeNull();
         MemoryPicturePolicy.WhyRefused(new MemoryPictureFacts {Named = false, CampaignLoaded = true, FileExists = true, Extension = ".png"}).Should().NotBeNull();
         MemoryPicturePolicy.WhyRefused(new MemoryPictureFacts {Named = true, CampaignLoaded = false, FileExists = true, Extension = ".png"}).Should().NotBeNull();
      }

      // STAKE: CR keeps a loaded picture by its path, so a memory painted anew must be copied under a new name.
      [Test]
      public void GIVEN_a_memory_painted_twice_WHEN_named_THEN_each_copy_has_its_own_name_and_no_path_sneaks_in()
      {
         string a = MemoryPicturePolicy.FileName("abc123", ".PNG");
         string b = MemoryPicturePolicy.FileName("abc123", ".PNG");

         a.Should().StartWith("abc123_").And.EndWith(".png");
         b.Should().NotBe(a);
         MemoryPicturePolicy.FileName("../../evil", ".png").Should().NotContain("/").And.NotContain("..");
      }

      // STAKE: GetMemories keeps secrets from other mods; a request to paint carries the memory's words, so a secret is
      // never offered for painting. Without a painter, the panel offers nothing.
      [Test]
      public void GIVEN_the_selected_memory_WHEN_the_panel_decides_THEN_it_offers_painting_only_for_a_public_memory_with_a_painter()
      {
         MemoryPicturePolicy.OffersPainting(new MemoryPaintFacts {AnyPainter = true}).Should().BeTrue();
         MemoryPicturePolicy.OffersPainting(new MemoryPaintFacts {AnyPainter = true, IsPrivate = true}).Should().BeFalse();
         MemoryPicturePolicy.OffersPainting(new MemoryPaintFacts {AnyPainter = false}).Should().BeFalse();
      }

      // STAKE: Bhelogan's agent: "A 'busy' state on the button while that runs would be ideal"; a painter that fails
      // silently must not leave it busy forever.
      [Test]
      public void GIVEN_a_painting_asked_for_WHEN_time_passes_THEN_it_is_busy_until_it_returns_or_times_out()
      {
         var underway = new MemoryPaintingsUnderway();
         var t0 = new DateTime(2026, 11, 20, 12, 0, 0);
         underway.Begin("m1", t0);

         underway.IsBusy("m1", t0.AddSeconds(30)).Should().BeTrue();
         MemoryPicturePolicy.PaintLabel(new MemoryPaintFacts {Busy = true}).Should().Be("Painting...");
         underway.IsBusy("m1", t0.AddSeconds(MemoryPicturePolicy.PaintingTimeoutSeconds + 1)).Should().BeFalse();

         underway.Begin("m2", t0);
         underway.End("m2");
         underway.IsBusy("m2", t0.AddSeconds(1)).Should().BeFalse();
      }
   }
}
