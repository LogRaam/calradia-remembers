// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Missions;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.happiness_tick — runs a weekly happiness pass now (idle/loved-one pressure, drift, and any departures).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("happiness_tick", "cr")]
      public static string HappinessTick(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (CompanionHappinessBehavior.Instance == null) return "Companion-happiness system is not active.";

         CompanionHappinessBehavior.Instance.RunTick();

         return "Ran a happiness tick. An unhappy companion (below the leave floor) asks for an audience; refuse it " +
                "thrice and they send a farewell letter and leave when it arrives. Requires Companion contentment ON.";
      }

      /// <summary>cr.weariness [name] — show a companion's war-weariness, band, tolerance, and retirement standing.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("weariness", "cr")]
      public static string Weariness(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         int score = WarWearinessService.Get(hero);
         int valor = hero.GetTraitLevel(DefaultTraits.Valor);
         int thr = WarWeariness.RetireThreshold(valor);
         bool invested = hero.GovernorOf != null || hero.PartyBelongedTo?.LeaderHero == hero;
         int served = WarWearinessBehavior.Instance?.DaysServed(hero) ?? 0;

         return $"{hero.Name}: weariness={score}/{WarWeariness.Max} ({WarWeariness.Band(score, valor)}), valor={valor}, " +
                $"tolerance={thr}, wantsToRetire={WarWeariness.WantsToRetire(score, valor)}, " +
                $"invested={invested} → {WarWeariness.Retirement(invested)}, steppedBack={WarWearinessService.HasSteppedBack(hero)}, " +
                $"daysServed={served}, servedLongEnough={WarWeariness.HasServedLongEnough(served)}.";
      }

      /// <summary>cr.weary &lt;value&gt; [name] — set a companion's war-weariness directly (test path).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("weary", "cr")]
      public static string SetWeariness(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (args == null || args.Count == 0 || !int.TryParse(args[0], out int value))
            return "Usage: cr.weary <value> [name]  (0..100)";

         Hero hero = ResolveHero(args.Skip(1).ToList());
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         WarWearinessService.Set(hero, value);

         return $"{hero.Name}: weariness set to {WarWearinessService.Get(hero)}. Run cr.weary_tick (or wait a weekly tick) " +
                "to see whether they ask to retire.";
      }

      /// <summary>
      ///   cr.audience [retire|grievance|lover|gratitude] [name]
      ///   Opens a companion AUDIENCE conversation directly (skips the popup): the companion speaks first, voicing
      ///   a war-weary retirement (default), a grievance, (lover/affection) confiding they wish to become your
      ///   discreet secret lover, or (gratitude/thanks) thanking you for a specific kindness. For a retirement,
      ///   grant it in your own words and the companion emits `retire`; for a lover audience, welcome it and they
      ///   emit `take_as_secret_lover` (needs adult content on + a bonded, orientation-compatible companion). The
      ///   gratitude option is a FOUND-TOPIC (unlike the other three): it delegates to the real resolver/spend-once
      ///   bookkeeping via CompanionAudienceTopicBehavior.TryForceFireGratitude and reports honestly when this
      ///   companion currently has no qualifying kindness to thank you for.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("audience", "cr")]
      public static string OpenAudience(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         if (behavior.LlmServices is not {IsAvailable: true}) return "LLM services not available. Set your OpenRouter API key first.";

         var reason = CompanionAudienceReason.Retirement;
         var forceGratitude = false;
         var nameTokens = new List<string>();
         foreach (string arg in args ?? new List<string>())
            if (arg.Equals("grievance", StringComparison.OrdinalIgnoreCase)) reason = CompanionAudienceReason.Grievance;
            else if (arg.Equals("retire", StringComparison.OrdinalIgnoreCase) || arg.Equals("retirement", StringComparison.OrdinalIgnoreCase)) reason = CompanionAudienceReason.Retirement;
            else if (arg.Equals("lover", StringComparison.OrdinalIgnoreCase) || arg.Equals("affection", StringComparison.OrdinalIgnoreCase) || arg.Equals("secret", StringComparison.OrdinalIgnoreCase)) reason = CompanionAudienceReason.SecretAffection;
            else if (arg.Equals("gratitude", StringComparison.OrdinalIgnoreCase) || arg.Equals("thanks", StringComparison.OrdinalIgnoreCase)) forceGratitude = true;
            else nameTokens.Add(arg);

         Hero hero = ResolveHero(nameTokens);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         if (forceGratitude)
         {
            if (CompanionAudienceTopicBehavior.Instance == null) return "Found-topic audience system is not active.";

            return CompanionAudienceTopicBehavior.Instance.TryForceFireGratitude(hero);
         }

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         bool landed = hero.GovernorOf != null || hero.PartyBelongedTo?.LeaderHero == hero;
         ChatManager.OpenForCompanionAudience(hero, profile, reason, landed);

         return $"Opened a {reason} audience with {hero.Name} (landed={landed}). They speak first; for a retirement, " +
                "grant it in your own words to fire `retire` (executed on close).";
      }

      /// <summary>cr.weary_tick — runs a weekly war-weariness pass now (rest, retirement requests, drag-back enforcement).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("weary_tick", "cr")]
      public static string WearinessTick(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (WarWearinessBehavior.Instance == null) return "War-weariness system is not active.";

         WarWearinessBehavior.Instance.RunTick();

         return "Ran a war-weariness tick. A spent companion asks to retire — a landless one leaves your service, a " +
                "landed one (governor / party-leader) steps back from the field. Requires Companion contentment ON.";
      }

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

      /// <summary>cr.missions — lists the errands companions are currently away on.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("missions", "cr")]
      public static string ListMissions(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         return CompanionMissionBehavior.Instance?.DescribeActive() ?? "Companion-mission system is not active.";
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

      /// <summary>The first living companion currently in the player's party (skipping any already away).</summary>
      private static Hero FirstCompanionInParty()
         => Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan) && h.PartyBelongedTo == MobileParty.MainParty && CompanionMissionBehavior.Instance?.IsAway(h.StringId) != true);

      /// <summary>
      ///   cr.audience_topics [name]
      ///   Found-topic companion audiences (player feedback, Nexus): with no name, lists every companion
      ///   travelling with you and, for each, which of the six topics (mission follow-up / companion conflict /
      ///   practical counsel / callback / rumour report / gratitude) currently qualifies and why, plus the global
      ///   cooldown state. With a name, shows that one companion's scan only. Diagnostic only, mutates nothing.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("audience_topics", "cr")]
      public static string ShowAudienceTopics(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (CompanionAudienceTopicBehavior.Instance == null) return "Found-topic audience system is not active.";

         Hero named = ResolveHero(args);

         return named != null
            ? CompanionAudienceTopicBehavior.Instance.DescribeScan(named)
            : CompanionAudienceTopicBehavior.Instance.DescribeRoster();
      }

      /// <summary>
      ///   cr.audience_fire
      ///   Force-fires the strongest found-topic audience anywhere in the party RIGHT NOW, bypassing the
      ///   global cooldown (the roll itself has no randomness to skip, a topic either qualifies or it does
      ///   not) and, unlike the real trigger, works from anywhere, not only on entering a settlement. Opens
      ///   the SAME popup a real settlement-entry offer would; refusing it costs nothing. Says so when no
      ///   companion currently has anything worth an audience (silence is the default).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("audience_fire", "cr")]
      public static string FireAudienceTopic(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (CompanionAudienceTopicBehavior.Instance == null) return "Found-topic audience system is not active.";
         if (!ModSettings.CompanionContentment) return "Companion contentment is OFF in Mod Options; the found-topic audiences are gated by the same switch.";

         return CompanionAudienceTopicBehavior.Instance.TryForceFireBest();
      }
   }
}
