// Code written by Gabriel Mailhot, 24/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Chronicle;
using CalradiaRemembers.Commoners;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Dynasty;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Lore;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Scenarios;
using CalradiaRemembers.Services;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Witnesses;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   Exposes diagnostic commands to the Bannerlord cheat console.
   ///   Open the console with Alt + ~ (must be enabled in launcher config),
   ///   then run any of:
   ///   campaign.test_llm     — run a synchronous LLM diagnostic
   ///   campaign.open_chat    — open the Calradia Remembers chat window (Sprint 7.6.A)
   ///   Sprint 10 — action testing without recreating a scene (group "cr").
   ///   These exercise the REAL pipeline (RelationGate → bridge.ExecuteAction →
   ///   ApplyPlayerRelation/GiveGoldAction), not a reimplementation:
   ///   cr.relation &lt;delta&gt; [name]            — simulate a change_relation action
   ///   cr.relation_intimate &lt;delta&gt; [name]   — same, with an intimacy event (cap +3, bypasses cooldown)
   ///   cr.give_gold &lt;amount&gt; [name]          — NPC gives the player gold
   ///   cr.take_gold &lt;amount&gt; [name]          — player gives the NPC gold
   ///   cr.info [name]                          — relation, cooldown state, captive status
   ///   cr.relation_reset [name]                — clear the relation-gain cooldown stamp
   ///   cr.discover_test [name]                 — inject 3 placeholder discoveries (Sprint 10.5 encyclopedia test)
   ///   [name] is an optional name fragment; if omitted, the current conversation NPC is used.
   /// </summary>
   public static class ModCheats
   {
      // ── Sprint 17: kink testing ───────────────────────────────────────────

      /// <summary>
      ///   cr.add_kink &lt;kink&gt; [name]
      ///   Adds a kink to the NPC's romantic profile so scene variety can be tested.
      ///   Apply BEFORE opening a captive scene (cr.crc_scene re-reads the profile on open).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("add_kink", "cr")]
      public static string AddKink(List<string> args)
         => MutateKink(args, true, "cr.add_kink <kink> [name]");

      /// <summary>
      ///   cr.memory_add &lt;days_ago&gt; &lt;text&gt; [name]
      ///   Creates a new memory event dated &lt;days_ago&gt; days in the past, so a player
      ///   can construct a prior relationship with a character (tester request: "not
      ///   everyone should be a stranger"). The last token is tried as an NPC name
      ///   fragment, same as cr.memory_set.
      ///   Example: cr.memory_add 30 We fought side by side at Pendraic. Derthert
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("memory_add", "cr")]
      public static string AddMemoryEvent(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (args == null || args.Count < 2 || !int.TryParse(args[0], out int daysAgo) || daysAgo < 0)
            return "Usage: cr.memory_add <days_ago> <text> [name]\n" + "       Example: cr.memory_add 30 We fought side by side at Pendraic. Derthert\n" + "       <days_ago> dates the memory in the past (0 = today).";

         // Try the last token as an NPC name fragment. If it resolves, strip it.
         Hero hero = null;
         int summaryEnd = args.Count; // exclusive
         if (args.Count >= 3)
         {
            string lastToken = args[args.Count - 1];
            Hero candidate = Hero.AllAliveHeroes.FirstOrDefault(h =>
               h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(lastToken, StringComparison.OrdinalIgnoreCase) >= 0);
            if (candidate != null)
            {
               hero = candidate;
               summaryEnd = args.Count - 1;
            }
         }

         if (hero == null)
            hero = Hero.OneToOneConversationHero;

         if (hero == null)
            return "No NPC found. Pass a name fragment as the last argument or start a conversation first.";

         string summary = string.Join(" ", args.Skip(1).Take(summaryEnd - 1)).Trim();

         if (string.IsNullOrWhiteSpace(summary))
            return "The memory text cannot be empty.";

         int gameDay = Math.Max(1, (int) CampaignTime.Now.ToDays - daysAgo);
         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         profile.Events.Add(new NotableEvent(gameDay, NotableEventType.Other, summary));
         // Keep chronology readable for both cr.memory and the prompt.
         profile.Events.Sort((a, b) => a.gameDay.CompareTo(b.gameDay));
         behavior.Store.Set(profile);

         return $"{hero.Name}: memory added ({DescribeDay(gameDay)}) — \"{summary}\".\n" + $"{profile.Events.Count} event(s) total. Remember: profiles persist when you SAVE the game.";
      }

      /// <summary>
      ///   cr.witness [witnessName]
      ///   Adds a named witness to the currently open chat session. Primary test path
      ///   for the multi-NPC feature — validates prompt injection, header UI, and the
      ///   request_privacy round-trip without needing a real settlement context.
      ///   The chat window must be open first (e.g. via cr.chat or a normal conversation).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("witness", "cr")]
      public static string AddWitness(List<string> args)
      {
         if (ChatManager.ActiveViewModel == null)
            return "No chat window is open. Open a conversation first.";

         string nameFragment = args.Count > 0
            ? string.Join(" ", args).Trim()
            : null;

         if (string.IsNullOrWhiteSpace(nameFragment))
            return "Usage: cr.witness [name]";

         Hero witnessHero = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsAlive && h.Name?.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0);

         string displayName = witnessHero?.Name?.ToString() ?? nameFragment;
         var relation = "a lord present";
         if (witnessHero != null)
         {
            int rel = witnessHero.GetRelation(Hero.MainHero);
            relation = rel >= 20 ? "a lord you regard with trust"
               : rel <= -20 ? "a rival lord"
               : "a lord you know";
         }

         var entry = new WitnessEntry {Name = displayName, RelationToNpc = relation};
         ChatManager.ActiveViewModel.AddWitness(entry);

         return $"Witness '{displayName}' ({relation}) added to chat session.";
      }

      /// <summary>
      ///   cr.scheme_advance
      ///   Advances every active scheme by one tick (no spawn) — to test the lifecycle without
      ///   waiting for daily ticks. Watch the result with cr.schemes.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme_advance", "cr")]
      public static string AdvanceSchemes(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         int before = store.Active.Count;
         store.DebugAdvance();

         return $"Advanced schemes one tick ({before} → {store.Active.Count} still active). See cr.schemes / CalradiaRemembers.log.";
      }

      /// <summary>
      ///   cr.capture [name]
      ///   Takes the given lord prisoner into your party — a shortcut so you can test the prisoner
      ///   freedom-bargain without winning a battle. Imprison them in a fief's dungeon (or keep them in
      ///   your party) and speak with them: a captive lord may then bargain for their release.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("capture", "cr")]
      public static string CaptureLord(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";
         if (hero == Hero.MainHero) return "You cannot capture yourself.";
         if (hero.IsPrisoner && hero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero)
            return $"{hero.Name} is already your prisoner.";

         TakePrisonerAction.Apply(PartyBase.MainParty, hero);

         return $"{hero.Name} is now your prisoner. Speak with them (in a fief dungeon, or test in conversation) " + $"— a captive lord may bargain for their freedom.";
      }

      // ── Sprint 12d: player letter compose ─────────────────────────────────

      /// <summary>
      ///   cr.letter_compose [name]
      ///   Opens the dedicated parchment courier window in COMPOSE mode for the given NPC — write a
      ///   fresh letter and seal it, without a tavernkeeper conversation. (Was cr.compose, in chat.)
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_compose", "cr")]
      public static string ComposeLetter(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         LetterWindowManager.OpenCompose(profile);

         return $"Parchment compose window opened for {hero.Name}.";
      }

      // ── Multi-NPC witnesses ───────────────────────────────────────────────

      /// <summary>
      ///   cr.witnesses [name]
      ///   Runs WitnessResolver.DiagnoseWitnesses for the given NPC and prints a
      ///   detailed report: CurrentSettlement, StayingInSettlement, HeroesWithoutParty
      ///   names with profile flags, party leaders inside the settlement, and
      ///   available player companions. Use this to verify which collection holds
      ///   present lords during a Talk conversation (vs Visit).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("witnesses", "cr")]
      public static string DiagnoseWitnesses(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         string report = WitnessResolver.DiagnoseWitnesses(hero, behavior.Store);

         // Console output is screen-only — mirror the report into the log so it can
         // be shared in bug reports / analyzed outside the game.
         foreach (string line in report.Split('\n'))
            ModLog.Info($"[cr.witnesses] {line.TrimEnd()}");

         return report;
      }

      // ── Sprint 12c: visible courier testing ───────────────────────────────

      /// <summary>
      ///   cr.courier [name]
      ///   Dispatches a test letter to the given NPC immediately, spawning the visible
      ///   courier party. Use to watch the courier travel the map, be intercepted, or
      ///   deliver — without going through the compose window.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("courier", "cr")]
      public static string DispatchCourier(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();
         int days = PlayerLetterService.CreateAndQueue(
            profile, "A test message carried by courier.", today, behavior.Store);

         PlayerLetter letter = profile.ReceivedPlayerLetters.LastOrDefault();
         string courier = letter?.CourierPartyId ?? "FAILED (date fallback)";

         return $"Courier dispatched to {hero.Name} (~{days}d). Party: {courier}.";
      }

      /// <summary>
      ///   cr.courier_slow [name]
      ///   Like cr.courier but spawns a SLOW, engageable courier (speed 2, does not avoid
      ///   hostiles) so bandit interception and the [COURIER] destroyed path can actually
      ///   be observed — the normal fast courier almost always outruns threats.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("courier_slow", "cr")]
      public static string DispatchSlowCourier(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         var letter = new PlayerLetter {
            RecipientId = profile.Id,
            RecipientName = profile.Name,
            Content = "A slow test courier — easy prey for bandits.",
            SentOnDay = today,
            DeliveryDay = today + 8
         };
         profile.ReceivedPlayerLetters.Add(letter);

         Hero recipient = Hero.FindFirst(h => h.StringId == profile.Id);
         bool spawned = CourierService.TrySpawnCourier(
            letter, recipient, 2.0f, false);
         behavior.Store.Set(profile);

         return spawned
            ? $"SLOW courier dispatched to {hero.Name} (party {letter.CourierPartyId}). Watch for interception."
            : $"Slow courier spawn FAILED for {hero.Name} (date fallback active).";
      }

      /// <summary>
      ///   cr.scenario_fire &lt;VERB[:payload]&gt; [heroName]
      ///   Manually fires a trigger to test scenarios without waiting for the real event. An optional hero
      ///   name becomes the event's primary hero (and their faction a context faction); the player is always
      ///   included. E.g. cr.scenario_fire ON_DEATH Rhagaea.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scenario_fire", "cr")]
      public static string FireScenario(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (args == null || args.Count == 0) return "Usage: cr.scenario_fire <VERB[:payload]> [heroName]";

         ScenarioRuntime runtime = ScenarioBehavior.Instance?.Runtime;

         if (runtime == null) return "Scenario engine not ready.";

         string verb = args[0];
         List<string> nameArgs = args.Count > 1
            ? args.GetRange(1, args.Count - 1)
            : null;
         Hero hero = nameArgs != null
            ? ResolveHero(nameArgs)
            : null;

         if (nameArgs != null && hero == null) return $"No hero matched \"{string.Join(" ", nameArgs)}\".";

         var heroes = new List<Hero>();
         if (hero != null) heroes.Add(hero);
         if (Hero.MainHero != null) heroes.Add(Hero.MainHero);

         var factions = new List<IFaction>();
         if (hero?.MapFaction != null) factions.Add(hero.MapFaction);

         var ctx = new ScenarioContext(heroes, factions);
         ScenarioRule fired = runtime.Fire(KeywordToken.Parse(verb), ctx);

         return fired != null
            ? $"Fired '{fired.Id}' on {verb}."
            : $"No rule fired for {verb}.";
      }

      /// <summary>
      ///   cr.escape_force
      ///   Makes the NEXT escape attempt in a captive scene succeed, so the full escape flow
      ///   (constrained captor reaction → freeing on scene close) can be tested despite the 10%
      ///   natural chance. Then, in the scene, type an escape attempt (e.g. "I lunge for the door
      ///   and bolt") so the LLM emits escape_attempt.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("escape_force", "cr")]
      public static string ForceEscape(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         behavior.GameStateBridge?.ForceNextEscapeSuccess();

         return "Armed: your next escape attempt in a captive scene will succeed. " + "Type an escape attempt in the scene to trigger it.";
      }

      /// <summary>
      ///   cr.intercept [name]
      ///   Forces the named lord (with a party on the map) to ride out and intercept the player,
      ///   bypassing the eligibility roll — to test the pursuit without waiting for the daily tick.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("intercept", "cr")]
      public static string ForceIntercept(List<string> args)
      {
         var behavior = InterceptionBehavior.Instance;

         if (behavior == null) return "Interception system is not active.";

         Hero lord = ResolveHero(args);

         if (lord == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         return behavior.DebugForceIntercept(lord);
      }

      /// <summary>
      ///   cr.marry [name]
      ///   Forces a native marriage between the player and the named hero so the
      ///   consequence resolver (warm/hostile family) can be tested end to end. Pair with
      ///   cr.marriage_approve / cr.marriage_revoke to test both outcomes.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marry", "cr")]
      public static string ForceMarry(List<string> args)
      {
         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";
         if (Hero.MainHero.Spouse != null) return $"You are already married to {Hero.MainHero.Spouse.Name}.";
         if (hero.Spouse != null) return $"{hero.Name} is already married to {hero.Spouse.Name}.";

         MarriageAction.Apply(Hero.MainHero, hero);

         return $"Married {hero.Name}. Check the quick-notice for the family's reaction (and cr.info on a kin).";
      }

      /// <summary>
      ///   cr.preg [name]            — the female PLAYER is the mother; [name] is the father (a lord),
      ///   else the shared hidden "Unknown" father (the faceless-bandit case).
      ///   cr.preg mother [name]     — that NPC is the mother and the player is the father (the bastard
      ///   path): used to run the in-game SPIKE on which clan the child lands in.
      ///   Forces a conception (bypasses the 15% roll) so the pregnancy → delivery path can be validated
      ///   without waiting on a real scene.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("preg", "cr")]
      public static string ForcePregnancy(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         // cr.preg mother <name> → that NPC is the mother, the player is the father.
         if (args is {Count: > 1} && string.Equals(args[0], "mother", StringComparison.OrdinalIgnoreCase))
         {
            List<string> motherArgs = args.GetRange(1, args.Count - 1);
            Hero mother = ResolveHero(motherArgs);

            if (mother == null) return $"No hero matched \"{string.Join(" ", motherArgs)}\".";

            string motherNote = PregnancyService.TryConceive(mother, Hero.MainHero, true);

            return motherNote ??
                   $"Conception did not occur — {mother.Name} must be a female of fertile age (18–45), " +
                   "not already pregnant, and past the post-birth recovery window. Check CalradiaRemembers.log.";
         }

         bool named = args is {Count: > 0};
         Hero father = named
            ? ResolveHero(args)
            : null;

         if (named && father == null) return $"No hero matched \"{string.Join(" ", args)}\".";

         string note = PregnancyService.TryConceive(father, true);

         if (note == null)
            return "Conception did not occur — the player must be a female of fertile age (18–45), " + "not already pregnant, and past the post-birth recovery window. Check CalradiaRemembers.log.";

         return note +
                (father == null
                   ? " [father: shared Unknown]"
                   : "");
      }

      /// <summary>
      ///   cr.quest_abandon [name]
      ///   Force-abandons the NPC's outstanding quest (as if the player told them they
      ///   quit), applying the broken-word penalty. Tests the abandon path without the LLM.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_abandon", "cr")]
      public static string ForceQuestAbandon(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int before = profile.ReputationWithPlayer;
         int day = behavior.GameStateBridge.GetCurrentGameDay();

         var fake = new ParsedResponse {Dialogue = "", QuestAbandoned = new QuestAbandonClaim()};
         QuestService.ApplyResponse(profile, hero, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         return $"{hero.Name}: abandon applied (regard {before}→{profile.ReputationWithPlayer}).";
      }

      /// <summary>
      ///   cr.quest_complete [name]
      ///   Simulates the NPC acknowledging the completed quest: closes the journal
      ///   entry with success and pays the promised reward. Requires cr.quest_done
      ///   first (the quest must already be in the awaiting-reward state).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_complete", "cr")]
      public static string ForceQuestComplete(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (!profile.ActiveQuests.Any(q => q.IsAwaitingReward))
            return $"{hero.Name} has no quest awaiting reward. Use cr.quest_done first.";

         int day = behavior.GameStateBridge.GetCurrentGameDay();
         var fake = new ParsedResponse {Dialogue = "", QuestCompleted = new QuestCompletionClaim()};

         QuestService.ApplyResponse(profile, hero, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         return $"{hero.Name}: quest completed — journal closed, reward paid.";
      }

      /// <summary>
      ///   cr.quest_done [name]
      ///   Force-stamps the NPC's outstanding quest as satisfied (bypassing real events)
      ///   so the giver will offer to reward it on the next exchange. Tests the
      ///   completion + reward path without fighting a battle.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_done", "cr")]
      public static string ForceQuestDone(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         InformalQuest quest = profile.ActiveQuests.FirstOrDefault(q => q.IsOutstanding);

         if (quest == null) return $"{hero.Name} has no outstanding quest. Seed one with cr.quest first.";

         int day = behavior.GameStateBridge.GetCurrentGameDay();
         quest.SatisfiedOnDay = day;
         quest.Evidence = "(debug) forced complete on day " + day;
         behavior.Store.Set(profile);

         // Update the journal mirror so "Done" appears without a real battle.
         QuestJournalBridge.NotifyDeedDone(quest, hero);

         return $"{hero.Name}: quest '{quest.Type}' marked satisfied — journal updated. " + $"Use cr.quest_complete to pay {quest.RewardGold}g/+{quest.RewardRelation} and close the journal.";
      }

      /// <summary>
      ///   cr.recruit [name]
      ///   Force-runs the join_clan action through the bridge (re-validates the gate, then moves the
      ///   lord into the player's clan). Tests the defection path without the LLM.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("recruit", "cr")]
      public static string ForceRecruit(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "join_clan"}, hero, null);

         return outcome?.Message ?? $"join_clan on {hero.Name} did nothing (see CalradiaRemembers.log).";
      }

      /// <summary>
      ///   cr.scheme [name]
      ///   Force-plots a scheme: the named hero (or the current conversation NPC) plots against their
      ///   most-hated rival lord. Use cr.scheme_advance to step it, cr.schemes to watch.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme", "cr")]
      public static string ForceScheme(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         Hero plotter = ResolveHero(args);

         if (plotter == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Hero target = Hero.AllAliveHeroes
                           .Where(h => h != plotter && h != Hero.MainHero && h.IsLord)
                           .OrderBy(h => h.GetRelation(plotter))
                           .FirstOrDefault();

         if (target == null) return $"Found no rival lord for {plotter.Name} to plot against.";

         SchemeKind kind = MBRandom.RandomInt(2) == 0
            ? SchemeKind.Slander
            : SchemeKind.Sabotage;

         return store.ForcePlot(plotter, target, kind);
      }

      /// <summary>
      ///   cr.scheme_assist [name]
      ///   Force the player to abet the named plotter's secret scheme (bypasses the LLM). The plotter
      ///   must regard you enough and the plot must be secret — set relation first with cr.relation.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme_assist", "cr")]
      public static string ForceSchemeAssist(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         Hero plotter = ResolveHero(args);

         if (plotter == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         string result = store.PlayerAssists(plotter);

         return result ?? $"{plotter.Name} has no secret plot you are eligible to abet (regard >= 20, plot still secret, you not allied with the target).";
      }

      /// <summary>
      ///   cr.scheme_warn [name]
      ///   Force the player to warn the named hero of a secret plot against them (bypasses the LLM):
      ///   exposes the scheme so the target can counter it on later ticks.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme_warn", "cr")]
      public static string ForceSchemeWarn(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         Hero target = ResolveHero(args);

         if (target == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         string result = store.TargetHeeds(target);

         return result ?? $"There is no secret plot against {target.Name} to warn of.";
      }

      /// <summary>
      ///   cr.forget_all [name]
      ///   Clears the entire memory of an NPC. Use when a session has badly corrupted
      ///   the profile and a fresh start is the cleanest fix.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("forget_all", "cr")]
      public static string ForgetAll(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int count = profile.Events.Count;
         profile.Events.Clear();
         behavior.Store.Set(profile);

         return $"{hero.Name}: all {count} memory event(s) cleared. They will start fresh from the next conversation.";
      }

      /// <summary>
      ///   cr.forget &lt;index&gt; [name]
      ///   Removes the memory event at the given 0-based index (see cr.memory).
      ///   Use this to delete a corrupted or erroneous entry so the NPC stops building on it.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("forget", "cr")]
      public static string ForgetEvent(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (args == null || args.Count == 0 || !int.TryParse(args[0], out int index))
            return "Usage: cr.forget <index> [name]  (use cr.memory to see indices)";

         List<string> nameArgs = args.Count > 1
            ? args.GetRange(1, args.Count - 1)
            : new List<string>();
         Hero hero = ResolveHero(nameArgs);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (index < 0 || index >= profile.Events.Count)
            return $"Index {index} out of range — {hero.Name} has {profile.Events.Count} event(s). Use cr.memory to see them.";

         NotableEvent removed = profile.Events[index];
         profile.Events.RemoveAt(index);
         behavior.Store.Set(profile);

         return $"{hero.Name}: removed event [{index}] (Day {removed.gameDay} | {removed.type}) — \"{removed.summary}\".\n" + $"{profile.Events.Count} event(s) remain.";
      }

      [CommandLineFunctionality.CommandLineArgumentFunction("give_gold", "cr")]
      public static string GiveGold(List<string> args)
         => SimulateGold("give_gold", args, "cr.give_gold <amount> [name]");

      /// <summary>cr.happiness_tick — runs a weekly happiness pass now (idle/loved-one pressure, drift, and any departures).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("happiness_tick", "cr")]
      public static string HappinessTick(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (CompanionHappinessBehavior.Instance == null) return "Companion-happiness system is not active.";

         CompanionHappinessBehavior.Instance.RunTick();

         return "Ran a happiness tick. Check cr.happiness [name] (a season at the leave floor parts ways).";
      }

      // ── Help ─────────────────────────────────────────────────────────────

      /// <summary>cr.help — list all cr.* commands with a short description.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("help", "cr")]
      public static string Help(List<string> args) => "Calradia Remembers console commands (group: cr)\n" + "\n" + "MEMORY EDITING\n" + "  cr.memory [name]                     List all recorded events with their index\n" + "  cr.memory_add <days_ago> <text> [name]  Create a memory dated in the past (build a prior relationship)\n" + "  cr.memory_set <index> <text> [name]  Replace the text of one event (last word = name if it matches a hero)\n" + "  cr.forget <index> [name]             Delete one event by index\n" + "  cr.forget_all [name]                 Wipe all memory events for a fresh start\n" + "\n" + "RELATION\n" + "  cr.relation <delta> [name]           Simulate change_relation (event Other) — cap +1, cooldown 8h\n" + "  cr.relation_intimate <delta> [name]  Same with Intimacy event — cap +3, bypasses cooldown\n" + "  cr.relation_reset [name]             Clear the relation-gain cooldown\n" + "  cr.rep_set <value> [name]            Force-set ReputationWithPlayer (bypasses gate — for testing)\n" + "  cr.info [name]                       Show relation, captive status, cooldown remaining\n" + "\n" + "GOLD\n" + "  cr.give_gold <amount> [name]         NPC gives the player gold\n" + "  cr.take_gold <amount> [name]         Player gives the NPC gold\n" + "\n" + "LETTERS\n" + "  cr.letter [name]                     Queue a RomanticCorrespondence test letter from the NPC (immediate delivery)\n" + "  cr.compose [name]                    Open the letter compose window to write to the NPC (Sprint 12d test path)\n" + "  cr.courier [name]                    Dispatch a test letter + spawn the visible courier party (Sprint 12c)\n" + "  cr.courier_slow [name]               Spawn a SLOW engageable courier to observe bandit interception\n" + "\n" + "MULTI-NPC WITNESSES\n" + "  cr.witnesses [name]                  Diagnose: log settlement/party presence data for an NPC (Talk-vs-Visit check)\n" + "  cr.witness [witnessName]             Add a witness to the open chat session (manual seed for testing)\n" + "\n" + "CAPTIVE ENCOUNTER (Sprint 17 — Hardcore only)\n" + "  cr.crc_scene [female|male] [solo|collective] [intent] [name]\n" + "      intent: interrogation (default) | desire | domination | torture | training | reward\n" + "      Example: cr.crc_scene female collective desire Alympia\n" + "  cr.bandit_scene [intent] [female|male]   Synthetic brigand captor scene (extortion|intimidation|revenge|spoil) — no capture needed\n" + "  cr.kinks [name]                      List the NPC's kinks and valid kink names\n" + "  cr.add_kink <kink> [name]            Add a kink (apply BEFORE opening a scene)\n" + "  cr.remove_kink <kink> [name]         Remove a kink from the NPC's profile\n" + "\n" + "PLAYER STANDING\n" + "  cr.standing                          Show player clan tier and arena rank/wins\n" + "\n" + "RECRUITMENT\n" + "  cr.hire_info [name]                  Show join_party recruitability + vanilla/floor/cap hire price\n" + "  cr.bargain_items [name]              Seed a pay-in-goods bargain (DeliverItems -> join). Then use the bag icon\n" + "\n" + "MARRIAGE (Phase A.1 engine test)\n" + "  cr.marriage_approve [name]           Seed the family's blessing to marry this hero\n" + "  cr.marriage_revoke [name]            Remove a seeded blessing\n" + "  cr.marriage_status [name]            Show blessing + basic eligibility\n" + "  cr.marry [name]                      Force a native marriage to test the family consequence\n" + "  cr.bargain_marriage [name]           Seed a marriage_consent bargain (clear bandits -> blessing). Then cr.quest_done/_complete\n" + "\n" + "DIAGNOSTICS\n" + "  cr.profile_check [name]              Round-trip the NPC's profile (serialize+read back); names the field if it fails\n" + "\n" + "DISCOVERY\n" + "  cr.discover_test [name]              Seed 3 PLACEHOLDER DiscoveredTraits (UI test only — real ones come from conversation)\n" + "\n" + "QUESTS\n" + "  cr.quest [name]                      Seed a bandit_clear quest on the NPC (journal opens)\n" + "  cr.quest_scout [name]                Seed a scout_army quest on the NPC (uses giver's first enemy)\n" + "  cr.quest_done [name]                 Force-stamp the outstanding quest as satisfied (journal updates)\n" + "  cr.quest_complete [name]             Pay reward and close the journal entry with success\n" + "  cr.quest_abandon [name]              Force-abandon the outstanding quest (-3 regard, journal closes)\n" + "  cr.quests [name]                     List the NPC's quests with their current state\n" + "\n" + "[name] is an optional name fragment (case-insensitive). If omitted, the current conversation NPC is used.";

      /// <summary>
      ///   cr.hire_info [name]
      ///   Shows whether the NPC is recruitable through conversation (join_party) and
      ///   the price band: vanilla asking price, negotiation floor (75 %) and cap (125 %).
      ///   Mirrors the exact eligibility checks the prompt gating and the bridge use.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("hire_info", "cr")]
      public static string HireInfo(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         var sb = new StringBuilder();
         sb.AppendLine($"{hero.Name}:");
         sb.AppendLine($"  Occupation      : {hero.Occupation}  (wanderer required)");
         sb.AppendLine($"  Alive/Prisoner  : {hero.IsAlive} / {hero.IsPrisoner}");
         sb.AppendLine($"  CompanionOf     : {hero.CompanionOf?.Name?.ToString() ?? "(none)"}");
         sb.AppendLine($"  PartyBelongedTo : {hero.PartyBelongedTo?.Name?.ToString() ?? "(none)"}");

         var clan = Clan.PlayerClan;
         int companions = clan?.Companions?.Count() ?? 0;
         sb.AppendLine($"  Player clan     : {companions}/{clan?.CompanionLimit ?? 0} companions");

         bool eligible = hero.IsAlive && !hero.IsPrisoner && hero.Occupation == Occupation.Wanderer && hero.CompanionOf == null && hero.PartyBelongedTo == null && clan != null && companions < clan.CompanionLimit;

         if (!eligible)
            return sb.Append("  → NOT recruitable — the RECRUITMENT prompt section is not taught.").ToString();

         try
         {
            int asking = Campaign.Current.Models.CompanionHiringPriceCalculationModel
                                 .GetCompanionHiringPrice(hero);
            sb.AppendLine($"  Asking price    : {asking} denars (vanilla model)");
            sb.AppendLine($"  Negotiation band: {(int) (asking * 0.75f)} – {(int) (asking * 1.25f)} denars");
            sb.Append("  → recruitable — RECRUITMENT section will be taught in the next conversation.");
         }
         catch (Exception ex)
         {
            sb.Append($"  Price model failed: {ex.Message}");
         }

         return sb.ToString();
      }

      /// <summary>
      ///   cr.chronicle_illuminate
      ///   Runs the optional LLM pass that rewrites the chronicle into flowing chronicler prose
      ///   (style editable in ModuleData/chronicle_style.txt). Async — reopen the viewer once done.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("chronicle_illuminate", "cr")]
      public static string IlluminateChronicle(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error))
            return error;

         return ChronicleIlluminationService.Extend();
      }

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
      ///   cr.journal_check
      ///   Diagnostic: prints how many CalradiaRemembers quests are registered in
      ///   Campaign.Current.QuestManager and lists their StringIds. Use to verify
      ///   that cr.quest actually created a QuestBase mirror in the journal.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("journal_check", "cr")]
      public static string JournalCheck(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         QuestManager qm = Campaign.Current?.QuestManager;

         if (qm == null) return "QuestManager not available.";

         List<CalradiaRemembersQuest> crQuests = qm.Quests.OfType<CalradiaRemembersQuest>().ToList();

         if (crQuests.Count == 0)
            return "No CalradiaRemembers quests in QuestManager. (OpenMirror may have failed — check the log or try cr.quest again.)";

         IEnumerable<string> lines = crQuests.Select(q =>
            $"  {q.StringId}  giver={q.QuestGiver?.Name}  isSpecialType={q.IsSpecialQuest}");

         return $"{crQuests.Count} CR quest(s) in QuestManager:\n" + string.Join("\n", lines);
      }

      /// <summary>
      ///   cr.journey_recall
      ///   Recalls EVERY away companion immediately — the conversation/console path (abandon the errand and
      ///   rejoin the party now). The safety net for the AI-Influence bug: a real homecoming, never a promise.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("journey_recall", "cr")]
      public static string JourneyRecall(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         return CompanionMissionBehavior.Instance?.RecallAll() ?? "Companion-mission system is not active.";
      }

      /// <summary>
      ///   cr.bandit_queen [solo|collective] [male|female]
      ///   Opens a captive scene with a forced FEMALE bandit chief (intent = spoil / PersonalDesire),
      ///   to test female-aggressor content (facesitting/smothering, reductive domination) without
      ///   waiting on the rare female-gang roll. Defaults: solo, and a MALE player (the classic
      ///   woman-dominates-a-man dynamic); pass 'female' for a woman-on-woman scene. The captor's
      ///   portrait is left blank rather than show a male troop face on a woman. Requires Hardcore.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bandit_queen", "cr")]
      public static string LaunchBanditQueenScene(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;
         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key first.";

         var collective = false;
         bool? playerGender = false; // default: male player (female-on-male)
         foreach (string arg in args ?? new List<string>())
            if (arg.Equals("collective", StringComparison.OrdinalIgnoreCase)) collective = true;
            else if (arg.Equals("solo", StringComparison.OrdinalIgnoreCase)) collective = false;
            else if (arg.Equals("female", StringComparison.OrdinalIgnoreCase)) playerGender = true;
            else if (arg.Equals("male", StringComparison.OrdinalIgnoreCase)) playerGender = false;

         LlmServices.TestPlayerGenderOverride = playerGender;
         behavior.LlmServices.Invalidate();

         List<WitnessEntry> witnesses = collective
            ? CaptiveEncounterService.BuildBanditWitnesses()
            : new List<WitnessEntry>();

         // Force a FEMALE chief and give her a real FEMALE face (a female troop/commoner portrait),
         // not a male troop. Falls back to a blank portrait if the game has no female character.
         NpcProfile captor = CaptiveEncounterService.BuildBanditCaptor(
            "bandit_queen_test", MBRandom.RandomInt(), "Brigands", true);
         CharacterObject queenFace = CaptiveEncounterService.ResolveFemaleBanditFace();
         ChatManager.OpenForCaptiveEncounter(null, captor, witnesses, collective, CaptiveSceneIntent.PersonalDesire, queenFace);

         string warn = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: set Adult Content Level to Hardcore in MCM for the CNC / female-aggressor rules.)"
            : "";

         return $"Female-chief bandit scene opened with {captor.Name} [spoil{(collective ? ", collective" : "")}, " + $"player {(playerGender == true ? "female" : "male")}].{warn}";
      }

      /// <summary>
      ///   cr.bandit_scene [extortion|intimidation|revenge|spoil] [female|male] [solo|collective]
      ///   Conjures a SYNTHETIC brigand captor (no real hero needed) and opens a captive scene —
      ///   tests the bandit-captivity path without being captured. Default intent: extortion.
      ///   'spoil' = the sexual CNC intent (Hardcore). female|male overrides player gender.
      ///   'collective' = the chief's men join in (group aggression). Default: solo.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bandit_scene", "cr")]
      public static string LaunchBanditScene(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;
         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key first.";

         bool? genderOverride = null;
         var intent = CaptiveSceneIntent.Extortion;
         var collective = false;
         foreach (string arg in args ?? new List<string>())
            if (arg.Equals("female", StringComparison.OrdinalIgnoreCase)) genderOverride = true;
            else if (arg.Equals("male", StringComparison.OrdinalIgnoreCase)) genderOverride = false;
            else if (arg.Equals("collective", StringComparison.OrdinalIgnoreCase)) collective = true;
            else if (arg.Equals("solo", StringComparison.OrdinalIgnoreCase)) collective = false;
            else if (arg.Equals("spoil", StringComparison.OrdinalIgnoreCase)) intent = CaptiveSceneIntent.PersonalDesire;
            else if (Enum.TryParse(arg, true, out CaptiveSceneIntent parsed)) intent = parsed;

         if (genderOverride.HasValue)
         {
            LlmServices.TestPlayerGenderOverride = genderOverride;
            behavior.LlmServices.Invalidate();
         }

         List<WitnessEntry> witnesses = collective
            ? CaptiveEncounterService.BuildBanditWitnesses()
            : new List<WitnessEntry>();

         CharacterObject banditFace = CaptiveEncounterService.ResolveBanditTroop(null);
         NpcProfile captor = CaptiveEncounterService.BuildBanditCaptor(
            "bandit_captor_test", MBRandom.RandomInt(), "Brigands", banditFace?.IsFemale);
         ChatManager.OpenForCaptiveEncounter(null, captor, witnesses, collective, intent, banditFace);

         string warn = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: adult level is not Hardcore — the captive prompt rules may not appear; set Hardcore in MCM.)"
            : "";

         return $"Bandit captive scene opened with {captor.Name} [{intent}{(collective ? ", collective" : "")}].{warn}";
      }

      // ── Sprint 17: captive encounter testing ──────────────────────────────

      /// <summary>
      ///   cr.crc_scene [female|male] [solo|collective] [name]
      ///   Opens a captive-encounter chat immediately, forcing PlayerStatus = Captive
      ///   so Section B (CNC rules) lands in the prompt — without the player actually
      ///   being a prisoner. Requires Hardcore adult level for the CNC rules to appear.
      ///   - female|male: override the player gender for this scene (test the other path)
      ///   - solo|collective: private scene, or a collective scene with soldier witnesses
      ///   - name: captor name fragment (defaults to the current conversation NPC)
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("crc_scene", "cr")]
      public static string LaunchCaptiveScene(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key first.";

         // Parse keyword args; the remainder is the captor name fragment.
         bool? genderOverride = null;
         var collective = false;
         var intent = CaptiveSceneIntent.Interrogation;
         var nameTokens = new List<string>();
         foreach (string arg in args ?? new List<string>())
            if (arg.Equals("female", StringComparison.OrdinalIgnoreCase)) genderOverride = true;
            else if (arg.Equals("male", StringComparison.OrdinalIgnoreCase)) genderOverride = false;
            else if (arg.Equals("collective", StringComparison.OrdinalIgnoreCase)) collective = true;
            else if (arg.Equals("solo", StringComparison.OrdinalIgnoreCase)) collective = false;
            else if (arg.Equals("desire", StringComparison.OrdinalIgnoreCase)) intent = CaptiveSceneIntent.PersonalDesire;
            else if (Enum.TryParse(arg, true, out CaptiveSceneIntent parsedIntent)) intent = parsedIntent;
            else nameTokens.Add(arg);

         Hero captor = ResolveHero(nameTokens);

         if (captor == null)
            return "No captor found. Pass a name fragment or start a conversation first.";

         NpcProfile captorProfile = GetOrCreateProfile(behavior, captor);

         // Apply the gender override (if any) and rebuild the PromptBuilder so it takes effect.
         if (genderOverride.HasValue)
         {
            LlmServices.TestPlayerGenderOverride = genderOverride;
            behavior.LlmServices.Invalidate();
         }

         var witnesses = new List<WitnessEntry>();
         if (collective)
            witnesses.AddRange(new[] {
               new WitnessEntry {
                  Name = "A soldier", RelationToNpc = "one of your men, present on your orders",
                  Persona = "Imposing — silent, obedient, watching"
               },
               new WitnessEntry {
                  Name = "Another soldier", RelationToNpc = "one of your men, present on your orders",
                  Persona = "Impassive — expressionless, unmoved"
               }
            });

         string adultWarning = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: adult level is not Hardcore — CNC rules will NOT appear; set it in MCM.)"
            : "";

         string genderNote = genderOverride.HasValue
            ? $" gender forced to {(genderOverride.Value ? "female" : "male")};"
            : "";

         ChatManager.OpenForCaptiveEncounter(captor, captorProfile, witnesses, collective, intent);

         return $"Captive scene opened with {captor.Name}.{genderNote} " + $"{(collective ? "collective" : "solo")} [{intent}].{adultWarning}";
      }

      /// <summary>cr.kinks [name] — list the NPC's current kinks and the valid kink names.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("kinks", "cr")]
      public static string ListKinks(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         string current = profile.Romantic?.Kinks == null || profile.Romantic.Kinks.Count == 0
            ? "(none)"
            : string.Join(", ", profile.Romantic.Kinks);

         return $"{hero.Name} kinks: {current}\nValid: {ValidKinkNames()}";
      }

      // ── Memory editing ────────────────────────────────────────────────────

      /// <summary>
      ///   cr.memory [name]
      ///   Lists all recorded memory events for an NPC, each prefixed by its 0-based
      ///   index so the player can identify which ones to remove with cr.forget.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("memory", "cr")]
      public static string ListMemory(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.Events.Count == 0)
            return $"{hero.Name} has no recorded memory events.";

         IEnumerable<string> lines = profile.Events.Select((e, i) =>
            $"  [{i}] {DescribeDay(e.gameDay)} | {e.type} | {e.summary}");

         return $"{hero.Name} memory ({profile.Events.Count} event(s)):\n" + string.Join("\n", lines);
      }

      /// <summary>cr.missions — lists the errands companions are currently away on.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("missions", "cr")]
      public static string ListMissions(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         return CompanionMissionBehavior.Instance?.DescribeActive() ?? "Companion-mission system is not active.";
      }

      /// <summary>
      ///   cr.quests [name]
      ///   Lists the NPC's quests with their state.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quests", "cr")]
      public static string ListQuests(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.ActiveQuests.Count == 0) return $"{hero.Name} has no quests.";

         IEnumerable<string> lines = profile.ActiveQuests.Select(q =>
         {
            string state = q.IsOutstanding ? "outstanding"
               : q.IsAwaitingReward ? "DONE (awaiting reward)"
               : q.Status.ToString();
            string deadline = q.DeadlineDay.HasValue
               ? $", deadline day {q.DeadlineDay}"
               : "";

            return $"  [{q.Type}] {state}{deadline} — {q.Description}";
         });

         return $"{hero.Name} quests:\n" + string.Join("\n", lines);
      }

      /// <summary>cr.scenarios — list the loaded scripted-scenario rules and which one-shots have fired.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scenarios", "cr")]
      public static string ListScenarios(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         ScenarioRuntime runtime = ScenarioBehavior.Instance?.Runtime;

         if (runtime == null) return "Scenario engine not ready.";

         var sb = new StringBuilder();
         sb.AppendLine($"Loaded scenario rules: {runtime.Rules.Count}");
         foreach (ScenarioRule r in runtime.Rules)
            sb.AppendLine($"  [{r.Id}] {r.Trigger}  guards:{r.Guards.Count} consequences:{r.Consequences.Count}{(r.FireOnce ? " (once)" : "")}");
         sb.Append($"Fired one-shots: {(runtime.FiredRuleIds.Count == 0 ? "none" : string.Join(", ", runtime.FiredRuleIds))}");

         return sb.ToString();
      }

      /// <summary>
      ///   cr.schemes
      ///   Lists the feud schemes currently in motion (plotter → target, kind, state, progress).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("schemes", "cr")]
      public static string ListSchemes(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         IReadOnlyList<Scheme> active = store.Active;

         if (active.Count == 0) return "No schemes are afoot.";

         var sb = new StringBuilder();
         sb.AppendLine($"{active.Count} scheme(s) afoot:");
         foreach (Scheme s in active)
         {
            string plotter = Hero.FindFirst(h => h.StringId == s.PlotterId)?.Name?.ToString() ?? s.PlotterId;
            string target = Hero.FindFirst(h => h.StringId == s.TargetId)?.Name?.ToString() ?? s.TargetId;
            sb.AppendLine($"  {plotter} → {target} | {s.Kind} | {s.State} | progress {s.Progress}");
         }

         return sb.ToString().TrimEnd();
      }

      // ── Marriage system Phase A.1: family-approval engine testing (group "cr") ──

      /// <summary>
      ///   cr.marriage_approve [name]
      ///   Seeds the family's blessing for the player to marry the named hero (approver =
      ///   their clan leader). Then marry them (cr.marry or in-game) to see the family warm
      ///   to you instead of turning hostile.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marriage_approve", "cr")]
      public static string MarriageApprove(List<string> args)
      {
         if (MarriageApprovalBehavior.Instance == null)
            return "Marriage behavior not active. Start a campaign first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         MarriageApprovalBehavior.Instance.Grant(
            hero, hero.Clan?.Leader, (int) CampaignTime.Now.ToDays);

         return $"{hero.Name}: family blessing seeded (by {hero.Clan?.Leader?.Name?.ToString() ?? "?"}). " + "Marry them now and the family will embrace the union instead of resenting it.";
      }

      /// <summary>cr.marriage_revoke [name] — removes a seeded blessing for the named hero.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marriage_revoke", "cr")]
      public static string MarriageRevoke(List<string> args)
      {
         if (MarriageApprovalBehavior.Instance == null)
            return "Marriage behavior not active. Start a campaign first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         bool removed = MarriageApprovalBehavior.Instance.Revoke(hero.StringId);

         return removed
            ? $"{hero.Name}: blessing revoked. Marrying them now would affront the family."
            : $"{hero.Name}: no blessing on record.";
      }

      /// <summary>
      ///   cr.marriage_status [name]
      ///   Reports whether the family has blessed a match with the named hero, plus the
      ///   basic eligibility the marriage path will check (spouse single, player single).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marriage_status", "cr")]
      public static string MarriageStatus(List<string> args)
      {
         if (MarriageApprovalBehavior.Instance == null)
            return "Marriage behavior not active. Start a campaign first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         bool approved = MarriageApprovalBehavior.Instance.IsApproved(hero.StringId);
         string clan = hero.Clan?.Name?.ToString() ?? "(no clan)";

         return $"{hero.Name} — family: {clan}; blessing: {(approved ? "GRANTED" : "none")}; " + $"spouse free: {hero.Spouse == null}; player free: {Hero.MainHero.Spouse == null}.";
      }

      /// <summary>
      ///   cr.mission_return
      ///   Force-resolves the first in-flight errand NOW and prints the report — the test path so you need
      ///   not wait the in-game days for the companion to ride there and back.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("mission_return", "cr")]
      public static string MissionReturn(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         return CompanionMissionBehavior.Instance?.ForceResolveFirst() ?? "Companion-mission system is not active.";
      }

      [CommandLineFunctionality.CommandLineArgumentFunction("open_chat", "campaign")]
      public static string OpenChat(List<string> args)
      {
         try
         {
            ChatManager.Open();

            return "Chat window opened.";
         }
         catch (Exception ex)
         {
            ModLog.Error($"open_chat failed: {ex.GetType().Name}: {ex.Message}");

            return $"Failed to open chat: {ex.Message}";
         }
      }

      /// <summary>
      ///   cr.chronicle_open
      ///   Opens the illuminated in-game chronicle viewer (Phase 2). Test path for the Gauntlet
      ///   window before the encyclopedia button is wired.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("chronicle_open", "cr")]
      public static string OpenChronicle(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error))
            return error;

         ChronicleManager.Open();

         return "Chronicle viewer opened.";
      }

      /// <summary>
      ///   cr.letter_read [name]
      ///   Delivers a synthetic letter from the given NPC through the real delivery path
      ///   (<see cref="LetterPopupManager.Show" />): the parchment window opens in READ mode with
      ///   Acknowledge / Reply. Use it to exercise the read layout and the in-place Reply transition.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_read", "cr")]
      public static string OpenLetterRead(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         LetterPopupManager.Show(BuildTestLetter(hero, behavior.GameStateBridge.GetCurrentGameDay()), profile);

         return $"Delivered a test letter from {hero.Name} to the parchment window (read mode).";
      }

      /// <summary>
      ///   cr.letter_reply [name]
      ///   Opens the parchment courier window straight in REPLY mode with a synthetic received letter —
      ///   write a reply and seal it. The send seals + queues the NPC's answer + closes synchronously
      ///   (no inline LLM call), the pattern that ended the close-on-reply hang and crash.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_reply", "cr")]
      public static string OpenLetterReply(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         LetterWindowManager.OpenReply(BuildTestLetter(hero, behavior.GameStateBridge.GetCurrentGameDay()), profile, hero);

         return $"Opened a parchment reply window from {hero.Name}. Type a reply, send, and watch it close.";
      }

      /// <summary>
      ///   cr.profile_check [name]
      ///   Builds (or fetches) the NPC's profile and runs the exact serialize→deserialize
      ///   round-trip the save uses, reporting success or the precise failure. The Newtonsoft
      ///   exception message carries the JSON path (e.g. "Path 'Romantic.Kinks[0]'"), which
      ///   pinpoints the field that breaks saves for freshly-created profiles. No save needed.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("profile_check", "cr")]
      public static string ProfileCheck(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         try
         {
            string json = JsonConvert.SerializeObject(profile);
            JsonConvert.DeserializeObject<NpcProfile>(json); // prove it reads back

            return $"{hero.Name}: profile round-trips OK ({json.Length} chars, {profile.Events.Count} event(s)).";
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PROFILE CHECK] {hero.Name} FAILED round-trip: {ex.GetType().Name}: {ex.Message}");

            return $"{hero.Name}: ROUND-TRIP FAILED — {ex.GetType().Name}: {ex.Message}";
         }
      }

      /// <summary>cr.rapport_tick — runs a weekly camp-life pass now (compatibility nudges + any companion weddings).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("rapport_tick", "cr")]
      public static string RapportTick(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (CompanionRapportBehavior.Instance == null) return "Camp-life system is not active.";

         CompanionRapportBehavior.Instance.RunTick();

         return "Ran a camp-life tick. Check cr.rapport [name].";
      }

      /// <summary>
      ///   cr.recall_letter
      ///   Sends a letter-borne recall order to the FIRST away companion (path B): on a daily tick after the
      ///   courier delay they abandon the errand and ride home. Use cr.missions to watch, or advance days.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("recall_letter", "cr")]
      public static string RecallLetter(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         return CompanionMissionBehavior.Instance?.OrderRecallByLetterFirst() ?? "Companion-mission system is not active.";
      }

      /// <summary>
      ///   cr.reload_lore
      ///   Reloads ModuleData/character_overrides.json and re-applies (or restores) the authored traits
      ///   per the current "Use custom traits" toggle — no relaunch needed.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("reload_lore", "cr")]
      public static string ReloadLore(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         var behavior = CharacterLoreBehavior.Instance;

         if (behavior == null) return "Character lore behavior is not active.";

         behavior.ReloadAndReapply();

         return $"Reloaded character_overrides.json ({CharacterOverrideLoader.Set.Count} entr(ies)); " + $"custom traits {(ModSettings.UseCustomTraits ? "applied" : "off (vanilla restored)")}.";
      }

      /// <summary>cr.remove_kink &lt;kink&gt; [name] — removes a kink from the NPC's profile.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("remove_kink", "cr")]
      public static string RemoveKink(List<string> args)
         => MutateKink(args, false, "cr.remove_kink <kink> [name]");

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

      /// <summary>cr.chronicle_rewrite — redo the whole illuminated chronicle (e.g. after editing the style file).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("chronicle_rewrite", "cr")]
      public static string RewriteChronicle(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error))
            return error;

         return ChronicleIlluminationService.RewriteAll();
      }

      /// <summary>
      ///   cr.rumor [settlement]
      ///   Opens a transient commoner conversation in the player's current settlement
      ///   (or the named settlement if supplied). No profile is stored; the commoner
      ///   exists only for this chat window. Tests the v1.1 Commoners path.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("rumor", "cr")]
      public static string Rumor(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Check OPENROUTER_API_KEY / model settings.";

         Settlement settlement = null;

         if (args is {Count: > 0})
         {
            string query = string.Join(" ", args).Trim();
            settlement = Settlement.All
                                   .FirstOrDefault(s => s.Name != null && s.Name.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

            if (settlement == null)
               return $"No settlement found matching '{query}'.";
         }
         else
         {
            settlement = Hero.MainHero?.CurrentSettlement;
         }

         if (settlement == null)
            return "No settlement found. Enter a town or castle, or pass a name: cr.rumor Pravend";

         CommonsKnowledge knowledge = CommonsKnowledgeBuilder.Build(settlement);
         NpcProfile profile = CommonsProfileFactory.Create(settlement);

         ChatManager.OpenForCommoner(profile, knowledge);

         return $"Opening commoner chat in {settlement.Name} — speak to {profile.Name}.";
      }

      /// <summary>
      ///   cr.declare_war_quest [name]
      ///   Seeds a declare_war bargain on the given NPC, targeting the first kingdom you can still
      ///   declare war on (not your own, not already at war). Then declare war on it (kingdom screen):
      ///   the deed is stamped done by the WarDeclared event, and the NPC pays out when you report back.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("declare_war_quest", "cr")]
      public static string SeedDeclareWarQuest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         IFaction player = Hero.MainHero?.MapFaction;

         if (player == null) return "You belong to no faction — there is nothing to declare war with.";

         IFaction target = Kingdom.All?.Cast<IFaction>()
                                  .FirstOrDefault(k => k != player && !FactionManager.IsAtWarAgainstFaction(player, k));

         if (target == null) return "No eligible faction (you are at war with all of them, or there are none).";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         var quest = new InformalQuest {
            Type = QuestType.DeclareWar,
            Description = $"Declare war on {target.Name} for {hero.Name}.",
            TargetFactionId = target.StringId,
            TargetFactionName = target.Name?.ToString(),
            IssuedOnDay = today,
            Reward = RewardGrant.None,
            RewardGold = 1000,
            RewardRelation = 8,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);
         QuestJournalBridge.OpenMirror(quest, hero);

         return $"{hero.Name} now wants you to declare war on {target.Name}. Declare it, then report back.";
      }

      /// <summary>
      ///   cr.deliver_prisoner [name]
      ///   Seeds a deliver_prisoner bargain on the given NPC targeting the first LORD you currently hold
      ///   (case a — an immediate hand-over). Then, in conversation with that NPC, offer to deliver them:
      ///   the NPC emits give_prisoner, the captive is transferred, and the reward (500g/+5) is paid.
      ///   Hold a lord prisoner first. (Case b — capture-and-deliver — arises in real play.)
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("deliver_prisoner", "cr")]
      public static string SeedDeliverPrisoner(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Hero held = MobileParty.MainParty?.PrisonRoster?.GetTroopRoster()
                                ?.Select(el => el.Character?.HeroObject)
                                .FirstOrDefault(h => h is {IsLord: true, IsHumanPlayerCharacter: false});

         if (held == null)
            return "You hold no lord prisoner. Capture an enemy lord first, then run this to test the hand-over (case a).";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         var quest = new InformalQuest {
            Type = QuestType.DeliverPrisoner,
            Description = $"Hand {held.Name} over to {hero.Name}.",
            TargetHeroId = held.StringId,
            TargetHeroName = held.Name?.ToString(),
            IssuedOnDay = today,
            Reward = RewardGrant.None,
            RewardGold = 500,
            RewardRelation = 5,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);

         return $"{hero.Name} now wants {held.Name} handed over. Talk to {hero.Name} and offer to deliver them.";
      }

      /// <summary>
      ///   cr.bargain_items [name]
      ///   Seeds a DeliverItems→JoinParty bargain on a recruitable wanderer, so the
      ///   pay-in-goods flow (bag icon → multi-select → bridge settles, the wanderer joins)
      ///   can be tested without waiting for the LLM to offer it. The required value is the
      ///   game-side floor (twice the vanilla hiring price). Open a conversation with the
      ///   wanderer first (or pass a name), then click the bag and hand over goods.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bargain_items", "cr")]
      public static string SeedItemBargain(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         if (!RecruitmentPricing.IsRecruitable(hero))
            return $"{hero.Name} is not a recruitable wanderer — no item bargain possible (see cr.hire_info).";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.ActiveQuests.Any(q => q.IsOutstanding))
            return $"{hero.Name} already has an outstanding task — settle or abandon it first.";

         var day = (int) CampaignTime.Now.ToDays;
         var proposal = new QuestProposal {
            Type = QuestType.DeliverItems,
            Reward = RewardGrant.JoinParty,
            Description = "Bring me goods worth my service, and I am yours."
         };

         InformalQuest quest = QuestFactory.Create(proposal, hero, day);

         if (quest == null) return $"Could not ground the bargain for {hero.Name}.";

         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);
         QuestJournalBridge.OpenMirror(quest, hero);

         int? hire = RecruitmentPricing.GetHiringPrice(hero);

         return $"{hero.Name}: item bargain seeded. Hire price {hire?.ToString() ?? "?"} denars → " + $"required goods value {quest.RequiredItemValue} denars (2×). " + "Open the chat, click the bag, and hand over goods to settle.";
      }

      // ── Sprint 12b: letter testing ────────────────────────────────────────

      /// <summary>
      ///   cr.letter [name]
      ///   Queues a test letter from the NPC with DeliveryDay = today so it shows
      ///   up on the next daily tick (or immediately via cr.letter_deliver).
      ///   Uses RomanticCorrespondence as the reason so personality is exercised.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter", "cr")]
      public static string SeedLetter(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         LetterGenerationService.QueueInitialLetter(
            profile, hero,
            LetterReason.RomanticCorrespondence,
            $"Test letter. Romantic status: {profile.Romantic?.Status.ToString() ?? "unknown"}.",
            behavior.LlmServices, behavior.Store, today);

         // Force immediate delivery by pre-dating it.
         // The LLM call is async; the letter appears on next daily tick once generated.
         return $"{hero.Name}: letter generation queued. It will appear at the next daily tick.";
      }

      /// <summary>
      ///   cr.bargain_marriage [name]
      ///   Seeds a marriage_consent bargain on a clan head (or a parent): a deed (clear bandits
      ///   near a village) conditions the blessing for the player to marry one of their eligible
      ///   kin. Tests the full A.2b path without the LLM: then cr.quest_done + cr.quest_complete
      ///   → the blessing is recorded; verify with cr.marriage_status [spouse].
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bargain_marriage", "cr")]
      public static string SeedMarriageBargain(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Hero spouse = FindEligibleCandidate(hero);

         if (spouse == null)
            return $"{hero.Name} has no unwed, eligible kin to offer (or you are already wed / no authority).";

         var village = Settlement.All.FirstOrDefault(s => s.IsVillage)?.Name?.ToString();

         if (village == null) return "No village found to anchor the deed.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.ActiveQuests.Any(q => q.IsOutstanding))
            return $"{hero.Name} already has an outstanding task — settle or abandon it first.";

         int day = behavior.GameStateBridge.GetCurrentGameDay();
         var proposal = new QuestProposal {
            Type = QuestType.BanditClear,
            Description = $"Prove yourself worthy of {spouse.Name}'s hand: clear the bandits near {village}.",
            TargetSettlement = village,
            Reward = RewardGrant.MarriageConsent,
            MarriageSpouse = spouse.Name?.ToString()
         };

         InformalQuest quest = QuestFactory.Create(proposal, hero, day);

         if (quest == null) return $"Could not ground the marriage bargain for {hero.Name} → {spouse.Name}.";

         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);
         QuestJournalBridge.OpenMirror(quest, hero);

         return $"{hero.Name}: marriage bargain seeded — clear bandits near {village} to earn the blessing for " + $"marrying {spouse.Name}. Then: cr.quest_done, cr.quest_complete, then cr.marriage_status {spouse.Name}.";
      }

      /// <summary>
      ///   cr.deed_heard [name]
      ///   Seeds a notable deed of the PLAYER's that WOUNDS the named (or talked-to) NPC's own faction,
      ///   then echoes what that NPC would now have heard of you — the influence-loop test path without
      ///   having to win a real siege. Open a conversation with them afterward to see it colour their tone.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("deed_heard", "cr")]
      public static string SeedPlayerDeedHeard(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         var store = WorldEventStore.Instance;

         if (store == null) return "World-event store is not active.";

         string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
         string factionName = hero.MapFaction?.Name?.ToString() ?? "their people";

         List<string> aggrieved = hero.MapFaction?.StringId != null
            ? new List<string> {hero.MapFaction.StringId}
            : new List<string>();
         var factions = new List<string>(aggrieved);
         if (Hero.MainHero?.MapFaction?.StringId != null) factions.Add(Hero.MainHero.MapFaction.StringId);

         store.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = (int) CampaignTime.Now.ToDays,
            PlaceId = hero.CurrentSettlement?.StringId,
            Magnitude = 70,
            InvolvesPlayer = true,
            Summary = $"{playerName} broke a host of {factionName} in open battle (test seed).",
            ActorIds = new List<string> {Hero.MainHero?.StringId}.Where(s => s != null).ToList(),
            FactionIds = factions,
            AggrievedFactionIds = aggrieved
         });

         string block = EncounterContextBuilder.ResolvePlayerDeedsHeard(hero);

         return block == null
            ? $"Seeded a deed against {factionName}, but {hero.Name} would not (yet) have heard of it."
            : $"Seeded. {hero.Name} has now heard of your deeds:\n{block}\n\n(Open a conversation to see it colour their tone.)";
      }

      // ── Sprint 11: informal quest testing (group "cr") ────────────────────

      /// <summary>
      ///   cr.quest [name]
      ///   Seeds a bandit-clear quest (near a real village, 10-day deadline, 300g/+3)
      ///   on the NPC, through the real issue path. Tests prompt injection of the quest
      ///   and the giver acknowledging it. Pair with cr.quest_done to test the reward.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest", "cr")]
      public static string SeedQuest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         var village = Settlement.All.FirstOrDefault(s => s.IsVillage)?.Name?.ToString();

         if (village == null) return "No village found to anchor the quest.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int day = behavior.GameStateBridge.GetCurrentGameDay();

         var proposal = new QuestProposal {
            Type = QuestType.BanditClear,
            Description = $"Clear the bandits troubling the roads near {village}.",
            TargetSettlement = village,
            DeadlineDays = 10,
            RewardGold = 300,
            RewardRelation = 3
         };
         var fake = new ParsedResponse {Dialogue = "", QuestGiven = proposal};

         QuestService.ApplyResponse(profile, hero, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         int outstanding = profile.ActiveQuests.Count(q => q.IsOutstanding);

         return $"{hero.Name}: seeded bandit_clear near {village} ({outstanding} outstanding quest(s)).";
      }

      /// <summary>
      ///   cr.quest_gold [amount] [name]
      ///   Seeds an outstanding ProvideGold quest on the NPC (e.g. a spouse's request for coin to
      ///   support their household) so the Give Gold button — and the take_gold stamping path — can be
      ///   tested deterministically, without waiting for the LLM to issue one or a child-support letter
      ///   to arrive. Any non-zero gold the player gives the giver satisfies it; [amount] is flavour for
      ///   the description (default 1000).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_gold", "cr")]
      public static string SeedProvideGoldQuest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         // Optional leading amount; the remainder (or all of it) is the name fragment.
         int amount = 1000;
         IEnumerable<string> nameTokens = args;
         if (args is {Count: > 0} && int.TryParse(args[0], out int parsed) && parsed > 0)
         {
            amount = parsed;
            nameTokens = args.Skip(1);
         }

         Hero hero = ResolveHero(nameTokens);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int day = behavior.GameStateBridge.GetCurrentGameDay();

         var proposal = new QuestProposal {
            Type = QuestType.ProvideGold,
            Description = $"{hero.Name} asks you for {amount} denars to help support their household.",
            DeadlineDays = 30,
            RewardRelation = 5
         };
         var fake = new ParsedResponse {Dialogue = "", QuestGiven = proposal};

         QuestService.ApplyResponse(profile, hero, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         int outstanding = profile.ActiveQuests.Count(q => q.IsOutstanding);

         return $"{hero.Name}: seeded ProvideGold (~{amount} denars). Open a chat and use the Give Gold " +
                $"button (or cr.take_gold <n> {hero.Name}) — any payment stamps it. " +
                $"({outstanding} outstanding quest(s); verify with cr.quests {hero.Name}).";
      }

      /// <summary>
      ///   cr.feed_test [summary]
      ///   Exercises the Extension Surface (Volet A): feeds a sample external world fact through
      ///   <see cref="Feed.Record" /> — the same door a third-party mod would use — and reports the result +
      ///   the new store size. Talk to a nearby NPC who'd plausibly have heard it.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("feed_test", "cr")]
      public static string FeedTest(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         string summary = args is {Count: > 0}
            ? string.Join(" ", args).Trim()
            : "The grey mare of Lord Caladog won the Pravend races; heavy purses changed hands.";

         var e = new ExternalWorldEvent {
            Id = "feedtest_" + Guid.NewGuid().ToString("N"),
            SourceMod = "ConsoleTest",
            Summary = summary,
            Magnitude = 30,
            Lifetime = ExternalEventLifetime.Rumor
         };

         FeedResult result = Feed.Record(e);

         return $"Feed.Record -> {result}. World-event store now holds {WorldEventStore.Instance?.Count ?? 0} event(s). " +
                "Speak with a nearby NPC who'd plausibly have heard it.";
      }

      /// <summary>
      ///   cr.realm_news
      ///   Seeds a high-magnitude WAR between two kingdoms so the "TALK OF ALL THE REALM" line is testable —
      ///   then speak with any NPC who would have heard of it (the influence-loop realm-news polish).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("realm_news", "cr")]
      public static string SeedRealmNews(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         var store = WorldEventStore.Instance;

         if (store == null) return "World-event store is not active.";

         List<Kingdom> kingdoms = Kingdom.All?.Where(k => k is {IsEliminated: false}).Take(2).ToList();

         if (kingdoms == null || kingdoms.Count < 2) return "Need at least two kingdoms in play.";

         Kingdom a = kingdoms[0],
                 b = kingdoms[1];
         store.Record(new WorldEvent {
            Type = WorldEventType.WarDeclared,
            Day = (int) CampaignTime.Now.ToDays,
            Magnitude = 80,
            Summary = $"{a.Name} declared war on {b.Name} (test seed).",
            FactionIds = new List<string> {a.StringId, b.StringId}
         });

         return $"Seeded war: {a.Name} vs {b.Name}. Speak with an NPC to hear the realm's talk of it.";
      }

      /// <summary>
      ///   cr.quest_scout [name]
      ///   Seeds a scout_army quest on the NPC, targeting their first enemy kingdom.
      ///   Pair with cr.quest_done to test the completion reward, or let proximity
      ///   detection fire naturally (OnHourlyTick) for a real-path test.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_scout", "cr")]
      public static string SeedScoutQuest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         IFaction enemy = hero.MapFaction != null
            ? Kingdom.All?.FirstOrDefault(k =>
               !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(hero.MapFaction, k))
            : null;

         if (enemy == null)
            return $"{hero.Name} is not at war with any kingdom — cannot seed a scout_army quest.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int day = behavior.GameStateBridge.GetCurrentGameDay();

         var proposal = new QuestProposal {
            Type = QuestType.ScoutArmy,
            Description = $"Find where the armies of {enemy.Name} are moving and report back.",
            TargetFaction = enemy.Name?.ToString(),
            DeadlineDays = 15,
            RewardGold = 400,
            RewardRelation = 3
         };
         var fake = new ParsedResponse {Dialogue = "", QuestGiven = proposal};

         QuestService.ApplyResponse(profile, hero, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         int outstanding = profile.ActiveQuests.Count(q => q.IsOutstanding);

         return $"{hero.Name}: seeded scout_army quest targeting {enemy.Name} ({outstanding} outstanding quest(s)).";
      }

      /// <summary>cr.send_barter [about] — sends your first companion to trade at a town (Trade); profit reaches you on return.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("send_barter", "cr")]
      public static string SendBarter(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (CompanionMissionBehavior.Instance == null) return "Companion-mission system is not active.";

         Hero c = FirstCompanionInParty();

         if (c == null) return "No companion in your party. Recruit one first.";

         string about = args is {Count: > 0}
            ? string.Join(" ", args).Trim()
            : null;

         return CompanionMissionBehavior.Instance.Dispatch(c, CompanionMissionType.Barter, about).Message;
      }

      /// <summary>
      ///   cr.send_envoy [about] — sends your first companion as an envoy to a faction's town (Charm); their mood comes
      ///   back in conversation.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("send_envoy", "cr")]
      public static string SendEnvoy(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (CompanionMissionBehavior.Instance == null) return "Companion-mission system is not active.";

         Hero c = FirstCompanionInParty();

         if (c == null) return "No companion in your party. Recruit one first.";

         string about = args is {Count: > 0}
            ? string.Join(" ", args).Trim()
            : null;

         return CompanionMissionBehavior.Instance.Dispatch(c, CompanionMissionType.Envoy, about).Message;
      }

      /// <summary>
      ///   cr.send_news [about]
      ///   Dispatches your first available companion to gather news — physically: they leave the party as
      ///   their own clan party and ride to a town. Optional [about] scopes the destination (a faction,
      ///   culture, or town: "cr.send_news Sturgia"). Use cr.mission_return to skip the travel and resolve now.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("send_news", "cr")]
      public static string SendNews(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (CompanionMissionBehavior.Instance == null) return "Companion-mission system is not active.";

         Hero c = FirstCompanionInParty();

         if (c == null) return "No companion in your party. Recruit one first.";

         string about = args is {Count: > 0}
            ? string.Join(" ", args).Trim()
            : null;

         return CompanionMissionBehavior.Instance.Dispatch(c, CompanionMissionType.GatherNews, about).Message;
      }

      /// <summary>cr.send_spy [about] — sends your first companion to scout a town (Roguery); intel comes back in conversation.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("send_spy", "cr")]
      public static string SendSpy(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (CompanionMissionBehavior.Instance == null) return "Companion-mission system is not active.";

         Hero c = FirstCompanionInParty();

         if (c == null) return "No companion in your party. Recruit one first.";

         string about = args is {Count: > 0}
            ? string.Join(" ", args).Trim()
            : null;

         return CompanionMissionBehavior.Instance.Dispatch(c, CompanionMissionType.Spy, about).Message;
      }

      /// <summary>
      ///   cr.send_steal [about] — sends your first companion to lift coin from a town (Roguery); loot reaches you on
      ///   return.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("send_steal", "cr")]
      public static string SendSteal(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (CompanionMissionBehavior.Instance == null) return "Companion-mission system is not active.";

         Hero c = FirstCompanionInParty();

         if (c == null) return "No companion in your party. Recruit one first.";

         string about = args is {Count: > 0}
            ? string.Join(" ", args).Trim()
            : null;

         return CompanionMissionBehavior.Instance.Dispatch(c, CompanionMissionType.Steal, about).Message;
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

      /// <summary>cr.happiness_set &lt;0..100&gt; [name] — sets a companion's contentment, to test the bands/encyclopedia line.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("happiness_set", "cr")]
      public static string SetHappiness(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         if (args == null || args.Count < 1 || !int.TryParse(args[0], out int val))
            return "Usage: cr.happiness_set <0..100> [name]";

         string frag = args.Count > 1
            ? string.Join(" ", args.GetRange(1, args.Count - 1)).Trim()
            : null;
         Hero hero = !string.IsNullOrWhiteSpace(frag)
            ? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0)
            : Hero.OneToOneConversationHero;

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile p = GetOrCreateProfile(behavior, hero);
         p.Happiness = CompanionHappiness.Clamp(val);
         behavior.Store.Set(p);

         return $"{hero.Name} contentment set to {p.Happiness} ({CompanionHappiness.Describe(p.Happiness)}).";
      }

      /// <summary>
      ///   cr.memory_set &lt;index&gt; &lt;new text&gt; [name]
      ///   Replaces the summary of the event at the given index with new text.
      ///   The last token is tried as an NPC name fragment; if it matches a hero it is
      ///   used as such, otherwise the whole remainder is the summary and the current
      ///   conversation NPC is used.
      ///   Example: cr.memory_set 2 She remembered me fondly. Alympia
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("memory_set", "cr")]
      public static string SetMemoryEvent(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (args == null || args.Count < 2 || !int.TryParse(args[0], out int index))
            return "Usage: cr.memory_set <index> <new text> [name]\n" + "       Example: cr.memory_set 2 She was kind and warm. Alympia\n" + "       Use cr.memory to see indices.";

         // Try the last token as an NPC name fragment. If it resolves, strip it.
         Hero hero = null;
         int summaryEnd = args.Count; // exclusive
         if (args.Count >= 3)
         {
            string lastToken = args[args.Count - 1];
            Hero candidate = Hero.AllAliveHeroes.FirstOrDefault(h =>
               h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(lastToken, StringComparison.OrdinalIgnoreCase) >= 0);
            if (candidate != null)
            {
               hero = candidate;
               summaryEnd = args.Count - 1;
            }
         }

         if (hero == null)
            hero = Hero.OneToOneConversationHero;

         if (hero == null)
            return "No NPC found. Pass a name fragment as the last argument or start a conversation first.";

         string newSummary = string.Join(" ", args.Skip(1).Take(summaryEnd - 1)).Trim();

         if (string.IsNullOrWhiteSpace(newSummary))
            return "The new text cannot be empty.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (index < 0 || index >= profile.Events.Count)
            return $"Index {index} out of range — {hero.Name} has {profile.Events.Count} event(s). Use cr.memory to see them.";

         NotableEvent old = profile.Events[index];
         profile.Events[index] = old with {summary = newSummary};
         behavior.Store.Set(profile);

         return $"{hero.Name}: event [{index}] updated.\n" + $"  Before: \"{old.summary}\"\n" + $"  After:  \"{newSummary}\"";
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

      // ── Player-authored character data ────────────────────────────────────

      /// <summary>
      ///   cr.backstory [name]
      ///   Shows the authored override for an NPC (if any) — its backstory and the effective vs vanilla
      ///   levels of the five personality traits — so you can confirm the file took effect.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("backstory", "cr")]
      public static string ShowBackstory(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         CharacterOverride ov = CharacterOverrideLoader.For(hero);


         string Fmt(TraitObject t)
         {
            return $"{hero.GetTraitLevel(t):+#;-#;0} (vanilla {hero.CharacterObject.GetTraitLevel(t):+#;-#;0})";
         }


         var sb = new StringBuilder();
         sb.AppendLine($"{hero.Name} — custom traits: {(ModSettings.UseCustomTraits ? "ON" : "OFF")}");
         sb.AppendLine($"  Valor {Fmt(DefaultTraits.Valor)} | Honor {Fmt(DefaultTraits.Honor)} | Mercy {Fmt(DefaultTraits.Mercy)}");
         sb.AppendLine($"  Generosity {Fmt(DefaultTraits.Generosity)} | Calculating {Fmt(DefaultTraits.Calculating)}");
         if (ov == null)
         {
            sb.AppendLine("  No authored override for this NPC.");
         }
         else
         {
            sb.AppendLine($"  Override matched (id={ov.Id ?? "—"}).");
            sb.AppendLine(ov.HasBackstory
               ? $"  Backstory: {ov.Backstory}"
               : "  Backstory: (none)");
         }

         return sb.ToString();
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

      /// <summary>
      ///   cr.chronicle
      ///   Renders the player's chronicle (their recorded deeds, in chronicler voice) to the console
      ///   and writes it to CalradiaRemembers_chronicle.log. Phase-1 test path for the world-event
      ///   store + templated chronicle before the in-game illuminated viewer exists.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("chronicle", "cr")]
      public static string ShowChronicle(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error))
            return error;

         string text = ChronicleWriter.Render();
         string path = ChronicleWriter.WriteToLog();

         return path != null
            ? $"{text}\n\n(written to {path})"
            : text;
      }

      /// <summary>cr.happiness [name] — shows a companion's contentment in your service (score + band).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("happiness", "cr")]
      public static string ShowHappiness(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         int score = CompanionHappinessService.Get(hero);
         string leave = CompanionHappiness.WantsToLeave(score)
            ? " — WANTS TO LEAVE"
            : "";

         return $"{hero.Name} — Contentment {score} ({CompanionHappiness.Describe(score)}); band {CompanionHappiness.Band(score)}{leave}.";
      }

      /// <summary>cr.rapport [name] — shows how a companion feels about their fellow companions (camp life).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("rapport", "cr")]
      public static string ShowRapport(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero is not {IsPlayerCompanion: true}) return "Pass one of your own companions' name.";

         List<string> lines = Hero.AllAliveHeroes
                                  .Where(h => h != hero && h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion)
                                  .Select(h => $"- {h.Name}: {hero.GetRelation(h)} ({CompanionRapport.Band(hero.GetRelation(h))})")
                                  .ToList();

         return lines.Count == 0
            ? $"{hero.Name} has no fellow companions to feel anything about."
            : $"{hero.Name} feels:\n{string.Join("\n", lines)}";
      }

      /// <summary>
      ///   cr.recruit_info [name]
      ///   Shows the lord-defection verdict for an NPC — the gathered gate facts and whether
      ///   join_clan would be taught. Diagnostic only; mutates nothing.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("recruit_info", "cr")]
      public static string ShowRecruitInfo(List<string> args)
      {
         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         LordRecruitmentFacts facts = LordRecruitmentFactsBuilder.Build(hero);

         if (facts == null)
            return $"{hero.Name} can never be a defection candidate (not a free lord of another clan).";

         LordRecruitmentBlock verdict = LordRecruitment.Evaluate(facts);

         return $"{hero.Name} — join_clan: {(verdict == LordRecruitmentBlock.None ? "ELIGIBLE" : "blocked: " + verdict)}\n" + $"  relation {facts.Relation} (need >= {LordRecruitment.MinRelation}), " + $"player tier {facts.PlayerClanTier} vs lord tier {facts.LordClanTier}\n" + $"  vulnerable: landless={facts.LordIsLandless}, clanWeak/destroyed={facts.LordClanWeakOrDestroyed}, " + $"hasLiege={facts.HasLiege} (rel {facts.RelationWithLiege}, need <= {LordRecruitment.LiegeResentmentThreshold})";
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

      // ── Sprint 12a: player standing diagnostic ────────────────────────────

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

      // ── Sprint 10: action testing (group "cr") ────────────────────────────

      [CommandLineFunctionality.CommandLineArgumentFunction("relation", "cr")]
      public static string SimulateRelation(List<string> args)
         => SimulateRelation(args, NotableEventType.Other, "cr.relation <delta> [name]");

      [CommandLineFunctionality.CommandLineArgumentFunction("relation_intimate", "cr")]
      public static string SimulateRelationIntimate(List<string> args)
         => SimulateRelation(args, NotableEventType.Intimacy, "cr.relation_intimate <delta> [name]");

      /// <summary>
      ///   cr.succession [predecessor]
      ///   Simulates dynastic succession with the current player as the heir: reframes every stored NPC
      ///   memory (drops the predecessor's private events, keeps the political/relational, marks it
      ///   inherited). The predecessor defaults to your father/mother; pass a name to use a specific hero.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("succession", "cr")]
      public static string SimulateSuccession(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         var heir = Hero.MainHero;

         if (heir == null) return "No player hero.";

         Hero predecessor = args is {Count: > 0}
            ? ResolveHero(args)
            : heir.Father ?? heir.Mother;

         if (predecessor == null)
            return "No predecessor found — pass an NPC name, or play a character who has a parent.";
         if (predecessor == heir)
            return "The predecessor and the heir cannot be the same hero.";

         int n = SuccessionBehavior.Instance?.Apply(predecessor, heir) ?? 0;

         return $"Succession simulated: {n} profile(s) reframed as the heir of {predecessor.Name}. " + $"Talk to an NPC — they should speak of {predecessor.Name} as your kin; private events are gone.";
      }

      [CommandLineFunctionality.CommandLineArgumentFunction("take_gold", "cr")]
      public static string TakeGold(List<string> args)
         => SimulateGold("take_gold", args, "cr.take_gold <amount> [name]");

      [CommandLineFunctionality.CommandLineArgumentFunction("test_llm", "campaign")]
      public static string TestLlm(List<string> args)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior == null)
            return "Calradia Remembers behavior not active. Start a campaign first.";

         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Check OPENROUTER_API_KEY environment variable.";

         _ = LlmDiagnostic.Run(behavior.LlmServices, behavior.Store, behavior.GameStateBridge)
                          .ContinueWith(t =>
                          {
                             if (t.Exception != null)
                                ModLog.Error($"LLM diagnostic faulted: {t.Exception.GetBaseException().Message}");
                          }, TaskScheduler.Default);

         return "LLM diagnostic started. Watch the combat log and the log file for results.";
      }

      /// <summary>
      ///   cr.jealousy [name]
      ///   Resolves and immediately fires a jealousy reaction as if the player had just made a
      ///   romantic advance toward the named NPC (or the current conversation partner). Bypasses
      ///   the witness/probability/cooldown gates so the resolution and consequence are testable
      ///   without building a full romance: it prints the inferred jealous party and applies the
      ///   grudge + grievance memory + a threatening letter.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("jealousy", "cr")]
      public static string TriggerJealousy(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error))
            return error;
         if (JealousyBehavior.Instance == null)
            return "Jealousy system is not active.";

         string nameFragment = args is {Count: > 0}
            ? string.Join(" ", args).Trim()
            : null;
         Hero courted = !string.IsNullOrWhiteSpace(nameFragment)
            ? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
            : Hero.OneToOneConversationHero;

         if (courted == null)
            return "Usage: cr.jealousy [name]\n" + "  Resolves and fires a jealousy reaction as if the player romanced this NPC.";

         return JealousyBehavior.Instance.DebugForce(courted, RomanticActTier.Flirt);
      }

      /// <summary>
      ///   cr.jealousy_comp
      ///   Reports the player's current partner household (native spouse + committed consorts) with
      ///   each one's culture, sharing-tolerance and how long since they were last attended to, then
      ///   forces one competition pass so the v1.5 ongoing-rivalry loop can be tested without waiting.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("jealousy_comp", "cr")]
      public static string TriggerJealousyCompetition(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error))
            return error;
         if (JealousyBehavior.Instance == null)
            return "Jealousy system is not active.";

         return JealousyBehavior.Instance.DebugCompetition();
      }

      // ── Self-test harness (cr.testall / cr.test <name>) ───────────────────

      /// <summary>
      ///   cr.testall
      ///   Runs every registered end-to-end self-test EXCEPT the irreversible ones (declaring war,
      ///   sealing one-shot scenarios) and prints a [SELFTEST] summary. Each test exercises the real
      ///   service/bridge pipeline and logs PASS/FAIL/MANUAL. The irreversible tests are held back so a
      ///   routine sweep never mutates the live campaign that way (and never poisons the conversation
      ///   tests); run them explicitly via cr.test &lt;name&gt;, or cr.test full on a throwaway save.
      ///   WARNING: even the safe sweep mutates relations/quests/clans — copy your save or reload when done.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("testall", "cr")]
      public static string RunAllSelfTests(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         return CrSelfTest.RunAll(behavior);
      }

      /// <summary>
      ///   cr.test [name|list]
      ///   Runs one self-test by name (e.g. cr.test scenario), or lists them (name + severity +
      ///   description) with no argument or "list". Adding a new test needs NO new console command —
      ///   it registers in CrSelfTest and is reachable here automatically.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("test", "cr")]
      public static string RunSelfTest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         string name = args is {Count: > 0}
            ? args[0].Trim()
            : null;

         if (string.IsNullOrEmpty(name) || name.Equals("list", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.List();

         if (name.Equals("chat", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.RunChat(behavior); // only the conversation smoke tests (LLM calls)
         if (name.Equals("mech", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.RunMechanical(behavior); // only the mechanical tests (no API cost)
         if (name.Equals("full", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.RunEverything(behavior); // EVERYTHING incl. irreversible (declare war / seal) — throwaway save only

         return CrSelfTest.Run(name, behavior);
      }

      // ── Deferred delivery test (can't be synchronous — births take game-days) ──

      private static string _pregBirthMotherId;
      private static int _pregBirthArmDay = -1;
      private static int _pregBirthChildrenBefore;

      /// <summary>
      ///   cr.preg_birth [name|reset]
      ///   Two-phase test that a CR-created pregnancy is actually DELIVERED by the engine (the open
      ///   question behind pregnancy's Hero.IsPregnant=False). First call ARMS: it force-conceives a
      ///   fertile female (named, or auto-picked) and records her child count. Advance ~84 days (fast-
      ///   forward, NO reload — the marker is in memory), then call again to CHECK: a new child = PASS
      ///   (the IsPregnant flag is cosmetic); 84+ days and none = FAIL (the flag blocks delivery).
      ///   Pass "reset" to re-arm. Not part of cr.testall — a birth cannot resolve inside one command.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("preg_birth", "cr")]
      public static string PregBirthTest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         int today = behavior.GameStateBridge.GetCurrentGameDay();
         bool reset = args is {Count: > 0} && args[0].Equals("reset", StringComparison.OrdinalIgnoreCase);

         // ── PHASE 2 — armed: has the pregnancy been delivered? ──
         if (!reset && _pregBirthMotherId != null)
         {
            Hero mom = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == _pregBirthMotherId);

            if (mom == null)
            {
               string lost = _pregBirthMotherId;
               _pregBirthMotherId = null;

               return $"Armed mother ({lost}) not found (reloaded, or she died). Marker cleared — run cr.preg_birth to arm again.";
            }

            int childrenNow = mom.Children?.Count ?? 0;
            int elapsed = today - _pregBirthArmDay;

            if (childrenNow > _pregBirthChildrenBefore)
            {
               _pregBirthMotherId = null;

               return $"[PREG_BIRTH] PASS — {mom.Name} delivered: {_pregBirthChildrenBefore} -> {childrenNow} children after {elapsed} days. " + "The reflective pregnancy IS delivered despite Hero.IsPregnant=False (the flag is cosmetic).";
            }

            if (elapsed < 84)
               return $"[PREG_BIRTH] PENDING — {mom.Name}: {elapsed}/84 days elapsed, still {childrenNow} children. Advance more time (no reload), then re-run.";

            return $"[PREG_BIRTH] FAIL — {mom.Name}: {elapsed} days passed and no new child ({_pregBirthChildrenBefore} -> {childrenNow}). " + "The reflectively-created pregnancy was NOT delivered — Hero.IsPregnant=False blocks the engine. PregnancyService must also set the flag.";
         }

         // ── PHASE 1 — arm: conceive on a fertile female and record the baseline ──
         List<string> nameTokens = reset && args != null
            ? args.GetRange(1, args.Count - 1)
            : args ?? new List<string>();
         Hero mother = nameTokens.Count > 0
            ? ResolveHero(nameTokens)
            : Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale && !h.IsPregnant && h.Age >= 18f && h.Age <= 45f);

         if (mother == null) return "No fertile (female, 18-45, not pregnant) NPC found. Pass a name, or none to auto-pick.";

         int before = mother.Children?.Count ?? 0;
         string note = PregnancyService.TryConceive(mother, Hero.MainHero, true);

         if (note == null)
            return $"{mother.Name}: conception did NOT take (ineligible — already pregnant, recently gave birth, or out of 18-45). Try another.";

         _pregBirthMotherId = mother.StringId;
         _pregBirthArmDay = today;
         _pregBirthChildrenBefore = before;

         return $"[PREG_BIRTH] ARMED — {mother.Name} conceived on day {today} (had {before} children, IsPregnant={mother.IsPregnant}). " + "Advance ~84 days WITHOUT reloading (the marker is in memory), then run cr.preg_birth again to verify delivery.";
      }

      /// <summary>
      ///   cr.bastard_check &lt;motherName&gt; &lt;fatherName&gt;
      ///   Read-only diagnostic for the bastards prestige gate ([[project-bastards]]): maps both heroes'
      ///   clans to a prestige score and prints whether a grown bastard would press a claim on the FATHER's
      ///   clan or follow the MOTHER's grander dynasty, plus the discovery risk for a few knower counts.
      ///   Mutates nothing. The last token is the father; everything before it is the mother's name.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_check", "cr")]
      public static string BastardCheck(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (args == null || args.Count < 2)
            return "Usage: cr.bastard_check <motherName> <fatherName>";

         Hero father = ResolveHero(new List<string> {args[args.Count - 1]});
         Hero mother = ResolveHero(args.GetRange(0, args.Count - 1));

         if (mother == null || father == null)
            return "Need two heroes — cr.bastard_check <motherName> <fatherName>.";

         int maternal = ClanPrestige(mother.Clan);
         int paternal = ClanPrestige(father.Clan);
         BastardClaimVerdict verdict = BastardPrestigePolicy.Resolve(maternal, paternal);

         var sb = new StringBuilder();
         sb.AppendLine($"Bastard of {mother.Name} (mother) x {father.Name} (father):");
         sb.AppendLine($"  maternal house {mother.Clan?.Name?.ToString() ?? "—"}: prestige {maternal}");
         sb.AppendLine($"  paternal house {father.Clan?.Name?.ToString() ?? "—"}: prestige {paternal}");
         sb.AppendLine($"  claim verdict: {verdict} (the child is born into the MOTHER's clan regardless)");
         sb.AppendLine($"  discovery risk by #knowers: 1={BastardDiscoveryPolicy.DiscoveryChancePercent(1)}% " +
                       $"3={BastardDiscoveryPolicy.DiscoveryChancePercent(3)}% " +
                       $"6={BastardDiscoveryPolicy.DiscoveryChancePercent(6)}%");

         return sb.ToString();
      }

      /// <summary>
      ///   cr.bastards — lists the player's tracked bastards: name, hidden/discovered, age, and the
      ///   per-season discovery chance at that age. Diagnostic only.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastards", "cr")]
      public static string ListBastards(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         BastardBehavior b = BastardBehavior.Instance;
         if (b == null) return "Bastard system not active.";
         if (b.Bastards.Count == 0)
            return "No tracked bastards. (They form at a non-marital CR birth, or via cr.bastard_tag <name>.)";

         var sb = new StringBuilder();
         sb.AppendLine($"{b.Bastards.Count} tracked bastard(s):");
         foreach (KeyValuePair<string, bool> kv in b.Bastards)
         {
            Hero h = Hero.AllAliveHeroes.FirstOrDefault(x => x.StringId == kv.Key);
            string name = h?.Name?.ToString() ?? kv.Key;
            string detail = h != null
               ? $"age {h.Age:0}, {BastardDiscoveryPolicy.DiscoveryChancePercent((int) h.Age)}%/season"
               : "—";
            sb.AppendLine($"  {name}: {(kv.Value ? "DISCOVERED" : "hidden")} ({detail}; blackmail: {b.BlackmailStatusOf(kv.Key)})");
         }

         return sb.ToString();
      }

      /// <summary>cr.bastard_tag [name] — force-tag a hero as a concealed player bastard (testing, no birth needed).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_tag", "cr")]
      public static string TagBastard(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         BastardBehavior.Instance.DebugTrack(hero);

         return $"{hero.Name} is now tracked as a concealed bastard (age {hero.Age:0}). " +
                "Run cr.bastard_season to roll discovery, or cr.bastards to list.";
      }

      /// <summary>cr.bastard_season — runs the seasonal discovery evaluation now (testing, skips waiting a season).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_season", "cr")]
      public static string RunBastardSeason(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         BastardBehavior.Instance.DebugEvaluateSeason();

         return "Seasonal discovery evaluated. Use cr.bastards to see who was exposed; a discovery records a " +
                "Scandal in the world-event store (cr.chronicle / talk to a nearby NPC) and dents your standing.";
      }

      /// <summary>
      ///   cr.bastard_claim [name] — resolves a grown bastard's claim against your house using the real
      ///   prestige gate (maternal house vs yours) and applies it: a lesser/equal maternal house presses a
      ///   claim (hostility + scandal); a grander one follows the mother's dynasty. Tests the coming-of-age path.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_claim", "cr")]
      public static string ForceBastardClaim(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         BastardClaimVerdict verdict = BastardBehavior.Instance.ResolveClaimVerdict(hero);
         BastardBehavior.Instance.DebugClaim(hero, verdict == BastardClaimVerdict.PressesClaim);

         return verdict == BastardClaimVerdict.PressesClaim
            ? $"{hero.Name} presses a claim against your house — relation drop + scandal recorded. (Verdict: {verdict}.)"
            : $"{hero.Name} follows the mother's grander dynasty — no claim. (Verdict: {verdict}.)";
      }

      /// <summary>
      ///   cr.bastard_blackmail [name] — reports whether an NPC holds a bastard secret over the player (she is
      ///   the mother of a still-concealed bastard of yours) and, if so, the gold she could demand for silence.
      ///   Diagnostic only. Tag one first with cr.bastard_tag if testing without a real birth.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_blackmail", "cr")]
      public static string CheckBastardBlackmail(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         int demand = BastardBehavior.Instance.BlackmailDemandOver(hero);

         return demand > 0
            ? $"{hero.Name} holds a bastard secret over you — she could demand ~{demand} denars for her silence."
            : $"{hero.Name} holds no bastard leverage over you (not the mother of a concealed bastard of yours).";
      }

      /// <summary>
      ///   cr.bastard_mother [name] — shows how an NPC would behave as the mother of the player's bastard,
      ///   from her traits + regard: blackmail (mercenary), longing, resentful, or a pragmatic plea for keep.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_mother", "cr")]
      public static string CheckBastardMother(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         BastardMotherDisposition d = BastardBehavior.Instance.MotherDisposition(hero);
         string note = d switch {
            BastardMotherDisposition.Mercenary => "she would BLACKMAIL for silence",
            BastardMotherDisposition.Longing => "she longs for you — come see the child, wishes you'd wed her",
            BastardMotherDisposition.Resentful => "she writes coldly, feeling wronged",
            _ => "she pragmatically asks for the child's keep"
         };

         return $"{hero.Name} — bastard-mother disposition: {d} ({note}). " +
                $"Honor {hero.GetTraitLevel(DefaultTraits.Honor):+#;-#;0}, Calculating {hero.GetTraitLevel(DefaultTraits.Calculating):+#;-#;0}, " +
                $"relation {hero.GetRelation(Hero.MainHero)}.";
      }

      /// <summary>cr.bastard_demand [name] — force-raise a blackmail demand over the named tracked bastard (testing the pay/leak loop).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_demand", "cr")]
      public static string RaiseBastardDemand(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass the bastard child's name fragment.";

         BastardBehavior.Instance.DebugRaiseDemand(hero);

         return $"Raised a blackmail demand over {hero.Name} — their mother now holds an active demand. " +
                $"Pay via cr.bastard_pay <mother>, or run cr.bastard_season across seasons to watch her leak it if unpaid.";
      }

      /// <summary>cr.bastard_pay [name] — pay an NPC's blackmail demands for her silence (gold to her, a grace period bought).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_pay", "cr")]
      public static string PayBastardBlackmail(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         (int settled, int gold) = BastardBehavior.Instance.Pay(hero);

         return settled > 0
            ? $"Paid {hero.Name} {gold} denars for silence over {settled} bastard(s) — she keeps quiet, for now."
            : $"{hero.Name} holds no bastard leverage to settle.";
      }

      #region private

      /// <summary>An abstract prestige score for a clan (tier-weighted + renown + a ruling-dynasty bonus).</summary>
      private static int ClanPrestige(Clan clan)
      {
         if (clan == null) return 0;
         bool ruling = clan.Kingdom != null && clan.Kingdom.RulingClan == clan;

         return BastardPrestigePolicy.PrestigeScore(clan.Tier, (int) clan.Renown, ruling);
      }

      /// <summary>A synthetic delivered letter for the cr.letter_* test commands.</summary>
      private static PendingLetter BuildTestLetter(Hero hero, int today) => new() {
         SenderId = hero.StringId,
         SenderName = hero.Name?.ToString(),
         Reason = LetterReason.RomanticCorrespondence,
         Content = $"I find my thoughts return to you more often than I would admit. Write back, if you would. — {hero.Name}",
         SentOnDay = today - 2,
         DeliveryDay = today,
         IsDelivered = true
      };

      /// <summary>
      ///   "Day 91078 (6 days ago)" — day numbers are absolute calendar days (the
      ///   Calradian year is 84 days), which players read as gibberish without the
      ///   relative part (tester report: guessed '9' was the day and the rest hours).
      /// </summary>
      private static string DescribeDay(int gameDay)
      {
         var today = (int) CampaignTime.Now.ToDays;

         if (gameDay <= 0 || gameDay > today) return $"Day {gameDay}";
         int ago = today - gameDay;
         string rel = ago == 0 ? "today" : ago == 1 ? "yesterday" : $"{ago} days ago";

         return $"Day {gameDay} ({rel})";
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

      /// <summary>First unwed, eligible kin of the NPC's house (clan + own children) the player could marry.</summary>
      private static Hero FindEligibleCandidate(Hero npc)
      {
         if (npc == null || Hero.MainHero == null) return null;
         var pool = new List<Hero>();
         if (npc.Clan != null) pool.AddRange(npc.Clan.Heroes);
         if (npc.Children != null) pool.AddRange(npc.Children);

         return pool.FirstOrDefault(h => h is {IsAlive: true} && MarriageProspectResolver.IsMarriageable(Hero.MainHero, h) && MarriageProspectResolver.HasAuthority(npc, h));
      }

      /// <summary>The first living companion currently in the player's party (skipping any already away).</summary>
      private static Hero FirstCompanionInParty()
         => Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan) && h.PartyBelongedTo == MobileParty.MainParty && CompanionMissionBehavior.Instance?.IsAway(h.StringId) != true);

      private static NpcProfile GetOrCreateProfile(NpcMemoryCampaignBehavior behavior, Hero hero)
      {
         NpcProfile profile = behavior.Store.Get(hero.StringId);

         if (profile != null) return profile;

         profile = HeroProfileMapper.FromHero(hero);
         behavior.Store.Set(profile);

         return profile;
      }

      private static string MutateKink(List<string> args, bool add, string usage)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (args == null || args.Count == 0)
            return $"Usage: {usage}\nKinks: {ValidKinkNames()}";

         if (!Enum.TryParse(args[0], true, out Kink kink) || kink == Kink.None)
            return $"Unknown kink '{args[0]}'. Valid: {ValidKinkNames()}";

         List<string> nameArgs = args.Count > 1
            ? args.GetRange(1, args.Count - 1)
            : new List<string>();
         Hero hero = ResolveHero(nameArgs);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.Romantic?.Kinks == null)
            return $"{hero.Name} has no romantic profile — cannot modify kinks.";

         bool changed;
         if (add)
         {
            if (profile.Romantic.Kinks.Contains(kink))
            {
               changed = false;
            }
            else
            {
               profile.Romantic.Kinks.Add(kink);
               changed = true;
            }
         }
         else
         {
            changed = profile.Romantic.Kinks.Remove(kink);
         }

         behavior.Store.Set(profile);

         string state = profile.Romantic.Kinks.Count == 0
            ? "(none)"
            : string.Join(", ", profile.Romantic.Kinks);

         if (!changed)
            return $"No change — {hero.Name} {(add ? "already has" : "does not have")} {kink}. Now: {state}.";

         return $"{kink} {(add ? "added to" : "removed from")} {hero.Name}. Now: {state}.";
      }

      /// <summary>
      ///   Resolves the target hero: a name fragment if provided (case-insensitive,
      ///   excluding the player), else the current conversation NPC.
      /// </summary>
      private static Hero ResolveHero(IEnumerable<string> nameTokens)
      {
         string query = nameTokens == null
            ? ""
            : string.Join(" ", nameTokens).Trim();

         if (!string.IsNullOrEmpty(query))
            return Hero.AllAliveHeroes.FirstOrDefault(h =>
               h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

         return Hero.OneToOneConversationHero;
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

      private static bool TryContext(out NpcMemoryCampaignBehavior behavior, out string error)
      {
         behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior == null)
         {
            error = "Calradia Remembers behavior not active. Start a campaign first.";

            return false;
         }

         error = null;

         return true;
      }

      private static string ValidKinkNames()
         => string.Join(", ", Enum.GetNames(typeof(Kink)).Where(n => n != nameof(Kink.None)));

      #endregion
   }
}