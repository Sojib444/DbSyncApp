# DBSync — Database Synchronisation Tool

A Windows desktop app to compare and sync tables between two databases.
Supports **SQL Server** and **PostgreSQL**.

---

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- ODBC Driver 17 for SQL Server (for MSSQL connections)
  → Download: https://learn.microsoft.com/en-us/sql/connect/odbc/download-odbc-driver-for-sql-server

---

## Build & Run

```bash
# 1. Restore packages
cd DBSyncApp
dotnet restore

# 2. Build
dotnet build

# 3. Run
dotnet run
```

Or open `DBSyncApp.csproj` in **Visual Studio 2022** and press F5.

---

## How to Use

### Step 1 — Add a Connection
- Click **+ New** under Source or Target
- Fill in server, port, database, credentials
- Click **Test Connection** to verify
- Click **Save**

Connections are saved automatically to:
`%APPDATA%\DBSyncApp\connections.json`

### Step 2 — Connect
- Select a saved profile from the dropdown
- Click **Connect**
- The table list loads automatically from the source database

### Step 3 — Select Tables
- Tick the tables you want to compare
- Use the search box to filter

### Step 4 — Compute Diff
- Click **⟳ Compute Diff**
- The app compares each selected table between source and target
- Results show in the Activity Log

### Step 5 — Review Changes
- Click **▶ Sync to Target** to open the Review window
- Left panel: tree of changed tables (INSERT / UPDATE / DELETE)
- Right panel: grid of changed rows — uncheck any you don't want
- Click any row to see its SQL in the editor below
- **Edit the SQL** if needed, then click **Apply Edit**
- **Reset** to go back to auto-generated SQL

### Step 6 — Confirm & Sync
- Click **Confirm & Sync Selected**
- Final confirmation dialog
- All selected SQL runs in a single transaction (auto-rollback on error)

---

## Notes

- **Primary keys**: The diff engine auto-detects primary keys.
  If detection fails, it defaults to `id`.
- **Deletes**: Rows that exist in target but NOT in source are reported
  in the diff but not deleted by default (safety measure).
- **PostgreSQL schemas**: Tables are shown as `schema.tablename`.
- **Windows Auth** (SQL Server only): Check the Windows Authentication
  box in the connection dialog — no username/password needed.

---

## Project Structure

```
DBSyncApp/
├── Models/
│   └── Models.cs              # ConnectionProfile, TableDiff, RowDiff
├── Services/
│   ├── DatabaseService.cs     # DB connections, table fetch, SQL exec, diff engine
│   └── ProfileStore.cs        # JSON persistence for saved connections
├── Forms/
│   ├── MainForm.cs            # Main window
│   ├── ConnectionDialog.cs    # Add/edit connection
│   └── DiffReviewForm.cs      # Review + edit SQL before sync
└── Program.cs
```
