// Code written by Gabriel Mailhot, 16/11/2026.
// ForgePreferencePolicy decides which weapons a lord would ask the player to forge: first a better copy of what he
// fights with, then what his people forge in the order of his training. Schmidt (Discord, 16/11/2026): "I can't see a
// Nord lord having much use for Khuzait short bows, or Monchug using a 2 handed axe in battle." Wrong, a khan asks for a
// great axe he has never lifted, or a lord ignores the weapon in his own hand.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ForgePreferencePolicyTests
   {
      // STAKE: Schmidt's Monchug: a Khuzait khan who fights with sabre and lance is never offered a two-handed axe.
      [Test]
      public void GIVEN_a_khuzait_khan_with_sabre_and_lance_WHEN_his_forge_wishes_are_read_THEN_no_two_handed_axe_is_among_them()
      {
         IReadOnlyList<string> suited = ForgePreferencePolicy.Suited(new ForgeAskerFacts {
            CultureId = "khuzait", KindsHeWields = new[] {"sword", "polearm"},
            SkillsBestFirst = new[] {WeaponSkillFamily.Polearm, WeaponSkillFamily.OneHanded, WeaponSkillFamily.TwoHanded, WeaponSkillFamily.Throwing}
         });

         suited.Should().StartWith(new[] {"sword", "polearm"});
         suited.Should().NotContain("two_handed_axe");
      }

      // STAKE: a Sturgian or Nord lord trained in two hands asks for the great axe of his people.
      [Test]
      public void GIVEN_a_nord_lord_trained_in_two_hands_and_carrying_nothing_the_smithy_makes_WHEN_read_THEN_he_asks_for_a_great_axe()
      {
         ForgePreferencePolicy.Suited(new ForgeAskerFacts {
            CultureId = "nord", KindsHeWields = new string[0],
            SkillsBestFirst = new[] {WeaponSkillFamily.TwoHanded, WeaponSkillFamily.OneHanded}
         }).Should().StartWith(new[] {"two_handed_axe"});
      }

      // STAKE: the weapon in his own hand comes first, even one his people do not make: he knows what he fights with.
      [Test]
      public void GIVEN_a_lord_who_fights_with_a_weapon_foreign_to_his_people_WHEN_read_THEN_it_is_still_among_his_wishes()
      {
         ForgePreferencePolicy.Suited(new ForgeAskerFacts {CultureId = "khuzait", KindsHeWields = new[] {"two_handed_axe"}})
                              .Should().Contain("two_handed_axe");
      }

      // STAKE: the cue names a few kinds, his, never the whole smithy.
      [Test]
      public void GIVEN_any_lord_WHEN_read_THEN_at_most_three_kinds_and_never_none()
      {
         IReadOnlyList<string> suited = ForgePreferencePolicy.Suited(new ForgeAskerFacts {
            CultureId = "vlandia", KindsHeWields = new[] {"sword", "polearm", "mace", "dagger"},
            SkillsBestFirst = new[] {WeaponSkillFamily.OneHanded, WeaponSkillFamily.TwoHanded}
         });
         suited.Count.Should().BeInRange(1, ForgePreferencePolicy.MaxSuggested);

         ForgePreferencePolicy.Suited(null).Should().NotBeEmpty();
      }

      // STAKE: every kind suggested is one the craft_weapon task can verify, or the task could never close.
      [Test]
      public void GIVEN_every_people_WHEN_their_forge_is_read_THEN_every_kind_is_one_the_task_verifies()
      {
         foreach (string culture in new[] {"vlandia", "empire", "sturgia", "nord", "battania", "khuzait", "aserai", "unknown"})
            ForgePreferencePolicy.PeopleForge(culture).Should().BeSubsetOf(CraftWeaponPolicy.Kinds, culture);
      }
   }
}
