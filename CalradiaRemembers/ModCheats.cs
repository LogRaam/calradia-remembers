// Code written by Gabriel Mailhot, 14/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Services;
using CalradiaRemembers.Witnesses;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
         return "Armed: your next escape attempt in a captive scene will succeed. " +
                "Type an escape attempt in the scene to trigger it.";
      }

      /// <summary>
      ///   cr.preg [name]
      ///   Forces a captive conception for the female player (bypasses the 15% roll) so the
      ///   pregnancy → delivery path can be validated without waiting on a real scene. With a
      ///   [name], that hero is the father (a lord captor); with none, the shared hidden
      ///   "Unknown" father is used (the bandit case). The child lands in the player's clan.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("preg", "cr")]
      public static string ForcePregnancy(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         bool named = args != null && args.Count > 0;
         Hero father = named ? ResolveHero(args) : null;
         if (named && father == null) return $"No hero matched \"{string.Join(" ", args)}\".";

         string note = CaptivePregnancyService.TryConceive(father, force: true);
         if (note == null)
            return "Conception did not occur — the player must be a female of fertile age (18–45), " +
                   "not already pregnant. Check CalradiaRemembers.log for engine details.";

         return note + (father == null ? " [father: shared Unknown]" : "");
      }

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
               h != Hero.MainHero
               && h.Name != null
               && h.Name.ToString().IndexOf(lastToken, StringComparison.OrdinalIgnoreCase) >= 0);
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
         if (Jealousy.JealousyBehavior.Instance == null)
            return "Jealousy system is not active.";

         string nameFragment = args != null && args.Count > 0 ? string.Join(" ", args).Trim() : null;
         Hero courted = !string.IsNullOrWhiteSpace(nameFragment)
            ? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.Name != null
                 && h.Name.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
            : Hero.OneToOneConversationHero;

         if (courted == null)
            return "Usage: cr.jealousy [name]\n" +
                   "  Resolves and fires a jealousy reaction as if the player romanced this NPC.";

         return Jealousy.JealousyBehavior.Instance.DebugForce(courted, Logic.RomanticActTier.Flirt);
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

         string text = WorldEvents.ChronicleWriter.Render();
         string path = WorldEvents.ChronicleWriter.WriteToLog();
         return path != null ? $"{text}\n\n(written to {path})" : text;
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

         Chronicle.ChronicleManager.Open();
         return "Chronicle viewer opened.";
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

         return Chronicle.ChronicleIlluminationService.Extend();
      }

      /// <summary>cr.chronicle_rewrite — redo the whole illuminated chronicle (e.g. after editing the style file).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("chronicle_rewrite", "cr")]
      public static string RewriteChronicle(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error))
            return error;

         return Chronicle.ChronicleIlluminationService.RewriteAll();
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
         if (Jealousy.JealousyBehavior.Instance == null)
            return "Jealousy system is not active.";

         return Jealousy.JealousyBehavior.Instance.DebugCompetition();
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
            h != Hero.MainHero
            && h.IsAlive
            && h.Name?.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0);

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

      // ── Sprint 12d: player letter compose ─────────────────────────────────

      /// <summary>
      ///   cr.compose [name]
      ///   Opens the letter compose window for the given NPC. Primary test path for
      ///   Sprint 12d — validates the compose UI and delivery pipeline without
      ///   requiring a tavernkeeper conversation.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("compose", "cr")]
      public static string ComposeLetter(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         ChatManager.OpenForCompose(profile);

         return $"Compose window opened for {hero.Name}.";
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
      ///   cr.recruit_info [name]
      ///   Shows the lord-defection verdict for an NPC — the gathered gate facts and whether
      ///   join_clan would be taught. Diagnostic only; mutates nothing.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("recruit_info", "cr")]
      public static string ShowRecruitInfo(List<string> args)
      {
         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Logic.LordRecruitmentFacts facts = GameState.LordRecruitmentFactsBuilder.Build(hero);
         if (facts == null)
            return $"{hero.Name} can never be a defection candidate (not a free lord of another clan).";

         Logic.LordRecruitmentBlock verdict = Logic.LordRecruitment.Evaluate(facts);
         return $"{hero.Name} — join_clan: {(verdict == Logic.LordRecruitmentBlock.None ? "ELIGIBLE" : "blocked: " + verdict)}\n" +
                $"  relation {facts.Relation} (need >= {Logic.LordRecruitment.MinRelation}), " +
                $"player tier {facts.PlayerClanTier} vs lord tier {facts.LordClanTier}\n" +
                $"  vulnerable: landless={facts.LordIsLandless}, clanWeak/destroyed={facts.LordClanWeakOrDestroyed}, " +
                $"hasLiege={facts.HasLiege} (rel {facts.RelationWithLiege}, need <= {Logic.LordRecruitment.LiegeResentmentThreshold})";
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

         var outcome = behavior.GameStateBridge.ExecuteAction(
            new NpcMemoryService.Core.Models.GameAction { Type = "join_clan" }, hero, null);

         return outcome?.Message ?? $"join_clan on {hero.Name} did nothing (see CalradiaRemembers.log).";
      }

      /// <summary>
      ///   cr.schemes
      ///   Lists the feud schemes currently in motion (plotter → target, kind, state, progress).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("schemes", "cr")]
      public static string ListSchemes(List<string> args)
      {
         var store = WorldEvents.SchemeStore.Instance;
         if (store == null) return "Scheme system is not active.";

         var active = store.Active;
         if (active.Count == 0) return "No schemes are afoot.";

         var sb = new System.Text.StringBuilder();
         sb.AppendLine($"{active.Count} scheme(s) afoot:");
         foreach (Logic.Scheme s in active)
         {
            string plotter = Hero.FindFirst(h => h.StringId == s.PlotterId)?.Name?.ToString() ?? s.PlotterId;
            string target  = Hero.FindFirst(h => h.StringId == s.TargetId)?.Name?.ToString() ?? s.TargetId;
            sb.AppendLine($"  {plotter} → {target} | {s.Kind} | {s.State} | progress {s.Progress}");
         }
         return sb.ToString().TrimEnd();
      }

      /// <summary>
      ///   cr.scheme [name]
      ///   Force-plots a scheme: the named hero (or the current conversation NPC) plots against their
      ///   most-hated rival lord. Use cr.scheme_advance to step it, cr.schemes to watch.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme", "cr")]
      public static string ForceScheme(List<string> args)
      {
         var store = WorldEvents.SchemeStore.Instance;
         if (store == null) return "Scheme system is not active.";

         Hero plotter = ResolveHero(args);
         if (plotter == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Hero target = Hero.AllAliveHeroes
            .Where(h => h != plotter && h != Hero.MainHero && h.IsLord)
            .OrderBy(h => h.GetRelation(plotter))
            .FirstOrDefault();
         if (target == null) return $"Found no rival lord for {plotter.Name} to plot against.";

         var kind = MBRandom.RandomInt(2) == 0 ? Logic.SchemeKind.Slander : Logic.SchemeKind.Sabotage;
         return store.ForcePlot(plotter, target, kind);
      }

      /// <summary>
      ///   cr.scheme_advance
      ///   Advances every active scheme by one tick (no spawn) — to test the lifecycle without
      ///   waiting for daily ticks. Watch the result with cr.schemes.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme_advance", "cr")]
      public static string AdvanceSchemes(List<string> args)
      {
         var store = WorldEvents.SchemeStore.Instance;
         if (store == null) return "Scheme system is not active.";

         int before = store.Active.Count;
         store.DebugAdvance();
         return $"Advanced schemes one tick ({before} → {store.Active.Count} still active). See cr.schemes / story log.";
      }

      /// <summary>
      ///   cr.scheme_assist [name]
      ///   Force the player to abet the named plotter's secret scheme (bypasses the LLM). The plotter
      ///   must regard you enough and the plot must be secret — set relation first with cr.relation.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scheme_assist", "cr")]
      public static string ForceSchemeAssist(List<string> args)
      {
         var store = WorldEvents.SchemeStore.Instance;
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
         var store = WorldEvents.SchemeStore.Instance;
         if (store == null) return "Scheme system is not active.";

         Hero target = ResolveHero(args);
         if (target == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         string result = store.TargetHeeds(target);
         return result ?? $"There is no secret plot against {target.Name} to warn of.";
      }

      /// <summary>
      ///   cr.intercept [name]
      ///   Forces the named lord (with a party on the map) to ride out and intercept the player,
      ///   bypassing the eligibility roll — to test the pursuit without waiting for the daily tick.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("intercept", "cr")]
      public static string ForceIntercept(List<string> args)
      {
         var behavior = Interception.InterceptionBehavior.Instance;
         if (behavior == null) return "Interception system is not active.";

         Hero lord = ResolveHero(args);
         if (lord == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         return behavior.DebugForceIntercept(lord);
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

         string block = Stance.StanceService.Describe(p) ?? "(wholly neutral)";
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
         if (args == null || args.Count < 3
             || !int.TryParse(args[0], out int t) || !int.TryParse(args[1], out int r) || !int.TryParse(args[2], out int f))
            return "Usage: cr.stance_set <trust> <respect> <fear> [name]";

         string nameFragment = args.Count > 3 ? string.Join(" ", args.GetRange(3, args.Count - 3)).Trim() : null;
         Hero hero = !string.IsNullOrWhiteSpace(nameFragment)
            ? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.Name != null
                 && h.Name.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
            : Hero.OneToOneConversationHero;
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile p = GetOrCreateProfile(behavior, hero);
         p.StanceTrust   = Logic.StanceGate.ClampAxis(Logic.StanceAxis.Trust, t);
         p.StanceRespect = Logic.StanceGate.ClampAxis(Logic.StanceAxis.Respect, r);
         p.StanceFear    = Logic.StanceGate.ClampAxis(Logic.StanceAxis.Fear, f);
         behavior.Store.Set(p);

         string block = Stance.StanceService.Describe(p) ?? "(wholly neutral)";
         return $"{hero.Name} stance set — Trust {p.StanceTrust}, Respect {p.StanceRespect}, Fear {p.StanceFear}\n{block}";
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

         bool eligible = hero.IsAlive
                         && !hero.IsPrisoner
                         && hero.Occupation == Occupation.Wanderer
                         && hero.CompanionOf == null
                         && hero.PartyBelongedTo == null
                         && clan != null
                         && companions < clan.CompanionLimit;

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

         double cooldownHours = Logic.RelationGate.CooldownHoursFor(ModSettings.RelationWarmth);
         string lastGain = profile.LastRelationGainHour.HasValue
            ? $"{profile.LastRelationGainHour.Value:F1}h"
            : "never";
         string cooldown = profile.LastRelationGainHour.HasValue
            ? $"{Math.Max(0.0, cooldownHours - (now - profile.LastRelationGainHour.Value)):F1}h remaining"
            : "ready";

         return $"{hero.Name}: relation={behavior.GameStateBridge.GetPlayerRelation(hero)}, " + $"captive={captive}, warmth={ModSettings.RelationWarmth}, lastGain={lastGain}, cooldown={cooldown} (now {now:F1}h).";
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
         if (behavior.LlmServices == null || !behavior.LlmServices.IsAvailable)
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
         if (behavior.LlmServices == null || !behavior.LlmServices.IsAvailable)
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
            "bandit_queen_test", MBRandom.RandomInt(), "Brigands", isFemaleOverride: true);
         CharacterObject queenFace = CaptiveEncounterService.ResolveFemaleBanditFace();
         ChatManager.OpenForCaptiveEncounter(null, captor, witnesses, collective, CaptiveSceneIntent.PersonalDesire, queenFace);

         string warn = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: set Adult Content Level to Hardcore in MCM for the CNC / female-aggressor rules.)"
            : "";

         return $"Female-chief bandit scene opened with {captor.Name} [spoil{(collective ? ", collective" : "")}, " +
                $"player {(playerGender == true ? "female" : "male")}].{warn}";
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

         if (behavior.LlmServices == null || !behavior.LlmServices.IsAvailable)
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
      ///   cr.letter_reply [name]
      ///   Opens the letter-REPLY window directly with a synthetic received letter — the exact context
      ///   of the close-on-reply crash, instantly (no delivery wait, no LLM). Type a reply and send:
      ///   the window close is the test. Must be on the campaign map (where the deferred-close fix runs).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_reply", "cr")]
      public static string OpenLetterReply(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         var letter = new PendingLetter
         {
            SenderId    = hero.StringId,
            SenderName  = hero.Name?.ToString(),
            Reason      = LetterReason.RomanticCorrespondence,
            Content     = $"I find my thoughts return to you more often than I would admit. Write back, if you would. — {hero.Name}",
            SentOnDay   = today - 2,
            DeliveryDay = today,
            IsDelivered = true
         };

         ChatManager.OpenForLetterReply(profile, hero, letter);
         return $"Opened a letter-reply window from {hero.Name}. Type a reply, send, and watch the close — that is the crash test.";
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
               h != Hero.MainHero
               && h.Name != null
               && h.Name.ToString().IndexOf(lastToken, StringComparison.OrdinalIgnoreCase) >= 0);
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
         string arenaNote = arenaRank > 0 && arenaRank <= 10
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
            new DiscoveredTrait {Key = "orientation", Description = $"{subj} seems drawn to {other}. (test seed)", GameDay = day},
            new DiscoveredTrait {Key = "archetype", Description = $"{subj} moves with the quiet confidence of someone who knows {poss} worth. (test seed)", GameDay = day},
            new DiscoveredTrait {Key = "preference_dominant", Description = $"{subj} prefers to lead in {poss} relationships. (test seed)", GameDay = day}
         };

         foreach (DiscoveredTrait seed in seeds)
         {
            bool exists = profile.DiscoveredTraits.Exists(t =>
               string.Equals(t.Key, seed.Key, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
               profile.DiscoveredTraits.Add(seed);
               added++;
            }
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

      [CommandLineFunctionality.CommandLineArgumentFunction("take_gold", "cr")]
      public static string TakeGold(List<string> args)
         => SimulateGold("take_gold", args, "cr.take_gold <amount> [name]");

      [CommandLineFunctionality.CommandLineArgumentFunction("test_llm", "campaign")]
      public static string TestLlm(List<string> args)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior == null)
            return "Calradia Remembers behavior not active. Start a campaign first.";

         if (behavior.LlmServices == null || !behavior.LlmServices.IsAvailable)
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

         if (behavior.LlmServices == null || !behavior.LlmServices.IsAvailable)
            return "LLM services not available. Check OPENROUTER_API_KEY / model settings.";

         Settlement settlement = null;

         if (args != null && args.Count > 0)
         {
            string query = string.Join(" ", args).Trim();
            settlement = TaleWorlds.CampaignSystem.Settlements.Settlement.All
               .FirstOrDefault(s => s.Name != null
                  && s.Name.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            if (settlement == null)
               return $"No settlement found matching '{query}'.";
         }
         else
         {
            settlement = Hero.MainHero?.CurrentSettlement;
         }

         if (settlement == null)
            return "No settlement found. Enter a town or castle, or pass a name: cr.rumor Pravend";

         var knowledge = Commoners.CommonsKnowledgeBuilder.Build(settlement);
         var profile   = Commoners.CommonsProfileFactory.Create(settlement);

         Chat.ChatManager.OpenForCommoner(profile, knowledge);

         return $"Opening commoner chat in {settlement.Name} — speak to {profile.Name}.";
      }

      #region private

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

         return pool.FirstOrDefault(h => h != null
                                         && h.IsAlive
                                         && MarriageProspectResolver.IsMarriageable(Hero.MainHero, h)
                                         && MarriageProspectResolver.HasAuthority(npc, h));
      }

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
               h != Hero.MainHero
               && h.Name != null
               && h.Name.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

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