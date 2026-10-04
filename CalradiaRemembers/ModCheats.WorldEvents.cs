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

         // Tagged the way a bridge should tag: the realm and the place. Until 2.5.22 this fed magnitude 30 with no
         // tag at all, which is under the awareness threshold on its first day, so nobody could ever mention it.
         string realmId = Hero.MainHero?.MapFaction?.StringId;
         Settlement place = NearestSettlementToPlayer();
         var e = new ExternalWorldEvent {
            Id = "feedtest_" + Guid.NewGuid().ToString("N"),
            SourceMod = "ConsoleTest",
            Summary = summary,
            Magnitude = 60,
            FactionIds = realmId != null ? new List<string> {realmId} : new List<string>(),
            PlaceId = place?.StringId,
            Lifetime = ExternalEventLifetime.Rumor
         };

         FeedResult result = Feed.Record(e);

         return $"Feed.Record -> {result} (id {e.Id}, magnitude {e.Magnitude}, realm {realmId ?? "none"}, place {place?.Name?.ToString() ?? "none"}). "
                + $"The world-event store now holds {WorldEventStore.Instance?.Count ?? 0} event(s).\n"
                + FedEventReach.Describe(e.Magnitude, realmId != null, place != null) + "\n"
                + $"Check who has heard it: cr.heard <name> {e.Id}\n"
                + "NOTE: this is a DEVELOPER / MODDER test of CR's inbound Feed API -- the same door OTHER MODS use to push "
                + "world events into CR. A character who has heard it brings it up as hearsay when it fits the talk; ASK "
                + "about recent news to draw it out. To make ONE specific NPC remember a fact directly, use cr.memory_add.";
      }

      /// <summary>
      ///   cr.heard [name] [event-id | all]
      ///   Whether a character has heard the stored world events, and why, from the same arithmetic the prompt
      ///   gates on. Nothing is copied into a character's memory: awareness is recomputed at every prompt, so this
      ///   verdict IS the answer. Asked for by a bridge author (tashmetu, 23/09/2026) testing fed events.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("heard", "cr")]
      public static string Heard(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         var store = WorldEventStore.Instance;

         if (store == null) return "World-event store is not active.";

         List<string> tokens = args?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList() ?? new List<string>();
         string eventId = null;
         var all = false;

         if (tokens.Count > 0 && string.Equals(tokens[tokens.Count - 1], "all", StringComparison.OrdinalIgnoreCase))
         {
            all = true;
            tokens.RemoveAt(tokens.Count - 1);
         }
         else if (tokens.Count > 0 && store.GetById(tokens[tokens.Count - 1]) != null)
         {
            eventId = tokens[tokens.Count - 1];
            tokens.RemoveAt(tokens.Count - 1);
         }
         else if (tokens.Count > 1 && ResolveHero(tokens) == null && ResolveHero(tokens.Take(tokens.Count - 1)) != null)
         {
            return $"No event with id '{tokens[tokens.Count - 1]}' is in the store. It was never recorded (see the [FEED] line in "
                   + "CalradiaRemembers.log for the result), or it was retracted, or a Rumor older than "
                   + $"{WorldEventPruner.RumorWindowDays} days was pruned.";
         }

         Hero hero = ResolveHero(tokens);

         if (hero == null) return "No character found. Pass a name fragment or start a conversation first. Usage: cr.heard [name] [event-id | all]";

         var today = (int) CampaignTime.Now.ToDays;
         IReadOnlyList<(WorldEvent Event, RumorAwarenessBreakdown Verdict)> verdicts = store.ExplainAll(hero, today);
         List<WorldEvent> heard = verdicts.Where(v => v.Verdict.Knows).Select(v => v.Event).ToList();

         var sb = new System.Text.StringBuilder();
         sb.AppendLine($"{hero.Name} (realm id {hero.MapFaction?.StringId ?? "none"}), day {today}: has heard {heard.Count} of the {verdicts.Count} stored event(s). "
                       + "The news block of a prompt draws from the best 6 heard; a question naming an event's people or realm can bring up any heard one.");

         IEnumerable<(WorldEvent Event, RumorAwarenessBreakdown Verdict)> shown = eventId != null
            ? verdicts.Where(v => v.Event.Id == eventId)
            : all
               ? verdicts.Take(30)
               : verdicts.Where(v => v.Event.SourceMod != null).Take(20);

         var any = false;

         foreach ((WorldEvent e, RumorAwarenessBreakdown v) in shown)
         {
            any = true;
            string place = e.PlaceId != null
               ? ", at " + (Settlement.Find(e.PlaceId)?.Name?.ToString() ?? e.PlaceId)
               : "";
            string summary = e.Summary != null && e.Summary.Length > 120
               ? e.Summary.Substring(0, 117) + "..."
               : e.Summary;
            int rank = heard.IndexOf(e);

            sb.AppendLine($"- {e.Id} [{e.SourceMod ?? "CR"}, day {e.Day}, magnitude {e.Magnitude}{place}{(e.IsPersistentDeed ? ", deed" : "")}]: {summary}");
            sb.AppendLine("    " + v.Describe()
                          + (rank >= 0 ? $" Ranks #{rank + 1} of what they have heard." : "")
                          + (e.InvolvesPlayer ? " The player is among its ActorHeroIds, so it is the player's own deed: it reaches them as a deed of yours, not as news." : ""));
         }

         if (!any)
            sb.AppendLine(all || eventId != null
                             ? "Nothing to show."
                             : "No event fed by another mod is in the store. Add 'all' to see CR's own events too.");

         return sb.ToString().TrimEnd();
      }

      // ── The player's say over world news (IdentityCrysys, Nexus, 03/10/2026) ──────────────────────────────
      // "is there a way to remove a world event from public memory and/or a specific NPC's awareness? ... Or
      // preventing a certain event from coming up in conversation with anyone (except who it involves directly,
      // maybe)?" Hearing is by score, never a stored list, so these set a veto on the event itself
      // (RumorDiscretion). They hide the NEWS, not the facts: a marriage that exists in the game is still known
      // to the spouses through their own family lines, and a memory a character keeps is cr.forget's business.

      /// <summary>cr.world_events [words]: lists stored world events (newest first) with their ids, filtered by text.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("world_events", "cr")]
      public static string ListWorldEvents(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         var store = WorldEventStore.Instance;
         if (store == null) return "World-event store is not active.";

         string filter = args is {Count: > 0} ? string.Join(" ", args).Trim() : "";
         List<WorldEvent> found = store.All()
                                       .Where(e => filter.Length == 0 || (e.Summary ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                                       .OrderByDescending(e => e.Day).Take(25).ToList();
         if (found.Count == 0) return filter.Length == 0 ? "No world event is stored." : $"No stored world event mentions '{filter}'.";

         var sb = new System.Text.StringBuilder();
         foreach (WorldEvent e in found)
            sb.AppendLine($"- {e.Id} [day {e.Day}{(e.KnownOnlyToThoseInvolved ? ", SECRET" : "")}{(e.HiddenFromIds?.Count > 0 ? $", hidden from {e.HiddenFromIds.Count}" : "")}]: {e.Summary}");
         sb.Append("Use the id with cr.world_event_remove, cr.world_event_secret or cr.world_event_hide.");

         return sb.ToString();
      }

      /// <summary>cr.world_event_remove &lt;id&gt;: removes a world event for everyone.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("world_event_remove", "cr")]
      public static string RemoveWorldEvent(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         string id = args is {Count: > 0} ? args[0].Trim() : null;
         if (string.IsNullOrEmpty(id)) return "Usage: cr.world_event_remove <id>   (ids: cr.world_events [words])";

         return WorldEventStore.Instance?.Remove(id) == true
            ? $"World event {id} removed: no one will hear of it any more. Save the game to keep it."
            : $"No world event with id '{id}'. See cr.world_events.";
      }

      /// <summary>cr.world_event_secret &lt;id&gt; [off]: only those the event names know of it.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("world_event_secret", "cr")]
      public static string SecretWorldEvent(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         WorldEvent e = args is {Count: > 0} ? WorldEventStore.Instance?.GetById(args[0].Trim()) : null;
         if (e == null) return "Usage: cr.world_event_secret <id> [off]   (ids: cr.world_events [words])";

         bool off = args.Count > 1 && string.Equals(args[1], "off", StringComparison.OrdinalIgnoreCase);
         e.KnownOnlyToThoseInvolved = !off;
         string named = string.Join(", ", (e.ActorIds ?? new List<string>()).Concat(e.AggrievedActorIds ?? new List<string>())
                                            .Select(id => Hero.FindFirst(h => h.StringId == id)?.Name?.ToString() ?? id));

         return off
            ? $"World event {e.Id} is public again."
            : $"World event {e.Id} is now a secret, known only to: {(named.Length > 0 ? named : "no one (it names nobody)")}. Save the game to keep it.";
      }

      /// <summary>cr.world_event_hide &lt;id&gt; &lt;name&gt; [off]: keeps one character unaware of a world event.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("world_event_hide", "cr")]
      public static string HideWorldEvent(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         WorldEvent e = args is {Count: > 1} ? WorldEventStore.Instance?.GetById(args[0].Trim()) : null;
         if (e == null) return "Usage: cr.world_event_hide <id> <name> [off]   (ids: cr.world_events [words])";

         List<string> nameTokens = args.Skip(1).ToList();
         bool off = string.Equals(nameTokens.Last(), "off", StringComparison.OrdinalIgnoreCase);
         if (off) nameTokens.RemoveAt(nameTokens.Count - 1);

         // A hero id is exact where a name may have namesakes.
         Hero hero = nameTokens.Count == 1 ? Hero.FindFirst(h => h.StringId == nameTokens[0]) : null;
         hero ??= ResolveHero(nameTokens);
         if (hero == null) return "No character found by that name or id.";

         e.HiddenFromIds ??= new List<string>();
         if (off) e.HiddenFromIds.Remove(hero.StringId);
         else if (!e.HiddenFromIds.Contains(hero.StringId)) e.HiddenFromIds.Add(hero.StringId);

         return off
            ? $"{hero.Name} may hear of world event {e.Id} again."
            : $"{hero.Name} ({hero.StringId}) will not know of world event {e.Id}. Save the game to keep it.";
      }

      /// <summary>The settlement the player is in, else the nearest town, castle or village to their party.</summary>
      private static Settlement NearestSettlementToPlayer()
      {
         if (Hero.MainHero?.CurrentSettlement != null) return Hero.MainHero.CurrentSettlement;

         Vec2? pos = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty?.GetPosition2D;

         if (pos == null) return null;

         return (Settlement.All ?? Enumerable.Empty<Settlement>())
                .Where(s => s.IsTown || s.IsCastle || s.IsVillage)
                .OrderBy(s => s.GetPosition2D.DistanceSquared(pos.Value))
                .FirstOrDefault();
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

         // And the place, the same way: the longest settlement name in the text, so "Sargot" is not beaten by a
         // village whose name happens to be inside it. Without a place, distance could not carry the news at all.
         Settlement place = (Settlement.All ?? Enumerable.Empty<Settlement>())
                            .Where(s => (s.IsTown || s.IsCastle || s.IsVillage) && !string.IsNullOrWhiteSpace(s.Name?.ToString())
                                        && summary.IndexOf(s.Name.ToString(), StringComparison.OrdinalIgnoreCase) >= 0)
                            .OrderByDescending(s => s.Name.ToString().Length)
                            .FirstOrDefault();

         var e = new ExternalWorldEvent {
            Id = "playerevent_" + Guid.NewGuid().ToString("N"),
            SourceMod = "PlayerConsole",
            Summary = summary,
            Magnitude = 50,
            FactionIds = factionIds,
            PlaceId = place?.StringId,
            Lifetime = ExternalEventLifetime.Rumor
         };

         FeedResult result = Feed.Record(e);

         string tagged = taggedNames.Count > 0
            ? $"Tagged realm(s): {string.Join(", ", taggedNames)}. "
            : "No kingdom named in the text. ";
         tagged += place != null
            ? $"Placed at {place.Name}. "
            : "No settlement named in the text. ";

         return $"Feed.Record -> {result}. {tagged}The world-event store now holds {WorldEventStore.Instance?.Count ?? 0} event(s).\n"
                + FedEventReach.Describe(e.Magnitude, factionIds.Count > 0, place != null) + "\n"
                + "Name a kingdom or a settlement in the text to carry it further and longer. A character who has heard it "
                + "may bring it up, as hearsay, when it fits the talk -- they never recite it. ASK about recent news to draw it "
                + $"out, or check with cr.heard <name> {e.Id}. To make ONE specific NPC remember a fact directly, use cr.memory_add.";
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
