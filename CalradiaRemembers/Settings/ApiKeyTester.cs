// Code written by Gabriel Mailhot, 05/06/2026.
// Sprint 16: MCM "Test Connection" button — validates the OpenRouter key without
//            starting a conversation (calls /auth/key, spends no tokens).
// Updated 09/06/2026: provider-aware — a parallel NanoGPT probe (GET /api/v1/models,
//            same Bearer auth path the real chat calls use) backs the NanoGPT button.

#region

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Settings
{
    /// <summary>
    ///   Backs the MCM "Test Connection" buttons. Resolves the current key for a provider
    ///   (MCM field, else the provider's env var — mirroring runtime) and probes a
    ///   lightweight endpoint, reporting the result in a popup. Self-contained so it works
    ///   from the main-menu options screen, before any campaign is loaded.
    /// </summary>
    public static class ApiKeyTester
    {
        private const string OpenRouterEnvVar = "OPENROUTER_API_KEY";
        private const string NanoGptEnvVar    = "NANOGPT_API_KEY";

        // OpenRouter exposes a dedicated key-introspection endpoint; NanoGPT does not, so we
        // list models — both are GET + Bearer and spend no completion tokens.
        private const string OpenRouterAuthUrl   = "https://openrouter.ai/api/v1/auth/key";
        private const string OpenRouterChatUrl   = "https://openrouter.ai/api/v1/chat/completions";
        private const string NanoGptModelsUrl    = "https://nano-gpt.com/api/v1/models";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static bool _running;

        /// <summary>
        ///   Tests OpenRouter end-to-end: the key (MCM field → OPENROUTER_API_KEY env var) AND
        ///   the configured model, via a one-token completion. A bad model id is the most common
        ///   silent misconfiguration, so its error is surfaced verbatim.
        /// </summary>
        public static void Run() => _ = RunOpenRouterAsync();

        /// <summary>Tests the NanoGPT key (MCM field → NANOGPT_API_KEY env var).</summary>
        public static void RunNanoGpt() =>
            Probe("NanoGPT", NanoGptModelsUrl, ResolveKey(CrSettings.Instance?.NanoGptApiKey, NanoGptEnvVar),
                  "nano-gpt.com");

        #region private

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

            string title, body;
            try
            {
                // Stage 1 — key introspection: separates a bad key (401) from a bad model.
                using (var req = new HttpRequestMessage(HttpMethod.Get, OpenRouterAuthUrl))
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    using (HttpResponseMessage resp = await Http.SendAsync(req).ConfigureAwait(false))
                        if (!resp.IsSuccessStatusCode)
                        {
                            int code = (int) resp.StatusCode;
                            ShowResult("Connection failed", code == 401
                                ? "Key rejected (401). It may be wrong, disabled, or revoked. Check it at openrouter.ai/keys."
                                : $"OpenRouter returned HTTP {code}. Check your key and your account credits.");
                            return;
                        }
                }

                // Stage 2 — a one-token completion confirms the model id actually works.
                (bool ok, string err) = await TestOpenRouterModelAsync(key, model).ConfigureAwait(false);
                title = ok ? "Connection OK" : "Model test failed";
                body  = ok
                    ? $"Your OpenRouter key is valid and the model '{model}' responded. You're ready to play."
                    : $"Your key is valid, but the model '{model}' did not work:\n\n{err}\n\n" +
                      "Check the exact id at openrouter.ai/models.";
            }
            catch (Exception ex)
            {
                title = "Connection error";
                body  = $"Could not reach OpenRouter: {ex.Message}";
            }
            finally
            {
                _running = false;
            }

            ShowResult(title, body);
        }

        private static async Task<(bool ok, string err)> TestOpenRouterModelAsync(string key, string model)
        {
            string payload = "{\"model\":\"" + JsonEscape(model)
                           + "\",\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}],\"max_tokens\":1}";

            using (var req = new HttpRequestMessage(HttpMethod.Post, OpenRouterChatUrl))
            {
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

        private static string JsonEscape(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

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
            if (i >= 0)
            {
                int colon = body.IndexOf(':', i + marker.Length);
                int q1    = colon >= 0 ? body.IndexOf('"', colon + 1) : -1;
                int q2    = q1 >= 0 ? body.IndexOf('"', q1 + 1) : -1;
                if (q1 >= 0 && q2 > q1) return $"HTTP {code}: {body.Substring(q1 + 1, q2 - q1 - 1)}";
            }

            return $"HTTP {code}: {(body.Length > 200 ? body.Substring(0, 200) + "…" : body)}";
        }

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

        private static async Task TestAsync(string provider, string url, string key, string keyPage)
        {
            string title, body;
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
                            body  = $"Your {provider} API key is valid. You're ready to play.";
                        }
                        else
                        {
                            int code = (int) resp.StatusCode;
                            title = "Connection failed";
                            body  = code == 401
                                ? $"Key rejected (401). It may be wrong, disabled, or revoked. Check it at {keyPage}."
                                : $"{provider} returned HTTP {code}. Check your key and your account credits.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                title = "Connection error";
                body  = $"Could not reach {provider}: {ex.Message}";
            }
            finally
            {
                _running = false;
            }

            ShowResult(title, body);
        }

        private static string ResolveKey(string mcmValue, string envVar)
        {
            if (!string.IsNullOrWhiteSpace(mcmValue)) return mcmValue.Trim();
            return Environment.GetEnvironmentVariable(envVar);
        }

        private static void ShowResult(string title, string body)
        {
            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    title, body, true, false, "OK", null, null, null),
                    pauseGameActiveState: false);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[API TEST] result popup failed: {ex.Message}");
                try { MBInformationManager.AddQuickInformation(new TextObject($"{title}: {body}")); }
                catch { /* last resort — nothing more we can do */ }
            }
        }

        #endregion
    }
}
