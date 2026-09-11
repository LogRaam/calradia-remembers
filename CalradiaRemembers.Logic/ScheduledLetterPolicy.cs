// Code written by Gabriel Mailhot, 17/07/2026.
// Courier audit 2.5. Decides, for one committed-but-unwritten letter (a ScheduledLetter, today only the child
// support due after a birth), what the daily host pass should do with it. The whole point of the pillar is that
// an LLM outage at the exact moment of the birth must NOT lose the pension forever, so this is a bounded retry
// loop, not a one-shot: wait until the due day, wait while a generation is in flight, retry a failed one, and
// give up only after a fixed number of attempts (or once the letter is confirmed written). The stakes of each
// branch are: too eager and a duplicate pension letter is queued while one is already being written; too timid
// and a single failed generation drops the demand and its quest with no recourse; unbounded and a permanently
// broken LLM re-queues the same letter every single day forever.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What the daily host pass should do with one <c>ScheduledLetter</c> this tick.</summary>
    public enum ScheduledLetterAction
    {
        /// <summary>Not due yet, or a generation is already in flight: do nothing this tick.</summary>
        Wait,

        /// <summary>Due, nothing in flight, attempts remain: generate the content and dispatch now.</summary>
        Generate,

        /// <summary>Too many failed attempts: abandon the intent so it stops retrying.</summary>
        GiveUp,

        /// <summary>The letter has already been written (a real one exists): the intent is done, drop it.</summary>
        Fulfilled
    }

    /// <summary>
    ///   Pure decision for the child-support (and any future) scheduled letter: a bounded daily retry that turns a
    ///   persisted intention into a written letter without ever losing it to a transient LLM failure, duplicating
    ///   it while one is in flight, or retrying a hopeless one forever.
    /// </summary>
    public static class ScheduledLetterPolicy
    {
        /// <summary>How many generation attempts before the intent is abandoned. Tuning: comfortably survives a short outage, not an endless one.</summary>
        public const int MaxAttempts = 5;

        /// <summary>
        ///   Where one scheduled letter stands. THREE INTS in a row, two of them DAYS whose comparison is the
        ///   due gate and one a COUNT of attempts: swap the days and a letter due tomorrow is generated today,
        ///   swap either with the attempts and it gives up before it has tried.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class ScheduledLetterFacts
        {
           /// <summary>The day it is due to go.</summary>
           public int SendDay { get; set; }

           /// <summary>The current campaign day.</summary>
           public int Today { get; set; }

           /// <summary>How many times generation has already been tried. At the bound it gives up.</summary>
           public int Attempts { get; set; }

           /// <summary>
           ///   A real letter of this reason already exists with content: generation succeeded on an earlier
           ///   tick. Wins outright.
           /// </summary>
           public bool Fulfilled { get; set; }

           /// <summary>A blank placeholder of that reason is mid-generation right now, so this tick waits.</summary>
           public bool GenerationInFlight { get; set; }
        }

        /// <summary>
        ///   Decides the action for one scheduled letter. <c>Fulfilled</c> is true when a real letter
        ///   of the same reason already exists with content (generation succeeded on an earlier tick);
        ///   <c>GenerationInFlight</c> is true when a blank placeholder of that reason is mid-generation
        ///   right now. Fulfilled wins outright; then the due-day gate; then in-flight; then the attempt bound.
        /// </summary>
        public static ScheduledLetterAction Decide(ScheduledLetterFacts facts)
        {
           if (facts == null) return ScheduledLetterAction.Wait;

           int sendDay = facts.SendDay;
           int today = facts.Today;
           int attempts = facts.Attempts;
           bool fulfilled = facts.Fulfilled;
           bool generationInFlight = facts.GenerationInFlight;

            if (fulfilled) return ScheduledLetterAction.Fulfilled;
            if (today < sendDay) return ScheduledLetterAction.Wait;
            if (generationInFlight) return ScheduledLetterAction.Wait;
            if (attempts >= MaxAttempts) return ScheduledLetterAction.GiveUp;

            return ScheduledLetterAction.Generate;
        }
    }
}
