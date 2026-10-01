// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026): "He hires you as envoy to negotiate the release of one of his guys... Here is 5000
// denars. Bring my lad alive. You can keep the change." Gabriel's ruling (option A): the purse is advanced; if the man
// goes free some other way first, nobody is at fault and the advance goes back. If these rules are wrong, a lord sends
// an envoy where the player is at war (that is a rescue), the player keeps a purse for a task that vanished under him,
// a ransom someone else paid completes the player's errand, or the refund takes more than the player has.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RansomEnvoyPolicyTests
    {
        private static RansomEnvoyFacts Sendable() => new RansomEnvoyFacts {IsPrisoner = true, IsTheGiversOwn = true, GiverCanPay = true};

        // ── When a lord sends an envoy ────────────────────────────────────────────────────────────

        // "He is at war with faction B. You are not": the case the errand exists for.
        [Test]
        public void GIVEN_his_own_man_held_by_a_realm_the_player_is_at_peace_with_WHEN_asked_THEN_he_may_send_the_player()
        {
            RansomEnvoyPolicy.MaySend(Sendable()).Should().BeTrue();
        }

        // At war with the captor, the player can take the man by force: that is rescue_prisoner, not an envoy.
        [Test]
        public void GIVEN_the_player_at_war_with_the_captor_WHEN_asked_THEN_it_is_a_rescue_not_an_envoys_errand()
        {
            RansomEnvoyFacts f = Sendable();
            f.PlayerAtWarWithTheCaptor = true;

            RansomEnvoyPolicy.MaySend(f).Should().BeFalse();
        }

        // Not his man, not a prisoner, already in the player's hands, or no purse to advance: no errand.
        [Test]
        public void GIVEN_a_stranger_a_free_man_one_the_player_holds_or_an_empty_purse_WHEN_asked_THEN_no_envoy_is_sent()
        {
            RansomEnvoyFacts stranger = Sendable(); stranger.IsTheGiversOwn = false;
            RansomEnvoyFacts free = Sendable(); free.IsPrisoner = false;
            RansomEnvoyFacts held = Sendable(); held.HeldByThePlayer = true;
            RansomEnvoyFacts poor = Sendable(); poor.GiverCanPay = false;

            RansomEnvoyPolicy.MaySend(stranger).Should().BeFalse();
            RansomEnvoyPolicy.MaySend(free).Should().BeFalse();
            RansomEnvoyPolicy.MaySend(held).Should().BeFalse();
            RansomEnvoyPolicy.MaySend(poor).Should().BeFalse();
        }

        // "You can keep the change": half again the man's ransom value, so a fair bargain leaves some.
        [Test]
        public void GIVEN_the_mans_ransom_value_WHEN_the_purse_is_set_THEN_it_is_half_again_as_much()
        {
            RansomEnvoyPolicy.Advance(1000).Should().Be(1500);
            RansomEnvoyPolicy.Advance(0).Should().Be(0);
        }

        // ── Whose deed the freeing is ─────────────────────────────────────────────────────────────

        // Bought from his captor, then let go or handed home: the player's deed. Broken out of a prison: also his.
        [Test]
        public void GIVEN_the_player_freed_a_man_he_held_or_broke_him_out_WHEN_judged_THEN_it_is_the_players_deed()
        {
            RansomEnvoyPolicy.IsThePlayersDeed(new EnvoyReleaseFacts {Reason = CaptivityEndReason.ByChoice, HeldByThePlayer = true}).Should().BeTrue();
            RansomEnvoyPolicy.IsThePlayersDeed(new EnvoyReleaseFacts {Reason = CaptivityEndReason.Escape, BrokenOutByThePlayer = true}).Should().BeTrue();
        }

        // "Prisoners tend to escape or be released": a ransom paid by his family, a peace, his own escape, his captor
        // letting him go. None is the player's deed, so none completes the errand (the task is voided instead).
        [Test]
        public void GIVEN_a_freeing_the_player_had_no_hand_in_WHEN_judged_THEN_it_is_not_his_deed()
        {
            RansomEnvoyPolicy.IsThePlayersDeed(new EnvoyReleaseFacts {Reason = CaptivityEndReason.Ransom}).Should().BeFalse();
            RansomEnvoyPolicy.IsThePlayersDeed(new EnvoyReleaseFacts {Reason = CaptivityEndReason.Peace}).Should().BeFalse();
            RansomEnvoyPolicy.IsThePlayersDeed(new EnvoyReleaseFacts {Reason = CaptivityEndReason.Escape}).Should().BeFalse();
            RansomEnvoyPolicy.IsThePlayersDeed(new EnvoyReleaseFacts {Reason = CaptivityEndReason.ByChoice, HeldByThePlayer = false}).Should().BeFalse();
        }

        // ── The safety wall ───────────────────────────────────────────────────────────────────────

        // The man went free without the player: the advance goes back, never more than the player's purse holds.
        [Test]
        public void GIVEN_the_task_voided_through_no_fault_WHEN_the_advance_is_returned_THEN_it_is_the_advance_up_to_the_purse()
        {
            RansomEnvoyPolicy.RefundOnVoid(advance: 1500, playerGold: 9000).Should().Be(1500);
            RansomEnvoyPolicy.RefundOnVoid(advance: 1500, playerGold: 400).Should().Be(400);
            RansomEnvoyPolicy.RefundOnVoid(advance: 1500, playerGold: 0).Should().Be(0);
        }
    }
}
