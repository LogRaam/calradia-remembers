// Code written by Gabriel Mailhot, 25/11/2026.
// What this pins: when a lord can raise one army with the player to attack an enemy, who commands it, which targets the
// model may name, and when it is over.
//
// froggyluv (Nexus, 25/11/2026): a lord planned at length how they would attack an enemy party together, "Once the
// conversation ended, she took off the other direction." Gabriel (25/11/2026): the lord who agrees raises an army of the
// two parties, the most powerful leads it, and it attacks the right target (party, town, village, castle, ship). If these
// fail, a lord promises an attack the game cannot carry out, the wrong one commands, or the army marches on a target
// the model invented.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Escort;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class JointAttackPolicyTests
    {
        private static JointAttackFacts Ready() => new()
        {
            NpcLeadsOwnParty = true, SameRealm = true, AnyTarget = true,
            Player = new JointAttackSide {ClanTier = 3}, Npc = new JointAttackSide {ClanTier = 3}
        };

        // STAKE: froggyluv's lady, same realm, her own party, an enemy in reach: the army can be raised.
        [Test]
        public void GIVEN_a_lord_of_the_players_realm_with_a_party_and_an_enemy_in_reach_WHEN_asked_THEN_the_army_can_be_raised()
        {
            JointAttackPolicy.Refusal(Ready()).Should().Be(JointAttackRefusal.None);
        }

        // STAKE: the game's armies belong to one kingdom; a lord of another realm promising one would promise nothing.
        [Test]
        public void GIVEN_a_lord_of_another_realm_WHEN_asked_THEN_it_is_refused_for_the_realm()
        {
            JointAttackFacts f = Ready();

            JointAttackPolicy.Refusal(new JointAttackFacts {NpcLeadsOwnParty = true, SameRealm = false, AnyTarget = true, Player = f.Player, Npc = f.Npc})
                .Should().Be(JointAttackRefusal.NotTheSameRealm);
        }

        // STAKE: a lord already in an army, a siege or a battle would be reclaimed by the game the same hour.
        [Test]
        public void GIVEN_a_lord_already_in_an_army_WHEN_asked_THEN_it_is_refused()
        {
            JointAttackFacts f = Ready();

            JointAttackPolicy.Refusal(new JointAttackFacts {NpcLeadsOwnParty = true, SameRealm = true, AnyTarget = true, NpcCommittedElsewhere = true, Player = f.Player, Npc = f.Npc})
                .Should().Be(JointAttackRefusal.CommittedElsewhere);
        }

        // STAKE: with no enemy in reach, the model would have to invent the target.
        [Test]
        public void GIVEN_no_enemy_in_reach_WHEN_asked_THEN_it_is_refused_for_the_target()
        {
            JointAttackFacts f = Ready();

            JointAttackPolicy.Refusal(new JointAttackFacts {NpcLeadsOwnParty = true, SameRealm = true, AnyTarget = false, Player = f.Player, Npc = f.Npc})
                .Should().Be(JointAttackRefusal.NoTarget);
        }

        // ── Who leads: Gabriel, "C'est le Lord le plus puissant qui dirige l'armee" ──

        // STAKE: the realm's ruler marching under a lesser lord's banner.
        [Test]
        public void GIVEN_the_npc_rules_the_realm_WHEN_choosing_the_leader_THEN_the_ruler_leads_whatever_the_tiers()
        {
            JointAttackPolicy.Leader(new JointAttackSide {ClanTier = 6, PartyStrength = 900}, new JointAttackSide {RulesTheRealm = true, ClanTier = 2})
                .Should().Be(JointAttackLeader.Npc);
        }

        // STAKE: a higher house following a lesser one.
        [Test]
        public void GIVEN_the_player_has_the_higher_clan_tier_WHEN_choosing_the_leader_THEN_the_player_leads()
        {
            JointAttackPolicy.Leader(new JointAttackSide {ClanTier = 5, PartyStrength = 100}, new JointAttackSide {ClanTier = 3, PartyStrength = 400})
                .Should().Be(JointAttackLeader.Player);
        }

        // STAKE: between equal houses, the stronger party commands.
        [Test]
        public void GIVEN_equal_tiers_WHEN_choosing_the_leader_THEN_the_stronger_party_leads()
        {
            JointAttackPolicy.Leader(new JointAttackSide {ClanTier = 3, PartyStrength = 500}, new JointAttackSide {ClanTier = 3, PartyStrength = 200})
                .Should().Be(JointAttackLeader.Player);
            JointAttackPolicy.Leader(new JointAttackSide {ClanTier = 3, PartyStrength = 200}, new JointAttackSide {ClanTier = 3, PartyStrength = 500})
                .Should().Be(JointAttackLeader.Npc);
        }

        // STAKE: the engine forbids a mercenary to lead an army ("Mercenaries cannot create or manage armies.").
        [Test]
        public void GIVEN_the_player_is_a_mercenary_WHEN_choosing_the_leader_THEN_the_lord_leads_even_from_a_lower_tier()
        {
            JointAttackPolicy.Leader(new JointAttackSide {IsMercenary = true, ClanTier = 6}, new JointAttackSide {ClanTier = 1})
                .Should().Be(JointAttackLeader.Npc);
        }

        // STAKE: two mercenaries could raise an army the engine says neither may lead.
        [Test]
        public void GIVEN_both_are_mercenaries_WHEN_asked_THEN_no_one_may_lead_and_it_is_refused()
        {
            JointAttackPolicy.Refusal(new JointAttackFacts {NpcLeadsOwnParty = true, SameRealm = true, AnyTarget = true,
                                                            Player = new JointAttackSide {IsMercenary = true}, Npc = new JointAttackSide {IsMercenary = true}})
                .Should().Be(JointAttackRefusal.NoOneMayLead);
        }

        // ── Targets ──

        // STAKE: a ship at sea named as the target when the two cannot sail: the army could never reach it.
        [Test]
        public void GIVEN_a_party_at_sea_WHEN_the_two_cannot_both_sail_THEN_it_is_not_offered()
        {
            IReadOnlyList<JointAttackCandidate> listed = JointAttackPolicy.Candidates(new[]
            {
                new JointAttackCandidate {Id = "sea_raiders", Kind = JointAttackTargetKind.Party, AtSea = true, BothCanSail = false, Distance = 1},
                new JointAttackCandidate {Id = "looters", Kind = JointAttackTargetKind.Party, Distance = 5}
            });

            listed.Should().ContainSingle().Which.Id.Should().Be("looters");
        }

        // STAKE: froggyluv's enemy "directly North" is the nearest one; a long list buries it.
        [Test]
        public void GIVEN_many_targets_WHEN_listing_THEN_the_nearest_few_come_first()
        {
            var all = new List<JointAttackCandidate>();
            for (int i = 0; i < 10; i++) all.Add(new JointAttackCandidate {Id = "t" + i, Distance = 10 - i});

            IReadOnlyList<JointAttackCandidate> listed = JointAttackPolicy.Candidates(all);

            listed.Should().HaveCount(JointAttackPolicy.MaxCandidates);
            listed[0].Id.Should().Be("t9");
        }

        // STAKE: an enemy across the map listed as "within reach" sends the army on a march nobody planned.
        [Test]
        public void GIVEN_a_target_beyond_reach_WHEN_listing_THEN_it_is_not_offered()
        {
            JointAttackPolicy.Candidates(new[] {new JointAttackCandidate {Id = "far", Distance = JointAttackPolicy.ReachMapUnits + 1}}).Should().BeEmpty();
        }

        // STAKE: the model names a target by its id or its name; anything else must not march an army.
        [Test]
        public void GIVEN_a_named_target_WHEN_resolving_THEN_only_a_listed_one_is_found()
        {
            var listed = new[] {new JointAttackCandidate {Id = "castle_V3", Name = "Rhotae Castle"}};

            JointAttackPolicy.Resolve(listed, "castle_V3").Should().NotBeNull();
            JointAttackPolicy.Resolve(listed, "rhotae castle").Should().NotBeNull();
            JointAttackPolicy.Resolve(listed, "Pravend").Should().BeNull();
        }

        // STAKE: in Enhanced mode the interpreter may name no target; the nearest enemy is the one the plan was about,
        // and the confirmation names it so the player can refuse a wrong one.
        [Test]
        public void GIVEN_no_target_named_WHEN_resolving_THEN_the_nearest_listed_enemy_is_taken()
        {
            var listed = JointAttackPolicy.Candidates(new[]
            {
                new JointAttackCandidate {Id = "far", Distance = 30},
                new JointAttackCandidate {Id = "near", Distance = 3}
            });

            JointAttackPolicy.ResolveOrNearest(listed, null).Id.Should().Be("near");
            JointAttackPolicy.ResolveOrNearest(listed, "far").Id.Should().Be("far");
        }

        // STAKE: the player gives up command of their party on a yes; the question must say who commands and against what.
        [Test]
        public void GIVEN_who_leads_WHEN_asking_the_player_THEN_the_question_names_the_commander_and_the_target()
        {
            JointAttackPolicy.Question(JointAttackLeader.Npc, "Lady Ira", "Looters").Should().Contain("Lady Ira's army").And.Contain("Looters");
            JointAttackPolicy.Question(JointAttackLeader.Player, "Lady Ira", "Looters").Should().Contain("under your command");
        }

        // ── The end ──

        // STAKE: the lord stays locked in the army after the enemy is beaten, never returning to their business.
        [Test]
        public void GIVEN_the_target_is_done_WHEN_checking_THEN_the_army_ends()
        {
            JointAttackPolicy.End(new JointAttackProgressFacts {ArmyIntact = true, TargetDone = true, StillAtWar = true, BothStanding = true})
                .Should().Be(JointAttackEnd.TargetDone);
        }

        // STAKE: an army marching on a realm its own now holds peace with.
        [Test]
        public void GIVEN_peace_with_the_target_WHEN_checking_THEN_the_army_ends()
        {
            JointAttackPolicy.End(new JointAttackProgressFacts {ArmyIntact = true, StillAtWar = false, BothStanding = true})
                .Should().Be(JointAttackEnd.Peace);
        }

        // STAKE: a promise with no end: the same term as an escort.
        [Test]
        public void GIVEN_the_term_has_run_WHEN_checking_THEN_the_army_ends_and_before_it_does_not()
        {
            JointAttackPolicy.End(new JointAttackProgressFacts {ArmyIntact = true, StillAtWar = true, BothStanding = true, DaysSinceRaised = JointAttackPolicy.TermDays})
                .Should().Be(JointAttackEnd.TermOver);
            JointAttackPolicy.End(new JointAttackProgressFacts {ArmyIntact = true, StillAtWar = true, BothStanding = true, DaysSinceRaised = JointAttackPolicy.TermDays - 1})
                .Should().Be(JointAttackEnd.None);
        }
    }
}
