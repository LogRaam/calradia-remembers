// Code written by Gabriel Mailhot, 20/08/2026.
// teach_skill closes an empty-promise class the mod fixes everywhere else: NPCs already offer lessons in
// prose, but nothing granted real skill XP. TeachSkillPolicy is the law the bridge (ExecuteTeachSkill)
// re-validates against before ever calling HeroDeveloper.AddSkillXp, so these tests are the only protection
// this silently-breaking rule has. The knobs it pins: a teacher must be genuinely competent (the
// TeacherSkillFloor gate closes "a novice cannot teach mastery they do not have"), the granted XP is a NUDGE
// whose ceiling scales with the teacher's own mastery but never exceeds MaxCeiling (never a level-up), and a
// per-NPC cooldown (mirroring RelationGate's cap+cooldown shape) stops one companion being re-asked for a
// fresh lesson every turn.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TeachSkillPolicyTests
   {
      // STAKE: without this floor, a companion who barely knows a skill (say, level 20) could be badgered into
      // "teaching" it, an empty lesson with nothing behind it. 100 is the line between dabbling and mastery.
      [Test]
      public void GIVEN_a_teacher_below_the_skill_floor_WHEN_checking_CanTeach_THEN_it_is_false()
      {
         TeachSkillPolicy.CanTeach(99).Should().BeFalse();
      }

      // STAKE: pins the exact boundary the floor sits at, so a future edit cannot silently shift it off by one
      // in either direction without a test noticing.
      [Test]
      public void GIVEN_a_teacher_exactly_at_the_skill_floor_WHEN_checking_CanTeach_THEN_it_is_true()
      {
         TeachSkillPolicy.CanTeach(TeachSkillPolicy.TeacherSkillFloor).Should().BeTrue();
      }

      // STAKE: the core "empty promise" guard. A weak teacher must never grant XP, however generous the LLM's
      // proposed amount, or the bridge would let a barely-competent NPC teach a lesson they have no business
      // giving.
      [Test]
      public void GIVEN_a_teacher_below_the_floor_WHEN_resolving_granted_xp_THEN_nothing_is_granted()
      {
         TeachSkillPolicy.GrantedXp(50, 40, null, 100.0).Should().Be(0);
      }

      // STAKE: "a lesson never grants more than a nudge" for the LEAST competent teacher who still clears the
      // floor: their ceiling is the smallest in the whole scale.
      [Test]
      public void GIVEN_a_teacher_just_at_the_floor_WHEN_a_generous_amount_is_requested_THEN_it_is_capped_to_the_minimum_ceiling()
      {
         int granted = TeachSkillPolicy.GrantedXp(TeachSkillPolicy.TeacherSkillFloor, 1000, null, 100.0);

         granted.Should().Be(10, "a bare-floor teacher's ceiling is the minimum of the scale");
      }

      // STAKE: "never a level-up", even the most masterful teacher at the vanilla skill cap (300) cannot
      // hand out unbounded XP; the absolute cap holds regardless of how generous the LLM's proposal is.
      [Test]
      public void GIVEN_a_master_teacher_at_the_skill_cap_WHEN_a_huge_amount_is_requested_THEN_it_is_capped_to_the_maximum_ceiling()
      {
         int granted = TeachSkillPolicy.GrantedXp(300, 5000, null, 100.0);

         granted.Should().Be(60, "even the most masterful teacher never exceeds the absolute nudge cap");
      }

      // STAKE: "a master grants more than a novice", the ceiling must genuinely scale with the teacher's own
      // mastery, not just clamp everyone to the same number regardless of skill.
      [Test]
      public void GIVEN_a_mid_tier_teacher_WHEN_a_generous_amount_is_requested_THEN_the_ceiling_sits_between_the_floor_and_the_cap()
      {
         // Halfway between the floor (100) and the cap (300): the ceiling should land roughly midway
         // between MinCeiling (10) and MaxCeiling (60), not pinned to either extreme.
         int granted = TeachSkillPolicy.GrantedXp(200, 1000, null, 100.0);

         granted.Should().BeInRange(11, 59, "a mid-tier teacher's ceiling must sit strictly between the extremes");
      }

      // STAKE: a modest, in-character request should pass through untouched, the ceiling only bites a
      // proposal that exceeds it, so a restrained lesson is never inflated up to the cap.
      [Test]
      public void GIVEN_a_requested_amount_below_the_ceiling_WHEN_resolving_granted_xp_THEN_the_requested_amount_lands_unclamped()
      {
         TeachSkillPolicy.GrantedXp(300, 5, null, 100.0).Should().Be(5);
      }

      // STAKE: the anti-farm guard at the heart of this policy. Without it, a player could re-ask the same
      // companion for a lesson every single exchange and rack up skill XP far faster than the design intends.
      [Test]
      public void GIVEN_a_second_lesson_within_the_cooldown_window_WHEN_resolving_granted_xp_THEN_nothing_is_granted()
      {
         // Last lesson at hour 100; a second request 5h later is well inside the 24h window.
         TeachSkillPolicy.GrantedXp(300, 30, 100.0, 105.0).Should().Be(0);
      }

      // STAKE: confirms the cooldown is a genuine WINDOW, not a permanent block: once 24h+ pass, the same NPC
      // may teach again, so a companion does not stop teaching forever after a single lesson.
      [Test]
      public void GIVEN_a_lesson_after_the_cooldown_window_has_elapsed_WHEN_resolving_granted_xp_THEN_it_lands_again()
      {
         // Last lesson at hour 100; 25h later is past the 24h window.
         TeachSkillPolicy.GrantedXp(300, 30, 100.0, 125.0).Should().Be(30);
      }

      // STAKE: a fresh NPC who has never taught before (null LastTaughtHour) must not be blocked by a
      // cooldown that never started, otherwise every companion's very first lesson would be gated to zero.
      [Test]
      public void GIVEN_the_teacher_has_never_taught_before_WHEN_resolving_granted_xp_THEN_it_is_not_blocked_by_the_cooldown()
      {
         TeachSkillPolicy.GrantedXp(300, 30, null, 100.0).Should().Be(30);
      }

      // STAKE: guards the early-return branch: a garbled or non-positive proposed amount (an unparsed
      // parameter defaulting to 0, or a negative value) must never grant XP, even from a fully eligible master.
      [Test]
      public void GIVEN_a_non_positive_requested_amount_WHEN_resolving_granted_xp_THEN_nothing_is_granted()
      {
         TeachSkillPolicy.GrantedXp(300, 0, null, 100.0).Should().Be(0);
         TeachSkillPolicy.GrantedXp(300, -5, null, 100.0).Should().Be(0);
      }
   }
}
