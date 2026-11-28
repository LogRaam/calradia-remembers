// Code written by Gabriel Mailhot, 28/11/2026.
// The conversation log: one Markdown file per chat, in the form of the group-scene simulator's transcripts. Gabriel
// (28/11/2026): "ajoute une option MCM qui permet de créer un log d'une conversation sous la forme que tu faisais déjà (ex:
// cr_group_third_20261127_235024.md) et qui explique le Why each turn went as it did (judge -> referee, scene reader)". This
// renders it from facts only: the scene as it was recorded, then, turn by turn, what the mod knows about why (the models that
// answered and what they cost, the deeds the game carried out or refused, the fallbacks, the recalled passages). Nothing is
// invented: the judge and the referee do not run in the two-person chat yet, so no line speaks for them; their entries join
// the same list when those stages exist (ChatLogWhyKind). Keys and URLs are masked, and no prompt is ever carried.

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
    /// <summary>What a recorded line of the scene is.</summary>
    public enum ChatLogLineKind
    {
        /// <summary>A spoken line (the player's or a character's).</summary>
        Speech,

        /// <summary>A narration block.</summary>
        Narration,

        /// <summary>An out-of-character note the game wrote to the model (a refused deed).</summary>
        Note
    }

    /// <summary>One line of the scene, as the chat recorded it.</summary>
    public sealed class ChatLogLine
    {
        /// <summary>The turn it was recorded in (0 = the opening, before the player's first line).</summary>
        public int Turn { get; init; }

        /// <summary>Who spoke it ("You" for the player).</summary>
        public string Speaker { get; init; }

        /// <summary>Who it was addressed to, or null when the chat does not say.</summary>
        public string To { get; init; }

        /// <summary>The words as recorded.</summary>
        public string Text { get; init; }

        /// <summary>Whether the player spoke it.</summary>
        public bool IsPlayer { get; init; }

        /// <summary>What the line is.</summary>
        public ChatLogLineKind Kind { get; init; }

        /// <summary>The deeds the game read under this line, each as the game reported it.</summary>
        public IReadOnlyList<string> Actions { get; init; }

        /// <summary>The kind of a recorded line: a player line opening on "(Out of character" is the game's note to the model.</summary>
        public static ChatLogLineKind KindOf(bool isPlayer, bool isNarration, string text)
        {
            if (isNarration) return ChatLogLineKind.Narration;

            return isPlayer && (text ?? "").TrimStart().StartsWith("(Out of character", StringComparison.Ordinal) ? ChatLogLineKind.Note : ChatLogLineKind.Speech;
        }
    }

    /// <summary>The kinds of a "why" entry. Later stages add theirs (judge, referee, dramaturge, keeper) to the same list.</summary>
    public static class ChatLogWhyKind
    {
        /// <summary>A reply's timing, as the chat measured it.</summary>
        public const string Reply = "reply";

        /// <summary>A deed the game carried out, refused or dropped.</summary>
        public const string Action = "action";

        /// <summary>A beat redone on another model (a refusal or a cut reply).</summary>
        public const string Fallback = "fallback";

        /// <summary>Passages of earlier conversations recalled for the turn (Long Memory).</summary>
        public const string Recall = "recall";

        /// <summary>Reserved for stage 6 and 7: who should speak next, as judged.</summary>
        public const string Judge = "judge";

        /// <summary>Reserved for stage 6 and 7: the referee's pick.</summary>
        public const string Referee = "referee";

        /// <summary>Reserved for stage 6 and 7: the dramaturge's reading of the scene.</summary>
        public const string Dramaturge = "dramaturge";

        /// <summary>Reserved for stage 6 and 7: the scene keeper's reading.</summary>
        public const string Keeper = "keeper";
    }

    /// <summary>One entry of "why each turn went as it did".</summary>
    public sealed class ChatLogWhy
    {
        /// <summary>The turn it belongs to.</summary>
        public int Turn { get; init; }

        /// <summary>What it is (see <see cref="ChatLogWhyKind" />).</summary>
        public string Kind { get; init; }

        /// <summary>What happened, in plain words.</summary>
        public string Text { get; init; }
    }

    /// <summary>One call to a model made while the chat was open.</summary>
    public sealed class ChatLogCall
    {
        /// <summary>The turn it was made in.</summary>
        public int Turn { get; init; }

        /// <summary>The role it served: prose, interpreter, NSFW or other (see <see cref="ChatLogRoles" />).</summary>
        public string Role { get; init; }

        /// <summary>The model that answered.</summary>
        public string Model { get; init; }

        /// <summary>The prompt tokens, when the provider reported them.</summary>
        public int? PromptTokens { get; init; }

        /// <summary>Of the prompt tokens, how many came from the provider's cache, when reported.</summary>
        public int? CachedTokens { get; init; }

        /// <summary>The reply tokens, when reported.</summary>
        public int? CompletionTokens { get; init; }

        /// <summary>What the provider billed, in US dollars, when it said so.</summary>
        public double? Cost { get; init; }

        /// <summary>Why the model stopped ("stop", "length", ...), when reported.</summary>
        public string FinishReason { get; init; }

        /// <summary>Whether the reply came from the client's second attempt.</summary>
        public bool WasRetried { get; init; }

        /// <summary>The error, when the call failed.</summary>
        public string Error { get; init; }
    }

    /// <summary>The models of the chat's roles, as set when it opened.</summary>
    public sealed class ChatLogModels
    {
        /// <summary>The mode's display name (Standard, Enhanced).</summary>
        public string Mode { get; init; }

        /// <summary>The model that writes the replies.</summary>
        public string Prose { get; init; }

        /// <summary>The action interpreter's model, or null outside Enhanced.</summary>
        public string Interpreter { get; init; }

        /// <summary>The NSFW model (the refusal fallback in Enhanced).</summary>
        public string Nsfw { get; init; }
    }

    /// <summary>Which role a call served.</summary>
    public static class ChatLogRoles
    {
        /// <summary>The role names.</summary>
        public const string Prose = "prose", Interpreter = "interpreter", Nsfw = "NSFW", Other = "other";

        /// <summary>
        ///   The role of a call: the interpreter is the one caller that names its subject (ActionInterpreterComposer), the NSFW
        ///   model is told apart when it differs from the prose one, and anything else (a summary, a background call) is "other".
        /// </summary>
        public static string Of(ChatLogModels models, string model, string subject)
        {
            if (!string.IsNullOrEmpty(subject)) return Interpreter;
            if (string.IsNullOrEmpty(model) || models == null) return Other;
            if (Same(model, models.Nsfw) && !Same(model, models.Prose)) return Nsfw;

            return Same(model, models.Prose) ? Prose : Other;
        }

        private static bool Same(string a, string b) => !string.IsNullOrEmpty(b) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A deed the game was asked to carry out, and what it answered.</summary>
    public sealed class ChatLogActionFacts
    {
        /// <summary>The verb (give_gold, change_relation, ...).</summary>
        public string Verb { get; init; }

        /// <summary>Whether the game gave any account of it; false means it said nothing.</summary>
        public bool Reported { get; init; }

        /// <summary>Whether the game refused it (a guard, the action gate, the world as it stands).</summary>
        public bool WasRefused { get; init; }

        /// <summary>Whether the game has no such deed at all.</summary>
        public bool WasUnknownVerb { get; init; }

        /// <summary>The game's own message, or null.</summary>
        public string Message { get; init; }
    }

    /// <summary>How a deed reads in the log: "verb: done (message)", "verb: REFUSED (reason)", "verb: no such deed".</summary>
    public static class ChatLogActionText
    {
        /// <summary>The deed's line.</summary>
        public static string For(ChatLogActionFacts facts)
        {
            if (facts == null || string.IsNullOrWhiteSpace(facts.Verb)) return null;

            string verdict = facts.WasUnknownVerb ? "no such deed in the game"
                : facts.WasRefused ? "REFUSED"
                : facts.Reported ? "done"
                : "sent, no account given";
            string message = facts.WasUnknownVerb || string.IsNullOrWhiteSpace(facts.Message) ? "" : $" ({facts.Message.Trim()})";

            return $"{facts.Verb.Trim()}: {verdict}{message}";
        }
    }

    /// <summary>Everything one conversation log is made of.</summary>
    public sealed class ChatLogFacts
    {
        /// <summary>The character the chat was opened with.</summary>
        public string Character { get; init; }

        /// <summary>Where it took place, or null.</summary>
        public string Place { get; init; }

        /// <summary>The game day, as the game writes it, or null.</summary>
        public string Day { get; init; }

        /// <summary>The roles' models and the mode.</summary>
        public ChatLogModels Models { get; init; }

        /// <summary>When the window opened (real time).</summary>
        public DateTime Opened { get; init; }

        /// <summary>When it closed (real time).</summary>
        public DateTime Closed { get; init; }

        /// <summary>The setting, when the chat has one to give, or null.</summary>
        public string Setting { get; init; }

        /// <summary>The scene, in order.</summary>
        public IReadOnlyList<ChatLogLine> Lines { get; init; }

        /// <summary>The model calls made while it was open, in order.</summary>
        public IReadOnlyList<ChatLogCall> Calls { get; init; }

        /// <summary>The "why" entries, in order.</summary>
        public IReadOnlyList<ChatLogWhy> Why { get; init; }
    }

    /// <summary>Renders a conversation log as Markdown. Pure.</summary>
    public static class ChatLogRenderer
    {
        /// <summary>The scene's heading.</summary>
        public const string SceneHeading = "## The scene";

        /// <summary>The why section's heading.</summary>
        public const string WhyHeading = "## Why each turn went as it did";

        /// <summary>What the why section holds today, said once under its heading.</summary>
        public const string WhyScope =
            "What the mod records about each turn: the models that answered and what they cost, the deeds the game carried out, refused or dropped, the fallbacks and the recalled passages. No judge or referee runs in this chat.";

        private static readonly Regex Url = new Regex(@"https?://\S+", RegexOptions.CultureInvariant);

        private static readonly Regex Unsafe = new Regex(@"[^A-Za-z0-9_\-]+", RegexOptions.CultureInvariant);

        /// <summary>The file name for a chat with <paramref name="character" /> closed at <paramref name="at" />: cr_chat_&lt;name&gt;_&lt;yyyyMMdd_HHmmss&gt;.md.</summary>
        public static string FileName(string character, DateTime at)
        {
            string name = Unsafe.Replace((character ?? "").Trim().ToLowerInvariant(), "_").Trim('_');
            if (name.Length > 40) name = name.Substring(0, 40).TrimEnd('_');
            if (name.Length == 0) name = "someone";

            return $"cr_chat_{name}_{at.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.md";
        }

        /// <summary>The log, as Markdown. Keys and URLs masked.</summary>
        public static string Render(ChatLogFacts facts)
        {
            facts ??= new ChatLogFacts();
            List<ChatLogLine> lines = (facts.Lines ?? new List<ChatLogLine>()).Where(l => l != null).ToList();
            List<ChatLogCall> calls = (facts.Calls ?? new List<ChatLogCall>()).Where(c => c != null).ToList();
            List<ChatLogWhy> why = (facts.Why ?? new List<ChatLogWhy>()).Where(w => w != null).ToList();
            var sb = new StringBuilder();

            sb.AppendLine(Title(facts, lines));
            sb.AppendLine(Header(facts, calls));
            List<string> speakers = lines.Where(l => l.Kind == ChatLogLineKind.Speech && !string.IsNullOrWhiteSpace(l.Speaker)).Select(l => l.Speaker.Trim()).Distinct().ToList();
            if (speakers.Count > 0) sb.AppendLine($"Speakers: {string.Join("; ", speakers)}.");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(facts.Setting))
            {
                sb.AppendLine($"*{OneLine(facts.Setting)}*");
                sb.AppendLine();
            }

            sb.AppendLine(SceneHeading);
            if (lines.Count == 0) sb.AppendLine("(nothing was said)");
            foreach (ChatLogLine l in lines)
            {
                sb.AppendLine(SceneLine(l));
                if (l.Actions is {Count: > 0}) sb.AppendLine($"  `actions: {string.Join("; ", l.Actions.Select(OneLine))}`");
                sb.AppendLine();
            }

            sb.AppendLine(WhyHeading);
            sb.AppendLine(WhyScope);
            sb.AppendLine();
            foreach (int turn in lines.Select(l => l.Turn).Concat(calls.Select(c => c.Turn)).Concat(why.Select(w => w.Turn)).Distinct().OrderBy(t => t))
            {
                sb.AppendLine(TurnHeading(turn, lines));
                foreach (ChatLogCall c in calls.Where(c => c.Turn == turn)) sb.AppendLine($"- call: {CallLine(c)}");
                foreach (ChatLogWhy w in why.Where(w => w.Turn == turn)) sb.AppendLine($"- {w.Kind ?? "note"}: {OneLine(w.Text)}");
                sb.AppendLine();
            }

            return Url.Replace(PlayerReportComposer.Mask(sb.ToString().Replace("\r\n", "\n")), "[url]");
        }

        private static string Title(ChatLogFacts facts, List<ChatLogLine> lines)
        {
            List<string> others = lines.Where(l => !l.IsPlayer && l.Kind == ChatLogLineKind.Speech && !string.IsNullOrWhiteSpace(l.Speaker))
                                       .Select(l => l.Speaker.Trim()).Distinct().ToList();
            string with = string.IsNullOrWhiteSpace(facts.Character) ? null : facts.Character.Trim();
            if (with == null) with = others.Count > 0 ? others[0] : "someone";
            List<string> rest = others.Where(o => !string.Equals(o, with, StringComparison.Ordinal)).ToList();
            string who = rest.Count == 0 ? with : $"{with}, with {string.Join(", ", rest)}";
            string where = string.IsNullOrWhiteSpace(facts.Place) ? "" : $", {facts.Place.Trim()}";
            string when = string.IsNullOrWhiteSpace(facts.Day) ? "" : $", {facts.Day.Trim()}";

            return $"# Conversation with {who}{where}{when}";
        }

        private static string Header(ChatLogFacts facts, List<ChatLogCall> calls)
        {
            ChatLogModels m = facts.Models ?? new ChatLogModels();
            var parts = new List<string> {$"Mode: {(string.IsNullOrWhiteSpace(m.Mode) ? "(unknown)" : m.Mode)}"};
            parts.Add($"prose {Model(m.Prose)} ({calls.Count(c => c.Role == ChatLogRoles.Prose)} calls)");
            if (!string.IsNullOrWhiteSpace(m.Interpreter)) parts.Add($"interpreter {m.Interpreter} ({calls.Count(c => c.Role == ChatLogRoles.Interpreter)})");
            if (!string.IsNullOrWhiteSpace(m.Nsfw)) parts.Add($"NSFW {m.Nsfw} ({calls.Count(c => c.Role == ChatLogRoles.Nsfw)})");
            int other = calls.Count(c => c.Role == ChatLogRoles.Other);
            if (other > 0) parts.Add($"other calls while open ({other})");

            List<double> priced = calls.Where(c => c.Cost != null).Select(c => c.Cost.Value).ToList();
            int unpriced = calls.Count - priced.Count;
            string cost = priced.Count == 0
                ? "Cost: not reported"
                : $"Cost: ${priced.Sum().ToString("0.0000", CultureInfo.InvariantCulture)}{(unpriced > 0 ? $" ({unpriced} calls not priced)" : "")}";
            double minutes = Math.Max(0, (facts.Closed - facts.Opened).TotalMinutes);

            return $"{string.Join(", ", parts)}. {calls.Count} model calls. {cost}. {minutes.ToString("0.0", CultureInfo.InvariantCulture)} min.";
        }

        private static string SceneLine(ChatLogLine l)
        {
            string text = OneLine(l.Text);
            switch (l.Kind)
            {
                case ChatLogLineKind.Narration: return $"> {text}";
                case ChatLogLineKind.Note: return $"*(game note to the model)* {text}";
                default:
                    string to = string.IsNullOrWhiteSpace(l.To) ? "" : $" *(to {l.To.Trim()})*";

                    return $"**{(string.IsNullOrWhiteSpace(l.Speaker) ? "?" : l.Speaker.Trim())}**{to}: {text}";
            }
        }

        private static string TurnHeading(int turn, List<ChatLogLine> lines)
        {
            if (turn <= 0) return "**Opening**";

            ChatLogLine said = lines.FirstOrDefault(l => l.Turn == turn && l.IsPlayer && l.Kind == ChatLogLineKind.Speech);
            if (said == null) return $"**Turn {turn}**";

            string text = OneLine(said.Text);

            return $"**Turn {turn}** (\"{(text.Length > 60 ? text.Substring(0, 60).TrimEnd() + "..." : text)}\")";
        }

        private static string CallLine(ChatLogCall c)
        {
            var bits = new List<string> {$"{c.Role ?? ChatLogRoles.Other} {Model(c.Model)}"};
            if (!string.IsNullOrWhiteSpace(c.Error))
            {
                bits.Add($"FAILED: {OneLine(c.Error)}");

                return string.Join(", ", bits);
            }

            if (c.PromptTokens != null || c.CompletionTokens != null)
                bits.Add($"{c.PromptTokens?.ToString(CultureInfo.InvariantCulture) ?? "?"} in{(c.CachedTokens is > 0 ? $" ({c.CachedTokens.Value.ToString(CultureInfo.InvariantCulture)} cached)" : "")} / {c.CompletionTokens?.ToString(CultureInfo.InvariantCulture) ?? "?"} out");
            if (c.Cost != null) bits.Add($"${c.Cost.Value.ToString("0.0000", CultureInfo.InvariantCulture)}");
            if (!string.IsNullOrWhiteSpace(c.FinishReason)) bits.Add($"finish {c.FinishReason}");
            if (c.WasRetried) bits.Add("retried");

            return string.Join(", ", bits);
        }

        private static string Model(string model) => string.IsNullOrWhiteSpace(model) ? "(unknown model)" : model.Trim();

        private static string OneLine(string text)
            => string.Join(" ", (text ?? "").Replace("\r", "").Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0));
    }
}
