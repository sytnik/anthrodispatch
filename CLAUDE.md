# AnthroDispatch — контекст проєкту

Дослідницький прототип для дисертації Ситніка Олега Олександровича
(«Моделі та методи розроблення програмного забезпечення
антропоцентричної диспетчеризації навчального процесу у ЗВО»).
Дисертаційний репозиторій: `D:\Git\phd-thesis` (свій `CLAUDE.md` там,
розділ "Дорожня карта: тестовий прототип → продакшн-система →
оновлення дисертації" — там само фази 1-3 і повний план).

## Три фази (з дисертаційного репо, коротко)

1. **Ця фаза — актуалізація цього прототипу** під формалізації,
   уточнені під час роботи над дисертацією.
2. Інтеграція актуалізованого ядра (AMD/SRA/IA) у робочу (продакшн)
   систему диспетчеризації, яка зараз працює з даними вручну.
3. Зворотне оновлення технічних розділів дисертації (§2.3, §4.1,
   можливо §4.3) за результатами інтеграції.

Зараз — фаза 1.

## Стек (перевірено 2026-08-12, не з паперу — папір/стаття 7 каже
"NET 9", реально .NET 10)

- .NET 10, 5 проєктів: `AnthroDispatch.Domain` (без залежностей),
  `AnthroDispatch.Application` (+ MathNet.Numerics 5.0.0),
  `AnthroDispatch.Infrastructure` (+ EF Core InMemory 10.0.8, Bogus
  35.6.5), `AnthroDispatch.Api` (Minimal API + Swashbuckle 10.1.7),
  `AnthroDispatch.ConsoleRunner`.
- Тести: `tests/AnthroDispatch.Tests` — NUnit 4.6.0 + FluentAssertions
  8.10.0. README досі каже "52 тести у 9 fixture" — застаріле, реально
  **71 тест у 12 fixture** після роботи 2026-08-12 (додано
  `SraServiceTests`, `ScoreIaServiceTests`,
  `ConformanceCheckingServiceTests` + 1 новий тест в `ApiSmokeTests`).
- Namespace-шар усередині Application: `Algorithms.{Objective, Genetic,
  Cpc, Awm, Repair, Sra, Explanation, WhatIf}` (не напряму під
  Application, як могло здатись зі статті 7).
- Git-історія: 2 коміти, увесь код прийшов одним великим комітом
  ("Initial prototype implementation") — тобто це одноразова AI-
  згенерована реалізація специфікації (`temp/anthrodispatch_specification*.md`,
  `temp/implementation_plan.md`), а не органічно розвинений код.

## Аудит коду проти дисертації (виконано 2026-08-12)

### Збігається з дисертацією — НЕ чіпати без потреби

- **Матриця S** (`Domain/Entities/CognitiveCompatibility.cs`) — вже
  спрямована: окремі `FromDisciplineId`/`ToDisciplineId`, явна
  асиметрія типово-процесних оцінок в
  `Infrastructure/MockData/AnthroDispatchMockDataGenerator.cs`
  (`(Analytical→Creative)=0.60` ≠ `(Creative→Analytical)=0.40`).
- **`l(d,s,g)` в `Application/Algorithms/Awm/AwmMutation.cs`** — вже
  `Discipline.LoadLevel` (enum `CognitiveLoadLevel`), зважений через
  `weights.Psych` (=w₃) як проксі психологічного комфорту — точно за
  формулою дисертації q(d,s,g)=w₂·a+w₃·(1−l)+w₄·c.
- **Хронотип** (`Application/Algorithms/Objective/CircadianActivityCalculator.cs`)
  — вже гаусова `a_χ(τ)=exp(-(τ-peak)²/2σ²)`, σ²=2.5, 5 MEQ-категорій
  (`Domain/Enums/ChronotypeCategory.cs`), ті самі peak-слоти
  (2,3,4,6,7). Плюс віковий модифікатор `AgeModifier`
  (clip[0.85,1.05], те саме, що в дисертації §2.1).
- **`Risk(x)`** (`Application/Algorithms/Explanation/RiskModelService.cs`)
  — δ=(0.30, 0.30, 0.25, 0.15) буквально збігається з §2.4.

### Розбіжності — потребують коду (фаза 1, у пріоритеті)

1. ~~**SRA: тільки звичайний OLS, ridge-регресії немає**~~ —
   **виправлено 2026-08-12.** `SraService.cs`: N≥50 — той самий
   β=(X'X)⁻¹X'y, N<50 — ridge β=(X'X+μI)⁻¹X'y, μ=0.1, інтерцепт
   (стовпець 0) не регуляризується (стандартна практика). Тести —
   `tests/AnthroDispatch.Tests/Algorithms/SraServiceTests.cs` (3 нові:
   валідний simplex на малому N, на великому N, стійкість при
   майже-колінеарних фічах). Повний прогін — **60/60 тестів зелені**
   (README досі каже 52 — застаріле число, не оновлював README).
   Побічна знахідка (не виправлялась, поза межами цього завдання):
   `Matrix.Inverse()` з MathNet на реально виродженій матриці (плоскі
   колінеарні фічі, N≥50, без ridge) **не завжди кидає виняток** —
   іноді мовчки повертає NaN замість спрацювання catch-fallback до
   старих вагів. Це існувало до моєї зміни (catch був і раніше), я
   лише додав ridge-гілку для N<50 — сам факт ненадійного
   NaN-виявлення для N≥50 лишається; якщо колись зустрінеться
   реальний NaN у продакшн-даних SRA — почати звідси.
2. ~~**`z(x)` і `Score_IA(x)` відсутні повністю**~~ —
   **виправлено 2026-08-12.** Підтверджено (як і передбачав TODO):
   `AmdService`/`BaselineGaService`/`CpcGaService`/`AwmGaService` до
   зміни повертали лише `population[0]` (єдиний найкращий розв'язок),
   решта популяції відкидалась. Реалізовано:
   - `OptimizationResult.TopCandidates` (`List<Timetable>?`, новий
     опційний параметр запису в кінці, зворотно сумісний) — X_cand.
   - `GaOptions.TopMCandidates` (int, за замовчуванням 5 — m не
     деталізовано в дисертації, скромне дефолтне значення).
   - Усі 4 GA-сервіси тепер `population.Take(TopMCandidates)` перед
     поверненням (однаковий патерн у кожному, як і решта коду).
   - `ExplanationService.ComputeExplainability(Timetable)` — нове:
     частка занять з ≥1 нетривіальною позитивною причиною (циркадна
     активність ≥0,5 — поріг НЕ деталізований дисертацією, власний
     вибір; АБО відсутність жорсткого конфлікту; АБО додатна
     когнітивна сумісність із попереднім заняттям дня).
   - **Новий модуль** `Application/Algorithms/ScoreIa/`:
     `CandidateVector.cs` (record z(x), 7 полів),
     `RankedCandidate.cs` (Timetable + z + ScoreIa),
     `ScoreIaService.cs` (`BuildZ`, `Score`, `RankCandidates` —
     ρ=(0,55;0,20;0,15;0,10), точно за §2.4). Коли немає попередньої
     затвердженої версії (`previous=null`) — FStable=1,0 (нема з чим
     порівнювати дестабілізацію) — власний вибір, не з дисертації.
   - 5 нових тестів `ScoreIaServiceTests.cs`. Повний прогін —
     **65/65 тестів зелені**.
   - **API/БД-шар — зроблено 2026-08-12.** `OptimizationRun.
     CandidatesJson` (нове поле) — X_cand, ранжований за Score_IA,
     обчислюється й серіалізується одразу в `/api/optimization/run`
     (дані вже в пам'яті на той момент, `previous=null`). Новий
     `GET /api/optimization/{runId}/candidates`
     (`ScoreIaEndpoints.cs`, зареєстровано в `Program.cs`) — той самий
     патерн, що й `/timetable`, `/metrics`. `RankedCandidateDto`/
     `ScheduledClassDto` — internal DTO поруч із наявними в
     `Api/Endpoints/`. Тест `ApiSmokeTests.
     CandidatesEndpoint_ShouldReturnRankedCandidatesSortedByScoreIa`
     — **66/66 тестів зелені**.
3. ~~**Process mining conformance checking — відсутній повністю**~~ —
   **виправлено 2026-08-12.** Новий модуль
   `Application/Algorithms/Conformance/`: `PetriNet.cs` (генеричний
   place/token механізм — реальні consume/produce, не замаскований
   лічильник), `ConformanceCheckingService.cs` (будує σ_x
   впорядкуванням `timetable.Classes` за (day,period,Id), N —
   capacity-1 місця для group/instructor/room double-booking
   (справжні Petri-net-місця) + 2 статичні guard-перевірки (room
   capacity, room type) — ті самі категорії, що вже валідує
   `FtechCalculator`, свідомо не продубльовано instructor/health
   hard-обмеження (вони вже покриті через F_tech в іншому місці; нова
   цінність цього сервісу — саме Petri-мережа ресурсів + діагностика,
   не вичерпне повторення C_hard(x)). **Задокументований вибір
   формули** (дисертація не фіксує точний алгоритм): r=m (кожен
   пропущений/запозичений токен лишається неабсорбованим у моделі
   виключних ресурсів) — дає Conform(x)=1 рівно коли C_hard(x)
   виконано (m=0), монотонно спадає з порушеннями, точна формула
   §3.4. `ConformanceResult`/`ConformanceViolation` — записи з
   day/period/group/instructor/room для alignment-діагностики.
   **API:** `GET /api/optimization/{runId}/conformance`
   (`ConformanceEndpoints.cs`) — обчислюється на льоту з уже
   збереженого `TimetableJson`, схему `OptimizationRun` розширювати
   не довелось (на відміну від Score_IA, де population відкидалась).
   5 нових тестів (`ConformanceCheckingServiceTests.cs`) + 1 API-тест
   — **71/71 тестів зелені**. **Свідомо не зроблено:** інтеграція
   alignment-діагностики як цільового списку в `RepairService`
   (дисертація описує це як оптимізацію repair, не вимогу) — не
   чіпав уже протестовану repair-логіку без окремого запиту.
4. **Тестове покриття:** SRA/ScoreIa/Conformance тепер мають власні
   fixture (див. вище). Досі немає окремого `RiskModelServiceTests.cs`
   для `RiskModelService`/`ExplanationService.ExplainClass/
   ExplainTimetable` (лише непрямо через `AlgorithmTests`/
   `ApiSmokeTests`/`ScoreIaServiceTests`). Додавати разом із наступною
   зміною цих сервісів, не окремим заходом.

## TODO (у порядку виконання)

- [x] **Ridge-регресія в SraService.cs** — зроблено 2026-08-12, див.
  "Розбіжності" вище.
- [x] **`z(x)` + `Score_IA(x)`** — зроблено 2026-08-12, див.
  "Розбіжності" вище. 65/65 тестів зелені.
- [x] **API/БД-шар для Score_IA** — зроблено 2026-08-12 (окремий
  `GET /api/optimization/{runId}/candidates`, обчислення на льоту
  всередині `/run`, кеш у `CandidatesJson`). 66/66 тестів.
- [x] **Process mining conformance checking** — зроблено 2026-08-12
  (`Application/Algorithms/Conformance/` + `ConformanceEndpoints.cs`).
  71/71 тестів. **Фаза 1 дисертаційної дорожньої карти — усі 3 пункти
  розбіжностей закриті.**
- [ ] Тести на Risk/Explanation (SRA, ScoreIa й Conformance вже
  покриті).
- [ ] (опційно, знайдено попутно, не блокує) README каже "52 тести",
  реально 71 — оновити таблицю в README, коли рука дійде.
- [ ] **Наступний крок — Фаза 2**: інтеграція актуалізованого ядра
  (AMD/SRA/IA/Conformance) у робочу систему диспетчеризації, яка
  зараз обробляє дані вручну. Деталі — `D:\Git\phd-thesis\CLAUDE.md`,
  розділ "Дорожня карта".

## Правила роботи з цим репо

- **Дисертаційний текст — джерело правди для формул**, якщо код і
  текст розходяться (за винятком щойно знайдених розбіжностей вище,
  де рухаємось у напрямку код→дисертація). Звіряти з `D:\Git\phd-thesis\src\06_chapter2_models.md`
  (§2.1-2.4) і `07_chapter3_methods.md` (§3.1-3.5) перед зміною формул.
- Не вигадувати нових гіперпараметрів — якщо дисертація не деталізує
  щось (напр. чи регуляризувати інтерцепт у ridge), обирати
  стандартну практику галузі й фіксувати вибір коментарем у коді.
- Після кожної зміни — прогнати тести (`dotnet test`), не лишати
  репо в стані, що не білдиться/не проходить тести.
- Один великий "AI-generated" коміт в історії — при подальшій роботі
  комітити природними логічними кроками (не намагатись відтворити
  цей паттерн).
