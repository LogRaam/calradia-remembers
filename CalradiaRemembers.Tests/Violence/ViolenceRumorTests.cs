// Code written by Gabriel Mailhot, 06/09/2026.
// Player violence Inc 4: whether a deed becomes news, and the words the realm uses for it. Two things are pinned
// here. The CIRCULATION rule decides what the world may confront the player about, and it carries the pillar's
// balance: a murder must travel even when nobody watched (a body is found, a name is missed), or the optimal play
// would be to isolate every victim and kill in silence forever; while a robbery nobody saw must NOT become realm
// news, or the map would gossip about every mugging and the feature would read as noise. The WORDING guards the
// same truth rule as the battle pillar: the realm speaks as hearsay, and only a real killing is spoken of as a
// death, because these lines are fed straight into a prompt.

#region

using CalradiaRemembers.Logic.Violence;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Violence
{
    [TestFixture]
    public sealed class ViolenceRumorTests
    {
        private static int Rumor(ViolentAct act,
            ViolenceVerdict verdict = ViolenceVerdict.Succeeded,
            ViolenceExposure exposure = ViolenceExposure.Alone)
            => ViolenceConsequencePolicy.Resolve(act, verdict, exposure).RumorMagnitude;

        // THE balance rule: a killing travels even with nobody watching. Without this, murdering in private would be
        // the strictly optimal play, which is the exploit the whole pillar is built to avoid.
        [Test]
        public void GIVEN_a_murder_nobody_saw_WHEN_priced_THEN_word_still_travels()
        {
            Rumor(ViolentAct.Kill).Should().BeGreaterThan(0);
        }

        // The other half: a robbery or beating nobody saw does NOT become realm news. It still cost the player with
        // the law and the victim, but the map must not gossip about every unseen mugging.
        [Test]
        public void GIVEN_an_unseen_robbery_or_beating_WHEN_priced_THEN_it_does_not_become_news()
        {
            Rumor(ViolentAct.Rob).Should().Be(0);
            Rumor(ViolentAct.Assault).Should().Be(0);
        }

        // Seen, they do travel: witnesses are what turn a private deed into something the realm repeats.
        [Test]
        public void GIVEN_witnesses_WHEN_priced_THEN_a_robbery_or_beating_becomes_news()
        {
            Rumor(ViolentAct.Rob, exposure: ViolenceExposure.Onlookers).Should().BeGreaterThan(0);
            Rumor(ViolentAct.Assault, exposure: ViolenceExposure.Onlookers).Should().BeGreaterThan(0);
        }

        // A murder is the graver news whatever the audience, so it must outweigh a beating people watched.
        [Test]
        public void GIVEN_a_murder_against_a_public_beating_WHEN_priced_THEN_the_murder_carries_further()
        {
            Rumor(ViolentAct.Kill).Should().BeGreaterThan(Rumor(ViolentAct.Assault, exposure: ViolenceExposure.Guarded));
        }

        // A failed attempt is talk, not news: it has already cost the player with the law and the victim, and the
        // realm does not carry rumours of things that did not happen.
        [Test]
        public void GIVEN_a_failed_attempt_WHEN_priced_THEN_nothing_circulates()
        {
            Rumor(ViolentAct.Kill, ViolenceVerdict.Failed).Should().Be(0);
            Rumor(ViolentAct.Assault, ViolenceVerdict.Failed, ViolenceExposure.Guarded).Should().Be(0);
        }

        // The realm speaks as HEARSAY, naming both parties: the recent-matter registers route a rumour about the
        // player differently from something a lord lived, so the voice has to be right.
        [Test]
        public void GIVEN_a_deed_that_travels_WHEN_worded_THEN_the_realm_speaks_as_hearsay()
        {
            string line = ViolenceRumorText.Gossiped("Aeliana", "Snorri", ViolentAct.Kill, "Sargot");

            line.Should().StartWith("They say").And.Contain("Aeliana").And.Contain("Snorri").And.Contain("Sargot");
        }

        // The truth rule, same as the battle pillar: only a real killing is spoken of as a death, so a beaten victim
        // is never mourned by an NPC who then meets them alive.
        [Test]
        public void GIVEN_a_beating_or_robbery_WHEN_worded_THEN_no_death_is_claimed()
        {
            ViolenceRumorText.Gossiped("Aeliana", "Snorri", ViolentAct.Assault, "").Should().NotContain("murder");
            ViolenceRumorText.Gossiped("Aeliana", "Snorri", ViolentAct.Rob, "").Should().NotContain("murder");
            ViolenceRumorText.Gossiped("Aeliana", "Snorri", ViolentAct.Kill, "").Should().Contain("murder");
        }

        // A bystander speaks in the FIRST person, because they lived it: that is the register the recent-matter
        // approach frames as a memory rather than as distant rumour.
        [Test]
        public void GIVEN_a_bystander_WHEN_worded_THEN_they_speak_first_hand()
        {
            ViolenceRumorText.Witnessed("Aeliana", "Snorri", ViolentAct.Kill).Should().StartWith("I saw");
        }

        // A missing place must not leave a dangling "at ." for the model to imitate, since these go into a prompt.
        [Test]
        public void GIVEN_no_place_WHEN_worded_THEN_the_line_stays_clean()
        {
            ViolenceRumorText.Gossiped("Aeliana", "Snorri", ViolentAct.Kill, null).Should().NotContain(" at .");
            ViolenceRumorText.Chronicled("Aeliana", "Snorri", ViolentAct.Rob, "").Should().NotContain(" at .");
        }
    }
}
