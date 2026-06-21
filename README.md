# TFS 2013 MCP Server (C# / .NET 10)

An [MCP (Model Context Protocol)](https://modelcontextprotocol.io) server that exposes
**TFS 2013 Work Item** operations as tools for AI clients like PostQode.

Includes a **full in-memory mock** so you can test all tools end-to-end without any TFS instance,
plus an **xUnit test suite** covering the mock store, config parsing, and response shaping.

---

## Solution Structure

```
TfsMcpServer.sln
├── src/TfsMcpServer/          — the MCP server (entry point + all source)
├── tests/TfsMcpServer.Tests/  — xUnit test suite
├── global.json                — pinned .NET SDK version
└── .editorconfig               — formatting & naming conventions
```

See [Architecture](#architecture--how-this-works) below for what each file inside `src/TfsMcpServer/`
does and how they fit together — that's documented separately so this structural overview doesn't
go stale every time a file is added.

**Design notes:**
- `WorkItemTools.cs` only orchestrates (call the store → shape via `WorkItemViewModels` → serialize). It doesn't know how to format JSON responses itself.
- `ServiceLocator` is a deliberate, documented exception to constructor injection — the MCP SDK's `[McpServerTool]` attribute requires static methods, which rules out normal DI. Everything else in the project uses constructor injection (`ILogger<T>`, `TfsConnectionFactory`, etc.).
- `AuthMode` is a proper enum, not a raw string — `TfsConfig.ParseAuthMode()` fails fast with a clear error message if `TFS_AUTH_MODE` is misspelled, instead of silently falling through at TFS-connection time.

---

## Exposed MCP Tools

| Tool                | Description                                              |
|---------------------|----------------------------------------------------------|
| `QueryWorkItems`    | Run any WIQL query and return matching items             |
| `GetWorkItem`       | Fetch full details of a work item by ID                  |
| `CreateWorkItem`    | Create a new work item in a project                      |
| `UpdateWorkItem`    | Update fields and/or add a history comment               |
| `ListWorkItemTypes` | Discover available work item types in a project          |

---

## Prerequisites

| Requirement       | Notes                                                          |
|-------------------|------------------------------------------------------------------|
| **.NET 10 SDK**   | https://dotnet.microsoft.com/download/dotnet/10                 |
| **PostQode**      | VS Code extension — https://postqode.ai                         |
| **TFS assemblies**| Only needed for real TFS mode — installed via NuGet (see below) |

`global.json` pins the SDK to `10.0.100`. If your installed SDK is a different patch version,
either install `10.0.100`+ or relax `rollForward` in `global.json`.

---

## Running the Tests

```bash
cd TfsMcpServer
dotnet test
```

The test project always builds the main project in `MockOnly` mode (set automatically via
`<MockOnly>true</MockOnly>` in `TfsMcpServer.Tests.csproj`), so tests run on **any OS** — no
TFS assemblies, no Windows, no live TFS instance required.

**Coverage:** ~40 tests across:
- `MockWorkItemStore` — Query (WIQL filter parsing, ordering, paging), GetById, Create, Update, ListWorkItemTypes
- `TfsConfig.ParseAuthMode` — case-insensitive parsing, defaulting, and the error path for typos
- `WorkItemViewModels` — response shaping for each of the 5 tool outputs

---

## Quick Start — Mock Mode (no TFS required)

### 1. Build options

**Option A — run directly (easier during development):**

No build step needed. PostQode launches and compiles the project on demand.
Skip to step 2 and use the `dotnet run` config snippet.

**Option B — publish first (faster cold start):**

```bash
cd TfsMcpServer/src/TfsMcpServer
dotnet publish -c Release -p:MockOnly=true -o ../../publish
```

### 2. Register in PostQode

Open PostQode → MCP Servers icon → Installed tab → Configure MCP Servers.

Add one of these entries to `postqode_mcp_settings.json`:

**Option A — dotnet run (no prior build needed):**

```json
{
  "mcpServers": {
    "tfs2013": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\TfsMcpServer\\src\\TfsMcpServer\\TfsMcpServer.csproj", "-p:MockOnly=true"],
      "env": {
        "TFS_AUTH_MODE": "mock"
      }
    }
  }
}
```

**Option B — published output (instant start):**

```json
{
  "mcpServers": {
    "tfs2013": {
      "command": "dotnet",
      "args": ["C:\\path\\to\\TfsMcpServer\\publish\\TfsMcpServer.dll"],
      "env": {
        "TFS_AUTH_MODE": "mock"
      }
    }
  }
}
```

> On macOS/Linux replace `C:\\path\\to\\` with the Unix path, e.g. `/home/user/TfsMcpServer/`

### 3. Test in PostQode chat

The mock is pre-seeded with 10 realistic work items across two projects.

**Projects:** `FabrikamFiber` · `AdventureWorks`
**Mock IDs:** 1–10

Try these prompts:

```
"List work item types in FabrikamFiber"
"Find all active bugs in FabrikamFiber"
"Show me full details of work item #1"
"Create a bug in FabrikamFiber titled 'Test the MCP server'"
"Update work item #3 to Resolved and assign to Alice Johnson"
```

---

## Mock Seed Data

| ID | Project        | Type                | Title                                          | State       | Assigned To    |
|----|----------------|---------------------|------------------------------------------------|-------------|----------------|
| 1  | FabrikamFiber  | Bug                 | Login page crashes on invalid email format     | Active      | Alice Johnson  |
| 2  | FabrikamFiber  | Bug                 | Dashboard widget does not refresh after update | Resolved    | Carol White    |
| 3  | FabrikamFiber  | Bug                 | Export to CSV truncates fields > 255 chars     | Active      | Bob Smith      |
| 4  | FabrikamFiber  | Task                | Add unit tests for AuthController              | To Do       | Alice Johnson  |
| 5  | FabrikamFiber  | Task                | Upgrade NuGet packages to latest stable        | In Progress | Bob Smith      |
| 6  | FabrikamFiber  | User Story          | As a user I can reset my password via email    | New         | (unassigned)   |
| 7  | FabrikamFiber  | User Story          | As an admin I can export all user activity     | Active      | Carol White    |
| 8  | AdventureWorks | Bug                 | Product image fails to load on mobile Safari   | Active      | Dave Lee       |
| 9  | AdventureWorks | Product Backlog Item| Implement product search with filters          | New         | Eve Martinez   |
| 10 | AdventureWorks | Task                | Set up CI pipeline in TFS Build                | Done        | Dave Lee       |

The mock supports WIQL filtering by `[System.TeamProject]`, `[System.WorkItemType]`,
`[System.State]` (= and <>), and `[System.AssignedTo]`, plus `ORDER BY [System.Id] ASC/DESC`.

---

## Production Mode — Real TFS 2013

The TFS client assemblies are installed automatically via NuGet — no manual DLL copying required.

### 1. Build for production

```bash
cd TfsMcpServer/src/TfsMcpServer
dotnet publish -c Release -o ../../publish
```

This restores `Microsoft.TeamFoundationServer.ExtendedClient` from NuGet and produces a
Windows-only (`win-x64`) executable.

### 2. Update PostQode config

```json
{
  "mcpServers": {
    "tfs2013": {
      "command": "dotnet",
      "args": ["C:\\path\\to\\TfsMcpServer\\publish\\TfsMcpServer.dll"],
      "env": {
        "TFS_COLLECTION_URL": "http://tfs2013:8080/tfs/DefaultCollection",
        "TFS_AUTH_MODE": "ntlm"
      }
    }
  }
}
```

### Auth modes

| `TFS_AUTH_MODE` | Enum value      | When to use                              | Extra env vars needed              |
|-----------------|-----------------|-------------------------------------------|-------------------------------------|
| `mock`          | `AuthMode.Mock` | Testing without TFS                      | None                               |
| `ntlm`          | `AuthMode.Ntlm` | Domain-joined Windows machine (default)  | Just `TFS_COLLECTION_URL`          |
| `basic`         | `AuthMode.Basic`| Non-domain machine, explicit credentials | + `TFS_USERNAME`, `TFS_PASSWORD`   |
| `pat`           | `AuthMode.Pat`  | TFS 2013 Update 5+ / TFS Service         | + `TFS_PASSWORD` (token only)      |

Values are parsed case-insensitively. An unrecognised value (e.g. a typo like `nmtl`) fails
immediately at startup with a clear error listing the valid options, rather than failing later
inside the TFS connection logic.

---

## Logging

All components log through `ILogger<T>` (standard `Microsoft.Extensions.Logging`), writing to
**stderr** so MCP's JSON-RPC protocol traffic on stdout stays uncorrupted. Default level is
`Information`. To see verbose output during troubleshooting, change `SetMinimumLevel(...)` in
`Program.cs` to `LogLevel.Debug` or `LogLevel.Trace`.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| Server not appearing in PostQode | Check the path in `args` is correct and the `publish` folder exists |
| `TFS_COLLECTION_URL is not set` error | You're not in mock mode — either set the URL or add `TFS_AUTH_MODE=mock` |
| `Unrecognised TFS_AUTH_MODE value` error | Check spelling — valid values are `ntlm`, `basic`, `pat`, `mock` |
| `Project 'X' not found` in mock | Use `FabrikamFiber` or `AdventureWorks` (case-insensitive) |
| Tool call times out | Increase Network Timeout in PostQode's MCP server config panel |
| `Permission Errors` in real TFS mode | Verify `TFS_USERNAME` / `TFS_PASSWORD` or check NTLM domain membership |
| `dotnet test` fails to restore | Confirm SDK `10.0.100`+ is installed (see `global.json`) |

---

## Architecture — How This Works

Think of the server as **4 layers**, like a sandwich. A tool call from PostQode flows down
through them, and the response flows back up.

```
PostQode (the caller)
        │  calls a tool
        ▼
┌─────────────────────────────────────────────────────────┐
│ Layer 1 — WorkItemTools.cs  (the menu)                   │
│ Each [McpServerTool] method: validate input → call the   │
│ store → shape via a view model → serialize. Nothing else.│
└─────────────────────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────────────────────┐
│ Layer 2 — IWorkItemStore  (the contract)                 │
│ Declares what operations exist: Query, GetById, Create,  │
│ Update, ListWorkItemTypes. Says nothing about *how*.      │
└─────────────────────────────────────────────────────────┘
        │                              │
        ▼                              ▼
┌──────────────────────┐   ┌──────────────────────────┐
│ MockWorkItemStore     │   │ TfsWorkItemStore          │
│ Layer 3a — in-memory, │   │ Layer 3b — real TFS, via  │
│ for testing           │   │ the TFS client SDK        │
└──────────────────────┘   └──────────────────────────┘
        │                              │
        └──────────────┬───────────────┘
                        ▼
              both return the same
              WorkItemData shape
                        │
                        ▼
┌─────────────────────────────────────────────────────────┐
│ WorkItemViewModels  (the formatter)                       │
│ Shapes WorkItemData into the JSON response — a brief      │
│ summary for lists, a full dump for single lookups, a      │
│ success message for creates/updates.                      │
└─────────────────────────────────────────────────────────┘
                        │
                        ▼
              JSON string returned to PostQode
```

### The 4 layers, in plain language

**Layer 1 — `WorkItemTools.cs` (the menu)**
This is what PostQode sees. Each method tagged `[McpServerTool]` is one callable action —
`QueryWorkItems`, `CreateWorkItem`, etc. These methods are deliberately dumb: take input → call
the store → hand the result to a view model → return JSON. No business logic lives here.

**Layer 2 — `IWorkItemStore` (the contract)**
This interface says "any work item store must support Query, GetById, Create, Update,
ListWorkItemTypes" — without saying *how*. It's the seam that lets mock and real TFS be
swappable.

**Layer 3 — `MockWorkItemStore` / `TfsWorkItemStore` (the actual work)**
Two different engines that both fulfill the `IWorkItemStore` contract. One fakes data in memory;
the other talks to real TFS. `ServiceLocator` picks one at startup based on `TFS_AUTH_MODE`.

**Back up to `WorkItemViewModels` (the formatter)**
Once you have a `WorkItemData` result, this decides what JSON shape goes back to PostQode — a
brief summary for lists, a full dump for single lookups, a success message for creates.

### What is a "ViewModel"?

A ViewModel is **a translator between "the data we have" and "the shape someone else needs to
see."** In this project: `WorkItemData` is the *full*, internal representation of a work item
(every field). The JSON response is what PostQode actually receives. `WorkItemViewModels` sits
between them and decides, for each tool, which fields matter and how they should be labeled.

Different tools need different views of the same data:

| Tool                | What it needs to show                                        |
|----------------------|---------------------------------------------------------------|
| `QueryWorkItems`     | Just enough to scan a list — id, title, state, assignee       |
| `GetWorkItem`        | Everything — full description, history, all custom fields     |
| `CreateWorkItem`     | Not the item at all — just a success message + new ID         |
| `UpdateWorkItem`     | Success message + which fields changed                        |

If every tool just serialized `WorkItemData` directly, every response would dump all 13+ fields
even when only 5 are needed — bloating the response and burying the signal. By centralizing
shaping in `WorkItemViewModels.cs`, changing what "full detail" means is a one-place edit that
every tool using `Full()` picks up automatically.

The name borrows from the MVVM pattern (Model–View–ViewModel): `WorkItemData` is the **Model**
(the raw truth), the JSON payload is the **View** (what the consumer sees), and
`WorkItemViewModels` is the **ViewModel** (the adapter shaping one into the other).

### Adding a new tool — concrete walkthrough

Say you want to add **`DeleteWorkItem`**. Here's every file you'd touch:

**1. Add the method to `IWorkItemStore.cs`**
```csharp
void Delete(int id);
```

**2. Implement it in both stores**

`MockWorkItemStore.cs`:
```csharp
public void Delete(int id)
{
    lock (_lock)
    {
        if (!_items.Remove(id))
            throw new KeyNotFoundException($"Work item #{id} does not exist in the mock store.");
    }
}
```

`TfsWorkItemStore.cs`:
```csharp
public void Delete(int id)
{
    var store = _factory.GetWorkItemStore();
    var wi = store.GetWorkItem(id);
    wi.State = "Removed"; // TFS work items aren't hard-deleted, typically
    wi.Save();
}
```

**3. Add a response shape to `WorkItemViewModels.cs`**
```csharp
public static object Deleted(int id) => new
{
    success = true,
    id,
    message = $"Work item #{id} deleted successfully."
};
```

**4. Add the tool method to `WorkItemTools.cs`**
```csharp
[McpServerTool, Description("Delete (or remove) a work item by ID.")]
public static string DeleteWorkItem(
    [Description("The numeric ID of the work item to delete.")]
    int id)
{
    Store.Delete(id);
    return JsonSerializer.Serialize(WorkItemViewModels.Deleted(id), JsonOptions.Default);
}
```

**5. Write a test in `tests/`**
A new `MockWorkItemStoreDeleteTests.cs` following the same pattern as the existing ones.

That's it — **5 small, mechanical edits, no architecture decisions.** `Program.cs`,
`ServiceLocator.cs`, and the `.csproj` never need to change for a new *work item* tool, because
the wiring is already generic.

**The one rule that makes this easy:** every tool follows the same shape —
`[Description] → Store.Method() → ViewModel.Shape() → Serialize`. If you ever find yourself
writing business logic *inside* `WorkItemTools.cs` — a loop, an `if` deciding how to format a
date, direct TFS API calls — that's the signal you've broken the pattern. Push it down into the
store (if it's "how do we get the data") or the view model (if it's "how do we present the
data").

### Adding a whole new TFS area (not just a new operation)

If you're adding tools for a different TFS area entirely — Builds, Source Control, Test Plans —
mirror the same 4 layers with new names:

```
IBuildStore.cs         (the contract)
MockBuildStore.cs       (fake implementation)
TfsBuildStore.cs        (real implementation)
BuildViewModels.cs       (response shaping)
BuildTools.cs           (the [McpServerTool] methods)
```

Then one line in `ServiceLocator.cs` to wire up which `IBuildStore` to use — same pattern as
`WorkItemStore`, just a sibling property. `Program.cs` doesn't need to change at all, because
`WithToolsFromAssembly()` auto-discovers any class with `[McpServerToolType]`.
