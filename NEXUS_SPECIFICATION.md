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

لتفادي التعقيد وضمان جودة الكود، نُفّذ المشروع عبر **8 مراحل متتالية ومحكمة**:

| المرحلة | العنوان | نطاق العمل والمخرجات الأساسية | الحالة (Status) |
|---|---|---|:---:|
| **Phase 1** | **Foundation** | إعداد الـ Solution، طبقات Clean Architecture، إعداد EF Core و SQL Server، المصادقة (Identity & JWT)، إدارة الـ Workspaces، والبنية الأساسية للتنقل في الـ Desktop Client (WPF MVVM). | ✅ مكتمل ومختبر |
| **Phase 2** | **Knowledge Workspace Core** | إدارة الصفحات (Pages) والشجرة الهرمية، الملاحظات الغنية (Notes) والوسوم، رفع وإدارة المستندات (PDF / Docx / Markdown / Text)، والبحث النصي الأساسي. | ✅ مكتمل ومختبر |
| **Phase 3** | **Embeddings & Vector Search** | التقطيع الذكي للمستندات (Chunking) مع التداخل الرمزي، مزودات الـ Embeddings (OpenAI / Ollama / In-Memory)، والتخزين الشعاعي والبحث بالتشابه الدلالي (Cosine Similarity). | ✅ مكتمل ومختبر |
| **Phase 4** | **RAG + AI Assistant** | محرك الـ RAG، نظام المحادثة الذكية (AI Conversations & Chat)، استرجاع السياق الذكي من مستندات الـ Workspace، والاستشهادات المرجعية الدقيقة (Citations & Source Snippets). | ✅ مكتمل ومختبر |
| **Phase 5** | **Knowledge Intelligence** | تدفقات الذكاء الاصطناعي التوليدي: التلخيص الآلي (Summarize)، الشرح المتعمق (Explain)، استخراج الأفكار الرئيسية (Key Points)، وتوليد مواد الدراسة وحفظها مباشرة في الملاحظات. | ✅ مكتمل ومختبر |
| **Phase 6** | **Study Engine & AI Tutor** | مواضيع المذاكرة (Study Topics)، البطاقات التعليمية التكرارية بنظام SuperMemo-2 (SM-2 Spaced Repetition)، توليد الاختبارات التفاعلية وتصحيحها، ونظام الـ AI Tutor والتقييم المعرفي. | ✅ مكتمل ومختبر |
| **Phase 7** | **Visual Thinking & Canvas** | لوحات المذاكرة وإدارة المهام (Boards & Kanban Items)، الخرائط الذهنية (Mind Maps & Nodes/Edges)، خوارزميات التوزيع التلقائي (Hierarchical Tree / Force-Directed)، وتوليد الخرائط بالذكاء الاصطناعي. | ✅ مكتمل ومختبر |
| **Phase 8** | **Production Hardening & Finalization** | الأمان المشدد (JWT Fail-Fast / Environment-aware HTTPS / CORS Lockdown)، منع تسريب البيانات الحساسة عبر Middleware موحد، فحص صحة النظام (`/health`)، تحسين استعلامات الأداء، وتعزيز متانة الـ Desktop Client. | ✅ مكتمل ومختبر |
| **Final Hardening** | **Safety, API Semantics & Legacy Cleanup** | تدقيق وإلغاء تسجيلات Mock AI القديمة من DI الإنتاجي، ضبط دلالات REST الصارمة (401 للمستخدم غير المصادق vs 403 للوصول الممنوع)، فرض سقوف أمان واسترجاع محدد (Bounded Endpoints & LimitExceeded)، والتحقق الصارم من قاعدة البيانات عند الإقلاع. | ✅ مكتمل ومختبر |

---

### 🔒 عقد أخطاء الـ API ودلالات الـ HTTP (API Error & Authorization Contract)

يتبع NEXUS المعايير القياسية لبروتوكول HTTP وفقًا لـ RFC 7235:

| رمز الحالة (Status Code) | الدلالة (Semantics) | شرط الإرجاع (Trigger Condition) |
|---|---|---|
| `400 Bad Request` | فشل التحقق من صحة المدخلات | مدخلات ناقصة، صيغة غير مدعومة، أو تجاوز حدود التحقق |
| `401 Unauthorized` | غياب المصادقة (Unauthenticated) | توكن JWT مفقود، غير صالح، أو منتهي الصلاحية |
| `403 Forbidden` | وصول مرفوض (Access Denied) | مستخدم مصادق عليه يحاول الوصول لمساحة عمل أخرى بدون صلاحية |
| `404 Not Found` | العنصر غير موجود | الكيان المطلوب غير موجود في مساحة العمل المحددة |
| `409 Conflict` | تعارض في حالة المورد | تكرار البريد الإلكتروني عند التسجيل، أو تعارض في التعديل المتزامن |
| `500 Internal Server Error` | خطأ غير معالج في الخادم | خطأ داخلي مع حجب التفاصيل الحساسة في بيئة الإنتاج وتسجيلها بأمان |

---

### 🔮 تطلعات مستقبلية (Future Roadmap / Post-Phase 8 Candidates)

- **التعاون اللحظي (Real-Time Multi-User Collaboration):** دعم التحرير المشترك باستخدام SignalR أو CRDT.
- **تطبيق الويب والهواتف (Web & Mobile Clients):** واجهة ويب تفاعلية وتطبيق للمراجعة السريعة على الهواتف الذكية.
- **الرسوم البيانية المعرفية المعقدة (Graph Database Integration):** ربط الكيانات عبر محرك رسومي متخصص عند توسع أحجام البيانات بشكل ضخم.

---

*Document updated and verified for Phase 8 & Final Hardening Pass completion in NEXUS workspace.*
