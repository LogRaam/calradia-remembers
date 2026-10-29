// Code written by Gabriel Mailhot, 28/10/2026.
// The parley with an enemy ruler (peace, tribute, prisoners, a hostage, a non-aggression pact) was built and tested but
// reachable only from the console. timicu (Nexus, 28/10/2026) asked for "negotiating wars, peace treaties [...] territorial
// concessions [...] through natural conversations with NPCs, with successful negotiations actually changing the game
// world"; Gabriel (28/10/2026): "go", put it on the campaign bar. Peace and war are the decisions of whoever leads the
// faction (CouncilLift refuses them otherwise), so only a player who leads their faction is offered a parley, and only
// with a realm at war with them whose ruler can be met.

namespace CalradiaRemembers.Logic.Assemblies
{
   /// <summary>What decides whether a parley is offered with one realm.</summary>
   public sealed class ParleyEntryFacts
   {
      /// <summary>The player leads their faction: a realm's ruler, or the head of an independent clan.</summary>
      public bool PlayerLeadsFaction { get; init; }

      /// <summary>That realm is at war with the player's faction.</summary>
      public bool AtWarWithPlayer { get; init; }

      /// <summary>That realm has a ruler to negotiate with.</summary>
      public bool HasRuler { get; init; }

      /// <summary>That realm still stands.</summary>
      public bool Eliminated { get; init; }
   }

   /// <summary>Whether the campaign bar offers a parley with a realm.</summary>
   public static class ParleyEntryPolicy
   {
      /// <summary>True when the player may seek a parley with that realm from the council picker.</summary>
      public static bool MayOffer(ParleyEntryFacts f) => f != null && f.PlayerLeadsFaction && f.AtWarWithPlayer && f.HasRuler && !f.Eliminated;
   }
}
