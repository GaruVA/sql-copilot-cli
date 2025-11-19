using System;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NL2SQL_CLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     Natural Language to SQL - CLI Test Version            ║");
            Console.WriteLine("║     Testing AI Model Before WinForms Integration          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Initialize services
            var llmService = new LlmInferenceService();
            var sqlService = new SqlQueryService(llmService);

            try
            {
                // Step 1: Load AI Model
                Console.WriteLine("[STEP 1] Loading AI Model...");
                Console.WriteLine("This will take 30-60 seconds on first run...");
                Console.WriteLine();

                var sw = Stopwatch.StartNew();
                await llmService.InitializeAsync();
                sw.Stop();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Model loaded successfully in {sw.Elapsed.TotalSeconds:F1} seconds");
                Console.ResetColor();
                Console.WriteLine();

                // Step 2: Initialize SQL Service (extract schema)
                Console.WriteLine("[STEP 2] Connecting to database and extracting schema...");
                sw.Restart();
                await sqlService.InitializeAsync();
                sw.Stop();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Database connected in {sw.Elapsed.TotalSeconds:F1} seconds");
                Console.ResetColor();
                Console.WriteLine();

                // Step 3: Interactive Query Loop
                Console.WriteLine("[STEP 3] Ready for queries!");
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("Commands:");
                Console.WriteLine("  - Type your question in natural language");
                Console.WriteLine("  - Type 'test' to run automatic test suite");
                Console.WriteLine("  - Type 'exit' or 'quit' to exit");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine();

                // Main loop
                while (true)
                {
                    Console.Write("Query > ");
                    string? input = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(input))
                        continue;

                    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                        input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (input.Equals("test", StringComparison.OrdinalIgnoreCase))
                    {
                        await RunTestSuite(sqlService, llmService);
                        continue;
                    }

                    // Process the query
                    await ProcessQuery(sqlService, llmService, input);
                }

                Console.WriteLine();
                Console.WriteLine("Shutting down...");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ FATAL ERROR: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Stack Trace:");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }
            finally
            {
                llmService?.Dispose();
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task ProcessQuery(SqlQueryService sqlService, LlmInferenceService llmService, string naturalLanguageQuery)
        {
            Console.WriteLine();
            Console.WriteLine("─────────────────────────────────────────────────────────────");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Question: {naturalLanguageQuery}");
            Console.ResetColor();
            Console.WriteLine("─────────────────────────────────────────────────────────────");
            Console.WriteLine();

            try
            {
                llmService.ResetContext();
                
                // Generate response (no execution)
                var response = await sqlService.GenerateResponseAsync(naturalLanguageQuery);
                
                // Display full AI response
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("AI Response:");
                Console.WriteLine("═════════════════════════════════════════════════════════════");
                Console.WriteLine(response.FullAiResponse);
                Console.WriteLine("═════════════════════════════════════════════════════════════");
                Console.ResetColor();
                Console.WriteLine();
                
                // Interactive query execution
                if (response.HasQueries)
                {
                    await ExecuteQueriesInteractively(sqlService, response.ExtractedSqlQueries);
                }
                else
                {
                    Console.WriteLine("(No SQL queries to execute)");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        static async Task ExecuteQueriesInteractively(SqlQueryService sqlService, List<string> queries)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"📊 Found {queries.Count} SQL {(queries.Count == 1 ? "query" : "queries")}.\n");
            Console.ResetColor();
            
            for (int i = 0; i < queries.Count; i++)
            {
                var query = queries[i];
                
                Console.WriteLine($"[Query {i + 1}/{queries.Count}]");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("─────────────────────────────────────────────────────────────");
                Console.WriteLine(query);
                Console.WriteLine("─────────────────────────────────────────────────────────────");
                Console.ResetColor();
                
                Console.Write("Execute? (Press Enter=yes, 'c'=cancel all, 's'=skip): ");
                var input = Console.ReadLine()?.Trim().ToLower() ?? "";
                
                if (input == "c")
                {
                    Console.WriteLine("❌ Cancelled remaining queries.\n");
                    break;
                }
                
                if (input == "s")
                {
                    Console.WriteLine("⏭️  Skipped.\n");
                    continue;
                }
                
                // Execute
                Console.WriteLine("⏳ Executing...");
                var sw = Stopwatch.StartNew();
                
                try
                {
                    var results = await sqlService.ExecuteQueryAsync(query);
                    sw.Stop();
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Completed in {sw.Elapsed.TotalSeconds:F2}s - {results.Rows.Count} rows");
                    Console.ResetColor();
                    Console.WriteLine();
                    
                    if (results.Rows.Count > 0)
                    {
                        DisplayResultsAsTable(results);
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Query failed: {ex.Message}");
                    Console.ResetColor();
                }
                
                Console.WriteLine();
            }
        }

        static void DisplayResultsAsTable(DataTable data, int maxRows = 20)
        {
            if (data.Rows.Count == 0)
                return;

            // Calculate column widths (max 30 chars per column)
            int[] columnWidths = new int[data.Columns.Count];
            for (int i = 0; i < data.Columns.Count; i++)
            {
                columnWidths[i] = data.Columns[i].ColumnName.Length;
                foreach (DataRow row in data.Rows)
                {
                    string value = row[i]?.ToString() ?? "NULL";
                    if (value.Length > columnWidths[i])
                        columnWidths[i] = Math.Min(value.Length, 30); // Cap at 30 chars
                }
                columnWidths[i] = Math.Max(columnWidths[i] + 2, 10); // Minimum 10 chars
            }

            // Print header
            Console.WriteLine("Results:");
            Console.WriteLine("┌" + string.Join("┬", Array.ConvertAll(columnWidths, w => new string('─', w))) + "┐");

            Console.Write("│");
            for (int i = 0; i < data.Columns.Count; i++)
            {
                string header = data.Columns[i].ColumnName;
                if (header.Length > columnWidths[i] - 1)
                    header = header.Substring(0, columnWidths[i] - 4) + "...";
                header = header.PadRight(columnWidths[i] - 1);
                Console.Write($" {header}│");
            }
            Console.WriteLine();

            Console.WriteLine("├" + string.Join("┼", Array.ConvertAll(columnWidths, w => new string('─', w))) + "┤");

            // Print rows
            int rowCount = Math.Min(data.Rows.Count, maxRows);
            for (int r = 0; r < rowCount; r++)
            {
                Console.Write("│");
                for (int c = 0; c < data.Columns.Count; c++)
                {
                    string value = data.Rows[r][c]?.ToString() ?? "NULL";
                    if (value.Length > columnWidths[c] - 1)
                        value = value.Substring(0, columnWidths[c] - 4) + "...";

                    value = value.PadRight(columnWidths[c] - 1);
                    Console.Write($" {value}│");
                }
                Console.WriteLine();
            }

            Console.WriteLine("└" + string.Join("┴", Array.ConvertAll(columnWidths, w => new string('─', w))) + "┘");

            if (data.Rows.Count > maxRows)
            {
                Console.WriteLine($"... and {data.Rows.Count - maxRows} more rows (showing first {maxRows})");
            }

            Console.WriteLine();
        }

        static async Task RunTestSuite(SqlQueryService sqlService, LlmInferenceService llmService)
        {
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              Running Automated Test Suite                  ║");
            Console.WriteLine("║           (Auto-executing all queries)                     ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            var testQueries = new[]
            {
                "How many orders do we have?",
                "What is the total freight cost?",
                "Show me all products with their category names",
                "List orders from last month",
                "What are the top 5 products by revenue?",
                "How many orders does each customer have?",
                "Show products in the Electronics category with more than 100 units in stock"
            };

            int passed = 0;
            int failed = 0;
            int totalQueries = 0;
            var timings = new List<double>();

            for (int i = 0; i < testQueries.Length; i++)
            {
                Console.WriteLine($"[Test {i + 1}/{testQueries.Length}] {testQueries[i]}");
                Console.WriteLine();

                // Reset context before each query
                if (i > 0)
                {
                    llmService.ResetContext();
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    // Generate conversational response
                    var response = await sqlService.GenerateResponseAsync(testQueries[i]);
                    var generationTime = sw.Elapsed.TotalSeconds;
                    timings.Add(generationTime);

                    // Display AI response (truncated)
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    var truncatedResponse = response.FullAiResponse.Length > 150 
                        ? response.FullAiResponse.Substring(0, 150) + "..." 
                        : response.FullAiResponse;
                    Console.WriteLine($"  Response: {truncatedResponse.Replace("\n", " ")}");
                    Console.ResetColor();
                    Console.WriteLine();

                    // Check if queries were extracted
                    if (!response.HasQueries)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  ✗ FAIL: No SQL queries found in response ({generationTime:F2}s)");
                        Console.ResetColor();
                        failed++;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  Found {response.ExtractedSqlQueries.Count} SQL {(response.ExtractedSqlQueries.Count == 1 ? "query" : "queries")}");
                        Console.ResetColor();

                        // Auto-execute all queries
                        int queryPassed = 0;
                        int queryFailed = 0;

                        for (int q = 0; q < response.ExtractedSqlQueries.Count; q++)
                        {
                            var query = response.ExtractedSqlQueries[q];
                            totalQueries++;

                            Console.WriteLine($"  [SQL {q + 1}/{response.ExtractedSqlQueries.Count}]");
                            var queryPreview = query.Replace("\n", " ").Substring(0, Math.Min(60, query.Length));
                            Console.WriteLine($"    {queryPreview}...");

                            var execSw = Stopwatch.StartNew();
                            try
                            {
                                var results = await sqlService.ExecuteQueryAsync(query);
                                execSw.Stop();

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"    ✓ Executed in {execSw.Elapsed.TotalSeconds:F2}s - {results.Rows.Count} rows");
                                Console.ResetColor();
                                queryPassed++;
                            }
                            catch (Exception ex)
                            {
                                execSw.Stop();
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"    ✗ Failed: {ex.Message}");
                                Console.ResetColor();
                                queryFailed++;
                            }
                        }

                        sw.Stop();

                        // Overall test result
                        if (queryFailed == 0)
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"  ✓ PASS ({sw.Elapsed.TotalSeconds:F2}s total) - All {queryPassed} queries executed");
                            Console.ResetColor();
                            passed++;
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"  ✗ FAIL ({sw.Elapsed.TotalSeconds:F2}s total) - {queryFailed}/{response.ExtractedSqlQueries.Count} queries failed");
                            Console.ResetColor();
                            failed++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ ERROR: {ex.Message}");
                    Console.ResetColor();
                    failed++;
                }

                Console.WriteLine();
            }

            // Summary
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Test Results: {passed} passed, {failed} failed");
            Console.WriteLine($"Success Rate: {(passed * 100.0 / testQueries.Length):F1}%");
            Console.WriteLine($"Total SQL Queries Executed: {totalQueries}");
            if (timings.Count > 0)
            {
                var avgTime = timings.Average();
                var minTime = timings.Min();
                var maxTime = timings.Max();
                Console.WriteLine($"Generation Time: Avg={avgTime:F2}s, Min={minTime:F2}s, Max={maxTime:F2}s");
            }
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();
        }
    }
}