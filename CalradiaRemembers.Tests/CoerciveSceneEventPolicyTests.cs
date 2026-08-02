// Code written by Gabriel Mailhot, 02/08/2026.
// Guards the line between coercion and intimacy in the memory model. A captive/captor scene is non-consensual;
// if a romantic [EVENT] from one were stored as consensual intimacy, the victim would "have intimacy with" their
// abuser, it would grant the intimacy regard gain, and it could rouse jealousy as courtship. This policy is what
// stops that. If it regresses, rape starts reading as a love affair in the NPC's memory.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CoerciveSceneEventPolicyTests
    {
        // The whole point: an Intimacy event emitted inside a coercive scene must be discarded, never stored as
        // a consensual memory.
        [Test]
        public void GIVEN_a_coercive_scene_WHEN_the_event_is_Intimacy_THEN_it_is_dropped()
        {
            CoerciveSceneEventPolicy.ShouldDropRomanticEvent(NotableEventType.Intimacy, true).Should().BeTrue();
        }

        // Flirt is romantic too: a "flirt" logged during a rape scene is the same category error, dropped as well.
        [Test]
        public void GIVEN_a_coercive_scene_WHEN_the_event_is_Flirt_THEN_it_is_dropped()
        {
            CoerciveSceneEventPolicy.ShouldDropRomanticEvent(NotableEventType.Flirt, true).Should().BeTrue();
        }

        // Only ROMANTIC events are coercion-sensitive: a coercive scene still records its non-romantic memories
        // (a conflict, a betrayal, the captivity itself) normally, so the drop must not swallow those.
        [Test]
        public void GIVEN_a_coercive_scene_WHEN_the_event_is_not_romantic_THEN_it_is_kept()
        {
            CoerciveSceneEventPolicy.ShouldDropRomanticEvent(NotableEventType.Conflict, true).Should().BeFalse();
            CoerciveSceneEventPolicy.ShouldDropRomanticEvent(NotableEventType.Captivity, true).Should().BeFalse();
        }

        // A consensual scene is the normal path: intimacy there is real intimacy and must be kept, so the drop
        // fires ONLY under coercion.
        [Test]
        public void GIVEN_a_non_coercive_scene_WHEN_the_event_is_Intimacy_THEN_it_is_kept()
        {
            CoerciveSceneEventPolicy.ShouldDropRomanticEvent(NotableEventType.Intimacy, false).Should().BeFalse();
            CoerciveSceneEventPolicy.ShouldDropRomanticEvent(NotableEventType.Flirt, false).Should().BeFalse();
        }

        // A turn with no [EVENT] at all is the common case; a null type must be a clean no-drop, never a throw.
        [Test]
        public void GIVEN_no_event_WHEN_asked_THEN_nothing_is_dropped()
        {
            CoerciveSceneEventPolicy.ShouldDropRomanticEvent(null, true).Should().BeFalse();
            CoerciveSceneEventPolicy.ShouldDropRomanticEvent(null, false).Should().BeFalse();
        }
    }
}
