// Code written by Gabriel Mailhot, 08/10/2026.
// The chat window's title. Gabriel (08/10/2026): "In the chat's title, we could remove 'Calradia Remembers -'". The
// player knows which window they are in; the title says whom (or where) they speak with, alone. The mod's name stands
// only when there is nothing else to name.
// If this breaks, every title carries the mod's name again, or a window opens with an empty title.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ChatTitleTextTests
   {
      // A character, a scene, a council's place: the name alone.
      [Test]
      public void GIVEN_someone_or_somewhere_to_name_WHEN_the_title_is_written_THEN_it_is_the_name_alone()
      {
         ChatTitleText.For("Lady Kun").Should().Be("Lady Kun");
         ChatTitleText.For("The wood by Atrion").Should().Be("The wood by Atrion");
         ChatTitleText.For("  Pravend ").Should().Be("Pravend");
      }

      // Nothing to name: the mod's name, never an empty title.
      [Test]
      public void GIVEN_nothing_to_name_WHEN_the_title_is_written_THEN_it_is_the_mods_name()
      {
         ChatTitleText.For(null).Should().Be(ChatTitleText.Fallback);
         ChatTitleText.For("   ").Should().Be(ChatTitleText.Fallback);
      }
   }
}
