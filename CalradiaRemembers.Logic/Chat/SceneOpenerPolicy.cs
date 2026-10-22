// Code written by Gabriel Mailhot, 22/10/2026.
// Lordfadooboo (Nexus, 21/10/2026): "when starting a conversation, instead of doing a cold open you could click
// 'generate scene' and the ai would set the stage a little before your first word." Gabriel (22/10/2026): no button,
// "ca devrait etre par defaut", as captive scenes already open themselves. An ordinary conversation the player starts
// now opens with the character setting the scene: what they were doing, whether they notice the player, their first
// gesture or word. The canned greeting stays on screen while it is written and the player may type at once; a message
// sent first sets the scene aside. An MCM switch turns it off (one more call per conversation; froggyluv, 20/10/2026,
// on cost).

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>What a conversation's opening knows when it decides whether the character sets the scene.</summary>
   public sealed class SceneOpenerFacts
   {
      /// <summary>The MCM switch "Characters Set The Scene" is on.</summary>
      public bool SettingOn { get; init; }

      /// <summary>The player opened an ordinary conversation through the greeting (not a tale meeting, an interception or any staged scene).</summary>
      public bool OpenedByGreeting { get; init; }

      /// <summary>A sitting kept from earlier today was put back: the conversation is already under way.</summary>
      public bool ResumedSitting { get; init; }

      /// <summary>Real lines of the game's own dialogue were carried into the chat (a wanderer's backstory): the scene has begun.</summary>
      public bool VanillaLinesCarried { get; init; }

      /// <summary>The language model can be called.</summary>
      public bool ModelAvailable { get; init; }
   }

   /// <summary>How the character's scene-setting turn ended.</summary>
   public enum SceneOpenerOutcome
   {
      /// <summary>The scene was written and shown in place of the greeting.</summary>
      Delivered,

      /// <summary>The call failed or came back empty.</summary>
      Failed,

      /// <summary>The player sent a message before the scene arrived.</summary>
      Abandoned
   }

   /// <summary>Decides whether the character sets the scene, the direction they are given, and what becomes of the greeting.</summary>
   public static class SceneOpenerPolicy
   {
      /// <summary>
      ///   The stage direction for the opening turn. It is never shown nor kept as a player line. It asks for the moment
      ///   before the first word and forbids what the game could prove false: someone who is not there, a deed, a promise.
      /// </summary>
      public const string Cue =
         "[The player has just come to speak with you, and has not yet said a word. Open the moment YOURSELF, briefly: "
       + "what you were doing as they came, whether you notice them, and your first gesture or words to them. Keep to "
       + "what is truly here: bring in no one who is not present and no event that did not happen. Do not speak or act "
       + "for the player, and do not take any action or make any promise yet. Do not quote this note.]";

      /// <summary>True when this opening should be the character's own scene-setting turn.</summary>
      public static bool ShouldOpen(SceneOpenerFacts facts)
         => facts != null
            && facts.SettingOn
            && facts.OpenedByGreeting
            && !facts.ResumedSitting
            && !facts.VanillaLinesCarried
            && facts.ModelAvailable;

      /// <summary>
      ///   Whether the canned greeting is kept as said (and remembered as said). It is replaced only by a scene that
      ///   arrived; a failed call or a player who spoke first leaves it standing, with no error line.
      /// </summary>
      public static bool GreetingStands(SceneOpenerOutcome outcome) => outcome != SceneOpenerOutcome.Delivered;
   }
}
