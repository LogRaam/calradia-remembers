// Code written by Gabriel Mailhot, 24/06/2026.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   One parsed keyword: a <see cref="Verb" /> (uppercased, e.g. <c>ON_DEATH</c>) and an optional
   ///   <see cref="Payload" /> (everything after the first colon). <see cref="Args" /> is the payload split
   ///   on commas, for convenience — the engine interprets them. Two tokens are equal when their verb and
   ///   payload match case-insensitively, so triggers compare cleanly.
   /// </summary>
   public sealed class KeywordToken : IEquatable<KeywordToken>
   {
      private KeywordToken(string verb, string payload, IReadOnlyList<string> args)
      {
         Verb = verb;
         Payload = payload;
         Args = args;
      }

      /// <summary>The payload split on commas (each trimmed, blanks dropped). Empty when there is no payload.</summary>
      public IReadOnlyList<string> Args { get; }

      /// <summary>True when this token matches any payload of the same verb — an empty payload or a lone <c>*</c>.</summary>
      public bool IsWildcardPayload => Payload.Length == 0 || Payload == "*";

      /// <summary>The raw text after the first colon, trimmed. Empty when the keyword carries no payload.</summary>
      public string Payload { get; }

      /// <summary>The keyword itself, uppercased and trimmed (e.g. <c>ON_MARRIAGE</c>). Empty for a blank input.</summary>
      public string Verb { get; }

      /// <summary>
      ///   Parses a keyword string. Splits on the FIRST colon: the left is the verb (uppercased), the rest
      ///   is the payload kept verbatim (further colons stay in the payload, e.g. <c>CHANGE_RELATION:Hero:+5</c>).
      ///   Null/blank yields an empty-verb token. Never throws.
      /// </summary>
      public static KeywordToken Parse(string raw)
      {
         string text = raw?.Trim() ?? string.Empty;

         if (text.Length == 0) return new KeywordToken(string.Empty, string.Empty, Array.Empty<string>());

         int colon = text.IndexOf(':');
         string verb = (colon < 0
            ? text
            : text.Substring(0, colon)).Trim().ToUpperInvariant();
         string payload = colon < 0
            ? string.Empty
            : text.Substring(colon + 1).Trim();

         var args = new List<string>();

         if (payload.Length <= 0) return new KeywordToken(verb, payload, args);
         foreach (string part in payload.Split(','))
         {
            string a = part.Trim();
            if (a.Length > 0) args.Add(a);
         }

         return new KeywordToken(verb, payload, args);
      }

      public bool Equals(KeywordToken other)
      {
         if (other is null) return false;

         return string.Equals(Verb, other.Verb, StringComparison.OrdinalIgnoreCase) && string.Equals(Payload, other.Payload, StringComparison.OrdinalIgnoreCase);
      }

      public override bool Equals(object obj) => Equals(obj as KeywordToken);

      public override int GetHashCode()
      {
         unchecked
         {
            int h = StringComparer.OrdinalIgnoreCase.GetHashCode(Verb ?? string.Empty);
            h = h * 397 ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Payload ?? string.Empty);

            return h;
         }
      }

      public override string ToString() => Payload.Length == 0
         ? Verb
         : $"{Verb}:{Payload}";
   }
}