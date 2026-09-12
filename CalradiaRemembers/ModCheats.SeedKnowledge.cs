// Code written by Gabriel Mailhot, 12/09/2026.
// A test bed for the knowledge packs, and then the run that uses it.
//
// The live sweep reported personal_bonds at 0 supplied out of 60 carried, and the reason turned out to be the
// world rather than the code: bonds come from exactly two places - a LIVE GRUDGE against somebody other than
// the player, and a TAKEN LOVER - and Gabriel's campaign had neither. His answer: "ma partie n'a pas de liens
// intimes. Tu peux en injecter. Et tu peux injecter différents scénarios à différents NPC pour tester tous les
// cas." Then, immediately and rightly: "est-ce que tu peux automatiser les tests... plutôt que j'aie à avoir
// une conversation en jeu avec eux."
//
// So each seeded character gets a DIFFERENT situation, and one command can then put the matching question to
// every one of them. Seeding without asking is half a tool.
//
// WHY THE ASKING MATTERS MORE THAN THE CONVENIENCE: cr.memory_bench hands a character facts assembled by hand,
// which proves the PACK behaves. This asks a REAL character in a REAL save, with the context the game itself
// builds - so it proves the host read the world, the packs composed, and the model then answered. The whole
// road, which a bench with hand-made facts can never reach.
//
// Everything it touches is reversible and marked.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>The marker every seeded grudge carries, so a leftover is recognisable long afterwards.</summary>
      private const string SeedMark = "[SEED]";

      /// <summary>What was seeded this session: what to ask about, and what to put back.</summary>
      private static readonly List<SeededCase> Seeded = new();

      /// <summary>
      ///   cr.seed_knowledge [test|clear] — gives several characters DIFFERENT situations so every knowledge
      ///   pack has something to show. `test` then puts the matching question to each of them against the real
      ///   model and reports; `clear` undoes it all.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("seed_knowledge", "cr")]
      public static string SeedKnowledge(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         string mode = args is {Count: > 0} ? args[0].Trim().ToLowerInvariant() : "";

         try
         {
            switch (mode)
            {
               case "clear": return Unseed(behavior);
               case "test":
                  // Seeding first when nothing is seeded makes the one-word command do the whole job, which is
                  // what was actually asked for.
                  if (Seeded.Count == 0) Seed(behavior);

                  return MemoryBench.RunSeeded(behavior, Seeded);
               default: return Seed(behavior);
            }
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

         if (pool.Count < 6) return "cr.seed_knowledge: not enough living lords in this save to seed.";

         Seeded.Clear();

         var report = new List<string> {"cr.seed_knowledge — each character gets a DIFFERENT case:"};

         // The two ends of the discretion axis. Neither shows anything on its own: a secret that is never
         // named proves nothing unless an open bond, elsewhere, IS named.
         report.Add(Lover(behavior, pool[0], pool[1], SeededKind.SecretLove, "a SECRET love (must never be named)"));
         report.Add(Lover(behavior, pool[2], pool[3], SeededKind.OpenLove, "an OPEN love (his to speak of in private)"));

         // Ordinary talk, and the half that stops the secret probe passing for the wrong reason - a character
         // who mentions nobody at all would otherwise sail through every "does not name X".
         report.Add(Grudge(pool[4], pool[5], today, "an OPEN grievance"));

         // A body that changes what may be PROMISED, which is the anti-empty-promise rule reaching a fact it
         // never used to have.
         report.Add(Wound(pool[6 % pool.Count], 0.10f, SeededKind.GrievousWound, "GRIEVOUSLY hurt (must refuse to ride out)"));
         report.Add(Wound(pool[7 % pool.Count], 0.60f, SeededKind.LightWound, "knocked about (should say so, still able)"));

         report.Add("");
         report.Add("Now run: cr.seed_knowledge test   (asks each of them, judges the answers)");
         report.Add("Or look:  cr.prompt_dump <name>    Undo: cr.seed_knowledge clear");

         return string.Join("\n", report);
      }

      private static string Unseed(NpcMemoryCampaignBehavior behavior)
      {
         if (Seeded.Count == 0)
            return "cr.seed_knowledge: nothing seeded this session. A seeded grudge is marked " + SeedMark
                   + " in its cause if you need to find one after a reload.";

         var undone = 0;

         foreach (SeededCase seeded in Seeded)
         {
            Hero hero = seeded.Hero;
            if (hero == null) continue;

            GrudgeBehavior.Instance?.ClearGrudgesFor(hero.StringId);

            NpcProfile profile = behavior.Store.Get(hero.StringId);

            if (profile?.Romantic != null)
            {
               profile.Romantic.TakenLoverId = null;
               profile.Romantic.LoverIsSecret = false;
               behavior.Store.Set(profile);
            }

            // Put their hit points back WHERE THEY WERE rather than to full: healing somebody the player had
            // actually wounded would be a gift nobody asked for.
            if (seeded.HitPointsBefore > 0)
               hero.HitPoints = Math.Min(hero.MaxHitPoints, seeded.HitPointsBefore);

            undone++;
         }

         Seeded.Clear();

         return $"cr.seed_knowledge: undone for {undone} character(s).";
      }

      private static string Lover(NpcMemoryCampaignBehavior behavior, Hero hero, Hero lover, SeededKind kind,
                                  string what)
      {
         NpcProfile profile = behavior.Store.Get(hero.StringId) ?? HeroProfileMapper.FromHero(hero);

         profile.Romantic ??= new RomanticProfile {IsFemale = hero.IsFemale};
         profile.Romantic.TakenLoverId = lover.StringId;
         profile.Romantic.LoverIsSecret = kind == SeededKind.SecretLove;
         behavior.Store.Set(profile);

         Seeded.Add(new SeededCase {Hero = hero, Kind = kind, CounterpartName = lover.Name?.ToString()});

         return $"  {hero.Name}: {what} — {lover.Name}";
      }

      private static string Grudge(Hero holder, Hero target, int today, string what)
      {
         bool planted = GrudgeBehavior.Instance?.TryPlant(
            holder, target, GrudgeSource.FailedQuest,
            $"{SeedMark} {target.Name} shamed me before my own people.", 40, false, today, false) == true;

         if (planted)
            Seeded.Add(new SeededCase {
               Hero = holder, Kind = SeededKind.OpenGrievance, CounterpartName = target.Name?.ToString()
            });

         return planted
            ? $"  {holder.Name}: {what} — against {target.Name}"
            : $"  {holder.Name}: grudge could not be planted (one may already stand)";
      }

      private static string Wound(Hero hero, float share, SeededKind kind, string what)
      {
         int before = hero.HitPoints;
         hero.HitPoints = Math.Max(1, (int) (hero.MaxHitPoints * share));

         Seeded.Add(new SeededCase {Hero = hero, Kind = kind, HitPointsBefore = before});

         return $"  {hero.Name}: {what}";
      }

      #endregion
   }
}
