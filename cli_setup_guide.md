# Natural Language to SQL - CLI Test Version
## Complete Step-by-Step Setup Guide

This CLI version lets you test the AI model functionality before building the full WinForms application.

---

## Prerequisites

### 1. Software Requirements
- **Visual Studio 2022** (Community Edition or higher) OR **Visual Studio Code** with C# extension
- **.NET 8.0 SDK** - Download from: https://dotnet.microsoft.com/download/dotnet/8.0
- **SQL Server** (LocalDB, Express, or Developer Edition)
- **SQL Server Management Studio (SSMS)** or **Azure Data Studio**

### 2. Verify .NET Installation
Open Command Prompt or PowerShell and run:
```bash
dotnet --version
```
Should show: `8.0.x` or higher

---

## Step 1: Download the AI Model

### Option A: SQLCoder-7B (Recommended)
Specifically trained for SQL generation.

1. **Go to:** https://huggingface.co/TheBloke/sqlcoder-7B-GGUF/tree/main
2. **Download file:** `sqlcoder-7B.Q4_K_M.gguf` (3.8 GB)
3. **Alternative if too slow:** `sqlcoder-7B.Q3_K_M.gguf` (2.9 GB)

### Option B: Mistral-7B-Instruct (Alternative)
General-purpose model with good SQL capabilities.

1. **Go to:** https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF/tree/main
2. **Download file:** `mistral-7b-instruct-v0.2.Q4_K_M.gguf` (4.1 GB)

### Where to Place the Model File

**Option 1 (Recommended):** Create a Models folder in your user directory
```
C:\Users\<YourUsername>\Models\sqlcoder-7b-q4_K_M.gguf
```

**Option 2:** In your project directory
```
<YourProjectFolder>\models\sqlcoder-7b-q4_K_M.gguf
```

**The CLI will automatically search these locations:**
1. `C:\Users\<YourUsername>\Models\`
2. `<ProjectFolder>\models\`
3. `<ProjectFolder>\` (current directory)

---

## Step 2: Setup the Database

### 2.1 Ensure SQL Server is Running

**Check if SQL Server is running:**
1. Press `Win + R`, type `services.msc`, press Enter
2. Look for **SQL Server** in the list
3. Status should be **Running**
4. If not running: Right-click → Start

**Common SQL Server service names:**
- `SQL Server (MSSQLSERVER)` - Default instance
- `SQL Server (SQLEXPRESS)` - Express edition

### 2.2 Create the Sample Database

1. **Open SQL Server Management Studio (SSMS)** or **Azure Data Studio**

2. **Connect to your local server:**
   - Server name: `localhost` or `localhost\SQLEXPRESS`
   - Authentication: Windows Authentication

3. **Create the database:**
   - Open a New Query window
   - Copy the entire contents from artifact: **"Sample Database Setup Script"**
   - Press F5 to execute
   - You should see: `Sample database created successfully!`

4. **Verify the tables were created:**
   ```sql
   USE SampleDB;
   SELECT * FROM INFORMATION_SCHEMA.TABLES;
   ```
   You should see 5 tables: Categories, Products, Customers, Orders, OrderDetails

### 2.3 Verify Connection String

The CLI uses this default connection string:
```
Server=localhost;Database=SampleDB;Integrated Security=true;TrustServerCertificate=true;
```

**If you use SQL Server Express, you may need:**
```
Server=localhost\SQLEXPRESS;Database=SampleDB;Integrated Security=true;TrustServerCertificate=true;
```

You can change this in `SqlQueryService.cs` line 24 (we'll do this in the next step).

---

## Step 3: Create the CLI Project

### Method A: Using Visual Studio 2022

1. **Open Visual Studio 2022**

2. **Create New Project**
   - Click "Create a new project"
   - Search for: **Console App**
   - Select: **Console App (.NET)** (not .NET Framework)
   - Click Next

3. **Configure Project**
   - Project name: `NL2SQL_CLI`
   - Location: Choose your preferred folder
   - Framework: **.NET 8.0**
   - Click Create

4. **Replace the .csproj file**
   - In Solution Explorer, right-click on project name
   - Click "Unload Project"
   - Right-click again → "Edit Project File"
   - Replace ALL content with the **"CLI Project File (.csproj)"** artifact
   - Right-click project → "Reload Project"

5. **Install NuGet Packages**
   - Right-click project → "Manage NuGet Packages"
   - Click "Restore" (should auto-restore from .csproj)
   - Or use Package Manager Console:
   ```powershell
   Install-Package LLamaSharp -Version 0.15.0
   Install-Package LLamaSharp.Backend.Cpu -Version 0.15.0
   Install-Package System.Data.SqlClient -Version 4.8.6
   ```

6. **Add Source Files**
   - Delete the default `Program.cs`
   - Right-click project → Add → New Item → Class
   - Create these 3 files and copy code from artifacts:
     - `Program.cs` → Copy from **"CLI Version - Program.cs"**
     - `LlmInferenceService.cs` → Copy from **"CLI Version - LlmInferenceService.cs"**
     - `SqlQueryService.cs` → Copy from **"CLI Version - SqlQueryService.cs"**

### Method B: Using Command Line (dotnet CLI)

1. **Create project folder**
   ```bash
   mkdir NL2SQL_CLI
   cd NL2SQL_CLI
   ```

2. **Create new console project**
   ```bash
   dotnet new console -n NL2SQL_CLI -f net8.0
   cd NL2SQL_CLI
   ```

3. **Replace .csproj file**
   - Open `NL2SQL_CLI.csproj` in a text editor
   - Replace contents with **"CLI Project File (.csproj)"** artifact

4. **Restore packages**
   ```bash
   dotnet restore
   ```

5. **Delete default Program.cs**
   ```bash
   del Program.cs
   ```

6. **Create source files**
   Create these 3 files and copy code from artifacts:
   - `Program.cs`
   - `LlmInferenceService.cs`
   - `SqlQueryService.cs`

---

## Step 4: Configure Connection String (If Needed)

If you're using **SQL Server Express** or a **named instance**, update the connection string:

1. **Open `SqlQueryService.cs`**
2. **Find line 24** (constructor):
   ```csharp
   public SqlQueryService(
       LlmInferenceService llmService, 
       string connectionString = "Server=localhost;Database=SampleDB;Integrated Security=true;TrustServerCertificate=true;")
   ```

3. **Update if needed:**
   ```csharp
   // For SQL Server Express:
   "Server=localhost\\SQLEXPRESS;Database=SampleDB;Integrated Security=true;TrustServerCertificate=true;"
   
   // For named instance:
   "Server=localhost\\INSTANCENAME;Database=SampleDB;Integrated Security=true;TrustServerCertificate=true;"
   
   // For SQL authentication:
   "Server=localhost;Database=SampleDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;"
   ```

---

## Step 5: Build the Project

### Using Visual Studio:
1. Press `Ctrl + Shift + B` (Build Solution)
2. Check Output window for errors
3. Should see: `Build succeeded`

### Using Command Line:
```bash
dotnet build
```

**Fix any compilation errors before proceeding.**

---

## Step 6: Run the CLI Application

### Using Visual Studio:
1. Press `F5` (Start Debugging) or `Ctrl + F5` (Start Without Debugging)

### Using Command Line:
```bash
dotnet run
```

---

## Step 7: What to Expect

### First Launch Output:

```
╔════════════════════════════════════════════════════════════╗
║     Natural Language to SQL - CLI Test Version            ║
║     Testing AI Model Before WinForms Integration          ║
╚════════════════════════════════════════════════════════════╝

[STEP 1] Loading AI Model...
This will take 30-60 seconds on first run...

  → Looking for model file...
  → Found model: C:\Users\YourName\Models\sqlcoder-7b-q4_K_M.gguf
  → Model size: 3.82 GB
  
  → Configuring model parameters...
  → Context Size: 4096 tokens
  → CPU Threads: 6
  → GPU Layers: 0 (CPU mode)
  → Batch Size: 512
  
  → Loading model into memory...
    (This is the slowest part - 30-60 seconds)
  → Creating inference context...
  → Initializing executor...
  → Initialization complete!
✓ Model loaded successfully in 45.3 seconds

[STEP 2] Connecting to database and extracting schema...
  → Testing database connection...
  → Connected to: SampleDB
  → Server: localhost
  → Extracting database schema...
  → Schema extracted successfully
  → Schema size: 2847 characters
✓ Database connected in 0.8 seconds

[STEP 3] Ready for queries!

═══════════════════════════════════════════════════════════
Commands:
  - Type your question in natural language
  - Type 'test' to run automatic test suite
  - Type 'exit' or 'quit' to exit
═══════════════════════════════════════════════════════════

Query > 
```

### Try Your First Query:

```
Query > How many orders do we have?

─────────────────────────────────────────────────────────────
Question: How many orders do we have?
─────────────────────────────────────────────────────────────
⏳ Generating SQL...
  → Prompt size: 3245 characters
  → Inference starting...
..........
  → Generated 48 tokens in 6.32s (7.6 tokens/sec)

Generated SQL:
─────────────────────────────────────────────────────────────
SELECT COUNT(*) AS TotalOrders
FROM dbo.Orders
─────────────────────────────────────────────────────────────

  → Executing SQL query...
✓ Query executed successfully in 8.15 seconds
✓ Rows returned: 1

Results:
┌────────────────┐
│ TotalOrders    │
├────────────────┤
│ 15             │
└────────────────┘

Query > 
```

---

## Step 8: Run the Automated Test Suite

Type `test` at the prompt to run all 7 test queries:

```
Query > test

╔════════════════════════════════════════════════════════════╗
║              Running Automated Test Suite                  ║
╚════════════════════════════════════════════════════════════╝

[Test 1/7] How many orders do we have?
  ✓ PASS (6.45s) - 1 rows
  SQL: SELECT COUNT(*) AS TotalOrders FROM dbo.Orders...

[Test 2/7] What is the total freight cost?
  ✓ PASS (5.89s) - 1 rows
  SQL: SELECT SUM(Freight) AS TotalFreight FROM dbo.Orders...

[Test 3/7] Show me all products with their category names
  ✓ PASS (7.23s) - 12 rows
  SQL: SELECT p.ProductID, p.ProductName, c.CategoryName, p.UnitPrice...

[Test 4/7] List orders from last month
  ✓ PASS (8.12s) - 4 rows
  SQL: SELECT * FROM dbo.Orders WHERE OrderDate >= DATEADD(MONTH, -1...

[Test 5/7] What are the top 5 products by revenue?
  ✓ PASS (9.34s) - 5 rows
  SQL: SELECT TOP 5 p.ProductName, SUM(od.Quantity * od.UnitPrice...

[Test 6/7] How many orders does each customer have?
  ✓ PASS (7.78s) - 8 rows
  SQL: SELECT c.CustomerName, COUNT(o.OrderID) AS OrderCount...

[Test 7/7] Show products in the Electronics category with more than 100 units in stock
  ✓ PASS (6.91s) - 2 rows
  SQL: SELECT p.ProductName, p.UnitPrice, p.UnitsInStock FROM dbo.Products...

═══════════════════════════════════════════════════════════
Test Results: 7 passed, 0 failed
Success Rate: 100.0%
═══════════════════════════════════════════════════════════
```

---

## Step 9: Try More Queries

**Simple queries:**
- `How many products do we have?`
- `Show me all customers`
- `What is the average product price?`

**Date-based queries:**
- `Show orders from this month`
- `List all orders from the last 30 days`

**Complex queries:**
- `What are total sales by category?`
- `Show me the top 10 customers by order count`
- `List products with low stock (less than 50 units)`

**Follow-up questions:**
- First: `Show me all electronics products`
- Then: `Now show only those with low stock`

---

## Troubleshooting

### Issue 1: "Model file not found"

**Solution:**
1. Verify you downloaded the model file
2. Check the file is in one of these locations:
   - `C:\Users\<YourUsername>\Models\sqlcoder-7b-q4_K_M.gguf`
   - `<ProjectFolder>\models\sqlcoder-7b-q4_K_M.gguf`
   - `<ProjectFolder>\sqlcoder-7b-q4_K_M.gguf`
3. Check the filename matches exactly (case-sensitive on some systems)

### Issue 2: "Database connection failed"

**Common fixes:**

1. **SQL Server not running:**
   - Press `Win + R`, type `services.msc`
   - Find "SQL Server" service
   - Right-click → Start

2. **Wrong connection string:**
   - For SQL Express: Add `\SQLEXPRESS` to server name
   - Update line 24 in `SqlQueryService.cs`

3. **Database not created:**
   - Open SSMS
   - Run the database setup script
   - Verify `SampleDB` database exists

4. **Test connection in SSMS first:**
   ```sql
   USE SampleDB;
   SELECT * FROM dbo.Orders;
   ```

### Issue 3: "Out of memory" or crashes

**Solutions:**
1. Close other applications
2. Use smaller model (Q3_K_M variant)
3. Reduce context size in `LlmInferenceService.cs` line 59:
   ```csharp
   ContextSize = 2048  // Reduced from 4096
   ```

### Issue 4: Very slow inference (>30 seconds per query)

**Solutions:**
1. Increase CPU threads in `LlmInferenceService.cs` line 61:
   ```csharp
   Threads = 8  // Increase from 6
   ```
2. Use faster Q3_K_M model variant
3. Close background applications
4. Reduce batch size if needed

### Issue 5: Generated SQL is incorrect

**This is normal during testing:**
- AI models have ~80-85% accuracy
- Complex queries may need prompt refinement
- Note which queries fail for later optimization
- Try rephrasing your question

### Issue 6: "Package restore failed"

**Solution:**
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore again
dotnet restore
```

### Issue 7: Build errors about LLama namespace

**Solution:**
1. Verify packages are installed:
   ```bash
   dotnet list package
   ```
   Should show:
   - LLamaSharp (0.15.0)
   - LLamaSharp.Backend.Cpu (0.15.0)
   - System.Data.SqlClient (4.8.6)

2. Clean and rebuild:
   ```bash
   dotnet clean
   dotnet restore
   dotnet build
   ```

---

## Performance Benchmarks (Expected)

### First Query (includes model warm-up):
- **Model inference:** 8-12 seconds
- **SQL execution:** 0.5-1 second
- **Total:** 10-15 seconds

### Subsequent Queries:
- **Model inference:** 4-7 seconds
- **SQL execution:** 0.5-1 second
- **Total:** 5-8 seconds

### Memory Usage:
- **Before model load:** ~100 MB
- **After model load:** ~4-6 GB
- **During inference:** ~5-7 GB
- **Peak:** Should stay under 8 GB

### Token Generation Speed:
- **Expected:** 5-15 tokens/second (CPU mode)
- **Depends on:** CPU speed, thread count, model size

---

## Success Criteria

Your CLI test is successful if:

✅ **Model loads without errors** (30-60 seconds)  
✅ **Database connection works**  
✅ **First query completes** (<15 seconds)  
✅ **Generated SQL is valid**  
✅ **Results are returned correctly**  
✅ **Test suite passes** (>80% queries succeed)  
✅ **No crashes or memory errors**  
✅ **Follow-up questions work** (context awareness)

---

## What to Test

### ✅ Test Checklist

**Basic Functionality:**
- [ ] Model loads successfully
- [ ] Database connection works
- [ ] Simple aggregation query (COUNT, SUM, AVG)
- [ ] Basic SELECT query
- [ ] Query with WHERE clause

**Join Queries:**
- [ ] Two-table join
- [ ] Three-table join
- [ ] Join with aggregation

**Date Handling:**
- [ ] Last month filter
- [ ] Last 30 days filter
- [ ] Year-to-date query

**Complex Queries:**
- [ ] GROUP BY with multiple columns
- [ ] TOP N queries
- [ ] Queries with HAVING clause

**Context Awareness:**
- [ ] Follow-up question that references previous query
- [ ] Multiple follow-up questions

**Error Handling:**
- [ ] Invalid query (should generate error message)
- [ ] SQL with forbidden keywords (should be blocked)

---

## Next Steps After CLI Test

Once your CLI version works:

1. **✅ Verify AI model works correctly**
2. **✅ Confirm database connectivity**
3. **✅ Validate SQL generation accuracy**
4. **✅ Measure performance metrics**

Then you can:

5. **→ Proceed to WinForms integration** with confidence
6. **→ Use the same LlmInferenceService and SqlQueryService classes**
7. **→ Just add UI layer** (forms, buttons, grids)
8. **→ No changes needed** to core logic

---

## Quick Reference: Common Commands

```bash
# Build project
dotnet build

# Run application
dotnet run

# Clean and rebuild
dotnet clean
dotnet build

# Restore packages
dotnet restore

# Check .NET version
dotnet --version

# List installed packages
dotnet list package
```

---

## File Checklist

Make sure you have all these files:

```
NL2SQL_CLI/
├── NL2SQL_CLI.csproj          ✓ From artifact
├── Program.cs                 ✓ From artifact  
├── LlmInferenceService.cs     ✓ From artifact
├── SqlQueryService.cs         ✓ From artifact
└── Models/                    ✓ Create this folder
    └── sqlcoder-7b-q4_K_M.gguf  ✓ Download from HuggingFace
```

---

## Expected First Run Timeline

- **Download model:** 10-20 minutes (one-time, depends on internet speed)
- **Setup database:** 5 minutes
- **Create project:** 5 minutes
- **First build:** 2 minutes
- **First run:** 30-60 seconds (model load)
- **First query:** 10-15 seconds
- **Total:** ~45-60 minutes

---

## Keyboard Shortcuts (While Running)

- **Ctrl + C** - Stop the application
- **Type 'test'** - Run automated test suite
- **Type 'exit' or 'quit'** - Clean shutdown

---

**You're ready to build! Start with Step 1 (download the model) and work through each step sequentially.**

**Good luck! 🚀**