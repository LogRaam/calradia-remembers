// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic.Letters;
using CalradiaRemembers.Logic.Prisoners;
using CalradiaRemembers.Verbs;
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
      ///   cr.couriers
      ///   Lists every in-flight courier: what it carries, its position, food, and how long it has sat still.
      ///   Robustness batch (ROADMAP 2026-07-20): reads a player report of a wandering courier off the console.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("couriers", "cr")]
      public static string ListCouriers(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         int today = behavior.GameStateBridge.GetCurrentGameDay();

         return CourierService.DescribeActiveCouriers(behavior.Store, today);
      }

      /// <summary>
      ///   cr.couriers_recall
      ///   Recalls every in-flight courier: parties despawn, and each letter reverts to date-based delivery so
      ///   nothing is lost. The support valve for "a courier is stuck / wandering" reports.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("couriers_recall", "cr")]
      public static string RecallCouriers(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         int recalled = CourierService.RecallAll(behavior.Store);

         return recalled == 0
            ? "No couriers in flight."
            : $"{recalled} courier(s) recalled; their letters revert to date-based delivery and still arrive.";
      }

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
      ///   cr.escort_channel [name]
      ///   Toggles and reports the ARMED ESCORT flag on a correspondent's whole thread (NpcProfile.EscortedChannel).
      ///   An escorted thread's couriers (both directions, all later legs) ride immune to bandit interception, so
      ///   this exercises the immunity without paying the tavern premium or waiting for a real dispatch.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("escort_channel", "cr")]
      public static string ToggleEscortChannel(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         profile.EscortedChannel = !profile.EscortedChannel;
         behavior.Store.Set(profile);

         return profile.EscortedChannel
            ? $"{hero.Name}'s correspondence is now ESCORTED: its couriers are immune to interception."
            : $"{hero.Name}'s correspondence is no longer escorted: its couriers are vulnerable again.";
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
         DeliverTestLetter(BuildTestLetter(hero, behavior.GameStateBridge.GetCurrentGameDay()), profile);

         return $"Delivered a test letter from {hero.Name} to the parchment window (read mode).";
      }

      /// <summary>
      ///   cr.letter_long [paragraphs] [name]
      ///   Delivers a test letter whose body is long enough to actually SCROLL (default 12 paragraphs), through the
      ///   same real delivery path as cr.letter_read. Written for the crash report of 2026-09-06 (a player crashed
      ///   as soon as they scrolled a letter): an ordinary test letter is one line, so the scroll path was never
      ///   exercised. Pair it with a high UI Scale, which is what engages the parchment's fit zoom.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_long", "cr")]
      public static string OpenLetterLong(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         var paragraphs = 12;
         var nameArgs = new List<string>(args ?? new List<string>());

         if (nameArgs.Count > 0 && int.TryParse(nameArgs[0], out int requested))
         {
            paragraphs = requested < 1 ? 1 : requested > 60 ? 60 : requested;
            nameArgs.RemoveAt(0);
         }

         Hero hero = ResolveHero(nameArgs);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         PendingLetter letter = BuildTestLetter(hero, behavior.GameStateBridge.GetCurrentGameDay());
         letter.Content = BuildLongBody(hero, paragraphs);
         DeliverTestLetter(letter, profile);

         return $"Delivered a {paragraphs}-paragraph letter from {hero.Name}. Scroll it (raise UI Scale first to engage the fit zoom).";
      }

      // Deliberately verbose and varied: enough real sentences that the parchment must scroll, rather than one long
      // unbroken word that would only exercise wrapping.
      private static string BuildLongBody(Hero hero, int paragraphs)
      {
         string[] lines = {
            "The roads south are worse than the season should allow, and every ford we tried had risen past the horses' knees.",
            "I have had word from the coast, though I cannot vouch for the man who carried it, and less for the man who paid him.",
            "There is talk in the market of levies being raised again, which the elders here greet the way they greet a hard winter.",
            "My steward insists the granary will hold until spring. My steward has been wrong before, and cheerfully so.",
            "You will remember the miller's daughter. She has married, and to a man with more sense than the last one, or so it is said.",
            "I think often of the last time we spoke, and of how little of what mattered actually got said aloud.",
            "The garrison captain asks after you by name whenever he is drunk, which is oftener than his rank permits.",
            "Two of my riders have not come back. I am told not to read anything into it yet, and I am trying to obey."
         };

         var sb = new StringBuilder();
         for (var i = 0; i < paragraphs; i++)
         {
            sb.AppendLine(lines[i % lines.Length]);
            sb.AppendLine();
         }

         sb.Append($"Write when you are able. Until then, I remain as I was. {hero.Name}");

         return sb.ToString();
      }

      /// <summary>
      ///   The scripts an LLM-written letter can really contain, one block per line, hardest last. Written as \u
      ///   ESCAPES on purpose: the source file stays pure ASCII, so no re-encoding of this .cs can quietly turn the
      ///   test data back into question marks and make a passing test meaningless. Block 1 is the exact Chinese
      ///   sentence the model returned in Gabriel's own log on 2026-09-06, which is what put us onto glyphs at all.
      /// </summary>
      private static readonly string[] GlyphBlocks = {
         "\u6211\u5199\u4fe1\u7ed9Arwa\uff0c\u544a\u8bc9\u5979\u6211\u6bd4\u9884\u60f3\u7684\u66f4\u5e38\u60f3\u8d77\u5979\u3002", // Chinese (the exact sentence the model returned in the live log)
         "\u0417\u0434\u0440\u0430\u0432\u0441\u0442\u0432\u0443\u0439\u0442\u0435, \u0434\u0440\u0443\u0433 \u043c\u043e\u0439.", // Cyrillic
         "\u039a\u03b1\u03bb\u03b7\u03bc\u03ad\u03c1\u03b1, \u03c6\u03af\u03bb\u03b5 \u03bc\u03bf\u03c5.", // Greek
         "\u00c0 la v\u00e9rit\u00e9, je n'\u00e9tais gu\u00e8re s\u00fbr de moi, ni de l'h\u00f4te.", // accented Latin
         "She said \u201cwrite to me\u201d, and I did\u2026 though it cost me \u2014 more than I admit \u2013 my pride.", // curly quotes, ellipsis, em and en dash
         "\u0645\u0631\u062d\u0628\u0627 \u064a\u0627 \u0635\u062f\u064a\u0642\u064a.", // Arabic (right-to-left)
         "Marks in the margin: \u2020 \u00a7 \u00b6 \u2665 \u00bd \u00d7 \u221e", // dagger, section, pilcrow, symbols
         "\ud83d\udde1 \ud83d\udcdc \ud83d\udc0e" // emoji: astral-plane surrogate pairs, the nastiest case
      };

      /// <summary>The tag every seeded archive letter carries, so cr.letter_history clear removes exactly those and never a real one.</summary>
      private const string ArchiveSeedTag = "(archive test)";

      /// <summary>
      ///   cr.letter_glyphs [blocks] [name]
      ///   Delivers a letter loaded with the characters a real LLM letter can contain, which an ASCII test letter
      ///   never exercises: CJK, Cyrillic, Greek, accents, curly quotes and dashes, right-to-left Arabic, symbols,
      ///   and emoji (astral-plane surrogate pairs). Written for the crash report of 2026-09-06, after Gabriel's own
      ///   log showed the model returning a whole letter summary in Chinese: a glyph missing from the font atlas
      ///   would explain BOTH reported symptoms, the crash while scrolling and the campaign font turning bolder
      ///   (a fallback face). Blocks are added hardest-last, so passing a count BISECTS which script is fatal:
      ///   cr.letter_glyphs 1 is Chinese only, 8 is everything.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_glyphs", "cr")]
      public static string OpenLetterGlyphs(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         int blocks = GlyphBlocks.Length;
         var nameArgs = new List<string>(args ?? new List<string>());

         if (nameArgs.Count > 0 && int.TryParse(nameArgs[0], out int requested))
         {
            blocks = requested < 1 ? 1 : requested > GlyphBlocks.Length ? GlyphBlocks.Length : requested;
            nameArgs.RemoveAt(0);
         }

         Hero hero = ResolveHero(nameArgs);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         var body = new StringBuilder();
         body.AppendLine("A letter in the many hands the model actually writes in:");
         body.AppendLine();

         for (var i = 0; i < blocks; i++)
         {
            body.AppendLine(GlyphBlocks[i]);
            body.AppendLine();
         }

         body.Append($"Yours, whatever alphabet finds you. {hero.Name}");

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         PendingLetter letter = BuildTestLetter(hero, behavior.GameStateBridge.GetCurrentGameDay());
         letter.Content = body.ToString();
         DeliverTestLetter(letter, profile);

         return $"Delivered a letter with {blocks} of {GlyphBlocks.Length} glyph block(s) from {hero.Name}. " +
                "Scroll it. If it crashes, halve the count to find the script that does it.";
      }

      /// <summary>
      ///   cr.letter_history [exchanges|clear] [name]
      ///   Seeds a long correspondence ARCHIVE with an NPC and opens the letter book on it, so the LEFT page's
      ///   scrollbar (the thread of past letters) can actually be exercised. The letter body has its own scrollbar,
      ///   tested by cr.letter_long; this is the other one, and the crash report of 2026-09-06 does not say which.
      ///   Each exchange is an incoming letter plus the player's reply, so it yields two thread entries.
      ///   WRITES TO THE SAVE: every seeded letter is tagged, and "cr.letter_history clear" removes exactly those.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_history", "cr")]
      public static string SeedLetterHistory(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         var exchanges = 15;
         var clearing = false;
         var nameArgs = new List<string>(args ?? new List<string>());

         if (nameArgs.Count > 0)
         {
            if (string.Equals(nameArgs[0], "clear", StringComparison.OrdinalIgnoreCase))
            {
               clearing = true;
               nameArgs.RemoveAt(0);
            }
            else if (int.TryParse(nameArgs[0], out int requested))
            {
               exchanges = requested < 1 ? 1 : requested > 100 ? 100 : requested;
               nameArgs.RemoveAt(0);
            }
         }

         Hero hero = ResolveHero(nameArgs);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (clearing)
         {
            int removed = profile.SentLetters.RemoveAll(l => l?.Content != null && l.Content.Contains(ArchiveSeedTag))
                          + profile.ReceivedPlayerLetters.RemoveAll(l => l?.Content != null && l.Content.Contains(ArchiveSeedTag));
            behavior.Store.Set(profile);

            return $"Removed {removed} seeded archive letter(s) from {hero.Name}. Real correspondence is untouched.";
         }

         int today = behavior.GameStateBridge.GetCurrentGameDay();

         for (var i = 0; i < exchanges; i++)
         {
            int day = today - (exchanges - i) * 3; // spread back through the past, oldest first

            profile.SentLetters.Add(new PendingLetter {
               SenderId = hero.StringId,
               SenderName = hero.Name?.ToString(),
               Reason = LetterReason.RomanticCorrespondence,
               Content = $"Letter {i + 1} of our correspondence. The season turns, the roads worsen, and still I write. {ArchiveSeedTag}",
               SentOnDay = day,
               DeliveryDay = day,
               IsDelivered = true,
               PlayerReply = $"My answer to your letter {i + 1}: briefly, and with more affection than the words carry. {ArchiveSeedTag}"
            });

            // Every third exchange, an unprompted letter from the player too, so both directions are represented.
            if (i % 3 != 0) continue;

            profile.ReceivedPlayerLetters.Add(new PlayerLetter {
               RecipientId = hero.StringId,
               RecipientName = hero.Name?.ToString(),
               Content = $"An unbidden note, the {i + 1}th, sent without waiting for your reply. {ArchiveSeedTag}",
               SentOnDay = day + 1,
               DeliveryDay = day + 1,
               IsDelivered = true
            });
         }

         behavior.Store.Set(profile);
         DeliverTestLetter(BuildTestLetter(hero, today), profile);

         return $"Seeded {exchanges} exchange(s) with {hero.Name} and opened the book. Scroll the LEFT page. " +
                "Undo with: cr.letter_history clear";
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
      ///   up on the next daily tick.
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
      ///   cr.letters [name]
      ///   Seeds a small dated correspondence thread with the NPC (if one does not already exist) and opens
      ///   the letter-archive thread view (<see cref="LetterArchiveManager.Open" />). The seed injects four
      ///   DELIVERED letters, both directions (NPC to player, and the player's letters to the NPC), dated to
      ///   varied PAST days so the archive shows a real chronological back-and-forth with dated headers.
      ///   Idempotent-ish: if the contact already holds a couple of archived letters, seeding is skipped and
      ///   the existing thread is simply opened, so repeated calls do not spam duplicate seeds.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letters", "cr")]
      public static string OpenLetterArchive(List<string> args)
      {
         try
         {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
               return error;

            Hero hero = ResolveHero(args);

            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, hero);
            int today = behavior.GameStateBridge.GetCurrentGameDay();

            int existing = profile.SentLetters.Count(LetterArchivePolicy.IsArchived)
                           + profile.ReceivedPlayerLetters.Count(LetterArchivePolicy.IsArchived);

            int seeded = 0;
            if (existing < 2)
            {
               seeded = SeedTestCorrespondence(profile, hero, today);
               behavior.Store.Set(profile);
            }

            LetterArchiveManager.Open(profile);

            string what = seeded > 0
               ? $"seeded {seeded} test letters (both directions, days {today - 60}/{today - 40}/{today - 20}/{today - 5})"
               : $"kept {existing} existing archived letter(s), no seed";

            return $"Letter archive opened for {hero.Name}: {what}.";
         }
         catch (Exception ex)
         {
            return $"cr.letters failed: {ex.GetType().Name}: {ex.Message}";
         }
      }

      /// <summary>
      ///   Injects a four-letter dated correspondence (both directions) marked DELIVERED so the archive
      ///   thread renders a real chronological exchange. Bodies are clearly labelled "(test)" seeds.
      ///   Returns the number of letters added.
      /// </summary>
      private static int SeedTestCorrespondence(NpcProfile profile, Hero hero, int today)
      {
         string npcName = hero.Name?.ToString();

         // Oldest first: NPC -> player (d-60), player -> NPC (d-40), NPC -> player (d-20), player -> NPC (d-5).
         profile.SentLetters.Add(new PendingLetter {
            SenderId = hero.StringId,
            SenderName = npcName,
            Reason = LetterReason.RomanticCorrespondence,
            Content = $"(test) It has been too long since we last spoke. I think of our time together often. - {npcName}",
            SentOnDay = today - 60,
            DeliveryDay = today - 57,
            DeliveredOnDay = today - 57,
            IsDelivered = true
         });

         profile.ReceivedPlayerLetters.Add(new PlayerLetter {
            RecipientId = profile.Id,
            RecipientName = profile.Name,
            Content = "(test) Your letter found me on the road. Know that you are not far from my thoughts either.",
            SentOnDay = today - 40,
            DeliveryDay = today - 37,
            IsDelivered = true,
            HasBeenRead = true
         });

         profile.SentLetters.Add(new PendingLetter {
            SenderId = hero.StringId,
            SenderName = npcName,
            Reason = LetterReason.RomanticCorrespondence,
            Content = $"(test) The season turns and still I wait for word of your return. Write soon. - {npcName}",
            SentOnDay = today - 20,
            DeliveryDay = today - 17,
            DeliveredOnDay = today - 17,
            IsDelivered = true
         });

         profile.ReceivedPlayerLetters.Add(new PlayerLetter {
            RecipientId = profile.Id,
            RecipientName = profile.Name,
            Content = "(test) A short line to say the campaign keeps me late, but I will come to you once it ends.",
            SentOnDay = today - 5,
            DeliveryDay = today - 2,
            IsDelivered = true,
            HasBeenRead = true
         });

         return 4;
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

      /// <summary>
      ///   cr.letter_tidings [name]
      ///   Runs the realm-tidings evaluation for one writer NOW: scores the world events they plausibly know,
      ///   picks the most worth-telling one they have not already relayed, and enqueues an unbidden "news of the
      ///   realm" letter into the mailbox (competing in the normal budget). Reports which event was chosen, or why
      ///   none was. Seed some conditions first with cr.worldcond if the store is empty.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_tidings", "cr")]
      public static string EvaluateRealmTidings(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         return RealmTidings.RealmTidingsBehavior.DebugEvaluate(behavior.Store, profile, hero);
      }

      /// <summary>
      ///   cr.letter_offer &lt;prisoner|service&gt; [name]
      ///   Opens a synthetic OFFER letter in read mode so the Accept / Decline affordance can be exercised without
      ///   waiting for the mailbox. "prisoner": a lord (the named one, else any solvent lord) offers to buy the
      ///   FIRST hero prisoner you hold, and Accept settles the REAL ransom (gold + transfer). "service": the named
      ///   lord offers you a place in their service, and Accept warms the bond (a light hook, no allegiance change).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_offer", "cr")]
      public static string OpenLetterOffer(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         string kind = (args != null && args.Count > 0 ? args[0] : "prisoner").Trim().ToLowerInvariant();
         var rest = args != null && args.Count > 1 ? args.GetRange(1, args.Count - 1) : new List<string>();
         Hero named = ResolveHero(rest);
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         if (kind == "service")
         {
            if (named == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            NpcProfile profile = GetOrCreateProfile(behavior, named);
            var serviceLetter = new PendingLetter {
               SenderId = named.StringId,
               SenderName = named.Name?.ToString(),
               Reason = LetterReason.ServiceOffer,
               Content = $"[TEST] Leave the lord you now serve and come to me. I would grant you a castle of your own to hold. .. {named.Name}",
               SentOnDay = today,
               DeliveryDay = today,
               IsDelivered = true
            };
            DeliverTestLetter(serviceLetter, profile);

            return $"Opened a ServiceOffer letter from {named.Name} (Accept warms the bond, light hook).";
         }

         Hero captive = BuyPrisonerVerb.PlayerHeldHeroPrisoners().FirstOrDefault();
         if (captive == null) return "You hold no hero prisoner to be offered for. Capture a lord first.";

         Hero buyer = named ?? Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != null && h.IsLord && h != Hero.MainHero && !h.IsPrisoner && h.Gold > 1000);
         if (buyer == null) return "No solvent lord available to play the buyer.";

         NpcProfile buyerProfile = GetOrCreateProfile(behavior, buyer);
         int amount = PrisonerOfferPolicy.OfferAmount(BuyPrisonerVerb.RansomAnchor(captive), true);
         var offer = new PendingLetter {
            SenderId = buyer.StringId,
            SenderName = buyer.Name?.ToString(),
            Reason = LetterReason.PrisonerOffer,
            Content = $"[TEST] I would buy {captive.Name} from you, and I offer {amount} denars for them. .. {buyer.Name}",
            OfferTargetId = captive.StringId,
            OfferAmount = amount,
            SentOnDay = today,
            DeliveryDay = today,
            IsDelivered = true
         };
         DeliverTestLetter(offer, buyerProfile);

         return $"Opened a PrisonerOffer from {buyer.Name} to buy {captive.Name} for {amount} denars (Accept settles the ransom).";
      }

      /// <summary>A synthetic delivered letter for the cr.letter_* test commands.</summary>
      /// <summary>
      ///   A test letter arrives as a real one does: held in its sender's letters, which is where the letter box looks
      ///   (Gabriel's test, 31/10/2026: the arrival notice and no letter in the box, the letter being held nowhere).
      /// </summary>
      private static void DeliverTestLetter(PendingLetter letter, NpcProfile sender)
      {
         if (sender.SentLetters != null && !sender.SentLetters.Contains(letter)) sender.SentLetters.Add(letter);
         LetterPopupManager.Show(letter, sender);
      }

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
