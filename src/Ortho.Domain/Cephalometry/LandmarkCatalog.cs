namespace Ortho.Domain.Cephalometry;

/// <summary>Catalogue des points céphalométriques supportés.</summary>
public static class LandmarkCatalog
{
    public static readonly IReadOnlyDictionary<string, LandmarkDefinition> All =
        new List<LandmarkDefinition>
        {
            new("S", "Sella (centre de la selle turcique)"),
            new("N", "Nasion"),
            new("A", "Point A (subspinale)"),
            new("B", "Point B (supramentale)"),
            new("D", "Point D (centre de la symphyse)"),
            new("Pog", "Pogonion"),
            new("Gn", "Gnathion"),
            new("Me", "Menton"),
            new("Go", "Gonion"),
            new("Po", "Porion"),
            new("Or", "Orbitale"),
            new("ANS", "Épine nasale antérieure"),
            new("PNS", "Épine nasale postérieure"),
            new("U1E", "Incisive supérieure — bord libre"),
            new("U1A", "Incisive supérieure — apex"),
            new("L1E", "Incisive inférieure — bord libre"),
            new("L1A", "Incisive inférieure — apex"),
        }.ToDictionary(l => l.Code);

    public static LandmarkDefinition Get(string code) => All[code];
}
