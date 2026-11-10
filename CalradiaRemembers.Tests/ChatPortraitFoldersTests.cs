// Code written by Gabriel Mailhot, 10/11/2026.
// Decides where a chat portrait is looked for, and which picture wins when the player has one in two places. Gabriel
// (10/11/2026), writing the Discord tutorial on portraits: ChatPortraits was the only folder of the player's own files the
// Custom folder did not cover, so deleting the module folder for a clean reinstall took the pictures with it. If the order
// is wrong, a picture the player moved to Custom is hidden by an old copy left in ModuleData; if the old folder is dropped,
// every portrait dropped before 2.8.8 disappears from the chat.

#region

using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ChatPortraitFoldersTests
   {
      // STAKE: the Custom folder is the player's, kept across updates (and outside the module when the MCM path is set);
      // a picture there must win over one left in the module's folder, which is still read so no portrait dropped before
      // 2.8.8 is lost.
      [Test]
      public void GIVEN_a_custom_folder_WHEN_portrait_folders_are_listed_THEN_the_custom_one_comes_before_the_modules_own()
      {
         ChatPortraitFolders.SearchOrder(@"D:\MyCR", @"G\CR\ModuleData")
                            .Should().Equal(Path.Combine(@"D:\MyCR", "ChatPortraits"), Path.Combine(@"G\CR\ModuleData", "ChatPortraits"));
      }

      // STAKE: with no Custom folder resolved, portraits keep working exactly as before, from the module's folder alone.
      [Test]
      public void GIVEN_no_custom_folder_WHEN_portrait_folders_are_listed_THEN_only_the_modules_own_is_read()
      {
         ChatPortraitFolders.SearchOrder(null, @"G\CR\ModuleData").Should().Equal(Path.Combine(@"G\CR\ModuleData", "ChatPortraits"));
      }

      // STAKE: a Custom path the player pointed at the module's own ModuleData must not list the same folder twice (every
      // lookup would test each file twice for nothing).
      [Test]
      public void GIVEN_the_custom_folder_is_the_module_data_folder_WHEN_portrait_folders_are_listed_THEN_it_is_listed_once()
      {
         IReadOnlyList<string> folders = ChatPortraitFolders.SearchOrder(@"G\CR\ModuleData\", @"G\CR\ModuleData");

         folders.Should().HaveCount(1);
      }
   }
}
