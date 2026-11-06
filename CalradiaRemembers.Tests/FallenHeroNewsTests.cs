// Code written by Gabriel Mailhot, 05/11/2026.
// How loud a lord's death is, and how the news tells it. 3chmidt (Nexus, 05/11/2026): killing Monchug "barely makes waves
// in the world, and only few know of it... At least the people of the realm should know if an important person of theirs
// is dead." If this is wrong, a khan's fall is forgotten as fast as a minor lord's, the news hides that he was a ruler, or
// the player who struck him is never named.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class FallenHeroNewsTests
    {
        // STAKE: 3chmidt's Monchug. A ruler's fall outweighs a lord's, so other realms hear it and his own keeps it longer.
        [Test]
        public void GIVEN_a_ruler_and_a_lord_fall_in_battle_WHEN_weighed_THEN_the_ruler_is_far_louder()
        {
            int ruler = FallenHeroNews.Magnitude(new FallenHeroFacts {RuledRealm = "the Khuzait Khanate"});
            int lord = FallenHeroNews.Magnitude(new FallenHeroFacts());

            lord.Should().Be(LongTermHistoryPolicy.FellInBattleMagnitude);
            ruler.Should().BeGreaterThan(lord + RumorAwareness.FactionBonus);
        }

        // STAKE: even with no tie of realm to the hearer, a ruler's fall clears the rumour bar on the day it happens.
        [Test]
        public void GIVEN_a_ruler_falls_WHEN_a_stranger_realm_weighs_it_THEN_it_is_heard_without_any_tie()
        {
            FallenHeroNews.Magnitude(new FallenHeroFacts {RuledRealm = "Vlandia"}).Should().BeGreaterThanOrEqualTo(RumorAwareness.Threshold);
        }

        // STAKE: the head of a house sits between a lord and a ruler, in the field and on the block alike.
        [Test]
        public void GIVEN_a_clan_head_WHEN_weighed_THEN_he_is_between_a_lord_and_a_ruler()
        {
            int head = FallenHeroNews.Magnitude(new FallenHeroFacts {LedClan = "the Arkits"});
            head.Should().BeGreaterThan(LongTermHistoryPolicy.FellInBattleMagnitude);
            head.Should().BeLessThan(FallenHeroNews.Magnitude(new FallenHeroFacts {RuledRealm = "Vlandia"}));
            FallenHeroNews.Magnitude(new FallenHeroFacts {LedClan = "the Arkits", Executed = true}).Should().BeGreaterThan(FallenHeroNews.LordExecutedMagnitude);
        }

        // STAKE: the news says who he was and that the player's host struck him, so the world knows whose deed it was.
        [Test]
        public void GIVEN_a_ruler_falls_to_the_players_host_WHEN_told_THEN_rank_slayer_and_place_are_named()
        {
            FallenHeroNews.Summary(new FallenHeroFacts {Name = "Monchug of the Arkits", RuledRealm = "the Khuzait Khanate", FellToPlayer = "Arwa", Near = "Makeb"})
                          .Should().Be("Monchug of the Arkits, ruler of the Khuzait Khanate, fell in battle against Arwa's host near Makeb.");
        }

        // STAKE: a plain lord's death reads as it always did, so the old news and the long-term memory keep their words.
        [Test]
        public void GIVEN_a_plain_lord_falls_with_no_known_slayer_WHEN_told_THEN_the_old_words_stand()
        {
            FallenHeroNews.Summary(new FallenHeroFacts {Name = "Lord Ulric", Near = "Pravend"}).Should().Be("Lord Ulric fell in battle near Pravend.");
            FallenHeroNews.Summary(new FallenHeroFacts {Name = "Lord Ulric"}).Should().Be("Lord Ulric fell in battle.");
        }

        // STAKE: an execution names the judge and the rank of the one put to death.
        [Test]
        public void GIVEN_a_clan_head_executed_WHEN_told_THEN_judge_and_rank_are_named()
        {
            FallenHeroNews.Summary(new FallenHeroFacts {Name = "Lord Ulric", LedClan = "dey Meroc", Executed = true, ExecutedBy = "Arwa"})
                          .Should().Be("Arwa had Lord Ulric, head of the dey Meroc clan, executed.");
            FallenHeroNews.Summary(new FallenHeroFacts {Name = "Lord Ulric", Executed = true}).Should().Be("Lord Ulric was executed.");
        }
    }
}
