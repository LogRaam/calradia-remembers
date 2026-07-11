// Code written by Gabriel Mailhot, 20/06/2026.
// Extracted from the mod (Sprint 24 jealousy): the reaction barèmes — how hard a jealous party
// turns on the player, how likely they are to write, and the grievance/letter framing. Pure so the
// numbers (which break silently) and the betrayal-vs-competition split stay protected by tests.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The pure tuning behind a jealousy reaction: the relation hit (<see cref="Severity" />),
    ///   the chance of a confronting letter (<see cref="LetterChance" />), and the in-character
    ///   grievance / letter framing. The host supplies the resolved stake, act tier, whether the
    ///   reaction is competition (a sharing culture tolerating the new partner) and the culture.
    /// </summary>
    public static class JealousyReaction
    {
        /// <summary>
        ///   The relation penalty (a positive magnitude; the host applies it as negative). A betrayed
        ///   exclusive partner cuts deepest; the same partner in a sharing culture merely competes.
        ///   Exclusivity-stake hits are lightly scaled by the culture's outrage, never to zero.
        /// </summary>
        public static int Severity(JealousStake stake, RomanticActTier tier, bool competition, JealousyCulture culture)
        {
            int b;
            switch (stake)
            {
                case JealousStake.SpouseOfCourted:
                    b = tier == RomanticActTier.Flirt ? 8 : tier == RomanticActTier.Intimacy ? 20 : 22;
                    return Scale(b, culture);
                case JealousStake.PlayerPartner:
                    if (competition)
                        return tier == RomanticActTier.Flirt ? 3 : tier == RomanticActTier.Intimacy ? 6 : 8;
                    return tier == RomanticActTier.Flirt ? 10 : tier == RomanticActTier.Intimacy ? 25 : 40;
                case JealousStake.RivalSuitor:
                    b = tier == RomanticActTier.Flirt ? 6 : tier == RomanticActTier.Intimacy ? 15 : 18;
                    return Scale(b, culture);
                case JealousStake.SecretLover:
                    // A discreet bond wounds quietly: real hurt, milder than a betrayed open partner, and
                    // culture never scales it (the bond is personal, outside any public custom).
                    return tier == RomanticActTier.Flirt ? 5 : tier == RomanticActTier.Intimacy ? 12 : 20;
                default: // SpurnedAdmirer
                    b = tier == RomanticActTier.Flirt ? 5 : tier == RomanticActTier.Intimacy ? 12 : 15;
                    return Scale(b, culture);
            }
        }

        /// <summary>Probability the jealous party sends a confronting/threatening letter, by stake.</summary>
        public static float LetterChance(JealousStake stake)
        {
            switch (stake)
            {
                case JealousStake.RivalSuitor:     return 0.80f;
                case JealousStake.SecretLover:     return 0.75f; // letters are the discreet bond's own channel
                case JealousStake.SpouseOfCourted: return 0.70f;
                case JealousStake.PlayerPartner:   return 0.65f;
                default:                           return 0.50f;
            }
        }

        /// <summary>The first-person grievance memory the jealous party records about the player.</summary>
        public static string BuildGrievance(JealousStake stake, bool competition, string courted)
        {
            switch (stake)
            {
                case JealousStake.SpouseOfCourted:
                    return $"The player made romantic advances toward {courted}, my own spouse. I will not forget the affront.";
                case JealousStake.PlayerPartner:
                    return competition
                        ? $"My partner has taken up with {courted} as well. I will not be made the lesser of their loves."
                        : $"My partner gave their affection to {courted} while bound to me. The betrayal cuts deep.";
                case JealousStake.RivalSuitor:
                    return $"The player has been courting {courted}, whom I had hoped to win for myself. I mean to make them regret it.";
                case JealousStake.SecretLover:
                    return $"What we share is secret, and still they turned to {courted}. I said nothing before the others; the hurt is mine to carry quietly.";
                default:
                    return $"I offered the player my heart, and they turned instead to {courted}.";
            }
        }

        /// <summary>The prompt context steering the jealous party's confronting letter.</summary>
        public static string BuildLetterContext(
            JealousStake stake, bool competition, string partyName, string courted, JealousyCulture culture)
        {
            string name = string.IsNullOrWhiteSpace(partyName) ? "the writer" : partyName;
            string hint = culture == null || string.IsNullOrWhiteSpace(culture.PromptHint) ? "" : " " + culture.PromptHint;
            switch (stake)
            {
                case JealousStake.SpouseOfCourted:
                    return $"You are {name}, wed to {courted}. The player has made romantic advances toward your spouse." +
                           $"{hint} Confront or warn the player, and demand they keep away from {courted}.";
                case JealousStake.PlayerPartner:
                    return competition
                        ? $"You are one of the player's partners. They have now taken up with {courted} as well. By your customs " +
                          $"this sharing is permitted —{hint} — so your grievance is rank and attention: insist you not be set " +
                          $"beneath {courted}, and that the player honour what is owed to you."
                        : $"You are the player's own partner, bound to them. They have given their affection to {courted} while " +
                          $"sworn to you.{hint} Write as the betrayed — wounded, cold, or openly threatening.";
                case JealousStake.RivalSuitor:
                    return $"You are {name}. You had hoped to win {courted}'s hand yourself, and now the player courts them." +
                           $"{hint} Warn the player off — make plain that you intend to marry {courted}, and that you will not yield quietly.";
                case JealousStake.SecretLover:
                    return $"You are {name}, the player's SECRET lover, a bond you both keep hidden by choice. They have " +
                           $"turned their affection toward {courted}. Write a PRIVATE, quietly wounded letter: tender and " +
                           "hurt, asking where you truly stand. You chose the secrecy to protect them, so you make NO " +
                           "public scene, NO threat, and NEVER hint at exposing what you share: the discretion holds even now.";
                default:
                    return $"You are {name}. You once felt deeply for the player, and they have turned instead to {courted}." +
                           $"{hint} Write as the spurned — bitter, reproachful, or vengeful.";
            }
        }

        /// <summary>Light cultural scaling for stakes where exclusivity stance modulates intensity (never to zero).</summary>
        private static int Scale(int baseValue, JealousyCulture culture)
        {
            JealousyOutrage outrage = culture?.Outrage ?? JealousyOutrage.High;
            float f = outrage == JealousyOutrage.High ? 1.0f
                    : outrage == JealousyOutrage.Medium ? 0.85f
                    : 0.7f;
            return Math.Max(1, (int) Math.Round(baseValue * f));
        }
    }
}
