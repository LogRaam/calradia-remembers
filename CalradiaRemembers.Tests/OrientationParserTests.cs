// Code written by Gabriel Mailhot, 10/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class OrientationParserTests
    {
        [TestCase("straight", SexualOrientation.Heterosexual)]
        [TestCase("Hetero", SexualOrientation.Heterosexual)]
        [TestCase("bi", SexualOrientation.Bisexual)]
        [TestCase("BISEXUAL", SexualOrientation.Bisexual)]
        [TestCase("gay", SexualOrientation.Homosexual)]
        [TestCase("lesbian", SexualOrientation.Homosexual)]
        [TestCase("pan", SexualOrientation.Pansexual)]
        [TestCase("asexual", SexualOrientation.Asexual)]
        [TestCase("bi-curious", SexualOrientation.BiCurious)]
        public void GIVEN_a_known_word_WHEN_parsing_THEN_it_maps_to_the_orientation(string raw, SexualOrientation expected)
        {
            OrientationParser.Parse(raw).Should().Be(expected);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("attack helicopter")]
        public void GIVEN_an_unknown_or_empty_word_WHEN_parsing_THEN_it_returns_null(string raw)
        {
            OrientationParser.Parse(raw).Should().BeNull();
        }
    }
}
