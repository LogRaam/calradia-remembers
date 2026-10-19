// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Stance;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.marry_info [name]
      ///   Reports, gate by gate, whether the player could wed this hero right now and, when they could not,
      ///   WHICH gate is shut and by how much. Written after a player report (2026-07-23) where a marriage
      ///   "happened" in dialogue and not in law: the mod had no way to answer "why", and neither did the
      ///   player, who had earned the family's blessing through a quest and never learned that the BRIDE's own
      ///   regard is a separate gate (a quest's relation reward goes to the giver, not to their daughter).
      ///   Read-only: reports the live state, changes nothing. The sibling of cr.hire_info.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marry_info", "cr")]
      public static string MarriageInfo(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "Usage: cr.marry_info [name] (or talk to someone first).";

         NpcProfile profile = behavior.Store?.Get(hero.StringId);
         MarriageBlockReason reason = EncounterContextBuilder.ResolveMarriageBlockReason(hero, profile);

         int regard = profile?.ReputationWithPlayer ?? 0;
         int vanilla = Hero.MainHero?.GetRelation(hero) ?? 0;
         int need = ModSettings.MarriageRegardThreshold;
         bool blessed = MarriageApprovalBehavior.Instance?.IsApproved(hero.StringId) ?? false;

         // Ratified 2026-07-23: a blessed match below the regard bar is ON OFFER as a POLITICAL union; the
         // wedding then plants a ForcedMarriage grudge scaled to the shortfall, so the report says which kind
         // of marriage the player would actually be getting.
         bool forced = reason == MarriageBlockReason.None && ForcedMarriagePolicy.IsForcedUnion(Math.Max(regard, vanilla));
         string verdict = reason != MarriageBlockReason.None
            ? "blocked by " + reason
            : forced
               ? $"ON OFFER AS A FORCED UNION (her heart is not won: expect a resentment of {ForcedMarriagePolicy.GrudgeMagnitude(Math.Max(regard, vanilla))})"
               : "MARRIAGE IS ON OFFER (a love match)";

         return $"{hero.Name}: {verdict}. " +
                $"Personal regard {regard}/{need}, vanilla relation {vanilla}/{need} (EITHER one suffices). " +
                $"Family blessing: {(blessed ? "given" : "not given")}. " +
                $"At war: {BannerlordGameStateBridge.IsAtWarWithPlayer(hero)}. " +
                $"Prisoner: npc={hero.IsPrisoner}, player={Hero.MainHero?.IsPrisoner}. " +
                $"Player already wed: {Hero.MainHero?.Spouse != null}.";
      }

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

         // Progressive-regard state (2026-09-05): the next +1 of personal regard costs (tier + 1) warm moments, and
         // each tier relaxes by one every RegardDecayDays in-game days. Show the live tier, moments banked, and how
         // many more moments the next point needs.
         int required = RelationGate.MomentsRequiredForTier(profile.RegardResistanceTier);
         int remaining = Math.Max(0, required - profile.RegardWarmthAccrued);

         return $"{hero.Name}: relation={behavior.GameStateBridge.GetPlayerRelation(hero)}, " +
                $"captive={captive}, pacing={ModSettings.RelationWarmth}, regard-tier={profile.RegardResistanceTier} " +
                $"(next +1 needs {required} warm moment(s), {profile.RegardWarmthAccrued} banked, {remaining} to go), " +
                $"decay={ModSettings.RegardDecayDays}d/step (now {now:F1}h).";
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
      ///   cr.discoveries [name]
      ///   Lists all real DiscoveredTraits recorded on an NPC's profile, each prefixed by its
      ///   0-based index so a polluted entry (Encyclopedia audit: an entry recording a fact about
      ///   the PLAYER instead of the NPC) can be identified and removed with cr.discover_forget.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("discoveries", "cr")]
      public static string ListDiscoveries(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.DiscoveredTraits.Count == 0)
            return $"{hero.Name} has no recorded discoveries.";

         IEnumerable<string> lines = profile.DiscoveredTraits.Select((t, i) =>
            $"  [{i}] {DescribeDay(t.GameDay)} | {t.Key} | {t.Description}");

         return $"{hero.Name} discoveries ({profile.DiscoveredTraits.Count}):\n" + string.Join("\n", lines);
      }

      /// <summary>
      ///   cr.discover_forget &lt;index&gt; [name]
      ///   Removes the discovered trait at the given 0-based index (see cr.discoveries). The repair
      ///   path for a player report (Nexus): a profile already carrying a discovery about the PLAYER
      ///   instead of the NPC can be cleaned up entry-by-entry without wiping the rest.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("discover_forget", "cr")]
      public static string ForgetDiscovery(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (args == null || args.Count == 0 || !int.TryParse(args[0], out int index))
            return "Usage: cr.discover_forget <index> [name]  (use cr.discoveries to see indices)";

         List<string> nameArgs = args.Count > 1
            ? args.GetRange(1, args.Count - 1)
            : new List<string>();
         Hero hero = ResolveHero(nameArgs);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (index < 0 || index >= profile.DiscoveredTraits.Count)
            return $"Index {index} out of range, {hero.Name} has {profile.DiscoveredTraits.Count} discoverie(s). Use cr.discoveries to see them.";

         DiscoveredTrait removed = profile.DiscoveredTraits[index];
         profile.DiscoveredTraits.RemoveAt(index);
         behavior.Store.Set(profile);

         return $"{hero.Name}: removed discovery [{index}] ({removed.Key}): \"{removed.Description}\".\n" + $"{profile.DiscoveredTraits.Count} discoverie(s) remain.";
      }

      /// <summary>
      ///   cr.discover_forget_all [name]
      ///   Clears every discovered trait recorded on an NPC's profile. Use when a profile has
      ///   accumulated enough polluted entries that clearing individually is not worth it.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("discover_forget_all", "cr")]
      public static string ForgetAllDiscoveries(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int count = profile.DiscoveredTraits.Count;
         profile.DiscoveredTraits.Clear();
         behavior.Store.Set(profile);

         return $"{hero.Name}: all {count} discoverie(s) cleared.";
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

      /// <summary>
      ///   cr.stance_plot [name]
      ///   "Stance with teeth" increment 2: forces the named lord (or the conversation NPC) to launch a
      ///   REAL scheme against the PLAYER (the Plot verdict), bypassing the stance/trait evaluation so
      ///   you can watch it mature with cr.scheme_advance / cr.schemes without waiting on the weekly tick.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("stance_plot", "cr")]
      public static string ForceStancePlot(List<string> args)
      {
         var store = SchemeStore.Instance;

         if (store == null) return "Scheme system is not active.";

         Hero plotter = ResolveHero(args);

         if (plotter == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         SchemeKind kind = MBRandom.RandomInt(2) == 0
            ? SchemeKind.Slander
            : SchemeKind.Sabotage;

         return store.ForcePlot(plotter, Hero.MainHero, kind);
      }

      /// <summary>
      ///   cr.stance_intercept [name]
      ///   "Stance with teeth" increment 2: forces the named lord (or the conversation NPC) through the
      ///   PRODUCTION intercept path (the Intercept verdict). Unlike cr.intercept (which always bypasses
      ///   the guard for raw pursuit testing), this respects InterceptionBehavior's own per-lord cooldown
      ///   and its "one pursuer at a time" rule, so it may correctly report that no pursuit began.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("stance_intercept", "cr")]
      public static string ForceStanceIntercept(List<string> args)
      {
         var behavior = InterceptionBehavior.Instance;

         if (behavior == null) return "Interception system is not active.";

         Hero lord = ResolveHero(args);

         if (lord == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         return behavior.TriggerStanceIntercept(lord)
            ? $"{lord.Name} rides out to intercept you (production path, cooldown respected)."
            : $"{lord.Name} did not ride out (on cooldown, another pursuit already active, or no party to send).";
      }

      /// <summary>
      ///   cr.stance_warning [name]
      ///   "Stance with teeth" increment 2: forces the named lord (or the conversation NPC) into the
      ///   Warning verdict now, bypassing the friendly-stance gate. If a real secret scheme currently
      ///   targets the player, the queued letter NAMES the plotter; otherwise it reads as the generic
      ///   friendly warmth. Pair with cr.stance_plot on a THIRD hero first to see the named case.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("stance_warning", "cr")]
      public static string ForceStanceWarning(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int lettersBefore = profile.SentLetters.Count(l => l.Reason == LetterReason.StanceFavor);
         profile.ReputationWithPlayer = 60; // genuine friendly affection, above the Favor floor
         profile.StanceTrust = 50; // trusts the player enough to tip them off
         profile.StanceLastConsequenceDay = 0; // cooldown elapsed
         behavior.Store.Set(profile);

         bool fired = StanceConsequenceBehavior.DebugEvaluate(hero);

         NpcProfile after = behavior.Store.Get(hero.StringId);
         string latest = after?.SentLetters.LastOrDefault(l => l.Reason == LetterReason.StanceFavor)?.Content;
         int lettersAfter = after?.SentLetters.Count(l => l.Reason == LetterReason.StanceFavor) ?? lettersBefore;

         return $"{hero.Name}: fired={fired}, StanceFavor letters {lettersBefore}->{lettersAfter}\n{latest ?? "(no letter queued)"}";
      }

      /// <summary>
      ///   cr.take_lover [name]
      ///   Open relationship (Inc 3a) dev command: forces the named committed partner (or the conversation NPC)
      ///   to take a lover of her own now, driving the REAL selection through the debug seam (bypassing only the
      ///   weekly neglect roll) and reporting whether the taken lover is OPEN or SECRET. Her culture decides:
      ///   an egalitarian partner needs open terms (use open_relationship first) and takes an OPEN lover; a
      ///   harem / patriarchal culture (e.g. Aserai) takes a SECRET lover and may stray even without open terms
      ///   (that uninvited path also needs the Jealousy MCM toggle on). Refuses, never a silent no-op.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("take_lover", "cr")]
      public static string ForceTakeLover(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero partner = ResolveHero(args);

         if (partner == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = behavior.Store?.Get(partner.StringId);

         if (profile?.Romantic == null)
            return $"{partner.Name} has no romantic profile. No lover taken.";
         if (!string.IsNullOrEmpty(profile.Romantic.TakenLoverId))
         {
            Hero existing = Hero.FindFirst(h => h.StringId == profile.Romantic.TakenLoverId);

            return $"{partner.Name} already has a {(profile.Romantic.LoverIsSecret ? "secret" : "open")} lover: {existing?.Name?.ToString() ?? profile.Romantic.TakenLoverId}.";
         }

         Hero lover = OpenPartnerLover.OpenPartnerLoverBehavior.DebugTakeLover(partner);

         if (lover == null)
            return $"{partner.Name}: no lover taken. Either her culture bars it (an egalitarian partner needs open terms; a harem-culture affair without open terms needs the Jealousy toggle on) or no plausible candidate exists (needs an unwed, orientation-compatible, acquainted adult lord outside your household).";

         NpcProfile after = behavior.Store?.Get(partner.StringId);
         bool secret = after?.Romantic?.LoverIsSecret ?? false;

         return secret
            ? $"{partner.Name} has taken {lover.Name} as her SECRET lover (hidden from the player; TakenLoverId + LoverIsSecret set, NO public rumour laid). Silent this increment."
            : $"{partner.Name} has taken {lover.Name} as her OPEN lover (TakenLoverId set + a faint public rumour recorded). Silent this increment.";
      }

      /// <summary>
      ///   cr.tryst [name]
      ///   Open relationship (Inc 3b-a) dev command: forces the named partner (or the conversation NPC), who must
      ///   already carry a taken lover (use cr.take_lover first), to have a tryst with him NOW, bypassing only the
      ///   "player is away" gate and the weekly meeting roll. Runs the REAL tryst: it warms the bond, increments
      ///   the tryst counter, records a private memory, and (for a SECRET affair) rolls the rising discovery
      ///   chance that may expose it. First surfaces an OPEN lover to the player. Reports the tryst count and
      ///   whether the lover is now known / the affair discovered. Refuses, never a silent no-op.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("tryst", "cr")]
      public static string ForceTryst(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero partner = ResolveHero(args);

         if (partner == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = behavior.Store?.Get(partner.StringId);

         if (profile?.Romantic == null)
            return $"{partner.Name} has no romantic profile. Use cr.take_lover first.";
         if (string.IsNullOrEmpty(profile.Romantic.TakenLoverId))
            return $"{partner.Name} has no taken lover to meet. Use cr.take_lover first.";

         // An open lover is surfaced gently the first time; a secret affair stays hidden until discovery.
         OpenPartnerLover.OpenPartnerLoverBehavior.DebugSurfaceOpenLover(partner);

         NpcProfile after = OpenPartnerLover.OpenPartnerLoverBehavior.DebugTryst(partner);

         if (after?.Romantic == null) return $"{partner.Name}: no tryst (no lover resolved).";

         Hero lover = Hero.FindFirst(h => h.StringId == after.Romantic.TakenLoverId);
         bool secret = after.Romantic.LoverIsSecret;

         return $"{partner.Name} met {(lover?.Name?.ToString() ?? after.Romantic.TakenLoverId)} " +
                $"({(secret ? "secret" : "open")} lover): trysts={after.Romantic.LoverTrystCount}, " +
                $"knownToPlayer={after.Romantic.LoverKnownToPlayer}, affairDiscovered={after.Romantic.AffairDiscovered}.";
      }

      /// <summary>
      ///   cr.lover_child [name]
      ///   Open relationship (Inc 3c) dev command: forces the named partner (or the conversation NPC), who must
      ///   already carry a taken lover (use cr.take_lover first) and be fertile, to conceive a child BY HER LOVER
      ///   now, bypassing only the per-tryst conception roll. Runs the REAL path: the child's father is the lover
      ///   (never the player), the pregnancy is registered as a lover-child so it lands in the LOVER's line at
      ///   birth, and a SECRET affair is revealed by the pregnancy. Reports the confirmed father (read back out of
      ///   the engine's own pregnancy list), the intended clan the child will join, and the discovery state.
      ///   Refuses with a reason, never a silent no-op.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("lover_child", "cr")]
      public static string ForceLoverChild(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero partner = ResolveHero(args);

         if (partner == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = behavior.Store?.Get(partner.StringId);

         if (profile?.Romantic == null)
            return $"{partner.Name} has no romantic profile. Use cr.take_lover first.";
         if (string.IsNullOrEmpty(profile.Romantic.TakenLoverId))
            return $"{partner.Name} has no taken lover to father a child. Use cr.take_lover first.";
         if (!PregnancyService.IsEligible(partner))
            return $"{partner.Name} is not fertile right now (needs a living female of fertile age, not already pregnant nor freshly delivered, with this campaign's Life/Death Cycle enabled). No child conceived.";

         Hero lover = OpenPartnerLover.OpenPartnerLoverBehavior.DebugConceiveByLover(partner);

         if (lover == null)
            return $"{partner.Name}: no child conceived (the engine refused the conception). See the log for detail.";

         PregnancyService.TryGetPregnancyInfo(partner, out Hero father, out int daysRemaining);
         Clan intended = lover.Clan != null && lover.Clan != Clan.PlayerClan
            ? lover.Clan
            : partner.Clan != null && partner.Clan != Clan.PlayerClan
               ? partner.Clan
               : null;
         NpcProfile after = behavior.Store?.Get(partner.StringId);

         return $"{partner.Name} has conceived a child by {lover.Name}. " +
                $"Confirmed father (from the engine's pregnancy list): {father?.Name?.ToString() ?? "(none)"} " +
                $"({(father == lover ? "the LOVER" : father == Hero.MainHero ? "THE PLAYER (BUG!)" : "someone else")}), NOT the player. " +
                $"Intended clan at birth: {intended?.Name?.ToString() ?? "(the lover's line; never the player's clan)"} " +
                $"(player clan is {Clan.PlayerClan?.Name?.ToString() ?? "-"}). " +
                $"Due in ~{daysRemaining} day(s). Affair known to player: {after?.Romantic?.LoverKnownToPlayer}, discovered: {after?.Romantic?.AffairDiscovered}.";
      }

      /// <summary>
      ///   cr.close_relationship [name]
      ///   Open relationship (Inc 3b-b) dev command: forces the named committed partner (or the conversation NPC)
      ///   through the REAL bridge close_relationship action, revoking the open terms so she takes no new lovers
      ///   (an existing lover, if any, is left in place: use cr.end_affair to end the current affair). Reports the
      ///   outcome, including the refusal reason when the target is not a committed partner or holds no open terms.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("close_relationship", "cr")]
      public static string ForceCloseRelationship(List<string> args)
      {
         return Dispatch(new GameAction {Type = "close_relationship"}, args, NotableEventType.Other, "close_relationship");
      }

      /// <summary>
      ///   cr.end_affair [name]
      ///   Open relationship (Inc 3b-b) dev command: forces the named partner (or the conversation NPC) through the
      ///   REAL bridge end_affair action, ending her current affair (clears TakenLoverId and resets every
      ///   affair-tracking field, records an ended-affair memory). Reports the outcome, including the refusal
      ///   reason when the partner has no taken lover to give up.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("end_affair", "cr")]
      public static string ForceEndAffair(List<string> args)
      {
         return Dispatch(new GameAction {Type = "end_affair"}, args, NotableEventType.Other, "end_affair");
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
