using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace NL2SQL_CLI
{
    public class QueryResult
    {
        public bool Success { get; set; }
        public string? GeneratedSql { get; set; }
        public DataTable? ResultData { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public class QueryResponse
    {
        public string FullAiResponse { get; set; } = string.Empty;
        public List<string> ExtractedSqlQueries { get; set; } = new();
        public bool HasQueries => ExtractedSqlQueries.Count > 0;
    }

    public class SqlQueryService
    {
        private readonly LlmInferenceService _llmService;
        private readonly string _connectionString;
        private string? _schemaContext;
        private readonly List<string> _conversationHistory;

        public SqlQueryService(
            LlmInferenceService llmService,
            string connectionString = "Server=localhost\\SQLEXPRESS;Database=SampleDB;Integrated Security=true;TrustServerCertificate=true;")
        {
            _llmService = llmService;
            _connectionString = connectionString;
            _conversationHistory = new List<string>();
        }

        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                Console.WriteLine("  → Testing database connection...");

                try
                {
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        connection.Open();
                        Console.WriteLine($"  → Connected to: {connection.Database}");
                        Console.WriteLine($"  → Server: {connection.DataSource}");
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ Database connection failed!");
                    Console.ResetColor();
                    Console.WriteLine($"  Error: {ex.Message}");
                    Console.WriteLine();
                    Console.WriteLine("Common fixes:");
                    Console.WriteLine("  1. Ensure SQL Server is running");
                    Console.WriteLine("  2. Check connection string in SqlQueryService.cs");
                    Console.WriteLine("  3. Run the database setup script first");
                    Console.WriteLine($"  Current connection string: {_connectionString}");
                    Console.WriteLine();
                    throw;
                }

                Console.WriteLine("  → Extracting database schema...");
                _schemaContext = ExtractDatabaseSchema();

                Console.WriteLine("  → Schema extracted successfully");
                Console.WriteLine($"  → Schema size: {_schemaContext.Length} characters");
            });
        }

        public async Task<QueryResponse> GenerateResponseAsync(string naturalLanguageQuery)
        {
            var response = new QueryResponse();

            try
            {
                Console.WriteLine();
                Console.WriteLine("  → [GENERATE] Building conversational prompt...");

                // Build conversational prompt
                string prompt = BuildConversationalPrompt(naturalLanguageQuery);
                Console.WriteLine($"  → [GENERATE] Prompt size: {prompt.Length} characters");

                // Generate conversational response using LLM
                Console.WriteLine("  → [GENERATE] Calling LLM for conversational response...");
                string llmResponse = await _llmService.GenerateResponseAsync(prompt, maxTokens: 384);
                Console.WriteLine($"  → [GENERATE] Response received: {llmResponse.Length} characters");

                response.FullAiResponse = llmResponse;

                // Extract all SQL queries from response
                Console.WriteLine("  → [GENERATE] Extracting SQL queries from response...");
                response.ExtractedSqlQueries = ExtractAllSqlQueries(llmResponse);
                Console.WriteLine($"  → [GENERATE] Found {response.ExtractedSqlQueries.Count} SQL queries");

                // Update conversation history
                _conversationHistory.Add($"Q: {naturalLanguageQuery}");
                if (response.HasQueries)
                {
                    _conversationHistory.Add($"SQL: {string.Join("; ", response.ExtractedSqlQueries.Take(2))}");
                }

                // Keep only last 4 exchanges (8 entries)
                if (_conversationHistory.Count > 8)
                {
                    _conversationHistory.RemoveRange(0, 2);
                }
            }
            catch (Exception ex)
            {
                response.FullAiResponse = $"Error generating response: {ex.Message}";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  → [GENERATE] EXCEPTION: {ex.Message}");
                Console.ResetColor();
            }

            return response;
        }

        public async Task<DataTable> ExecuteQueryAsync(string sql)
        {
            Console.WriteLine("  → [EXECUTE] Preparing to execute query...");
            
            // Convert to T-SQL if needed
            sql = ConvertToTSql(sql);
            
            // Validate SQL
            if (!ValidateSql(sql, out string validationError))
            {
                throw new InvalidOperationException($"SQL Validation Failed: {validationError}");
            }

            // Add safety limits
            sql = AddSafetyLimits(sql);
            
            // Warn about expensive queries
            if (IsExpensiveQuery(sql))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  ⚠️  Warning: This query may be slow on large tables.");
                Console.ResetColor();
            }

            // Execute SQL
            return await ExecuteSqlQueryAsync(sql);
        }

        // Keep old method for backward compatibility during testing
        public async Task<QueryResult> ProcessNaturalLanguageQueryAsync(string naturalLanguageQuery)
        {
            var sw = Stopwatch.StartNew();
            var result = new QueryResult();

            try
            {
                var response = await GenerateResponseAsync(naturalLanguageQuery);
                
                if (response.HasQueries)
                {
                    result.GeneratedSql = response.ExtractedSqlQueries[0];
                    result.ResultData = await ExecuteQueryAsync(result.GeneratedSql);
                    result.Success = true;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "No SQL queries found in response";
                    result.GeneratedSql = response.FullAiResponse;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.GeneratedSql = result.GeneratedSql ?? "Error occurred during processing";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  → [PROCESS] EXCEPTION: {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                sw.Stop();
                result.ProcessingTime = sw.Elapsed;
            }

            return result;
        }

        private string BuildConversationalPrompt(string naturalLanguageQuery)
        {
            var sb = new StringBuilder();

            sb.AppendLine("### Task");
            sb.AppendLine("You are a helpful database analyst. Answer the user's question with clear explanations.");
            sb.AppendLine("You can generate multiple SQL queries to support your analysis.");
            sb.AppendLine();

            // Database schema - use relevant schema only
            sb.AppendLine("### Database Schema");
            sb.AppendLine(GetRelevantSchema(naturalLanguageQuery));
            sb.AppendLine();

            // Previous context for follow-up questions
            if (_conversationHistory.Count > 0)
            {
                sb.AppendLine("### Previous Context");
                foreach (var entry in _conversationHistory)
                {
                    sb.AppendLine(entry);
                }
                sb.AppendLine();
            }

            // Current question
            sb.AppendLine("### Question");
            sb.AppendLine(naturalLanguageQuery);
            sb.AppendLine();

            // Output instructions
            sb.AppendLine("### Instructions");
            sb.AppendLine("- Provide a helpful response with explanations");
            sb.AppendLine("- Include SQL queries when needed to answer the question");
            sb.AppendLine("- Format each SQL query clearly on its own line(s) ending with semicolon");
            sb.AppendLine("- Use SQL Server T-SQL syntax (GETDATE(), DATEADD(), TOP N, etc.)");
            sb.AppendLine("- Use SELECT statements only (no INSERT, UPDATE, DELETE, DROP)");
            sb.AppendLine();
            sb.AppendLine("### Response:");

            return sb.ToString();
        }

        private string GetRelevantSchema(string userQuestion)
        {
            // Extract table names from schema context
            var tables = new[] { "Orders", "Products", "Customers", "OrderDetails", "Categories", "Suppliers", "Employees" };
            var lowerQuestion = userQuestion.ToLower();
            
            // Find tables mentioned in the question
            var relevantTables = tables.Where(t => 
                lowerQuestion.Contains(t.ToLower()) ||
                lowerQuestion.Contains(t.TrimEnd('s').ToLower()) ||
                lowerQuestion.Contains("order") && t == "Orders" ||
                lowerQuestion.Contains("product") && t == "Products" ||
                lowerQuestion.Contains("customer") && t == "Customers" ||
                lowerQuestion.Contains("categor") && t == "Categories"
            ).ToList();
            
            // If no specific tables found or if query is complex, return full schema
            if (!relevantTables.Any() || lowerQuestion.Contains("all") || lowerQuestion.Contains("analyze"))
            {
                return _schemaContext ?? "";
            }
            
            // Return only relevant table schemas
            var lines = (_schemaContext ?? "").Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new StringBuilder();
            bool include = false;
            
            foreach (var line in lines)
            {
                if (line.StartsWith("Table:"))
                {
                    include = relevantTables.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase));
                }
                
                if (include || line.StartsWith("Relationships:"))
                {
                    result.AppendLine(line);
                }
            }
            
            return result.Length > 0 ? result.ToString() : _schemaContext ?? "";
        }

        private List<string> ExtractAllSqlQueries(string response)
        {
            var queries = new List<string>();
            
            // Save raw response for debugging
            try
            {
                var logDir = Path.Combine(Directory.GetCurrentDirectory(), "llm_logs");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, $"response_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
                File.WriteAllText(logFile, response);
            }
            catch { }
            
            // Find all SELECT statements (including WITH/CTE)
            var pattern = @"((?:WITH[\s\S]*?)?SELECT[\s\S]*?;)";
            var matches = Regex.Matches(response, pattern, RegexOptions.IgnoreCase);
            
            foreach (Match match in matches)
            {
                var query = match.Value.Trim().TrimEnd(';').Trim();
                
                // Validate and clean the query
                if (ValidateSql(query, out _))
                {
                    queries.Add(query);
                }
            }
            
            return queries;
        }

        private string AddSafetyLimits(string sql)
        {
            // If no WHERE clause and no TOP/LIMIT, add TOP 1000
            if (!Regex.IsMatch(sql, @"\bWHERE\b", RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(sql, @"\bTOP\s+\d+\b", RegexOptions.IgnoreCase))
            {
                sql = Regex.Replace(sql, @"^(\s*SELECT)\s+", "$1 TOP 1000 ", RegexOptions.IgnoreCase);
                Console.WriteLine("  → [SAFETY] Added TOP 1000 limit to query without WHERE clause");
            }
            
            return sql;
        }

        private bool IsExpensiveQuery(string sql)
        {
            var expensive = new[]
            {
                // SELECT * without WHERE clause
                @"SELECT\s+\*.*FROM.*(?!WHERE)",
                // Cross joins
                @"CROSS\s+JOIN",
                // Multiple joins without WHERE
                @"JOIN.*JOIN.*(?!WHERE)"
            };
            
            return expensive.Any(pattern => 
                Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline));
        }

        private string ExtractSqlFromResponse(string llmResponse)
        {
            Console.WriteLine("  → [EXTRACT] Starting SQL extraction...");
            Console.WriteLine($"  → [EXTRACT] Response length: {llmResponse.Length} characters");
            Console.WriteLine($"  → [EXTRACT] First 150 chars: {(llmResponse.Length > 150 ? llmResponse.Substring(0, 150) : llmResponse)}");
            // Save raw response for debugging (append)
            try
            {
                var logDir = Path.Combine(Directory.GetCurrentDirectory(), "llm_logs");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, $"response_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
                File.WriteAllText(logFile, llmResponse);
            }
            catch
            {
                // Ignore logging failures
            }

            // Work with a trimmed copy
            string text = (llmResponse ?? string.Empty).Trim();

            // Remove common garbage sequences repeated by some models
            text = Regex.Replace(text, @"(/\*\*\*/;|/\*\*/;|/\*\*/|/\*\*/;)+", string.Empty);

            // Remove leading non-SQL characters (like leading semicolons or stray punctuation)
            text = Regex.Replace(text, @"^[^A-Za-z0-9\(\[]+", string.Empty);

            // If there's a fenced code block, prefer its contents
            var codeBlockMatch = Regex.Match(text, @"```(?:sql)?\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (codeBlockMatch.Success)
            {
                text = codeBlockMatch.Groups[1].Value.Trim();
            }

            // Remove trailing assistant/human markers that sometimes appear
            text = Regex.Replace(text, "(Human:|Assistant:|User:).*$", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();

            // Try to find the first complete SELECT or WITH statement (including multi-line)
            var selectPattern = new Regex(@"((?:WITH[\s\S]*?)?SELECT[\s\S]*?;)", RegexOptions.IgnoreCase);
            var m = selectPattern.Match(text);
            if (m.Success)
            {
                var candidate = m.Groups[1].Value.Trim();
                // Remove trailing semicolon
                candidate = candidate.TrimEnd(';').Trim();
                return candidate;
            }

            // Fallback: find a line that starts with SELECT or WITH and collect until semicolon
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var outLines = new List<string>();
            bool capturing = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (!capturing && (line.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || line.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)))
                {
                    capturing = true;
                }

                if (capturing)
                {
                    outLines.Add(line);
                    if (line.EndsWith(";"))
                        break;
                }
            }

            var finalSql = string.Join("\n", outLines).Trim();
            finalSql = finalSql.TrimEnd(';').Trim();

            // If still empty, return empty string
            return finalSql;
        }

        private string ConvertToTSql(string sql)
        {
            // Convert standard SQL syntax to SQL Server T-SQL syntax
            string result = sql;

            // Replace CURRENT_DATE with GETDATE() - must match word boundaries
            result = Regex.Replace(result, @"\bCURRENT_DATE\b", "GETDATE()", RegexOptions.IgnoreCase);
            
            // Replace CURRENT_TIMESTAMP with GETDATE()
            result = Regex.Replace(result, @"\bCURRENT_TIMESTAMP\b", "GETDATE()", RegexOptions.IgnoreCase);

            // Replace INTERVAL syntax (e.g., "INTERVAL '1 month'" or "INTERVAL '1' DAY")
            // Pattern: INTERVAL 'N' MONTH/DAY/YEAR etc
            result = Regex.Replace(result, @"INTERVAL\s+'(\d+)'\s+(MONTH|DAY|YEAR|HOUR|MINUTE|SECOND|WEEK)", (m) =>
            {
                string num = m.Groups[1].Value;
                string unit = m.Groups[2].Value.ToUpper();
                
                // Convert to DATEADD format: DATEADD(MONTH, 1, GETDATE())
                // Note: The caller will handle the base date reference (usually in a WHERE clause)
                return $"DATEADD({unit}, {num}, GETDATE())";
            }, RegexOptions.IgnoreCase);

            // Remove NULLS FIRST/LAST clauses (not standard in SQL Server, causes syntax errors)
            result = Regex.Replace(result, @"\s+NULLS\s+(FIRST|LAST)\b", "", RegexOptions.IgnoreCase);

            // Replace LIMIT N with TOP N (case insensitive)
            result = Regex.Replace(result, @"\bLIMIT\s+(\d+)\s*;?\s*$", "TOP $1", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            // Replace LIMIT N with TOP N in general positions (be careful with ORDER BY context)
            result = Regex.Replace(result, @"\bLIMIT\s+(\d+)\b", "TOP $1", RegexOptions.IgnoreCase);

            // Move TOP to correct position if needed (after SELECT)
            if (Regex.IsMatch(result, @"SELECT\s+TOP", RegexOptions.IgnoreCase))
            {
                // Already in correct position
            }
            else if (Regex.IsMatch(result, @"SELECT.*TOP", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                // TOP is somewhere else, move it to after SELECT
                result = Regex.Replace(result, @"(SELECT)\s+(.*)?\s+(TOP\s+\d+)", "$1 $3 $2", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            return result;
        }

        private bool ValidateSql(string sql, out string errorMessage)
        {
            errorMessage = null;

            Console.WriteLine("  → [VALIDATE] Starting SQL validation...");
            Console.WriteLine($"  → [VALIDATE] SQL length: {sql.Length}");

            if (string.IsNullOrWhiteSpace(sql))
            {
                errorMessage = "Generated SQL is empty";
                Console.WriteLine($"  → [VALIDATE] FAIL: {errorMessage}");
                return false;
            }

            // Security checks
            string sqlLower = sql.ToLower();

            // Block dangerous operations
            string[] dangerousKeywords = {
                "drop ", "delete ", "insert ", "update ", "truncate ",
                "alter ", "create ", "exec ", "execute ", "sp_", "xp_",
                "grant ", "revoke ", "deny "
            };

            foreach (var keyword in dangerousKeywords)
            {
                if (sqlLower.Contains(keyword))
                {
                    errorMessage = $"SQL contains forbidden operation: {keyword.Trim().ToUpper()}";
                    Console.WriteLine($"  → [VALIDATE] FAIL: {errorMessage}");
                    return false;
                }
            }

            // Must contain SELECT
            if (!sqlLower.Contains("select"))
            {
                errorMessage = "SQL must contain a SELECT statement";
                Console.WriteLine($"  → [VALIDATE] FAIL: {errorMessage}");
                return false;
            }

            // Basic syntax check - balanced parentheses
            int openParens = sql.Count(c => c == '(');
            int closeParens = sql.Count(c => c == ')');
            if (openParens != closeParens)
            {
                errorMessage = $"Mismatched parentheses in SQL (open: {openParens}, close: {closeParens})";
                Console.WriteLine($"  → [VALIDATE] FAIL: {errorMessage}");
                return false;
            }

            Console.WriteLine("  → [VALIDATE] PASS: SQL validation successful");
            return true;
        }

        private async Task<DataTable> ExecuteSqlQueryAsync(string sql)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.CommandTimeout = 30; // 30 second timeout
                var dataTable = new DataTable();

                await connection.OpenAsync();
                using (var adapter = new SqlDataAdapter(command))
                {
                    await Task.Run(() => adapter.Fill(dataTable));
                }

                return dataTable;
            }
        }

        private string ExtractDatabaseSchema()
        {
            var sb = new StringBuilder();

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // Get all tables with columns
                string tablesQuery = @"
                    SELECT 
                        t.TABLE_SCHEMA,
                        t.TABLE_NAME,
                        c.COLUMN_NAME,
                        c.DATA_TYPE,
                        c.CHARACTER_MAXIMUM_LENGTH,
                        c.IS_NULLABLE,
                        CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 'YES' ELSE 'NO' END AS IS_PRIMARY_KEY
                    FROM INFORMATION_SCHEMA.TABLES t
                    INNER JOIN INFORMATION_SCHEMA.COLUMNS c 
                        ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
                    LEFT JOIN (
                        SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                            ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    ) pk ON c.TABLE_NAME = pk.TABLE_NAME 
                        AND c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                        AND c.COLUMN_NAME = pk.COLUMN_NAME
                    WHERE t.TABLE_TYPE = 'BASE TABLE'
                    ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION";

                using (var cmd = new SqlCommand(tablesQuery, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    string currentTable = null;

                    while (reader.Read())
                    {
                        string schema = reader["TABLE_SCHEMA"].ToString();
                        string tableName = reader["TABLE_NAME"].ToString();
                        string fullTableName = $"{schema}.{tableName}";

                        if (currentTable != fullTableName)
                        {
                            if (currentTable != null)
                                sb.AppendLine();

                            sb.AppendLine($"Table: {fullTableName}");
                            currentTable = fullTableName;
                        }

                        string columnName = reader["COLUMN_NAME"].ToString();
                        string dataType = reader["DATA_TYPE"].ToString();
                        string isPk = reader["IS_PRIMARY_KEY"].ToString();
                        string nullable = reader["IS_NULLABLE"].ToString();

                        sb.Append($"  - {columnName} ({dataType}");

                        if (dataType == "varchar" || dataType == "nvarchar" || dataType == "char")
                        {
                            var maxLength = reader["CHARACTER_MAXIMUM_LENGTH"];
                            if (maxLength != DBNull.Value)
                                sb.Append($"({maxLength})");
                        }

                        if (isPk == "YES")
                            sb.Append(", PRIMARY KEY");
                        if (nullable == "NO" && isPk == "NO")
                            sb.Append(", NOT NULL");

                        sb.AppendLine(")");
                    }
                }

                // Get foreign key relationships
                string fkQuery = @"
                    SELECT 
                        fk.name AS FK_NAME,
                        tp.name AS PARENT_TABLE,
                        cp.name AS PARENT_COLUMN,
                        tr.name AS REFERENCED_TABLE,
                        cr.name AS REFERENCED_COLUMN
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                    INNER JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
                    INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
                    INNER JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
                    INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id";

                sb.AppendLine();
                sb.AppendLine("Relationships:");

                using (var cmd = new SqlCommand(fkQuery, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sb.AppendLine($"  - {reader["PARENT_TABLE"]}.{reader["PARENT_COLUMN"]} -> {reader["REFERENCED_TABLE"]}.{reader["REFERENCED_COLUMN"]}");
                    }
                }
            }

            return sb.ToString();
        }
    }
}