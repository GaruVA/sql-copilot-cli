# Multi-Step SQL Reasoning - Usage Guide

## What Is Multi-Step Reasoning?

Instead of generating one SQL query, the AI breaks complex questions into logical steps:
1. Each step generates one SQL query
2. Results from previous queries inform the next query
3. User confirms execution at each step
4. AI synthesizes final answer from all results

## When to Use Multi-Step Mode?

✅ **Use multi-step when your question requires:**
- Multiple dependent queries (query 2 needs results from query 1)
- Complex analysis broken into stages
- Filtering → Aggregation → Ranking chains
- Questions with "then", "after that", "finally"

❌ **Use single-query mode for:**
- Simple lookups ("show all orders")
- Single aggregations ("total revenue")
- Basic JOINs ("products with their categories")

## Example Flow

### Example 1: Customer Analysis Chain
```
Query > multi Find customers who placed more than 5 orders last year, then show their total spending, and finally list their top 3 purchased products.

═══════════════════ STEP 1 ═══════════════════

  → AI is planning next step...
  → Generated in 1.2s

Step 1 Explanation:
  I'll first identify customers with more than 5 orders in the last 12 months.

Query #1:
─────────────────────────────────────────────────────────────
SELECT c.CustomerID, c.CustomerName, COUNT(o.OrderID) AS OrderCount
FROM dbo.Customers c
JOIN dbo.Orders o ON o.CustomerID = c.CustomerID
WHERE o.OrderDate >= DATEADD(YEAR, -1, GETDATE())
GROUP BY c.CustomerID, c.CustomerName
HAVING COUNT(o.OrderID) > 5;
─────────────────────────────────────────────────────────────

Execute Query #1? (Enter=yes, c=cancel all, s=skip step): [ENTER]

⏳ Executing...
✓ Completed in 0.04s - 2 rows returned

Results:
┌──────────────┬────────────────────┬────────────┐
│ CustomerID   │ CustomerName       │ OrderCount │
├──────────────┼────────────────────┼────────────┤
│ 18           │ Alice Brown        │ 8          │
│ 42           │ John Chen          │ 11         │
└──────────────┴────────────────────┴────────────┘

═══════════════════ STEP 2 ═══════════════════

  → AI is planning next step...
  → Generated in 1.5s

Step 2 Explanation:
  I'll calculate total spending for the customers returned in Query 1.

Query #2:
─────────────────────────────────────────────────────────────
SELECT o.CustomerID, SUM(od.UnitPrice * od.Quantity) AS TotalSpending
FROM dbo.Orders o
JOIN dbo.OrderDetails od ON od.OrderID = o.OrderID
WHERE o.CustomerID IN (18, 42)
GROUP BY o.CustomerID;
─────────────────────────────────────────────────────────────

Execute Query #2? (Enter=yes, c=cancel all, s=skip step): [ENTER]

⏳ Executing...
✓ Completed in 0.03s - 2 rows returned

Results:
┌──────────────┬───────────────┐
│ CustomerID   │ TotalSpending │
├──────────────┼───────────────┤
│ 18           │ 12800.50      │
│ 42           │ 17340.00      │
└──────────────┴───────────────┘

═══════════════════ STEP 3 ═══════════════════

  → AI is planning next step...
  → Generated in 1.8s

Step 3 Explanation:
  Now I'll find the top 3 products purchased by these customers.

Query #3:
─────────────────────────────────────────────────────────────
SELECT TOP 3 od.ProductID, p.ProductName, 
       SUM(od.Quantity) AS TotalUnitsPurchased
FROM dbo.Orders o
JOIN dbo.OrderDetails od ON od.OrderID = o.OrderID
JOIN dbo.Products p ON p.ProductID = od.ProductID
WHERE o.CustomerID IN (18, 42)
GROUP BY od.ProductID, p.ProductName
ORDER BY TotalUnitsPurchased DESC;
─────────────────────────────────────────────────────────────

Execute Query #3? (Enter=yes, c=cancel all, s=skip step): [ENTER]

⏳ Executing...
✓ Completed in 0.02s - 3 rows returned

Results:
┌───────────┬──────────────────────┬─────────────────────┐
│ ProductID │ ProductName          │ TotalUnitsPurchased │
├───────────┼──────────────────────┼─────────────────────┤
│ 104       │ Ultra HD Router      │ 44                  │
│ 22        │ Noise Cancelling Set │ 41                  │
│ 9         │ Smart Home Hub       │ 28                  │
└───────────┴──────────────────────┴─────────────────────┘

✓ Analysis complete!

═══════════════════════════════════════════════════════════
FINAL SUMMARY
═══════════════════════════════════════════════════════════
Based on the analysis, we found 2 customers who placed more than 
5 orders in the last year:

1. Alice Brown (CustomerID 18): 8 orders, $12,800.50 total spending
2. John Chen (CustomerID 42): 11 orders, $17,340.00 total spending

Their most frequently purchased products are:
1. Ultra HD Router - 44 units
2. Noise Cancelling Set - 41 units
3. Smart Home Hub - 28 units

These high-value customers show strong preference for electronics 
and tech products.
═══════════════════════════════════════════════════════════
```

## Interactive Controls

At each step:

- **Press Enter**: Execute the query and continue
- **Type 'c'**: Cancel entire multi-step analysis
- **Type 's'**: Skip this step and continue to next

## Command Reference

```bash
# Single-query mode (default)
Query > How many orders do we have?

# Multi-step mode (prefix with "multi")
Query > multi Find high-value customers, calculate their spending, then show their favorite products

# Test suites
Query > test          # Single-query tests
Query > test-multi    # Multi-step reasoning tests

# Exit
Query > exit
```

## Tips for Best Results

### ✅ Good Multi-Step Questions

```
✓ "Find X, then calculate Y, and finally show Z"
✓ "Identify products with low stock, check their recent sales, and recommend reorders"
✓ "Get top customers by revenue, then analyze their purchase patterns"
✓ "Find orders from last month, calculate shipping costs, and identify late deliveries"
```

### ❌ Better as Single Query

```
✗ "Show all customers"
✗ "Count orders"
✗ "List products in Electronics category"
```

## Advanced Example: Inventory Analysis

```
Query > multi Identify products with under 50 units in stock, check their sales in the last 6 months, and determine if we should reorder based on sales velocity.

Step 1: Get low stock products
Step 2: Calculate sales for those products
Step 3: Compare stock vs. sales rate
Step 4: Generate reorder recommendations

Final Summary: Provides actionable reorder list with quantities
```

## How It Works Under the Hood

1. **AI receives your question** + database schema
2. **Plans the analysis** - determines logical steps
3. **Generates Query 1** with explanation
4. **Waits for your approval**
5. **Executes Query 1** locally on your database
6. **Feeds results back to AI** as context
7. **AI generates Query 2** using results from Query 1
8. **Repeats** until analysis is complete
9. **Synthesizes final answer** from all collected data

## Privacy & Data Flow

```
┌─────────────────┐
│   Your Question │  → Sent to AI
└─────────────────┘

┌─────────────────┐
│ Database Schema │  → Sent to AI
└─────────────────┘

┌─────────────────┐
│   SQL Query 1   │  ← AI generates, YOU execute locally
└─────────────────┘

┌─────────────────┐
│   Results 1     │  → Summarized results sent to AI
└─────────────────┘     (e.g., "2 rows: CustomerID 18, 42")

┌─────────────────┐
│   SQL Query 2   │  ← AI generates using Results 1
└─────────────────┘

[Repeat until complete]

┌─────────────────┐
│ Final Summary   │  ← AI synthesizes answer
└─────────────────┘
```

**Key point**: Raw data stays in your database. Only:
- Table/column names
- Query result summaries (row counts, sample data)
- Your questions

...are sent to the AI.

## Limitations

- **Max 10 steps** - Safety limit to prevent infinite loops
- **Manual approval** - Each step requires your confirmation
- **Context window** - Very long analyses may hit token limits
- **Model dependent** - Quality varies by LLM provider

## Troubleshooting

**"AI generated invalid SQL"**
→ Model may not understand schema - try simpler question or better model (Groq/Claude)

**"Step failed to execute"**
→ Check SQL syntax - model might hallucinate columns
→ Results still feed to next step as "Step X failed"

**"AI says COMPLETE too early"**
→ Question may be ambiguous - rephrase to be more explicit
→ Try breaking into smaller multi-step questions

**"Too many steps"**
→ Simplify your question
→ Break into multiple separate multi-step queries

## Performance Tips

- **Groq**: Best for multi-step (fast, accurate)
- **Ollama with good model**: Decent (15b+ recommended)
- **Local GGUF**: Not recommended (too slow, hallucinations)

Each step takes 0.5-3s with Groq, 5-10s with Ollama, 15-30s with local GGUF.
