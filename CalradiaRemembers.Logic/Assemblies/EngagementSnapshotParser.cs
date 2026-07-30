// Code written by Gabriel Mailhot, 30/07/2026.
// Council "Engagements" feature, slice B: turns the LLM extractor's raw JSON output into the snapshot
// EngagementLedger.Reconcile consumes. This is pure and heavily tolerant on purpose: the input is LLM output, so
// it may be fenced in markdown, wrapped in prose, or occasionally malformed, and none of that may throw mid a
// live council turn. Kept here rather than only exercised live so the fragile parsing is unit-tested.

#region

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Parses a <see cref="CouncilEngagementExtractor" />-shaped JSON array into snapshot items.</summary>
    public static class EngagementSnapshotParser
    {
        /// <summary>
        ///   Parses <paramref name="raw" /> (the extractor's raw content) into snapshot items ready for
        ///   <c>EngagementLedger.Reconcile</c>. Never throws: blank input, a missing array, or a malformed tail
        ///   all degrade to an empty list (or whatever well-formed items were parsed before the trouble), because
        ///   a bad extraction must never crash the council rather than simply not updating the ledger this turn.
        ///   Tolerates the model wrapping the array in markdown fences or surrounding prose by locating the JSON
        ///   array itself: the first '[' through its matching last ']'. An element missing a non-blank "id" or a
        ///   non-blank "decision" is skipped, since <c>EngagementLedger.Reconcile</c> would ignore it anyway and
        ///   dropping it here keeps the ledger input clean.
        /// </summary>
        public static IReadOnlyList<EngagementSnapshotItem> Parse(string raw)
        {
            var results = new List<EngagementSnapshotItem>();
            if (string.IsNullOrWhiteSpace(raw)) return results;

            string arrayText = ExtractArrayText(raw);
            if (arrayText == null) return results;

            try
            {
                using (var stringReader = new StringReader(arrayText))
                using (var jsonReader = new JsonTextReader(stringReader))
                {
                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.StartArray) return results;

                    while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
                    {
                        if (jsonReader.TokenType != JsonToken.StartObject) continue;

                        // Load ONE object at a time: a malformed element further down the array must not erase
                        // the well-formed ones already read before it.
                        JObject obj;
                        try
                        {
                            obj = JObject.Load(jsonReader);
                        }
                        catch
                        {
                            break;
                        }

                        AddIfValid(results, obj);
                    }
                }
            }
            catch
            {
                // Any other reader failure: keep whatever well-formed items were parsed so far.
            }

            return results;
        }

        #region private

        /// <summary>
        ///   Maps one flat element to a snapshot item and adds it to <paramref name="results" />, unless it is
        ///   missing a non-blank "id" or "decision" (an extraction hiccup <c>EngagementLedger.Reconcile</c> would
        ///   ignore anyway).
        /// </summary>
        private static void AddIfValid(List<EngagementSnapshotItem> results, JObject obj)
        {
            string id = (string)obj["id"];
            string decision = (string)obj["decision"];
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(decision)) return;

            string subject = (string)obj["subject"];
            string status = (string)obj["status"];

            results.Add(new EngagementSnapshotItem {
                Key = id.Trim(),
                Subject = subject?.Trim() ?? "",
                Text = decision.Trim(),
                Status = string.Equals(status?.Trim(), "agreed", StringComparison.OrdinalIgnoreCase)
                    ? EngagementStatus.Agreed
                    : EngagementStatus.Discussed
            });
        }

        /// <summary>
        ///   Locates the JSON array within <paramref name="raw" /> by its first '[' and matching last ']', so a
        ///   markdown fence or surrounding prose around the array does not stop the parse. Returns null when no
        ///   opening bracket is found at all.
        /// </summary>
        private static string ExtractArrayText(string raw)
        {
            int start = raw.IndexOf('[');
            int end = raw.LastIndexOf(']');
            if (start < 0 || end < start) return null;

            return raw.Substring(start, end - start + 1);
        }

        #endregion
    }
}
