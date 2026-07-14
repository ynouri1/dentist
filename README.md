# Ortho — Logiciel d'orthodontie

Alternative locale à OnyxCeph pour les orthodontistes tunisiens : gestion des patients, import d'imagerie (DICOM/JPG/PNG/TIFF), céphalométrie manuelle (Steiner, Ricketts, Tweed, McNamara, Downs, Jarabak) et rapports PDF. Fonctionnement 100 % local, données chiffrées.

## Documentation

- [plan.md](plan.md) — vision produit et périmètre P0 → P3
- [architecture.md](architecture.md) — architecture, bibliothèques, risques techniques
- [sprints.md](sprints.md) — découpage en sprints

## Stack

.NET 10 (LTS) · Avalonia UI (MVVM) · EF Core + SQLite · Serilog · QuestPDF — détails et justifications dans [architecture.md](architecture.md).

## Structure

```
src/
  Ortho.Domain/          # Entités, analyses céphalométriques — zéro dépendance
  Ortho.Application/     # Cas d'usage, interfaces (ports)
  Ortho.Infrastructure/  # EF Core/SQLite, object store fichiers, Serilog
  Ortho.Reporting/       # Génération PDF (QuestPDF)
  Ortho.UI/              # Application desktop Avalonia
tests/
  Ortho.Domain.Tests/
  Ortho.Integration.Tests/
```

## Démarrer

```bash
dotnet build Ortho.sln    # compiler
dotnet test Ortho.sln     # lancer les tests
dotnet run --project src/Ortho.UI   # lancer l'application
```

Prérequis : SDK .NET 10.
