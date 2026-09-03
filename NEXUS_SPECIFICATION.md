# 🧠 NEXUS — AI Knowledge & Learning Workspace
> **Master Architectural Blueprint & Project Specification**

---

## 📌 1. نظرة عامة على المنتج (Product Overview & Vision)

**NEXUS** هو بيئة عمل ذكية للمعلومات والتعلم (**AI Knowledge & Learning Workspace**) تجمع بين إدارة المعرفة، وتدوين الملاحظات، والتفكير البصري (Mind Maps & Boards)، والتعلم الذاتي المدعوم بالذكاء الاصطناعي (RAG Engine & AI Tutor).

```
                    ┌─────────────────────────┐
                    │        NEXUS            │
                    │ AI Knowledge Workspace  │
                    └────────────┬────────────┘
                                 │
       ┌─────────────┬───────────┼────────────┬─────────────┐
       ↓             ↓           ↓            ↓             ↓
   📚 Knowledge    📝 Notes    📋 Board     🧠 Mind Map   🤖 AI
       │             │           │            │             │
       └─────────────┴───────────┴────────────┴─────────────┘
                                 │
                         🔍 Knowledge Engine
                                 │
                    ┌────────────┴────────────┐
                    ↓                         ↓
              🔎 Search Engine           🧠 RAG Engine
                    │                         │
                    └────────────┬────────────┘
                                 ↓
                           🤖 AI Assistant
```

---

## 🏗️ 2. الهيكلية العامة للنظام (System Architecture)

يعتمد النظام على نمط **Modular Monolith** لضمان سرعة التطوير، وتقليل تعقيدات البنية التحتية، مع إمكانية فصل أي موديول لاحقًا إلى Microservice عند الحاجة.

```
┌─────────────────────────────────────────────────────┐
│                    NEXUS CLIENT                     │
│                                                     │
│                    WPF Desktop                      │
│                    MVVM                             │
└──────────────────────────┬──────────────────────────┘
                           │
                           │ HTTP / REST & SignalR
                           ↓
┌─────────────────────────────────────────────────────┐
│                  NEXUS API                          │
│                  ASP.NET Core                       │
│                                                     │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐             │
│  │ Identity │ │ Workspace│ │ Notes    │             │
│  └──────────┘ └──────────┘ └──────────┘             │
│                                                     │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐             │
│  │ Board    │ │ MindMap  │ │ Study    │             │
│  └──────────┘ └──────────┘ └──────────┘             │
│                                                     │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐             │
│  │Documents │ │ Search   │ │ AI       │             │
│  └──────────┘ └──────────┘ └──────────┘             │
└──────────────────────────┬──────────────────────────┘
                           │
              ┌────────────┼──────────────┐
              ↓            ↓              ↓
        SQL Server      File Storage    AI Provider
              │            │              │
              ↓            ↓              ↓
         Metadata       PDFs/Docs       LLM
                                         │
                                         ↓
                                  Embedding Model
                                         │
                                         ↓
                                  Vector Database
```

---

## 🧱 3. الهيكلية الداخلية (Clean Architecture)

النظام مبني على مبادئ **Clean Architecture** مع فصل صارم للمسؤوليات وعدم تسريب التبعيات الخارجية إلى طبقة الـ Domain:

```
NEXUS
│
├── NEXUS.Domain
│   ├── Entities/
│   │   ├── User.cs
│   │   ├── Workspace.cs
│   │   ├── Page.cs
│   │   ├── Note.cs
│   │   ├── Document.cs
│   │   ├── DocumentChunk.cs
│   │   ├── Board.cs
│   │   ├── BoardItem.cs
│   │   ├── MindMap.cs
│   │   ├── MindMapNode.cs
│   │   ├── MindMapEdge.cs
│   │   ├── TaskItem.cs
│   │   ├── Tag.cs
│   │   ├── Flashcard.cs
│   │   └── Quiz.cs
│   ├── ValueObjects/
│   ├── Enums/
│   ├── DomainEvents/
│   └── Interfaces/
│
├── NEXUS.Application
│   ├── Features/
│   │   ├── Authentication/
│   │   ├── Workspaces/
│   │   ├── Pages/
│   │   ├── Notes/
│   │   ├── Documents/
│   │   ├── Boards/
│   │   ├── MindMaps/
│   │   ├── Study/
│   │   ├── Search/
│   │   └── AI/
│   ├── DTOs/
│   ├── Interfaces/
│   ├── Behaviors/ (Validation, Logging, Caching)
│   └── Common/
│
├── NEXUS.Infrastructure
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── Configurations/
│   │   ├── Migrations/
│   │   └── Repositories/
│   ├── AI/
│   │   ├── LLM/ (OpenAI / Local LLM / Ollama)
│   │   ├── Embeddings/
│   │   └── RAG/
│   ├── Search/ (Hybrid Search, Lucene / SQL FTS)
│   ├── Storage/ (Local File Storage / MinIO / Blob)
│   └── Services/
│
├── NEXUS.API
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── WorkspacesController.cs
│   │   ├── PagesController.cs
│   │   ├── NotesController.cs
│   │   ├── DocumentsController.cs
│   │   ├── BoardsController.cs
│   │   ├── MindMapsController.cs
│   │   ├── SearchController.cs
│   │   ├── AIController.cs
│   │   └── StudyController.cs
│   ├── Middleware/
│   └── Program.cs
│
└── NEXUS.Desktop
    ├── Views/
    ├── ViewModels/
    ├── Models/
    ├── Services/ (API Client, TokenStorage, LocalCache)
    ├── Commands/
    ├── Navigation/
    ├── Controls/ (Custom MindMap Canvas, Kanban Board)
    └── Themes/ (Dark / Modern UI Theme)
```

---

## 🗄️ 4. تصميم قاعدة البيانات (Database Design & Hierarchy)

قاعدة البيانات الأساسية هي **SQL Server** لتخزين البيانات العلائقية والـ Metadata، بالإضافة إلى **Vector Store** لتخزين الـ Embeddings:

```
Users
│
└── Workspaces
      │
      ├── Pages (Structured wiki-like documentation)
      │
      ├── Notes (Rich text / Markdown notes)
      │
      ├── Documents (PDFs, Docs, Articles)
      │     └── DocumentChunks (Vector IDs & Content)
      │
      ├── Boards (Study & Kanban Boards)
      │     └── BoardItems (Tasks, Topics with AI progress)
      │
      ├── MindMaps (Interactive Knowledge Graphs)
      │     ├── MindMapNodes (Linked to Notes/Docs/AI)
      │     └── MindMapEdges
      │
      ├── Tasks (Action items & study goals)
      │
      ├── Flashcards & Quizzes (Study & assessment engine)
      │
      └── Tags (Cross-entity taxonomy)
```

---

## 🧠 5. محرك المعرفة ومسار المعالجة (Knowledge Ingestion Pipeline)

عند رفع مستند (مثل `EF-Core.pdf`)، يمر بالمراحل التالية لتحويله إلى معرفة قابلة للبحث والتحليل:

```
             Upload PDF
                 │
                 ↓
          Document Storage (Local / Cloud)
                 │
                 ↓
          Text Extraction (PDF / Markdown / Docx parsers)
                 │
                 ↓
          Text Cleaning & Normalization
                 │
                 ↓
          Smart Chunking (Semantic / Sliding Window)
                 │
                 ↓
          Create Embeddings (e.g. OpenAI / Local Embeddings)
                 │
                 ↓
          Vector Storage (Qdrant / Milvus / SQL Vector / SQLite Vector)
                 │
                 ↓
          Searchable Knowledge Base
```

---

## 🤖 6. محرك الـ RAG والبحث الهجين (RAG & Hybrid Search)

### أ. مسار الإجابة مع المصادر (RAG Pipeline)
عند طرح المستخدم لسؤال مثل: *"Explain EF Core tracking"*

```
User Question
      │
      ↓
Query Understanding & Intent Extraction
      │
      ↓
Query Embedding
      │
      ↓
Vector Search + Keyword Search (Hybrid)
      │
      ↓
Relevant Chunks Extraction (Documents & Notes)
      │
      ↓
Prompt Augmentation + System Context
      │
      ↓
LLM Generation
      │
      ↓
Answer + Explicit Citations
```

**مثال على مخرج الـ RAG:**
> **Answer:**
> EF Core tracking means that the `DbContext` monitors changes made to entity instances...
>
> **Sources:**
> - 📄 `EF Core Documentation.pdf` — *Section: Change Tracking*
> - 📝 `My EF Core Notes` — *Page: Tracking & Lifetime*

---

### ب. البحث الهجين (Hybrid Search)

دمج البحث الدقيق للكلمات المفتاحية مع البحث الدلالي:

```
                 Search Query
                      │
            ┌─────────┴─────────┐
            ↓                   ↓
       Keyword Search       Vector Search
     (Exact terms, Code)   (Semantic / Context)
            │                   │
            └─────────┬─────────┘
                      ↓
              Reciprocal Rank Fusion (RRF) / Ranking
                      │
                      ↓
                Final Results
```

---

## 📋 7. لوحة المذاكرة الذكية (Smart Study Board)

ليست مجرد Kanban عادي، بل لوحة مذاكرة يفهمها الذكاء الاصطناعي:

```
Study Board
┌─────────────┬─────────────┬─────────────┐
│   TO LEARN  │  LEARNING   │   MASTERED  │
├─────────────┼─────────────┼─────────────┤
│ EF Core     │ DbContext   │ SQL JOIN    │
│ LINQ        │ Tracking    │ SQL GROUP BY│
│ Docker      │ Migrations  │ Git         │
└─────────────┴─────────────┴─────────────┘
```

- **تفاعل الذكاء الاصطناعي مع اللوحة:**
  - المستخدم يسأل: *"ما هي الموضوعات التي أحتاج مراجعتها اليوم؟"*
  - الـ AI يحلل: المهام، الملاحظات، نتائج الاختبارات (Quizzes)، ومعدل التقدم، ثم يقدم توصيات ذكية:
    - 🔴 **EF Relationships** (Needs urgent review - Quiz score 60%)
    - 🟡 **IQueryable vs IEnumerable** (Last reviewed 2 weeks ago)
    - 🟢 **SQL Joins** (Mastered)

---

## 🧠 8. الخرائط الذهنية الذكية (AI-Powered Mind Mapping)

ربط الخرائط الذهنية مباشرة بالـ Knowledge Base:

```
                     EF CORE
                        │
          ┌─────────────┼─────────────┐
          │             │             │
      DbContext       LINQ       Relationships
          │             │             │
      ┌───┴───┐       Query       ┌───┴────┐
      │       │         │          │        │
   DbSet  Tracking  IQueryable   One-Many Many-Many
```

- **ميزات العقدة (Node Features):**
  - عند الضغط على عقدة مثل `DbContext`:
    - 📄 المستندات المرتبطة (`Related Documents`).
    - 📝 الملاحظات المرتبطة (`Notes`).
    - 💻 أمثلة برمجية (`Code Snippets`).
    - 🤖 زر سريع لسؤال الـ AI حول المفهوم.
    - 🔗 المفاهيم المشتركة (`Related Concepts`).
- **توليد الخرائط آليًا بالذكاء الاصطناعي:**
  - *"أنشئ خريطة ذهنية من هذا الكتاب/المستند"* ➡️ توليد هيكل تفاعلي من الفصول والمفاهيم.

---

## 📚 9. محرك المذاكرة والاختبار (Study Engine & AI Tutor)

نظام متكامل لكل موضوع تعليمي (**Topic Hub**):

```
Topic
│
├── Notes
├── Documents
├── Mind Map
├── Board
├── Flashcards (Spaced Repetition)
├── Quiz (Auto-generated MCQs / Open questions)
├── Tasks
└── AI Tutor
```

- **التقييم الذكي (Knowledge Assessment):**
  - اختبارات تفاعلية تتولد من ملاحظات وملفات المستخدم.
  - إحصائيات دقيقة بنقاط القوة والضعف ومقترحات مخصصة للمراجعة.

---

## 🎨 10. واجهة المستخدم (Desktop UI Layout - WPF MVVM)

تصميم مكتبي حديث واحترافي يدمج كافة أدوات المعرفة مع مساعد الذكاء الاصطناعي المدمج:

```
┌────────────────────────────────────────────────────────────────────────┐
│ NEXUS                                            🔍 Quick Search   👤 │
├────────────┬───────────────────────────────────────────────────────────┤
│            │                                                           │
│ 🏠 Home    │                     WORKSPACE                             │
│            │                                                           │
│ 📚 Library │         ┌───────────────────────────────────────┐         │
│            │         │                                       │         │
│ 📝 Notes   │         │           Active Content Area         │         │
│            │         │   (Document / MindMap / Board / Note) │         │
│ 📋 Boards  │         │                                       │         │
│            │         └───────────────────────────────────────┘         │
│ 🧠 Maps    │                                                           │
│            │                                                           │
│ 🎓 Study   │                                                           │
│            │                                                           │
│ 🤖 AI Chat │                                                           │
│            │                                                           │
└────────────┴───────────────────────────────────────────────────────────┘
```

**لوحة المساعد الذكي الجانبية (AI Sidebar Panel):**
```
┌─────────────────────────────────────────┐
│ 🤖 NEXUS AI Assistant                   │
├─────────────────────────────────────────┤
│ Ask anything about your workspace...    │
│                                         │
│ 👤 You:                                 │
│ Explain DbContext tracking behavior     │
│                                         │
│ 🤖 AI:                                  │
│ DbContext tracks entity state changes...│
│                                         │
│ 📌 Verified Sources:                    │
│ 📄 EF Core Guide.pdf (p. 42)            │
│ 📝 My EF Notes (Section 3)              │
├─────────────────────────────────────────┤
│ 💬 Type your question...          [ ➤ ] │
└─────────────────────────────────────────┘
```

---

## 🚀 11. مراحل بناء وتطوير المشروع (Implementation Roadmap)

لتفادي التعقيد وضمان جودة الكود، ينفذ المشروع عبر **7 مراحل متتالية**:

| المرحلة | العنوان | نطاق العمل والمخرجات الأساسية |
|---|---|---|
| **Phase 1** | **Foundation** | إعداد الـ Solution، طبقات Clean Architecture، إعداد EF Core و SQL Server، المصادقة (Identity & JWT)، إدارة الـ Workspaces، والبنية الأساسية للتنقل في الـ Desktop Client. |
| **Phase 2** | **Knowledge** | رفع المستندات (PDF / Docs)، نظام تخزين الملفات (Storage)، استخراج النصوص (Text Extraction)، التقطيع الذكي (Chunking)، توليد الـ Embeddings، والبحث الشعاعي (Vector Search). |
| **Phase 3** | **AI & RAG** | محرك الـ RAG، نظام المحادثة الذكية (AI Chat)، الاستشهادات المرجعية الدقيقة (Citations)، التلخيص الآلي (Summarization)، وواجهة *Ask Workspace*. |
| **Phase 4** | **Productivity** | إدارة الصفحات (Pages)، الملاحظات الغنية (Notes)، إدارة المهام (Tasks)، نظام الوسوم (Tags)، ومحرك البحث الهجين الموحد. |
| **Phase 5** | **Visual Thinking** | لوحات المذاكرة (Study / Kanban Board)، قماش الرسم (Canvas)، الخرائط الذهنية (Mind Maps & Graph Nodes/Edges)، وخاصية السحب والإفلات (Drag & Drop). |
| **Phase 6** | **Study Engine** | وضع المذاكرة التفاعلي (Study Mode)، البطاقات التعليمية (Flashcards مع Spaced Repetition)، توليد الاختبارات (Quizzes)، تقييم المعرفة وتوصيات الـ AI Tutor. |
| **Phase 7** | **Polish & Production** | تحسين الـ UI/UX، الحركات الدقيقة (Animations)، التخزين المؤقت (Caching)، السجلات والتعامل مع الأخطاء (Logging & Error Handling)، تحسين الأداء، واختبارات الوحدة والتكامل (Unit & Integration Testing). |

---

*Document created for project tracking and phased execution in NEXUS workspace.*
