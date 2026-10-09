// Code written by Gabriel Mailhot, 04/10/2026.
// What these pin: that the tale editor reads quest files with the fields, types, defaults, help and closed lists of the
// model the game reads, not of last week's (docs/design/quest-editor/goal.md: "A JSON Schema generated from the C#
// model ... kept current by a test with the regenerate with CR_UPDATE_MAPS=1 pattern"). If they are wrong, the editor
// offers a field the game ignores, misses one it reads, or calls a file sound that the game refuses.
//
// A failure here is never a bug in the code: the model moved and the schema did not. Regenerate (Tools/Update-Maps.ps1)
// and commit the schema, and the editor carrying its copy, with the change.

#region

using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Maps
{
   [TestFixture]
   public sealed class QuestEditorIsCurrentTests
   {
      // The schema an author or another tool reads: the model as it stands.
      [Test]
      public void GIVEN_the_quest_model_as_it_stands_WHEN_the_schema_is_regenerated_THEN_it_matches_the_committed_schema()
         => ApprovedMap.Verify(QuestSchemaMap.Path, QuestSchemaMap.Generate());

      // The copy the shipped editor works from, offline: the same schema, so the editor cannot fall behind the game.
      [Test]
      public void GIVEN_the_quest_model_as_it_stands_WHEN_the_schema_is_regenerated_THEN_the_copy_inside_the_editor_matches()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}.");

         string path = System.IO.Path.Combine(SourceTree.ModRepo!, QuestSchemaMap.EditorPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
         string html = File.ReadAllText(path);
         int start = html.IndexOf(QuestSchemaMap.EmbedStart, StringComparison.Ordinal);
         Assert.That(start, Is.GreaterThanOrEqualTo(0), $"{QuestSchemaMap.EditorPath} has no {QuestSchemaMap.EmbedStart} block.");
         start += QuestSchemaMap.EmbedStart.Length;
         int end = html.IndexOf(QuestSchemaMap.EmbedEnd, start, StringComparison.Ordinal);

         string fresh = "\n" + QuestSchemaMap.Generate();
         string embedded = html.Substring(start, end - start);
         if (Environment.GetEnvironmentVariable(ApprovedMap.UpdateVariable) == "1")
         {
            if (embedded.Replace("\r\n", "\n") != fresh) File.WriteAllText(path, html.Substring(0, start) + fresh + html.Substring(end));

            return;
         }

         Assert.That(embedded.Replace("\r\n", "\n"), Is.EqualTo(fresh),
            $"The schema inside {QuestSchemaMap.EditorPath} no longer matches the model. Regenerate with Tools/Update-Maps.ps1 and commit the editor with the change.");
      }

      // "Start from the werewolf as a template" (goal): the template inside the editor is the werewolf CR ships, so an
      // author never starts from a stale copy.
      [Test]
      public void GIVEN_the_werewolf_that_ships_WHEN_the_editor_offers_it_as_a_template_THEN_it_is_the_same_tale()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"Cannot see {SourceTree.MissingAnchor}.");

         const string start = "<script id=\"template-werewolf\" type=\"application/json\">";
         string path = System.IO.Path.Combine(SourceTree.ModRepo!, QuestSchemaMap.EditorPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
         string shipped = File.ReadAllText(AuthoredTales.WolfFile).Replace("\r\n", "\n").TrimEnd('\n');
         string html = File.ReadAllText(path);
         int from = html.IndexOf(start, StringComparison.Ordinal) + start.Length;
         int to = html.IndexOf(QuestSchemaMap.EmbedEnd, from, StringComparison.Ordinal);
         string embedded = html.Substring(from, to - from).Replace("\r\n", "\n").Trim('\n');

         if (Environment.GetEnvironmentVariable(ApprovedMap.UpdateVariable) == "1")
         {
            if (embedded != shipped) File.WriteAllText(path, html.Substring(0, from) + "\n" + shipped + "\n" + html.Substring(to));

            return;
         }

         Assert.That(embedded, Is.EqualTo(shipped), "The werewolf inside the editor is not the one CR ships. Regenerate with Tools/Update-Maps.ps1.");
      }
   }
}
