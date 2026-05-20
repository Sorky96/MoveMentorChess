---
name: 🔧 Refactor Task
about: Zaplanuj refaktoryzację bez zmiany zachowania
labels: refactor, technical-debt
assignees: ''
---

## 🎯 Cel użytkownika / motywacja

> Dlaczego ta refaktoryzacja jest potrzebna? Jaki problem rozwiązuje lub jaką wartość niesie?

_Jako programista, chcę [zmienić X], żeby [łatwiej / bezpieczniej / szybciej] [osiągnąć Y]._

---

## 📍 Zakres zmian

> Które pliki / klasy / moduły są objęte refaktoryzacją?

| Plik / Klasa | Obecna odpowiedzialność | Docelowa odpowiedzialność |
|---|---|---|
| `...` | ... | ... |
| `...` | ... | ... |

---

## ✅ Acceptance Criteria

> Refaktoryzacja jest skończona, gdy:

- [ ] Zachowanie zewnętrzne jest **identyczne** z poprzednim (testy przechodzą)
- [ ] Build przechodzi bez warningów: `dotnet build ... --no-restore -m:1`
- [ ] Testy przechodzą: `dotnet test ... --no-restore -m:1 --verbosity minimal`
- [ ] Kod przeszedł review i nie ma nowych naruszeń `dotnet_diagnostic`
- [ ] `CLEAN_CODE_AUDIT.md` jest zaktualizowany (jeśli dotyczy)

---

## 🚫 Non-goals

> Czego **nie** zmieniamy przy tej okazji.

- Nie dodajemy nowych funkcjonalności
- Nie zmieniamy publicznego API / kontraktu klas
- Nie refaktorujemy X (osobny ticket)

---

## 🏗️ Strategia refaktoryzacji

> Opisz podejście krok po kroku (incremental preferred over big-bang).

### Kroki

1. [ ] Wyodrębnij `...` do osobnej klasy / metody
2. [ ] Przenieś logikę `...` z `View` do `ViewModel` / `Service`
3. [ ] Usuń martwy kod: `...`
4. [ ] Zaktualizuj DI / `AppCompositionRoot`
5. [ ] Uruchom build i testy po każdym kroku

> 💡 **Tip:** Po każdym kroku uruchom `dotnet build` — nie czekaj do końca.

---

## ⚠️ Ryzyka

> Co może pójść nie tak?

- [ ] Ryzyko zmiany zachowania w: ...
- [ ] Ryzyko konfliktu merge z równoległymi PR: ...
- [ ] Ryzyko naruszenia Avalonia bindings: ...
- [ ] Inne: ...

---

## 🧪 Sugerowane testy

> Testy, które potwierdzą, że zachowanie się nie zmieniło.

- Istniejące testy do uruchomienia: `...Tests`
- Nowe testy regresyjne: scenariusz ...
- Weryfikacja manualna: ...

---

## 📎 Powiązane

> Linki do `CLEAN_CODE_AUDIT.md`, powiązanych issues, PR, ADR.

- Audit: ...
- Issue: #...
- ADR: ...
