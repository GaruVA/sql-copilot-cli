using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NL2SQL_CLI
{
    public class QueryStep
    {
        public int StepNumber { get; set; }
        public string Explanation { get; set; }
        public string SqlQuery { get; set; }
        public DataTable Results { get; set; }
        public bool WasExecuted { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    public class MultiStepPlan
    {
        public string OriginalQuestion { get; set; }
        public List<QueryStep> Steps { get; set; } = new();
        public string FinalSummary { get; set; }
        public bool IsComplete { get; set; }
    }

    public class MultiStepQueryOrchestrator
    {
        private readonly UnifiedLlmService _llmService;
        private readonly SqlQueryService _sqlService;
        private readonly string _schemaContext;

        public MultiStepQueryOrchestrator(
            UnifiedLlmService llmService, 
            SqlQueryService sqlService,
            string schemaContext)
        {
            _llmService = llmService;
            _sqlService = sqlService;
            _schemaContext = schemaContext;
        }

        public async Task<MultiStepPlan> ExecuteMultiStepQueryAsync(string userQuestion)
        {
            var plan = new MultiStepPlan
            {
                OriginalQuestion = userQuestion
            };

            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         MULTI-STEP REASONING MODE ACTIVATED                ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            int stepNumber = 1;
            var conversationHistory = new List<string>();

            while (true)
            {
                Console.WriteLine($"═══════════════════ STEP {stepNumber} ═══════════════════");
                Console.WriteLine();

                // Build prompt for next step
                string prompt = BuildMultiStepPrompt(
                    userQuestion, 
                    plan.Steps, 
                    conversationHistory,
                    stepNumber
                );

                // Get AI response
                Console.WriteLine("  → AI is planning next step...");
                var sw = Stopwatch.StartNew();
                var aiResponse = await _llmService.GenerateResponseAsync(prompt, maxTokens: 512);
                sw.Stop();
                Console.WriteLine($"  → Generated in {sw.Elapsed.TotalSeconds:F1}s");
                Console.WriteLine();
                
                // Parse AI response
                var parsedStep = ParseAiStepResponse(aiResponse, stepNumber);

                if (parsedStep.IsComplete)
                {
                    plan.FinalSummary = parsedStep.Summary;
                    plan.IsComplete = true;
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Analysis complete!");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine("FINAL SUMMARY");
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine(plan.FinalSummary);
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    break;
                }

                if (parsedStep.SqlQuery == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ AI failed to generate valid query for this step");
                    Console.ResetColor();
                    break;
                }

                // Display step info
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Step {stepNumber} Explanation:");
                Console.WriteLine($"  {parsedStep.Explanation}");
                Console.ResetColor();
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Query #{stepNumber}:");
                Console.WriteLine("─────────────────────────────────────────────────────────────");
                Console.WriteLine(parsedStep.SqlQuery);
                Console.WriteLine("─────────────────────────────────────────────────────────────");
                Console.ResetColor();
                Console.WriteLine();

                // User confirmation
                Console.Write($"Execute Query #{stepNumber}? (Enter=yes, c=cancel all, s=skip step): ");
                var input = Console.ReadLine()?.Trim().ToLower() ?? "";

                if (input == "c")
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("❌ Multi-step analysis cancelled by user");
                    Console.ResetColor();
                    break;
                }

                if (input == "s")
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⏭️  Step skipped - continuing to next step");
                    Console.ResetColor();
                    
                    var skippedStep = new QueryStep
                    {
                        StepNumber = stepNumber,
                        Explanation = parsedStep.Explanation,
                        SqlQuery = parsedStep.SqlQuery,
                        WasExecuted = false
                    };
                    plan.Steps.Add(skippedStep);
                    
                    conversationHistory.Add($"Step {stepNumber} was skipped by user.");
                    stepNumber++;
                    Console.WriteLine();
                    continue;
                }

                // Execute query
                Console.WriteLine("⏳ Executing...");
                sw.Restart();
                
                try
                {
                    var results = await _sqlService.ExecuteQueryAsync(parsedStep.SqlQuery);
                    sw.Stop();

                    var executedStep = new QueryStep
                    {
                        StepNumber = stepNumber,
                        Explanation = parsedStep.Explanation,
                        SqlQuery = parsedStep.SqlQuery,
                        Results = results,
                        WasExecuted = true,
                        ExecutionTime = sw.Elapsed
                    };
                    plan.Steps.Add(executedStep);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Completed in {sw.Elapsed.TotalSeconds:F2}s - {results.Rows.Count} rows returned");
                    Console.ResetColor();
                    Console.WriteLine();

                    // Display results
                    if (results.Rows.Count > 0)
                    {
                        DisplayResultsCompact(results);
                    }
                    else
                    {
                        Console.WriteLine("(No rows returned)");
                    }
                    Console.WriteLine();

                    // Add results to conversation history
                    string resultSummary = GenerateResultSummary(results);
                    conversationHistory.Add($"Step {stepNumber} executed successfully. Results: {resultSummary}");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Query failed: {ex.Message}");
                    Console.ResetColor();
                    
                    var failedStep = new QueryStep
                    {
                        StepNumber = stepNumber,
                        Explanation = parsedStep.Explanation,
                        SqlQuery = parsedStep.SqlQuery,
                        WasExecuted = false
                    };
                    plan.Steps.Add(failedStep);
                    
                    conversationHistory.Add($"Step {stepNumber} failed with error: {ex.Message}");
                }

                stepNumber++;
                Console.WriteLine();

                // Safety limit
                if (stepNumber > 10)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠️  Reached maximum step limit (10). Stopping.");
                    Console.ResetColor();
                    break;
                }
            }

            return plan;
        }

        private string BuildMultiStepPrompt(
            string originalQuestion, 
            List<QueryStep> completedSteps,
            List<string> conversationHistory,
            int nextStepNumber)
        {
            var sb = new StringBuilder();

            sb.AppendLine("### ROLE");
            sb.AppendLine("You are an expert SQL analyst performing multi-step query reasoning.");
            sb.AppendLine("Break complex questions into logical steps. Execute one query at a time.");
            sb.AppendLine();

            sb.AppendLine("### DATABASE SCHEMA");
            sb.AppendLine(_schemaContext);
            sb.AppendLine();

            sb.AppendLine("### ORIGINAL USER QUESTION");
            sb.AppendLine(originalQuestion);
            sb.AppendLine();

            // Show conversation history
            if (conversationHistory.Any())
            {
                sb.AppendLine("### PREVIOUS STEPS COMPLETED");
                foreach (var historyItem in conversationHistory)
                {
                    sb.AppendLine($"- {historyItem}");
                }
                sb.AppendLine();
            }

            // Show previous results data
            if (completedSteps.Any(s => s.WasExecuted && s.Results != null))
            {
                sb.AppendLine("### RESULTS FROM PREVIOUS QUERIES");
                foreach (var step in completedSteps.Where(s => s.WasExecuted && s.Results != null))
                {
                    sb.AppendLine($"Step {step.StepNumber} returned:");
                    sb.AppendLine(GenerateResultSummary(step.Results));
                    sb.AppendLine();
                }
            }

            sb.AppendLine($"### YOUR TASK - STEP {nextStepNumber}");
            sb.AppendLine("Determine the next logical step to answer the user's question.");
            sb.AppendLine();
            sb.AppendLine("You have TWO options:");
            sb.AppendLine();
            sb.AppendLine("OPTION 1: Generate the next SQL query");
            sb.AppendLine("Format:");
            sb.AppendLine("EXPLANATION: [One sentence explaining this step]");
            sb.AppendLine("SQL:");
            sb.AppendLine("[Your T-SQL query here];");
            sb.AppendLine("###");
            sb.AppendLine();
            sb.AppendLine("OPTION 2: If all necessary data has been gathered, provide final summary");
            sb.AppendLine("Format:");
            sb.AppendLine("COMPLETE");
            sb.AppendLine("SUMMARY: [Comprehensive answer to user's original question based on all results]");
            sb.AppendLine("###");
            sb.AppendLine();

            sb.AppendLine("### IMPORTANT RULES");
            sb.AppendLine("1. Use ONLY tables/columns from the schema above");
            sb.AppendLine("2. Use T-SQL syntax (dbo. prefix, GETDATE(), TOP N, DATEADD)");
            sb.AppendLine("3. Reference results from previous steps when building queries");
            sb.AppendLine("4. Each query should build logically on previous results");
            sb.AppendLine("5. When you have enough data to answer the original question, output COMPLETE");
            sb.AppendLine();

            sb.AppendLine("### RESPONSE");
            sb.AppendLine("Provide your response now:");

            return sb.ToString();
        }

        private (bool IsComplete, string Summary, string Explanation, string SqlQuery) ParseAiStepResponse(
            string aiResponse, 
            int stepNumber)
        {
            // Check for completion signal
            if (aiResponse.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                var summaryMatch = Regex.Match(
                    aiResponse, 
                    @"SUMMARY:\s*(.+?)(?=###|$)", 
                    RegexOptions.Singleline | RegexOptions.IgnoreCase
                );

                string summary = summaryMatch.Success 
                    ? summaryMatch.Groups[1].Value.Trim() 
                    : "Analysis complete based on previous steps.";

                return (true, summary, null, null);
            }

            // Parse explanation
            var explanationMatch = Regex.Match(
                aiResponse,
                @"EXPLANATION:\s*(.+?)(?=SQL:|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase
            );

            string explanation = explanationMatch.Success
                ? explanationMatch.Groups[1].Value.Trim()
                : $"Step {stepNumber}";

            // Parse SQL query
            var sqlMatch = Regex.Match(
                aiResponse,
                @"SQL:\s*(.+?);",
                RegexOptions.Singleline | RegexOptions.IgnoreCase
            );

            if (!sqlMatch.Success)
            {
                // Fallback: try to find any SELECT statement
                sqlMatch = Regex.Match(
                    aiResponse,
                    @"(SELECT[\s\S]+?;)",
                    RegexOptions.IgnoreCase
                );
            }

            string sqlQuery = sqlMatch.Success
                ? sqlMatch.Groups[1].Value.Trim().TrimEnd(';').Trim()
                : null;

            return (false, null, explanation, sqlQuery);
        }

        private string GenerateResultSummary(DataTable results)
        {
            if (results == null || results.Rows.Count == 0)
                return "No rows returned.";

            var sb = new StringBuilder();
            sb.AppendLine($"{results.Rows.Count} rows, {results.Columns.Count} columns");
            
            // Include column names
            var columnNames = results.Columns.Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .ToList();
            sb.AppendLine($"Columns: {string.Join(", ", columnNames)}");

            // Include first few rows as examples
            int rowsToShow = Math.Min(3, results.Rows.Count);
            sb.AppendLine("Sample data:");
            
            for (int i = 0; i < rowsToShow; i++)
            {
                var rowValues = results.Rows[i].ItemArray
                    .Select(v => v?.ToString() ?? "NULL")
                    .ToList();
                sb.AppendLine($"  Row {i + 1}: {string.Join(" | ", rowValues)}");
            }

            if (results.Rows.Count > rowsToShow)
            {
                sb.AppendLine($"  ... and {results.Rows.Count - rowsToShow} more rows");
            }

            return sb.ToString();
        }

        private void DisplayResultsCompact(DataTable data, int maxRows = 10)
        {
            if (data.Rows.Count == 0)
                return;

            Console.WriteLine("Results:");
            
            // Calculate column widths
            int[] columnWidths = new int[data.Columns.Count];
            for (int i = 0; i < data.Columns.Count; i++)
            {
                columnWidths[i] = Math.Max(
                    data.Columns[i].ColumnName.Length,
                    data.Rows.Cast<DataRow>()
                        .Take(maxRows)
                        .Max(row => (row[i]?.ToString() ?? "NULL").Length)
                );
                columnWidths[i] = Math.Min(columnWidths[i] + 2, 30);
            }

            // Print header
            Console.WriteLine("┌" + string.Join("┬", columnWidths.Select(w => new string('─', w))) + "┐");
            Console.Write("│");
            for (int i = 0; i < data.Columns.Count; i++)
            {
                string header = data.Columns[i].ColumnName.PadRight(columnWidths[i] - 1);
                if (header.Length > columnWidths[i] - 1)
                    header = header.Substring(0, columnWidths[i] - 4) + "...";
                Console.Write($" {header}│");
            }
            Console.WriteLine();
            Console.WriteLine("├" + string.Join("┼", columnWidths.Select(w => new string('─', w))) + "┤");

            // Print rows
            int rowCount = Math.Min(data.Rows.Count, maxRows);
            for (int r = 0; r < rowCount; r++)
            {
                Console.Write("│");
                for (int c = 0; c < data.Columns.Count; c++)
                {
                    string value = (data.Rows[r][c]?.ToString() ?? "NULL").PadRight(columnWidths[c] - 1);
                    if (value.Length > columnWidths[c] - 1)
                        value = value.Substring(0, columnWidths[c] - 4) + "...";
                    Console.Write($" {value}│");
                }
                Console.WriteLine();
            }

            Console.WriteLine("└" + string.Join("┴", columnWidths.Select(w => new string('─', w))) + "┘");

            if (data.Rows.Count > maxRows)
            {
                Console.WriteLine($"... and {data.Rows.Count - maxRows} more rows");
            }
        }
    }
}
