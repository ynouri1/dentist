using Ortho.Application.Imaging;

namespace Ortho.Application.Abstractions;

/// <summary>Landmark proposé par l'IA : point en coordonnées image + score de confiance 0–1.</summary>
public record DetectedLandmark(string Code, ImagePoint Point, double Confidence);

/// <summary>
/// Détection automatique des landmarks céphalométriques (P1).
/// L'implémentation embarque un modèle ONNX ; en son absence, <see cref="IsAvailable"/>
/// est faux et le placement reste 100 % manuel. Le praticien VALIDE toujours les
/// points proposés — l'IA ne décide jamais seule (positionnement réglementaire).
/// </summary>
public interface ILandmarkDetector
{
    /// <summary>Vrai si un modèle est chargé et prêt à inférer.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Propose une position pour chacun des <paramref name="requestedCodes"/> détectables.
    /// Retourne une liste vide si aucun modèle n'est disponible.
    /// </summary>
    Task<IReadOnlyList<DetectedLandmark>> DetectAsync(
        byte[] displayPng, IReadOnlyList<string> requestedCodes, CancellationToken ct = default);
}
