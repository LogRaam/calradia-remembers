// Code written by Gabriel Mailhot, 26/11/2026.
//
// WHY THIS EXISTS. Players want to speak with every character in a group on the same footing (Gabriel, 26/11/2026), and
// whiskeypatt's chat with Rhagaea and a lord who kept joining showed the main-and-witnesses model breaking. Gabriel's design
// (26/11/2026): one thread per character; after every line, a fast, cheap JUDGE model says, for each listener, whether they
// react and whether they want to speak; the judge never writes dialogue, the player's prose model does. His rules: speech is
// exclusive, intention is concurrent; a reaction is not a reply; the one addressed has a priority, not a monopoly ("NPC2
// reacts and takes the floor even if the player does not address him directly"); intentions are re-judged after every line,
// never queued replies. And: "pour que le joueur ressente une conversation credible, il ne faut pas sacrifier la qualite".
//
// This is the referee. The judge gives only what code cannot know (how much a character wants to speak, a reaction); the
// facts the game knows (who was addressed or named, who was mocked, rank, how long since one spoke, who just spoke) are
// read by the caller, and the weights below combine them. Pure and stateless: one call per round, on fresh bids, so the
// re-judging rule holds by construction. Not wired yet: it replaces EnsembleFloorPolicy when group chat is integrated.
// Scenarios validated by Gabriel on 26/11/2026 are its tests.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>How much a listener wants to speak, as the judge sees it. Coarse on purpose: a small model's fine scores are noise.</summary>
   public enum SpeakingIntent
   {
      /// <summary>Nothing to say.</summary>
      None,

      /// <summary>Would say something if the floor fell to them.</summary>
      Low,

      /// <summary>Wants to speak: has something to add or to answer.</summary>
      High,

      /// <summary>Must speak now: provoked, threatened, or the matter is theirs.</summary>
      Urgent
   }

   /// <summary>One listener's bid for the next line: the judge's verdict plus what the game knows of them this round.</summary>
   public sealed class FloorBid
   {
      /// <summary>The character's id.</summary>
      public string Id { get; init; }

      /// <summary>The judge's verdict on how much they want to speak.</summary>
      public SpeakingIntent Intent { get; init; }

      /// <summary>The judge's non-verbal (or brief sound) reaction to the last line, or null. Shown whether or not they speak.</summary>
      public string Reaction { get; init; }

      /// <summary>The last line was addressed to them alone (not to everyone, not to someone else).</summary>
      public bool Addressed { get; init; }

      /// <summary>The last line put a question to them by name ("And you, Ira?"): it all but obliges them to answer.</summary>
      public bool AskedByName { get; init; }

      /// <summary>The last line spoke about them: mocked, accused or named as the matter, whoever it was addressed to.</summary>
      public bool Targeted { get; init; }

      /// <summary>Social standing in this room, 0 (lowest) to 4 (a ruler).</summary>
      public int Standing { get; init; }

      /// <summary>Lines spoken by others since this character last spoke.</summary>
      public int TurnsSilent { get; init; }

      /// <summary>This character spoke the last line.</summary>
      public bool SpokeLast { get; init; }

      /// <summary>This character was about to answer last round and was cut off: owed the next chance.</summary>
      public bool WasCutOff { get; init; }

      /// <summary>The final tie-break: lower wins. The caller draws it (seeded in tests) so ties are not always won by the same seat.</summary>
      public int TieBreak { get; init; }
   }

   /// <summary>The state of the round the referee decides.</summary>
   public sealed class FloorRound
   {
      /// <summary>Every listener's bid (the one who spoke the last line included, with <see cref="FloorBid.SpokeLast" />).</summary>
      public IReadOnlyList<FloorBid> Bids { get; init; } = new List<FloorBid>();

      /// <summary>Lines characters have spoken since the player's last line.</summary>
      public int NpcLinesSincePlayer { get; init; }

      /// <summary>The last line was the player's.</summary>
      public bool PlayerSpokeLast { get; init; }
   }

   /// <summary>What happens next.</summary>
   public enum FloorOutcome
   {
      /// <summary>A character speaks: <see cref="FloorDecision.SpeakerId" />.</summary>
      Speak,

      /// <summary>Nobody speaks: the player's line hangs, only reactions follow.</summary>
      Silence,

      /// <summary>The floor goes back to the player.</summary>
      BackToPlayer
   }

   /// <summary>The referee's decision for one round.</summary>
   public sealed class FloorDecision
   {
      /// <summary>What happens next.</summary>
      public FloorOutcome Outcome { get; init; }

      /// <summary>Who speaks, or null.</summary>
      public string SpeakerId { get; init; }

      /// <summary>The one addressed wanted to answer and the speaker cut in first: narrate the cut, owe them the next chance.</summary>
      public string CutOffId { get; init; }

      /// <summary>The reactions to show before the next line, in order (one at a time: a sound never overlaps a line).</summary>
      public IReadOnlyList<string> ReactorIds { get; init; } = new List<string>();
   }

   /// <summary>Decides who holds the floor next in a group conversation. Pure; one call per round, on fresh bids.</summary>
   public static class TurnArbiter
   {
      /// <summary>Characters' lines allowed after the player's before the floor returns to them, unless something is urgent.</summary>
      public const int NpcLinesPerPlayerLine = 2;

      /// <summary>The hard ceiling: even urgent, past this many character lines the player gets the floor back.</summary>
      public const int NpcLinesCeiling = 3;

      /// <summary>Weight of the judge's verdict: Low.</summary>
      public const int LowIntentWeight = 10;

      /// <summary>Weight of the judge's verdict: High.</summary>
      public const int HighIntentWeight = 30;

      /// <summary>Weight of the judge's verdict: Urgent.</summary>
      public const int UrgentIntentWeight = 55;

      /// <summary>The one addressed: a priority, not a monopoly (an urgent, provoked listener still beats a merely willing addressee).</summary>
      public const int AddressedBonus = 25;

      /// <summary>A question by name: beats almost everything.</summary>
      public const int AskedByNameBonus = 60;

      /// <summary>Spoken about (mocked, accused): what lets a provoked listener cut in.</summary>
      public const int TargetedBonus = 15;

      /// <summary>Per point of standing (0 to 4).</summary>
      public const int StandingWeight = 6;

      /// <summary>Per line of silence, up to <see cref="SilenceCap" /> lines: one who has said nothing for long rises.</summary>
      public const int SilenceWeight = 3;

      /// <summary>Lines of silence that still count.</summary>
      public const int SilenceCap = 5;

      /// <summary>The one cut off last round is owed the next chance.</summary>
      public const int CutOffBonus = 30;

      /// <summary>How many listeners get a judge call each round in a large group; the others are judged on a later round.</summary>
      public const int JudgedPerRound = 4;

      /// <summary>The referee's decision for this round.</summary>
      public static FloorDecision Decide(FloorRound round)
      {
         IReadOnlyList<FloorBid> bids = round?.Bids ?? new List<FloorBid>();
         var reactors = bids.Where(b => !string.IsNullOrWhiteSpace(b.Reaction)).OrderByDescending(Score).ThenBy(b => b.TieBreak).Select(b => b.Id).ToList();

         int lines = round?.NpcLinesSincePlayer ?? 0;
         FloorBid winner = bids.Where(b => Eligible(b, lines)).OrderByDescending(Score).ThenByDescending(b => b.Standing).ThenByDescending(b => b.TurnsSilent).ThenBy(b => b.TieBreak).FirstOrDefault();

         if (winner == null)
            return new FloorDecision {Outcome = round?.PlayerSpokeLast == true ? FloorOutcome.Silence : FloorOutcome.BackToPlayer, ReactorIds = reactors};

         // Only one who truly wanted to answer is "cut off": narrating a mouth opening for someone with nothing to say is false.
         FloorBid addressed = bids.FirstOrDefault(b => b.Addressed && !ReferenceEquals(b, winner) && b.Intent != SpeakingIntent.None && Eligible(b, lines));

         return new FloorDecision {Outcome = FloorOutcome.Speak, SpeakerId = winner.Id, CutOffId = addressed?.Id, ReactorIds = reactors};
      }

      /// <summary>
      ///   Who gets a judge call this round, at most <see cref="JudgedPerRound" />: those the line concerns first (named,
      ///   addressed, spoken about), then the longest silent. A large room does not cost a call per head every line.
      /// </summary>
      public static IReadOnlyList<string> WhoToJudge(IReadOnlyList<FloorBid> listeners)
         => (listeners ?? new List<FloorBid>())
            .Where(l => !l.SpokeLast)
            .OrderByDescending(l => l.AskedByName)
            .ThenByDescending(l => l.Addressed)
            .ThenByDescending(l => l.Targeted)
            .ThenByDescending(l => l.WasCutOff)
            .ThenByDescending(l => l.TurnsSilent)
            .ThenBy(l => l.TieBreak)
            .Take(JudgedPerRound)
            .Select(l => l.Id)
            .ToList();

      /// <summary>A bid's weight this round.</summary>
      public static int Score(FloorBid bid)
      {
         if (bid == null) return 0;

         int intent = bid.Intent switch {
            SpeakingIntent.Urgent => UrgentIntentWeight,
            SpeakingIntent.High => HighIntentWeight,
            SpeakingIntent.Low => LowIntentWeight,
            _ => 0
         };

         return intent
                + (bid.Addressed ? AddressedBonus : 0)
                + (bid.AskedByName ? AskedByNameBonus : 0)
                + (bid.Targeted ? TargetedBonus : 0)
                + Math.Max(0, Math.Min(4, bid.Standing)) * StandingWeight
                + Math.Min(SilenceCap, Math.Max(0, bid.TurnsSilent)) * SilenceWeight
                + (bid.WasCutOff ? CutOffBonus : 0);
      }

      private static bool Eligible(FloorBid bid, int npcLinesSincePlayer)
      {
         if (bid == null) return false;

         // One addressed or asked by name always has something to answer: a question left hanging reads as a fault.
         bool obliged = bid.AskedByName || bid.Addressed;
         SpeakingIntent intent = obliged && bid.Intent == SpeakingIntent.None ? SpeakingIntent.Low : bid.Intent;
         if (intent == SpeakingIntent.None) return false;

         // Nobody speaks twice running unless the line was put to them.
         if (bid.SpokeLast && !obliged) return false;

         if (npcLinesSincePlayer >= NpcLinesCeiling) return false;
         if (npcLinesSincePlayer >= NpcLinesPerPlayerLine) return intent == SpeakingIntent.Urgent;
         // After the first character's line, a mere "would say something" no longer earns another: no two idle replies. A line
         // put to someone still gets its answer (EnsembleFloorPolicy's rule: a beat never ends on a question left hanging).
         if (npcLinesSincePlayer >= 1) return intent >= SpeakingIntent.High || obliged;

         return true;
      }
   }
}
