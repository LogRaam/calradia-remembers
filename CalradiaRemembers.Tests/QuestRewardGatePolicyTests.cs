// Code written by Gabriel Mailhot, 04/08/2026.
// COUNCIL_ACTIONS.md Partie 5 (the "Caladog" case): a king promised the player a fief and marriage to himself
// as a quest reward, and both evaporated on completion because no executor existed. QuestRewardGatePolicy is
// the pure gate behind the fix: SAFETY-CRITICAL, since with ModSettings.AllowFiefAndMarriageQuestRewards left
// at its default (false), NOTHING about today's behavior may change. Every test group below pins one of the
// three moments the policy guards: emission (never promise what cannot be paid), the toggle-off floor (the
// feature is fully inert unless explicitly opted into), and payout re-validation (the world may have moved
// between the promise and the completion, so nothing is ever delivered on stale facts).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestRewardGatePolicyTests
    {
        // ── Toggle-off floor: the single most important guarantee of this whole feature ──

        // The MCM opt-in default is false, and this is the ONE fact the whole feature's safety rests on: even
        // with every other condition perfect (the giver truly leads their own clan and truly owns the named
        // fief), a toggle left at its default must still refuse. Anything else would mean today's players see
        // new behavior they never asked for.
        [Test]
        public void GIVEN_the_toggle_is_off_WHEN_a_fief_promise_would_otherwise_qualify_THEN_it_is_still_refused()
        {
            QuestRewardGatePolicy.CanPromiseFief(toggleOn: false, giverLeadsOwnClan: true, fiefOwnedByGiverClan: true)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_toggle_is_off_WHEN_a_marriage_reward_would_otherwise_qualify_THEN_it_is_still_refused()
        {
            QuestRewardGatePolicy.CanPromiseMarriageReward(toggleOn: false, spouseResolved: true,
                    giverHasAuthorityOverSpouse: true, spouseMarriageable: true, playerIsReigningSovereign: false)
                .Should().BeFalse();
        }

        // Defense in depth: a promise made while the toggle was on, paid out after Gabriel (or a player)
        // flipped it back off, must still refuse rather than honor a promise the current settings forbid.
        [Test]
        public void GIVEN_the_toggle_is_off_WHEN_a_fief_payout_would_otherwise_succeed_THEN_it_is_refused()
        {
            QuestRewardGatePolicy.DecideFiefPayout(toggleOn: false, giverAlive: true, giverLeadsOwnClan: true,
                    fiefStillOwnedByGiverClan: true, playerAtWarWithGiverClan: false)
                .Should().Be(QuestRewardPayoutVerdict.Refuse);
        }

        [Test]
        public void GIVEN_the_toggle_is_off_WHEN_a_marriage_reward_payout_would_otherwise_succeed_THEN_it_is_refused()
        {
            QuestRewardGatePolicy.DecideMarriageRewardPayout(toggleOn: false, giverAlive: true,
                    giverHasAuthorityOverSpouse: true, spouseStillMarriageable: true, playerAtWarWithGiverClan: false,
                    playerIsReigningSovereign: false)
                .Should().Be(QuestRewardPayoutVerdict.Refuse);
        }

        // ── Emission (the golden rule: refuse at issuance rather than promise a broken reward) ──

        [Test]
        public void GIVEN_the_giver_truly_leads_their_own_clan_and_owns_the_fief_WHEN_the_toggle_is_on_THEN_the_promise_is_allowed()
        {
            QuestRewardGatePolicy.CanPromiseFief(toggleOn: true, giverLeadsOwnClan: true, fiefOwnedByGiverClan: true)
                .Should().BeTrue();
        }

        // The Kimi review's own condition: a vassal cannot give away a fief that is not truly theirs to give
        // (the council's grant_fief authority rule, mirrored here in the opposite direction).
        [Test]
        public void GIVEN_the_giver_does_not_lead_their_own_clan_WHEN_promising_a_fief_THEN_it_is_refused()
        {
            QuestRewardGatePolicy.CanPromiseFief(toggleOn: true, giverLeadsOwnClan: false, fiefOwnedByGiverClan: true)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_named_fief_is_not_owned_by_the_givers_clan_WHEN_promising_it_THEN_it_is_refused()
        {
            QuestRewardGatePolicy.CanPromiseFief(toggleOn: true, giverLeadsOwnClan: true, fiefOwnedByGiverClan: false)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_every_marriage_condition_holds_WHEN_the_toggle_is_on_THEN_the_promise_is_allowed()
        {
            QuestRewardGatePolicy.CanPromiseMarriageReward(toggleOn: true, spouseResolved: true,
                    giverHasAuthorityOverSpouse: true, spouseMarriageable: true, playerIsReigningSovereign: false)
                .Should().BeTrue();
        }

        // COUNCIL_ACTIONS.md Kimi review "Trou 3": a reigning sovereign's own marriage can move their crown as
        // a side effect of MarriageAction. Never risked as a mere quest-reward side effect, so a player who
        // already reigns can never be promised a marriage_reward at all, whoever the spouse would be.
        [Test]
        public void GIVEN_the_player_already_reigns_over_a_realm_WHEN_promising_a_marriage_reward_THEN_it_is_refused()
        {
            QuestRewardGatePolicy.CanPromiseMarriageReward(toggleOn: true, spouseResolved: true,
                    giverHasAuthorityOverSpouse: true, spouseMarriageable: true, playerIsReigningSovereign: true)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_giver_has_no_authority_over_the_named_spouse_WHEN_promising_a_marriage_reward_THEN_it_is_refused()
        {
            QuestRewardGatePolicy.CanPromiseMarriageReward(toggleOn: true, spouseResolved: true,
                    giverHasAuthorityOverSpouse: false, spouseMarriageable: true, playerIsReigningSovereign: false)
                .Should().BeFalse();
        }

        // ── Payout re-validation (M-B4 pattern: the world may have moved since the promise) ──

        [Test]
        public void GIVEN_every_fief_precondition_still_holds_WHEN_deciding_the_payout_THEN_it_delivers()
        {
            QuestRewardGatePolicy.DecideFiefPayout(toggleOn: true, giverAlive: true, giverLeadsOwnClan: true,
                    fiefStillOwnedByGiverClan: true, playerAtWarWithGiverClan: false)
                .Should().Be(QuestRewardPayoutVerdict.Deliver);
        }

        // The Caladog case itself: Ortysia (the promised fief) fell to another clan between the promise and
        // the completion. The reward must be refused honestly, never faked, per COUNCIL_ACTIONS.md's own
        // reference test ("if Ortysia has been lost between the promise and the completion, the game must SAY
        // so").
        [Test]
        public void GIVEN_the_fief_no_longer_belongs_to_the_givers_clan_WHEN_deciding_the_payout_THEN_it_refuses()
        {
            QuestRewardGatePolicy.DecideFiefPayout(toggleOn: true, giverAlive: true, giverLeadsOwnClan: true,
                    fiefStillOwnedByGiverClan: false, playerAtWarWithGiverClan: false)
                .Should().Be(QuestRewardPayoutVerdict.Refuse);
        }

        [Test]
        public void GIVEN_the_giver_died_before_completion_WHEN_deciding_the_fief_payout_THEN_it_refuses()
        {
            QuestRewardGatePolicy.DecideFiefPayout(toggleOn: true, giverAlive: false, giverLeadsOwnClan: true,
                    fiefStillOwnedByGiverClan: true, playerAtWarWithGiverClan: false)
                .Should().Be(QuestRewardPayoutVerdict.Refuse);
        }

        // Delivering a fief to a wartime enemy is absurd, and vanilla's own transfer action carries no such
        // guard on its own: a war declared between the promise and the completion must suspend the delivery.
        [Test]
        public void GIVEN_the_player_is_now_at_war_with_the_givers_clan_WHEN_deciding_the_fief_payout_THEN_it_refuses()
        {
            QuestRewardGatePolicy.DecideFiefPayout(toggleOn: true, giverAlive: true, giverLeadsOwnClan: true,
                    fiefStillOwnedByGiverClan: true, playerAtWarWithGiverClan: true)
                .Should().Be(QuestRewardPayoutVerdict.Refuse);
        }

        [Test]
        public void GIVEN_every_marriage_precondition_still_holds_WHEN_deciding_the_payout_THEN_it_delivers()
        {
            QuestRewardGatePolicy.DecideMarriageRewardPayout(toggleOn: true, giverAlive: true,
                    giverHasAuthorityOverSpouse: true, spouseStillMarriageable: true, playerAtWarWithGiverClan: false,
                    playerIsReigningSovereign: false)
                .Should().Be(QuestRewardPayoutVerdict.Deliver);
        }

        // Kimi review "Trou 2"/"Trou 4": the spouse wed another (or died) between the promise and the
        // completion. The blessing-only marriage_consent path already re-validates this exact fact
        // (MarriageProspectResolver.IsMarriageable at payout); marriage_reward must hold to the same standard.
        [Test]
        public void GIVEN_the_promised_spouse_is_no_longer_marriageable_WHEN_deciding_the_payout_THEN_it_refuses()
        {
            QuestRewardGatePolicy.DecideMarriageRewardPayout(toggleOn: true, giverAlive: true,
                    giverHasAuthorityOverSpouse: true, spouseStillMarriageable: false, playerAtWarWithGiverClan: false,
                    playerIsReigningSovereign: false)
                .Should().Be(QuestRewardPayoutVerdict.Refuse);
        }

        // The sovereignty guard is re-tested at PAYOUT too, not only at issuance: the player may have become a
        // sovereign (won a kingdom) in the weeks between the promise and the completion.
        [Test]
        public void GIVEN_the_player_became_a_reigning_sovereign_since_the_promise_WHEN_deciding_the_marriage_payout_THEN_it_refuses()
        {
            QuestRewardGatePolicy.DecideMarriageRewardPayout(toggleOn: true, giverAlive: true,
                    giverHasAuthorityOverSpouse: true, spouseStillMarriageable: true, playerAtWarWithGiverClan: false,
                    playerIsReigningSovereign: true)
                .Should().Be(QuestRewardPayoutVerdict.Refuse);
        }

        // ── Deadline clamp: "Deadline OBLIGATOIRE... une promesse de fief sans echeance est une bombe a
        //    retardement de save" (Kimi review). A heavy reward must always terminate. ──

        [Test]
        public void GIVEN_the_llm_proposed_no_deadline_WHEN_clamping_a_heavy_reward_THEN_the_maximum_is_used_instead_of_open_ended()
        {
            QuestRewardGatePolicy.ClampHeavyRewardDeadlineDays(null)
                .Should().Be(QuestRewardGatePolicy.MaxHeavyRewardDeadlineDays);
        }

        [Test]
        public void GIVEN_the_llm_proposed_a_deadline_beyond_the_cap_WHEN_clamping_THEN_it_is_pulled_down_to_the_maximum()
        {
            QuestRewardGatePolicy.ClampHeavyRewardDeadlineDays(365)
                .Should().Be(QuestRewardGatePolicy.MaxHeavyRewardDeadlineDays);
        }

        [Test]
        public void GIVEN_the_llm_proposed_a_sane_deadline_WHEN_clamping_THEN_its_own_value_is_kept()
        {
            QuestRewardGatePolicy.ClampHeavyRewardDeadlineDays(21)
                .Should().Be(21);
        }
    }
}
