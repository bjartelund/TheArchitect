# TheArchitect – Agent Instructions

This file provides context for AI coding agents (GitHub Copilot, etc.) working on this repository.

---

## Project overview

**TheArchitect** is a .NET 10 / Aspire application that helps software architects explore the
[Azure Well-Architected Framework](https://learn.microsoft.com/azure/well-architected/) using
AI-powered RAG (Retrieval-Augmented Generation) and chat.

### Services

| Project | Role |
|---|---|
| `TheArchitect.AppHost` | .NET Aspire orchestrator – starts all services |
| `TheArchitect.ApiService` | ASP.NET Core Web API – chat, search, ingest endpoints |
| `TheArchitect.Web` | Blazor Server frontend – Chat and Search pages |
| `TheArchitect.ServiceDefaults` | Shared OpenTelemetry, health-check, service-discovery defaults |

---

## Running the application

```bash
# Restore & build everything
dotnet build

# Run all services through the Aspire orchestrator (recommended)
dotnet run --project TheArchitect.AppHost

# Run individual services (useful for focused debugging)
dotnet run --project TheArchitect.ApiService   # API at http://localhost:5363
dotnet run --project TheArchitect.Web          # UI at https://localhost:5001
```

The Aspire dashboard is available at the URL printed by `AppHost` on startup.

---

## Backend – `TheArchitect.ApiService`

**Language / Framework:** C# 13, ASP.NET Core 10 (minimal APIs)

**External dependencies (all started by `AppHost`):**
- **Qdrant** – vector database (Docker container, persistent lifetime)
- **Ollama** – local LLM inference
  - `nomic-embed-text` – text → embedding
  - `qwen3:4b` – chat / summarisation

### API endpoints

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/chat` | Start new chat thread. Rephrases question → embedding → Qdrant search → summarises chunks → final answer. Returns `ChatReply`. |
| `POST` | `/chat/{thread:Guid}` | Continue existing thread. Appends user message, calls LLM, returns `ChatReply`. |
| `GET` | `/search?query=` | Vector search (top 5 results). Returns raw Qdrant `ScoredPoint[]`. |
| `POST` | `/ingest` | Chunk & embed all `*.md` files under `well-architected/well-architected/`, upsert into Qdrant collection `architect`. |
| `GET` | `/embed?input=` | Return raw embedding for a string (debugging). |

**Response type for chat endpoints:**
```csharp
public record ChatReply(Guid Thread, string Text, string[] Sources);
```

**Search response shape (Qdrant `ScoredPoint` serialised via System.Text.Json camelCase):**
```jsonc
[
  {
    "id": { "num": "12345" },
    "score": 0.8765,
    "payload": {
      "file": { "stringValue": "well-architected/well-architected/reliability/..." }
    }
  }
]
```

The `file` payload value follows the Qdrant gRPC `Value` oneof pattern
(`stringValue` / `integerValue` / `doubleValue` / `boolValue`).

---

## Frontend – `TheArchitect.Web`

**Language / Framework:** C# 13, Blazor Server (Interactive Server render mode), Bootstrap 5

### Pages

| Route | Component | Purpose |
|---|---|---|
| `/` | `Pages/Home.razor` | Landing page with links to Chat and Search |
| `/chat` | `Pages/Chat.razor` | Chat UI – first message initialises thread via `POST /chat`; subsequent messages use `POST /chat/{thread}` |
| `/search` | `Pages/Search.razor` | Vector-search UI + ingest trigger button |

### Key files

| File | Purpose |
|---|---|
| `ArchitectApiClient.cs` | Typed `HttpClient` wrapping the backend API (`StartChatAsync`, `ContinueChatAsync`, `SearchAsync`, `IngestAsync`) |
| `Components/Layout/NavMenu.razor` | Sidebar navigation links |
| `Components/Layout/MainLayout.razor` | Root layout |
| `wwwroot/app.css` | Global CSS – includes chat bubble styles (`.chat-bubble`, `.chat-bubble-user`, `.chat-bubble-assistant`) |
| `Program.cs` | DI registration and middleware pipeline |

### Adding a new page

1. Create `Components/Pages/MyPage.razor` with `@page "/my-route"` and `@rendermode InteractiveServer`.
2. Add a `<NavLink>` entry in `Components/Layout/NavMenu.razor`.
3. If the page needs backend calls, add a method to `ArchitectApiClient` (or create a new typed client and register it in `Program.cs`).

---

## Data ingestion

Before the chat and search features work, documents must be ingested:

1. Place markdown files under `TheArchitect.ApiService/well-architected/well-architected/` (any subdirectory depth).
2. Call `POST /ingest` once (button available on the **Search** page, or via the Scalar UI at `/scalar/v1`).

The ingestor creates a Qdrant collection named `architect` (cosine distance, 768-dim vectors matching `nomic-embed-text`) and upserts one point per chunk (5000-char chunks, 500-char overlap).

---

## Development notes

- **No automated tests** exist yet. If adding tests, use xUnit and create a new
  `TheArchitect.ApiService.Tests` or `TheArchitect.Web.Tests` project.
- **OpenAPI docs** are served at `/scalar/v1` when running `ApiService` in Development mode.
- **HTTP test file** `TheArchitect.ApiService/TheArchitect.ApiService.http` can be used with
  VS / Rider HTTP client tooling to test endpoints manually.
- The Aspire orchestrator injects service discovery via `https+http://apiservice`; do not
  hard-code `localhost` URLs in the frontend.
- Chat history is held **in-process** in a `Dictionary<Guid, List<ChatMessage>>` in
  `ApiService/Program.cs`. This is intentionally simple – it is not persisted across restarts.
