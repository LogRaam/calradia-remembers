// Code written by Gabriel Mailhot, 05/06/2026.
// Sprint 16: MCM "Test Connection" button — validates the OpenRouter key without
//            starting a conversation (calls /auth/key, spends no tokens).

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
    ///   Backs the MCM "Test Connection" button. Resolves the current key (MCM field,
    ///   else the env var — mirroring runtime) and probes OpenRouter's lightweight
    ///   <c>/auth/key</c> endpoint, reporting the result in a popup. Self-contained so it
    ///   works from the main-menu options screen, before any campaign is loaded.
    /// </summary>
    public static class ApiKeyTester
    {
        private const string ApiKeyEnvVar = "OPENROUTER_API_KEY";
        private const string AuthUrl      = "https://openrouter.ai/api/v1/auth/key";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private static bool _running;

        public static void Run()
        {
            if (_running) return;

            string key = ResolveKey();
            if (string.IsNullOrWhiteSpace(key))
            {
                ShowResult("No API key", "Paste your OpenRouter key in the field above first.");
                return;
            }

            _running = true;
            MBInformationManager.AddQuickInformation(new TextObject("Testing OpenRouter connection..."));
            _ = TestAsync(key);
        }

        #region private

        private static async Task TestAsync(string key)
        {
            string title, body;
            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, AuthUrl))
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    using (HttpResponseMessage resp = await Http.SendAsync(req).ConfigureAwait(false))
                    {
                        if (resp.IsSuccessStatusCode)
                        {
                            title = "Connection OK";
                            body  = "Your OpenRouter API key is valid. You're ready to play.";
                        }
                        else
                        {
                            int code = (int) resp.StatusCode;
                            title = "Connection failed";
                            body  = code == 401
                                ? "Key rejected (401). It may be wrong, disabled, or revoked. " +
                                  "Check it at openrouter.ai/keys."
                                : $"OpenRouter returned HTTP {code}. Check your key and your account credits.";
                        }
                    }
                }
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

        private static string ResolveKey()
        {
            string mcm = CrSettings.Instance?.ApiKey;
            if (!string.IsNullOrWhiteSpace(mcm)) return mcm.Trim();
            return Environment.GetEnvironmentVariable(ApiKeyEnvVar);
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
