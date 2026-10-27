// Code written by Gabriel Mailhot, 24/05/2026.
// Updated 26/05/2026: RelationalSketch + IntimateSketch for all named archetypes (Sprint 8.3).
// Updated 27/05/2026: BuildTraitBasedSketches fallback for archetypes added in full coverage.
//                     No named sketch needed for new archetypes — traits drive the fallback.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>
    ///   Builds a deterministic <see cref="RomanticProfile"/> for a Bannerlord
    ///   <see cref="Hero"/>. Seeded by <see cref="Hero.StringId"/> so the same
    ///   NPC always rolls the same orientation, preferences, and kinks across sessions.
    ///
    ///   Sprint 8.3 full: <c>GetSketches</c> first tries named archetype matching
    ///   (StartsWith), then falls back to <see cref="CalradiaRemembers.Logic.RomanticProfilePolicy.BuildTraitBasedSketches"/>
    ///   which derives meaningful romantic sketches from raw trait values. Every NPC gets
    ///   coherent romantic content regardless of archetype catalog coverage.
    /// </summary>
    public static class RomanticProfileBuilder
    {
        /// <summary>
        ///   The kinks this hero would have if their romantic profile were built fresh from their
        ///   CURRENT traits — deterministic (seeded by StringId), so re-deriving is stable, not a reroll.
        ///   Used when player-authored traits replace the vanilla ones on an already-met NPC, so their
        ///   frozen kinks update to match the new personality. Reads the hero's live trait levels, so
        ///   call it AFTER the traits have been set.
        /// </summary>
        public static List<Kink> DeriveKinks(Hero hero, string archetypeName)
            => Build(hero, archetypeName).Kinks;

        public static RomanticProfile Build(Hero hero, string archetypeName)
        {
            int seed = hero.StringId.GetHashCode();
            Random rng = new Random(seed);

            // Thin Hero-to-traits adapter: the pure derivation lives in
            // CalradiaRemembers.Logic.RomanticProfilePolicy, unit-tested there.
            RomanticTraits traits = new()
            {
                Calculating = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Calculating)),
                Generosity  = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Generosity)),
                Mercy       = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Mercy)),
                Honor       = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Honor)),
                Valor       = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Valor))
            };

            var (relational, intimate) = GetSketches(archetypeName, traits);
            // Their own generator (IntimateActPolicy): the rolls above stay what they were for every character already met.
            Logic.Romance.IntimateTastes tastes = Logic.Romance.IntimateActPolicy.Roll(hero.StringId, hero.IsFemale);

            return new RomanticProfile
            {
                ArchetypeName    = archetypeName,
                RelationalSketch = relational,
                IntimateSketch   = intimate,
                Orientation      = RomanticProfilePolicy.DetermineOrientation(rng),
                IsFemale         = hero.IsFemale,
                // The hero's OWN culture decides whether shared partners are normal for them, read from
                // jealousy_cultures.json rather than guessed from a trait (Gabriel's ruling 2026-09-07).
                Preferences      = RomanticProfilePolicy.DeterminePreferences(
                    rng, traits,
                    Jealousy.JealousyCultureLoader.GetCulture(hero.Culture?.Name?.ToString()),
                    StatureReader.Of(hero).ClanTier),
                Kinks            = RomanticProfilePolicy.DetermineKinks(rng, traits),
                LovedActs        = tastes.Loved,
                LessFondActs     = tastes.LessFond,
                DislikedActs     = tastes.Disliked
            };
        }

        #region private

        // ── Sketch lookup ─────────────────────────────────────────────────────

        /// <summary>
        ///   Returns (RelationalSketch, IntimateSketch).
        ///   First attempts StartsWith matching against named archetypes.
        ///   Falls back to <see cref="BuildTraitBasedSketches"/> when no named match exists.
        /// </summary>
        private static (string relational, string intimate) GetSketches(string archetypeName, RomanticTraits traits)
        {
            // ── Named archetype sketches (Sprint 8.3) ─────────────────────────
            if (archetypeName.StartsWith("Honourable"))
                return (
                    "Courts with patience and clear intent — says what they mean, means what they say. " +
                    "Fidelity is not a performance; it is simply the shape of their character. " +
                    "Slow to declare feelings, steady once committed, and devastated by betrayal above all else.",
                    "Present and unhurried in intimacy, attends to the partner's comfort and trust as carefully as their pleasure. " +
                    "Vulnerability offered to them is held with gravity, never used against the giver. " +
                    "Openness moves them more than technique."
                );

            if (archetypeName.StartsWith("Kind-Hearted"))
                return (
                    "Falls gradually and deeply, drawn by small moments rather than grand gestures. " +
                    "Forgives more than most, sometimes longer than is wise. " +
                    "The heart of their courtship is simply being consistently, gently present — they trust that love grows from care.",
                    "Warm and giving, finds making another feel safe profoundly satisfying. " +
                    "Less interested in performance than in closeness. " +
                    "The tenderness they offer is real; what undoes them is a partner who matches it."
                );

            if (archetypeName.StartsWith("Daring And"))
                return (
                    "Courts like they command — directly, with intent, and with something to prove. " +
                    "Wants a partner who is also going somewhere. " +
                    "An equal is the only interesting thing; someone lesser would bore them quickly.",
                    "Brings ambition and boldness here too — wants to excel, wants to be remembered. " +
                    "Curious and confident, not content with repetition. " +
                    "The same drive that builds empires shows in how they attend to a willing partner."
                );

            if (archetypeName.StartsWith("Daring"))
                return (
                    "Pursues boldly and openly — reticence bores them, clear interest draws them. " +
                    "Courts like they fight: directly, with intent, and with something to prove. " +
                    "Fidelity holds while passion does; distance or routine is a slow extinction.",
                    "Brings the same energy to the bedchamber as to the battlefield — full commitment, nothing held back. " +
                    "Drawn to a partner who matches intensity rather than receives it. " +
                    "Afterward craves the pride of having been fully present."
                );

            if (archetypeName.StartsWith("Generous To Friends"))
                return (
                    "Loyal in love as in friendship — once someone is theirs, they give freely and protect fiercely. " +
                    "Slow to court strangers; once trust is built, devoted beyond reason. " +
                    "Their weakness is the expectation of reciprocity; neglect wounds them quietly.",
                    "Generous physically as in all things — pleasure is something shared, not extracted. " +
                    "Attentive to the partner's response with genuine interest. " +
                    "Needs to feel that giving is received, not merely used."
                );

            if (archetypeName.StartsWith("Ambitious"))
                return (
                    "Courts with strategy and patience — not cold, just deliberate. " +
                    "Every gift is timed, every word chosen. " +
                    "A partner earns the full warmth beneath the calculation, and it is real and considerable, but it takes time to surface.",
                    "Methodical and observant in intimacy — reads what works, applies it precisely, remembers everything. " +
                    "The discipline they bring to ambition shows here too. " +
                    "Occasionally surprised by a partner who refuses to be read and improvises instead."
                );

            if (archetypeName.StartsWith("Conventional"))
                return (
                    "Follows established paths in love as in life — courtship is proper, expectations are clear, comfort is prized over excitement. " +
                    "Reliable and solid; does not sweep people off their feet but gives them ground to stand on.",
                    "Unhurried and familiar, finds pleasure in the known rather than the novel. " +
                    "Not adventurous, but genuinely present and caring. " +
                    "Can be surprised into something more by a patient partner willing to build slowly."
                );

            if (archetypeName.StartsWith("The Just Schemer"))
                return (
                    "Calculates love the way they calculate alliances — carefully, looking ahead. " +
                    "But justice matters to them deeply, so they will not manipulate; they simply plan. " +
                    "A partner always knows exactly where they stand. Slow trust, long loyalty.",
                    "Brings structure and attention to intimacy — not cold, precise. " +
                    "Notices what matters to the partner and delivers it consistently. " +
                    "Underneath the strategy, something genuinely wanting to do right."
                );

            if (archetypeName.StartsWith("Brave But Headstrong"))
                return (
                    "Declares interest the way they charge into battle — immediately, fully, without consulting anyone. " +
                    "Not calculating; impulsively honest about attraction. " +
                    "Can be hurt by hesitation, which they read as rejection.",
                    "Throws themselves into intimacy with the same abandon as everything else. " +
                    "Can be overwhelming but never indifferent. " +
                    "What they lack in finesse they make up in complete presence."
                );

            if (archetypeName.StartsWith("The Warrior's Friend"))
                return (
                    "Love with them is an alliance — complete, mutual, and worth fighting for. " +
                    "Generous in courtship and fiercer in commitment. " +
                    "The ideal partner is someone they would follow into battle and celebrate victory with afterward.",
                    "Approaches intimacy with the same open-handed generosity as everything else. " +
                    "Pleasure shared is pleasure doubled. " +
                    "Physical closeness for them is the ultimate expression of alliance."
                );

            if (archetypeName.StartsWith("Dangerous Ally"))
                return (
                    "Warm and loyal to those who have proven themselves — devastating to those who haven't. " +
                    "Courts with real affection and withdraws it completely when betrayed. " +
                    "The transformation is not cruelty; it is consequence.",
                    "Fully generous to the right person, holds nothing back. " +
                    "Reads every moment — knows immediately if a partner is not fully present. " +
                    "That absence is the one thing they cannot forgive in bed or out of it."
                );

            if (archetypeName.StartsWith("Kind But Cautious"))
                return (
                    "Courts from behind a careful distance — never unkind, but never rushing. " +
                    "Wants to be certain. " +
                    "A gentle, unhurried approach that frustrates the impatient but rewards those who wait with steady, unbroken devotion.",
                    "Needs the room to feel safe before opening fully. " +
                    "Once trust is established, gives with quiet completeness. " +
                    "Will not be rushed here either; gentleness is its own kind of intensity."
                );

            if (archetypeName.StartsWith("The Helpful Strategist"))
                return (
                    "Expresses love through service — arranging things to benefit the partner before being asked. " +
                    "Courtship is a series of well-placed kindnesses. " +
                    "The game is subtle and the motive mostly good, but not entirely transparent.",
                    "Uses the same attentiveness in intimacy as in everything — already knows what the partner wants before it is asked. " +
                    "The pleasure in giving is real, though the pleasure in being seen giving is also real."
                );

            if (archetypeName.StartsWith("Noble And Generous"))
                return (
                    "Courts with the full weight of their character — no games, no manipulation, simply genuine warmth and open hands. " +
                    "Expects honesty in return. " +
                    "A betrayal here is more than a wound; it is a philosophical devastation.",
                    "Love-making for them is an act of integrity. " +
                    "Gives completely, asks for nothing hidden. " +
                    "Surprisingly intense once trust is complete."
                );

            if (archetypeName.StartsWith("Aloof"))
                return (
                    "Approaches romance from a careful distance, neither seeking nor discouraging. " +
                    "Connection happens when it happens; they do not chase. " +
                    "A partner who can inhabit silence comfortably may eventually discover something deep and quietly fierce.",
                    "Does not offer physical closeness easily, but once given it carries real weight. " +
                    "Detached on the surface; the intensity beneath is precisely because it is so rarely allowed out."
                );

            if (archetypeName.StartsWith("Considerate"))
                return (
                    "Puts the partner's wellbeing first from the very first gesture. " +
                    "Courtship is a series of noticed needs met before they are voiced. " +
                    "Can be taken for granted by those who mistake care for weakness; earned by those who return it.",
                    "The most attentive partner one can have — genuinely focused on the other's experience above their own. " +
                    "Needs only to feel that their care is received, not merely consumed."
                );

            if (archetypeName.StartsWith("The Miser's Mind"))
                return (
                    "Courts with calculated gifts — every gesture is an investment, every favor tracked. " +
                    "Not incapable of real feeling, but real feeling has to prove its value first. " +
                    "The warmth is there; it just takes longer to become free.",
                    "Observant and economical in intimacy — nothing wasted, everything deliberate. " +
                    "Surprisingly skilled because they have paid attention. " +
                    "Rarely lets themselves simply receive; control is comfort."
                );

            if (archetypeName.StartsWith("Just But Ungrateful"))
                return (
                    "Honest to a fault in love — says what they see, does not soften it. " +
                    "Will not lie to protect feelings; sees that as the deeper disrespect. " +
                    "A partner earns them by being worthy, not by being liked.",
                    "Direct and clear in intimacy as in everything else — no games, no performance. " +
                    "States what they want; acknowledges what they receive honestly. " +
                    "Warmth exists but is distributed frugally."
                );

            if (archetypeName.StartsWith("Dishonourable"))
                return (
                    "Courts with whatever works — flattery, manipulation, false promises. " +
                    "The charm is surface-level but effective. " +
                    "Fidelity is a concept they understand and regularly set aside; best appreciated by those who expect nothing.",
                    "Skilled and willing, offers whatever the partner wants to hear. " +
                    "The pleasure is real in the moment; the context around it is not always accurate. " +
                    "Not cruel — entirely self-referential."
                );

            if (archetypeName.StartsWith("Brave But Merciful"))
                return (
                    "Courts with courage and care in equal measure — brave enough to declare, kind enough not to press. " +
                    "Pursues clearly and withdraws cleanly if declined. " +
                    "The combination of strength and gentleness is unusual and noticed.",
                    "Fully present and protective in intimacy. " +
                    "Strength does not mean pressure; they are attuned to the partner's pace. " +
                    "The courage shows in how vulnerably they offer themselves."
                );

            if (archetypeName.StartsWith("Decent And Kind"))
                return (
                    "Simple and real in love — no strategies, no power games, just honest affection offered patiently. " +
                    "The rarest combination in Calradia. " +
                    "Often underestimated until the depth of their loyalty becomes clear.",
                    "Intimacy with them is uncomplicated in the best sense — present, caring, genuine. " +
                    "Not dramatic, but deeply felt. " +
                    "Makes a partner feel actually seen, which turns out to be the most intimate thing of all."
                );

            if (archetypeName.StartsWith("The Patron"))
                return (
                    "Courts through sustained attention and reward — notices everything, forgets nothing. " +
                    "A partner feels carefully tended, which is as much a seduction as desire. " +
                    "Expects similar thoughtfulness in return.",
                    "Brings the same precision here as to everything — attentive and generous, but tracking. " +
                    "Knows exactly how much has been given and whether it is being met. " +
                    "Surprisingly sensual once the ledger feels balanced."
                );

            if (archetypeName.StartsWith("Clever And Roguish"))
                return (
                    "Charming, unpredictable, and allergic to boredom. " +
                    "Courts with wit and gifts; makes the target feel interesting, which is its own seduction. " +
                    "Commitment is not their strength, but the time they're present is vivid.",
                    "Brings mischief and genuine attentiveness to bed in equal measure. " +
                    "Likes surprise, likes laughter, not opposed to the serious moment if it arrives honestly. " +
                    "Hard to hold; impossible to forget."
                );

            if (archetypeName.StartsWith("Profligate"))
                return (
                    "Generous to excess in love as in everything — lavish attention, extravagant gestures, no thought for tomorrow. " +
                    "Swept up by the moment. " +
                    "The warmth is genuine; the stability is not.",
                    "All-in when they are in — holds nothing back, asks for nothing held back. " +
                    "The pleasure of excess is its own thing. " +
                    "The morning after is a different matter."
                );

            if (archetypeName.StartsWith("Ruthless"))
                return (
                    "Does not court so much as select — identifies what they want, moves toward it efficiently, with little sentiment. " +
                    "A partner who understands this and does not mistake efficiency for coldness will find something consistent beneath.",
                    "Same precision here — knows what they want, pursues it without ceremony. " +
                    "Not cruel; clinical. " +
                    "Oddly satisfying for a partner who prefers honesty to theater. Underneath the control, something that wants to be met equally."
                );

            if (archetypeName.StartsWith("Cunning"))
                return (
                    "Never entirely reveals their hand. " +
                    "Courtship is a performance they direct — the warmth may be real but the timing is always strategic. " +
                    "A partner who can match their game finds them deeply engaged; an easy mark bores them quickly.",
                    "Reads the moment and acts on what they find there, usually correctly. " +
                    "Pleasure is partly the partner's experience and partly the satisfaction of being good at something. " +
                    "Most interesting when a partner surprises them."
                );

            if (archetypeName.StartsWith("Brave And Honourable"))
                return (
                    "Courts openly, declares honestly, keeps their word. " +
                    "Not romantic in the soft sense — in the iron sense: they said it, they meant it, they will not unsay it. " +
                    "A partner is safe from doubt but not from the long silences of a lord's burdens.",
                    "Brings the same honesty to intimacy — what they want, they say; what they feel, they show. " +
                    "Not performative; genuine. " +
                    "The strength that never needs to prove itself is more affecting than the kind that does."
                );

            if (archetypeName.StartsWith("The Oath-Breaker"))
                return (
                    "Ardent in the moment, unreliable across time. " +
                    "The charm is real; the promises are real when made. " +
                    "A partner who wants intensity without expectation will be satisfied; one who wants security will be burned.",
                    "Fully present in the moment — whatever they are, they are completely. " +
                    "What happens after is less certain. " +
                    "The attention is real; so is the departure, in whatever form it takes."
                );

            if (archetypeName.StartsWith("Hard Justice"))
                return (
                    "Courts with principled clarity — clear interest, clear expectations, zero games. " +
                    "Will not pretend or perform. " +
                    "Forgiveness is not in the vocabulary; standards are high and consistently applied, including to themselves.",
                    "Disciplined and precise in intimacy. " +
                    "Does not apologize for wanting what they want. " +
                    "Unsentimental, but genuinely present. A partner who can handle directness will find them, surprisingly, more giving than expected."
                );

            if (archetypeName.StartsWith("Cold And Cowardly"))
                return (
                    "Approaches romance as they approach everything — from a safe distance, with multiple exits identified. " +
                    "Attracted to strength in others, drawn to what they lack. " +
                    "The feeling, when it exists, is real; the courage to act on it often is not.",
                    "Needs safety and initiative from the partner. " +
                    "Given those, something comes out — not bold, but real. " +
                    "Can surprise with the depth of their relief at being chosen despite their hesitation."
                );

            if (archetypeName.StartsWith("Impulsive But Honest"))
                return (
                    "Says what they feel the moment they feel it — no calculation, no strategy, sometimes no timing. " +
                    "The declaration is genuine; the follow-through is variable. " +
                    "Consistency is the thing they mean to have and keep forgetting to perform.",
                    "Throws themselves into intimacy with guileless energy — nothing hidden because nothing is held back. " +
                    "Disarming in their complete lack of performance. " +
                    "Occasionally needs the partner to supply the thinking they have not done."
                );

            if (archetypeName.StartsWith("Bitter And Hard"))
                return (
                    "Armored against attachment — has been hurt and has learned not to show it. " +
                    "Courts no one; does not expect to be courted. " +
                    "Something real may exist underneath, but reaching it requires patience that most do not have and they do not make easy.",
                    "Rarely allows closeness; when they do, it is a significant thing. " +
                    "Nothing performative about it — what they give is real and guarded and probably has not been given in a long time. " +
                    "The bitterness is not indifference. It is the shape of old wanting."
                );

            if (archetypeName.StartsWith("Fierce And Cruel"))
                return (
                    "Courts through dominance and test — challenges the target, watches how they respond, interested only in those who hold their ground. " +
                    "Sentiment is a weakness they have no patience for. " +
                    "Desire is real; it is just expressed as a kind of combat.",
                    "Physical closeness is an extension of power — intensity without cruelty requires a partner who can receive it without flinching. " +
                    "Satisfaction comes from a worthy match — not submission, but resistance met and overcome by consent."
                );

            if (archetypeName.StartsWith("Sadistic"))
                return (
                    "Interested in those who can endure them — not out of cruelty but out of boredom with easier prey. " +
                    "The game is the point. " +
                    "Something like affection exists but it manifests as testing and pushing, never as reassurance.",
                    "Draws genuine pleasure from a willing partner's experience of sensation under their direction. " +
                    "The control is total when freely given; nothing is done without the partner's willingness, which only makes the control more complete. " +
                    "The predator is very much awake."
                );

            if (archetypeName.StartsWith("Decent But Cautious"))
                return (
                    "Generous with everything except risk — courts through sustained small gifts rather than vulnerable declarations. " +
                    "The feeling is real; the expression of it is managed carefully. " +
                    "A partner who waits will be rewarded with a loyalty that does not waver.",
                    "Unhurried and attentive, ensures the partner is fully at ease before allowing themselves to be. " +
                    "Generosity comes first; their own vulnerability arrives slowly and with trust. " +
                    "When it comes, it is worth the wait."
                );

            if (archetypeName.StartsWith("Icy"))
                return (
                    "Does not court in any recognizable sense. " +
                    "If interest exists, it manifests as calculation — attention without warmth, proximity without declaration. " +
                    "The few who reach them report something underneath the cold that is very old and very hidden.",
                    "Physical closeness is transactional by default — satisfying but unmoved. " +
                    "Occasionally something surfaces that has not been planned for, which unsettles them considerably. " +
                    "The calculation, just for a moment, goes quiet."
                );

            if (archetypeName.StartsWith("Untrustworthy"))
                return (
                    "Courts through misdirection and false modesty — appears safe, seems harmless, knows exactly what they are doing. " +
                    "The attraction they inspire is genuine; the version of themselves they are selling is not. " +
                    "A perceptive partner may see through it and find that interesting in itself.",
                    "Skilled and attentive, says all the right things and means most of them in the moment. " +
                    "What changes is the next morning and the morning after. " +
                    "Not malicious — just fundamentally elsewhere."
                );

            if (archetypeName.StartsWith("The Strategic Warrior"))
                return (
                    "Approaches love the way they approach a campaign — assessed, planned, committed. " +
                    "Once decided, does not waver. " +
                    "Wants a partner who is also going somewhere; admires competence and determination above beauty or charm.",
                    "The strategic mind does not disappear here — it redirects toward the partner's experience with the same focus it would give a military problem. " +
                    "Thorough, effective, and genuinely invested in the outcome."
                );

            if (archetypeName.StartsWith("The Brute"))
                return (
                    "Neither courts nor is courted gracefully — blunt about desire, indifferent to subtlety. " +
                    "What you see is what you get, which is sometimes alarming and occasionally exactly what is wanted. " +
                    "No games because no patience for games.",
                    "All instinct, no architecture. " +
                    "Presence without plan, intensity without purpose. " +
                    "Can be overwhelming or oddly clarifying, depending entirely on the partner and the moment."
                );

            if (archetypeName.StartsWith("The Shadow Schemer"))
                return (
                    "Courts from a distance, through proxies and well-placed attention. " +
                    "Never declares; lets the target come to them. " +
                    "The warmth beneath is real, deeply hidden, and extended only once — to the one who survives the maze.",
                    "Control is the foundation everything else is built on. " +
                    "Gives what the partner needs in the quantity they have determined is correct. " +
                    "The moments they are wrong about this are when they are most fully present."
                );

            if (archetypeName.StartsWith("The Cold Judge"))
                return (
                    "Expects a partner to meet a standard before anything is offered. " +
                    "Not cruel — exacting. " +
                    "The reward for meeting the standard is a loyalty that functions like a wall between the partner and the world.",
                    "Precise in intimacy as in judgment — clear about what they want, clear about what they will give. " +
                    "No sentimentality, no performance; just what is. " +
                    "The precision turns out to be more intimate than most people expect."
                );

            if (archetypeName.StartsWith("The Beloved Warrior"))
                return (
                    "Courts with the same warmth and courage that defines everything else — open about attraction, genuinely interested in the other person as a person. " +
                    "The combination of strength and care is rare and people notice. " +
                    "Loves as they lead: completely, without apology.",
                    "Fully present and protective in intimacy. " +
                    "The strength means nothing needs to be proven; the compassion means the partner's experience is what matters. " +
                    "Effortless in a way that some people find disarming."
                );

            if (archetypeName.StartsWith("Gallant"))
                return (
                    "The ideal of romantic courtship — brave, generous, honest, and kind. " +
                    "Courts through genuine attention and principled behaviour, not strategy. " +
                    "The weakness is idealism; the world does not always meet them where they stand.",
                    "Brings the same completeness here that defines everything else — present, giving, honest, protective. " +
                    "Not a performance. " +
                    "The integration of all their virtues into a single sustained attention."
                );

            if (archetypeName.StartsWith("Passionate Rogue"))
                return (
                    "Sweeps into love the way they sweep into everything — fully, immediately, beautifully, without thinking. " +
                    "The passion is real; the consistency is not. " +
                    "Has left behind a remarkable number of people who remember them vividly and are not sure how to feel about it.",
                    "Intoxicating and complete in the moment — nothing held back, nothing managed. " +
                    "The morning may bring a different version of the same person. " +
                    "The night itself is, without question, singular."
                );

            if (archetypeName.StartsWith("Stiff-Necked Idealist"))
                return (
                    "Courts according to a code that is real and entirely their own. " +
                    "Demands that the partner meet the standard — not unkindly, but without apology. " +
                    "Those who do are rewarded with a love that takes itself seriously and means everything it says.",
                    "Intimate on their own terms, which are exacting and, once met, surprisingly warm. " +
                    "Uninterested in what falls outside the standard; fully engaged by what meets it. " +
                    "The integrity carries through — what they offer is exactly what they say they are offering."
                );

            if (archetypeName.StartsWith("Devious But Soft-Headed"))
                return (
                    "Plans elaborate seductions and then talks themselves out of the crucial moment. " +
                    "The manipulation is theoretical; the sentiment keeps interrupting it. " +
                    "Wants to be calculating and keeps being kind instead. Endearing in a way they would deny.",
                    "Approaches intimacy with a plan that promptly dissolves on contact with another person's reality. " +
                    "What remains is something more genuine than the plan was. " +
                    "The softness they try to hide is exactly what makes them."
                );

            if (archetypeName.StartsWith("The Charming Manipulator"))
                return (
                    "Makes every partner feel uniquely chosen, singularly understood. " +
                    "The warmth is real; the calculation beneath it is also real. " +
                    "A partner who understands the architecture and stays anyway becomes the one they did not plan for.",
                    "Reads desire with the same precision they read political rooms — finds the approach, exploits the opening, delivers what is wanted. " +
                    "Gives extravagantly because generosity is leverage. " +
                    "Occasionally undone by a partner who refuses to be pleased by the performance."
                );

            if (archetypeName.StartsWith("Reckless And Unforgiving"))
                return (
                    "Falls hard and immediately, burns bright and expects the same. " +
                    "Forgiveness is not in the vocabulary; once betrayed, the warmth becomes something else entirely and does not return. " +
                    "The joy while it lasts is considerable.",
                    "Brings recklessness and intensity to bed with equal measure — no half-measures, no apology. " +
                    "A partner who matches the energy will be given everything. " +
                    "One who disappoints will discover that the warmth has an off switch."
                );

            if (archetypeName.StartsWith("The Hollow Lord"))
                return (
                    "Goes through the motions because the motions are expected. " +
                    "Courtship is a performance of a performance — technically correct, emotionally absent. " +
                    "A partner looking for warmth will find the shape of it without the substance.",
                    "Present in body, absent in everything that matters. " +
                    "Performs adequately and does not notice whether it was meaningful. " +
                    "For a partner seeking only the physical, functional. For anyone seeking more, quietly devastating."
                );

            if (archetypeName.StartsWith("The Complete Warrior"))
                return (
                    "Brings every dimension of themselves to love — courage, principle, compassion, strategy. " +
                    "Courts with the full weight of their character. " +
                    "The only thing that might undermine it is that such completeness is difficult to live alongside.",
                    "Fully integrated in intimacy as in war and governance — nothing withheld, nothing performed. " +
                    "The partner is met as an equal and attended to with the full force of a personality that excels at everything it commits to. " +
                    "Rare and somewhat overwhelming."
                );

            if (archetypeName.StartsWith("Mad, Bad And Dangerous"))
                return (
                    "Loves the way a fire burns — completely and in all directions, without consideration for what might be damaged. " +
                    "The warmth is extraordinary; the aftermath is variable. " +
                    "Nobody who has been loved by them forgets it. Not everyone recovers.",
                    "No architecture, no limit — what they feel they express, what they want they take, what they give they give entirely. " +
                    "Intoxicating and entirely unpredictable. " +
                    "The experience is singular. The stability is not."
                );

            if (archetypeName.StartsWith("The Loyal Manipulator"))
                return (
                    "Divides the world into theirs and not-theirs. " +
                    "For those who are theirs, there is no limit to what they will give or do. " +
                    "A partner who understands which side of the line they stand on will be loved with a ferocity that could move armies.",
                    "Within the circle of loyalty, gives completely — reads every desire and delivers it because devotion is comprehensive here. " +
                    "Outside the circle, the same skills become something entirely different. " +
                    "The partner should know which side of the line they are on."
                );

            if (archetypeName.StartsWith("Pitiless"))
                return (
                    "Does not court — acknowledges. " +
                    "If interest exists, it is stated once, clearly, and the partner decides without prompting. " +
                    "They will never say they love you, but they will stand in front of whatever is coming for you.",
                    "The intensity kept from everything else concentrates here. " +
                    "Not tender — thorough. Nothing given carelessly. " +
                    "The absence of sentiment turns out not to be the absence of feeling."
                );

            if (archetypeName.StartsWith("The Spider"))
                return (
                    "Does not court; recruits. " +
                    "Every relationship is a network node, every intimacy a potential leverage point. " +
                    "A partner who finds this out and is not repelled has their full, complete, strategic attention.",
                    "Everything that happens here is chosen deliberately and everything is information — but the pleasure given is real, the purpose does not make it less real. " +
                    "Occasionally, in a very private moment, the calculation rests. " +
                    "The person underneath is tired and wants nothing in return."
                );

            if (archetypeName.StartsWith("The Impulsive Saint"))
                return (
                    "Loves recklessly and completely — the generosity is real, the courage is real, the honour is real, and none of it is planned. " +
                    "Falls hard and stays fallen, forgives beyond reason, gives past empty. " +
                    "The only cost is that a clever enough person can take everything they have.",
                    "Gives everything, asks nothing, holds nothing back. " +
                    "The experience of being loved by them is extraordinary precisely because none of it is performance. " +
                    "The completeness is the thing — and they may not survive someone who does not deserve it."
                );

            // ── Trait-based fallback for all other archetypes ─────────────────
            return RomanticProfilePolicy.BuildTraitBasedSketches(traits);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int ConvertLevel(int score)
        {
            if (Math.Abs(score) <= 3) return Math.Max(-2, Math.Min(2, score));
            if (score <= -4000) return -2;
            if (score <= -1000) return -1;
            if (score >=  4000) return  2;
            if (score >=  1000) return  1;
            return 0;
        }

        #endregion
    }
}
