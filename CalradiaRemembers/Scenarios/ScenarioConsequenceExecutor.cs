// Code written by Gabriel Mailhot, 24/06/2026.
// The "law" side of a scenario: a consequence keyword is performed as a REAL, verified game action — never
// just narrated. Starter vocabulary (DECLARE_WAR, MAKE_PEACE, CHANGE_RELATION); unknown verbs are logged and
// skipped, and every call is guarded so a bad pack can never brick a campaign. The rule's Message is recorded
// separately by the runtime (into the World-Event store), so it is not a consequence here.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Scenarios
{
   /// <summary>Applies one scenario consequence keyword as a real game mutation.</summary>
   public static class ScenarioConsequenceExecutor
   {
      /// <summary>The consequences <see cref="Execute" /> performs; any other is skipped (checked by cr.testall scenario_packs).</summary>
      public static readonly IReadOnlyCollection<string> KnownVerbs = new[]
      {
         "DECLARE_WAR", "MAKE_PEACE", "CHANGE_RELATION", "CHANGE_INFLUENCE", "INJECT_MEMORY"
      };

      public static void Execute(KeywordToken consequence, ScenarioContext context)
      {
         if (consequence == null || context == null) return;

         try
         {
            switch (consequence.Verb)
            {
               case "DECLARE_WAR":      DeclareWar(consequence, context); break;
               case "MAKE_PEACE":       MakePeace(consequence, context); break;
               case "CHANGE_RELATION":  ChangeRelation(consequence, context); break;
               case "CHANGE_INFLUENCE": ChangeInfluence(consequence, context); break;
               case "INJECT_MEMORY":    InjectMemory(consequence, context); break;
               default:
                  ModLog.Info($"[SCENARIO] unknown consequence '{consequence.Verb}' — skipped.");
                  break;
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SCENARIO] consequence '{consequence.Verb}' failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      // DECLARE_WAR:FactionA,FactionB — start a war between two factions (no-op if already at war).
      private static void DeclareWar(KeywordToken c, ScenarioContext ctx)
      {
         if (c.Args.Count < 2) { ModLog.Info("[SCENARIO] DECLARE_WAR needs two factions."); return; }

         IFaction a = ctx.ResolveFaction(c.Args[0]);
         IFaction b = ctx.ResolveFaction(c.Args[1]);
         if (a == null || b == null) { ModLog.Info($"[SCENARIO] DECLARE_WAR could not resolve '{c.Args[0]}' / '{c.Args[1]}'."); return; }
         if (a == b || FactionManager.IsAtWarAgainstFaction(a, b)) return;

         DeclareWarAction.ApplyByDefault(a, b);
         ModLog.Info($"[SCENARIO] DECLARE_WAR: {a.Name} vs {b.Name}.");
      }

      // MAKE_PEACE:FactionA,FactionB — end a war between two factions (no-op if not at war).
      private static void MakePeace(KeywordToken c, ScenarioContext ctx)
      {
         if (c.Args.Count < 2) { ModLog.Info("[SCENARIO] MAKE_PEACE needs two factions."); return; }

         IFaction a = ctx.ResolveFaction(c.Args[0]);
         IFaction b = ctx.ResolveFaction(c.Args[1]);
         if (a == null || b == null || a == b) return;
         if (!FactionManager.IsAtWarAgainstFaction(a, b)) return;

         MakePeaceAction.Apply(a, b);
         ModLog.Info($"[SCENARIO] MAKE_PEACE: {a.Name} & {b.Name}.");
      }

      // CHANGE_RELATION:HeroRef:+N — shift the player's relation with a hero (HeroRef may be a name or id).
      private static void ChangeRelation(KeywordToken c, ScenarioContext ctx)
      {
         if (!ScenarioArgs.TryParseTargetDelta(c.Payload, out string target, out int delta))
         {
            ModLog.Info($"[SCENARIO] CHANGE_RELATION bad payload '{c.Payload}' (want HeroRef:+N).");
            return;
         }

         Hero hero = ctx.ResolveHero(target);
         if (hero == null || hero == Hero.MainHero || delta == 0) return;

         ChangeRelationAction.ApplyPlayerRelation(hero, delta);
         ModLog.Info($"[SCENARIO] CHANGE_RELATION: player {(delta >= 0 ? "+" : "")}{delta} with {hero.Name}.");
      }

      // CHANGE_INFLUENCE:Ref:+N — change the clan influence of a hero's clan (Ref = PLAYER or a hero name/id).
      private static void ChangeInfluence(KeywordToken c, ScenarioContext ctx)
      {
         if (!ScenarioArgs.TryParseTargetDelta(c.Payload, out string target, out int delta))
         {
            ModLog.Info($"[SCENARIO] CHANGE_INFLUENCE bad payload '{c.Payload}' (want Ref:+N).");
            return;
         }

         Clan clan = ctx.ResolveHero(target)?.Clan;
         if (clan == null || delta == 0) return;

         ChangeClanInfluenceAction.Apply(clan, delta);
         ModLog.Info($"[SCENARIO] CHANGE_INFLUENCE: {clan.Name} {(delta >= 0 ? "+" : "")}{delta}.");
      }

      // INJECT_MEMORY:Ref:text — record a memory on an NPC's profile so they reference it in conversation
      // (the "Calradia remembers" channel). Only NPCs the player has actually met (a profile exists).
      private static void InjectMemory(KeywordToken c, ScenarioContext ctx)
      {
         if (!ScenarioArgs.TryParseTargetRest(c.Payload, out string target, out string text))
         {
            ModLog.Info($"[SCENARIO] INJECT_MEMORY bad payload '{c.Payload}' (want Ref:text).");
            return;
         }

         Hero hero = ctx.ResolveHero(target);
         if (hero == null || hero == Hero.MainHero) return;

         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(hero.StringId);
         if (profile == null) return; // not yet met — nothing to remember on

         profile.Events.Add(new NotableEvent((int) CampaignTime.Now.ToDays, NotableEventType.Other, text));
         store.Set(profile);
         ModLog.Info($"[SCENARIO] INJECT_MEMORY: {hero.Name} now remembers \"{text}\".");
      }
   }
}
