// Code written by Gabriel Mailhot, 17/05/2026.

#region

using System;
using System.Net.Http;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.LlmClient.OpenRouter;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;

#endregion

namespace CalradiaRemembers.Services
{
   /// <summary>
   ///   Centralizes the construction and lifetime of the LLM stack
   ///   (HTTP client, OpenRouter client, parser, prompt builder, chat service).
   ///   Built once at mod startup; queried by anyone needing LLM access.
   ///   If the OPENROUTER_API_KEY environment variable is missing, <see cref="IsAvailable" />
   ///   returns false and <see cref="ChatService" /> remains null. The mod must
   ///   gracefully degrade in that case (skip LLM features, keep vanilla behavior).
   /// </summary>
   public sealed class LlmServices : IDisposable
   {
      private const string ApiKeyEnvVar = "OPENROUTER_API_KEY";
      private const string DefaultModel = "x-ai/grok-4.3";

      private HttpClient _httpClient;
      public NpcChatService ChatService { get; private set; }

      public bool IsAvailable { get; private set; }
      public ILlmClient LlmClient { get; private set; }
      public string ModelId { get; private set; }

      public void Dispose()
      {
         _httpClient?.Dispose();
         _httpClient = null;
      }

      /// <summary>
      ///   Initializes the LLM stack. Call once at mod startup.
      ///   Returns true on success, false if the API key is missing.
      /// </summary>
      public bool Initialize()
      {
         var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);

         if (string.IsNullOrWhiteSpace(apiKey))
         {
            ModLog.Error($"Environment variable {ApiKeyEnvVar} is not set. LLM features disabled.");
            ModLog.Info($"To enable LLM dialogue, set {ApiKeyEnvVar} to your OpenRouter API key " + "via Windows Environment Variables, then restart the game.");
            IsAvailable = false;

            return false;
         }

         try
         {
            _httpClient = new HttpClient();
            var config = new OpenRouterConfig {ApiKey = apiKey, Model = DefaultModel};

            LlmClient = new OpenRouterClient(_httpClient, config);
            ChatService = new NpcChatService(
               LlmClient,
               new SectionResponseParser(),
               new PromptBuilder());

            ModelId = DefaultModel;
            IsAvailable = true;

            ModLog.Info($"LLM stack initialized with model {DefaultModel}.");

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"Failed to initialize LLM stack: {ex.Message}");
            IsAvailable = false;

            return false;
         }
      }
   }
}