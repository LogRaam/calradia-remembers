// Code written by Gabriel Mailhot, 06/10/2026.
// The banner raised when the player learns a grudge was formed. It once always said "against you"; a tale's ending can
// set two lords at odds (tashmetu, Nexus, 06/10/2026), and then the banner names the one it is held against.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>Pure text of the grudge-formed banner.</summary>
    public static class GrudgeBannerText
    {
        public static string Compose(string holderName, string targetName, bool targetIsPlayer)
            => targetIsPlayer || string.IsNullOrWhiteSpace(targetName)
                ? $"{holderName} now holds a grievance against you."
                : $"{holderName} now holds a grievance against {targetName}.";
    }
}
