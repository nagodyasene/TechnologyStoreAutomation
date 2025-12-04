# TechnologyStoreAutomation - Trend Watching System

A retail store automation system focused on inventory trend analysis and AI-driven recommendations for technology products.

## 🎯 What This System Does

This is an intelligent inventory management dashboard that:

- **Tracks Product Lifecycles** - Automatically categorizes products as ACTIVE, LEGACY, or OBSOLETE
- **Analyzes Sales Trends** - Calculates 7-day moving averages, trend directions, and velocity
- **Predicts Stock Runway** - Shows how many days until you run out based on current sales velocity
- **Generates AI Recommendations** - Provides actionable advice (reorder, discount, liquidate, etc.)
- **Records Sales** - Simple interface to log daily transactions
- **Real-Time Dashboard** - Auto-refreshes every 5 minutes with color-coded alerts

## 📦 Features Implemented

### ✅ Phase 1: Sales Data Foundation (COMPLETE)
- PostgreSQL database schema with full tables
- Sales transaction recording
- Inventory transaction ledger
- Daily summary snapshots for fast queries

### ✅ Phase 2: Trend Calculator (COMPLETE)
- **TrendCalculator.cs** - Analyzes sales patterns, calculates:
  - 7-day moving average
  - Runway days (stock ÷ daily velocity)
  - Trend direction (Rising, Falling, Stable, Volatile)
  - Trend strength (-1 to 1 scale)
  - Acceleration detection

- **RecommendationEngine.cs** - Generates smart recommendations:
  - Reorder alerts (URGENT, CRITICAL, etc.)
  - Discount suggestions for LEGACY/OBSOLETE
  - Stock level adjustments based on trends
  - Automatic phase transition suggestions

### ✅ Phase 3: Sales Entry UI (COMPLETE)
- **SalesEntryForm** - User-friendly dialog to record sales
  - Product dropdown (shows ACTIVE/LEGACY only)
  - Quantity selector with stock validation
  - Sale date picker
  - Auto-calculates total amount
  - Updates inventory automatically

### ✅ Phase 4: Enhanced Dashboard (COMPLETE)
- **Improved MainForm** with toolbar
- "Record Sale" button (green)
- "Simulate Launch Event" button (blue)
- "Refresh" button
- Color-coded rows (Red=OBSOLETE, Orange=LEGACY, White=ACTIVE)
- Wider columns for recommendations

## 🚀 Setup Instructions

### 1. Database Setup

```bash
# Install PostgreSQL (if not already installed)
brew install postgresql@14

# Start PostgreSQL service
brew services start postgresql@14

# Create database
createdb techtrend_db

# Run schema creation
psql -d techtrend_db -f backend/trendCalculator/data/schema.sql
```

The schema includes:
- `products` table
- `sales_transactions` table
- `inventory_transactions` table
- `daily_summaries` table
- `lifecycle_audit_log` table
- Sample data (8 products with 14 days of sales)

### 2. Build and Run

```bash
# Restore dependencies
dotnet restore

# Build project
dotnet build

# Run (must be done on Windows machine)
# On your Mac, commit and push, then pull on Windows PC
```

## Environment variables (required)

The application requires database credentials provided through environment variables; it will not use any hardcoded credentials or fallbacks. Provide one of the following (order of precedence):

1. `DB_CONNECTION_STRING` — full libpq-style connection string (e.g. `Host=...;Port=...;Database=...;Username=...;Password=...;`).
2. `DATABASE_URL` — Heroku-style URL (e.g. `postgres://user:pass@host:port/dbname`). The app parses this into a libpq string automatically.
3. Individual variables: `DB_HOST` (or `PGHOST`), `DB_NAME` (or `PGDATABASE`), `DB_USER` (or `PGUSER`), `DB_PASSWORD` (or `PGPASSWORD`), and optional `DB_PORT` (or `PGPORT`, default `5432`).

If none of the above are provided the application will fail fast with a clear error message.

Example (macOS / zsh):

```bash
# Export individual vars
export DB_HOST=localhost
export DB_NAME=techtrend_db
export DB_USER=postgres
export DB_PASSWORD=your_password_here
export DB_PORT=5432

# Or provide a full connection string
export DB_CONNECTION_STRING="Host=localhost;Port=5432;Database=techtrend_db;Username=postgres;Password=your_password_here;"

# Or provide a DATABASE_URL
export DATABASE_URL="postgres://postgres:your_password_here@localhost:5432/techtrend_db"
```

Example (Windows PowerShell):

```powershell
# Set individual variables for current session
$env:DB_HOST = "localhost"
$env:DB_NAME = "techtrend_db"
$env:DB_USER = "postgres"
$env:DB_PASSWORD = "your_password_here"
$env:DB_PORT = "5432"

# Or provide full connection string
$env:DB_CONNECTION_STRING = "Host=localhost;Port=5432;Database=techtrend_db;Username=postgres;Password=your_password_here;"

# Or provide DATABASE_URL
$env:DATABASE_URL = "postgres://postgres:your_password_here@localhost:5432/techtrend_db"
```

Testing the configuration locally

- After exporting the env vars, start the app (or build/publish) on your Windows machine and it will read the variables automatically.
- On macOS you can still run `dotnet build` to validate compilation, but running the WinForms UI requires a Windows environment.

Security recommendations

- Do not commit credentials to Git. Add `.env` or other files to `.gitignore` if you keep local copies for convenience.
- For development consider using the .NET Secret Manager (`dotnet user-secrets`) or your OS credential store.
- For production prefer platform-managed secret stores (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault).

If you'd like, I can also add a small `env.sample` file to the repo (gitignored) and a short script to load it for local testing.

## 📊 How to Use

### Recording Sales

1. Click **"📝 Record Sale"** button
2. Select a product from dropdown
3. Enter quantity sold
4. Select sale date (defaults to today)
5. Click **"Record Sale"**
6. Dashboard automatically refreshes

### Understanding Dashboard

| Column | Meaning |
|--------|---------|
| **Product** | Product name |
| **Phase** | ACTIVE (current), LEGACY (superseded), OBSOLETE (discontinued) |
| **Stock** | Current inventory count |
| **7-Day Sales** | Total units sold in last 7 days |
| **Runway (Days)** | Days until stock depletes at current velocity |
| **AI Recommendation** | Actionable advice from recommendation engine |

### Reading Recommendations

- 🚨 **CRITICAL** - Reorder immediately (≤3 days runway)
- ⚠️ **URGENT** - Reorder today (≤7 days)
- 📦 **Reorder recommended** (≤14 days)
- 🚀 **ACCELERATING** - Sales increasing rapidly
- 📈 **TRENDING UP** - Steady growth
- 📉 **DECLINING** - Reduce future orders
- 🔴 **LIQUIDATE** - Clear obsolete stock
- 🟡 **LEGACY** - Discount to clear
- ✅ **Normal** - All good

### Simulating Product Launches

1. Select a product row in the grid
2. Click **"🚀 Simulate Launch Event"**
3. Confirm dialog
4. Product phase changes to LEGACY
5. Recommendation updates to suggest discounts

## 🧠 How the Intelligence Works

### Trend Calculation Logic

```
Daily Sales Average = Sum(Last 7 Days Sales) ÷ 7
Runway Days = Current Stock ÷ Daily Sales Average
Trend Direction = Compare Recent Week vs Previous Week
Trend Strength = (Recent - Previous) ÷ Previous (clamped -1 to 1)
Acceleration = Week1 > Week2 > Week3
```

### Recommendation Logic Priority

1. **Lifecycle overrides** - OBSOLETE/LEGACY always get clearance advice
2. **Critical stock** - Low runway triggers urgent reorder
3. **Trend-based** - Rising trends suggest increasing stock
4. **Falling trends** - Suggest reducing orders

### Example Scenarios

**Scenario 1: Hot Product**
- iPhone 15 Pro: 45 stock, 14 sold/week → Runway = 22 days
- Trend: Rising (25% increase week-over-week)
- Recommendation: "📈 TRENDING UP - Monitor for restock"

**Scenario 2: Dying Legacy Product**
- iPhone 12: 8 stock, 1 sold/week → Runway = 56 days
- Phase: OBSOLETE
- Recommendation: "🔴 LIQUIDATE - Clear remaining stock"

**Scenario 3: Critical Stock**
- Sony WH-1000XM5: 5 stock, 12 sold/week → Runway = 2 days
- Recommendation: "🚨 CRITICAL - Reorder IMMEDIATELY"

## 📁 Project Structure

```
TechnologyStoreAutomation/
├── backend/
│   └── trendCalculator/
│       ├── TrendCalculator.cs          ✅ Core intelligence
│       ├── TrendAnalysis.cs            ✅ Analysis result model
│       ├── RecommendationEngine.cs     ✅ Recommendation generator
│       ├── LifecycleSentinel.cs        (Existing - web scraper)
│       ├── InventoryManager.cs         (Existing)
│       └── data/
│           ├── ProductRepository.cs    ✅ Expanded with sales methods
│           ├── ProductDashboardDto.cs  ✅ Dashboard DTO
│           ├── Product.cs              ✅ Core entity
│           ├── SalesTransaction.cs     ✅ Sales record
│           ├── DailySummary.cs         ✅ Snapshot model
│           └── schema.sql              ✅ Complete database schema
├── ui/
│   └── SalesEntryForm.cs               ✅ Sales recording UI
├── MainForm.cs                         ✅ Enhanced dashboard
├── MainForm.Designer.cs                ✅ Form designer
└── Program.cs                          ✅ Entry point
```

## 🔮 Next Steps (Future Enhancements)

### Week 6-7: Polish & Reporting
- [ ] Add Excel export for dashboard data
- [ ] Create sales trend charts (line graph)
- [ ] Add filter dropdown (show only low stock, only LEGACY, etc.)
- [ ] Product details popup with full sales history graph

### Week 8-9: Advanced Features
- [ ] Automated daily summary generation (scheduled job)
- [ ] Email alerts for critical stock levels
- [ ] Bulk reorder wizard (select multiple products)
- [ ] Price optimization suggestions based on velocity

### Week 10+: Production Readiness
- [ ] Move connection string to `appsettings.json`
- [ ] Add logging framework (Serilog)
- [ ] User authentication/roles
- [ ] Multi-store support
- [ ] REST API for mobile app integration

## 🐛 Troubleshooting

### Database Connection Errors

```
Error: Password authentication failed for user "postgres"
```

**Solution**: Update connection string password in `MainForm.cs`

### Missing Tables Error

```
Error: relation "products" does not exist
```

**Solution**: Run `schema.sql` script in your database

### No Sales Data Showing

**Solution**: 
1. Check that `schema.sql` was executed (includes sample data)
2. Manually record sales using "Record Sale" button
3. Verify sales were recorded: `SELECT * FROM sales_transactions;`

## 📝 License

MIT License - Free to use for personal and commercial projects.

## 👨‍💻 Development

Built with:
- .NET 8.0
- Windows Forms (Desktop)
- PostgreSQL 14+
- Dapper (Micro-ORM)
- Npgsql (PostgreSQL driver)

Developed on macOS using Rider, deployed on Windows.
