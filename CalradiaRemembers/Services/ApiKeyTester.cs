// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Providers;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Services
{
   /// <summary>
   ///   Backs the MCM "Test Connection" buttons. Resolves the current key for a provider
   ///   (MCM field, else the provider's env var — mirroring runtime) and probes a
   ///   lightweight endpoint, reporting the result in a popup. Self-contained so it works
   ///   from the main-menu options screen, before any campaign is loaded.
   /// </summary>
   public static class ApiKeyTester
   {
      private const string NanoGptEnvVar = "NANOGPT_API_KEY";
      private const string NanoGptModelsUrl = "https://nano-gpt.com/api/v1/models";

      // OpenRouter exposes a dedicated key-introspection endpoint; NanoGPT does not, so we
      // list models — both are GET + Bearer and spend no completion tokens.
      private const string OpenRouterAuthUrl = "https://openrouter.ai/api/v1/auth/key";
      private const string OpenRouterChatUrl = "https://openrouter.ai/api/v1/chat/completions";
      private const string OpenRouterEnvVar = "OPENROUTER_API_KEY";

      private static readonly HttpClient Http = new() {Timeout = TimeSpan.FromSeconds(20)};

      // The context probe reads thousands of tokens, which a local model on a CPU may take a minute to read: it runs on
      // its own client, bounded by the player's LLM Response Timeout instead of the 20 seconds a key check gets.
      private static readonly HttpClient ProbeHttp = new() {Timeout = System.Threading.Timeout.InfiniteTimeSpan};
      private static bool _running;

      /// <summary>
      ///   Tests OpenRouter end-to-end: the key (MCM field → OPENROUTER_API_KEY env var) AND
      ///   every configured model (roleplay, plus the NSFW model when set), via one-token
      ///   completions. A bad model id is the most common silent misconfiguration, so its
      ///   error is surfaced verbatim.
      /// </summary>
      public static void Run()
      {
         ModLog.Info("[API TEST] OpenRouter test clicked.");
         _ = RunOpenRouterAsync();
      }

      /// <summary>
      ///   Tests a custom OpenAI-compatible endpoint by sending a one-token completion.
      ///   The key is optional — blank = keyless server (LM Studio / Ollama default).
      /// </summary>
      public static void RunCustom()
      {
         ModLog.Info("[API TEST] endpoint test clicked.");
         _ = RunCustomAsync();
      }

      /// <summary>Tests the NanoGPT key (MCM field → NANOGPT_API_KEY env var).</summary>
      public static void RunNanoGpt()
      {
         ModLog.Info("[API TEST] NanoGPT test clicked.");
         Probe("NanoGPT", NanoGptModelsUrl, ResolveKey(CrSettings.Instance?.NanoGptApiKey, NanoGptEnvVar),
            "nano-gpt.com");
      }

      #region private

      /// <summary>
      ///   Pulls the human-readable message out of an OpenRouter error body
      ///   (<c>{"error":{"message":"..."}}</c>) without taking a JSON dependency.
      ///   Falls back to a trimmed raw body.
      /// </summary>
      private static string ExtractError(string body, int code)
      {
         if (string.IsNullOrWhiteSpace(body)) return $"HTTP {code}.";

         const string marker = "\"message\"";
         int i = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

         if (i < 0) return $"HTTP {code}: {(body.Length > 200 ? body.Substring(0, 200) + "…" : body)}";
         int colon = body.IndexOf(':', i + marker.Length);
         int q1 = colon >= 0
            ? body.IndexOf('"', colon + 1)
            : -1;
         int q2 = q1 >= 0
            ? body.IndexOf('"', q1 + 1)
            : -1;

         if (q1 >= 0 && q2 > q1) return $"HTTP {code}: {body.Substring(q1 + 1, q2 - q1 - 1)}";

         return $"HTTP {code}: {(body.Length > 200 ? body.Substring(0, 200) + "…" : body)}";
      }

      private static string JsonEscape(string s)
         => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

      private static void Probe(string provider, string url, string key, string keyPage)
      {
         if (_running) return;

         if (string.IsNullOrWhiteSpace(key))
         {
            ShowResult("No API key", $"Paste your {provider} key in the field above first.");

            return;
         }

         _running = true;
         MBInformationManager.AddQuickInformation(new TextObject($"Testing {provider} connection..."));
         _ = TestAsync(provider, url, key, keyPage);
      }

      private static string ResolveKey(string mcmValue, string envVar)
      {
         if (!string.IsNullOrWhiteSpace(mcmValue)) return mcmValue.Trim();

         return Environment.GetEnvironmentVariable(envVar);
      }

      private static async Task RunCustomAsync()
      {
         if (_running) return;

         string url = CrSettings.Instance?.CustomEndpointUrl?.Trim();
         if (string.IsNullOrWhiteSpace(url))
         {
            ShowResult("No endpoint URL", "Paste the server URL in the Endpoint URL field first (e.g. http://localhost:1234/v1).");

            return;
         }

         string model = CrSettings.Instance?.Model?.Trim();
         if (string.IsNullOrWhiteSpace(model) || model == Services.LlmServices.DefaultModel)
         {
            ShowResult("No model", "Enter your server's model id in the Model (roleplay) field of the LLM Provider section first.");

            return;
         }

         string key = CrSettings.Instance?.CustomApiKey?.Trim(); // blank is valid — keyless server

         _running = true;
         MBInformationManager.AddQuickInformation(new TextObject("Testing custom endpoint..."));

         string title,
                body;
         try
         {
            string chatUrl = url.TrimEnd('/') + "/chat/completions";
            (title, body) = await TestConfiguredModelsAsync(
               m => TestCustomChatAsync(chatUrl, key, m),
               model,
               "Your endpoint is reachable",
               "Make sure the server is running, the URL includes /v1, and the model ids are correct.").ConfigureAwait(false);

            int window = ModSettings.LocalContextTokens;
            if (title == "Connection OK" && ContextLengthPolicy.ShouldProbe(url, window))
               (title, body) = await ProbeContextAsync(chatUrl, key, model, window, body).ConfigureAwait(false);
         }
         catch (Exception ex)
         {
            title = "Connection error";
            body = $"Could not reach the endpoint: {ex.Message}";
         }
         finally
         {
            _running = false;
         }

         ShowResult(title, body);
      }

      /// <summary>
      ///   One-token-tests every configured model: the roleplay model, then the NSFW model when
      ///   it is set and different (sekiz5's translation, 07/11/2026, found the hint naming a 'consensual model' the
      ///   menu no longer shows: that hidden legacy field now only feeds the NSFW model's fallback, so the NSFW
      ///   model as scenes resolve it is what gets tested). Two model fields are two chances at a typo, and an explicit id
      ///   that only fails when a scene flips the mode is the worst place to find out — so the
      ///   Test button exercises both up front. Returns the popup title and body.
      /// </summary>
      /// <summary>
      ///   After the one-word test passed, sends one request of CR's real size (YubYub02, 24/11/2026: the button said
      ///   "ready to play" and every message in the game failed at once, refused by a model loaded with a short
      ///   context). Returns the popup title and body: the OK body with the size it held, or the context advice.
      /// </summary>
      private static async Task<(string title, string body)> ProbeContextAsync(string chatUrl, string key, string model, int window, string okBody)
      {
         int tokens = ContextLengthPolicy.ProbeTokens(window);
         MBInformationManager.AddQuickInformation(new TextObject($"Testing a request of about {tokens} tokens..."));

         LlmWireParameters shape = LlmParameterPolicy.Resolve(model, ModSettings.CustomParameterCompatibility);
         string tokenKey = shape.UseMaxCompletionTokens ? "max_completion_tokens" : "max_tokens";
         string text = ContextLengthPolicy.Filler(tokens) + "Reply with the word OK.";
         string payload = "{\"model\":\"" + JsonEscape(model) + "\",\"messages\":[{\"role\":\"user\",\"content\":\"" + JsonEscape(text) + "\"}],\"" + tokenKey + "\":1}";
         int timeoutSeconds = Math.Max(30, ModSettings.LlmResponseTimeoutSeconds);

         try
         {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            using (var req = new HttpRequestMessage(HttpMethod.Post, chatUrl))
            {
               if (!string.IsNullOrWhiteSpace(key))
                  req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
               req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
               using (HttpResponseMessage resp = await ProbeHttp.SendAsync(req, cts.Token).ConfigureAwait(false))
               {
                  string respBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                  ModLog.Info($"[API TEST] context probe of ~{tokens} tokens: HTTP {(int) resp.StatusCode}.");

                  if (resp.IsSuccessStatusCode)
                  {
                     string declareIt = window > 0
                        ? ""
                        : " Set 'Local model context (tokens)' below to the context length your server loads the model with, so CR fits each request to it.";

                     return ("Connection OK", $"{okBody}\n\nIt also read a request of about {tokens} tokens.{declareIt}");
                  }

                  string err = ExtractError(respBody, (int) resp.StatusCode);
                  string advice = ContextLengthPolicy.ExplainOverflow(respBody, window) ?? ContextLengthPolicy.ExplainOverflow(err, window);

                  return ("Context too short",
                     $"The model answered a one-word test, but refused a request of about {tokens} tokens, the size of a turn in the game:\n\n{err}" +
                     (advice == null ? "" : $"\n\n{advice}"));
               }
            }
         }
         catch (OperationCanceledException)
         {
            return ("Connection OK, but slow",
               $"{okBody}\n\nBut a request of about {tokens} tokens got no answer within {timeoutSeconds}s. Raise 'LLM Response Timeout', " +
               "try a smaller model or turn on 'Compact prompt for small models'.");
         }
         catch (Exception ex)
         {
            return ("Context test failed", $"{okBody}\n\nBut a request of about {tokens} tokens failed: {ex.Message}");
         }
      }

      private static async Task<(string title, string body)> TestConfiguredModelsAsync(
         Func<string, Task<(bool ok, string err)>> testModel,
         string roleplayModel,
         string successPrefix,
         string failureAdvice)
      {
         (bool rpOk, string rpErr) = await testModel(roleplayModel).ConfigureAwait(false);

         string consensualModel = ModSettings.CncModel;
         bool hasDistinctConsensual = consensualModel.Length > 0 && !string.Equals(consensualModel, roleplayModel, StringComparison.Ordinal);

         if (!hasDistinctConsensual)
         {
            string note = consensualModel.Length > 0
               ? " The NSFW model is the same id, so it is covered."
               : string.Empty;

            return rpOk
               ? ("Connection OK", $"{successPrefix} and the model '{roleplayModel}' responded.{note} You're ready to play.")
               : ("Model test failed", $"The model '{roleplayModel}' did not work:\n\n{rpErr}\n\n{failureAdvice}");
         }

         (bool consensualOk, string consensualErr) = await testModel(consensualModel).ConfigureAwait(false);

         if (rpOk && consensualOk)
            return ("Connection OK",
               $"{successPrefix}. Roleplay model '{roleplayModel}' responded. NSFW model '{consensualModel}' responded. " +
               "You're ready to play.");

         var sb = new StringBuilder();
         sb.Append(rpOk
            ? $"Roleplay model '{roleplayModel}': OK.\n"
            : $"Roleplay model '{roleplayModel}' FAILED:\n{rpErr}\n");
         sb.Append(consensualOk
            ? $"NSFW model '{consensualModel}': OK.\n"
            : $"NSFW model '{consensualModel}' FAILED:\n{consensualErr}\n");
         sb.Append('\n').Append(failureAdvice);

         return ("Model test failed", sb.ToString());
      }

      /// <summary>
      ///   Two-stage OpenRouter test: key introspection (clean 401 separation), then a
      ///   one-token completion against the chosen model so an invalid model id is caught
      ///   and reported with the provider's own error text.
      /// </summary>
      private static async Task RunOpenRouterAsync()
      {
         if (_running) return;

         string key = ResolveKey(CrSettings.Instance?.ApiKey, OpenRouterEnvVar);
         if (string.IsNullOrWhiteSpace(key))
         {
            ShowResult("No API key", "Paste your OpenRouter key in the field above first.");

            return;
         }

         string model = CrSettings.Instance?.Model?.Trim();
         if (string.IsNullOrWhiteSpace(model))
         {
            ShowResult("No model", "Enter an OpenRouter model id in the Model field first (e.g. x-ai/grok-4.20).");

            return;
         }

         _running = true;
         MBInformationManager.AddQuickInformation(new TextObject("Testing OpenRouter connection..."));

         string title,
                body;
         try
         {
            // Stage 1 — key introspection: separates a bad key (401) from a bad model.
            using (var req = new HttpRequestMessage(HttpMethod.Get, OpenRouterAuthUrl))
            {
               req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
               using (HttpResponseMessage resp = await Http.SendAsync(req).ConfigureAwait(false))
               {
                  if (!resp.IsSuccessStatusCode)
                  {
                     var code = (int) resp.StatusCode;
                     ShowResult("Connection failed", code == 401
                        ? "Key rejected (401). It may be wrong, disabled, or revoked. Check it at openrouter.ai/keys."
                        : $"OpenRouter returned HTTP {code}. Check your key and your account credits.");

                     return;
                  }
               }
            }

            // Stage 2 — a one-token completion per configured model (roleplay, then NSFW when
            // set) confirms the ids actually work before a scene depends on them.
            // The SAME routing the chat sends (Windmolen, 29/09/2026: this test passed with no pin while every chat
            // failed on his pin). The roleplay model rides the roleplay pin; another model the NSFW pin, or the
            // roleplay pin when that is blank, as ModelModePolicy resolves it at runtime.
            string prosePin = ModSettings.OpenRouterProviderPin;
            string nsfwPin = string.IsNullOrWhiteSpace(ModSettings.OpenRouterNsfwProviderPin) ? prosePin : ModSettings.OpenRouterNsfwProviderPin;
            (title, body) = await TestConfiguredModelsAsync(
               m => TestOpenRouterModelAsync(key, m, string.Equals(m, model, StringComparison.Ordinal) ? prosePin : nsfwPin),
               model,
               "Your OpenRouter key is valid",
               "Your key is valid; check the exact ids at openrouter.ai/models.").ConfigureAwait(false);
         }
         catch (Exception ex)
         {
            title = "Connection error";
            body = $"Could not reach OpenRouter: {ex.Message}";
         }
         finally
         {
            _running = false;
         }

         ShowResult(title, body);
      }

      private static void ShowResult(string title, string body)
      {
         // The button leaves no other trace: "the Test button no longer works" (Gabriel, 19/10/2026) is answered by
         // this line, or by its absence.
         ModLog.Info($"[API TEST] {title}: {body}");
         try
         {
            InformationManager.ShowInquiry(new InquiryData(
               title, body, true, false, "OK", null, null, null));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[API TEST] result popup failed: {ex.Message}");
            try { MBInformationManager.AddQuickInformation(new TextObject($"{title}: {body}")); }
            catch
            {
               /* last resort — nothing more we can do */
            }
         }
      }

      private static async Task TestAsync(string provider, string url, string key, string keyPage)
      {
         string title,
                body;
         try
         {
            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
               req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
               using (HttpResponseMessage resp = await Http.SendAsync(req).ConfigureAwait(false))
               {
                  if (resp.IsSuccessStatusCode)
                  {
                     title = "Connection OK";
                     body = $"Your {provider} API key is valid. You're ready to play.";
                  }
                  else
                  {
                     var code = (int) resp.StatusCode;
                     title = "Connection failed";
                     body = code == 401
                        ? $"Key rejected (401). It may be wrong, disabled, or revoked. Check it at {keyPage}."
                        : $"{provider} returned HTTP {code}. Check your key and your account credits.";
                  }
               }
            }
         }
         catch (Exception ex)
         {
            title = "Connection error";
            body = $"Could not reach {provider}: {ex.Message}";
         }
         finally
         {
            _running = false;
         }

         ShowResult(title, body);
      }

      private static async Task<(bool ok, string err)> TestCustomChatAsync(string chatUrl, string key, string model)
      {
         // Mirror the runtime parameter policy so the test exercises the same wire shape: an OpenAI reasoning
         // model (gpt-5*, o1/o3/o4*) rejects "max_tokens", which is the exact 400 a player saw on this button.
         LlmWireParameters shape = LlmParameterPolicy.Resolve(model, ModSettings.CustomParameterCompatibility);
         string tokenKey = shape.UseMaxCompletionTokens ? "max_completion_tokens" : "max_tokens";
         string payload = "{\"model\":\"" + JsonEscape(model) + "\",\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}],\"" + tokenKey + "\":1}";

         using (var req = new HttpRequestMessage(HttpMethod.Post, chatUrl))
         {
            if (!string.IsNullOrWhiteSpace(key))
               req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            using (HttpResponseMessage resp = await Http.SendAsync(req).ConfigureAwait(false))
            {
               string respBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

               return resp.IsSuccessStatusCode
                  ? (true, null)
                  : (false, ExtractError(respBody, (int) resp.StatusCode));
            }
         }
      }

      private static async Task<(bool ok, string err)> TestOpenRouterModelAsync(string key, string model, string pin)
      {
         bool fallbacks = ModSettings.AllowOpenRouterProviderFallbacks;
         string payload = "{\"model\":\"" + JsonEscape(model) + "\",\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}],\"max_tokens\":1"
                          + ProviderPinFailurePolicy.RoutingJson(pin, fallbacks) + "}";

         using (var req = new HttpRequestMessage(HttpMethod.Post, OpenRouterChatUrl))
         {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            using (HttpResponseMessage resp = await Http.SendAsync(req).ConfigureAwait(false))
            {
               string respBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

               if (resp.IsSuccessStatusCode) return (true, null);

               string err = ExtractError(respBody, (int) resp.StatusCode);

               return (false, ProviderPinFailurePolicy.Explain(respBody, !string.IsNullOrWhiteSpace(pin), fallbacks) ?? err);
            }
         }
      }

      #endregion
   }
}