// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   The exact set of view-model operations the round-table lap (<see cref="RoundTableController.RunAsync" />)
   ///   drives, so the controller depends on this seam rather than the whole ChatViewModel. ChatViewModel
   ///   implements it with thin forwards to its existing private helpers.
   /// </summary>
   internal interface IRoundTableHost
   {
      /// <summary>True once the conversation has ended (NPC farewell / end_conversation) so the lap stops.</summary>
      bool IsConversationEnded { get; }

      /// <summary>True while this view model is still the active chat (guards a stale async continuation).</summary>
      bool IsStillActiveView { get; }

      /// <summary>
      ///   True when <paramref name="speaker"/> is STILL in the room. The lap walks a snapshot taken when the
      ///   floor opened, so a participant who leaves mid-lap (an honoured <c>witness_leaves</c>) would
      ///   otherwise speak again on the wrap-around while everyone else's prompt already describes them as
      ///   gone (round-table audit D3). The main NPC is always present: they host the conversation.
      /// </summary>
      bool IsStillPresent(ChatSpeaker speaker);

      /// <summary>Rebuilds the given speaker's attributed ChatSession from the running transcript.</summary>
      ChatSession BuildSession(ChatSpeaker speaker);

      /// <summary>The witness list to inject into this speaker's prompt (every other present participant).</summary>
      IReadOnlyList<WitnessEntry> BuildContextWitnesses(ChatSpeaker speaker);

      /// <summary>Adds a "(considering...)" placeholder bubble for the speaker, scrolls to it, and returns it.</summary>
      ChatMessageViewModel AddPlaceholder(ChatSpeaker speaker);

      /// <summary>Removes a placeholder bubble entirely (used when a turn failed or was narration-only).</summary>
      void RemovePlaceholder(ChatMessageViewModel placeholder);

      /// <summary>Renders the speaker's turn into its placeholder and returns the spoken line (empty for narration-only).</summary>
      string RenderTurn(ChatMessageViewModel placeholder, ChatSpeaker speaker, ParsedResponse response);

      /// <summary>Appends one attributed line to the running transcript.</summary>
      void RecordTranscriptLine(bool isPlayer, string name, string heroId, string text);

      /// <summary>
      ///   Displays a scene-narration line and mirrors it to the story log and transcript. When
      ///   <paramref name="insertBefore" /> is given (the speaker's pending placeholder), the line is
      ///   inserted ahead of it — used when the model led its turn with the narration.
      /// </summary>
      void AppendNarration(string narration, ChatMessageViewModel insertBefore = null);

      /// <summary>Honors any witness_leaves actions in the response (a player companion stepping out).</summary>
      void HandleWitnessLeaves(ParsedResponse response);

      /// <summary>Locks the conversation the way an NPC's end_conversation does (Leave button, system line).</summary>
      void MarkConversationEndedBySpeaker(string speakerName);

      /// <summary>Diagnostic VRAM sample line, tagged for the log.</summary>
      void LogVram(string tag);
   }
}
