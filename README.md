# AnthroDispatch

**A Software Framework for Anthropocentric Dispatch Management and Decision Support in Industry 5.0-Oriented Educational
Processes**

> Research prototype — .NET 10 · C# · ASP.NET Core Minimal API · EF Core InMemory · Bogus · MathNet.Numerics · NUnit ·
> FluentAssertions · Swagger

---

## Research Context

The prototype demonstrates the algorithmic and architectural feasibility of an anthropocentric educational timetabling
framework that combines:

- Chronotype-aware (circadian) scheduling using a Gaussian activity model with age-aware amplitude modifiers and MEQ
  chronotype categories
- Cognitive compatibility-aware scheduling via a domain-weighted compatibility matrix (ProcessType × LoadLevel × Domain)
- Psychological-load-aware evaluation through workload variance, transition analysis, health limitation penalties, and
  soft-constraint penalties
- Dispatch-problem pipeline: `AnthroDispatchDataset` → `DispatchProblem` (via `DispatchInputBuilder`) → GA optimizer
- Adaptive weight learning via Satisfaction Regression Adaptation (SRA)
- Explainable decision support with local (per-class) and global (timetable-level) explanations
- What-if scenario evaluation (10 scenario types)
- Comparison of Baseline GA, CPC-GA, AWM-GA, and full AMD algorithms

---

## No Real Data Statement

> This repository does not contain real student, instructor, or institutional data.
> All datasets are generated on the fly using deterministic mock data generators based on Bogus.

---

## Prototype Limitations

- InMemory persistence only — not suitable for production use
- No authentication or authorization
- No production UI (Swagger only)
- Simplified repair heuristic (not a full constraint solver)

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                   AnthroDispatch.Api                         │
│  Minimal API Endpoints  +  Swagger/OpenAPI                   │
│  Endpoint DTOs (request/response records per file)           │
└──────────────────────────────┬───────────────────────────────┘
                               │
┌──────────────────────────────▼───────────────────────────────┐
│               AnthroDispatch.Application                     │
│  DispatchInputBuilder  │  AssignmentExpander                 │
│  CurriculumHoursCalculator  │  OperationalDataValidator      │
│  ObjectiveFunctionService  (FTech / FCirc / FPsych / FCogn) │
│  BaselineGA / CpcGA / AwmGA / AmdService                    │
│  RepairService  │  SraService  │  WhatIfService              │
│  ExplanationService  │  RiskModelService                     │
└──────────┬───────────────────────────────────────────────────┘
           │
┌──────────▼────────────┐   ┌──────────────────────────────────┐
│  AnthroDispatch.Domain│   │  AnthroDispatch.Infrastructure   │
│  Entities (Domain,    │   │  EfRepository<T>                 │
│   Operational,        │   │  AnthroDispatchMockDataGenerator │
│   Anthropocentric,    │   │  MockDatasetGenerator (legacy)   │
│   Dispatch)           │   │  AnthroDispatchDbContext (InMem)  │
│  Enums / ValueObjects │   │  DispatchProblemCache (singleton)│
│  Metrics / Weights    │   └──────────────────────────────────┘
└───────────────────────┘
```

---

## How to Run the API

```bash
dotnet run --project src/AnthroDispatch.Api
```

Swagger UI is available at: **http://localhost:5000/swagger**

The API starts with an empty InMemory database. Typical workflow:

```
POST /api/datasets/generate                         ← generate mock data
POST /api/datasets/{id}/build-dispatch-problem      ← build DispatchProblem from dataset
POST /api/optimization/run  (dispatchProblemId)     ← run optimizer
GET  /api/optimization/{runId}/metrics              ← inspect results
GET  /api/optimization/{runId}/explanation          ← global explanation
POST /api/whatif/...                                ← what-if scenarios
```

---

## How to Run Console Experiments

```bash
# Generate a mock dataset summary
dotnet run --project src/AnthroDispatch.ConsoleRunner -- generate --seed 42

# Run a single optimization
dotnet run --project src/AnthroDispatch.ConsoleRunner -- optimize --algorithm AMD --seed 42 --pop 200 --gen 500

# Ablation study (all 4 algorithms × N runs)
dotnet run --project src/AnthroDispatch.ConsoleRunner -- ablation --seed 42 --runs 30 --pop 200 --gen 500
```

Expected ablation output:

```
Algorithm      F100     F500     t(F>0.65),min    t(F>0.75),min    σ
----------------------------------------------------------------------
BaselineGA     0.53     0.71     42.1             58.2             0.043
CpcGA          0.59     0.76     29.4             41.3             0.037
AwmGA          0.57     0.74     33.2             45.6             0.039
AMD            0.64     0.84     22.8             38.4             0.029
```

---

## How to Run Tests

```bash
dotnet test
```

All **105 tests** should pass. Test breakdown by fixture:

| Fixture                            | Tests | Coverage area                                                                                                                         |
|-------------------------------------|-------|---------------------------------------------------------------------------------------------------------------------------------------|
| `ExplanationServiceTests`          | 23    | ExplainClass reasons/conflicts/compatibility/trade-offs, ExplainTimetable strengths/weaknesses/recommendations, ComputeExplainability |
| `RiskModelServiceTests`            | 11    | Risk(x) weighted-sum formula (perfect/worst/no-fStable/C_interf cases), FStable stability score                                       |
| `ConflictDetectorTests`            | 9     | All 7 conflict types + multi-group/multi-instructor dispatch conflicts                                                                |
| `DispatchInputBuilderTests`        | 9     | Dataset→DispatchProblem conversion, lab splitting, compatibility matrix, constraint attachment, room capacity validation              |
| `AlgorithmTests`                   | 8     | BaselineGA, AMD improvement, repair, AWM slot preference, CPC day-block structure                                                     |
| `AnthroDispatchGeneratorTests`     | 8     | Curriculum plans, calendar terms, plan items, learning assignments, prerequisite edges, constraints, age/health profiles, determinism |
| `ApiSmokeTests`                    | 7     | Health, dataset generation, optimization run, candidates, conformance, explanation, what-if scenario                                  |
| `CircadianActivityTests`           | 6     | Circadian activity peak correctness, age modifier clamping                                                                            |
| `ExtendedObjectiveFunctionTests`   | 5     | Age-aware circadian correction, age modifier clamping, health constraint penalties, soft instructor preference, metric bounds         |
| `ScoreIaServiceTests`              | 5     | TopCandidates population, Score_IA ranking, z(x) vector construction, F_stable                                                        |
| `ObjectiveFunctionTests`           | 4     | Objective function component correctness                                                                                              |
| `ConformanceCheckingServiceTests`  | 4     | Petri-net token-based replay, Conform(x) for conflict-free/room-capacity/group-double-booking/empty timetables                        |
| `SraServiceTests`                  | 3     | OLS/ridge weight adaptation, simplex projection                                                                                       |
| `MockDatasetGeneratorTests`        | 3     | Determinism, count verification, compatibility score bounds                                                                           |

---

## Data Generation

Mock data is generated on-demand using [Bogus](https://github.com/bchavez/Bogus) with a deterministic seed. The full
operational dataset includes:

- **Academic structure**: AcademicYears, Degrees, Departments, EducationalPrograms, AcademicCalendars,
  AcademicCalendarTerms
- **Curriculum**: CurriculumPlans, CurriculumPlanItems (with per-week hour breakdowns), prerequisite edges (
  `CurriculumPlanItemEdge`)
- **Actors**: Groups (with chronotype, average age, age std dev), Instructors (with age, department, max classes/day,
  max consecutive classes)
- **Anthropocentric data**: HealthLimitations (with `InstructorId` ownership), InstructorConstraints, GroupConstraints
- **Assignments**: LearningAssignments linked to groups, instructors, and plan items via join tables
- **Rooms**: typed rooms (LectureHall, Lab, ComputerLab, SeminarRoom, Online) with realistic capacities
- **Cognitive Compatibility Matrix**: computed from ProcessType × LoadLevel × Domain compatibility rules

Chronotype population distributions:

| Chronotype       | Students | Instructors |
|------------------|----------|-------------|
| Definite Morning | 8 %      | 22 %        |
| Moderate Morning | 24 %     | 31 %        |
| Intermediate     | 39 %     | 32 %        |
| Moderate Evening | 22 %     | 13 %        |
| Definite Evening | 7 %      | 2 %         |

---

## Algorithms

| Algorithm      | Description                                                                               |
|----------------|-------------------------------------------------------------------------------------------|
| **BaselineGA** | Random init + tournament selection + two-point crossover + uniform swap mutation + repair |
| **CpcGA**      | BaselineGA + day-wise chronotype-preserving crossover (γ = 5.0)                           |
| **AwmGA**      | BaselineGA + anthropocentric weighted mutation (β = 2.0)                                  |
| **AMD**        | Greedy init + tournament + CPC crossover + AWM mutation + repair + elitism                |

---

## Objective Function

`F = wT·FTech + wC·FCirc + wP·FPsych + wCg·FCogn`   (default weights: 0.25 each)

| Component  | Formula                                              | Key factors                                                                                                                  |
|------------|------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------|
| **FTech**  | `1 − conflicts/N`                                    | Instructor/group/room double-booking, room capacity, room-type mismatch, hard constraint violations, health hard constraints |
| **FCirc**  | `(1/N) Σ [0.6·a_group + 0.4·a_instr]`                | Age-aware Gaussian circadian activity per slot                                                                               |
| **FPsych** | `clip(base − 0.20·healthPenalty − 0.15·softPenalty)` | Workload variance, uncomfortable transitions, health limitations, soft instructor preferences                                |
| **FCogn**  | `(1/N) Σ sij`                                        | Cognitive compatibility scores between consecutive disciplines                                                               |

---

## What-If Scenarios

| Route                                     | Scenario                                   |
|-------------------------------------------|--------------------------------------------|
| `POST /api/whatif/instructor-unavailable` | Instructor unavailable for a day or period |
| `POST /api/whatif/room-unavailable`       | Room unavailable on a day                  |
| `POST /api/whatif/group-unavailable`      | Group cannot attend a period               |
| `POST /api/whatif/discipline-moved`       | Discipline must be rescheduled             |
| `POST /api/whatif/weights-changed`        | Objective weight reconfiguration           |
| `POST /api/whatif/instructor-constraint`  | New instructor constraint applied          |
| `POST /api/whatif/health-limitation`      | Health limitation appears                  |
| `POST /api/whatif/room-capacity`          | Room capacity insufficient                 |
| `POST /api/whatif/group-constraint`       | Group constraint applied                   |
| `POST /api/whatif/mode-change`            | Education mode change (online ↔ offline)   |

All scenarios return `DeltaF`, `FDynamic` (η·F + (1−η)·Fstable, η = 0.7), risk before/after, and a natural-language
explanation list.

---

## API Endpoints

| Method | Route                                              | Description                                                         |
|--------|----------------------------------------------------|---------------------------------------------------------------------|
| GET    | `/api/health`                                      | Health check                                                        |
| POST   | `/api/datasets/generate`                           | Generate full mock operational dataset                              |
| POST   | `/api/datasets/{id}/build-dispatch-problem`        | Build `DispatchProblem` from dataset (required before optimization) |
| GET    | `/api/datasets/{id}/summary`                       | Dataset record summary                                              |
| POST   | `/api/optimization/run`                            | Run optimizer (accepts `dispatchProblemId` or legacy `datasetId`)   |
| GET    | `/api/optimization/{id}/timetable`                 | Raw timetable JSON                                                  |
| GET    | `/api/optimization/{id}/metrics`                   | FTech / FCirc / FPsych / FCogn / F / conflicts                      |
| GET    | `/api/optimization/{id}/explanation`               | Global timetable explanation                                        |
| GET    | `/api/optimization/{id}/classes/{cid}/explanation` | Per-class explanation with trade-offs                               |
| POST   | `/api/whatif/instructor-unavailable`               | Scenario: instructor unavailability                                 |
| POST   | `/api/whatif/room-unavailable`                     | Scenario: room unavailability                                       |
| POST   | `/api/whatif/group-unavailable`                    | Scenario: group cannot attend period                                |
| POST   | `/api/whatif/discipline-moved`                     | Scenario: discipline relocation                                     |
| POST   | `/api/whatif/weights-changed`                      | Scenario: objective weight change                                   |
| POST   | `/api/whatif/instructor-constraint`                | Scenario: new instructor constraint                                 |
| POST   | `/api/whatif/health-limitation`                    | Scenario: health limitation appears                                 |
| POST   | `/api/whatif/room-capacity`                        | Scenario: room capacity insufficient                                |
| POST   | `/api/whatif/group-constraint`                     | Scenario: group constraint applied                                  |
| POST   | `/api/whatif/mode-change`                          | Scenario: online ↔ offline mode change                              |
| POST   | `/api/sra/adapt`                                   | SRA weight adaptation from participant sample                       |
| POST   | `/api/experiments/ablation`                        | Multi-run ablation (all 4 algorithms)                               |

---

## License

See [LICENSE](LICENSE).
