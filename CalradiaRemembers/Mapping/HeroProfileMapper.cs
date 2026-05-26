// Code written by Gabriel Mailhot, 17/05/2026.
// Updated 24/05/2026: Style C archetypes (Sprint 8.1).
// Updated 24/05/2026: Passes archetype to RomanticProfileBuilder (Sprint 8.3).

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Mapping
{
    public static class HeroProfileMapper
    {
        public static NpcProfile FromHero(Hero hero)
        {
            // Compute archetype once — used for both Trait and RomanticProfile.
            string archetype = RetrievePersonalityFor(hero);

            NpcProfile profile = new()
            {
                Id          = hero.StringId,
                Name        = hero.Name?.ToString() ?? "Unknown",
                Faction     = hero.MapFaction?.Name?.ToString() ?? "Unaligned",
                Clan        = hero.Clan?.Name?.ToString() ?? "No clan",
                Personality = BuildPersonality(hero),
                Trait       = archetype
            };

            profile.Romantic = RomanticProfileBuilder.Build(hero, archetype);

            return profile;
        }

        #region private

        private static string BuildPersonality(Hero hero)
        {
            var parts = new List<string>();

            var culture = hero.Culture?.Name?.ToString();
            if (!string.IsNullOrEmpty(culture))
                parts.Add($"A {culture} by culture.");

            parts.Add(DescribeRank(hero));
            return string.Join(" ", parts);
        }

        private static string DescribeRank(Hero hero)
        {
            if (hero.IsKingdomLeader) return "Holds royal authority over a kingdom.";
            if (hero.IsFactionLeader) return "Leads a faction.";
            if (hero.IsClanLeader)    return "Leads a noble clan.";
            if (hero.IsLord)          return "A noble lord with land and retainers.";
            if (hero.IsMerchant)      return "A merchant by trade, a notable of the town.";
            if (hero.IsRuralNotable)  return "A respected village notable.";
            if (hero.IsArtisan)       return "An artisan of the town.";
            if (hero.IsGangLeader)    return "Leads an underworld gang.";
            return "A person of some standing in Calradia.";
        }

        private static int ConvertLevel(int score)
        {
            if (score <= -4000) return -2;
            if (score <= -1000) return -1;
            if (score >=  4000) return  2;
            if (score >=  1000) return  1;
            return 0;
        }

        private static string RetrievePersonalityFor(Hero hero)
        {
            Traits t = new()
            {
                Calculating = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Calculating)),
                Generosity  = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Generosity)),
                Mercy       = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Mercy)),
                Honor       = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Honor)),
                Valor       = ConvertLevel(hero.GetTraitLevel(DefaultTraits.Valor))
            };

            var count = CountActiveTraits(t);
            if (count == 1) return OneTraitPersonality(t);
            if (count == 2) return TwoTraitPersonality(t);
            if (count == 3) return ThreeTraitPersonality(t);
            if (count == 4) return FourTraitPersonality(t);
            if (count == 5) return FiveTraitPersonality(t);

            return "Thoroughly Conventional. Neither particularly noble nor cruel — shaped more by circumstance than character. Do not expect consistency.";
        }

        private static int CountActiveTraits(Traits t)
        {
            var count = 0;
            if (t.Calculating >= 1) count++;
            if (t.Generosity  >= 1) count++;
            if (t.Mercy       >= 1) count++;
            if (t.Honor       >= 1) count++;
            if (t.Valor       >= 1) count++;
            return count;
        }

        private static string OneTraitPersonality(Traits t)
        {
            if (t.Honor       == 1)  return "Honourable. Their word is their bond — they will refuse a profitable lie over a costly truth, and hold others to the same standard.";
            if (t.Mercy       == 1)  return "Kind-Hearted. Extends compassion where others would not, believes in second chances. Can be mistaken for weakness by those who prey on it.";
            if (t.Valor       == 1)  return "Daring. Seeks glory in danger and scorns those who hesitate. The battlefield is where they feel most alive.";
            if (t.Generosity  == 1)  return "Generous To Friends. Shares freely with those who have earned their loyalty — but gives nothing to strangers who have not yet proved their worth.";
            if (t.Calculating == 1)  return "Ambitious. Every word is weighed, every favor catalogued. Speaks with purpose and expects the same from others.";
            if (t.Valor       == -1) return "Conventional. Avoids unnecessary risk and prefers the safe path — not cowardly, but no hero either. Stability over glory.";
            return "Thoroughly Conventional. Neither particularly noble nor cruel — shaped more by circumstance than character.";
        }

        private static string TwoTraitPersonality(Traits t)
        {
            if (t.Honor == 1 && t.Calculating == 1)            return "The Just Schemer. Desires fairness and works hard to achieve it — but through careful strategy, not impulsive action. Slow to commit, precise in judgment.";
            if (t.Calculating == -1 && t.Valor == 1)           return "Brave But Headstrong. Charges where others hesitate, driven by courage rather than caution. Plans are for the timid — deeds speak louder.";
            if (t.Generosity == 1 && t.Valor == 1)             return "The Warrior's Friend. Brave and open-handed, loyal to a fault. Worth having at your side in battle and at your table in peace.";
            if (t.Generosity == 1 && t.Mercy == -1)            return "Dangerous Ally. Generous and warm to friends, coldly unforgiving to enemies. Cross them once and the warmth vanishes entirely.";
            if (t.Mercy == 1 && t.Valor == -1)                 return "Kind But Cautious. Gentle in spirit and careful in action — will not charge into danger, but will quietly stand by those who need them.";
            if (t.Mercy == 1 && t.Calculating == 1)            return "The Helpful Strategist. Uses their wits in service of others. Quietly arranges things to benefit those they care for — not always transparently.";
            if (t.Valor == 1 && t.Calculating == 1)            return "Daring And Ambitious. Brave enough to act boldly, clever enough to act wisely. A dangerous combination in an enemy, invaluable in an ally.";
            if (t.Generosity == 1 && t.Honor == 1)             return "Noble And Generous. Holds to principle and opens their purse freely. The kind of lord songs are written about — and rivals quietly resent.";
            if (t.Valor == -1 && t.Generosity == -1)           return "Aloof. Keeps to themselves, neither spending freely nor risking their neck. Cold company, but reliable within narrow limits.";
            if (t.Mercy == 1 && t.Generosity == 1)             return "Considerate. Puts others before themselves, generous with both coin and compassion. Can be taken advantage of by the ruthless.";
            if (t.Generosity == -1 && t.Calculating == 1)      return "The Miser's Mind. Tight-fisted and calculating — every coin spent is an investment, every favor a future debt to collect.";
            if (t.Generosity == -1 && t.Honor == 1)            return "Just But Ungrateful. Honest and principled, but expects others to earn every scrap. Will not lie, but will not reward loyalty either.";
            if (t.Generosity == -1 && t.Honor == -1)           return "Dishonourable In Every Way. Breaks oaths and hoards coin without shame or remorse. Cannot be trusted in any dealing, ever.";
            if (t.Valor == 1 && t.Mercy == 1)                  return "Brave But Merciful. Fights when needed, spares when possible. Commands respect from allies and defeated enemies alike.";
            if (t.Honor == 1 && t.Mercy == 1)                  return "Decent And Kind. Principled and compassionate — rare in Calradia. Others mistake it for weakness; they call it the only way to live.";
            if (t.Generosity == 1 && t.Calculating == 1)       return "The Patron. Rewards loyalty precisely and generously, keeping careful account of who serves them well. A good lord to follow.";
            if (t.Generosity == 1 && t.Honor == -1)            return "Clever And Roguish. Charming and open-handed, but plays fast and loose with oaths. Likeable — just do not shake their hand on a deal.";
            if (t.Generosity == 1 && t.Calculating == -1)      return "Profligate. Spends freely without thought of tomorrow. Beloved by those who benefit, doomed by those who plan against them.";
            if (t.Mercy == -1 && t.Calculating == 1)           return "Ruthless. Cold, deliberate, and without compassion for those in the way. Does not hate enemies — simply removes them.";
            if (t.Calculating == 1 && t.Honor == -1)           return "Cunning. Bends the truth when it suits them and plans three moves ahead. Never show them your hand.";
            if (t.Valor == 1 && t.Honor == 1)                  return "Brave And Honourable. Fights with courage and keeps their word. An old-fashioned lord in an age that has largely forgotten what that means.";
            if (t.Honor == -1 && t.Valor == 1)                 return "The Oath-Breaker. Brave enough to fight anyone, but their word means nothing. Will charge into battle beside you today and betray you tomorrow if it serves them.";
            if (t.Mercy == -1 && t.Honor == 1)                 return "Hard Justice. Principled and unforgiving — upholds their code to the letter but shows no compassion in doing so. Right and merciless in equal measure.";
            if (t.Mercy == -1 && t.Valor == -1)                return "Cold And Cowardly. Neither brave nor kind. Avoids danger and shows no mercy when others face it in their place.";
            if (t.Calculating == -1 && t.Honor == 1)           return "Impulsive But Honest. Says what they mean and acts on instinct — no schemes, no plots, just blunt principle and unfiltered reaction.";
            if (t.Mercy == -1 && t.Generosity == -1)           return "Bitter And Hard. Neither gives nor forgives. Life in Calradia has not been kind to them, and they see no reason to be kind to others.";
            return "Thoroughly Conventional. Neither particularly noble nor cruel — shaped more by circumstance than character.";
        }

        private static string ThreeTraitPersonality(Traits t)
        {
            if (t.Generosity == -1 && t.Valor == 1 && t.Mercy == -1)         return "Fierce And Cruel. Brave in battle and vicious in victory — does not give, does not forgive. Enemies fear them; subjects endure them.";
            if (t.Generosity == -1 && t.Honor == -1 && t.Mercy == -1)        return "Sadistic. Takes pleasure in the suffering of others, breaks oaths without guilt, hoards what others need. A predator wearing a lord's title.";
            if (t.Generosity == 1 && t.Honor == 1 && t.Calculating == -1)    return "Noble And Generous. High-minded and open-handed, acts on principle before strategy. May be outmaneuvered by sharper minds, but never loses their honor.";
            if (t.Generosity == 1 && t.Valor == -1 && t.Calculating == 1)    return "Decent But Cautious. Generous and strategic, but avoids the front line. Their gifts and plans speak louder than their sword arm.";
            if (t.Generosity == -1 && t.Mercy == -1 && t.Calculating == 1)   return "Icy. Hoards, schemes, and shows no compassion. Everything is a transaction; sentiment is a liability they have long since shed.";
            if (t.Honor == -1 && t.Valor == -1 && t.Calculating == 1)        return "Untrustworthy. Neither brave nor honest, but clever enough to be dangerous. Lies when cornered, retreats when challenged, schemes always.";
            if (t.Generosity == 1 && t.Honor == -1 && t.Valor == 1)          return "Clever And Roguish. Bold, charming, and free with coin — but oaths sit lightly on their shoulders. Thrilling company, uncertain ally.";
            if (t.Honor == 1 && t.Valor == 1 && t.Calculating == 1)          return "The Strategic Warrior. Brave, principled, and calculating — fights with a plan, holds to their word, and never charges without purpose. A formidable lord.";
            if (t.Honor == -1 && t.Mercy == -1 && t.Calculating == -1)       return "The Brute. Dishonest, cruel, and impulsive — acts on base instinct without plan or principle. Dangerous precisely because they are unpredictable.";
            if (t.Generosity == -1 && t.Valor == -1 && t.Calculating == 1)   return "The Shadow Schemer. Risk-averse, tight-fisted, and calculating. Never fights when they can maneuver; never spends when they can leverage debt instead.";
            if (t.Honor == 1 && t.Mercy == -1 && t.Calculating == 1)         return "The Cold Judge. Principled and strategic, utterly without compassion. Enforces their code precisely and punishes deviation without a moment's hesitation.";
            if (t.Mercy == 1 && t.Generosity == 1 && t.Valor == 1)           return "The Beloved Warrior. Brave, generous, and compassionate — the kind of lord who inspires songs. Not always wise, but deeply loved by those they lead.";
            return "Thoroughly Conventional. Neither particularly noble nor cruel — shaped more by circumstance than character.";
        }

        private static string FourTraitPersonality(Traits t)
        {
            if (t.Generosity == 1 && t.Honor == 1 && t.Valor == 1 && t.Mercy == 1)                return "Gallant. Brave, generous, honorable, and merciful — the ideal knight, if such a thing truly exists in Calradia. Inspires loyalty and earns respect even from enemies.";
            if (t.Generosity == 1 && t.Honor == -1 && t.Valor == 1 && t.Calculating == -1)        return "Passionate Rogue. Bold and generous but acts on impulse without regard for oaths or plans. Magnificent in a charge, disastrous in a negotiation.";
            if (t.Generosity == 1 && t.Honor == 1 && t.Mercy == -1 && t.Calculating == -1)        return "Stiff-Necked Idealist. Principled and open-handed but inflexible — demands others meet their standard and punishes those who fall short without remorse.";
            if (t.Honor == -1 && t.Valor == -1 && t.Mercy == 1 && t.Calculating == 1)             return "Devious But Soft-Headed. Schemes and manipulates but cannot bring themselves to see plans through when real suffering is required. A schemer without the stomach for it.";
            if (t.Generosity == 1 && t.Honor == -1 && t.Mercy == 1 && t.Calculating == 1)         return "The Charming Manipulator. Warm, generous, and compassionate on the surface — but every kindness is calculated, every friendship a future leverage point.";
            if (t.Generosity == 1 && t.Valor == 1 && t.Mercy == -1 && t.Calculating == -1)        return "Reckless And Unforgiving. Bold and generous with friends, merciless and impulsive with enemies. Their feasts are legendary — and so are their vendettas.";
            if (t.Generosity == 2 && t.Honor == 2 && t.Mercy == 1 && t.Calculating == 1)          return "Decent And Kind. Genuinely good — generous, honest, merciful, and thoughtful. Rare in Calradia. Most assume there is an angle; there is none.";
            if (t.Honor == -1 && t.Valor == -1 && t.Mercy == -1 && t.Calculating == -1)           return "The Hollow Lord. No courage, no compassion, no honor, no cunning. Holds their title by inheritance alone. A vacancy wearing armor, waiting to be removed.";
            if (t.Honor == 1 && t.Valor == 1 && t.Mercy == 1 && t.Calculating == 1)               return "The Complete Warrior. Brave, principled, merciful, and calculating — a rare and formidable combination. Either the realm's greatest protector or its most dangerous adversary.";
            return "Thoroughly Conventional. Neither particularly noble nor cruel — shaped more by circumstance than character.";
        }

        private static string FiveTraitPersonality(Traits t)
        {
            if (t.Generosity == 1 && t.Honor == -1 && t.Valor == 1 && t.Mercy == 1 && t.Calculating == -1)
                return "Mad, Bad And Dangerous To Know. Brave, warm-hearted, and giving — but bound by no oath and ruled entirely by impulse. Magnificent and catastrophic in equal measure.";
            if (t.Generosity == 1 && t.Honor == -1 && t.Valor == 1 && t.Mercy == -1 && t.Calculating == 1)
                return "The Loyal Manipulator. Fiercely protective of their inner circle, calculating in all else, and without mercy for those outside it. A wonderful friend. A terrible enemy.";
            if (t.Generosity == -1 && t.Honor == 1 && t.Valor == 1 && t.Mercy == -1 && t.Calculating == -1)
                return "Pitiless. Principled and brave — their honor is real — but utterly without compassion. Sees mercy as weakness and charity as waste. The code they follow is iron.";
            if (t.Honor == -1 && t.Valor == -1 && t.Mercy == -1 && t.Generosity == -1 && t.Calculating == 1)
                return "The Spider. No virtue but cleverness — lies, schemes, hoards, and avoids danger without shame. Rules through fear and manipulation, never through respect or love.";
            if (t.Honor == 1 && t.Valor == 1 && t.Mercy == 1 && t.Generosity == 1 && t.Calculating == -1)
                return "The Impulsive Saint. Brave, generous, honorable, and merciful — but acts entirely on feeling, never on strategy. Beloved by all, outmaneuvered by most.";
            return "Thoroughly Conventional. Neither particularly noble nor cruel — shaped more by circumstance than character.";
        }

        #endregion
    }

    public class Traits
    {
        public int Calculating;
        public int Generosity;
        public int Honor;
        public int Mercy;
        public int Valor;
    }
}
