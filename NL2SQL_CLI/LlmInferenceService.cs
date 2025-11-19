using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LLama;
using LLama.Common;

namespace NL2SQL_CLI
{
    public class LlmInferenceService : IDisposable
    {
        private LLamaWeights? _model;
        private LLamaContext? _context;
        private InteractiveExecutor? _executor;
        private bool _isInitialized;
        private string? _modelPath;  // Store the model path for context reset

        public bool IsInitialized => _isInitialized;

        public void ResetContext()
        {
            // Clear the context for the next query
            if (_context != null)
            {
                Console.WriteLine("  → [DEBUG] Resetting context for next query...");
                _context.Dispose();
                
                if (_model != null && _modelPath != null)
                {
                    var parameters = new ModelParams(_modelPath)
                    {
                        ContextSize = 4096,
                        GpuLayerCount = 0,
                        Threads = 6,
                        BatchSize = 512,
                        UseMemoryLock = true,
                        UseMemorymap = true
                    };
                    
                    _context = _model.CreateContext(parameters);
                    _executor = new InteractiveExecutor(_context);
                    Console.WriteLine("  → [DEBUG] Context reset complete");
                }
            }
        }

        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                Console.WriteLine("  → Looking for model file...");

                // Try multiple common locations
                string[] possiblePaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Models", "sqlcoder-7b.Q4_K_M.gguf"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Models", "mistral-7b-instruct-v0.2.Q4_K_M.gguf"),
                    Path.Combine(Directory.GetCurrentDirectory(), "models", "sqlcoder-7b.Q4_K_M.gguf"),
                    Path.Combine(Directory.GetCurrentDirectory(), "sqlcoder-7b.Q4_K_M.gguf"),
                    // Also check bin/Debug directory
                    Path.Combine(Directory.GetCurrentDirectory(), "bin", "Debug", "net8.0", "sqlcoder-7b.Q4_K_M.gguf"),
                    Path.Combine(Directory.GetCurrentDirectory(), "bin", "Release", "net8.0", "sqlcoder-7b.Q4_K_M.gguf")
                };

                string modelPath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        modelPath = path;
                        Console.WriteLine($"  → Found model: {path}");
                        break;
                    }
                }

                if (modelPath == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  ✗ Model file not found!");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("Please download a GGUF model file:");
                    Console.WriteLine();
                    Console.WriteLine("Option 1: SQLCoder-7B (Recommended for SQL)");
                    Console.WriteLine("  URL: https://huggingface.co/TheBloke/sqlcoder-7B-GGUF");
                    Console.WriteLine("  File: sqlcoder-7B.Q4_K_M.gguf (3.8 GB)");
                    Console.WriteLine();
                    Console.WriteLine("Option 2: Mistral-7B-Instruct (General purpose)");
                    Console.WriteLine("  URL: https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF");
                    Console.WriteLine("  File: mistral-7b-instruct-v0.2.Q4_K_M.gguf (4.1 GB)");
                    Console.WriteLine();
                    Console.WriteLine("Place the file in one of these locations:");
                    foreach (var path in possiblePaths)
                    {
                        Console.WriteLine($"  - {path}");
                    }
                    Console.WriteLine();

                    throw new FileNotFoundException("Model file not found. See instructions above.");
                }

                var fileInfo = new FileInfo(modelPath);
                Console.WriteLine($"  → Model size: {fileInfo.Length / 1024.0 / 1024.0 / 1024.0:F2} GB");
                Console.WriteLine();

                Console.WriteLine("  → Configuring model parameters...");
                var parameters = new ModelParams(modelPath)
                {
                    ContextSize = 4096,      // Context window size
                    GpuLayerCount = 0,       // 0 = CPU only (2GB VRAM insufficient)
                    Threads = 6,             // Use 6 CPU threads (adjust for your i7)
                    BatchSize = 512,         // Batch size for processing
                    UseMemoryLock = true,    // Lock memory pages
                    UseMemorymap = true      // Memory-map the model file
                };

                Console.WriteLine($"  → Context Size: {parameters.ContextSize} tokens");
                Console.WriteLine($"  → CPU Threads: {parameters.Threads}");
                Console.WriteLine($"  → GPU Layers: 0 (CPU mode)");
                Console.WriteLine($"  → Batch Size: {parameters.BatchSize}");
                Console.WriteLine();

                Console.WriteLine("  → Loading model into memory...");
                Console.WriteLine("    (This is the slowest part - 30-60 seconds)");

                _modelPath = modelPath;  // Store the model path for context reset
                _model = LLamaWeights.LoadFromFile(parameters);
                Console.WriteLine("  → Creating inference context...");

                _context = _model.CreateContext(parameters);
                Console.WriteLine("  → Initializing executor...");

                _executor = new InteractiveExecutor(_context);

                _isInitialized = true;
                Console.WriteLine("  → Initialization complete!");
            });
        }

        public async Task<string> GenerateResponseAsync(string prompt, int maxTokens = 384)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("LLM service not initialized");

            if (_executor == null)
                throw new InvalidOperationException("Executor is null - model not properly initialized");

            Console.WriteLine("  → Generating response...\n");
            var startTime = DateTime.Now;

            // Create inference params optimized for conversational responses
            var inferenceParams = new InferenceParams
            {
                MaxTokens = maxTokens,
                AntiPrompts = new[] { "\n\nUser:", "\n\nHuman:", "###END###" }
            };

            var sb = new StringBuilder();
            int tokenCount = 0;
            
            // Stream output in real-time
            Console.ForegroundColor = ConsoleColor.Gray;
            try
            {
                await foreach (var token in _executor.InferAsync(prompt, inferenceParams))
                {
                    Console.Write(token);  // Show user progress
                    sb.Append(token);
                    tokenCount++;
                }
            }
            catch (Exception ex)
            {
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ Error during inference: {ex.Message}");
                Console.ResetColor();
                throw;
            }
            
            Console.ResetColor();
            Console.WriteLine("\n");

            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            Console.WriteLine($"  → {tokenCount} tokens in {elapsed:F2}s ({(tokenCount/elapsed):F1} t/s)");

            return sb.ToString().Trim();
        }

        // Keep old method for backward compatibility
        public async Task<string> GenerateSqlAsync(string prompt, int maxTokens = 512)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("LLM service not initialized");

            if (_executor == null)
                throw new InvalidOperationException("Executor is null - model not properly initialized");

            Console.WriteLine("  → Inference starting...");
            Console.WriteLine($"  → Max tokens: {maxTokens}");
            Console.WriteLine($"  → Prompt length: {prompt.Length} characters");
            var startTime = DateTime.Now;

            var inferenceParams = new InferenceParams
            {
                MaxTokens = maxTokens
            };

            Console.WriteLine("  → InferenceParams created successfully");
            Console.WriteLine($"    - MaxTokens: {inferenceParams.MaxTokens}");

            var sb = new StringBuilder();
            int tokenCount = 0;

            try
            {
                Console.WriteLine("  → Starting inference loop...");
                bool firstToken = true;
                
                await foreach (var text in _executor.InferAsync(prompt, inferenceParams))
                {
                    if (firstToken)
                    {
                        Console.WriteLine("  → Model responding...");
                        firstToken = false;
                    }
                    
                    sb.Append(text);
                    tokenCount++;

                    if (tokenCount % 50 == 0)
                    {
                        Console.Write(".");
                    }
                }
                Console.WriteLine();
                Console.WriteLine($"  → Inference loop completed. Total tokens: {tokenCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ Error during inference: {ex.Message}");
                Console.WriteLine($"  ✗ Stack Trace: {ex.StackTrace}");
                Console.ResetColor();
                throw;
            }

            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            var tokensPerSec = tokenCount > 0 ? tokenCount / elapsed : 0;
            Console.WriteLine($"  → Generated {tokenCount} tokens in {elapsed:F2}s ({tokensPerSec:F1} tokens/sec)");

            var result = sb.ToString().Trim();
            Console.WriteLine($"  → Result length: {result.Length} characters");
            Console.WriteLine($"  → First 100 chars: {(result.Length > 100 ? result.Substring(0, 100) : result)}...");

            return result;
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing LLM resources...");
            _context?.Dispose();
            _model?.Dispose();
        }
    }
}