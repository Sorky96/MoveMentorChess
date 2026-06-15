# MoveMentorChess.Presentation

`MoveMentorChess.Presentation` is a framework-neutral presentation adapter layer. It may depend on domain and use-case projects so it can translate analysis, opening, profile, and localization data into view-ready records, color tokens, and formatted text.

It must not own platform rendering or UI toolkit behavior. Avalonia controls, GDI/System.Drawing rendering, persistence access, engine lifecycle, and composition wiring belong in App, Tracking, Persistence, Engine, or composition-specific adapters.
