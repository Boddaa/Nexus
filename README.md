# 🧠 NEXUS — AI Knowledge & Learning Workspace

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20Architecture%20%7C%20Modular%20Monolith-blue.svg)]()
[![Frontend](https://img.shields.io/badge/Client-WPF%20%28MVVM%29-green.svg)]()
[![Backend](https://img.shields.io/badge/API-ASP.NET%20Core%20Web%20API-orange.svg)]()

**NEXUS** is an enterprise-grade, privacy-first **AI Knowledge & Learning Workspace** built on .NET 10. It unifies personal and workspace knowledge management, rich note-taking, multi-format document ingestion, semantic vector retrieval, hybrid search, visual thinking (mind mapping and boards), and an interactive AI Tutor powered by Retrieval-Augmented Generation (RAG).

---

## 🏗️ Architecture Overview

NEXUS is engineered as a **Modular Monolith** adhering to the principles of **Clean Architecture**:

```text
┌─────────────────────────────────────────────────────────────┐
│                    DESKTOP CLIENT                           │
│                 WPF (.NET 10) · MVVM                        │
│         ApiClient · Reactive ViewModels · Canvas UI         │
└──────────────────────────────┬──────────────────────────────┘
                               │ HTTP / REST (JWT Bearer)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                       NEXUS API                             │
│                  ASP.NET Core Web API                       │
│    Controllers · JWT Auth · CORS · Health Checks · Swagger  │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                   APPLICATION LAYER                         │
│   Use Cases · Services · DTOs · Interfaces · Business Rules  │
│   (Auth, Workspaces, Pages, Notes, Documents, Search,       │
│    Conversations, AI Intelligence, Study, Visual Thinking)  │
└──────────────────────┬──────────────────────┬───────────────┘
                       │                      │
                       ▼                      ▼
┌──────────────────────────────┐  ┌───────────────────────────┐
│         DOMAIN LAYER         │  │   INFRASTRUCTURE LAYER    │
│  Entities · Value Objects    │  │  EF Core 10 · SQL Server  │
│  Enums · Domain Primitives   │  │  Local File Storage       │
│  (Pure C# - No Dependencies) │  │  Document Extractors      │
└──────────────────────────────┘  │  AI Providers (OpenAI)    │
                                  │  Ollama / Mock Providers  │
                                  │  Vector Index & Search    │
                                  └───────────────────────────┘
```

### Architectural Principles
1. **Separation of Concerns**: The `Nexus.Desktop` client communicates solely via HTTP/REST through the API contracts. It contains **no direct reference** to `Nexus.Infrastructure`, SQL Server, or EF Core `DbContext`.
2. **Domain Independence**: The `Nexus.Domain` layer is purely isolated and depends on zero external libraries or database frameworks.
3. **Inversion of Control**: The `Nexus.Application` layer defines interfaces (e.g., `IAppDbContext`, `ILLMService`, `IFileStorage`), implemented by `Nexus.Infrastructure`.
4. **Resilience & Production Hardening**: Strict fail-fast secret verification, environment-aware CORS and HTTPS enforcement, sanitized error outputs, configurable migrations, and explicit timeouts on external I/O.

---

## ✨ Implemented Capabilities (Phases 1–8)

NEXUS includes 8 fully realized, integrated modules:

### 1. Identity & Multi-Tenancy (Phase 1)
- User registration and authentication with BCrypt password hashing.
- Secure JWT token generation with configurable issuer, audience, and lifetime.
- Workspace creation, ownership isolation, and multi-tenant access controls.

### 2. Knowledge Workspace Core (Phase 2)
- **Hierarchical Pages**: Unlimited nested tree structures for workspace documentation.
- **Rich Notes**: Pinning, tag filtering, markdown support, and page association.
- **Document Management**: 50 MB upload limits, checksum deduplication, path-traversal prevention, and isolated file storage.
- **Text Extractors**: High-fidelity extraction from `.pdf`, `.docx`, `.md`, and `.txt` files.

### 3. Chunking, Embeddings & Vector Search (Phase 3)
- Token-aware document chunking with configurable overlap.
- Modular embedding providers: OpenAI `text-embedding-3-small`, Ollama, and deterministic in-memory vector stores.
- Cosine similarity vector search over ingested knowledge chunks.

### 4. RAG Pipeline & AI Assistant (Phase 4)
- Context-aware conversational AI assistant (`Ask Workspace`).
- Dynamic retrieval of the most relevant document chunks and notes.
- Provenance tracking with verified citations, confidence scores, and source snippet references.

### 5. Knowledge Intelligence & AI Workflows (Phase 5)
- Automated one-click summarization of pages, notes, and documents.
- Deep explanations of complex technical concepts from workspace content.
- Key takeaways and bullet-point extraction.
- AI study material and question generation with direct save-to-notes capability.

### 6. Study Engine & AI Tutor (Phase 6)
- **Study Topics**: Topic organization linked to source documents or notes.
- **Spaced Repetition Flashcards**: SuperMemo-2 (SM-2) algorithm calculating intervals, ease factors, and due dates.
- **Interactive Quizzes**: Auto-generated multiple-choice and open-ended quizzes with grading, explanations, and review modes.
- **AI Tutor & Knowledge Assessment**: Real-time personalized tutoring and gap analysis.

### 7. Visual Thinking & Knowledge Canvas (Phase 7)
- **Study & Kanban Boards**: Visual item cards, column organization, tag badges, and drag-and-drop workflow tracking.
- **Mind Maps**: Infinite canvas with nodes, directed edges, label badges, color palettes, and hierarchical auto-layout algorithms (Tree Layout, Force-Directed Layout).
- **AI Mind Map Generator**: Atomic generation and persistence of full mind maps directly from workspace knowledge documents.
- **Related Knowledge Finder**: Discovers semantic connections between canvas nodes and existing workspace materials.

### 8. Production Hardening & Finalization (Phase 8)
- **Strict Security**: Fail-fast validation of JWT secrets in production; rejection of hardcoded fallback credentials.
- **HTTPS & CORS Lockdown**: Environment-aware metadata enforcement and origin-restricted CORS policies.
- **Information Leak Prevention**: Centralized exception middleware sanitizing error responses in production.
- **Health Checks**: `/health` endpoint validating process state and database connectivity.
- **Performance Optimizations**: Database-level count projections eliminating redundant in-memory collection loading; bounded conversation histories; explicit HTTP timeouts for external AI providers.
- **Multi-Instance Migration Decoupling**: Configurable startup migrations via `Database:ApplyMigrationsOnStartup`.

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.100 or later)
- SQL Server (LocalDB, SQL Server Express, or standard SQL Server instance)
- *(Optional)* OpenAI API key or [Ollama](https://ollama.com/) for local LLM inference

### Configuration

Configuration files reside in `src/Nexus.API/`:
- `appsettings.json`: Base configuration (safe placeholders, migrations disabled by default).
- `appsettings.Development.json`: Local development defaults (enables migrations on startup, development JWT key).
- `appsettings.Production.json`: Production template (strict logging, empty secret placeholders).

#### Environment Variables for Production
In production, supply secrets via environment variables or cloud secret stores:

| Variable | Description | Example |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | `Server=sql.internal;Database=NexusDb;User Id=...` |
| `JwtSettings__SecretKey` | JWT Signing Key (>= 32 characters / 256 bits) | `YourStrongProductionSecretKeyMustBe32CharsLong!` |
| `JwtSettings__Issuer` | Valid JWT Issuer | `https://api.nexus.workspace` |
| `JwtSettings__Audience` | Valid JWT Audience | `https://app.nexus.workspace` |
| `Cors__AllowedOrigins__0` | Allowed CORS Origin | `https://app.nexus.workspace` |
| `AI__LLM__ApiKey` or `OPENAI_API_KEY` | OpenAI API Key *(if using OpenAI)* | `sk-proj-...` |
| `AI__LLM__Provider` | LLM Provider (`OpenAI`, `Ollama`, or `None`) | `OpenAI` |
| `Database__ApplyMigrationsOnStartup` | Run migrations on API boot | `false` (recommended for production) |

---

## 🗄️ Database Migrations

### Apply Migrations via CLI (Recommended for Production)
```bash
dotnet ef database update --project src/Nexus.Infrastructure --startup-project src/Nexus.API
```

### Add a New Migration
```bash
dotnet ef migrations add <MigrationName> --project src/Nexus.Infrastructure --startup-project src/Nexus.API
```

---

## 🏃 Running the Application

### 1. Run the Web API
```bash
dotnet run --project src/Nexus.API
```
- The API starts on `http://localhost:5000` (or configured port).
- In Development mode, interactive Swagger documentation is available at root: `http://localhost:5000/`.
- Health check endpoint is available at `http://localhost:5000/health`.

### 2. Run the WPF Desktop Client
```bash
dotnet run --project src/Nexus.Desktop
```

---

## 🧪 Testing

The solution contains a comprehensive test suite across all layers (Domain, Application, Infrastructure, API, Desktop).

Run the complete test suite:
```bash
dotnet test Nexus.sln
```

Expected verification baseline:
- **Build**: 0 errors, 0 warnings
- **Test Results**: 100% passed, 0 failed, 0 skipped

---

## 📂 Project Structure

```text
NEXUS/
├── src/
│   ├── Nexus.Domain/              # Pure business entities, enums, value objects, domain errors
│   ├── Nexus.Application/         # DTOs, interfaces, service implementations, validators
│   ├── Nexus.Infrastructure/      # EF Core DbContext, migrations, AI providers, local storage
│   ├── Nexus.API/                 # REST controllers, middleware, JWT auth, health checks
│   └── Nexus.Desktop/             # WPF MVVM client, views, viewmodels, canvas rendering
├── tests/
│   ├── Nexus.Domain.Tests/        # Unit tests for domain models and business invariants
│   ├── Nexus.Application.Tests/   # Unit tests for application services and workflows
│   ├── Nexus.Infrastructure.Tests/# Tests for chunking, text extractors, and storage
│   ├── Nexus.API.Tests/           # Integration tests using WebApplicationFactory
│   └── Nexus.Desktop.Tests/       # ViewModels and UI state unit tests
├── Nexus.sln                      # Solution configuration
├── README.md                      # Project documentation and guide
└── NEXUS_SPECIFICATION.md         # Master architectural blueprint and roadmap
```

---

## 📄 License
This project is proprietary and maintained for the NEXUS AI Knowledge & Learning Workspace.
