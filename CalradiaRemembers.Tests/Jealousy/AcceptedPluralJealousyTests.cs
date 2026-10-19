// Code written by Gabriel Mailhot, 22/09/2026.
// What these pin: what it costs a partner to have SAID she accepts the player's other wives, when her own
// people's custom says it is betrayal.
//
// yozakura12, Nexus, 22/09/2026: "I am playing with multiple spouses, and the main issue is that jealousy
// penalties are much stronger than normal relationship gains. Corein is the clearest example. She says she
// accepts the harem as long as she remains the first/senior wife, but jealousy can still trigger repeatedly and
// eventually push her toward divorce... I do not want jealousy removed. Corein being possessive, angry,
// competitive, and demanding to remain 'number one' fits her character very well. I would just like the
// numerical penalty to be less destructive."
//
// He is right, and the reason is sharper than the numbers. The gentle track already exists: a partner whose
// CULTURE tolerates shared partners takes 3/6/8 where an exclusive one takes 10/25/40, and a floor added on
// 12/09/2026 stops her sliding to hatred over a practice her own people keep. What decides which track she is
// on is her culture and nothing else. So a woman of an exclusive people can say, in her own voice, that she
// accepts the arrangement, and the ledger never hears it: she keeps taking 10/25/40, down to -100. In a mod
// whose whole premise is that what a character says is true, her word moves nothing.
//
// Gabriel's ruling, 22/09/2026: the culture sets the PRICE, not a veto. A Vlandian may accept, and she is
// somewhat more tolerant for having done so ("une femme qui accepte malgre que sa culture veut le contraire
// pourrait etre un peu plus tolerante"), but she is not a Khuzait: she has consented to bear what her own
// people call betrayal, and it must keep costing her something.

#region

using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Jealousy
{
   [TestFixture]
   public sealed class AcceptedPluralJealousyTests
   {
      // THE REPORTED CASE, in one assertion. Corein is of an exclusive people and has said she accepts the
      // harem. Every jealousy event still cost her the full betrayal price, which is what "push her toward
      // divorce" means in numbers: two marriages is -80 of regard, against the +1 a good conversation buys.
      [TestCase(RomanticActTier.Flirt, 10, 5)]
      [TestCase(RomanticActTier.Intimacy, 25, 13)]
      [TestCase(RomanticActTier.Marriage, 40, 20)]
      public void GIVEN_a_partner_who_accepted_against_her_culture_WHEN_the_player_takes_another_THEN_it_wounds_her_less_than_a_betrayal(
         RomanticActTier tier, int betrayal, int accepted)
      {
         JealousyReaction.Severity(JealousStake.PlayerPartner, tier, JealousyTrack.Betrayal, null)
                         .Should().Be(betrayal, "the exclusive track is unchanged for anyone who never agreed");

         JealousyReaction.Severity(JealousStake.PlayerPartner, tier, JealousyTrack.AcceptedAgainstCulture, null)
                         .Should().Be(accepted);
      }

      // AND SHE IS STILL NOT A KHUZAIT. Gabriel's line the design turns on: she consented to bear what her own
      // people call betrayal, so it costs her more than it costs a woman whose custom never called it anything.
      // Collapse the two and the cultures stop meaning anything, which is the objection he raised against the
      // first version of this: "une Vlandienne qui culturellement n'accepte pas le harem pourrait l'accepter
      // sans consequence?"
      [Test]
      public void GIVEN_the_three_tracks_WHEN_compared_at_the_same_act_THEN_accepting_against_your_culture_sits_between_them()
      {
         int betrayal = JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, JealousyTrack.Betrayal, null);
         int accepted = JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, JealousyTrack.AcceptedAgainstCulture, null);
         int competition = JealousyReaction.Severity(JealousStake.PlayerPartner, RomanticActTier.Marriage, JealousyTrack.Competition, null);

         accepted.Should().BeLessThan(betrayal).And.BeGreaterThan(competition);
      }

      // SHE CAN COME TO RESENT IT, AND STOP THERE. The competition floor is 0: a woman whose people share
      // partners may cool completely and never become an enemy over it. A woman bearing this against her own
      // custom is owed more room than that, and less than the -100 a real betrayal opens. Wary, not hostile,
      // and the rung is one that already has a name.
      [Test]
      public void GIVEN_years_of_it_WHEN_her_regard_has_fallen_to_wary_THEN_it_stops_costing_regard_and_costs_only_the_scene()
      {
         JealousyReactionPolicy.AcceptedAgainstCultureDelta(Regard.WaryTier, -20).Should().Be(0);
         JealousyReactionPolicy.AcceptedAgainstCultureDelta(Regard.WaryTier - 5, -20).Should().Be(0,
            "already past it from some other wound, this grievance does not push her further");
      }

      // THE LAST STEP LANDS EXACTLY ON THE RUNG, never past it. Without the clamp a single marriage could carry
      // her from just above wary to well below it, and the floor would only be a floor on the way back up.
      [Test]
      public void GIVEN_one_more_slight_from_just_above_the_floor_WHEN_it_lands_THEN_it_takes_her_to_the_floor_and_no_further()
      {
         JealousyReactionPolicy.AcceptedAgainstCultureDelta(Regard.WaryTier + 5, -20).Should().Be(-5);
      }

      // AND SHE IS NOT IMMUNE ON THE WAY DOWN. The softening is not a shrug: from an ordinary warm standing a
      // marriage still takes a real bite, which is what keeps yozakura12's own words true ("I do not want
      // jealousy removed").
      [Test]
      public void GIVEN_a_warm_partner_WHEN_the_player_marries_another_THEN_it_still_costs_her_the_full_softened_wound()
      {
         JealousyReactionPolicy.AcceptedAgainstCultureDelta(40, -20).Should().Be(-20);
      }

      // A GAIN IS NEVER TOUCHED. The floor answers a question about wounds; handing it a positive delta must
      // leave it alone, the same contract SharingRankDelta already keeps.
      [Test]
      public void GIVEN_a_positive_delta_WHEN_the_floor_is_asked_THEN_it_is_returned_untouched()
      {
         JealousyReactionPolicy.AcceptedAgainstCultureDelta(Regard.WaryTier - 40, 3).Should().Be(3);
      }

      // ── which track she is on ────────────────────────────────────────────────

      // A CULTURE THAT ALREADY SHARES needs no acceptance and gets the gentle track, exactly as before. Saying
      // yes adds nothing she did not already have, and must not accidentally move her onto the harsher band by
      // being treated as a promise she had to make.
      [Test]
      public void GIVEN_a_partner_whose_people_share_partners_WHEN_the_track_is_chosen_THEN_it_is_competition_whether_or_not_she_ever_agreed()
      {
         JealousyTrackPolicy.For(new JealousyTrackFacts {CultureToleratesSharing = true}).Should().Be(JealousyTrack.Competition);
         JealousyTrackPolicy.For(new JealousyTrackFacts {CultureToleratesSharing = true, PartnerAcceptedTheArrangement = true})
                            .Should().Be(JealousyTrack.Competition);
      }

      // THE NEW CASE. Her people call it betrayal; she said yes anyway.
      [Test]
      public void GIVEN_an_exclusive_culture_and_a_partner_who_said_yes_WHEN_the_track_is_chosen_THEN_it_is_the_softened_one()
         => JealousyTrackPolicy.For(new JealousyTrackFacts {PartnerAcceptedTheArrangement = true})
                               .Should().Be(JealousyTrack.AcceptedAgainstCulture);

      // AND SILENCE IS NOT CONSENT. A partner who never agreed is on the track her culture puts her on, which
      // is the whole of today's behaviour and must not move.
      [Test]
      public void GIVEN_an_exclusive_culture_and_a_partner_who_never_agreed_WHEN_the_track_is_chosen_THEN_it_is_betrayal()
         => JealousyTrackPolicy.For(new JealousyTrackFacts()).Should().Be(JealousyTrack.Betrayal);

      // THE BOUNDARY THAT KEEPS ACCEPTANCE FROM BECOMING A BLANK CHEQUE. She accepted a household, not every
      // act: a woman she was never told of is a betrayal at full price, whatever she agreed to. Without this,
      // "I accept your other wives" would buy permanent immunity, which is precisely the defect in
      // open_relationship that this work exists to correct (it suppresses her jealousy outright, for good).
      [Test]
      public void GIVEN_a_liaison_outside_what_she_agreed_to_WHEN_the_track_is_chosen_THEN_it_is_betrayal_at_full_price()
         => JealousyTrackPolicy.For(new JealousyTrackFacts {
               PartnerAcceptedTheArrangement = true,
               ActIsOutsideTheArrangement = true
            }).Should().Be(JealousyTrack.Betrayal);

      // AND THE SAME BOUNDARY HOLDS FOR A SHARING CULTURE. Khuzait custom shares a household; it does not
      // excuse a bond kept hidden from her, which is a different wound and keeps its own weight.
      [Test]
      public void GIVEN_a_sharing_culture_but_a_liaison_kept_from_her_WHEN_the_track_is_chosen_THEN_it_is_still_betrayal()
         => JealousyTrackPolicy.For(new JealousyTrackFacts {
               CultureToleratesSharing = true,
               ActIsOutsideTheArrangement = true
            }).Should().Be(JealousyTrack.Betrayal);

      // HER WORD CAN LAPSE. yozakura12 gave the condition himself: Corein accepts "as long as she remains the
      // first/senior wife". An acceptance that survives its own condition would be the mod telling the player
      // she agreed to something she plainly did not, so a withdrawn acceptance returns her to her culture's
      // own track rather than leaving her softened for ever.
      [Test]
      public void GIVEN_an_acceptance_she_has_since_withdrawn_WHEN_the_track_is_chosen_THEN_she_is_back_on_her_cultures_track()
         => JealousyTrackPolicy.For(new JealousyTrackFacts {PartnerAcceptedTheArrangement = false}).Should().Be(JealousyTrack.Betrayal);
   }
}
