// Code written by Gabriel Mailhot, 29/09/2026.
// When OpenRouter finds no endpoint because of the player's own provider pin (Windmolen, Nexus, 29/09/2026). He had pinned
// providers in MCM with fallbacks off (the default); none of them served deepseek/deepseek-v3.2 any more, and OpenRouter
// answered 404 "No endpoints found ... Filter by Fallback". The chat said only that the AI did not respond, and the
// in-game connection test passed, because it sent no pin at all. This names the cause, and builds the same routing
// the chat sends so the test fails where the chat fails.

#region

using System.Linq;
using System.Text.RegularExpressions;
using NpcMemoryService.Core.LlmClient.OpenRouter;

#endregion

namespace CalradiaRemembers.Logic.Providers
{
   /// <summary>Recognises a dead end made by the player's provider pin, and builds the pin into a request. Pure.</summary>
   public static class ProviderPinFailurePolicy
   {
      private const string NoEndpoints = "No endpoints found";

      /// <summary>
      ///   The player-facing explanation when the pin itself left OpenRouter no endpoint, or null when this failure
      ///   is something else (no pin, fallbacks allowed, or another error), which then keeps its own message.
      /// </summary>
      public static string Explain(string error, bool pinActive, bool fallbacksAllowed)
      {
         if (!pinActive || fallbacksAllowed || string.IsNullOrEmpty(error) || error.IndexOf(NoEndpoints, System.StringComparison.OrdinalIgnoreCase) < 0)
            return null;

         Match model = Regex.Match(error, NoEndpoints + @" for (\S+?)\.(?:\s|""|$)", RegexOptions.IgnoreCase);
         string which = model.Success ? model.Groups[1].Value : "your model";

         return $"None of the providers pinned in \"Pin Providers\" (Mod Options, OpenRouter) serves {which} any more. "
              + "Clear the pin, pin a provider that still serves it, or turn on \"Allow provider fallbacks\".";
      }

      /// <summary>
      ///   The routing a request carries for a pin, as a JSON fragment to append after the other fields (empty when
      ///   nothing is pinned): the same order and fallback rule OpenRouterClient sends with every chat request.
      /// </summary>
      public static string RoutingJson(string pinCsv, bool fallbacksAllowed)
      {
         string[] slugs = ProviderRouting.ParseSlugs(pinCsv);
         if (slugs.Length == 0) return "";

         string order = string.Join(",", slugs.Select(s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""));

         return $",\"provider\":{{\"order\":[{order}],\"allow_fallbacks\":{(fallbacksAllowed ? "true" : "false")}}}";
      }
   }
}
