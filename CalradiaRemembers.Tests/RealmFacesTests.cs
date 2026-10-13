// Code written by Gabriel Mailhot, 12/10/2026.
// Queens with a chosen face (Gabriel, 12/10/2026: "une reine par faction", made in the character editor; "une reine par
// Empire, et on habille la reine du roi"). A faces file says which face the queen of each realm wears; CR dresses the
// ruler's wife in it and gives a former queen her own face back. If this is wrong: a broken line crashes the face
// generator or dresses nobody in silence, a mod's face overrides the player's own choice, two women wear one face, or a
// widowed king's new wife never gets the face while the old queen keeps it forever.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RealmFacesTests
   {
      private const string Key = "<BodyProperties version=\"4\" age=\"25.84\" weight=\"0.5\" build=\"0.5\" key=\"000AB0\" />";

      // The editor's own line (Ctrl+C) is what the player pastes, one per realm.
      [Test]
      public void GIVEN_a_faces_file_WHEN_read_THEN_each_realm_has_the_line_the_editor_copied()
      {
         var problems = new List<string>();
         IReadOnlyList<RealmFace> faces = RealmFaceFile.Parse("{ \"queens\": { \"vlandia\": \"" + Key.Replace("\"", "\\\"") + "\" } }", "faces.json", problems);

         faces.Should().ContainSingle(f => f.RealmId == "vlandia" && f.Key == Key && f.Source == "faces.json");
         problems.Should().BeEmpty();
      }

      // A line that is not the editor's would never load as a face: it is reported and left out, never applied.
      [Test]
      public void GIVEN_a_line_that_is_no_face_or_a_file_that_is_no_json_WHEN_read_THEN_it_is_reported_and_left_out()
      {
         var problems = new List<string>();

         RealmFaceFile.Parse("{ \"queens\": { \"aserai\": \"a pretty face\" } }", "faces.json", problems).Should().BeEmpty();
         RealmFaceFile.Parse("{ queens: [", "broken.json", problems).Should().BeEmpty();
         problems.Should().HaveCount(2);
      }

      // The player's own faces (Custom) win over a mod's, a mod's over CR's: one face per realm, the first read keeps it.
      [Test]
      public void GIVEN_the_same_realm_in_several_files_WHEN_merged_THEN_the_first_read_keeps_it()
      {
         var mine = new RealmFace {RealmId = "sturgia", Key = Key, Source = "Custom"};
         var mods = new RealmFace {RealmId = "sturgia", Key = Key.Replace("000AB0", "111"), Source = "SomeMod"};
         var crs = new RealmFace {RealmId = "battania", Key = Key, Source = "CalradiaRemembers"};

         IReadOnlyDictionary<string, RealmFace> merged = RealmFaceFile.Merge(new[] {mine, mods, crs});

         merged["sturgia"].Source.Should().Be("Custom");
         merged.Keys.Should().BeEquivalentTo("sturgia", "battania");
      }

      // Gabriel (12/10/2026) sends a chat portrait per queen: a realm may name one beside its face, and the plain line
      // (a face alone) still reads as before, so a file written for 2.7.10's first faces keeps working.
      [Test]
      public void GIVEN_a_realm_with_a_face_and_a_portrait_WHEN_read_THEN_both_are_kept_and_a_face_alone_still_reads()
      {
         var problems = new List<string>();
         string esc = Key.Replace("\"", "\\\"");
         IReadOnlyList<RealmFace> faces = RealmFaceFile.Parse(
            "{ \"queens\": { \"aserai\": { \"face\": \"" + esc + "\", \"portrait\": \"QueenPortraits/queen_aserai.jpg\" }, \"vlandia\": \"" + esc + "\" } }",
            "faces.json", problems);

         faces.Should().ContainSingle(f => f.RealmId == "aserai" && f.Key == Key && f.Portrait == "QueenPortraits/queen_aserai.jpg");
         faces.Should().ContainSingle(f => f.RealmId == "vlandia" && f.Key == Key && f.Portrait == null);
         problems.Should().BeEmpty();
      }

      // A faces file can come from any mod: its portrait must stay a picture inside that mod's own folder, never a path
      // that climbs out of it or points anywhere on the disk. A bad portrait is reported and dropped; the face still dresses her.
      [Test]
      public void GIVEN_a_portrait_outside_the_faces_folder_or_no_picture_WHEN_read_THEN_it_is_reported_and_the_face_alone_is_kept()
      {
         string esc = Key.Replace("\"", "\\\"");
         foreach (string bad in new[] {"../../secret.jpg", "C:/Windows/x.png", "/etc/x.png", "queen.exe", "QueenPortraits\\\\..\\\\x.jpg"})
         {
            var problems = new List<string>();
            IReadOnlyList<RealmFace> faces = RealmFaceFile.Parse(
               "{ \"queens\": { \"khuzait\": { \"face\": \"" + esc + "\", \"portrait\": \"" + bad + "\" } } }", "faces.json", problems);

            faces.Should().ContainSingle(f => f.RealmId == "khuzait" && f.Key == Key && f.Portrait == null, bad);
            problems.Should().ContainSingle(bad);
         }
      }

      // ── Who wears it ──

      // The ruler's wife wears her realm's face; the queen already dressed is left as she is.
      [Test]
      public void GIVEN_a_queen_not_yet_dressed_WHEN_planned_THEN_she_is_dressed_and_a_dressed_one_is_left_alone()
      {
         RealmFacePlan plan = RealmFacePlanner.Plan(new RealmFaceFacts {
            FaceRealms = new[] {"vlandia", "aserai"},
            QueenOf = new Dictionary<string, string> {["vlandia"] = "queen_v", ["aserai"] = "queen_a"},
            Dressed = new Dictionary<string, string> {["queen_a"] = "aserai"}
         });

         plan.Dress.Should().BeEquivalentTo(new Dictionary<string, string> {["queen_v"] = "vlandia"});
         plan.Restore.Should().BeEmpty();
      }

      // A king who remarries: the new queen wears the face, the old one gets her own face back.
      [Test]
      public void GIVEN_a_new_queen_WHEN_planned_THEN_she_is_dressed_and_the_former_queen_gets_her_face_back()
      {
         RealmFacePlan plan = RealmFacePlanner.Plan(new RealmFaceFacts {
            FaceRealms = new[] {"vlandia"},
            QueenOf = new Dictionary<string, string> {["vlandia"] = "queen_new"},
            Dressed = new Dictionary<string, string> {["queen_old"] = "vlandia"}
         });

         plan.Dress.Keys.Should().Equal("queen_new");
         plan.Restore.Should().Equal("queen_old");
      }

      // A widower king dresses nobody; a face taken out of the file gives the queen her own face back.
      [Test]
      public void GIVEN_no_queen_or_no_face_any_more_WHEN_planned_THEN_nobody_is_dressed_and_the_face_is_given_back()
      {
         RealmFacePlan plan = RealmFacePlanner.Plan(new RealmFaceFacts {
            FaceRealms = new[] {"vlandia"},
            QueenOf = new Dictionary<string, string> {["vlandia"] = null, ["aserai"] = "queen_a"},
            Dressed = new Dictionary<string, string> {["queen_a"] = "aserai"}
         });

         plan.Dress.Should().BeEmpty();
         plan.Restore.Should().Equal("queen_a");
      }
   }
}
