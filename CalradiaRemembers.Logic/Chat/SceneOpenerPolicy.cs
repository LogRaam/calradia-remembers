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

      /// <summary>
      ///   The composed intro will open this chat ("Composed opening scene" and Scene Narration on, an ordinary chat). It opens
      ///   every such chat, with or without "Characters Set The Scene", and takes the place of the game's own dialogue lines.
      /// </summary>
      public bool ComposedIntro { get; init; }
   }

   /// <summary>How the character's scene-setting turn ended.</summary>
   public enum SceneOpenerOutcome
   {
      /// <summary>The scene was written and shown in place of the greeting.</summary>
      Delivered,

      /// <summary>The call failed or came back empty.</summary>
      Failed,

      /// <summary>The player sent a message before the scene arrived.</summary>
      Abandoned,

      /// <summary>
      ///   A composed opening (Stage 3, 28/11/2026) was shown above the greeting: the greeting stands as the first words,
      ///   and the scene is set, so no opening narration is owed to the next turn.
      /// </summary>
      Composed
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

      /// <summary>
      ///   True when this opening should be the character's own scene-setting turn. The composed intro opens every ordinary
      ///   chat (Gabriel, 28/11/2026: "toujours une intro a l'ouverture du chat"), so it needs neither the scene switch nor a chat free of the game's lines.
      /// </summary>
      public static bool ShouldOpen(SceneOpenerFacts facts)
         => facts != null
            && facts.OpenedByGreeting
            && facts.ModelAvailable
            // A sitting put back from earlier today gets the intro too (Gabriel, 28/11/2026: "je pourrais rencontrer le lord sur la
            // MAP et ensuite dans sa ville"): the place may have changed, and the composer reads the talk so far.
            && (facts.ComposedIntro || (facts.SettingOn && !facts.ResumedSitting && !facts.VanillaLinesCarried));

      /// <summary>
      ///   True when a composed intro that failed may hand over to the ordinary opener. Never once the two have spoken (the game's
      ///   lines carried, or a talk put back from earlier today): its cue tells the model the player "has not yet said a word", and
      ///   a lady the player had just introduced herself to greeted her as a stranger, "traveller". Gabriel (28/11/2026): 'Le joueur fait son intro en Vanille ou il se presente ... le mieux serait d'afficher l'intro, et ensuite remettre la conversation vanille ... le NPC nomme le joueur de voyageur comme si il ne l'avait jamais vu.'
      ///   The composed intro itself goes ABOVE the game's lines, which stay on screen.
      /// </summary>
      public static bool OrdinaryOpenerMayFollow(SceneOpenerFacts facts)
         => facts != null && !facts.VanillaLinesCarried && !facts.ResumedSitting;

      /// <summary>
      ///   Whether the canned greeting is kept as said (and remembered as said). It is replaced only by a scene that
      ///   arrived; a failed call or a player who spoke first leaves it standing, with no error line.
      /// </summary>
      public static bool GreetingStands(SceneOpenerOutcome outcome) => outcome != SceneOpenerOutcome.Delivered;

      /// <summary>
      ///   Whether the next turn still owes the opening narration: yes when the greeting stood alone (a failed call, a player
      ///   who spoke first), no when a scene was delivered or a composed opening set it.
      /// </summary>
      public static bool OpeningNarrationOwed(SceneOpenerOutcome outcome)
         => outcome == SceneOpenerOutcome.Failed || outcome == SceneOpenerOutcome.Abandoned;
   }
}
