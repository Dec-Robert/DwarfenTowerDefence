/// <summary>
///     Pasywny budynek wspierający.
///     Może być zbudowany tylko w sąsiedztwie Kuźni Runicznej.
///     Posiada globalny limit ilościowy zależny od Meta-Progresji.
/// </summary>
public class RuneTowerEntity : BuildingEntity
{
    // Budynek pasywny. O jego istnieniu Kuźnia dowiaduje się przez skanowanie sąsiadów
    // (HexGridMath.GetNeighbors) przy pomocy swojej metody ForceRescan().
}