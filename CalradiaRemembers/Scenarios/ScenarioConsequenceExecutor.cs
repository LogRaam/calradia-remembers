// Code written by Gabriel Mailhot, 24/06/2026.
// The "law" side of a scenario: a consequence keyword is performed as a REAL, verified game action — never
// just narrated. Starter vocabulary (DECLARE_WAR, MAKE_PEACE, CHANGE_RELATION); unknown verbs are logged and
// skipped, and every call is guarded so a bad pack can never brick a campaign. The rule's Message is recorded
// separately by the runtime (into the World-Event store), so it is not a consequence here.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Scenarios;
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
         "DECLARE_WAR", "MAKE_PEACE", "CHANGE_RELATION", "CHANGE_INFLUENCE", "INJECT_MEMORY", "PRESS_FOR_WAR", "OPEN_TALE"
      };

      /// <summary>
      ///   Opens a written tale with a giver (OPEN_TALE), set by the authored quests behaviour so the scenarios never depend on
      ///   it: (tale id, giver, dry run) returns null when the tale opens (or would), else why not.
      /// </summary>
      public static Func<string, Hero, bool, string> TaleOpener { get; set; }

      /// <summary>
      ///   Whether the consequence can really apply in the world as it is now (Gabriel, 11/10/2026: a rule plays only when
      ///   all of them can, so the realm is never told of a war that was not declared). The same checks
      ///   <see cref="Execute" /> makes before acting; an unknown verb cannot apply.
      /// </summary>
      public static bool CanApply(KeywordToken consequence, ScenarioContext context)
      {
         if (consequence == null || context == null) return false;

         try
         {
            switch (consequence.Verb)
            {
               case "DECLARE_WAR":
               case "MAKE_PEACE":
                  if (!TryFactions(consequence, context, out IFaction a, out IFaction b)) return false;
                  return consequence.Verb == "DECLARE_WAR" ? !FactionManager.IsAtWarAgainstFaction(a, b) : FactionManager.IsAtWarAgainstFaction(a, b);
               case "PRESS_FOR_WAR":
                  return Pressing(consequence, context, out _, out Kingdom pressed, out IFaction pressedOn)
                         && pressed != pressedOn && !FactionManager.IsAtWarAgainstFaction(pressed, pressedOn);
               case "CHANGE_RELATION":
                  return ScenarioArgs.TryParseTargetDelta(consequence.Payload, out string who, out int delta) && delta != 0
                         && context.ResolveHero(who) is { } hero && hero != Hero.MainHero && Mapping.HeroPresence.IsInTheWorld(hero);
               case "CHANGE_INFLUENCE":
                  return ScenarioArgs.TryParseTargetDelta(consequence.Payload, out string whose, out int amount) && amount != 0
                         && context.ResolveHero(whose)?.Clan != null;
               case "OPEN_TALE":
                  return OpenTale(consequence, context, dryRun: true);
               case "INJECT_MEMORY":
                  return ScenarioArgs.TryParseTargetRest(consequence.Payload, out string target, out _)
                         && context.ResolveHero(target) is { } remembers && remembers != Hero.MainHero
                         && Mapping.HeroPresence.IsInTheWorld(remembers) && NpcMemoryCampaignBehavior.Instance?.Store != null;
               default:
                  return false;
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SCENARIO] checking consequence '{consequence.Verb}' failed: {ex.GetType().Name}: {ex.Message}");
            return false;
         }
      }

      // Two distinct factions named by the consequence's two arguments.
      private static bool TryFactions(KeywordToken c, ScenarioContext ctx, out IFaction a, out IFaction b)
      {
         a = c.Args.Count > 0 ? ctx.ResolveFaction(c.Args[0]) : null;
         b = c.Args.Count > 1 ? ctx.ResolveFaction(c.Args[1]) : null;

         return a != null && b != null && a != b;
      }

      public static void Execute(KeywordToken consequence, ScenarioContext context)
      {
         if (consequence == null || context == null) return;

         try
         {
            switch (consequence.Verb)
            {
               case "DECLARE_WAR":      DeclareWar(consequence, context); break;
               case "PRESS_FOR_WAR":    PressForWar(consequence, context); break;
               case "MAKE_PEACE":       MakePeace(consequence, context); break;
               case "CHANGE_RELATION":  ChangeRelation(consequence, context); break;
               case "CHANGE_INFLUENCE": ChangeInfluence(consequence, context); break;
               case "INJECT_MEMORY":    InjectMemory(consequence, context); break;
               case "OPEN_TALE":        OpenTale(consequence, context, dryRun: false); break;
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
      // PRESS_FOR_WAR:presser,target (the tale-scenario bridge, Gabriel 12/10/2026: "rancune et pousse son roi vers la
      // guerre"): the presser's king weighs it (WarPressPolicy) and declares on the target's realm, or does not.
      private static bool Pressing(KeywordToken c, ScenarioContext ctx, out Hero presser, out Kingdom pressed, out IFaction target)
      {
         presser = c.Args.Count > 0 ? ctx.ResolveHero(c.Args[0]) : null;
         pressed = presser?.MapFaction as Kingdom;
         target = c.Args.Count > 1 ? ctx.ResolveFaction(c.Args[1]) : null;

         return presser != null && Mapping.HeroPresence.IsInTheWorld(presser) && pressed != null && !pressed.IsEliminated
                && pressed.Leader != null && target != null;
      }

      private static void PressForWar(KeywordToken c, ScenarioContext ctx)
      {
         if (!Pressing(c, ctx, out Hero presser, out Kingdom pressed, out IFaction target)) return;
         if (pressed == target || FactionManager.IsAtWarAgainstFaction(pressed, target)) return;

         Hero king = pressed.Leader;
         var facts = new WarPressFacts {
            PresserRules = king == presser, KingRegardForPresser = king.GetRelation(presser),
            KingOpenWars = System.Linq.Enumerable.Count(Kingdom.All, k => k != pressed && !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(pressed, k)),
            StrengthRatio = target.CurrentTotalStrength > 0 ? pressed.CurrentTotalStrength / target.CurrentTotalStrength : 10.0
         };
         bool declares = WarPressPolicy.KingDeclares(facts);
         ModLog.Info($"[SCENARIO] PRESS_FOR_WAR: {presser.Name} presses {king.Name} against {target.Name} (regard {facts.KingRegardForPresser}, "
                     + $"wars {facts.KingOpenWars}, strength {facts.StrengthRatio:0.00}): {(declares ? "war" : "declined")}.");
         if (declares) DeclareWarAction.ApplyByDefault(pressed, target);
      }

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

      // OPEN_TALE:tale_id,Giver (Gabriel, 24/10/2026: a scenario opens the night Rhagaea sends for her prisoner): the tale's
      // popup opens with that giver, when the tale's own rules allow it.
      private static bool OpenTale(KeywordToken c, ScenarioContext ctx, bool dryRun)
      {
         string taleId = c.Args.Count > 0 ? c.Args[0] : null;
         Hero giver = c.Args.Count > 1 ? ctx.ResolveHero(c.Args[1]) : null;
         if (string.IsNullOrWhiteSpace(taleId) || giver == null || TaleOpener == null) return false;

         string whyNot = TaleOpener(taleId.Trim(), giver, dryRun);
         if (whyNot != null && !dryRun) ModLog.Info($"[SCENARIO] OPEN_TALE '{taleId}' with {giver.Name}: not opened, {whyNot}.");
         if (whyNot == null && !dryRun) ModLog.Info($"[SCENARIO] OPEN_TALE '{taleId}' with {giver.Name}.");

         return whyNot == null;
      }

      // INJECT_MEMORY:Ref:text: record a memory on an NPC's profile so they reference it in conversation (the "Calradia
      // remembers" channel). 11/10/2026: the profile is created when CR holds none yet, as CrNpcMemory.AddMemory does. A
      // rule now plays only when all its consequences can apply, so a missing profile would have silently cancelled
      // every Rhagaea marriage outcome for a player CR had never recorded her with.
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
         if (store == null) return;
         NpcProfile profile = store.Get(hero.StringId) ?? Mapping.HeroProfileMapper.FromHero(hero);

         profile.Events.Add(new NotableEvent((int) CampaignTime.Now.ToDays, NotableEventType.Other, text));
         store.Set(profile);
         ModLog.Info($"[SCENARIO] INJECT_MEMORY: {hero.Name} now remembers \"{text}\".");
      }
   }
}
