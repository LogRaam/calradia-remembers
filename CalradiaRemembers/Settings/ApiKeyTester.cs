// Code written by Gabriel Mailhot, 05/06/2026.
// Sprint 16: MCM "Test Connection" button — validates the OpenRouter key without
//            starting a conversation (calls /auth/key, spends no tokens).
// Updated 09/06/2026: provider-aware — a parallel NanoGPT probe (GET /api/v1/models,
//            same Bearer auth path the real chat calls use) backs the NanoGPT button.

#region

using System;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private const string NanoGptModelsUrl    = "https://nano-gpt.com/api/v1/models";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private static bool _running;

        /// <summary>Tests the OpenRouter key (MCM field → OPENROUTER_API_KEY env var).</summary>
        public static void Run() =>
            Probe("OpenRouter", OpenRouterAuthUrl, ResolveKey(CrSettings.Instance?.ApiKey, OpenRouterEnvVar),
                  "openrouter.ai/keys");

        /// <summary>Tests the NanoGPT key (MCM field → NANOGPT_API_KEY env var).</summary>
        public static void RunNanoGpt() =>
            Probe("NanoGPT", NanoGptModelsUrl, ResolveKey(CrSettings.Instance?.NanoGptApiKey, NanoGptEnvVar),
                  "nano-gpt.com");

        #region private

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
