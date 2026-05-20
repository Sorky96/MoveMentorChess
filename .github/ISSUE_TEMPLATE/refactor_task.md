---
name: 🔧 Refactor Task
about: Plan a refactoring without changing observable behaviour
labels: refactor, technical-debt
assignees: ''
---

## 🎯 User Goal / Motivation

> Why is this refactoring needed? What problem does it solve or what value does it bring?

_As a developer, I want to [change X] so that I can [more easily / safely / quickly] [achieve Y]._

---

## 📍 Scope of Changes

> Which files / classes / modules are affected by this refactoring?

| File / Class | Current Responsibility | Target Responsibility |
|---|---|---|
| `...` | ... | ... |
| `...` | ... | ... |

---

## ✅ Acceptance Criteria

> The refactoring is done when:

- [ ] External behaviour is **identical** to before (all tests pass)
- [ ] Build passes without warnings: `dotnet build ... --no-restore -m:1`
- [ ] Tests pass: `dotnet test ... --no-restore -m:1 --verbosity minimal`
- [ ] Code has passed review with no new `dotnet_diagnostic` violations
- [ ] `CLEAN_CODE_AUDIT.md` is updated (if applicable)

---

## 🚫 Non-goals

> What we are **not** changing in scope of this task.

- Not adding new features
- Not changing the public API / class contracts
- Not refactoring X (separate ticket)

---

## 🏗️ Refactoring Strategy

> Describe the step-by-step approach (incremental preferred over big-bang).

### Steps

1. [ ] Extract `...` into a separate class / method
2. [ ] Move `...` logic from `View` to `ViewModel` / `Service`
3. [ ] Remove dead code: `...`
4. [ ] Update DI / `AppCompositionRoot`
5. [ ] Run build and tests after each step

> 💡 **Tip:** Run `dotnet build` after each step — don't wait until the end.

---

## ⚠️ Risks

> What could go wrong?

- [ ] Risk of behaviour change in: ...
- [ ] Risk of merge conflict with parallel PRs: ...
- [ ] Risk of breaking Avalonia bindings: ...
- [ ] Other: ...

---

## 🧪 Suggested Tests

> Tests that will confirm behaviour has not changed.

- Existing tests to run: `...Tests`
- New regression tests: scenario ...
- Manual verification: ...

---

## 📎 Related

> Links to `CLEAN_CODE_AUDIT.md`, related issues, PRs, ADRs.

- Audit: ...
- Issue: #...
- ADR: ...
