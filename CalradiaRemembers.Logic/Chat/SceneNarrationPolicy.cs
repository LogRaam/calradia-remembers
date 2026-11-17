// Code written by Gabriel Mailhot, 17/11/2026.
// 1Enfant (Nexus, 16/11/2026): "How do I disable the description of the surrounding environment? (purple section in
// the conversation)". Gabriel (17/11/2026): "oui", a switch. With "Scene Narration" off the prompt no longer teaches
// the block (PromptBuilder.EnableSceneNarration); this decides what the chat does with a narration a model writes
// anyway. Captive scenes keep theirs: there it carries what is done to the prisoner, which has no other channel.

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>What the chat knows about one narration the model wrote.</summary>
   public sealed class SceneNarrationFacts
   {
      /// <summary>The MCM switch "Scene Narration" is on.</summary>
      public bool SettingOn { get; init; }

      /// <summary>The conversation is a captive scene (the player held, the player as captor, a companion victim).</summary>
      public bool CaptiveScene { get; init; }

      /// <summary>The same reply carries no spoken line: the narration is all the turn has.</summary>
      public bool NothingSpoken { get; init; }
   }

   /// <summary>Decides whether a model-written scene narration is shown (and kept in the conversation's record).</summary>
   public static class SceneNarrationPolicy
   {
      /// <summary>
      ///   True when the narration is shown: the switch is on, the scene is a captive one, or the narration is the whole
      ///   reply (hiding it would leave the player staring at silence).
      /// </summary>
      public static bool Shows(SceneNarrationFacts facts)
         => facts == null || facts.SettingOn || facts.CaptiveScene || facts.NothingSpoken;
   }
}
