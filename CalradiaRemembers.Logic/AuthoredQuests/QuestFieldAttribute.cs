// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales and the tale editor (docs/design/quest-editor/goal.md): the one-line explanation an author reads
// beside each field of a quest file, kept ON the model so the editor's help and the game's own reading cannot drift.
// The JSON Schema (docs/design/quest-editor/quest.schema.json) is generated from these, and the editor takes its
// fields, its help and its "read but not yet played" marks from that schema.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>What an author is told about one field of a quest file, in the guide's words.</summary>
   [AttributeUsage(AttributeTargets.Property)]
   public sealed class QuestFieldAttribute : Attribute
   {
      public QuestFieldAttribute(string help) => Help = help;

      /// <summary>The one-line explanation shown beside the field.</summary>
      public string Help { get; }

      /// <summary>False for a field the game reads but does nothing with yet (HOWTO_Quests.md, "Read but not played yet").</summary>
      public bool Played { get; set; } = true;

      /// <summary>
      ///   The closed list of values the game understands, by the name of a public static list on
      ///   <see cref="AuthoredQuestLoader" /> (so the schema's enum is the loader's own list, never a copy).
      /// </summary>
      public string ClosedList { get; set; }
   }
}
