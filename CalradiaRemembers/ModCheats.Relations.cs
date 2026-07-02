// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Stance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      [CommandLineFunctionality.CommandLineArgumentFunction("relation", "cr")]
      public static string SimulateRelation(List<string> args)
         => SimulateRelation(args, NotableEventType.Other, "cr.relation <delta> [name]");

      [CommandLineFunctionality.CommandLineArgumentFunction("relation_intimate", "cr")]
      public static string SimulateRelationIntimate(List<string> args)
         => SimulateRelation(args, NotableEventType.Intimacy, "cr.relation_intimate <delta> [name]");

      [CommandLineFunctionality.CommandLineArgumentFunction("relation_reset", "cr")]
      public static string ResetRelationCooldown(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         profile.LastRelationGainHour = null;
         behavior.Store.Set(profile);

         return $"{hero.Name}: relation-gain cooldown cleared. Next positive gain will land immediately.";
      }

      /// <summary>
      ///   cr.rep_set &lt;value&gt; [name]
      ///   Directly sets ReputationWithPlayer to the given value, bypassing RelationGate.
      ///   Use for testing scenarios that require a specific trust level (e.g. +100 to
      ///   verify that a trusted NPC accepts a private audience request).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("rep_set", "cr")]
      public static string SetReputation(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (args.Count == 0 || !int.TryParse(args[0], out int value))
            return "Usage: cr.rep_set <value> [name]  (e.g. cr.rep_set 100 Alympia)";

         List<string> nameArgs = args.Count > 1
            ? args.GetRange(1, args.Count - 1)
            : new List<string>();
         Hero hero = ResolveHero(nameArgs);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int previous = profile.ReputationWithPlayer;
         profile.ReputationWithPlayer = value;
         behavior.Store.Set(profile);

         ModLog.Info($"[cr.rep_set] {hero.Name}: rep {previous} → {value} (gate bypassed).");

         return $"{hero.Name}: ReputationWithPlayer set to {value} (was {previous}).";
      }

      [CommandLineFunctionality.CommandLineArgumentFunction("give_gold", "cr")]
      public static string GiveGold(List<string> args)
         => SimulateGold("give_gold", args, "cr.give_gold <amount> [name]");

      [CommandLineFunctionality.CommandLineArgumentFunction("take_gold", "cr")]
      public static string TakeGold(List<string> args)
         => SimulateGold("take_gold", args, "cr.take_gold <amount> [name]");

      [CommandLineFunctionality.CommandLineArgumentFunction("info", "cr")]
      public static string Info(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         double now = CampaignTime.Now.ToHours;
         bool captive = behavior.GameStateBridge.GetPlayerStatus(hero) == PlayerStatusVsNpc.NpcIsCaptive;

         double cooldownHours = RelationGate.CooldownHoursFor(ModSettings.RelationWarmth);
         string lastGain = profile.LastRelationGainHour.HasValue
            ? $"{profile.LastRelationGainHour.Value:F1}h"
            : "never";
         string cooldown = profile.LastRelationGainHour.HasValue
            ? $"{Math.Max(0.0, cooldownHours - (now - profile.LastRelationGainHour.Value)):F1}h remaining"
            : "ready";

         return $"{hero.Name}: relation={behavior.GameStateBridge.GetPlayerRelation(hero)}, " + $"captive={captive}, pacing={ModSettings.RelationWarmth}, lastGain={lastGain}, cooldown={cooldown} (now {now:F1}h).";
      }

      /// <summary>
      ///   cr.standing
      ///   Shows the player's clan tier and arena rank/wins — the two standing metrics
      ///   that drive NPC social-attraction instructions in the prompt.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("standing", "cr")]
      public static string ShowStanding(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         var player = Hero.MainHero;

         if (player == null) return "Player hero not available.";

         int clanTier = player.Clan?.Tier ?? 0;
         string clanName = player.Clan?.Name?.ToString() ?? "no clan";

         var arenaRank = 0;
         var arenaWins = 0;
         try
         {
            ITournamentManager tm = Campaign.Current?.TournamentManager;
            if (tm != null)
            {
               arenaRank = tm.GetLeaderBoardRank(player);
               List<KeyValuePair<Hero, int>> lb = tm.GetLeaderboard();
               foreach (KeyValuePair<Hero, int> kvp in lb)
                  if (kvp.Key == player)
                  {
                     arenaWins = kvp.Value;

                     break;
                  }
            }
         }
         catch
         {
            /* TournamentManager not yet initialised — leave at 0 */
         }

         string rankStr = arenaRank > 0
            ? $"#{arenaRank} ({arenaWins} win(s))"
            : "unranked (0 tournament wins)";
         string attractionNote = clanTier >= 3
            ? " [marriage interest threshold MET]"
            : " [marriage interest threshold NOT met (need tier 3+)]";
         string arenaNote = arenaRank is > 0 and <= 10
            ? " [affair interest threshold MET]"
            : " [affair interest threshold NOT met (need rank 1–10)]";

         return $"Player standing:\n" + $"  Clan: {clanName} — tier {clanTier}{attractionNote}\n" + $"  Arena rank: {rankStr}{arenaNote}";
      }

      /// <summary>
      ///   cr.bargain [name] — reports whether an NPC may offer the female player the intimacy-as-currency bargain (Phase
      ///   3).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bargain", "cr")]
      public static string ShowBargain(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         bool eligible = IntimacyBargainEligibility.IsEligible(hero);
         bool leverage = behavior.Store.Get(hero.StringId)?.IntimacyLeverageHeld == true;

         return $"{hero.Name} — intimacy bargain eligible: {eligible} " + "(needs a female player; this man unrelated, dishonourable + cynical/callous; Adult ≥ Explicit). " + $"Leverage held: {leverage}.";
      }

      /// <summary>cr.bargain_leverage [name] — marks an NPC as already holding leverage, to test the escalation prompt.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bargain_leverage", "cr")]
      public static string SetBargainLeverage(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile p = GetOrCreateProfile(behavior, hero);
         p.IntimacyLeverageHeld = true;
         behavior.Store.Set(p);

         return $"{hero.Name} now holds leverage — open a chat (female player, Adult ≥ Explicit) to see them press it.";
      }

      /// <summary>
      ///   cr.discover_test [name]
      ///   Injects 3 PLACEHOLDER discoveries into the NPC's profile so the encyclopedia
      ///   discovery section can be validated without playing through a conversation.
      ///   These are fixed test strings (gender-adapted), NOT real observations about
      ///   the character — real discoveries are made in conversation. Renamed from
      ///   cr.discover after a tester ran it on three NPCs, got identical "She…" lines
      ///   on King Derthert, and reasonably concluded the discovery system was broken.
      ///   Safe to call repeatedly — deduplicated by key.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("discover_test", "cr")]
      public static string SimulateDiscover(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         var added = 0;
         var day = (int) CampaignTime.Now.ToDays;
         string subj = hero.IsFemale
            ? "She"
            : "He";
         string poss = hero.IsFemale
            ? "her"
            : "his";
         string other = hero.IsFemale
            ? "men"
            : "women";
         DiscoveredTrait[] seeds = {
            new() {Key = "orientation", Description = $"{subj} seems drawn to {other}. (test seed)", GameDay = day},
            new() {Key = "archetype", Description = $"{subj} moves with the quiet confidence of someone who knows {poss} worth. (test seed)", GameDay = day},
            new() {Key = "preference_dominant", Description = $"{subj} prefers to lead in {poss} relationships. (test seed)", GameDay = day}
         };

         foreach (DiscoveredTrait seed in seeds)
         {
            bool exists = profile.DiscoveredTraits.Exists(t =>
               string.Equals(t.Key, seed.Key, StringComparison.OrdinalIgnoreCase));

            if (exists) continue;
            profile.DiscoveredTraits.Add(seed);
            added++;
         }

         behavior.Store.Set(profile);

         return $"{hero.Name}: {added} TEST discovery/ies added ({profile.DiscoveredTraits.Count} total). " + "These are placeholders for UI testing — real discoveries happen in conversation. " + "Open encyclopedia to verify.";
      }

      /// <summary>
      ///   cr.stance [name]
      ///   Shows an NPC's posture toward the player (Affection/Trust/Respect/Fear) and the prompt block.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("stance", "cr")]
      public static string ShowStance(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile p = behavior.Store.Get(hero.StringId);

         if (p == null) return $"{hero.Name} has no profile yet (you have never spoken).";

         string block = StanceService.Describe(p) ?? "(wholly neutral)";

         return $"{hero.Name} — Affection {p.ReputationWithPlayer}, Trust {p.StanceTrust}, Respect {p.StanceRespect}, Fear {p.StanceFear}\n{block}";
      }

      /// <summary>
      ///   cr.stance_set &lt;trust&gt; &lt;respect&gt; &lt;fear&gt; [name]
      ///   Sets the Trust/Respect/Fear axes directly (Affection stays on the reputation path) to test
      ///   the posture block in conversation. Values are clamped to each axis's range.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("stance_set", "cr")]
      public static string SetStance(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         if (args == null || args.Count < 3 || !int.TryParse(args[0], out int t) || !int.TryParse(args[1], out int r) || !int.TryParse(args[2], out int f))
            return "Usage: cr.stance_set <trust> <respect> <fear> [name]";

         string nameFragment = args.Count > 3
            ? string.Join(" ", args.GetRange(3, args.Count - 3)).Trim()
            : null;
         Hero hero = !string.IsNullOrWhiteSpace(nameFragment)
            ? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
            : Hero.OneToOneConversationHero;

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile p = GetOrCreateProfile(behavior, hero);
         p.StanceTrust = StanceGate.ClampAxis(StanceAxis.Trust, t);
         p.StanceRespect = StanceGate.ClampAxis(StanceAxis.Respect, r);
         p.StanceFear = StanceGate.ClampAxis(StanceAxis.Fear, f);
         behavior.Store.Set(p);

         string block = StanceService.Describe(p) ?? "(wholly neutral)";

         return $"{hero.Name} stance set — Trust {p.StanceTrust}, Respect {p.StanceRespect}, Fear {p.StanceFear}\n{block}";
      }

      private static string SimulateGold(string type, List<string> args, string usage)
      {
         if (args == null || args.Count == 0 || !int.TryParse(args[0], out int amount))
            return $"Usage: {usage}";

         var action = new GameAction {
            Type = type,
            Parameters = new Dictionary<string, string> {{"amount", amount.ToString()}}
         };

         return Dispatch(action, args.Skip(1), NotableEventType.Other, $"{type} {amount}");
      }

      private static string SimulateRelation(List<string> args, NotableEventType emittedEvent, string usage)
      {
         if (args == null || args.Count == 0 || !int.TryParse(args[0], out int delta))
            return $"Usage: {usage}";

         var action = new GameAction {
            Type = "change_relation",
            Parameters = new Dictionary<string, string> {{"delta", delta.ToString()}}
         };

         return Dispatch(action, args.Skip(1), emittedEvent, $"proposed {delta:+#;-#;0}");
      }

      /// <summary>
      ///   Runs an action through the live bridge against the resolved hero,
      ///   persists any cooldown stamp, mirrors the outcome into an open chat
      ///   window, and reports the before/after relation.
      /// </summary>
      private static string Dispatch(GameAction action,
                                     IEnumerable<string> nameTokens,
                                     NotableEventType emittedEvent,
                                     string requested)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(nameTokens);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         try
         {
            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            int before = behavior.GameStateBridge.GetPlayerRelation(hero);

            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
               action, hero, new ActionContext(profile, emittedEvent));

            behavior.Store.Set(profile); // persist LastRelationGainHour if stamped
            if (outcome != null)
               ChatManager.ActiveViewModel?.AppendSystemMessage(outcome.Message, outcome.IsPositive);

            int after = behavior.GameStateBridge.GetPlayerRelation(hero);
            string result = outcome?.Message ?? "gated to 0 (cooldown / clamp / invalid)";

            return $"{hero.Name}: {requested} → {result} | relation {before}→{after}.";
         }
         catch (Exception ex)
         {
            ModLog.Error($"cr action dispatch failed: {ex.GetType().Name}: {ex.Message}");

            return $"Failed: {ex.Message}";
         }
      }
   }
}
