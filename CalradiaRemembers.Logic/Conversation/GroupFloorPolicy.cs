// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 7 of the group-scene plan, the group engine (docs/design/group-chat/integration-plan-2026-11-28.md): in the Dramaturge
// mode, who speaks next in an ensemble scene is decided by the floor judge (SDK FloorJudge) and the turn arbiter (TurnArbiter),
// written 26/11/2026 and "not wired yet", in place of the fixed order of RunEnsembleTurnAsync with EnsembleFloorPolicy's one
// answer to a hanging question. Gabriel's design (26/11/2026): "speech is exclusive, intention is concurrent; a reaction is not
// a reply; the one addressed has a priority, not a monopoly", intentions re-judged after every line. NOT A/B-validated: it runs
// only in the Dramaturge mode, and everywhere else the ensemble turn is exactly as it was.
//
// This file holds the pure half the simulator's RunAsync loop held (GroupConversationSimulation): the bids built from what
// the game knows (who the line was put to, who was named, rank, how long since each spoke, who was cut off), the cap on
// characters' lines per player line, rank as standing, and which reaction is worth showing. The engine glue is
// ChatViewModel.GroupFloor.cs.
//
// What the simulator had and the game does not: a per-listener inner life. So its "telling" reactions (a change of tactic, of
// aim) cannot be chosen here, and only the first sign a character gives in the scene is shown; and Bold/Timid temperaments are
// not read (the mod has no such concept yet; inferring them from Valor was judged a decision for Gabriel), so both stay false.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Prompts;

#endregion

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>The facts the group engine is switched on by.</summary>
   public sealed class GroupFloorGateFacts
   {
      /// <summary>The MCM "Chat Composition" says Dramaturge.</summary>
      public bool DramaturgeMode { get; init; }

      /// <summary>The in-chat switch holds the MCM mode's position (Enhanced/Dramaturge), not Roleplay or NSFW.</summary>
      public bool SwitchOnTheMcmMode { get; init; }
   }

   /// <summary>One listener of the line just spoken, as the game and the judge know them this round.</summary>
   public sealed class GroupFloorListener
   {
      /// <summary>The character's hero id.</summary>
      public string Id { get; init; }

      /// <summary>Their standing in the room, 0 to 4 (<see cref="GroupFloorPolicy.StandingOf" />).</summary>
      public int Standing { get; init; }

      /// <summary>The judge's verdict for them, or null when they were not judged this round (nothing to say).</summary>
      public FloorJudgeVerdict Verdict { get; init; }

      /// <summary>Lines spoken by others since they last spoke.</summary>
      public int TurnsSilent { get; init; }

      /// <summary>Their name (a distinctive part of it) appears in the line: they are spoken about.</summary>
      public bool Named { get; init; }

      /// <summary>The reaction last shown for them in this beat, or null.</summary>
      public string LastShownReaction { get; init; }

      /// <summary>The tie-break drawn for them this round (lower wins).</summary>
      public int TieBreak { get; init; }
   }

   /// <summary>The round the bids are built for: the line just spoken and who heard it.</summary>
   public sealed class GroupFloorRound
   {
      /// <summary>Everyone present who did not speak the line.</summary>
      public IReadOnlyList<GroupFloorListener> Listeners { get; init; } = new List<GroupFloorListener>();

      /// <summary>Who spoke the line: a hero id, or null for the player.</summary>
      public string LastSpeakerId { get; init; }

      /// <summary>The line was the player's.</summary>
      public bool PlayerSpokeLast { get; init; }

      /// <summary>
      ///   Who the line was put to: for the player, the one the chat resolved as addressed (the leader of the beat); for a
      ///   character, the one their line calls on by name; null for the room.
      /// </summary>
      public string AddressedId { get; init; }

      /// <summary>The one the line calls on by name (a vocative or a stage direction to them), or null.</summary>
      public string CalledOnId { get; init; }

      /// <summary>The standing (0 to 4) of whoever spoke the line, the player included.</summary>
      public int LastSpeakerStanding { get; init; } = -1;

      /// <summary>The one cut off last round, owed the next chance, or null.</summary>
      public string CutOffId { get; init; }
   }

   /// <summary>What decides whether one reaction is narrated before the next line.</summary>
   public sealed class GroupReactionFacts
   {
      /// <summary>The referee's decision for this round (its reactors, already ordered and filtered for repeats).</summary>
      public FloorDecision Decision { get; init; }

      /// <summary>Lines since a reaction was last narrated (1 or more means the last line had none).</summary>
      public int LinesSinceReaction { get; init; }

      /// <summary>Who has already given a sign in this scene.</summary>
      public IReadOnlyCollection<string> ShownEver { get; init; } = new string[0];
   }

   /// <summary>The group engine's rules: the gate, the bids, the cap, standing and the reaction. Pure.</summary>
   public static class GroupFloorPolicy
   {
      /// <summary>
      ///   How long one judge call may take (the interpreter client, in parallel for every listener judged) before the round is
      ///   decided without it. The simulator's judge answered in about a second; a turn must not hang on it.
      /// </summary>
      public const int JudgeTimeoutSeconds = 8;

      /// <summary>How long the narrated reaction may take on the prose client before it is skipped (it is a garnish, never a wait).</summary>
      public const int ReactionTimeoutSeconds = 8;

      /// <summary>True only in the Dramaturge mode with the switch on it; every other mode keeps the fixed ensemble order.</summary>
      public static bool Runs(GroupFloorGateFacts facts) => facts != null && facts.DramaturgeMode && facts.SwitchOnTheMcmMode;

      /// <summary>
      ///   Characters' lines one player line may draw: one, plus the MCM "NPC Cross-Talk" rounds, never past the arbiter's own
      ///   ceiling (<see cref="TurnArbiter.NpcLinesCeiling" />) and never zero (a player line always gets its answer).
      /// </summary>
      public static int LineCap(int crossTalkRounds) => Math.Max(1, Math.Min(TurnArbiter.NpcLinesCeiling, 1 + Math.Max(0, crossTalkRounds)));

      /// <summary>
      ///   Standing in the room (TurnArbiter's 0 to 4), from the one rank reader (Stature): a ruler 4, a great house 3, landed
      ///   nobility 2, an established house 1, anyone else 0. A companion serves the player, whatever the clan's tier: 0.
      /// </summary>
      public static int StandingOf(Stature stature, bool servesThePlayer)
      {
         if (servesThePlayer) return 0;
         if (stature.IsRuler) return 4;
         if (stature.IsGreatHouse) return 3;
         if (stature.IsLandedNoble) return 2;

         return stature.IsEstablished ? 1 : 0;
      }

      /// <summary>
      ///   Lines spoken by others since <paramref name="id" /> last spoke, read from the speakers of the scene in order (oldest
      ///   first; null for the player). Never spoke: every line counts.
      /// </summary>
      public static int TurnsSilent(IReadOnlyList<string> speakerIds, string id)
      {
         var n = 0;
         for (int i = (speakerIds?.Count ?? 0) - 1; i >= 0; i--)
         {
            if (speakerIds[i] == id) return n;
            n++;
         }

         return n;
      }

      /// <summary>
      ///   The bids for the referee, as the simulator built them: put to by name means asked by name; the addressee of the line
      ///   is addressed; one named but not addressed is spoken about; the one who spoke the line bids last with SpokeLast.
      /// </summary>
      public static List<FloorBid> BuildBids(GroupFloorRound round)
      {
         var bids = new List<FloorBid>();
         if (round == null) return bids;

         foreach (GroupFloorListener l in round.Listeners ?? new List<GroupFloorListener>())
         {
            if (l == null || string.IsNullOrEmpty(l.Id)) continue;

            bool askedByName = l.Id == round.CalledOnId;
            bool addressed = askedByName || l.Id == round.AddressedId;
            bids.Add(new FloorBid {
               Id = l.Id, Intent = l.Verdict?.Intent ?? JudgedIntent.None, Reaction = l.Verdict?.Reaction,
               Addressed = addressed, AskedByName = askedByName, Targeted = !addressed && l.Named,
               Standing = l.Standing, TurnsSilent = l.TurnsSilent, WasCutOff = l.Id == round.CutOffId,
               LastSpeakerStanding = round.LastSpeakerStanding, TieBreak = l.TieBreak, LastShownReaction = l.LastShownReaction
            });
         }

         bids.Add(new FloorBid {Id = round.PlayerSpokeLast ? "_player" : round.LastSpeakerId, SpokeLast = true});

         return bids;
      }

      /// <summary>
      ///   The one reaction worth narrating before the next line, or null (the simulator's measured rule, 28/11/2026: with every
      ///   reaction shown the scenes repeated, with none they lost subtext; one at most a line, never on two lines running, and
      ///   only one that tells the reader something). Without a per-listener inner life, what tells is the first sign a
      ///   character gives in the scene. Never the one about to speak.
      /// </summary>
      public static string PickReaction(GroupReactionFacts facts)
      {
         if (facts?.Decision == null || facts.LinesSinceReaction < 1) return null;

         IReadOnlyCollection<string> shown = facts.ShownEver ?? new string[0];

         return facts.Decision.ReactorIds.FirstOrDefault(id => id != facts.Decision.SpeakerId && !shown.Contains(id));
      }

      /// <summary>One line for the conversation log: each bid and the referee's decision.</summary>
      public static string Describe(IReadOnlyList<FloorBid> bids, FloorDecision decision, Func<string, string> nameOf)
      {
         string Name(string id) => nameOf?.Invoke(id) ?? id;
         string each = string.Join("; ", (bids ?? new List<FloorBid>()).Where(b => b != null && !b.SpokeLast).Select(b =>
            $"{Name(b.Id)} {b.Intent.ToString().ToLowerInvariant()}{(TurnArbiter.EffectiveIntent(b) != b.Intent ? "->" + TurnArbiter.EffectiveIntent(b).ToString().ToLowerInvariant() : "")}"
            + $"{(b.AskedByName ? " [called on]" : b.Addressed ? " [to]" : "")}{(b.Targeted ? " [about]" : "")}{(b.Reaction != null ? " (" + b.Reaction + ")" : "")}"));
         string what = decision == null ? "no decision"
            : decision.Outcome == FloorOutcome.Speak ? Name(decision.SpeakerId) + " speaks" + (decision.CutOffId != null ? ", cutting off " + Name(decision.CutOffId) : "")
            : decision.Outcome == FloorOutcome.Silence ? "nobody speaks" : "the floor goes back to the player";

         return $"{(each.Length == 0 ? "no listener" : each)} => {what}";
      }
   }

   /// <summary>The narrated reaction's prompt and the reading of its line (the simulator's NarrationPrompt, cut to one reaction). Pure.</summary>
   public static class GroupReactionPrompt
   {
      /// <summary>The narration writer's instructions.</summary>
      public const string System =
         "You write ONE short line of narration for a scene in a medieval world, present tense, third person, no dialogue, no asterisks."
         + " Show the named reaction as a small, specific gesture of that person, different from any gesture already in the scene."
         + " Show what the person LETS SHOW: a feeling they hide leaks only through one small detail, or not at all."
         + " A deliberate act (pouring wine, turning away, a smile) often says more than a clenched jaw."
         + " Show only that person: no other character, no thoughts, no explanation of what the reaction means. One sentence.";

      /// <summary>The writer's message: the last lines of the scene, then the one reaction to show.</summary>
      public static string User(string sceneText, string name, bool isFemale, string reaction)
         => "THE SCENE SO FAR:\n" + (string.IsNullOrWhiteSpace(sceneText) ? "(nothing yet)" : sceneText.Trim())
            + $"\n\nTHE REACTION TO SHOW:\n- {name} ({(isFemale ? "a woman, she/her" : "a man, he/him")}): {reaction}\nThe narration line:";

      /// <summary>
      ///   The line to show: the first non-empty line, without asterisks; null when there is none, when it reads as a refusal,
      ///   or when it only copies a line already in the scene (the simulator dropped echoes of an event this way).
      /// </summary>
      public static string Read(string content, IEnumerable<string> sceneLines)
      {
         string first = (content ?? "").Replace("\r", "").Split('\n')
                                       .Select(l => l.Replace("*", "").Replace("\u2014", ", ").Trim().Trim('"', '`').Trim())
                                       .FirstOrDefault(l => l.Length > 0);
         if (string.IsNullOrEmpty(first) || DialogueHeuristics.LooksLikeRefusal(first)) return null;

         bool echo = (sceneLines ?? new string[0]).Any(l => string.Equals((l ?? "").Replace("*", "").Trim(), first, StringComparison.OrdinalIgnoreCase));

         return echo ? null : first;
      }
   }
}
