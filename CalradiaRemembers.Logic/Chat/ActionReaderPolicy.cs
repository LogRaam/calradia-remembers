// Code written by Gabriel Mailhot, 25/10/2026.
// The one junction between the two compositions (Gabriel, 25/10/2026): "tout devrait etre un seul chemin et la seule
// jonction devrait etre a quel service je demande l'interpretation." A reply's actions are read either from the reply's
// own [ACTION] tags (the same call wrote prose and tags: Standard composition, and every turn Enhanced leaves single-call)
// or by a second call, the action interpreter, over the prose just shown (Enhanced). Everything before (what is taught)
// and after (the actions loop, the bridge) is meant to be one path; this is where the road forks, and the only place.
//
// The rules are the ones the live turn already applied, moved here unchanged (ROADMAP, one path for actions, step 2):
// only an ordinary main-NPC 1:1 turn on a real player message is read by the interpreter, never a captive or captor
// scene, an ensemble voice or a synthetic player line (ChatViewModel.Exchanges, "captive/captor scenes, councils,
// routed-witness turns and synthetic openers deliberately stay Integrated for now"); and a turn the prose fallback redid
// as one integrated call reads the fallback's own tags ("running the interpreter over them again would be redundant, and
// would target the wrong prose, the fallback's").

#region

using CalradiaRemembers.Logic.Providers;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
    /// <summary>Who reads a reply's actions.</summary>
    public enum ActionReader
    {
        /// <summary>The reply's own [ACTION] and [EVENT] tags, written by the same call that wrote the prose.</summary>
        SameCall,

        /// <summary>The action interpreter, a second call over the prose just shown.</summary>
        Interpreter
    }

    /// <summary>The facts of a turn that decide who reads its actions.</summary>
    public sealed class ActionReadingFacts
    {
        /// <summary>The in-chat switch's composition this turn (not the raw MCM dropdown).</summary>
        public ChatCompositionMode Composition { get; init; }

        /// <summary>The main character of the conversation speaks (not an ensemble voice or an addressed witness).</summary>
        public bool IsMainSpeaker { get; init; }

        /// <summary>The player's line was written by the game (a scene opener, a continuation), not typed by the player.</summary>
        public bool IsSyntheticPlayerLine { get; init; }

        /// <summary>A captive scene: the player is held and the captor acts.</summary>
        public bool IsCaptiveEncounter { get; init; }

        /// <summary>A captor scene: the player holds the character captive.</summary>
        public bool IsCaptorScene { get; init; }
    }

    /// <summary>Decides who reads a reply's actions: the junction between Standard and Enhanced. Pure.</summary>
    public static class ActionReaderPolicy
    {
        /// <summary>
        ///   Whether this turn is meant for the interpreter, decided before the prose call: such a turn asks the prose
        ///   call to fail fast rather than retry a cut reply, and may have its beat redone by the prose fallback.
        /// </summary>
        public static bool IsInterpreterTurn(ActionReadingFacts facts)
            => facts != null
               && facts.Composition == ChatCompositionMode.Enhanced
               && facts.IsMainSpeaker
               && !facts.IsSyntheticPlayerLine
               && !facts.IsCaptiveEncounter
               && !facts.IsCaptorScene;

        /// <summary>
        ///   Who reads the reply's actions, decided once the prose is in: the interpreter on an interpreter turn, unless
        ///   the prose fallback redid the beat as one integrated call, whose own tags then stand.
        /// </summary>
        public static ActionReader ReaderFor(ActionReadingFacts facts, bool usedProseFallback)
            => IsInterpreterTurn(facts) && !usedProseFallback ? ActionReader.Interpreter : ActionReader.SameCall;
    }
}
