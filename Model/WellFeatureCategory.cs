using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;
using OSDC.DotnetLibraries.General.ResourceClassification;

namespace OSDC.Drilling.Well.Model;

/// <summary>Well FeatureCategory contract backed by the shared resource classification implementation.</summary>
[Semantic(Concepts.FeatureCategory)]
public class WellFeatureCategory : FeatureCategory<WellFeatureOption>
{
}
