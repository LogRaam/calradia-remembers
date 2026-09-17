// Code written by Gabriel Mailhot, 24/07/2026.
// Durable async-summary queue, increment B (pure logic only): the queue itself, no engine, no SyncData. A
// background worker (a later increment) drains this FIFO to actually call the summarizer; a load-time replay
// re-enqueues whatever survived a save. Three failure modes this queue exists to prevent: (1) a crash mid-flight
// silently loses the enrichment plan forever, the whole point of the durable queue; (2) an endpoint that never
// recovers retries the same job every tick forever, burning tokens on a dead LLM call (Fail's bounded-attempts
// drop); (3) a corrupt or pre-feature save blob bricks the load instead of just starting with no pending jobs
// (Deserialize's tolerant degrade, per the save-migration rule).

#region

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What <see cref="ConversationSummaryQueue.Enqueue" /> actually did with the new job.</summary>
    public enum SummaryEnqueueOutcome
    {
        /// <summary>Added normally: the queue had room and no matching job was already queued.</summary>
        Added,

        /// <summary>Not added: a job with the same (ProfileId, AnchorDay, AnchorSummary) was already queued.</summary>
        Deduplicated,

        /// <summary>Added, but the queue was at capacity, so the OLDEST job was dropped to make room.</summary>
        AddedOldestDropped
    }

    /// <summary>
    ///   A pure, ordered (FIFO) queue of <see cref="PendingSummaryJob" />. Deduplicates on the anchor key, caps
    ///   its size by dropping the oldest job, and bounds retries by dropping a job that keeps failing.
    ///   Serializes to and from a plain JSON string so the mod's own persistence layer can save and reload it
    ///   verbatim; a null/corrupt blob degrades to an empty queue rather than throwing.
    /// </summary>
    public sealed class ConversationSummaryQueue
    {
        /// <summary>Tuning: how many pending jobs the queue holds before it starts dropping the oldest.</summary>
        public const int DefaultMaxJobs = 64;

        /// <summary>Tuning: how many failed attempts a job survives before it is dropped for good.</summary>
        public const int DefaultMaxAttempts = 3;

        private readonly List<PendingSummaryJob> _jobs = new List<PendingSummaryJob>();
        private readonly int _maxJobs;
        private readonly int _maxAttempts;

        public ConversationSummaryQueue(int maxJobs = DefaultMaxJobs, int maxAttempts = DefaultMaxAttempts)
        {
            _maxJobs = maxJobs;
            _maxAttempts = maxAttempts;
        }

        /// <summary>How many jobs are currently queued.</summary>
        public int Count => _jobs.Count;

        /// <summary>A read-only snapshot of the queue, oldest first, for diagnostics.</summary>
        public IReadOnlyList<PendingSummaryJob> Pending => _jobs;

        /// <summary>
        ///   Adds a job to the back of the queue, unless a job with the same (ProfileId, AnchorDay, AnchorSummary)
        ///   is already queued (dedup: the anchor key identifies one base event, so a second job to redo the same
        ///   enrichment would be wasted work). When the queue is already at capacity, the OLDEST job is dropped
        ///   to make room rather than refusing the new one.
        /// </summary>
        public SummaryEnqueueOutcome Enqueue(PendingSummaryJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            if (IndexOfMatch(job) >= 0)
                return SummaryEnqueueOutcome.Deduplicated;

            bool droppedOldest = false;
            if (_jobs.Count >= _maxJobs)
            {
                _jobs.RemoveAt(0);
                droppedOldest = true;
            }

            _jobs.Add(job);
            return droppedOldest ? SummaryEnqueueOutcome.AddedOldestDropped : SummaryEnqueueOutcome.Added;
        }

        /// <summary>The front (oldest) job a worker should process next, or null when the queue is empty.</summary>
        public PendingSummaryJob NextPending() => _jobs.Count > 0 ? _jobs[0] : null;

        /// <summary>Removes a finished job from the queue. Returns false if it was already gone.</summary>
        public bool Complete(PendingSummaryJob job)
        {
            int index = IndexOfMatch(job);
            if (index < 0) return false;

            _jobs.RemoveAt(index);
            return true;
        }

        /// <summary>
        ///   Records one failed attempt at <paramref name="job" />. Increments the STORED job's
        ///   <see cref="PendingSummaryJob.Attempts" />; once it reaches the configured max attempts (default
        ///   <see cref="DefaultMaxAttempts" />) the job is dropped for good, a permanently-failing endpoint must
        ///   not retry forever, and this returns true. Otherwise the job is kept, moved to the back of the queue
        ///   for fair retry, and this returns false.
        /// </summary>
        public bool Fail(PendingSummaryJob job)
        {
            int index = IndexOfMatch(job);
            if (index < 0) return false;

            PendingSummaryJob stored = _jobs[index];
            stored.Attempts++;
            _jobs.RemoveAt(index);

            if (stored.Attempts >= _maxAttempts) return true; // dropped for good

            _jobs.Add(stored);
            return false;
        }

        /// <summary>Serializes the queue's jobs to a plain JSON array string.</summary>
        public string Serialize() => JsonConvert.SerializeObject(_jobs, Formatting.None);

        /// <summary>
        ///   Rebuilds a queue from <see cref="Serialize" />'s output. Null, empty, or garbage/corrupt input
        ///   degrades to an EMPTY queue rather than throwing: an old save with no pending-summary data at all,
        ///   or a blob damaged some other way, must still let the game load and keep running.
        /// </summary>
        public static ConversationSummaryQueue Deserialize(string json, int maxJobs = DefaultMaxJobs, int maxAttempts = DefaultMaxAttempts)
        {
            var queue = new ConversationSummaryQueue(maxJobs, maxAttempts);
            if (string.IsNullOrWhiteSpace(json)) return queue;

            try
            {
                List<PendingSummaryJob> jobs = JsonConvert.DeserializeObject<List<PendingSummaryJob>>(json);
                if (jobs != null) queue._jobs.AddRange(jobs);
            }
            catch
            {
                // Corrupt/garbage blob: leave the queue empty rather than bricking the load (save-migration rule).
            }

            return queue;
        }

        /// <summary>
        ///   Which stored job is THIS job, by identity where there is one and by value where there is not.
        ///
        ///   The value triple - profile, day, summary - cannot tell two conversations apart, because the
        ///   anchor is always "Spoke with {player}." on today's date. A second sitting with the same person on
        ///   the same day was therefore deduplicated away as a repeat of the first (audit, 15/09/2026).
        ///   AnchorId is unique per conversation, so it decides whenever BOTH jobs carry one.
        ///
        ///   The value fallback stays for jobs enqueued before identities existed and replayed from an older
        ///   save: for those, today's behaviour is the best available and is exactly what they had.
        /// </summary>
        private int IndexOfMatch(PendingSummaryJob job)
        {
            if (job == null) return -1;

            for (int i = 0; i < _jobs.Count; i++)
            {
                PendingSummaryJob j = _jobs[i];

                if (!string.Equals(j.ProfileId, job.ProfileId, StringComparison.Ordinal)) continue;

                bool bothIdentified = !string.IsNullOrEmpty(j.AnchorId) && !string.IsNullOrEmpty(job.AnchorId);

                if (bothIdentified)
                {
                    if (string.Equals(j.AnchorId, job.AnchorId, StringComparison.Ordinal)) return i;

                    continue;
                }

                if (j.AnchorDay == job.AnchorDay &&
                    string.Equals(j.AnchorSummary, job.AnchorSummary, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
