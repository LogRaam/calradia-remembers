// Code written by Gabriel Mailhot, 13/09/2026.
//
// WHY THIS EXISTS. Gabriel's play-test: Nabb, briefed to charm Ira, ended his beat by turning to her and
// asking "I'd wager you've killed men of both sorts. Which ones do you remember?" - and nothing happened.
// The floor came straight back to the player, with a direct question hanging in the air. To make Ira answer
// he had to spend a turn typing "*I watch and wait to see what happens*", which is a player steering a beat
// that should have carried itself.
//
// THE SHAPE OF THE FAULT, which is not what it looks like. It is not that Ira has no voice: the ensemble turn
// (RunEnsembleTurnAsync) already gives every active participant its own LLM call. It is that the ORDER of the
// beat is fixed before anyone has spoken. Ira led, Nabb followed, and Nabb's reply could then address someone
// whose turn was already spent. A single pass in a pre-decided order cannot answer a question raised during it.
//
// So this is not "let witnesses speak more" - they already speak. It is narrower and it is the whole defect:
// A BEAT MUST NOT END ON A QUESTION PUT TO SOMEBODY STANDING RIGHT THERE.
//
// WHY A QUESTION, AND NOTHING WIDER. Anything looser ("did the last voice mention another") turns a two-NPC
// scene into an unbounded back-and-forth the player watches. A question mark is a positive, cheap, and honest
// signal that the speaker handed the floor to someone. The failure modes are both mild and asymmetric: a
// missed question costs exactly what the game does today, while a false one costs one extra reply that will
// read as a character choosing to respond. Bounded to RepliesPerBeat so a scene can never run away.
//
// The 15C auto-exchange (RunNpcWitnessExchangeAsync) does not cover this and should not be stretched to: it
// answers PASSIVE [WITNESS_REACTION] blocks that the main speaker's own call authored, and it is suppressed
// wholesale inside an ensemble turn, where witnesses are full voices rather than reactions.

#region

using System.Collections.Generic;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>
   ///   Decides whether an ensemble beat left a question hanging, and who in the room should answer it. Pure:
   ///   it reads the lines the beat produced and the cast list, and returns a hero id or null.
   /// </summary>
   public static class EnsembleFloorPolicy
   {
      /// <summary>
      ///   How many answering turns one player message may produce beyond the ordered pass. One: enough to
      ///   close a question, never enough for two NPCs to hold a conversation the player only watches. Raising
      ///   this is a design decision about how much of a beat the player gives up, not a tuning knob.
      /// </summary>
      public const int RepliesPerBeat = 1;

      // A balanced *...* span is a stage direction, matching WitnessSpeechDetector's own rule. Stripped before
      // the question test because a beat very often CLOSES on narration ("*The question hangs - not a
      // challenge, an invitation.*"), which would hide the question mark that came just before it.
      private static readonly Regex StageDirection = new Regex(@"\*[^*]*\*", RegexOptions.Compiled);

      /// <summary>
      ///   True when <paramref name="text" /> ends on a question once stage directions and trailing quotation
      ///   marks are stripped. Deliberately about the END of the line, not "contains a question mark": a
      ///   rhetorical question in the middle followed by a statement is a speaker holding the floor, not
      ///   handing it over.
      /// </summary>
      public static bool EndsOnAQuestion(string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return false;

         string spoken = StageDirection.Replace(text, "").Trim();

         // Walk back over the punctuation a line can legitimately close with after its question mark: the
         // closing quote of the speech, a bracket, whitespace. Anything else means the line did not end asking.
         for (int i = spoken.Length - 1; i >= 0; i--)
         {
            char c = spoken[i];
            if (c == '?') return true;
            if (char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '”' || c == '’'
             || c == ')' || c == ']' || c == '*') continue;

            return false;
         }

         return false;
      }

      /// <summary>
      ///   Who should answer, or null for "the floor goes back to the player".
      ///   <para>
      ///     <paramref name="beatLines" /> are the lines this beat produced, oldest first, player lines
      ///     included (they are skipped). <paramref name="activeIds" /> is the cast that has a voice;
      ///     <paramref name="anchorId" /> the conversation's own NPC, used only to break a tie in a room of
      ///     three or more. <paramref name="repliesAlready" /> is how many answering turns this beat has
      ///     already spent.
      ///   </para>
      /// </summary>
      public static string WhoAnswers(IReadOnlyList<TranscriptLine> beatLines,
                                      IReadOnlyList<string> activeIds,
                                      string anchorId,
                                      int repliesAlready)
      {
         if (beatLines == null || activeIds == null) return null;
         if (repliesAlready >= RepliesPerBeat) return null;

         // The last thing SAID by somebody in the room. Narration lines are skipped: the beat's closing stage
         // direction is the narrator's, and a question inside it is asked of nobody.
         TranscriptLine last = null;
         for (int i = beatLines.Count - 1; i >= 0; i--)
         {
            TranscriptLine line = beatLines[i];
            if (line == null || line.IsPlayer || line.IsNarration) continue;
            if (string.IsNullOrWhiteSpace(line.SpeakerHeroId)) continue;

            last = line;

            break;
         }

         if (last == null || !EndsOnAQuestion(last.Text)) return null;

         // Everyone with a voice except the one who just asked. A speaker cannot answer their own question.
         var candidates = new List<string>();
         foreach (string id in activeIds)
            if (!string.IsNullOrWhiteSpace(id) && id != last.SpeakerHeroId)
               candidates.Add(id);

         if (candidates.Count == 0) return null;   // nobody else has a voice: the question was for the player
         if (candidates.Count == 1) return candidates[0]; // the overwhelmingly common case, and unambiguous

         // Three or more voices, so the question has to be aimed. Whoever the asker NAMED wins, and the LAST
         // name mentioned wins over an earlier one ("Thais said as much, but Ira - which ones do you
         // remember?"). Names come from the beat's own lines, so no separate roster is needed and a hero the
         // policy cannot name simply never wins this way.
         string named = LastNamed(last.Text, candidates, beatLines);
         if (named != null) return named;

         // Nobody named: the anchor is whose meeting this is, so an unaimed question falls to them.
         if (!string.IsNullOrWhiteSpace(anchorId) && candidates.Contains(anchorId)) return anchorId;

         // Not even an anchor among them: whoever spoke most recently before the asker is the one being
         // answered. Falls back to the first candidate when nobody else has spoken yet this beat.
         for (int i = beatLines.Count - 1; i >= 0; i--)
         {
            TranscriptLine line = beatLines[i];
            if (line == null || line.IsPlayer || string.IsNullOrWhiteSpace(line.SpeakerHeroId)) continue;
            if (candidates.Contains(line.SpeakerHeroId)) return line.SpeakerHeroId;
         }

         return candidates[0];
      }

      #region private

      /// <summary>
      ///   The candidate whose name appears LATEST in <paramref name="text" />, or null when none is named.
      ///   Names are read off the beat's own transcript lines, so this needs no roster passed in.
      /// </summary>
      private static string LastNamed(string text,
                                      IReadOnlyList<string> candidates,
                                      IReadOnlyList<TranscriptLine> beatLines)
      {
         if (string.IsNullOrWhiteSpace(text)) return null;

         string best = null;
         int bestAt = -1;

         foreach (string id in candidates)
         {
            string name = NameOf(id, beatLines);
            if (string.IsNullOrWhiteSpace(name)) continue;

            // The FIRST word of the name: models address people as "Ira", rarely as "Ira of Pethros".
            int space = name.IndexOf(' ');
            string first = space > 0 ? name.Substring(0, space) : name;
            if (first.Length < 2) continue;

            int at = text.LastIndexOf(first, System.StringComparison.OrdinalIgnoreCase);
            if (at > bestAt)
            {
               bestAt = at;
               best = id;
            }
         }

         return best;
      }

      private static string NameOf(string heroId, IReadOnlyList<TranscriptLine> beatLines)
      {
         foreach (TranscriptLine line in beatLines)
            if (line != null && line.SpeakerHeroId == heroId && !string.IsNullOrWhiteSpace(line.SpeakerName))
               return line.SpeakerName;

         return null;
      }

      #endregion
   }
}
