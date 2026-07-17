# ADR-001 — Technologie du viewer 3D (STL, puis CBCT)

**Statut** : décidé (spike Sprint 8) · **Risque adressé** : R3 (licence ActiViz)

## Contexte

Les phases P1 (viewer STL) et P2 (CBCT) exigent du rendu 3D. Le plan citait VTK. Or le
wrapper .NET officiel de VTK, **ActiViz (Kitware)**, est **commercial** — le découvrir en
P2 bloquerait le développement ou imposerait un coût non budgété. Ce spike tranche la
techno **avant** tout développement 3D.

## Options évaluées

| Option | Licence | STL (P1) | CBCT (P2) | Intégration Avalonia | Verdict |
|--------|---------|----------|-----------|----------------------|---------|
| **ActiViz** (VTK .NET) | Commerciale (payante) | ✅ | ✅ | Interop WinForms/hôte | ❌ Piège de licence, écarté |
| **HelixToolkit** (SharpDX/Core) | MIT | ✅ bon | ❌ pas de rendu volumique | Native .NET | ✅ pour P1 |
| **VTK C++ + couche C interop maison** | BSD | ✅ | ✅ | Lourd (build natif multi-plateforme) | ⚠️ Réserve P2 |
| **vtk.js dans WebView** | BSD | ✅ | ✅ | WebView + pont JS | ⚠️ Alternative P2 |

## Décision

1. **P1 (STL) : HelixToolkit** (MIT, 100 % .NET/Avalonia). Suffisant pour afficher, faire
   pivoter, mesurer et comparer des maillages. Zéro risque de licence, intégration directe.
2. **P2 (CBCT)** : décision différée à un spike dédié en début de P2, entre **VTK C++ + interop**
   (contrôle total, BSD) et **vtk.js en WebView** (plus simple, mais pont JS et perfs à valider
   sur les gros volumes — cf. R5). **ActiViz reste exclu.**

## Conséquences

- Le viewer STL du Sprint 11–12 se construit sur HelixToolkit sans dépendance payante.
- Aucune ligne de code ne dépend d'ActiViz.
- Le registre des licences est à jour : HelixToolkit (MIT) ajouté ; ActiViz explicitement écarté.
- Le rendu volumique CBCT reste un point ouvert, mais isolé et sans impact sur P0/P1.
