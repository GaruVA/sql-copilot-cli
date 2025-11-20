using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LLama;
using LLama.Common;

namespace NL2SQL_CLI
{
    public class UnifiedLlmService : IDisposable
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private LLamaWeights? _localModel;
        private LLamaContext? _localContext;
        private InteractiveExecutor? _localExecutor;
        
        private string _providerType; // "groq", "ollama", "local"
        private string _apiKey;
        private string _apiUrl;
        private string _modelName;
        private List<string> _conversationHistory = new();

        public string ActiveProviderName { get; private set; }

        public async Task InitializeAsync()
        {
            Console.WriteLine("Select LLM Provider:");
            Console.WriteLine("1. Groq API (Recommended - Fast & Accurate)");
            Console.WriteLine("2. Ollama (Local, Privacy-First)");
            Console.WriteLine("3. Local GGUF Model (Existing SQLCoder)");
            Console.WriteLine();
            Console.Write("Enter choice (1-3): ");
            
            string? choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await InitializeGroqAsync();
                    break;
                case "2":
                    await InitializeOllamaAsync();
                    break;
                case "3":
                default:
                    await InitializeLocalGgufAsync();
                    break;
            }
        }

        private async Task InitializeGroqAsync()
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("Groq API Setup");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            
            // Check environment variable first
            _apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            
            if (string.IsNullOrEmpty(_apiKey))
            {
                Console.WriteLine("Enter your Groq API key:");
                Console.WriteLine("(Get free key at: https://console.groq.com/keys)");
                Console.Write("API Key: ");
                _apiKey = Console.ReadLine()?.Trim();
            }
            else
            {
                Console.WriteLine($"✓ Using API key from GROQ_API_KEY environment variable");
                Console.WriteLine($"  Key preview: {_apiKey.Substring(0, 10)}...");
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new Exception("API key is required for Groq");
            }

            _providerType = "groq";
            _apiUrl = "https://api.groq.com/openai/v1/chat/completions";
            _modelName = "llama-3.3-70b-versatile"; // Fast and accurate for SQL
            ActiveProviderName = "Groq (Llama 3.3 70B)";

            // Test connection
            Console.WriteLine();
            Console.WriteLine("Testing Groq API connection...");
            
            try
            {
                var testResponse = await GenerateResponseAsync("test", maxTokens: 10);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Groq API connected successfully!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Failed to connect to Groq: {ex.Message}");
                Console.ResetColor();
                throw;
            }

            await Task.CompletedTask;
        }

        private async Task InitializeOllamaAsync()
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("Ollama Setup");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            
            Console.Write("Enter Ollama URL (default: http://localhost:11434): ");
            string? url = Console.ReadLine()?.Trim();
            _apiUrl = string.IsNullOrEmpty(url) ? "http://localhost:11434/api/generate" : $"{url}/api/generate";
            
            Console.Write("Enter model name (default: codellama:13b): ");
            string? model = Console.ReadLine()?.Trim();
            _modelName = string.IsNullOrEmpty(model) ? "codellama:13b" : model;
            
            _providerType = "ollama";
            ActiveProviderName = $"Ollama ({_modelName})";

            Console.WriteLine();
            Console.WriteLine("Testing Ollama connection...");
            
            try
            {
                var testResponse = await GenerateResponseAsync("test", maxTokens: 10);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Ollama connected successfully!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Failed to connect to Ollama: {ex.Message}");
                Console.WriteLine("  Make sure Ollama is running: ollama serve");
                Console.ResetColor();
                throw;
            }

            await Task.CompletedTask;
        }

        private async Task InitializeLocalGgufAsync()
        {
            Console.WriteLine();
            Console.WriteLine("Loading local GGUF model...");
            
            string modelPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "sqlcoder-7b.Q4_K_M.gguf"
            );

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Model file not found: {modelPath}");
            }

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 4096,
                GpuLayerCount = 0,
                Threads = (int?)Math.Max(1, Environment.ProcessorCount - 2),
                BatchSize = 512
            };

            _localModel = LLamaWeights.LoadFromFile(parameters);
            _localContext = _localModel.CreateContext(parameters);
            _localExecutor = new InteractiveExecutor(_localContext);

            _providerType = "local";
            ActiveProviderName = "Local GGUF (SQLCoder 7B)";

            await Task.CompletedTask;
        }

        public async Task<string> GenerateResponseAsync(string prompt, int maxTokens = 256)
        {
            switch (_providerType)
            {
                case "groq":
                    return await GenerateGroqResponseAsync(prompt, maxTokens);
                case "ollama":
                    return await GenerateOllamaResponseAsync(prompt, maxTokens);
                case "local":
                    return await GenerateLocalResponseAsync(prompt, maxTokens);
                default:
                    throw new InvalidOperationException("LLM service not initialized");
            }
        }

        private async Task<string> GenerateGroqResponseAsync(string prompt, int maxTokens)
        {
            var requestBody = new
            {
                model = _modelName,
                messages = new[]
                {
                    new { role = "system", content = "You are an expert SQL analyst. Generate accurate T-SQL queries." },
                    new { role = "user", content = prompt }
                },
                max_tokens = maxTokens,
                temperature = 0.1 // Low temperature for consistent SQL generation
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
            request.Content = content;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseBody);
            
            return jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }

        private async Task<string> GenerateOllamaResponseAsync(string prompt, int maxTokens)
        {
            var requestBody = new
            {
                model = _modelName,
                prompt = prompt,
                stream = false,
                options = new
                {
                    num_predict = maxTokens,
                    temperature = 0.1
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseBody);
            
            return jsonDoc.RootElement
                .GetProperty("response")
                .GetString() ?? "";
        }

        private async Task<string> GenerateLocalResponseAsync(string prompt, int maxTokens)
        {
            if (_localExecutor == null)
                throw new InvalidOperationException("Local model not initialized");

            var inferenceParams = new InferenceParams
            {
                MaxTokens = maxTokens,
                AntiPrompts = new[] { "\n\n###", "\n\nUser:", "\n\nHuman:", "\n\nQuestion:" }
            };

            var sb = new StringBuilder();
            await foreach (var text in _localExecutor.InferAsync(prompt, inferenceParams))
            {
                sb.Append(text);
            }

            return sb.ToString();
        }

        public void ResetContext()
        {
            _conversationHistory.Clear();
            
            // For local model, we might want to reset the context
            // but LlamaSharp doesn't provide easy context reset
            // So we just clear our conversation tracking
        }

        public void Dispose()
        {
            _localContext?.Dispose();
            _localModel?.Dispose();
        }
    }
}
