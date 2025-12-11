# TechnologyStoreAutomation

> An intelligent retail inventory management system with AI-driven trend analysis, lifecycle monitoring, and automated recommendations for technology stores.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-14+-336791)](https://www.postgresql.org/)
[![Windows Forms](https://img.shields.io/badge/Windows%20Forms-Desktop-0078D4)](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/)
[![License](https://img.shields.io/badge/License-See%20LICENSE-green)](LICENSE)

---

## 🎯 Overview

**TechnologyStoreAutomation** is a desktop application designed for technology retailers who need intelligent inventory management. It goes beyond simple stock tracking by analyzing sales trends, monitoring product lifecycles, and generating actionable recommendations—helping store managers make data-driven decisions about reordering, discounting, and discontinuing products.

### Who Is This For?

- **Technology retailers** managing diverse product inventories (phones, laptops, tablets, etc.)
- **Store managers** who need real-time insights into stock levels and sales velocity
- **Operations teams** looking to automate inventory decisions and reduce manual oversight

---

## ✨ Key Features

### 📊 Real-Time Dashboard
A color-coded inventory dashboard that auto-refreshes every 5 minutes, giving you instant visibility into:
- Current stock levels for all products
- Sales velocity and 7-day moving averages
- Runway days (how long until stock runs out)
- AI-generated recommendations for each product

![Dashboard](docs/images/dashboard-preview.png) <!-- Optional: Add screenshot -->

### 📈 Trend Analysis Engine
The **TrendCalculator** analyzes historical sales data to identify patterns:
- **7-day moving average** calculation for smoothed sales velocity
- **Trend direction** detection (Rising, Falling, Stable, Volatile)
- **Trend strength** measurement (-1 to +1 scale)
- **Acceleration detection** to identify momentum shifts
- **Runway prediction** showing days until stockout

### 🤖 AI-Powered Recommendations
The **RecommendationEngine** generates smart, actionable advice:
- **Reorder alerts**: CRITICAL, URGENT, or standard restock notifications
- **Discount suggestions**: Percentage-based discounts for aging inventory
- **Lifecycle transitions**: Automatic suggestions to move products between phases
- **Trend-based adjustments**: Stock level modifications based on sales patterns

### 🔄 Product Lifecycle Management
Automatically categorizes products into three lifecycle phases:

| Phase | Description | Actions |
|-------|-------------|---------|
| **🟢 ACTIVE** | Current products with strong sales | Normal ordering, full pricing |
| **🟡 LEGACY** | Older models with declining demand | Reduced orders, promotional discounts |
| **🔴 OBSOLETE** | End-of-life products | No reorders, liquidation pricing |

### 🕷️ Lifecycle Sentinel (Web Scraping)
The **LifecycleSentinel** automatically monitors manufacturer websites to detect when products become vintage or obsolete:
- Scrapes Apple's vintage/obsolete product lists
- Monitors Google Pixel end-of-life announcements
- Automatically updates product phases in the database
- Runs as a scheduled background job

### ⏰ Background Job Scheduling
Powered by **Hangfire** for reliable job execution:
- **Daily snapshots** (1:00 AM) — Generates sales summaries for analytics
- **Lifecycle audits** (2:00 AM) — Runs manufacturer website checks
- **Weekly cleanup** (Sundays 3:00 AM) — Maintains Hangfire job history

### 👥 Visitor Traffic Prediction
Predicts store visitor counts based on historical sales data:
- Historical traffic analysis (30-day lookback)
- 7-day forward prediction
- Traffic summary for staffing decisions

### 🏥 Health Check System
Built-in diagnostics to ensure system reliability:
- Database connectivity checks
- Response time monitoring
- Degraded/Unhealthy status detection
- Detailed health reports for troubleshooting

### 💾 Caching Layer
**CachedProductRepository** improves performance:
- In-memory caching for frequently accessed data
- Configurable expiration times
- Automatic cache invalidation on data changes

---

## 🏗️ Architecture

```
TechnologyStoreAutomation/
├── 📁 backend/
│   ├── 📁 trendCalculator/        # Core business logic
│   │   ├── TrendCalculator.cs     # Sales trend analysis
│   │   ├── RecommendationEngine.cs # AI recommendations
│   │   ├── LifecycleSentinel.cs   # Web scraping for lifecycle updates
│   │   ├── InventoryManager.cs    # Stock level management
│   │   └── 📁 data/               # Data access layer
│   │       ├── ProductRepository.cs
│   │       ├── IProductRepository.cs
│   │       └── schema.sql         # Database schema
│   ├── 📁 visitorCountPrediction/ # Traffic forecasting
│   ├── BackgroundJobService.cs    # Hangfire job scheduling
│   └── CachedProductRepository.cs # Caching decorator
├── 📁 ui/
│   └── SalesEntryForm.cs          # Sales recording dialog
├── MainForm.cs                    # Main dashboard UI
├── HealthCheckService.cs          # System diagnostics
├── ServiceConfiguration.cs        # DI container setup
└── Program.cs                     # Application entry point
```

### Technology Stack

| Component | Technology |
|-----------|------------|
| **Framework** | .NET 8.0 |
| **UI** | Windows Forms |
| **Database** | PostgreSQL 14+ |
| **ORM** | Dapper (micro-ORM) |
| **Job Scheduling** | Hangfire |
| **Web Scraping** | HtmlAgilityPack |
| **Caching** | Microsoft.Extensions.Caching.Memory |
| **DI Container** | Microsoft.Extensions.DependencyInjection |
| **Logging** | Microsoft.Extensions.Logging |
| **Testing** | xUnit, Moq, Testcontainers |

---

## 🚀 Getting Started

### Prerequisites

- **.NET 8.0 SDK** or later
- **PostgreSQL 14+** database server
- **Windows** (required for Windows Forms UI)

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/TechnologyStoreAutomation.git
cd TechnologyStoreAutomation
```

### 2. Set Up the Database

```bash
# Install PostgreSQL (macOS)
brew install postgresql@14
brew services start postgresql@14

# Create database
createdb techtrend_db

# Run schema creation
psql -d techtrend_db -f TechnologyStoreAutomation/backend/trendCalculator/data/schema.sql
```

The schema creates:
- `products` — Product catalog with lifecycle phases
- `sales_transactions` — Individual sale records
- `inventory_transactions` — Stock movements
- `daily_summaries` — Aggregated daily snapshots
- `lifecycle_audit_log` — Phase change history

### 3. Configure Environment Variables

The application requires database credentials via environment variables (no hardcoded values):

**Option A: Full connection string**
```bash
export DB_CONNECTION_STRING="Host=localhost;Port=5432;Database=techtrend_db;Username=your_user;Password=your_password;"
```

**Option B: Heroku-style DATABASE_URL**
```bash
export DATABASE_URL="postgres://your_user:your_password@localhost:5432/techtrend_db"
```

**Option C: Individual variables**
```bash
export DB_HOST=localhost
export DB_PORT=5432
export DB_NAME=techtrend_db
export DB_USER=your_user
export DB_PASSWORD=your_password
```

### 4. Build and Run

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run --project TechnologyStoreAutomation
```

> **Note**: The UI requires Windows. For cross-platform development, you can build on macOS/Linux and run on a Windows machine.

---

## 🧪 Testing

The project includes comprehensive unit and integration tests:

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Categories

| Test File | Coverage |
|-----------|----------|
| `TrendCalculatorTests.cs` | Trend analysis algorithms |
| `RecommendationEngineTests.cs` | AI recommendation logic |
| `LifecycleSentinelTests.cs` | Web scraping and lifecycle updates |
| `HealthCheckServiceTests.cs` | System diagnostics |
| `VisitorCountPredictorTests.cs` | Traffic prediction |
| `ProductRepositoryTests.cs` | Data access layer |
| `Integration/` | Full database integration tests (using Testcontainers) |

---

## ⚙️ Configuration

### appsettings.json

```json
{
  "Application": {
    "Name": "Technology Store Automation"
  },
  "UI": {
    "WindowWidth": 1200,
    "WindowHeight": 800,
    "RefreshIntervalMs": 300000,
    "ToolbarHeight": 50,
    "StatusBarHeight": 30
  },
  "Caching": {
    "DashboardDataExpirationSeconds": 60,
    "ProductListExpirationSeconds": 300
  },
  "Manufacturers": {
    "Apple": {
      "VintageListUrl": "https://support.apple.com/en-us/102772"
    }
  }
}
```

---

## 📖 Usage Guide

### Recording a Sale

1. Click the **"Record Sale"** button (green) in the toolbar
2. Select a product from the dropdown (shows ACTIVE and LEGACY products)
3. Enter the quantity sold
4. Select the sale date
5. Click **"Save"** — the inventory updates automatically

### Understanding the Dashboard

| Column | Description |
|--------|-------------|
| **Product** | Product name and SKU |
| **Phase** | ACTIVE / LEGACY / OBSOLETE |
| **Stock** | Current inventory count |
| **Velocity** | 7-day average sales per day |
| **Runway** | Days until stockout at current velocity |
| **Trend** | Rising ↑ / Falling ↓ / Stable → / Volatile ⚡ |
| **Recommendation** | AI-generated action item |

### Row Colors

- **White** — ACTIVE products, normal status
- **Orange** — LEGACY products, approaching end-of-life
- **Red** — OBSOLETE products, requires immediate action

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📄 License

See the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [Hangfire](https://www.hangfire.io/) for reliable background job processing
- [Dapper](https://github.com/DapperLib/Dapper) for high-performance data access
- [HtmlAgilityPack](https://html-agility-pack.net/) for web scraping capabilities
- [Testcontainers](https://testcontainers.com/) for integration testing with real PostgreSQL
