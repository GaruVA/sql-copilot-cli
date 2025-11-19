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

            var sw = Stopwatch.StartNew();

            try
            {
                // Reset context before each interactive query (except first)
                Console.WriteLine("⏳ Preparing inference context...");
                llmService.ResetContext();
                
                Console.WriteLine("⏳ Generating SQL...");
                var result = await sqlService.ProcessNaturalLanguageQueryAsync(naturalLanguageQuery);
                sw.Stop();

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Generated SQL:");
                Console.WriteLine("─────────────────────────────────────────────────────────────");
                Console.WriteLine(result.GeneratedSql);
                Console.WriteLine("─────────────────────────────────────────────────────────────");
                Console.ResetColor();
                Console.WriteLine();

                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Query executed successfully in {sw.Elapsed.TotalSeconds:F2} seconds");
                    Console.WriteLine($"✓ Rows returned: {result.ResultData.Rows.Count}");
                    Console.ResetColor();
                    Console.WriteLine();

                    // Display results
                    if (result.ResultData.Rows.Count > 0)
                    {
                        DisplayResultsAsTable(result.ResultData);
                    }
                    else
                    {
                        Console.WriteLine("(No rows returned)");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Query failed: {result.ErrorMessage}");
                    Console.ResetColor();
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

        static void DisplayResultsAsTable(DataTable data, int maxRows = 20)
        {
            if (data.Rows.Count == 0)
                return;

            // Calculate column widths
            int[] columnWidths = new int[data.Columns.Count];
            for (int i = 0; i < data.Columns.Count; i++)
            {
                columnWidths[i] = data.Columns[i].ColumnName.Length;
                foreach (DataRow row in data.Rows)
                {
                    string value = row[i]?.ToString() ?? "NULL";
                    if (value.Length > columnWidths[i])
                        columnWidths[i] = Math.Min(value.Length, 50); // Cap at 50 chars
                }
                columnWidths[i] += 2; // Padding
            }

            // Print header
            Console.WriteLine("Results:");
            Console.WriteLine("┌" + string.Join("┬", Array.ConvertAll(columnWidths, w => new string('─', w))) + "┐");

            Console.Write("│");
            for (int i = 0; i < data.Columns.Count; i++)
            {
                string header = data.Columns[i].ColumnName.PadRight(columnWidths[i] - 1);
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
                    if (value.Length > 50)
                        value = value.Substring(0, 47) + "...";

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

            for (int i = 0; i < testQueries.Length; i++)
            {
                Console.WriteLine($"[Test {i + 1}/{testQueries.Length}] {testQueries[i]}");

                // Reset context before each query to avoid state issues
                if (i > 0)
                {
                    llmService.ResetContext();
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    var result = await sqlService.ProcessNaturalLanguageQueryAsync(testQueries[i]);
                    sw.Stop();

                    if (result.Success)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  ✓ PASS ({sw.Elapsed.TotalSeconds:F2}s) - {result.ResultData?.Rows.Count ?? 0} rows");
                        Console.ResetColor();
                        Console.WriteLine($"  SQL: {result.GeneratedSql?.Replace("\n", " ").Substring(0, Math.Min(80, result.GeneratedSql?.Length ?? 0)) ?? "N/A"}...");
                        passed++;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  ✗ FAIL: {result.ErrorMessage}");
                        Console.ResetColor();
                        failed++;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ ERROR: {ex.Message}");
                    Console.ResetColor();
                    failed++;
                }

                Console.WriteLine();
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Test Results: {passed} passed, {failed} failed");
            Console.WriteLine($"Success Rate: {(passed * 100.0 / testQueries.Length):F1}%");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();
        }
    }
}