# OpenMU Upstream Strategy — Git Repository Audit

**Date:** 2026-11-22  
**Scope:** Phase 0 commits → Phase 1 remotes/fork/`main` → **Phase 2 upstream sync audit** (read-only)  
**Constraint (Phase 2):** `git fetch upstream` allowed; **no** merge / rebase / cherry-pick / code changes / push of sync.

**Verdict:** Phase 0+1 **DONE**. Phase 2 audit: ProMU `main` @ `ec762d271` is **5 commits ahead / 6 behind** `upstream/master` (`b02b5a71c`). **No same-file overlap** with ProMU overlay; `git merge-tree` → no conflict markers. Sync looks **low-risk / high-value** (esp. Lord’s Mix). Merge still requires explicit Phase 3 go.

---

## 1. Текущее Git-состояние

| Параметр | Факт |
|----------|------|
| Repo path | `C:\dev\mu-lab\OpenMU` |
| `origin` (fetch/push) | `https://github.com/MUnique/OpenMU.git` |
| `upstream` remote | **отсутствует** |
| Текущая ветка | `master` (единственная локальная) |
| Tracking | `master` → `origin/master` |
| `HEAD` | `45678e37f1f55d5622227b50004c10a17feb5092` |
| Subject | `feat(items): items which expire… (#1548)` |
| Author / date | pllanosclaria · 2026-11-20 |
| Describe | `v0.11.0-400-g45678e37f` |
| Ahead / behind `origin/master` (локальный кэш) | **0 / 0** |
| Reflog | единственная запись: `clone` 2026-11-21 18:02 +0300 from MUnique |
| Stash | пусто |
| Собственные commits с `promu` в сообщении | **нет** (ни на одной известной ветке) |

### Remotes / fork

- **Fork ProMU / `blissthemes/OpenMU` в remotes не настроен.**
- Публичный поиск по GitHub не показал fork `blissthemes/OpenMU` (lab remote `blissthemes/promu` — отдельный репозиторий docs/scripts; OpenMU в нём в `.gitignore`).
- `gh` CLI на машине **не установлен** — точную проверку «есть ли приватный fork» через API не выполняли.
- Сейчас любой `git push origin …` целил бы в **чужой** `MUnique/OpenMU` (будет отклонён без write access) — push-пути для ProMU **нет**.

### Локальные / remote-tracking ветки

- Локально: только `master`.
- Remote-tracking: `origin/master` + большой набор чужих `origin/claude/*`, `origin/feat/*`, `origin/fix/*`, `origin/copilot/*` (кэш после clone; **не** ProMU-ветки).
- Веток `main`, `integration/upstream-sync`, `feature/*` **нет**.

### Dirty working tree (критично)

На момент аудита (`git status --porcelain`):

| Тип | Кол-во (записи status) | Суть |
|-----|------------------------|------|
| Modified (`M`) | **29** | правки поверх upstream + lab `AGENTS.md` |
| Untracked (`??`) | **11** | новые ProMU Security / rewards / migration / tests |
| Deleted | 0 | — |
| Staged | 0 | ничего не в индексе |

`git diff --stat` по отслеживаемым файлам (без untracked): порядка **+514 / −25** в 28 файлах с реальным содержимым (плюс `Map.js` — см. ниже).

**Ни одно ProMU-изменение не закреплено commit’ом.**

---

## 2. Собственные изменения (относительно оригинального OpenMU)

База = stock OpenMU `@45678e37f`. Всё ниже — **только working tree**.

### A. Lab meta (не runtime)

| Путь | Суть |
|------|------|
| `AGENTS.md` | Указатель на родительские lab docs (`../AGENTS.md`, `PROJECT_CONTEXT`, …) |

### B. Stage AC — anti-cheat / defense (основной объём)

Новые каталоги/файлы (untracked):

- `src/ConnectServer/Security/` — Connect challenge
- `src/GameLogic/PlugIns/Security/` — ConnectionGuard, MacroBehavior, AccountTransactionLock, SecurityViolationBus
- `src/GameServer/Security/` — SecureSession, PacketRateLimiter, PromuSecurityPackets
- `src/Network/Security/` — `PromuNetworkKeys`, `NetworkKeyEnvironment`
- Persistence: `SecurityViolation` + repository + AdminPanel migration `20261122160000_AddSecurityViolations*`
- `tests/MUnique.OpenMU.Tests/PromuAntiCheatSecurityTests.cs`

Модификации stock (modified):

- SpeedHack harden: `SpeedHackDetect*`, `AttackSpeedCheck`, `AttackSpeedModel`, `SpeedHackViolationRecorder`
- InstantMove / move gate: `CharacterMoveBaseHandlerPlugIn.cs`
- Item/trade hooks + transaction lock: `Drop/Pickup/Move/ItemCraft/TradeButtonAction`
- Wiring: `Program.cs`, `GameServer.cs`, `RemotePlayer.cs`, AdminPanel EF context/DI/snapshot
- `MUnique.OpenMU.GameLogic.csproj` — `InternalsVisibleTo` для тестов

Канон по смыслу (lab): `C:\dev\mu-lab\docs\implementation\stage-AC\`.

### C. Crypto keys (ProMU поверх stock encryptors)

- `OpenSourceClientNetworkEncryptionFactoryPlugIn` → ключи через `PromuNetworkKeys`
- `Xor3Encryptor` / `Xor3Decryptor` — ctor с кастомным 3-byte key
- Login password XOR: `LogInHandlerPlugIn` → `PromuNetworkKeys.Xor3Keys`
- Login window → `SecureSessionService.SendChallengeAsync`
- ConnectServer hello path → `ConnectChallengeHandler`

Поведение: `PROMU_CRYPTO_ENV=local` ≈ stock keys; `test`/`prod` — внешние ключи (env/path). Это **совместимо с lab**, но hard-fork риск при sync, если touch’нутся те же файлы upstream.

### D. Website ↔ OpenMU bridge

| Путь | Суть |
|------|------|
| `src/Web/AdminPanel/API/InternalRewardsController.cs` | Internal rewards API (`/api/internal/rewards`), защита `PROMU_INTERNAL_API_SECRET` + API key |

Отдельного большого «website identity» форка OpenMU в этом дереве нет: портал в `website/` ходит в stock Website API; этот controller — ProMU overlay для rewards.

### E. Шум / сомнительное

| Путь | Оценка |
|------|--------|
| `src/Web/Map/wwwroot/js/MUnique.OpenMU.Web.Map.js` | В status как `M`, но `git diff --numstat` / ignore-CR пустой → почти наверняка **только CRLF/line-ending**, не функциональный ProMU-diff |
| `tests/.../ServerToClientPacketTests.cs` | +58 строк — вероятно связано с AC/packet extensions; проверить перед commit, не смешивать с «чужим» мусором |

### F. Что уже является отдельными commits?

**Ничего.**  
`HEAD` = чистый upstream commit. ProMU = uncommitted overlay. Это главный архитектурный дефект текущего Git-состояния.

---

## 3. Риски потери изменений

| Риск | Уровень | Почему |
|------|---------|--------|
| `git checkout -- .` / `git restore` / `git reset --hard` | **Критический** | Снесёт все 29+11 ProMU правок без следа в history |
| `git clean -fd` | **Критический** | Удалит все untracked `Security/`, migration, InternalRewards, tests |
| `git pull` / `merge` / `rebase` на грязное дерево | **Высокий** | Конфликты + риск abort с потерей контекста; untracked не в merge |
| Fetch + accidental fast-forward при «чистом» ошибочном discard | **Высокий** | Сейчас дерево «выглядит» как tracking upstream; люди часто «чинят» pull’ом |
| Нет fork / нет push remote | **Высокий** | Нет off-machine backup исходников OpenMU ProMU |
| IDE / antivirus / disk cleanup | **Средний** | Untracked легче потерять, чем commits |
| Смешение CRLF `Map.js` в один commit с AC | **Низкий** | Засоряет историю, усложняет будущие merge |
| Подключение `upstream` remote **само по себе** | **Низкий** | Добавление remote не меняет файлы; опасно только последующее merge/rebase без commits |

**Вывод:** до первого набора ProMU commits + push на личный fork любое обновление upstream — игра с огнём.

---

## 4. Рекомендуемая структура веток

Целевая модель (совпадает с запросом; имена можно зафиксировать так):

```text
upstream/master     ← MUnique/OpenMU (read-only sync source)
origin/main         ← ваш fork: стабильная ProMU-линия (то, что крутит X10)
main                ← локальный стабильный ProMU (tracks origin/main)
integration/upstream-sync  ← временная ветка проверки обновлений
feature/<topic>     ← разработка (website bridge, AC harden, rates, …)
```

### Роли

| Ветка | Назначение | Правила |
|-------|------------|---------|
| `main` | Стабильная ProMU-версия для lab/prod X10 | Только merge из проверенных `feature/*` или из `integration/upstream-sync` после теста |
| `integration/upstream-sync` | Подтянуть `upstream/master`, разрешить конфликты, прогнать тесты | Никогда не деплоить напрямую; после OK → merge в `main` |
| `feature/*` | Одна тема = одна ветка | Короткие PR в `main`; не мешать AC + rates + website в одном PR |
| `upstream/master` | Зеркало оригинала | Только fetch; не commit’ить сюда |

### Замечание про `master` vs `main`

Upstream OpenMU использует **`master`**. ProMU лучше держать стабильную линию на **`main`**, чтобы:

1. не путать «я на stock» и «я на ProMU»;
2. не делать случайный push мыслей в сторону upstream naming;
3. явно видеть `main...upstream/master` как delta ProMU.

Первый ProMU commit: создать `main` от текущего `45678e37f` **после** (или вместе с) коммитом overlay — не раньше, чем изменения сохранены.

### Много миров (X1/X10/X100) в будущем

- **Один** Git-форк OpenMU ProMU; разные миры = разные runtime/DB/config (как уже решено в lab Stage 9/11), не отдельные долгоживущие fork-ветки на мир.
- Если когда-то понадобится мир-специфичный код — `feature/world-x10-rates` → merge в `main`, конфиг per-instance; избегать `main-x10` / `main-x1` как вечных расходящихся линий.

---

## 5. План первоначальной настройки upstream / origin

**Не выполнять в рамках этого аудита.** Порядок, когда owner даст go:

### Phase 0 — сохранить ProMU (обязательно первым)

1. Убедиться, что рабочее дерево не трогают другие агенты/IDE.
2. Снять backup working tree (архив каталога или `git stash push -u` **только если** понимаете, что stash — локальный; лучше сразу commits).
3. Разложить изменения по логическим commits на новой ветке, например:
   - `chore(lab): AGENTS.md pointer`
   - `feat(ac): security violations + SpeedHack harden + InstantMove gate + …`
   - `feat(network): PromuNetworkKeys injection`
   - `feat(website): InternalRewardsController`
   - отдельно решить судьбу `Map.js` (не коммитить, если CRLF-only)
4. Локально: `dotnet build` / `dotnet test --filter PromuAntiCheatSecurityTests` до push.

### Phase 1 — создать fork и remotes

1. На GitHub (под `blissthemes` или org ProMU): **Fork** `MUnique/OpenMU`.
2. В локальном клоне (концептуально):
   - переименовать текущий `origin` → `upstream` **или** добавить `upstream` = MUnique и сменить `origin` на fork;
   - целевой вид:

```text
origin    https://github.com/<you>/OpenMU.git          # fork (push)
upstream  https://github.com/MUnique/OpenMU.git        # read-only
```

3. Запушить `main` (с ProMU commits) на `origin`.
4. Защитить `main` на GitHub (no force-push).

### Phase 2 — зафиксировать pin

1. Записать в lab `SOURCE_VERSION_MATRIX` / `PROJECT_CONTEXT`:  
   `upstream_base = 45678e37f` + `promu_main_sha = <after first push>`.
2. Тег опционально: `promu-base-45678e37f` на первом ProMU commit поверх pin.

### Phase 3 — только потом upstream sync

Подключать регулярный sync **после** Phase 0–2. Сам факт `git remote add upstream` безопасен; опасны merge/rebase без commits и без fork backup.

---

## 6. Регламент регулярного обновления

Рекомендуемый цикл (раз в 1–2 недели или по релизу/критическому fix upstream):

1. **Clean gate:** `main` чистый; все ProMU work — в commits; CI/local green.
2. `git fetch upstream` (и `origin`).
3. `git switch -c integration/upstream-sync origin/main` (или от актуального `main`).
4. `git merge upstream/master` (**предпочтительно merge**, не rebase долгоживущего `main` — проще аудит и откат).  
   Альтернатива для очень линейной истории: rebase только коротких `feature/*`.
5. Разрешить конфликты с приоритетом:
   - не потерять `Security/`, keys injection, InternalRewards;
   - stock SpeedHack/CS IP — переносить upstream-улучшения, не откатывать ProMU harden без review (см. Stage AC audit).
6. Сборка + тесты (раздел 7).
7. Lab smoke: Startup `-autostart`, Connect `44406`, login `test1`, вход в мир.
8. Merge `integration/upstream-sync` → `main`; push `origin`; удалить integration-ветку.
9. Обновить lab docs: `SOURCE_VERSION_MATRIX`, `CURRENT_STATE` (новый upstream pin).

### Чего не делать

- Не `git pull` на грязном дереве.
- Не rebase `main` после того, как он запушен и от него живут feature-ветки (без явной команды).
- Не коммитить секреты (`PROMU_*` keys, `DB_ADMIN_PW`, connection strings).
- Не считать DeepWiki/docs источником правды при конфликте — смотреть pinned source.
- Не смешивать «sync upstream» и «новая фича» в одном PR.

---

## 7. План тестирования после обновления

Минимальный gate перед merge в `main`:

### Build / unit

```powershell
dotnet build C:\dev\mu-lab\OpenMU\src\Startup\MUnique.OpenMU.Startup.csproj
dotnet test C:\dev\mu-lab\OpenMU\tests\MUnique.OpenMU.Tests\MUnique.OpenMU.Tests.csproj --filter "FullyQualifiedName~PromuAntiCheatSecurityTests"
```

При затронутых Network packets — также packet tests project.

### Runtime smoke (lab)

| Шаг | Ожидание |
|-----|----------|
| Startup + AdminPanel `:80` | Up |
| Connect Open Source `44406` | Started |
| Server 0 | Started |
| MuMain → login `test1` → character → world | OK |
| `PROMU_CRYPTO_ENV=local` | Клиент со stock/local keys коннектится |
| Internal rewards (если ключ настроен) | Контроллер отвечает; без секрета — отказ |
| SpeedHack plugin | Загружен; violations path не падает |
| Нет `-reinit` | DB/accounts целы |

### Regression focus после конфликтов в файлах

- `OpenSourceClientNetworkEncryptionFactoryPlugIn`, Xor3*
- `CharacterMoveBaseHandlerPlugIn` (InstantMove)
- `SpeedHackDetect*`
- `Program.cs` / AdminPanel DI
- `InternalRewardsController`

### Website

- `website` API: `/api/worlds`, login-world / OpenMU Website API status для X10/X1 — без регрессий (портал не внутри этого git, но зависит от stock Website API + internal rewards).

---

## Phase 0 — Local thematic commits (2026-11-22)

### Commit plan executed

| Commit | SHA (short) | Файлы | Назначение | Риски |
|--------|-------------|-------|------------|-------|
| 1 | `629cb723a` | 46 files (Security overlays, SpeedHack harden, crypto injection, violations EF, InstantMove, CS IP default 6, AC tests) | `feat(security): add ProMU defense layer` | Lab HMAC fallback string is intentional non-prod placeholder; real secrets via env only |
| 2 | `3f20ab3da` | `InternalRewardsController.cs` | `feat(rewards): add internal rewards API` | Secret from `PROMU_INTERNAL_API_SECRET` env — not hardcoded |
| 3 | `7dfe735f0` | `AGENTS.md` + audit README | `docs(promu): document local integration` | Docs only |
| 3b+ | docs follow-ups on tip | audit README only | SHA/table polish (`1ade7fcf0` and later) | Docs only; tip = `git rev-parse HEAD` |

### Secrets / CRLF inspection (Stage A)

- **No production credentials** found in committed trees.
- `PromuNetworkKeys`: local = stock `DefaultKeys`; test/prod load from env/path.
- `SecureSessionOptions` / ConnectChallenge: fallback `"promu-local-session-hmac-not-for-prod"` + `PROMU_SESSION_HMAC_SECRET` override — lab placeholder, not a real prod secret.
- `InternalRewardsController`: reads `PROMU_INTERNAL_API_SECRET` from environment only.
- **Excluded:** `src/Web/Map/wwwroot/js/MUnique.OpenMU.Web.Map.js` — CRLF-only (`diff --numstat` empty with ignore-CR).
- **Excluded:** `tests/.../ServerToClientPacketTests.cs` — LuckyCoin packet size stubs, **unrelated** to ProMU AC/rewards (left dirty intentionally).

### Final Git check (after Phase 0)

| Check | Result |
|-------|--------|
| Branch | `master` (no new branches); tip via `git rev-parse HEAD` |
| Ahead of `origin/master` | **4+** ProMU commits (local only; not pushed) |
| Merge-base with pin | `45678e37f` |
| `git diff --cached` | empty |
| Untracked | none |
| Remaining dirty | `Map.js` (CRLF-only status noise), `ServerToClientPacketTests.cs` (+58 LuckyCoin stubs) |
| `origin` | still `https://github.com/MUnique/OpenMU.git` |

### Ready for Phase 1?

| Prerequisite | Status |
|--------------|--------|
| ProMU code in commits | **Yes** |
| Thematic split | **Yes** (security / rewards / docs) |
| No secrets in commits | **Yes** (inspected) |
| Personal GitHub fork | **No** — Phase 1 |
| Remotes `origin`=fork / `upstream`=MUnique | **No** — Phase 1 |
| Branch `main` / `integration/upstream-sync` | **No** — Phase 1 |
| Push backup | **No** — Phase 1 (local-only until then) |

---

## Phase 1 — Remotes / main / fork backup (DONE)

| Item | Result |
|------|--------|
| `origin` | `https://github.com/blissthemes/OpenMU.git` |
| `upstream` | `https://github.com/MUnique/OpenMU.git` |
| `main` / `integration/upstream-sync` | created @ `ec762d271`; pushed to fork |
| `master` | kept locally |
| HTTPS push | works after Contents:Write on PAT (SSH used for first push) |

---

## Phase 2 — Upstream Sync Audit (2026-11-22, read-only)

### Snapshot

| Ref | SHA | Note |
|-----|-----|------|
| ProMU `main` / `integration/upstream-sync` / local `master` | `ec762d271` | Phase 0 tip |
| Merge-base | `45678e37f` | pin when ProMU branched |
| `upstream/master` (after `git fetch upstream`) | `b02b5a71c` | 2026-11-21 |
| `main...upstream/master` | **5 ahead / 6 behind** | diverged histories |

Dirty (unchanged by audit): `Map.js` (CRLF), `ServerToClientPacketTests.cs` (LuckyCoin +58).

### 1. New upstream commits (`main..upstream/master`)

| SHA | Date | Subject | Importance for ProMU |
|-----|------|---------|----------------------|
| `a81229db3` | 2026-11-21 | fix: run periodic tasks on every GS (#1588) | **High** — multi-GS correctness (save/weather/waiting items/cash playtime) |
| `717abffa6` | 2026-11-21 | fix(bots): wait for reset before spawn (#1587) | **Medium** — loadtest / bots (Stage AC LOADTEST) |
| `9b6946c97` | 2026-11-21 | fix(web): search input standardize (#1585) | **Low** — AdminPanel UX only |
| `41d629c6f` | 2026-11-21 | feat(web): localized input toggle (#1581) | **Low** — AdminPanel; also adds LuckyCoin packet tests |
| `0997676a0` | 2026-11-21 | Lord's Mix castle senior crafting (#1571) | **High** — closes AC audit gap “Lord’s Mix not in tree”; EF migration `AddLordsMix` |
| `b02b5a71c` | 2026-11-21 | feat(web): shared pager (#1589) | **Low** — AdminPanel tables |

**Volume:** ~106 files, +10632 / −324 vs `45678e37f`.

### 2. ProMU-only commits (`upstream/master..main`)

Unchanged set: `629cb723a` security · `3f20ab3da` rewards · docs (`7dfe735f0`…).

### 3. Conflict analysis

| Check | Result |
|-------|--------|
| Same-path intersection (ProMU tree vs upstream tree) | **Empty** — no shared modified paths |
| `git merge-tree <base> main upstream/master` | **No** `CONFLICT` / `changed in both` markers |
| Direct risk to Security/ / PromuNetworkKeys / InternalRewards | **None** in this window |
| Near-miss (adjacent) | ProMU `ItemCraftAction` + `AccountTransactionLock` vs upstream `SimpleItemCraftingHandler` / `LordsMixCrafting` — **different files**; re-test craft/trade locks after merge |
| Dirty `ServerToClientPacketTests.cs` | **Identical** to upstream LuckyCoin hunk from `#1581` — not ProMU IP; discard or keep; will match after sync |
| Dirty `Map.js` | CRLF-only; ignore during sync |
| Migrations | Upstream: **EntityDataContext** `AddLordsMix`; ProMU: **AdminPanel** `AddSecurityViolations` — separate contexts |

### 4. Risk rating for a future merge into `integration/upstream-sync`

| Area | Risk |
|------|------|
| Text conflict on ProMU overlay files | **Low** |
| Semantic / runtime after Lord’s Mix + craft locks | **Medium** — smoke craft/trade |
| EF / DB apply `AddLordsMix` (no `-reinit`) | **Medium** — use OpenMU update plugins / normal migrate path only |
| AdminPanel UI churn | **Low** |
| Multi-GS periodic tasks vs lab single Server 0 | **Low** lab; **High** value when multi-GS |

**Overall:** sync is **recommended** and looks **mergeable with low conflict risk**.

### 5. Integration plan (Phase 3 — **do not execute now**)

1. Clean or stash dirty files (or leave Map.js; LuckyCoin already = upstream).
2. `git switch integration/upstream-sync` (should equal `main` @ `ec762d271`).
3. `git merge upstream/master` (**merge**, not rebase of published `main`).
4. Resolve any unexpected conflicts preserving ProMU `Security/` / rewards.
5. Build + `PromuAntiCheatSecurityTests` + Startup smoke (44406 / test1).
6. Extra: Lord’s Mix / craft path; AdminPanel loads; no `-reinit`.
7. Merge `integration/upstream-sync` → `main`; `git push origin main` + integration branch.
8. Update lab pin in `SOURCE_VERSION_MATRIX` / `CURRENT_STATE` to `b02b5a71c` (+ ProMU tip).

### 6. Phase 2 safety confirmation

- Fetched `upstream` only; **no** merge / rebase / cherry-pick / push of sync.
- Code/config runtime **not** changed by this audit (docs update in this README only).
- ProMU commits / fork branches untouched.

---

## Recommendations (now)

1. **Phase 3 (owner go):** merge `upstream/master` into `integration/upstream-sync`, test, then → `main`.
2. Prioritize absorbing **#1571 Lord’s Mix** and **#1588 per-GS periodic tasks**.
3. After sync, update Stage AC note: Lord’s Mix gap may be closed in source (still needs lab dry-run).
4. Keep sync cadence: fetch upstream → audit delta → merge via integration branch.

**PHASE 2 COMPLETE (audit only). Stopped before merge.**
