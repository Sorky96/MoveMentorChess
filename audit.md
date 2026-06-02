# Clean Code / Clean Architecture Audit

Data audytu: 2026-06-02
Punkt odniesienia: świeży `master`, commit `6eb556d`
Zakres: solucja `MoveMentorChess.sln`, struktura projektów, zależności warstw, największe klasy, miejsca o wysokim sprzężeniu, testy architektoniczne i ryzyka utrzymaniowe.

## 1. Business Analyst

### Cel biznesowy

Celem audytu jest wskazanie, czy obecna architektura pozwala bezpiecznie rozwijać aplikację MoveMentorChess bez narastania kosztu zmian w UI, analizie partii, treningu otwarć, profilach gracza i warstwie SQLite.

### Kryteria akceptacji

- Audyt wykonany na aktualnym `master`.
- Wnioski zapisane w nowym pliku `audit.md`.
- Rekomendacje są praktyczne: wskazują priorytet, ryzyko i przykładowe pliki.
- Nie wykonano refaktoryzacji przy okazji audytu.

## 2. Executive Summary

Ocena ogólna: **architektura jest w dobrym kierunku, ale nadal przejściowa**.

Największe plusy:

- Solucja jest już podzielona na sensowne obszary: `Domain`, `Analysis`, `Training`, `Profiles`, `Persistence`, `Presentation`, `Tracking`, `App`.
- Włączone są centralne ustawienia jakości: `TreatWarningsAsErrors`, `AnalysisMode=Recommended`, nullable i implicit usings.
- Istnieją testy architektoniczne pilnujące części granic i budżetów rozmiaru klas.
- W ostatnich zmianach widać dobrą tendencję do wyciągania portów, np. `IPlayerMistakeProfileSource`, runtime environment resolvery i adaptery cache.

Największe ryzyka:

- Warstwy domenowo-aplikacyjne nadal znają `Persistence`, więc kierunek zależności nie jest czysty.
- `App` i część ViewModeli nadal agregują zbyt dużo zachowań, renderowania i nawigacji.
- Statyczne globalne mechanizmy cache/store utrudniają testowanie, izolację i przewidywanie stanu.
- Część modeli prezentacji i rendererów jest Avalonia-specyficzna lub leży w `ViewModels`, co zaciera granicę między prezentacją framework-neutralną a UI.
- Testy są liczne i wartościowe, ale część z nich jest bardzo duża, co grozi wolnym, kruchym cyklem refaktoryzacji.

## 3. Stan Solucji

### Metryki

- 13 projektów w `MoveMentorChess.sln`.
- Około 486 plików C# w kodzie produkcyjnym.
- Około 77 plików C# w testach.
- Około 433 przypadków `Fact`/`Theory`.

Największe obszary po liczbie linii:

- `MoveMentorChess.App`: około 11.4k linii.
- `MoveMentorChess.Training`: około 6.7k linii.
- `MoveMentorChess.Analysis`: około 5.8k linii.
- `MoveMentorChess.Domain`: około 5.0k linii.
- `MoveMentorChessServices.Tests`: około 14.3k linii.

Największe pliki ryzyka:

- `MoveMentorChess.App/ViewModels/OpeningTrainerWindowViewModel.cs`: 2170 linii.
- `MoveMentorChess.App/ViewModels/MainWindowViewModel.cs`: 1383 linie.
- `MoveMentorChess.Training/OpeningTrainingSessionBuilder.cs`: 1168 linii.
- `MoveMentorChess.Training/OpeningTrainerWorkspaceService.cs`: 945 linii.
- `MoveMentorChess.Domain/Models/ChessGame.cs`: 860 linii.
- `MoveMentorChess.App/ViewModels/ProfileCoachSectionRenderer.cs`: 802 linie.
- `MoveMentorChess.App/Views/ProfilesWindow.axaml.cs`: 686 linii.

## 4. Architektura Warstw

### Co działa dobrze

`Domain` jest bazową warstwą dla zasad szachowych i modeli. `Engine`, `Opening`, `Persistence`, `Tracking`, `Analysis`, `Training`, `Profiles` i `Presentation` są już oddzielone projektowo. `App` pełni rolę kompozycji i UI, co jest właściwym kierunkiem.

Istnieją też testy ochronne w `MoveMentorChessServices.Tests/App/AppArchitectureTests.cs`, między innymi:

- blokada dostępu do `AnalysisStoreProvider.GetStore()` z Views/ViewModels,
- pilnowanie wybranych granic referencji projektów,
- budżety rozmiaru dla klas po wcześniejszym cleanupie,
- ochrona fasady SQLite przed ponownym przejęciem SQL-i.

### Główna słabość

Architektura nadal miesza **warstwy use-case/application services** z **infrastrukturą persistence**. Przykłady:

- `MoveMentorChess.Analysis/MoveMentorChess.Analysis.csproj:10` referencjonuje `MoveMentorChess.Persistence`.
- `MoveMentorChess.Training/MoveMentorChess.Training.csproj:12` referencjonuje `MoveMentorChess.Persistence`.
- `MoveMentorChess.Profiles/MoveMentorChess.Profiles.csproj:13` referencjonuje `MoveMentorChess.Persistence`.
- `MoveMentorChess.Analysis/GlobalUsings.cs:4`, `MoveMentorChess.Training/GlobalUsings.cs:4`, `MoveMentorChess.Profiles/GlobalUsings.cs:4` propagują tę zależność globalnie.

To nie jest awaria produkcyjna, ale jest to główna bariera przed czystą architekturą. Use-case'y nie powinny zależeć od SQLite/cache/providerów, tylko od portów zdefiniowanych bliżej domeny lub application layer.

## 5. Findings

### P1 - Odwrócić zależności od `Persistence`

Ryzyko: wysokie utrzymaniowo.

`Analysis`, `Training` i `Profiles` zależą od `Persistence`, przez co infrastruktura przecieka do logiki aplikacyjnej. Widać to także w kodzie:

- `MoveMentorChess.Analysis/Services/GameAnalysisService.cs:35` domyślnie tworzy `StoreBackedPlayerMistakeProfileSource`.
- `MoveMentorChess.Analysis/Services/StoreBackedPlayerMistakeProfileSource.cs:10` domyślnie używa `AnalysisStoreProvider.GetStore`.
- `MoveMentorChess.Training/OpeningTrainingSessionBuilder.cs:245` i `MoveMentorChess.Training/OpeningWeaknessService.cs:175` używają `GameAnalysisCacheKey` z persistence.
- `MoveMentorChess.Profiles/PlayerProfileService.cs:122` oraz `:159` tworzą `OpeningWeaknessService` bez portu/fabryki.

Rekomendacja:

Utworzyć małą warstwę portów dla application services. Najbezpieczniej zacząć od istniejących interfejsów persistence i przenieść lub zdublować kierunkowo tylko kontrakty wymagane przez `Analysis`, `Training`, `Profiles`. Implementacje SQLite i globalne providery powinny zostać w `Persistence`, a `AppCompositionRoot` powinien podawać implementacje przez konstruktor/fabrykę.

Akceptacja dla pierwszego PR:

- `GameAnalysisService` nie tworzy domyślnie store-backed źródła profilu.
- `Training` nie musi referencjonować `Persistence` dla samego `GameAnalysisCacheKey`.
- Test architektoniczny wykrywa ponowną referencję `Analysis -> Persistence` albo przynajmniej blokuje nowe bezpośrednie użycia `AnalysisStoreProvider`.

### P1 - Rozbić orkiestrację `OpeningTrainerWindowViewModel`

Ryzyko: wysokie dla dalszego rozwoju UI treningu.

`OpeningTrainerWindowViewModel.cs` ma 2170 linii i nadal odpowiada za:

- stan kreatora i nawigację między stronami,
- komendy,
- telemetry,
- wybór rekomendacji,
- sterowanie sesją,
- feedback animowany,
- formatowanie tekstów,
- mapowanie ruchów na UI,
- reakcje planszy.

W pliku istnieje już komentarz `Compatibility shims` przy stanie delegowanym do `OpeningTrainerSessionController`, co pokazuje dobrą, ale niedokończoną migrację.

Rekomendacja:

Kontynuować ekstrakcję po jednej odpowiedzialności:

1. `OpeningTrainerSelectionViewModel` - wybór profilu, filtr, rekomendacja dnia.
2. `OpeningTrainerOverviewViewModel` - overview i priorytety.
3. `OpeningTrainerStudyViewModel` - aktualna pozycja, wejście ruchu, hinty, plansza.
4. `OpeningTrainerResultsViewModel` - wynik, learning plan, next actions.

Nie robić big-bang refaktora. Po każdym wycięciu jednej odpowiedzialności uruchomić build/testy.

### P1 - Uporządkować `MainWindowViewModel`

Ryzyko: wysokie dla stabilności importu, analizy i obsługi planszy.

`MainWindowViewModel.cs` ma 1383 linie i miesza logikę planszy, import PGN, cache analizy, formatowanie oceny, bulk analysis i UI state. Dodatkowo używa `Avalonia.Media` i `MoveMentorChess.Persistence`, co czyni go mniej przenośnym i trudniejszym do testowania.

Rekomendacja:

Wyciągnąć:

- `ImportedGameReplayController` albo `ReplayNavigationService`,
- `MainBoardStatePresenter`,
- `BulkAnalysisCoordinator`,
- formatery oceny i statusów do `Presentation`.

ViewModel powinien zostać cienką warstwą bindowalnych właściwości i komend.

### P2 - Oddzielić renderery Avalonia od ViewModeli

Ryzyko: średnie, ale rosnące.

`MoveMentorChess.App/ViewModels/ProfileCoachSectionRenderer.cs` leży w folderze `ViewModels`, ale importuje `Avalonia`, `Avalonia.Controls`, `Avalonia.Layout`, `Avalonia.Media` i buduje kontrolki bezpośrednio. Podobne sygnały występują w ViewModelach z `Avalonia.Media`.

Rekomendacja:

- Przenieść renderery kontrolek do `MoveMentorChess.App/Views` lub `MoveMentorChess.App/Renderers`.
- W `MoveMentorChess.Presentation` trzymać framework-neutralne modele: sekcje, wiersze, metryki, serie wykresów, kolory jako tokeny/enumy zamiast `IBrush`.
- ViewModel powinien wystawiać modele prezentacyjne, a renderer Avalonia tłumaczyć je na kontrolki.

### P2 - Zmniejszyć zależność od globalnego stanu

Ryzyko: średnie dla testów i przewidywalności.

Globalne mechanizmy:

- `MoveMentorChess.Persistence/AnalysisStoreProvider.cs`,
- `MoveMentorChess.Persistence/GameAnalysisCache.cs`,
- `MoveMentorChess.Persistence/PersistenceDiagnostics.cs`,
- singletony runtime environment i `LlamaCppServerManager.Instance`.

Są częściowo opakowane adapterami, co jest dobrym kierunkiem. Nadal jednak globalny stan może wpływać na kolejność testów, izolację okien i przyszłe scenariusze wieloprofilowe.

Rekomendacja:

- Nie usuwać wszystkiego naraz.
- Dla nowych funkcji wymagać portu w konstruktorze.
- Pozostawić globalne klasy jako compatibility facade, ale nie rozwijać ich API.
- Dodać test architektoniczny: nowe użycia `AnalysisStoreProvider.GetStore` tylko w composition/root/adapters.

### P2 - Usługi treningowe wymagają dalszego cięcia

Ryzyko: średnie/wysokie.

`OpeningTrainingSessionBuilder.cs`, `OpeningTrainerWorkspaceService.cs`, `TrainingPlanService.cs` i `OpeningWeaknessService.cs` są nadal duże. Widać, że wyciągnięto już część odpowiedzialności, ale te klasy nadal łączą pobieranie danych, scoring, dobór pozycji, budowę tekstów, fallbacki i konstrukcję rezultatów.

Rekomendacja:

Najlepsza kolejność ekstrakcji:

1. Pure selectors/scorers bez store i bez zegara.
2. Snapshot loaders i mappers.
3. Formatery/reason builders do `Presentation` albo osobnego formattera.
4. Orkiestrator zostaje jako cienki use-case.

### P2 - Testy są mocne, ale część plików jest zbyt ciężka

Ryzyko: średnie dla tempa refaktoryzacji.

Największy test `MoveMentorChessServices.Tests/Analysis/GameAnalysisServiceTests.cs` ma 2283 linie. `SqliteAnalysisStoreTests.cs` ma 1389 linii. Przy refaktorach architektonicznych takie testy często stają się trudne do aktualizacji i zachęcają do kopiowania dużych fixture'ów.

Rekomendacja:

- Dodać test data builders dla partii, analiz, replay i SQLite records.
- Podzielić testy według zachowania, np. classification, advice context, cache/persistence, progress/cancellation.
- Zachować istniejące testy integracyjne, ale przenosić szczegółowe przypadki do mniejszych testów jednostkowych.

### P3 - `Domain` jest wartościowy, ale `ChessGame` to przyszły kandydat do ekstrakcji

Ryzyko: niskie/średnie.

`ChessGame.cs` ma 860 linii i odpowiada za FEN, PGN/SAN, legal moves, wykonywanie ruchów, roszadę, en passant, promocję, ataki pól i snapshot stanu. To jest akceptowalne jako stabilny core, ale przy kolejnych zmianach w regułach lub importerze będzie coraz trudniej izolować regresje.

Rekomendacja:

Nie refaktorować prewencyjnie. Gdy pojawi się zmiana w notacji lub legal move generation, wyciągnąć mały komponent:

- `FenParser/FenWriter`,
- `SanMoveResolver`,
- `LegalMoveGenerator`,
- `AttackMap`.

### P3 - Testy architektoniczne powinny przejść z budżetów na cele docelowe

Ryzyko: niskie, ale ważne strategicznie.

`AppArchitectureTests` dobrze chronią przed regresją, ale obecne limity są tolerancyjne, np. `OpeningTrainerWindowViewModel.cs` do 2600 linii i `ProfilesWindow.axaml.cs` do 820 linii. To zatrzymuje pogorszenie, lecz nie wymusza dojścia do czystej struktury.

Rekomendacja:

Dodać drugi poziom testów jako "target architecture":

- brak nowych referencji `Analysis/Training/Profiles -> Persistence`,
- brak `Avalonia.*` w ViewModelach poza jawnie oznaczonymi adapterami,
- nowe renderery Avalonia tylko w `Views`, `Controls`, `Renderers`,
- stopniowo obniżane budżety linii po każdym zakończonym slice.

## 6. Proponowany Roadmap

### Sprint 1 - Granice persistence

Cel: zatrzymać dalsze przeciekanie SQLite/cache do use-case'ów.

Zakres:

- Porty dla źródeł profilu, historii treningu, importowanych gier i wyników analizy.
- Rejestracja implementacji w `AppCompositionRoot`/fabrykach.
- Test architektoniczny blokujący nowe bezpośrednie użycia `AnalysisStoreProvider.GetStore`.

### Sprint 2 - Opening trainer ViewModel split

Cel: zmniejszyć największy hotspot UI.

Zakres:

- Wyciągnąć selection/overview/study/results w małych krokach.
- Zachować istniejący publiczny facade dla okna.
- Po każdym kroku build/test.

### Sprint 3 - Presentation boundary

Cel: wyczyścić granicę Avalonia.

Zakres:

- `ProfileCoachSectionRenderer` przenieść poza `ViewModels`.
- Kolory/brushes w ViewModelach zastąpić tokenami prezentacyjnymi.
- `Presentation` trzyma modele, `App` renderuje.

### Sprint 4 - Training service decomposition

Cel: ułatwić rozwój rekomendacji i planów treningowych.

Zakres:

- Selectory/scorery jako pure services.
- Oddzielne loadery snapshotów.
- Formatery tekstów poza orkiestratorami.

## 7. Reviewer Notes

Ten audyt nie zmienia kodu produkcyjnego. Ryzyko regresji runtime jest zerowe, bo dodany jest tylko dokument.

Ryzyko merytoryczne: audyt bazuje na statycznym przeglądzie kodu i metrykach, nie na uruchomionej aplikacji UI. Wnioski są jednak poparte konkretnymi zależnościami projektów, rozmiarami plików i istniejącymi testami architektonicznymi.

Najważniejsza decyzja architektoniczna przed kolejnym refaktorem: czy porty aplikacyjne mają trafić do istniejących projektów (`Analysis`, `Training`, `Profiles`) czy do osobnego projektu typu `MoveMentorChess.Application.Abstractions`. Dla małego, inkrementalnego cleanupu rekomenduję zacząć od istniejących projektów i dopiero po drugim lub trzecim porcie zdecydować, czy osobny projekt daje realną korzyść.
