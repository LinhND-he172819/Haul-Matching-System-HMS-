# Haul Matching System (HMS)

Haul Matching System is a logistics fleet-control and shipment-matching project for road transport companies that operate their own drivers and vehicles.

The system focuses on:

- Hub-based shipment receiving and consolidation.
- Route-based shipment matching using actual road geometry.
- Weight and volume capacity optimization.
- Real-time GPS fleet visibility.
- Off-system load declaration and commission reconciliation.
- Exception handling for failed delivery, breakdown, cancellation, and return-to-hub flow.

## Repository Structure

```text
.
├── AGENTS.md              # Instructions for Codex/AI agents working in this repo
├── PROJECT_CONTEXT.md     # Product, architecture, and requirement context
├── docs/                  # Project documents, reports, diagrams, requirements notes
│   ├── source/
│   ├── reports/
│   ├── diagrams/
│   ├── architecture/
│   └── requirements/
├── src/                   # Future application source code
└── tests/                 # Future automated tests
```

## For Codex Sessions

Start by reading:

1. `PROJECT_CONTEXT.md`
2. `AGENTS.md`
3. `docs/README.md`

Then inspect the specific documents or source files related to the task.

## Current Documentation Baseline

- SRS: `docs/reports/Report 3.0_SRS_HMS_v1.0.0_with_diagrams.docx`
- RTW: `docs/reports/Report 3.1_RTW_HMS_v1.0.0.xlsx`
- Draw.io source diagrams: `docs/diagrams/*.drawio`

## Architecture Direction

The preferred implementation style is a modular monolith split by business domain:

- Identity & Admin
- Warehouse & Shipment
- Trip & GPS
- Matching Engine
- Realtime & Notification

Avoid a single large shared domain/infrastructure layer for all modules.
