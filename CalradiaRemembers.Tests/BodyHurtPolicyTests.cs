// Code written by Gabriel Mailhot, 12/09/2026.
// What these pin: that all four answers about a body are actually reachable, and that each one means what it
// says.
//
// WHY THEY EXIST, and it is the whole reason this policy was pulled out of the host at all. The first version
// lived inline in EncounterContextBuilder and TWO OF ITS FOUR BANDS COULD NEVER HAPPEN. Nothing noticed until
// a seeded lord at 60% of his health was asked how he fared and answered "I fare well enough."
//
// The rule had deferred entirely to Hero.IsWounded - "not wounded by the engine's own reckoning is WHOLE,
// whatever the arithmetic says" - and the engine only calls a hero wounded near the BOTTOM of the bar. So
// above that threshold everyone was Unhurt however battered, and below it the full-bar ratio was always tiny,
// so everyone was Grievous. Scratched never happened at all.
//
// The deference itself was right; it was pointed at the wrong question. IsWounded answers "can this person
// still fight", which is the correct authority for the bottom of the scale and no authority whatever on
// whether a man carries bruises worth mentioning.
//
// If these fail, a character has either stopped noticing he is hurt or started dying of a scratch.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Linq;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class BodyHurtPolicyTests
   {
      // Bannerlord's own shape: a hero of 100 who counts as wounded at 20 or below.
      private const int Whole = 100;
      private const int WoundLimit = 20;

      private static BodyHurtPolicy.Hurt Read(int hitPoints)
         => BodyHurtPolicy.Read(hitPoints, Whole, hitPoints <= WoundLimit, WoundLimit);

      // THE BUG, as the seeded run met it: a man at 60% who answered "I fare well enough".
      [Test]
      public void GIVEN_a_lord_at_sixty_percent_WHEN_asked_THEN_he_is_knocked_about_rather_than_whole()
      {
         Read(60).Should().Be(BodyHurtPolicy.Hurt.Scratched);
      }

      // THE PROPERTY THAT WOULD HAVE CAUGHT IT ON DAY ONE: every band must be something the world can
      // actually produce. A four-value scale of which two are unreachable is a two-value scale that misleads
      // whoever reads it.
      [Test]
      public void GIVEN_the_whole_range_of_health_WHEN_walked_THEN_every_band_is_reachable()
      {
         var seen = Enumerable.Range(0, Whole + 1).Select(Read).Distinct().ToList();

         seen.Should().Contain(BodyHurtPolicy.Hurt.Unhurt);
         seen.Should().Contain(BodyHurtPolicy.Hurt.Scratched);
         seen.Should().Contain(BodyHurtPolicy.Hurt.Wounded);
         seen.Should().Contain(BodyHurtPolicy.Hurt.Grievous);
      }

      // Being whole earns no words, and it has to stay the common case: this pack is carried by every
      // character in the game and most of them are fine.
      [Test]
      public void GIVEN_a_man_in_good_health_WHEN_read_THEN_he_is_whole_and_says_nothing()
      {
         Read(Whole).Should().Be(BodyHurtPolicy.Hurt.Unhurt);
         Read(95).Should().Be(BodyHurtPolicy.Hurt.Unhurt);
      }

      // The engine keeps the authority it actually has: once IT calls somebody wounded, they are down, and
      // this scale only decides how far.
      [Test]
      public void GIVEN_the_engine_calls_him_wounded_WHEN_read_THEN_the_answer_is_never_merely_bruised()
      {
         foreach (int hp in new[] {1, 5, 12, 19, 20})
         {
            BodyHurtPolicy.Hurt hurt = Read(hp);

            hurt.Should().NotBe(BodyHurtPolicy.Hurt.Unhurt);
            hurt.Should().NotBe(BodyHurtPolicy.Hurt.Scratched);
         }
      }

      // Graded WITHIN the wounded range rather than against the whole bar, which is the fix itself: the
      // wounded range is the bottom slice, so measuring it against full health puts everybody at its floor.
      [Test]
      public void GIVEN_two_wounded_men_WHEN_compared_THEN_the_worse_hurt_of_them_is_the_worse_hurt()
      {
         Read(3).Should().Be(BodyHurtPolicy.Hurt.Grievous);
         Read(18).Should().Be(BodyHurtPolicy.Hurt.Wounded);
      }

      // A hero the engine has no numbers for says nothing rather than claiming health, like every other read
      // in the pillar.
      [Test]
      public void GIVEN_a_body_with_no_numbers_WHEN_read_THEN_nothing_is_claimed()
      {
         BodyHurtPolicy.Read(50, 0, false, 20).Should().Be(BodyHurtPolicy.Hurt.Unknown);
      }

      // The shape holds for a hero with a different maximum, because heroes do not all have 100.
      [Test]
      public void GIVEN_a_hardier_hero_WHEN_read_THEN_the_bands_still_mean_the_same_thing()
      {
         BodyHurtPolicy.Read(120, 200, false, 40).Should().Be(BodyHurtPolicy.Hurt.Scratched);
         BodyHurtPolicy.Read(195, 200, false, 40).Should().Be(BodyHurtPolicy.Hurt.Unhurt);
         BodyHurtPolicy.Read(10, 200, true, 40).Should().Be(BodyHurtPolicy.Hurt.Grievous);
         BodyHurtPolicy.Read(35, 200, true, 40).Should().Be(BodyHurtPolicy.Hurt.Wounded);
      }
   }
}
