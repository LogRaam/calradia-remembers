// Code written by Gabriel Mailhot, 26/07/2026.
// StyleCachePolicy decides whether the mod's StyleService must re-read a style .txt file from disk before
// building the next prompt, or may keep serving its cached text. Reading a file is expensive, so this is
// consulted before every message; the engine hands it what is cached and the file's cheap last-write
// stamp. Get it wrong and either the player picks a new style (or a modder edits the .txt mid-session) and
// the stale cached voice keeps applying, silently ignoring the change, or the engine pays a disk read on
// every single message even when nothing changed.

#region

using CalradiaRemembers.Logic.Styles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Styles
{
    [TestFixture]
    public sealed class StyleCachePolicyTests
    {
        // STAKE: the first message of a session must load the style from disk, since nothing is cached yet.
        [Test]
        public void GIVEN_nothing_cached_yet_WHEN_deciding_THEN_it_reloads()
        {
            StyleCachePolicy.ShouldReload(null, "Default", 0, 100).Should().BeTrue();
            StyleCachePolicy.ShouldReload("   ", "Default", 100, 100).Should().BeTrue();
        }

        // STAKE: this is the whole point of the cache, no disk read on every message when nothing changed.
        [Test]
        public void GIVEN_the_same_name_and_the_same_timestamp_WHEN_deciding_THEN_it_does_not_reload()
        {
            StyleCachePolicy.ShouldReload("Whimsical", "Whimsical", 12345, 12345).Should().BeFalse();
        }

        // STAKE: Gabriel's rule (a), picking another style in the popup must take effect on the very next send.
        [Test]
        public void GIVEN_a_different_requested_name_WHEN_deciding_THEN_it_reloads()
        {
            StyleCachePolicy.ShouldReload("Whimsical", "Gritty-Noir", 12345, 12345).Should().BeTrue();
        }

        // STAKE: Gabriel's rule (b), a modder fine-tuning the .txt during play must see it apply on the
        // next message without restarting.
        [Test]
        public void GIVEN_the_same_name_WHEN_the_timestamp_changed_THEN_it_reloads()
        {
            StyleCachePolicy.ShouldReload("Whimsical", "Whimsical", 12345, 67890).Should().BeTrue();
        }

        // STAKE: restoring a previous version of a style file must apply; this is why the rule compares
        // for inequality rather than "newer than" (a strictly-newer test would wrongly keep the stale cache).
        [Test]
        public void GIVEN_the_same_name_WHEN_the_timestamp_is_older_than_cached_THEN_it_still_reloads()
        {
            StyleCachePolicy.ShouldReload("Whimsical", "Whimsical", 67890, 12345).Should().BeTrue();
        }
    }
}
