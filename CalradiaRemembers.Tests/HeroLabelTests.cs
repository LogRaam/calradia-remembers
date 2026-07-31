// Code written by Gabriel Mailhot, 31/07/2026.
// Protects the one rule that tells a hero apart from a namesake: a Battanian lord "Luichan" who took
// a castle, per a world rumor, was conflated by an NPC with the player's own six-year-old son of the
// same bare first name, who was then spoken of as the one who took the castle. HeroLabel is the fix
// reused at every naming site (rumor, kin list, mentioned-hero hint); a wrong sentinel here would
// print something as absurd as "Luichan of No clan".

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class HeroLabelTests
    {
        // The floor case: nothing to disambiguate with, so the label is just the name, unchanged.
        [Test]
        public void GIVEN_a_name_alone_WHEN_describing_THEN_the_label_is_the_bare_name()
        {
            HeroLabel.Describe("Luichan", null).Should().Be("Luichan");
        }

        // The exact bug this increment fixes: a lord and a small child sharing "Luichan" must read
        // differently the moment a clan is known, or a rumor about one is read as news of the other.
        [Test]
        public void GIVEN_a_name_and_clan_WHEN_describing_THEN_the_clan_is_appended()
        {
            HeroLabel.Describe("Luichan", "Fen Company").Should().Be("Luichan of Fen Company");
        }

        // The full disambiguator: clan first, then culture in parentheses, so two "Luichan"s in
        // different clans (and possibly different cultures) are never read as the same person.
        [Test]
        public void GIVEN_a_name_clan_and_culture_WHEN_describing_THEN_both_are_appended_in_order()
        {
            HeroLabel.Describe("Luichan", "Fen Company", "Battanian").Should().Be("Luichan of Fen Company (Battanian)");
        }

        // Culture alone (no clan on record) still gives a sharper label than the bare name, so a
        // clanless notable is not left as indistinguishable as the child who shares his name.
        [Test]
        public void GIVEN_a_name_and_culture_but_no_clan_WHEN_describing_THEN_only_the_culture_is_appended()
        {
            HeroLabel.Describe("Luichan", null, "Battanian").Should().Be("Luichan (Battanian)");
        }

        // Clanless heroes carry the engine's own placeholder clan names rather than a real one; printing
        // "Luichan of No clan" would read as a lie about a clan that does not exist, so the sentinel
        // must be recognised and dropped rather than appended verbatim.
        [Test]
        public void GIVEN_the_no_clan_sentinel_WHEN_describing_THEN_it_is_omitted()
        {
            HeroLabel.Describe("Luichan", "No clan").Should().Be("Luichan");
        }

        // The engine's second clanless placeholder, seen on some wanderers and mercenaries: same
        // fabrication risk as "No clan" above, so it gets the same treatment.
        [Test]
        public void GIVEN_the_unaligned_sentinel_WHEN_describing_THEN_it_is_omitted()
        {
            HeroLabel.Describe("Luichan", "Unaligned").Should().Be("Luichan");
        }

        // A null clan is the common case for callers that never resolved one; it must fall through to
        // the bare name rather than throwing or printing " of ".
        [Test]
        public void GIVEN_a_null_clan_WHEN_describing_THEN_it_is_omitted_without_throwing()
        {
            HeroLabel.Describe("Luichan", null).Should().Be("Luichan");
        }

        // A blank (whitespace-only) clan string must be treated the same as null or the sentinels
        // above, since a caller passing an unresolved clan name could hand over "" just as easily.
        [Test]
        public void GIVEN_a_blank_clan_WHEN_describing_THEN_it_is_omitted()
        {
            HeroLabel.Describe("Luichan", "   ").Should().Be("Luichan");
        }

        // Anti-fabrication floor for the name itself: a null name must never throw at a call site that
        // forgot to guard it, and must not silently become the literal word "null" or a clan-only label.
        [Test]
        public void GIVEN_a_null_name_WHEN_describing_THEN_an_empty_string_is_returned_without_throwing()
        {
            HeroLabel.Describe(null, "Fen Company").Should().BeEmpty();
        }

        // A blank name is the same anti-fabrication case as null, just spelled differently by whatever
        // engine call produced it; it must come back unchanged, not garnished with a clan it cannot own.
        [Test]
        public void GIVEN_a_blank_name_WHEN_describing_THEN_it_is_returned_unchanged()
        {
            HeroLabel.Describe("   ", "Fen Company").Should().Be("   ");
        }
    }
}
