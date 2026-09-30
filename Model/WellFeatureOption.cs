using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;
using OSDC.DotnetLibraries.General.ResourceClassification;

namespace OSDC.Drilling.Well.Model;

/// <summary>Well FeatureOption contract backed by the shared resource classification implementation.</summary>
[Semantic(Concepts.FeatureOption)]
public class WellFeatureOption : FeatureOption
{
}
