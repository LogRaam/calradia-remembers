// Code written by Gabriel Mailhot, 24/06/2026.
// Protects the conception policy: who (if anyone) becomes the mother of a vaginal act. The "same-sex /
// no fertile female → no child" rule and the fertile window are the whole guarantee — a drift would
// silently conceive impossible children or deny valid ones.
//
// WHY IT MATTERS: the resolved role feeds two places, both load-bearing. BannerlordGameStateBridge's
// impregnation_risk action calls PregnancyService.TryConceive(mother, father) directly off this result, and
// EncounterContextBuilder maps it to ConceptionRisk to decide whether the prompt even TEACHES the action to the
// LLM this scene. A wrong mother/father split corrupts whose clan the child is born into; a wrong None/risk
// split either teaches an impossible pregnancy or silently withholds a legitimate one.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ConceptionResolverTests
    {
        private const double Fertile = 25d;

        // ── The two productive pairings ────────────────────────────────────────

        // The baseline the whole resolver exists for: TryConceive is called as (mother, father), so getting
        // the direction right here is what puts the pregnancy, and the resulting child, in the correct clan.
        [Test]
        public void GIVEN_a_fertile_female_player_and_a_living_male_npc_WHEN_resolving_THEN_the_player_is_the_mother()
        {
            ConceptionResolver.Resolve(new ConceptionResolver.ConceptionFacts {
                    Player = new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = Fertile},
                    Npc = new ConceptionResolver.Participant {IsFemale = false, IsAlive = true, Age = 30d}
                })
                .Should().Be(ConceptionRole.PlayerIsMother);
        }

        // The mirror of the test above: the resolver must not silently favor the player as mother by default.
        // Whichever party is actually the fertile female becomes the mother, regardless of which side (player
        // or NPC) they are.
        [Test]
        public void GIVEN_a_male_player_and_a_fertile_female_npc_WHEN_resolving_THEN_the_npc_is_the_mother()
        {
            ConceptionResolver.Resolve(new ConceptionResolver.ConceptionFacts {
                    Player = new ConceptionResolver.Participant {IsFemale = false, IsAlive = true, Age = 30d},
                    Npc = new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = Fertile}
                })
                .Should().Be(ConceptionRole.NpcIsMother);
        }

        // ── Same-sex pairings produce no child ─────────────────────────────────

        // Same-sex romance is a real, supported path in this mod (via orientation overrides), but a same-sex
        // encounter must never be flagged as a conception risk: there is no father to call TryConceive with,
        // and EncounterContextBuilder must not teach an impossible pregnancy action into the prompt.
        [Test]
        public void GIVEN_two_females_WHEN_resolving_THEN_no_conception_for_lack_of_a_father()
        {
            ConceptionResolver.Resolve(new ConceptionResolver.ConceptionFacts {
                    Player = new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = Fertile},
                    Npc = new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = Fertile}
                })
                .Should().Be(ConceptionRole.None);
        }

        // The mirror case: two males have no fertile-female mother between them, so this must resolve to None
        // just as reliably as the two-females case above, not merely by coincidence of the first branch failing.
        [Test]
        public void GIVEN_two_males_WHEN_resolving_THEN_no_conception_for_lack_of_a_mother()
        {
            ConceptionResolver.Resolve(new ConceptionResolver.ConceptionFacts {
                    Player = new ConceptionResolver.Participant {IsFemale = false, IsAlive = true, Age = 30d},
                    Npc = new ConceptionResolver.Participant {IsFemale = false, IsAlive = true, Age = 30d}
                })
                .Should().Be(ConceptionRole.None);
        }

        // ── Fertility window on the mother ─────────────────────────────────────

        // Below MinFertileAge (18) the resolver must refuse conception even though the pairing is otherwise a
        // valid opposite-sex, both-alive match: age is checked independently of the sex/life gates, not folded
        // into them.
        [Test]
        public void GIVEN_a_female_player_below_the_fertile_window_WHEN_resolving_THEN_no_conception()
        {
            ConceptionResolver.Resolve(new ConceptionResolver.ConceptionFacts {
                    Player = new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = 17d},
                    Npc = new ConceptionResolver.Participant {IsFemale = false, IsAlive = true, Age = 30d}
                })
                .Should().Be(ConceptionRole.None);
        }

        // The upper edge of the same window (MaxFertileAge = 45): an older NPC must not be flagged fertile just
        // because she's otherwise a plausible partner, keeping the pregnancy pillar's outcomes believable.
        [Test]
        public void GIVEN_a_female_npc_above_the_fertile_window_WHEN_resolving_THEN_no_conception()
        {
            ConceptionResolver.Resolve(new ConceptionResolver.ConceptionFacts {
                    Player = new ConceptionResolver.Participant {IsFemale = false, IsAlive = true, Age = 30d},
                    Npc = new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = 46d}
                })
                .Should().Be(ConceptionRole.None);
        }

        // Pins both ends of the window as inclusive and exercises IsFertileMother directly, so a future retune
        // of MinFertileAge/MaxFertileAge (or an off-by-one in the comparison) can't silently narrow or widen
        // who the resolver treats as fertile without a test failing.
        [Test]
        public void GIVEN_the_fertile_window_bounds_WHEN_checking_the_mother_THEN_eighteen_and_fortyfive_are_inclusive()
        {
            ConceptionResolver.IsFertileMother(new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = 18d}).Should().BeTrue();
            ConceptionResolver.IsFertileMother(new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = 45d}).Should().BeTrue();
            ConceptionResolver.IsFertileMother(new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = 17.9d}).Should().BeFalse();
            ConceptionResolver.IsFertileMother(new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = 45.1d}).Should().BeFalse();
        }

        // ── The dead conceive nothing ──────────────────────────────────────────

        // A dead participant must never be flagged as a fertile mother regardless of her age falling in
        // window; a scene where the woman has died must not go on to conceive a child.
        [Test]
        public void GIVEN_a_dead_fertile_female_WHEN_resolving_THEN_no_conception()
        {
            ConceptionResolver.Resolve(new ConceptionResolver.ConceptionFacts {
                    Player = new ConceptionResolver.Participant {IsFemale = true, IsAlive = false, Age = Fertile},
                    Npc = new ConceptionResolver.Participant {IsFemale = false, IsAlive = true, Age = 30d}
                })
                .Should().Be(ConceptionRole.None);
        }

        // IsViableFather requires the father to be alive too: a fertile mother alone is not enough. Without this
        // check a dead player could still be resolved as the father of a new conception.
        [Test]
        public void GIVEN_a_fertile_female_npc_and_a_dead_male_player_WHEN_resolving_THEN_no_conception_for_lack_of_a_living_father()
        {
            ConceptionResolver.Resolve(new ConceptionResolver.ConceptionFacts {
                    Player = new ConceptionResolver.Participant {IsFemale = false, IsAlive = false, Age = 30d},
                    Npc = new ConceptionResolver.Participant {IsFemale = true, IsAlive = true, Age = Fertile}
                })
                .Should().Be(ConceptionRole.None);
        }
    }
}
