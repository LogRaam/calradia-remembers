// Code written by Gabriel Mailhot, 11/07/2026.

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   The exact set of view-model operations the action-confirmation flow
   ///   (<see cref="ActionConfirmationController" />) drives, so the controller depends on this seam rather than the
   ///   whole ChatViewModel. ChatViewModel implements it with thin forwards to its existing rendering helpers.
   /// </summary>
   internal interface IActionConfirmationHost
   {
      /// <summary>True once the conversation has ended (NPC farewell / end_conversation); queued spends are then dropped, not applied.</summary>
      bool IsConversationEnded { get; }

      /// <summary>Adds an out-of-character (terminal-style) line to the chat: the confirmation prompts and their results.</summary>
      void AppendOoc(string text);

      /// <summary>Adds a system notification line (a relation/gold outcome) to the chat, colour-coded by whether it favors the player.</summary>
      void AppendSystemMessageLine(string text, bool positive);

      /// <summary>Appends one attributed line to the running transcript (used for the declined-action correction note).</summary>
      void RecordTranscriptLine(bool isPlayer, string name, string heroId, string text);

      /// <summary>The recompose path: restores the typed text as the player's input and sends it as a fresh LLM turn.</summary>
      void SendPlayerMessage(string raw);
   }
}
