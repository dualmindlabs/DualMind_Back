# Codebase Structure

**Analysis Date:** 2026-02-27

## Directory Layout

```
src/DualMind.API/
├── AI/                     # AI integrations layer
│   ├── Contracts/          # Abstraction interfaces for AI providers
│   ├── Gateway/            # AI Factory and Routing logic
│   └── Providers/          # Specific LLM Provider implementations
├── Controllers/            # API endpoints
│   ├── Admin/              # Protected administrative APIs
│   └── Api/                # Core application REST APIs
├── Core/                   # Core business logic
│   ├── Exceptions/         # Custom exception definitions
│   ├── Models/             # Shared DTOs and view models
│   └── Services/           # Services implementing logic flows
├── Filters/                # ASP.NET Core filters
├── Infrastructure/         # Cross-cutting concerns and data access
│   ├── Configuration/      # Environment/settings models
│   ├── Data/               # Supabase HTTP REST Clients
│   └── Security/           # Security/auth utilities
└── Program.cs              # Application entry point
```

## Directory Purposes

**`AI/`:**
- Purpose: All logic related to sending requests to external Large Language Models
- Contains: Interfaces, Factory classes, Provider implementations
- Key files: `src/DualMind.API/AI/Gateway/ChatProviderFactory.cs`, `src/DualMind.API/AI/Providers/GroqService.cs`

**`Controllers/`:**
- Purpose: Defines HTTP API structure and handles requests
- Contains: `ControllerBase` derivatives handling route attributes
- Key files: `src/DualMind.API/Controllers/Api/ArenaController.cs`, `src/DualMind.API/Controllers/ThreadsController.cs`

**`Core/`:**
- Purpose: Application-specific rules, model structures, and coordination
- Contains: Core application services, model definitions
- Key files: `src/DualMind.API/Core/Services/ModelSelector.cs`, `src/DualMind.API/Core/Services/ModelStatsService.cs`

**`Infrastructure/`:**
- Purpose: Interaction with the external state (database) and environment
- Contains: Supabase HTTP clients, App Configuration bindings
- Key files: `src/DualMind.API/Infrastructure/Data/SupabaseService.cs`

## Key File Locations

**Entry Points:**
- `src/DualMind.API/Program.cs`: Setup Dependency Injection, configure Middleware, configure JWT

**Configuration:**
- `src/DualMind.API/Infrastructure/Configuration/EnvConfig.cs`: Loading/parsing `.env` configuration manually
- `src/DualMind.API/Properties/launchSettings.json`: Local development run profile

**Core Logic:**
- `src/DualMind.API/Core/Services/ArenaService.cs`: AI execution logic and comparison handling
- `src/DualMind.API/Core/Services/ThreadsService.cs`: Thread persistence logic

**Testing:**
- (No immediate testing structure identified in main app src logic)

## Naming Conventions

**Files:**
- C# Classes match filename: `ChatRequest.cs` contains `class ChatRequest`
- Interfaces start with `I`: `IThreadsService.cs`

**Directories:**
- PascalCase for namespaces and folders: `Core/Services/`

## Where to Add New Code

**New Feature:**
- Primary code: Create new `Controller` in `src/DualMind.API/Controllers/Api/`
- Tests: Add corresponding testing project or mock setup

**New Component/Module:**
- Implementation: Add to `src/DualMind.API/Core/Services/` for standard application logic or `src/DualMind.API/AI/Providers/` for a new AI source

**Utilities:**
- Shared helpers: `src/DualMind.API/Infrastructure/` (e.g., Security logic)

## Special Directories

**`.planning/`:**
- Purpose: Contains GSD codebase analysis documents (like this one)
- Generated: Yes
- Committed: Yes

**`.vs/`:**
- Purpose: Visual Studio user-specific configurations and workspaces
- Generated: Yes
- Committed: No

---

*Structure analysis: 2026-02-27*