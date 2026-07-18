// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
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

      /// <summary>
      ///   cr.courier [name]
      ///   Dispatches a test letter to the given NPC immediately, spawning the visible
      ///   courier party. Use to watch the courier travel the map, be intercepted, or
      ///   deliver — without going through the compose window.
      /// </summary>
      /// <summary>
      ///   cr.courier_test <settlement fragment>
      ///   Spawns a TEST courier from the named settlement's GATE, riding to the player with a stamped test
      ///   letter. Built for a player validating the gate-spawn fix across towns / ports / castles: if the
      ///   courier appears outside the walls and rides out, the spawn is good; if it sits frozen, report the
      ///   settlement. The letter delivers normally on arrival (or is waylaid like any courier on the road).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("courier_test", "cr")]
      public static string CourierTestFrom(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         string fragment = args == null ? null : string.Join(" ", args).Trim();
         if (string.IsNullOrEmpty(fragment)) return "Usage: cr.courier_test <settlement name fragment>";

         Settlement settlement = Settlement.All.FirstOrDefault(s =>
            (s.IsTown || s.IsCastle) && s.Name != null &&
            s.Name.ToString().IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
         if (settlement == null) return $"No town or castle matches '{fragment}'.";

         // Prefer a hero actually AT the settlement so the sender's identity fits; any living lord otherwise
         // (the identity is cosmetic, the spawn position is forced to the settlement's gate either way).
         Hero sender = settlement.HeroesWithoutParty?.FirstOrDefault(h => h is {IsAlive: true} && h != Hero.MainHero)
                       ?? Hero.AllAliveHeroes.FirstOrDefault(h => h.IsLord && h != Hero.MainHero);
         if (sender == null) return "No hero available to play the sender.";

         NpcProfile profile = GetOrCreateProfile(behavior, sender);
         int today = behavior.GameStateBridge.GetCurrentGameDay();
         var letter = new PendingLetter {
            SenderId = sender.StringId,
            SenderName = sender.Name?.ToString(),
            Reason = LetterReason.TournamentVictory,
            Content = $"[COURIER TEST] This letter was dispatched from the gate of {settlement.Name} on day {today}. " +
                      "If you are reading it, the courier escaped the walls and reached you.",
            SentOnDay = today,
            DispatchDay = today,
            DeliveryDay = today + 15, // far date fallback so the visible courier does the delivering
            IsDelivered = false
         };
         profile.SentLetters.Add(letter);
         behavior.Store.Set(profile);

         bool spawned = CourierService.TrySpawnReplyCourier(letter, sender, settlement.GatePosition.ToVec2());

         return spawned
            ? $"Test courier spawned at the GATE of {settlement.Name} ({(settlement.IsTown ? (settlement.HasPort ? "port town" : "town") : "castle")}), riding to you. Party: {letter.CourierPartyId}."
            : $"Spawn FAILED at {settlement.Name} — report this settlement.";
      }

      [CommandLineFunctionality.CommandLineArgumentFunction("courier", "cr")]
      public static string DispatchCourier(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();
         (bool _, int days) = PlayerLetterService.CreateAndQueue(
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
      ///   cr.courier_waylay [name]
      ///   Dispatches a courier and immediately waylays it (as a catching bandit band would), so the loss
      ///   handling and the "Send again" Resend prompt can be seen on demand without chasing anything on the map.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("courier_waylay", "cr")]
      public static string DispatchWaylaidCourier(List<string> args)
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
            Content = "A test letter meant to be waylaid, then resent.",
            SentOnDay = today,
            DeliveryDay = today + 8
         };
         profile.ReceivedPlayerLetters.Add(letter);

         Hero recipient = Hero.FindFirst(h => h.StringId == profile.Id);
         bool spawned = CourierService.TrySpawnCourier(letter, recipient, 2.0f, false);
         behavior.Store.Set(profile);

         if (!spawned) return $"Courier spawn FAILED for {hero.Name} (date fallback active).";

         string result = CourierService.WaylayCourierNow(letter.CourierPartyId);

         return $"Courier to {hero.Name} dispatched, then {result}";
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
      ///   cr.letter_where [name]
      ///   Whereabouts grounding test (player-reported bug: a correspondent kept writing as if still in a
      ///   town both parties had left). Plants a memory placing the last meeting in a DIFFERENT settlement
      ///   than where the NPC currently is, then generates a letter and logs, to CalradiaRemembers.log:
      ///   the planted (stale) location, the NPC's REAL resolved whereabouts, and the full letter body.
      ///   The letter should ground itself to the real location (or the field), never to the planted town.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_where", "cr")]
      public static string LetterWhereaboutsTest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your API key / model in Mod Options first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         string realWhere = LetterGenerationService.WhereaboutsFor(profile) ?? "(uncertain, the guard will apply)";

         Settlement decoy = PickDecoySettlement(hero);
         string decoyName = decoy?.Name?.ToString() ?? "Dunglanys";

         // Plant a memory that names a place the NPC is NOT at right now, exactly the stale-location trap.
         profile.Events.Add(new NotableEvent(today - 5, NotableEventType.Other,
            $"We last spoke together within the walls of {decoyName}, some days ago."));
         behavior.Store.Set(profile);

         ModLog.Info($"[LETTER WHERE] {hero.Name}: planted a memory of {decoyName}; real whereabouts = {realWhere}. Generating a letter...");

         LetterGenerationService.GenerateForDiagnostic(
            profile, LetterReason.RomanticCorrespondence,
            "A test letter to check the writer's sense of place.",
            behavior.LlmServices,
            (fedWhereabouts, content) =>
            {
               ModLog.Info($"[LETTER WHERE] {hero.Name} whereabouts fed to the prompt: {fedWhereabouts ?? "(none, stale-location guard active)"}");
               ModLog.Info($"[LETTER WHERE] planted (stale) location that must NOT be claimed as current: {decoyName}");
               ModLog.Info($"[LETTER WHERE] letter body:\n{content ?? "(generation failed)"}");
            });

         return $"{hero.Name}: planted a memory of {decoyName}; real location = {realWhere}. " +
                "Letter is generating (async). Watch CalradiaRemembers.log for the [LETTER WHERE] lines.";
      }

      /// <summary>A town the hero is NOT currently in, to plant as a stale-location decoy in memory.</summary>
      private static Settlement PickDecoySettlement(Hero hero)
      {
         Settlement current = hero?.CurrentSettlement;
         var towns = Settlement.All?
                               .Where(s => s != null && s.IsTown && s != current)
                               .ToList();

         if (towns == null || towns.Count == 0) return null;

         return towns[MBRandom.RandomInt(towns.Count)];
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
   }
}
