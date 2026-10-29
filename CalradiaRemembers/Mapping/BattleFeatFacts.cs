// Code written by Gabriel Mailhot, 06/09/2026.
// Battle-memory enrichment, Inc 1: the one bridge between a live Hero and the pure prowess ruling. Reads only what
// the game itself fights with (the best of the weapon skills, mobility, level) plus the clan tier as the FAME axis,
// because Bannerlord keeps renown on the clan and not on the hero. Kept in one place so the mod never grows a
// second, drifting notion of "who is the better fighter".

#region

using CalradiaRemembers.Logic.Battles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads a live hero's fighting facts for <see cref="CombatProwessPolicy" />.</summary>
   public static class BattleFeatFacts
   {
      /// <summary>The hero's fighting facts, or a harmless floor when the hero cannot be read.</summary>
      public static CombatantFacts For(Hero hero)
      {
         if (hero == null) return new CombatantFacts {BestWeaponSkill = 0, Riding = 0, Athletics = 0, Level = 0, ClanTier = 0};

         int bestWeapon = Best(hero, DefaultSkills.OneHanded, DefaultSkills.TwoHanded, DefaultSkills.Polearm,
            DefaultSkills.Bow, DefaultSkills.Crossbow, DefaultSkills.Throwing);

         return new CombatantFacts {BestWeaponSkill = bestWeapon, Riding = Skill(hero, DefaultSkills.Riding), Athletics = Skill(hero, DefaultSkills.Athletics), Level = hero.Level, ClanTier = StatureReader.Of(hero).ClanTier};
      }

      private static int Best(Hero hero, params SkillObject[] skills)
      {
         var best = 0;
         foreach (SkillObject skill in skills)
         {
            int value = Skill(hero, skill);
            if (value > best) best = value;
         }

         return best;
      }

      private static int Skill(Hero hero, SkillObject skill)
      {
         try { return skill == null ? 0 : hero.GetSkillValue(skill); }
         catch { return 0; }
      }
   }
}
