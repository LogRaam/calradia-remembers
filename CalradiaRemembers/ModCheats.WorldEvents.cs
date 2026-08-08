// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Chronicle;
using CalradiaRemembers.Commoners;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
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

         return $"Feed.Record -> {result}. The world-event store now holds {WorldEventStore.Instance?.Count ?? 0} event(s).\n" +
                "NOTE: this is a DEVELOPER / MODDER test of CR's inbound Feed API -- the same door OTHER MODS use to push " +
                "world events into CR. It is not a way to script your own lore in normal play.\n" +
                "Whether an NPC mentions it follows CR's normal hearsay rules: only someone who would PLAUSIBLY have heard " +
                "it (by distance, faction, and how recent it is) may bring it up, and only as hearsay when it fits the talk " +
                "-- they never recite it. To draw it out, ASK a nearby NPC about recent news. A bare test rumour with no " +
                "faction or hero tag reaches fewer people than a real, tagged event would.\n" +
                "To make ONE specific NPC remember a fact directly (no hearsay), use cr.memory_add instead -- that writes " +
                "straight into their memory.";
      }

      /// <summary>
      ///   cr.world_event &lt;what happened&gt;
      ///   A PLAYER tool (not a test): injects your own world rumour into the campaign through the same Feed door
      ///   a third-party mod uses, so NPCs may gossip it. Any kingdom named in the text is auto-tagged so its
      ///   people are likeliest to have heard it. Spreads by CR's hearsay rules (distance / faction / recency, as
      ///   hearsay when it fits) -- NOT a "make everyone know it instantly" tool; for that on ONE NPC use cr.memory_add.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("world_event", "cr")]
      public static string AddWorldEvent(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         string summary = args is {Count: > 0}
            ? string.Join(" ", args).Trim()
            : "";

         if (string.IsNullOrWhiteSpace(summary))
            return "Usage: cr.world_event <what happened>\n" +
                   "  e.g. cr.world_event A great fire gutted the Vlandian docks at Sargot.";

         // Auto-tag any kingdom named in the text so its people are the likeliest to have heard the news.
         var factionIds = new List<string>();
         var taggedNames = new List<string>();
         foreach (Kingdom k in Kingdom.All ?? Enumerable.Empty<Kingdom>())
         {
            string name = k?.Name?.ToString();
            if (k == null || k.IsEliminated || string.IsNullOrWhiteSpace(name)) continue;
            if (summary.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;

            factionIds.Add(k.StringId);
            taggedNames.Add(name);
         }

         var e = new ExternalWorldEvent {
            Id = "playerevent_" + Guid.NewGuid().ToString("N"),
            SourceMod = "PlayerConsole",
            Summary = summary,
            Magnitude = 50,
            FactionIds = factionIds,
            Lifetime = ExternalEventLifetime.Rumor
         };

         FeedResult result = Feed.Record(e);

         string tagged = taggedNames.Count > 0
            ? $"Tagged faction(s): {string.Join(", ", taggedNames)}. "
            : "No kingdom named in the text, so it spreads as general realm news. ";

         return $"Feed.Record -> {result}. {tagged}The world-event store now holds {WorldEventStore.Instance?.Count ?? 0} event(s).\n" +
                "It spreads by CR's hearsay rules: an NPC who'd PLAUSIBLY have heard it (distance, faction, recency) may " +
                "bring it up, as hearsay, when it fits the talk -- they never recite it. ASK a nearby NPC about recent news " +
                "to draw it out. To make ONE specific NPC remember a fact directly (no hearsay), use cr.memory_add instead.";
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
      ///   cr.worldcond
      ///   Force-runs the weekly world-conditions scan immediately (war looming between kingdoms, town hardship)
      ///   and reports how many events each condition recorded. The same scan runs on its own weekly cadence.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("worldcond", "cr")]
      public static string ScanWorldConditions(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         IReadOnlyList<WorldConditionsFeeder.ConditionScanResult> results = behavior.WorldConditions.RunScan();

         string report = string.Join(", ", results.Select(r => $"{r.Recorded} {r.Label}"));

         return $"World-conditions scan complete: recorded {report}.";
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
   }
}
