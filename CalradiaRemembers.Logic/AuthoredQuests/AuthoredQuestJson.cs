// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales (docs/design/quest-editor/goal.md): the few places where a quest file may be written two ways, so a
// file written before flags existed reads exactly as it did. A line is a plain string or {"text", "if"}; a canvas is
// one line or a list of them; a flag list ("if", "sets") is one name or a list of names. Anything else is refused
// with a plain message, which the loader reports as "not valid JSON (...)" like any other malformed file.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>A line: "text", or {"text": "...", "if": ...}. Written back the shortest way that keeps it.</summary>
   public sealed class AuthoredLineConverter : JsonConverter
   {
      public override bool CanConvert(Type objectType) => objectType == typeof(AuthoredLine);

      public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
         => Read(JToken.Load(reader), reader.Path);

      internal static AuthoredLine Read(JToken token, string path)
      {
         switch (token.Type)
         {
            case JTokenType.Null:
               return null;
            case JTokenType.String:
            case JTokenType.Date:
               return new AuthoredLine((string) token);
            case JTokenType.Object:
               var o = (JObject) token;
               JToken text = Property(o, "text"), cond = Property(o, "if");
               if (text != null && text.Type != JTokenType.String && text.Type != JTokenType.Null)
                  throw new JsonSerializationException($"at {path}: a line's \"text\" must be words in quotes");

               return new AuthoredLine {Text = (string) text, If = OneOrManyConverter.Read(cond, path)};
            default:
               throw new JsonSerializationException($"at {path}: a line must be words in quotes, or {{\"text\": ..., \"if\": ...}}");
         }
      }

      public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
      {
         var line = (AuthoredLine) value;
         if (line.If == null || line.If.Count == 0)
         {
            writer.WriteValue(line.Text);

            return;
         }

         writer.WriteStartObject();
         writer.WritePropertyName("text");
         writer.WriteValue(line.Text);
         writer.WritePropertyName("if");
         OneOrManyConverter.Write(writer, line.If);
         writer.WriteEndObject();
      }

      /// <summary>Property names are matched without regard to case, as Newtonsoft matches every other field.</summary>
      internal static JToken Property(JObject o, string name)
         => o.Property(name)?.Value ?? o.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
   }

   /// <summary>A canvas: one line, or a list of lines (each said only while its flags hold).</summary>
   public sealed class AuthoredLinesConverter : JsonConverter
   {
      public override bool CanConvert(Type objectType) => objectType == typeof(List<AuthoredLine>);

      public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
      {
         JToken token = JToken.Load(reader);
         if (token.Type == JTokenType.Null) return new List<AuthoredLine>();
         if (token.Type != JTokenType.Array) return new List<AuthoredLine> {AuthoredLineConverter.Read(token, reader.Path)}.Where(l => l != null).ToList();

         return token.Children().Select(t => AuthoredLineConverter.Read(t, reader.Path)).Where(l => l != null).ToList();
      }

      public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
      {
         var lines = (List<AuthoredLine>) value;
         if (lines.Count == 1)
         {
            serializer.Serialize(writer, lines[0]);

            return;
         }

         if (lines.Count == 0)
         {
            writer.WriteValue("");

            return;
         }

         writer.WriteStartArray();
         foreach (AuthoredLine l in lines) serializer.Serialize(writer, l);
         writer.WriteEndArray();
      }
   }

   /// <summary>A list of flag names: "name", or ["a", "!b"].</summary>
   public sealed class OneOrManyConverter : JsonConverter
   {
      public override bool CanConvert(Type objectType) => objectType == typeof(List<string>);

      public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
         => Read(JToken.Load(reader), reader.Path);

      internal static List<string> Read(JToken token, string path)
      {
         if (token == null || token.Type == JTokenType.Null) return new List<string>();
         if (token.Type == JTokenType.String) return new List<string> {(string) token};
         if (token.Type == JTokenType.Array && token.Children().All(t => t.Type == JTokenType.String))
            return token.Children().Select(t => (string) t).ToList();

         throw new JsonSerializationException($"at {path}: flags must be a name in quotes, or a list of names in quotes");
      }

      public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => Write(writer, (List<string>) value);

      internal static void Write(JsonWriter writer, List<string> flags)
      {
         if (flags.Count == 1)
         {
            writer.WriteValue(flags[0]);

            return;
         }

         writer.WriteStartArray();
         foreach (string f in flags) writer.WriteValue(f);
         writer.WriteEndArray();
      }
   }
}
