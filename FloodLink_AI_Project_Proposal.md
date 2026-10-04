# FloodLink AI 

## An AI-Coordinated Disaster & Flood Relief Resource Platform 

### _Project Proposal_ 

SE3090 — Software Engineering Frameworks 

Assignment 1: Integrated Full-Stack and Agentic AI Application Development BSc (Hons) in Information Technology — Specializing in Software Engineering / AI 

Faculty of Computing, SLIIT Year 3, Semester 1 \| 2026 

Group: \[Group Number\] Members: \[Member A\], \[Member B\], \[Member C\], \[Member D\] 

Date: \[Submission Date\] 

# 1. Introduction 

Sri Lanka experiences recurrent seasonal flooding that displaces thousands of people annually, straining the ability of disaster management authorities, the Red Cross, and local volunteer networks to allocate scarce relief supplies quickly and fairly. Today, this coordination is largely manual — conducted over phone calls, WhatsApp groups, and paper logs — which makes it difficult to see, in real time, which shelters are most in need, what supplies are actually available where, and whether a dispatch decision is safe and traceable. 

FloodLink AI is a full-stack, AI-assisted disaster relief coordination platform that connects field volunteers, warehouse/depot staff, and relief coordinators through a single shared system. It replaces ad-hoc manual coordination with a structured, auditable workflow: field reports are automatically triaged and prioritized, matched against real inventory, routed for delivery, and validated against safety constraints — with a human coordinator making the final call before any truck is dispatched or scarce stock is reallocated. 

# 2. Problem Statement 

- Disaster relief coordination in Sri Lanka is largely manual, relying on phone calls and messaging apps, which is slow and error-prone during fast-moving flood events. 
- There is no shared, real-time view of shelter needs versus available inventory across depots, leading to over-supply in some areas and shortages in others. 
- Decisions about where to send limited supplies are made without a consistent, auditable prioritization process. 
- Existing digital tools in this space are typically either simple reporting forms (no intelligence) or fully automated systems with no human checkpoint before high-stakes actions — neither is appropriate for life-and-safety decisions. 

# 3. Proposed Solution 

FloodLink AI combines a Flutter mobile app for field volunteers, a React web dashboard for coordinators and depot managers, a shared ASP.NET Core Web API and PostgreSQL database, and a four-agent Agentic AI subsystem that plans, matches, routes, and validates relief dispatches — pausing for mandatory human approval before any supplies are committed. 

## 3.1 Core Workflow 

- A field volunteer submits a shelter needs report via Flutter, including GPS location and photo evidence. 
- A Triage/Planner agent reviews new reports and produces a prioritized, structured response plan. 
- A Logistics/Matching agent matches the prioritized needs against real-time inventory across depots. 
- A Route/ETA agent calls a maps/routing API to calculate delivery distance and estimated arrival time. 
- A Validation/Safety agent performs deterministic checks — stock availability, vehicle capacity, reserve thresholds — before any plan can proceed. 
- A relief coordinator reviews the complete plan in React and approves, rejects, or requests revision — the point at which supplies are actually committed and a dispatch is created. 
- The originating volunteer sees the updated status back in Flutter, closing the loop from field report to delivered supplies. 

# 4. Objectives 

- Design and implement a secure, role-based full-stack system integrating ASP.NET Core, PostgreSQL, React, and Flutter around one shared data and business-rules layer. 
- Build a controlled, multi-agent Agentic AI workflow that plans, delegates, uses external tools, validates its own output, and defers to human approval for high-impact actions. 
- Demonstrate a complete, auditable, cross-platform workflow from field report to approved dispatch and back to a status update. 
- Apply professional software engineering practice — Git/GitHub, CI/CD, automated testing, and documented architecture decisions — throughout development. 
- Produce a system with genuine real-world relevance to Sri Lanka's disaster response context, beyond the scope of a typical classroom CRUD exercise. 

# 5. Target Users & Roles 

| **Role**  | **Description**  | **Primary Interface**  |
| --- | --- | --- |
| Field Volunteer  | Reports shelter needs and status from the ground, tracks the status of their own reports  | Flutter (mobile)  |
| Relief Coordinator  | Reviews AI-generated plans and approves, rejects, or requests revision of dispatches  | React (web)  |
| Depot / Warehouse Manager  | Manages inventory levels and confirms stock reservations  | React (web) + Flutter (stock check-in)  |
| Administrator  | Manages users, roles, and system-wide configuration; views analytics  | React (web)  |

# 6. Scope 

## 6.1 In Scope 

- Shelter and field-report management with photo and GPS evidence capture. 
- Depot and inventory management with transactional stock reservation. 
- A four-agent Agentic AI pipeline: triage/planning, logistics/matching, routing, and validation. 
- Human-in-the-loop approval workflow for supply dispatch, with full audit trail. 
- Third-party maps/routing API integration for delivery ETA calculation. 
- Role-based authentication and authorization (JWT) across React and Flutter, backed by one shared API. 
- Automated testing (unit, integration, end-to-end, and agent evaluation) and a GitHub Actions CI pipeline. 
- Cloud deployment of the API, database, and web app, plus a distributable Android APK. 

## 6.2 Out of Scope (for this assignment) 

- Real-time chat/messaging between volunteers and coordinators (status updates only). 
- Payment processing or financial transactions. 
- iOS-specific builds (Android APK only, per submission requirements). 
- Integration with live government/NGO disaster-management systems (simulated/sample data will be used for demonstration). 

# 7. Technology Stack 

| **Layer**  | **Technology**  | **Justification**  |
| --- | --- | --- |
| Backend  | C#, ASP.NET Core Web API  | Mandatory per module specification; strong typing and mature ecosystem for a secure, authoritative API layer  |
| Database  | PostgreSQL, Entity Framework Core  | Mandatory; relational integrity fits well-structured relief/inventory data with transactional guarantees  |

| Web App  | React (functional components, hooks)  | Mandatory; suited to the coordinator/admin dashboard's data-dense, monitoring-heavy interface  |
| --- | --- | --- |
| Mobile App  | Flutter, Dart  | Mandatory; single codebase for the field volunteer app with strong camera/GPS/offline support  |
| Agentic AI  | LangGraph (or equivalent orchestration framework) + LLM  | Explicit multi-step graph/state orchestration model fits the plan → delegate → validate → approve workflow  |
| Maps/Routing  | OpenRouteService or Mapbox Directions API  | No-cost tier available; provides real distance/ETA calculation for the Route agent  |
| Auth  | JWT with role-based authorization  | Required for securing shared endpoints across two client applications  |
| CI/CD  | GitHub Actions  | Mandatory; automated build and test on every push/PR to main  |

# 8. Agentic AI Subsystem Overview 

Four distinct agents, each with a defined responsibility, input/output contract, and controlled tool access, form the core AI subsystem: 

| **Agent**  | **Responsibility**  | **Key Tool(s)**  |
| --- | --- | --- |
| Triage / Planner  | Scores urgency and produces a prioritized, structured response plan from raw field reports  | Database read (reports, shelter history)  |
| Logistics / Matching  | Matches prioritized needs against real-time inventory to propose concrete allocations  | Database read (inventory), matching logic  |
| Route / ETA  | Calculates delivery distance and estimated arrival time for a proposed allocation  | External maps/routing API  |
| Validation / Safety  | Performs deterministic checks (stock, capacity, thresholds) before a plan reaches human approval  | Rule-based validator (no LLM call)  |

 A relief coordinator must approve, reject, or request revision of any complete plan before supplies are reserved or a dispatch is created — the system's one clearly defined high-impact action requiring authorized human sign-off, as required by the assignment specification. 

# 9. Third-Party Integration 

A maps/routing API (OpenRouteService or Mapbox Directions) will be called by the Route/ETA agent, routed through the ASP.NET Core backend rather than called directly by either client. It serves a genuine functional purpose — computing realistic delivery distance and ETA for an approved dispatch — rather than a decorative map display, and will include timeout handling, retry limits, and graceful failure if the service is unavailable. 

# 10. Expected Outcomes & Impact 

- A working, deployed, end-to-end system demonstrating integrated use of the full mandated technology stack. 
- A defensible example of responsible agentic AI design: multi-step planning, tool use, deterministic validation, and mandatory human approval for high-stakes actions — rather than a fully autonomous black box. 
- A system with a credible real-world use case for Sri Lanka's disaster response context, with genuine potential for extension beyond the classroom (e.g. a pilot with a local volunteer or NGO network). 
- Demonstrated team competence across backend, database, web, mobile, AI orchestration, testing, and DevOps practice. 

# 11. Team & Component Ownership 

| **Member**  | **Primary Component**  | **Agent Owned**  |
| --- | --- | --- |
| Member A  | Field Intake & Shelter Reporting  | Triage / Planner Agent  |
| Member B  | Inventory & Depot Management  | Logistics / Matching Agent  |
| Member C  | Agent Orchestration & Workflow Engine  | Route / ETA Agent  |
| Member D  | Validation, Approval & Dispatch Audit  | Validation / Safety Agent  |

 _Full task-level breakdown per member is documented separately in the project development plan._ 

# 12. Proposed Timeline 

| **Phase**  | **Weeks**  | **Milestone**  |
| --- | ---: | --- |
| Setup & Foundations  | 1–2  | Repository, CI skeleton, ER diagram, shared auth, agent I/O contracts agreed  |
| Core Development  | 3–4  | Component CRUD, base React/Flutter screens functional  |
| Agent Development  | 5–6  | Individual agents built and integrated into one pipeline  |
| Approval Flow & Integration  | 7  | Coordinator approval UI, maps API integration, human-in-the-loop tested  |
| Testing & Deployment  | 8  | Full test suites, CI green, cloud deployment, APK build  |
| Finalization  | 9  | Demo rehearsal, ADRs, reports, AI usage logs, submission  |

# 13. Conclusion 

FloodLink AI addresses a genuine coordination gap in Sri Lanka's disaster relief efforts while satisfying every mandatory requirement of the SE3090 Assignment 1 specification: an integrated ASP.NET Core, PostgreSQL, React, and Flutter system; a four-agent Agentic AI subsystem with real planning, tool use, and validation; a meaningful human-approval checkpoint on a genuinely high-impact action; and a defensible, real-world domain that goes beyond a typical university exercise. The team believes this project offers strong potential both as an academic submission and as a foundation for future development beyond the course. 
