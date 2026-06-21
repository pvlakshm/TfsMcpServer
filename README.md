# TFS 2013 MCP Server (C# / .NET 10)

An [MCP (Model Context Protocol)](https://modelcontextprotocol.io) server that exposes
**TFS 2013 Work Item** operations as tools for AI clients like PostQode.

Includes a **full in-memory mock** so you can test all tools end-to-end without any TFS instance,
plus an **xUnit test suite** covering the mock store, config parsing, and response shaping.

---

## Solution Structure

```
TfsMcpServer.sln
├── global.json                          # Pins the .NET SDK version
├── .editorconfig                        # Formatting & naming conventions
├── README.md
├── src/
│   └── TfsMcpServer/
│       ├── Program.cs                   # Entry point — config, DI, MCP host
│       ├── TfsMcpServer.csproj          # Project file & NuGet package references
│       └── src/
│           ├── AuthMode.cs              # Ntlm | Basic | Pat | Mock enum
│           ├── IWorkItemStore.cs        # IWorkItemStore interface
│           ├── WorkItemData.cs          # Read model returned by all store operations
│           ├── CreateWorkItemRequest.cs # Write model for creating work items
│           ├── MockWorkItemStore.cs     # In-memory implementation (testing)
│           ├── TfsWorkItemStore.cs      # Real TFS implementation (production)
│           ├── TfsConnectionFactory.cs  # TFS auth & connection management
│           ├── TfsConfig.cs             # Configuration model + AuthMode parser
│           ├── ServiceLocator.cs        # Wires up mock or real store at startup
│           ├── JsonOptions.cs           # Shared JSON serialiser settings
│           ├── WorkItemViewModels.cs    # Response shaping (separate from tool logic)
│           └── WorkItemTools.cs         # The 5 MCP tool definitions (thin orchestration)
└── tests/
    └── TfsMcpServer.Tests/
        ├── TfsMcpServer.Tests.csproj
        ├── MockWorkItemStoreQueryTests.cs
        ├── MockWorkItemStoreGetByIdTests.cs
        ├── MockWorkItemStoreCreateTests.cs
        ├── MockWorkItemStoreUpdateTests.cs
        ├── MockWorkItemStoreListWorkItemTypesTests.cs
        ├── TfsConfigParseAuthModeTests.cs
        └── WorkItemViewModelsTests.cs
```

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

## Extending the Server

To add tools for another TFS area (Builds, Source Control, Test Plans):

1. Add an interface (e.g. `IBuildStore`) following the `IWorkItemStore` pattern, plus mock/real implementations.
2. Add a new tool class (e.g. `BuildTools.cs`) with `[McpServerToolType]` + `[McpServerTool]` methods that only orchestrate — push response shaping into a sibling `BuildViewModels.cs`.
3. Wire the new store into `ServiceLocator.Initialise()`.
4. Add a test project folder mirroring the `MockWorkItemStore*Tests.cs` pattern.

`WithToolsFromAssembly()` in `Program.cs` auto-discovers all `[McpServerToolType]` classes, so no
registration step is needed beyond writing the class.
