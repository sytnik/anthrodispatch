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

Фаза 1 завершена 2026-08-12 (усі TODO закриті, 104/104 тестів).
Наступний крок — фаза 2.

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
4. ~~**Тестове покриття:** SRA/ScoreIa/Conformance тепер мають власні
   fixture, RiskModelService/ExplanationService — не мали.~~ —
   **виправлено 2026-08-12.** Додано `RiskModelServiceTests.cs` (10
   тестів: зважена сума Risk(x) для ідеальних/найгірших метрик, внесок
   без fStable, окремо перевірений внесок FTech<1.0, FStable для
   ідентичних/повністю змінених/частково змінених/порожніх розкладів)
   і `ExplanationServiceTests.cs` (23 тести: `ExplainClass` — невідомий
   id, конфлікти групи/викладача, когнітивна сумісність попереднього
   заняття дня, trade-off по періоду, блендинг 60/40 group/instructor
   активності; `ExplainTimetable` — найсильніший/найслабший компонент,
   конфлікти, найризиковіші групи, найгірші когнітивні послідовності,
   перевантажені викладачі, рекомендації по кожному з трьох компонентів
   через `[TestCase]`; `ComputeExplainability` — конфліктний і мішаний
   розклад). Обидва файли — `tests/AnthroDispatch.Tests/Algorithms/`.
5. **Risk_cognitive не був C_interf(x) — знайдено при написанні тестів
   на RiskModelService (2026-08-12), виправлено.** §2.4 визначає
   `Risk_cognitive(x) = C_interf(x)`, де `C_interf(x)` (§2.2) — це
   `(1/|pairs(x)|)·Σmax(−s_kl, 0)`, сума **лише негативних** когнітивних
   пар. Код рахував `1.0 - metrics.FCogn` — не еквівалентно: FCogn
   усереднює і позитивні, і негативні пари `(s+1)/2`, тож `1-FCogn`
   завищує ризик, коли в розкладі є позитивні (синергетичні) пари.
   Виправлено: новий `CInterfCalculator.cs` (аналогічний
   `FcognCalculator.cs`, але `Math.Max(-s, 0)` замість `(s+1)/2`, і
   дефолт `0.0` замість `0.5` при відсутності пар — "немає даних" для
   суто негативної метрики означає "нуль інтерференції", не
   "нейтрально"), нове поле `TimetableMetrics.CInterf`, обчислюється в
   `ObjectiveFunctionService.Evaluate` поруч з `FCogn`, скопійовано в
   `Timetable.DeepClone`. `RiskModelService.Calculate` тепер читає
   `metrics.CInterf` напряму. Побічно знайдено й виправлено той самий
   день: `WhatIfEndpoints.ToResponse` мав **третю, окрему** копію
   формули Risk(x) (той самий баг `1-FCogn` + відсутній доданок
   `δ4·Rchange`), яка розходилася навіть із власним
   `WhatIfService.BuildResult` (він уже коректно викликав
   `RiskModelService.Calculate`) — HTTP-відповідь `/api/whatif/*`
   показувала інший risk, ніж рядок `Explanation` того самого запиту.
   Виправлено — `ToResponse` тепер теж викликає
   `RiskModelService.Calculate`/`FStable` замість дублювання формули.
6. **§3.5 каже "шість типів сценаріїв «що-якщо»", `WhatIfService.cs`
   реалізує 10** — не суперечність, а неповний опис. Шість описаних
   типів мапляться 1:1 на `InstructorUnavailable`,
   `InstructorConstraintApplied`, `GroupConstraintApplied`,
   `HealthLimitationApplied`, `RoomCapacityInsufficient`, `ModeChanged`
   (усі 6 реалізовані буквально). Ще 4 методи —
   `RoomUnavailable`/`GroupUnavailable`/`DisciplineMoved`/
   `WeightsChanged` — практичні розширення того самого механізму, не
   описані текстом. Код не чіпав (робочі, протестовані сценарії),
   уточнив `07_chapter3_methods.md` §3.5 у дисертаційному репо: "шість
   базових типів" + речення про 4 практичні розширення в реалізації.
   Повний прогін після п.5-6 — **105/105 тестів зелені** (додано
   `Calculate_HigherInterference_ShouldIncreaseCognitiveRisk`).

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
- [x] **Тести на Risk/Explanation** — зроблено 2026-08-12, див.
  "Розбіжності" п.4 вище. 104/104 тестів зелені.
- [x] **README оновлено 2026-08-12** — таблиця fixture-ів перебудована
  з нуля за реальним TRX-звітом (раніше показувала лише 9 старих
  fixture і суму 52, тоді як фактично вже 71 і 8 більше fixture),
  тепер 14 fixture, 104 тести, відсортовані за кількістю тестів.
- [ ] **Наступний крок — Фаза 2**: інтеграція актуалізованого ядра
  (AMD/SRA/IA/Conformance) у робочу систему диспетчеризації. Цільова
  система знайдена й детально вивчена 2026-08-12 — `D:\Git\university`
  (репозиторій "University", .NET 10 Blazor Server, публічний портал
  ХАІ). Повний аудит — розділ "Інтеграція з University" нижче.

## Інтеграція з University (Фаза 2, аудит 2026-08-12)

Цільова продакшн-система — `D:\Git\university` (свій `CLAUDE.md` +
`agents.md` + `EPOS.md` там, детальний технічний опис). Два
Blazor Server застосунки (`Education`, `Epos`) + спільна бібліотека
`Shared`, SQL Server (`UniversityContext`, EF Core, ID — вручну
призначені `int`, без auto-increment і без EF-міграцій).

### Де саме "ручна диспетчеризація" (об'єкт інтеграції)

`Education/Pages/Dispatcher/ScheduleGridPage.razor` (1018 рядків) —
drag-and-drop grid-редактор: диспетчер тягне рядок навчального
доручення (`Grouping`) на клітинку (день, пара, чисельник/знаменник),
підтверджує кімнату й тип тижня в діалозі
(`ScheduleGridDropDialog.razor`) → записується `GroupingLesson`.
**Єдина автоматична перевірка на весь файл** —
`GetMiniGridCellClass()` фарбує клітинку `winforms-mini-conflict`,
якщo group/instructor вже зайняті о цьому слоті (простий boolean
occupied/free, без жодної цільової функції, хронотипу чи ваг). Це
підтверджує документовану в дисертації тезу "жоден з розглянутих
підходів не враховує хронотип/психологічне навантаження/когнітивну
сумісність" — і в самій цільовій продакшн-системі теж, не лише в
конкурентних LMS з §1.2.

`Shared/Services/DispatchingService.cs` — наразі лише READ (один
метод `GetGroupingsDetailsAsync`, читає view `GroupingsView`); немає
жодної write/optimize логіки — усе пише вручну сам
`ScheduleGridPage.razor`.

Паралельно існує **друга, старіша схема розкладу**: `SchLesson` +
`LessonGroup`/`LessonRoom`/`LessonEmployee`, наповнювана з
Excel/XML-імпорту (`Education/Services/ScheduleService.cs`),
відображається на `/union/schedule/*`. Нова `Grouping`-based схема
(dispatcher grid) відображається окремо на `/schedule/grouping`
(`GroupingSchedulePage.razor`) — **дві паралельні системи розкладу
співіснують**, ще не об'єднані. З'ясувати при переході до інтеграції,
яка з них буде цільовою для інжекції AMD/SRA/IA (ймовірно
`Grouping`-based, бо саме там ручний диспетчерський workflow).

### Мапінг сутностей University ↔ AnthroDispatch

**Усі геп-точки нижче звірені й підтверджені напряму через SQL Server
2026-08-12** (не лише за кодом `Shared/Dbo/**`) — повна схема БД,
розбіжності з наявною документацією й масштаб даних (кількість
рядків у кожній таблиці) — `D:\Git\university\DATABASE.md`.

| University (`Shared/Dbo`) | AnthroDispatch (`Domain/Entities`) | Примітка |
|---|---|---|
| `Grouping` (LessonType, Hrs1/Hrs2, DepartmentId, Term, DegreeId) | `TeachingAssignment` (GroupId, InstructorId, DisciplineId, ClassType, RequiredPeriods) | Найближчий аналог, але `Grouping` — множинний (m:m через `GroupingGroup`/`GroupingEmployee`), `TeachingAssignment` — одинарний. **4978 рядків у БД.** |
| `GroupingGroup`/`GroupingEmployee` (m:m join) | `ScheduledClass.GroupIds`/`InstructorIds` (вже є, "Multi-group/multi-instructor dispatch flow additions") | **Позитивна знахідка**: прототип уже спроєктований під множинність груп/викладачів — саме під такий кейс, як University. Мінімальне тертя тут. |
| `GroupingLesson` (Cycle, Day, Pair, RoomId, isPersonal) | `ScheduledClass` (Slot = TimeSlot(Day, Period), RoomId) | **Розбіжність**: `Cycle` (1=чисельник/2=знаменник/3=обидва — двотижнева ротація розкладу ЗВО) **не має аналога** в `TimeSlot`. `TimeSlot` моделює лише (Day, Period) без поняття парного/непарного тижня. Реальний домен-геп, не дрібниця — потрібне розширення `TimeSlot` або окремий вимір x[d][s][g][cycle]. **Лише 4 рядки в БД** — новий dispatcher-grid pipeline практично не використовується в продакшні (див. "Важливий висновок" в `DATABASE.md`); уточнити з користувачем, чи це справді цільова таблиця для інтеграції, чи лише пілот. |
| `DboGroup` (cohort1, Coursenum, EduForm, +IsChina/OldId/IsArchive) | `AcademicGroup` (Chronotype, MeanMeqScore, AverageAge...) | **Геп, підтверджено на рівні БД**: `Groups`-таблиця (761 рядок) не має жодного хронотипового/MEQ поля. Дані профілю (§3.1) доведеться або збирати окремим опитуванням і зберігати в нових таблицях, або розширювати `DboGroup`. |
| `Employee` (DepartmentId, PositionId, IsBusy) | `Instructor` (Chronotype, MeqScore, MaxClassesPerDay, MaxConsecutiveClasses) | Той самий геп, підтверджено на рівні БД (890 рядків, жодного профільного поля). |
| `Discipline` (FullName, ShortName, English, +SchedId) | `Discipline` (ProcessType, LoadLevel, Domain) | **Геп, підтверджено на рівні БД**: 4007 дисциплін, жодної когнітивної класифікації. Матрицю S (§2.2) нема з чого будувати без нової класифікації. |
| `SysRoom` (лише Id/Name/DbCampus/Slug — 4 поля, підтверджено) | `Room` (Type, Capacity) | **Геп, підтверджено на рівні БД**: 251 аудиторія, жодна не має ні місткості, ні типу. Сценарій `RoomCapacityInsufficient` (§3.5) не можна перевірити на реальних даних без нової таблиці/колонок. |
| ID: `int`, вручну (`MAX(Id)+1`), без auto-increment | ID: `Guid` | Наскрізна розбіжність типів ID по всій моделі — потрібен шар трансляції `int ↔ Guid` на межі інтеграції (внутрішня деталь адаптера при in-process інтеграції, див. нижче). Виняток: `Announcements.Id` — єдина `uniqueidentifier`-таблиця в усій БД University. |

### Архітектурне рішення (зафіксовано користувачем 2026-08-12)

Не REST-сервіс/HTTP — **in-process інтеграція, спільна пам'ять/контекст**
у складі самого Blazor Server застосунку (`Education` та/або `Epos`).
AnthroDispatch.Application/Domain (GA, SRA, IA) підключається як
project reference до `Education.csproj`/`Epos.csproj` (найімовірніше
через `Shared`, яку вже реферують обидва застосунки — один сервіс,
не дублювання), реєструється у DI як Scoped/Singleton-сервіс за тим
самим патерном, що й `DispatchingService`/export-сервіси, і працює в
тому ж процесі й тому ж запиті, що й решта University — без
HTTP-round-trip, без окремого деплою. `AnthroDispatch.Api` (Minimal
API ендпоїнти) лишається для автономного research-прототипу/тестів,
не є частиною цього шляху інтеграції.

**Наслідок для розбіжності ID (`int` vs `Guid`, див. таблицю вище):**
раз рушій оптимізації працюватиме в одному процесі з `UniversityContext`
і читатиме звідти дані напряму (не через API-контракт із власним
простором ID), трансляція `int ↔ Guid` — внутрішня деталь
адаптер-шару всередині AnthroDispatch-інтеграції, а не публічний
контракт. Обирати спосіб (детермінований `GuidV5` з `int`, чи власний
словник відповідностей) — при написанні цього адаптера, не зараз.

**Крок 1 — лише адитивно, лише для адміна (зафіксовано користувачем
2026-08-12):** перший інкремент інтеграції — це **тільки** нові
таблиці/колонки в БД University і нові адмін-only UI-елементи для
введення/перегляду цих даних (хронотип/MEQ, когнітивна класифікація
дисциплін, профіль аудиторій), **без** зміни існуючої поведінки
(`ScheduleGridPage.razor` і решта — не чіпати на цьому кроці).
Конкретні задачі з підключення цих даних до реальної оптимізації
диспетчер-гріда користувач визначить окремо пізніше — не випереджати
подіями, не проєктувати той крок заздалегідь.

**Схема БД для Кроку 1 — застосована 2026-08-12 (не лише план):**
4 SQL-скрипти в `D:\Git\university\sql\anthropocentric-dispatch\`
(`001_person_chronotype_profile.sql`, `002_discipline_cognitive_
profile.sql`, `003_discipline_cognitive_compatibility.sql`,
`004_room_profile.sql`), кожен ідемпотентний (`IF NOT EXISTS`).
**Застосовано на `DefaultConnection`** (`lpc:(local)`, БД
`university` — локальна/dev, НЕ `RemoteProductionConnection`) з
дозволу користувача, перевірено (`INFORMATION_SCHEMA` після
застосування — усі 4 таблиці на місці, повторний запуск без помилок).
**На проді — ще ні**, скрипти лежать у репо для ручного застосування,
коли це стане можливим (`agents.md` University: "схема керується
зовні, без EF-міграцій"). Деталі рішень (naming, `IDENTITY` замість
ручних ID, мапінг enum-значень на C#-ordinal) — `sql/
anthropocentric-dispatch/README.md` у репо University.

Створені таблиці (усі — нові, FK на існуючі `Persons`/`Disciplines`/
`SysRoom`, самі існуючі таблиці не змінено):
- `PersonChronotypeProfile` (PersonId, MeqScore, Chronotype 1-5,
  SurveyDate — історія, "поточний" профіль = останній SurveyDate).
- `DisciplineCognitiveProfile` (DisciplineId — унікальний, ProcessType/
  LoadLevel/Domain).
- `DisciplineCognitiveCompatibility` (FromDisciplineId/ToDisciplineId/
  Score — спрямована пара, пряма відповідність
  `CognitiveCompatibility` з прототипу).
- `RoomProfile` (RoomId — унікальний, Capacity, RoomType).

**Ще НЕ зроблено з Кроку 1:** адмін-сторінки (Blazor UI) для
введення/перегляду цих даних, EF-моделі (`Shared/Dbo/**`) для нових
таблиць, будь-яке заповнення даними (seed) — таблиці порожні.

### Відкриті питання (не вигадувати, уточнювати з користувачем при постановці конкретних задач)

- Формат/джерело хронотипових даних (MEQ-опитування) для реальних
  студентів/викладачів ХАІ — новий процес збору чи є вже десь
  (напр. Moodle-плагін, окрема анкета)?
- Хто класифікує дисципліни для матриці S (§2.2) — експертна разова
  розмітка кафедрами чи автоматизація?
- Чи розширювати `TimeSlot` під `Cycle` (чисельник/знаменник), чи
  генерувати два незалежні розклади (кожен тиждень окремо)? —
  не актуально для Кроку 1 (адитивний, без чіпання диспетчер-гріда),
  але залишається відкритим для наступних кроків.
- Розміщення нових адмін-сторінок — `Education` (де вже є
  `Pages/Management/**`/`Pages/Dispatcher/**`) чи `Epos` (де вже є
  `Components/Admin/**`); `Shared/Components/Management/**` доступний
  з обох.
- Коли й як застосувати ті самі 4 скрипти на
  `RemoteProductionConnection`.
- `Education` чи `Epos` (чи обидва) — фінальне розміщення нових
  адмін-сторінок Кроку 1.

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
