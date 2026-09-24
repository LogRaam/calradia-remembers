// Code written by Gabriel Mailhot, 23/09/2026.
// Documents the modder API's BLOCKING guard. Gabriel, 23/09/2026: no example showed the blocking mode or a ratchet in
// action on this project; these are that example, taken from the day it was needed. Every other map accepts what the
// code now says. This one refuses to let a SHIPPED member vanish, because the one who pays is someone else's bridge
// failing on a player's machine, reported to them and not to us. The live guard, run against the real code, is in
// MapsAreCurrentTests; these pin what it decides.

#region

using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   [TestFixture]
   public sealed class ModderApiGuardTests
   {
      private const string Released = @"### ExternalWorldEvent

_One world fact._

- `List<string> HeroIds { get; set; }`
- `string PlaceId { get; set; }`

### Feed

- `static FeedResult Record(ExternalWorldEvent e)`
";

      // The case the guard exists for: a bridge built on 2.5.22 sets PlaceId. If a later change drops it, the bridge's
      // reflection finds nothing (silently) or a compiled reference fails to load. The suite must say so before release.
      [Test]
      public void GIVEN_a_released_member_WHEN_the_code_no_longer_has_it_THEN_it_is_refused()
      {
         const string current = @"### ExternalWorldEvent

- `List<string> HeroIds { get; set; }`

### Feed

- `static FeedResult Record(ExternalWorldEvent e)`
";

         ModderApiGuard.Vanished(Released, current).Should().Equal("ExternalWorldEvent: string PlaceId { get; set; }");
      }

      // A changed signature breaks a compiled caller exactly as a removal does, so it must read as one.
      [Test]
      public void GIVEN_a_released_method_WHEN_its_signature_changes_THEN_the_old_signature_is_refused_as_vanished()
      {
         const string current = @"### ExternalWorldEvent

- `List<string> HeroIds { get; set; }`
- `string PlaceId { get; set; }`

### Feed

- `static FeedResult Record(ExternalWorldEvent e, bool strict)`
";

         ModderApiGuard.Vanished(Released, current).Should().Equal("Feed: static FeedResult Record(ExternalWorldEvent e)");
      }

      // The 23/09/2026 case, and why the baseline is the RELEASED surface rather than the committed map:
      // PlayerIsActor was committed, then replaced by ActorHeroIds before 2.5.23 shipped. Nobody could have built on it,
      // so refusing its removal would only have trained us to reach for the escape hatch.
      [Test]
      public void GIVEN_a_member_that_never_shipped_WHEN_it_is_withdrawn_before_release_THEN_it_passes()
      {
         const string current = @"### ExternalWorldEvent

- `List<string> ActorHeroIds { get; set; }`
- `List<string> HeroIds { get; set; }`
- `string PlaceId { get; set; }`

### Feed

- `static FeedResult Record(ExternalWorldEvent e)`
";

         ModderApiGuard.Vanished(Released, current).Should().BeEmpty("PlayerIsActor is not in the released snapshot, and additions are free");
      }

      // Two types can carry a member of the same shape; removing it from one must not be hidden by the other.
      [Test]
      public void GIVEN_two_types_with_the_same_member_WHEN_one_loses_it_THEN_that_one_is_named()
      {
         const string released = "### A\n\n- `string Id { get; set; }`\n\n### B\n\n- `string Id { get; set; }`\n";
         const string current = "### A\n\n- `string Id { get; set; }`\n\n### B\n\n";

         ModderApiGuard.Vanished(released, current).Should().Equal("B: string Id { get; set; }");
      }

      // The failure message names the version a member shipped in, so the release note can say "since 2.5.22".
      [Test]
      public void GIVEN_a_recorded_snapshot_WHEN_read_back_THEN_it_names_its_version_and_keeps_every_member()
      {
         string snapshot = ModderApiGuard.Snapshot(Released, "v2.5.23");

         ModderApiGuard.ReleasedVersionOf(snapshot).Should().Be("v2.5.23");
         ModderApiGuard.MembersListedIn(snapshot).Should().BeEquivalentTo(ModderApiGuard.MembersListedIn(Released));
      }
   }
}
