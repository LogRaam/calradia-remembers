// Code written by Gabriel Mailhot, 27/06/2026.
// Extension Surface, Volet A — the validation rules at the ingestion door. A third-party mod pushes facts into
// CR's world-event store; before any enters, its scalars are normalised: magnitude clamped, summary bounded
// (the save-blob guard), and the source held to a daily quota (anti-flood). Pure so the contract — and its
// tuning — stays protected by tests; the id-resolution and dedup are engine-side in the Feed facade.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The normalisation + quota rules every externally-fed world event passes through.</summary>
    public static class ExternalEventRules
    {
        /// <summary>How far news can carry (0..100).</summary>
        public const int MaxMagnitude = 100;

        /// <summary>The longest a summary may be — the save-blob guard against an oversized injected line. Tuning.</summary>
        public const int MaxSummaryChars = 300;

        /// <summary>How many events one source may feed per game-day before it is throttled (anti-flood). Tuning.</summary>
        public const int MaxPerSourcePerDay = 20;

        /// <summary>Clamps a magnitude into the carryable range.</summary>
        public static int ClampMagnitude(int magnitude)
            => magnitude < 0
                ? 0
                : magnitude > MaxMagnitude
                    ? MaxMagnitude
                    : magnitude;

        /// <summary>
        ///   Trims and bounds a summary to the blob-safe length. Null or whitespace returns null, which the
        ///   caller treats as an invalid (rejected) event — a world event with no line says nothing.
        /// </summary>
        public static string BoundSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary)) return null;

            string trimmed = summary.Trim();

            return trimmed.Length <= MaxSummaryChars
                ? trimmed
                : trimmed.Substring(0, MaxSummaryChars);
        }

        /// <summary>
        ///   Whether a fed fact is the PLAYER'S OWN DEED (their chronicle, told back to them as something they did)
        ///   rather than news: true when the player is among the heroes who DID it. Until 2.5.23 the player's id
        ///   among the heroes it CONCERNS decided it, and a bridge naming a besieged town's OWNER turned a siege of
        ///   the player's town into a deed of the player's (tashmetu, 23/09/2026). Being named is being concerned,
        ///   not being the one who acted. The feeder describes roles; deciding what is the player's stays here, so
        ///   no bridge has to ask "is this the player?".
        /// </summary>
        public static bool IsPlayersOwnDeed(IEnumerable<string> actorHeroIds, string playerHeroId)
            => !string.IsNullOrEmpty(playerHeroId) && actorHeroIds != null && actorHeroIds.Contains(playerHeroId);

        /// <summary>True while a source is still within its daily quota (the host counts its sends this day).</summary>
        public static bool WithinDailyQuota(int sentToday) => sentToday < MaxPerSourcePerDay;

        /// <summary>
        ///   The longest an event's Details may be. A request (Bellum Civile / AI Rebel Relay integration, 17/11/2026) to
        ///   keep "complete event information" (kingdom digests of 10 to 20 developments, an old homeland's fall): the full
        ///   text is kept OUTSIDE the save, never cut, and refused whole beyond this. With the daily quota of events it
        ///   bounds how much a source can write in a day. Tuning.
        /// </summary>
        public const int MaxDetailsChars = 20000;

        /// <summary>True when a summary is longer than <see cref="MaxSummaryChars" /> and BoundSummary will cut it (the host says so in its log).</summary>
        public static bool SummaryIsCut(string summary) => summary != null && summary.Trim().Length > MaxSummaryChars;

        /// <summary>
        ///   Details as they will be kept: trimmed, or null when blank (no details). Never cut: a text longer than
        ///   <see cref="MaxDetailsChars" /> is refused whole by <see cref="DetailsFit" />, so nothing is silently lost.
        /// </summary>
        public static string NormalizeDetails(string details) => string.IsNullOrWhiteSpace(details) ? null : details.Trim();

        /// <summary>True when normalized details (null included) fit under <see cref="MaxDetailsChars" />.</summary>
        public static bool DetailsFit(string normalized) => normalized == null || normalized.Length <= MaxDetailsChars;

        /// <summary>
        ///   The name a text is kept under: the same source and text always give the same key, a changed text a new one. A
        ///   save holds only the key, so a save loaded later reads exactly the text it wrote, never a newer one written on
        ///   another branch of the same campaign (the story archive's lesson). Null for no text.
        /// </summary>
        public static string DetailsKey(string sourceMod, string normalized)
        {
            if (normalized == null) return null;

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes((sourceMod ?? "").Trim().ToLowerInvariant() + "" + normalized));

                return BitConverter.ToString(hash, 0, 16).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
