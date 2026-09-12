// Code written by Gabriel Mailhot, 12/09/2026.
// A test bed for the knowledge packs, because Gabriel's save had nothing to show them.
//
// The live sweep reported personal_bonds at 0 supplied out of 60 carried, and the reason turned out to be the
// world rather than the code: bonds come from exactly two places - a LIVE GRUDGE against somebody other than
// the player, and a TAKEN LOVER - and his campaign had neither. His answer: "ma partie n'a pas de liens
// intimes. Tu peux en injecter. Et tu peux injecter différents scénarios à différents NPC pour tester tous les
// cas."
//
// So each seeded character gets a DIFFERENT situation rather than all of them the same one. That is the point:
// the discretion axis only shows itself when a secret and an open bond sit side by side, and "do not promise
// what your body cannot do" only shows itself on somebody who is actually hurt.
//
// Everything it touches is reversible and marked. The grudges it plants carry a seed marker in their cause, so
// a leftover is identifiable in a log months later.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Knowledge;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>The marker every seeded grudge carries, so a leftover can be recognised long afterwards.</summary>
      private const string SeedMark = "[SEED]";

      /// <summary>Who was seeded this session, so `clear` knows whom to undo.</summary>
      private static readonly Dictionary<string, int> SeededHeroes = new();

      /// <summary>
      ///   cr.seed_knowledge [clear] — gives several characters DIFFERENT situations so every knowledge pack
      ///   has something to show, and reports what each now carries. `clear` undoes it.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("seed_knowledge", "cr")]
      public static string SeedKnowledge(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         bool clearing = args is {Count: > 0}
                         && string.Equals(args[0].Trim(), "clear", StringComparison.OrdinalIgnoreCase);

         try
         {
            return clearing ? Unseed(behavior) : Seed(behavior);
         }
         catch (Exception ex)
         {
            return $"cr.seed_knowledge failed: {ex.GetType().Name}: {ex.Message}";
         }
      }

      #region private

      private static string Seed(NpcMemoryCampaignBehavior behavior)
      {
         var today = (int) CampaignTime.Now.ToDays;

         List<Hero> pool = Hero.AllAliveHeroes
                               .Where(h => h != null && h != Hero.MainHero && h.IsAlive && !h.IsPrisoner
                                           && h.IsLord && !string.IsNullOrEmpty(h.StringId))
                               .Take(12)
                               .ToList();

         if (pool.Count < 4) return "cr.seed_knowledge: not enough living lords in this save to seed.";

         var report = new List<string> {"cr.seed_knowledge — each character gets a DIFFERENT case:"};

         // 1. A SECRET love. The pack must refuse to render it however it is asked: the mystery-lover pillar
         //    keeps sole ownership of when a secret surfaces.
         report.Add(GiveLover(behavior, pool[0], pool[1], true, "a SECRET love (must NEVER be named)"));

         // 2. An openly held lover. Guarded rather than secret, so it surfaces only in private and only to
         //    somebody they think well of - the discretion axis, which needs both cases to be visible at all.
         report.Add(GiveLover(behavior, pool[2], pool[3], false, "an OPEN love (only in private, regard 30+)"));

         // 3. A grievance against a third party. Open: it may be spoken to anyone, and it is the half that
         //    stops the secret test passing for the wrong reason (a character who names nobody).
         report.Add(GiveGrudge(pool[4 % pool.Count], pool[5 % pool.Count], today, "an OPEN grievance"));

         // 4. Both at once, to see the cap and the ordering rather than one bond in isolation.
         report.Add(GiveLover(behavior, pool[6 % pool.Count], pool[7 % pool.Count], true, "a secret love AND"));
         report.Add(GiveGrudge(pool[6 % pool.Count], pool[8 % pool.Count], today, "   ...a grievance"));

         // 5 and 6. A body that changes what may be promised. Grievous first, because that is the case with a
         //    consequence: he must refuse to ride out.
         report.Add(Wound(pool[9 % pool.Count], 0.10f, "GRIEVOUSLY hurt (must refuse to ride out)"));
         report.Add(Wound(pool[10 % pool.Count], 0.60f, "knocked about (should mention it, still able)"));

         report.Add("");
         report.Add("Talk to them, or run: cr.prompt_dump <name>. Undo with: cr.seed_knowledge clear");

         return string.Join("\n", report);
      }

      private static string Unseed(NpcMemoryCampaignBehavior behavior)
      {
         if (SeededHeroes.Count == 0)
            return "cr.seed_knowledge: nothing seeded this session. A seeded grudge is marked "
                   + SeedMark + " in the log if you need to find one after a reload.";

         var undone = 0;

         foreach (KeyValuePair<string, int> entry in SeededHeroes)
         {
            Hero hero = Hero.FindFirst(h => h != null && h.StringId == entry.Key);
            if (hero == null) continue;

            GrudgeBehavior.Instance?.ClearGrudgesFor(entry.Key);

            NpcProfile profile = behavior.Store.Get(entry.Key);

            if (profile?.Romantic != null)
            {
               profile.Romantic.TakenLoverId = null;
               profile.Romantic.LoverIsSecret = false;
               behavior.Store.Set(profile);
            }

            // Their hit points are put back where they were rather than to full: healing somebody the player
            // had actually wounded would be a gift nobody asked for.
            if (entry.Value > 0) hero.HitPoints = Math.Min(hero.MaxHitPoints, entry.Value);

            undone++;
         }

         SeededHeroes.Clear();

         return $"cr.seed_knowledge: undone for {undone} character(s).";
      }

      private static string GiveLover(NpcMemoryCampaignBehavior behavior, Hero hero, Hero lover, bool secret,
                                      string what)
      {
         NpcProfile profile = behavior.Store.Get(hero.StringId) ?? HeroProfileMapper.FromHero(hero);

         profile.Romantic ??= new RomanticProfile {IsFemale = hero.IsFemale};
         profile.Romantic.TakenLoverId = lover.StringId;
         profile.Romantic.LoverIsSecret = secret;
         behavior.Store.Set(profile);

         Remember(hero, 0);

         return $"  {hero.Name}: {what} — {lover.Name}";
      }

      private static string GiveGrudge(Hero holder, Hero target, int today, string what)
      {
         bool planted = GrudgeBehavior.Instance?.TryPlant(
            holder, target, GrudgeSource.FailedQuest,
            $"{SeedMark} {target.Name} shamed me before my own people.", 40, false, today, false) == true;

         if (planted) Remember(holder, 0);

         return planted
            ? $"  {holder.Name}: {what} — against {target.Name}"
            : $"  {holder.Name}: grudge could not be planted (one may already stand)";
      }

      private static string Wound(Hero hero, float share, string what)
      {
         int before = hero.HitPoints;
         hero.HitPoints = Math.Max(1, (int) (hero.MaxHitPoints * share));
         Remember(hero, before);

         return $"  {hero.Name}: {what}";
      }

      /// <summary>Records what to put back, keeping the FIRST reading if a character is seeded twice.</summary>
      private static void Remember(Hero hero, int hitPointsBefore)
      {
         if (!SeededHeroes.ContainsKey(hero.StringId)) SeededHeroes[hero.StringId] = hitPointsBefore;
         else if (SeededHeroes[hero.StringId] == 0 && hitPointsBefore > 0)
            SeededHeroes[hero.StringId] = hitPointsBefore;
      }

      #endregion
   }
}
