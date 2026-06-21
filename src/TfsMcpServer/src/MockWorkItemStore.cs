using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TfsMcpServer;

/// <summary>
/// Fully in-memory work item store for testing without a live TFS instance.
/// Pre-seeded with realistic work items across two projects.
/// Supports basic WIQL filtering by TeamProject, WorkItemType, State, and AssignedTo.
/// </summary>
public sealed class MockWorkItemStore : IWorkItemStore
{
    private readonly Lock _lock = new();
    private readonly ILogger<MockWorkItemStore>? _logger;
    private int _nextId = 1001;

    // In-memory store: id → WorkItemData
    private readonly Dictionary<int, WorkItemData> _items;

    // Known work item types per project
    private readonly Dictionary<string, List<string>> _projectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FabrikamFiber"] = ["Bug", "Task", "User Story", "Feature", "Epic", "Test Case"],
        ["AdventureWorks"] = ["Bug", "Task", "Product Backlog Item", "Feature", "Impediment"]
    };

    /// <param name="logger">
    /// Optional — null in unit tests that don't need a DI container.
    /// </param>
    public MockWorkItemStore(ILogger<MockWorkItemStore>? logger = null)
    {
        _logger = logger;
        _items = BuildSeedData();
    }

    // -------------------------------------------------------------------------
    // IWorkItemStore implementation
    // -------------------------------------------------------------------------

    public IReadOnlyList<WorkItemData> Query(string wiql, int maxResults)
    {
        lock (_lock)
        {
            var all = _items.Values.AsEnumerable();

            // Parse a small useful subset of WIQL WHERE clauses so demo queries work.
            all = ApplyWiqlFilters(wiql, all);

            // ORDER BY [System.Id] DESC / ASC
            if (Regex.IsMatch(wiql, @"ORDER\s+BY\s+\[System\.Id\]\s+DESC", RegexOptions.IgnoreCase))
                all = all.OrderByDescending(i => i.Id);
            else
                all = all.OrderBy(i => i.Id);

            return all.Take(maxResults).ToList();
        }
    }

    public WorkItemData GetById(int id)
    {
        lock (_lock)
        {
            return _items.TryGetValue(id, out var wi)
                ? wi
                : throw new KeyNotFoundException($"Work item #{id} does not exist in the mock store.");
        }
    }

    public WorkItemData Create(CreateWorkItemRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Title is required.");

        if (!_projectTypes.ContainsKey(req.Project))
            throw new ArgumentException(
                $"Project '{req.Project}' not found. Known projects: {string.Join(", ", _projectTypes.Keys)}");

        if (!_projectTypes[req.Project].Contains(req.WorkItemType, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Work item type '{req.WorkItemType}' not found in '{req.Project}'. " +
                $"Available: {string.Join(", ", _projectTypes[req.Project])}");

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var id = _nextId++;

            var wi = new WorkItemData
            {
                Id = id,
                Type = req.WorkItemType,
                Project = req.Project,
                Title = req.Title,
                State = string.IsNullOrWhiteSpace(req.State) ? DefaultState(req.WorkItemType) : req.State,
                AssignedTo = req.AssignedTo,
                CreatedBy = "MockUser",
                ChangedBy = "MockUser",
                CreatedDate = now,
                ChangedDate = now,
                Description = req.Description,
                Priority = req.Priority > 0 ? req.Priority : 2,
                Url = $"http://mock-tfs/tfs/{req.Project}/_workitems/edit/{id}"
            };

            wi.Fields = BuildFields(wi);
            _items[id] = wi;
            _logger?.LogInformation("[mock] Created work item #{Id} in {Project}", id, req.Project);
            return wi;
        }
    }

    public WorkItemData Update(int id, Dictionary<string, object?> fields, string comment)
    {
        lock (_lock)
        {
            if (!_items.TryGetValue(id, out var wi))
                throw new KeyNotFoundException($"Work item #{id} does not exist in the mock store.");

            foreach (var (key, value) in fields)
            {
                var strVal = value?.ToString() ?? "";

                // Map common field reference names → properties
                switch (key.ToLowerInvariant())
                {
                    case "system.title":           wi.Title = strVal; break;
                    case "system.state":           wi.State = strVal; break;
                    case "system.assignedto":      wi.AssignedTo = strVal; break;
                    case "system.description":     wi.Description = strVal; break;
                    case "microsoft.vsts.common.priority":
                    case "priority":
                        if (int.TryParse(strVal, out var p)) wi.Priority = p;
                        break;
                    default:
                        // Store in the generic fields bag
                        wi.Fields[key] = value;
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(comment))
                wi.History = comment;

            wi.ChangedBy = "MockUser";
            wi.ChangedDate = DateTime.UtcNow;
            wi.Fields = BuildFields(wi); // refresh fields bag
            return wi;
        }
    }

    public IReadOnlyList<string> ListWorkItemTypes(string project)
    {
        if (!_projectTypes.TryGetValue(project, out var types))
            throw new ArgumentException(
                $"Project '{project}' not found. Known projects: {string.Join(", ", _projectTypes.Keys)}");
        return types;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static IEnumerable<WorkItemData> ApplyWiqlFilters(
        string wiql, IEnumerable<WorkItemData> source)
    {
        // [System.TeamProject] = 'X'
        var projectMatch = Regex.Match(wiql,
            @"\[System\.TeamProject\]\s*=\s*'([^']+)'", RegexOptions.IgnoreCase);
        if (projectMatch.Success)
        {
            var p = projectMatch.Groups[1].Value;
            source = source.Where(i => i.Project.Equals(p, StringComparison.OrdinalIgnoreCase));
        }

        // [System.WorkItemType] = 'X'
        var typeMatch = Regex.Match(wiql,
            @"\[System\.WorkItemType\]\s*=\s*'([^']+)'", RegexOptions.IgnoreCase);
        if (typeMatch.Success)
        {
            var t = typeMatch.Groups[1].Value;
            source = source.Where(i => i.Type.Equals(t, StringComparison.OrdinalIgnoreCase));
        }

        // [System.State] = 'X'
        var stateMatch = Regex.Match(wiql,
            @"\[System\.State\]\s*=\s*'([^']+)'", RegexOptions.IgnoreCase);
        if (stateMatch.Success)
        {
            var s = stateMatch.Groups[1].Value;
            source = source.Where(i => i.State.Equals(s, StringComparison.OrdinalIgnoreCase));
        }

        // [System.State] <> 'X'  (common pattern: "active work")
        var stateNeqMatch = Regex.Match(wiql,
            @"\[System\.State\]\s*<>\s*'([^']+)'", RegexOptions.IgnoreCase);
        if (stateNeqMatch.Success)
        {
            var s = stateNeqMatch.Groups[1].Value;
            source = source.Where(i => !i.State.Equals(s, StringComparison.OrdinalIgnoreCase));
        }

        // [System.AssignedTo] = 'X'
        var assignedMatch = Regex.Match(wiql,
            @"\[System\.AssignedTo\]\s*=\s*'([^']+)'", RegexOptions.IgnoreCase);
        if (assignedMatch.Success)
        {
            var a = assignedMatch.Groups[1].Value;
            source = source.Where(i => i.AssignedTo.Equals(a, StringComparison.OrdinalIgnoreCase));
        }

        return source;
    }

    private static Dictionary<string, object?> BuildFields(WorkItemData wi) => new()
    {
        ["System.Id"] = wi.Id,
        ["System.Title"] = wi.Title,
        ["System.State"] = wi.State,
        ["System.WorkItemType"] = wi.Type,
        ["System.TeamProject"] = wi.Project,
        ["System.AssignedTo"] = wi.AssignedTo,
        ["System.CreatedBy"] = wi.CreatedBy,
        ["System.ChangedBy"] = wi.ChangedBy,
        ["System.CreatedDate"] = wi.CreatedDate,
        ["System.ChangedDate"] = wi.ChangedDate,
        ["System.Description"] = wi.Description,
        ["System.History"] = wi.History,
        ["Microsoft.VSTS.Common.Priority"] = wi.Priority
    };

    private static string DefaultState(string workItemType) => workItemType switch
    {
        "Bug" => "Active",
        "Task" => "To Do",
        "User Story" or "Product Backlog Item" => "New",
        "Feature" or "Epic" => "New",
        _ => "Active"
    };

    // -------------------------------------------------------------------------
    // Seed data — realistic fake work items across two projects
    // -------------------------------------------------------------------------

    private Dictionary<int, WorkItemData> BuildSeedData()
    {
        var now = DateTime.UtcNow;

        var items = new List<WorkItemData>
        {
            // ---- FabrikamFiber bugs ----
            new() {
                Id = 1, Type = "Bug", Project = "FabrikamFiber",
                Title = "Login page crashes on invalid email format",
                State = "Active", AssignedTo = "Alice Johnson", Priority = 1,
                CreatedBy = "Bob Smith", ChangedBy = "Alice Johnson",
                CreatedDate = now.AddDays(-10), ChangedDate = now.AddDays(-2),
                Description = "Entering an email without '@' causes a NullReferenceException in AuthController.cs line 42.",
                Url = "http://mock-tfs/tfs/FabrikamFiber/_workitems/edit/1"
            },
            new() {
                Id = 2, Type = "Bug", Project = "FabrikamFiber",
                Title = "Dashboard widget does not refresh after data update",
                State = "Resolved", AssignedTo = "Carol White", Priority = 2,
                CreatedBy = "Alice Johnson", ChangedBy = "Carol White",
                CreatedDate = now.AddDays(-15), ChangedDate = now.AddDays(-1),
                Description = "The summary widget on the main dashboard shows stale data until the page is manually refreshed.",
                Url = "http://mock-tfs/tfs/FabrikamFiber/_workitems/edit/2"
            },
            new() {
                Id = 3, Type = "Bug", Project = "FabrikamFiber",
                Title = "Export to CSV truncates fields longer than 255 chars",
                State = "Active", AssignedTo = "Bob Smith", Priority = 2,
                CreatedBy = "Carol White", ChangedBy = "Bob Smith",
                CreatedDate = now.AddDays(-5), ChangedDate = now.AddDays(-5),
                Description = "Any field value exceeding 255 characters is silently truncated in the CSV export.",
                Url = "http://mock-tfs/tfs/FabrikamFiber/_workitems/edit/3"
            },

            // ---- FabrikamFiber tasks ----
            new() {
                Id = 4, Type = "Task", Project = "FabrikamFiber",
                Title = "Add unit tests for AuthController",
                State = "To Do", AssignedTo = "Alice Johnson", Priority = 2,
                CreatedBy = "Alice Johnson", ChangedBy = "Alice Johnson",
                CreatedDate = now.AddDays(-3), ChangedDate = now.AddDays(-3),
                Description = "Cover happy path, invalid credentials, and locked account scenarios.",
                Url = "http://mock-tfs/tfs/FabrikamFiber/_workitems/edit/4"
            },
            new() {
                Id = 5, Type = "Task", Project = "FabrikamFiber",
                Title = "Upgrade NuGet packages to latest stable",
                State = "In Progress", AssignedTo = "Bob Smith", Priority = 3,
                CreatedBy = "Bob Smith", ChangedBy = "Bob Smith",
                CreatedDate = now.AddDays(-7), ChangedDate = now.AddDays(-1),
                Url = "http://mock-tfs/tfs/FabrikamFiber/_workitems/edit/5"
            },

            // ---- FabrikamFiber user stories ----
            new() {
                Id = 6, Type = "User Story", Project = "FabrikamFiber",
                Title = "As a user I can reset my password via email",
                State = "New", AssignedTo = "", Priority = 1,
                CreatedBy = "Product Owner", ChangedBy = "Product Owner",
                CreatedDate = now.AddDays(-20), ChangedDate = now.AddDays(-20),
                Description = "Users should receive a secure time-limited reset link to their registered email address.",
                Url = "http://mock-tfs/tfs/FabrikamFiber/_workitems/edit/6"
            },
            new() {
                Id = 7, Type = "User Story", Project = "FabrikamFiber",
                Title = "As an admin I can export all user activity to CSV",
                State = "Active", AssignedTo = "Carol White", Priority = 2,
                CreatedBy = "Product Owner", ChangedBy = "Carol White",
                CreatedDate = now.AddDays(-12), ChangedDate = now.AddDays(-4),
                Url = "http://mock-tfs/tfs/FabrikamFiber/_workitems/edit/7"
            },

            // ---- AdventureWorks project ----
            new() {
                Id = 8, Type = "Bug", Project = "AdventureWorks",
                Title = "Product image fails to load on mobile Safari",
                State = "Active", AssignedTo = "Dave Lee", Priority = 1,
                CreatedBy = "Eve Martinez", ChangedBy = "Dave Lee",
                CreatedDate = now.AddDays(-6), ChangedDate = now.AddDays(-2),
                Description = "Product detail page shows a broken image icon on iOS Safari 16+.",
                Url = "http://mock-tfs/tfs/AdventureWorks/_workitems/edit/8"
            },
            new() {
                Id = 9, Type = "Product Backlog Item", Project = "AdventureWorks",
                Title = "Implement product search with filters",
                State = "New", AssignedTo = "Eve Martinez", Priority = 2,
                CreatedBy = "Product Owner", ChangedBy = "Product Owner",
                CreatedDate = now.AddDays(-25), ChangedDate = now.AddDays(-25),
                Description = "Allow customers to filter search results by category, price range, and rating.",
                Url = "http://mock-tfs/tfs/AdventureWorks/_workitems/edit/9"
            },
            new() {
                Id = 10, Type = "Task", Project = "AdventureWorks",
                Title = "Set up CI pipeline in TFS Build",
                State = "Done", AssignedTo = "Dave Lee", Priority = 2,
                CreatedBy = "Dave Lee", ChangedBy = "Dave Lee",
                CreatedDate = now.AddDays(-30), ChangedDate = now.AddDays(-28),
                Url = "http://mock-tfs/tfs/AdventureWorks/_workitems/edit/10"
            }
        };

        foreach (var wi in items)
            wi.Fields = BuildFields(wi);

        return items.ToDictionary(i => i.Id);
    }
}
