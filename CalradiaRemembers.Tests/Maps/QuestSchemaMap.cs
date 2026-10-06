// Code written by Gabriel Mailhot, 04/10/2026.
// The tale editor's contract (docs/design/quest-editor/goal.md, "the one real risk: two validators drifting apart"):
// the JSON Schema of a quest file, generated from the C# model the game reads, never written by hand. Its fields, types,
// defaults, help lines (QuestField) and closed lists (the loader's own) are what the editor shows and reads files by;
// a copy rides inside the shipped editor so it works offline. Both copies are compared with a fresh generation by the
// Maps suite, so a field added to the model and forgotten in the editor turns the suite red.
//
// Beside standard JSON Schema it carries what the editor's reader needs to read a file exactly as Newtonsoft does:
// "x-kind" on every field (how its value is read), the order of the properties (the order Newtonsoft writes them, which
// is the order the loader finds an unbound placeholder in), and "x-lists", the loader's closed lists by name.

#region

using System.Collections;
using System.Reflection;
using CalradiaRemembers.Logic.AuthoredQuests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   /// <summary>Generates docs/design/quest-editor/quest.schema.json from the quest model.</summary>
   internal static class QuestSchemaMap
   {
      public const string Path = "docs/design/quest-editor/quest.schema.json";

      /// <summary>The shipped editor, which carries a copy of the schema between these markers.</summary>
      public const string EditorPath = "CalradiaRemembers/Extension/TaleEditor.html";

      public const string EmbedStart = "<script id=\"quest-schema\" type=\"application/json\">";
      public const string EmbedEnd = "</script>";

      public static string Generate()
      {
         var defs = new JObject();
         var queue = new Queue<Type>(new[] {typeof(AuthoredQuest)});
         var seen = new HashSet<Type>();

         while (queue.Count > 0)
         {
            Type t = queue.Dequeue();
            if (!seen.Add(t)) continue;
            defs[t.Name] = Definition(t, queue);
         }

         defs["line"] = new JObject {
            ["description"] = "A line, written plain, or under flags so it is said only while they hold.",
            ["oneOf"] = new JArray(
               new JObject {["type"] = "string"},
               new JObject {
                  ["type"] = "object",
                  ["properties"] = new JObject {
                     ["text"] = Field(typeof(AuthoredLine).GetProperty(nameof(AuthoredLine.Text))!, new Queue<Type>()),
                     ["if"] = Field(typeof(AuthoredLine).GetProperty(nameof(AuthoredLine.If))!, new Queue<Type>())
                  }
               },
               new JObject {["type"] = "null"})
         };
         defs["lines"] = new JObject {
            ["description"] = "One line, or a list of lines, each said while its flags hold.",
            ["oneOf"] = new JArray(new JObject {["$ref"] = "#/$defs/line"}, new JObject {["type"] = "array", ["items"] = new JObject {["$ref"] = "#/$defs/line"}})
         };
         defs["flags"] = new JObject {
            ["description"] = "A flag's name (\"name\", or \"!name\" for not set), or a list of them; all must hold.",
            ["oneOf"] = new JArray(new JObject {["type"] = "string"}, new JObject {["type"] = "array", ["items"] = new JObject {["type"] = "string"}},
               new JObject {["type"] = "null"})
         };

         var lists = new JObject();
         foreach (FieldInfo f in typeof(AuthoredQuestLoader).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => typeof(IEnumerable<string>).IsAssignableFrom(f.FieldType)))
            lists[f.Name] = new JArray(((IEnumerable<string>) f.GetValue(null)!).Cast<object>().ToArray());

         var schema = new JObject {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = "https://calradia-remembers/quest.schema.json",
            ["title"] = "Calradia Remembers: a written tale",
            ["description"] = "GENERATED from CalradiaRemembers.Logic.AuthoredQuests by the Maps suite (QuestSchemaMap). Do not edit: "
                              + "regenerate with Tools/Update-Maps.ps1. A file holds one tale, or a list of tales.",
            ["oneOf"] = new JArray(new JObject {["$ref"] = "#/$defs/AuthoredQuest"},
               new JObject {["type"] = "array", ["items"] = new JObject {["$ref"] = "#/$defs/AuthoredQuest"}}),
            ["x-lists"] = lists,
            ["$defs"] = defs
         };

         return schema.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
      }

      private static JObject Definition(Type t, Queue<Type> queue)
      {
         var props = new JObject();
         foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetCustomAttribute<JsonPropertyAttribute>() != null))
            props[p.GetCustomAttribute<JsonPropertyAttribute>()!.PropertyName!] = Field(p, queue);

         return new JObject {
            ["type"] = new JArray("object", "null"),
            ["description"] = DocOf(t),
            ["properties"] = props
         };
      }

      private static JObject Field(PropertyInfo p, Queue<Type> queue)
      {
         var field = new JObject();
         Type type = p.PropertyType;
         Type? converter = p.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType;
         string kind;

         if (converter == typeof(OneOrManyConverter))
         {
            kind = "flags";
            field["$ref"] = "#/$defs/flags";
         }
         else if (converter == typeof(AuthoredLinesConverter))
         {
            kind = "lines";
            field["$ref"] = "#/$defs/lines";
         }
         else kind = KindOf(type, field, queue);

         field["x-kind"] = kind;
         QuestFieldAttribute? help = p.GetCustomAttribute<QuestFieldAttribute>();
         if (help != null)
         {
            field["description"] = help.Help;
            if (!help.Played) field["x-played"] = false;
            if (help.ClosedList != null)
            {
               var list = (IEnumerable<string>) typeof(AuthoredQuestLoader).GetField(help.ClosedList)!.GetValue(null)!;
               field["enum"] = new JArray(list.Cast<object>().ToArray());
               field["x-closed-list"] = help.ClosedList;
            }
         }

         object? sample = p.DeclaringType == typeof(AuthoredLine) ? null : Activator.CreateInstance(p.DeclaringType!);
         object? value = sample == null ? null : p.GetValue(sample);
         if (value is string or int or double or float or bool && kind != "flags") field["default"] = JToken.FromObject(value);
         else if (value != null) field["x-default-empty"] = true; // absent from the file, it is an empty list, map or object, not null

         return field;
      }

      private static string KindOf(Type type, JObject field, Queue<Type> queue)
      {
         if (type == typeof(string)) { field["type"] = new JArray("string", "null"); return "string"; }
         if (type == typeof(int)) { field["type"] = "integer"; return "int"; }
         if (type == typeof(double) || type == typeof(float)) { field["type"] = "number"; return "number"; }
         if (type == typeof(bool)) { field["type"] = "boolean"; return "bool"; }
         if (type == typeof(int[])) { field["type"] = new JArray("array", "null"); field["items"] = new JObject {["type"] = "integer"}; return "int[]"; }
         if (type == typeof(AuthoredLine)) { field["$ref"] = "#/$defs/line"; return "line"; }

         if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
         {
            Type item = type.GetGenericArguments()[0];
            field["type"] = new JArray("array", "null");
            var items = new JObject();
            string itemKind = KindOf(item, items, queue);
            if (item == typeof(string)) itemKind = "string";
            field["items"] = items;

            return itemKind + "[]";
         }

         if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
         {
            var values = new JObject();
            string valueKind = KindOf(type.GetGenericArguments()[1], values, queue);
            field["type"] = new JArray("object", "null");
            field["additionalProperties"] = values;

            return "map:" + valueKind;
         }

         if (type.IsClass)
         {
            queue.Enqueue(type);
            field["$ref"] = "#/$defs/" + type.Name;

            return "object:" + type.Name;
         }

         throw new InvalidOperationException($"QuestSchemaMap cannot describe {type.Name}; teach it, and the editor's reader with it.");
      }

      /// <summary>The type's own first doc sentence, read from the source as the other maps read docs; its name when it has none.</summary>
      private static string DocOf(Type t)
      {
         if (!SourceTree.IsReachable) return t.Name;
         Microsoft.CodeAnalysis.SyntaxNode? declaration = SourceTree.Production
            .Where(f => f.RelativePath.Contains("AuthoredQuests/"))
            .SelectMany(f => f.Root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>())
            .FirstOrDefault(c => c.Identifier.Text == t.Name);
         string doc = declaration == null ? "" : DocText.FirstSentenceOf(declaration);

         return string.IsNullOrWhiteSpace(doc) ? t.Name : doc;
      }
   }
}
