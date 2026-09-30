using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;
using OSDC.DotnetLibraries.General.ResourceClassification;

namespace OSDC.Drilling.Well.Model;

/// <summary>Well Identity contract backed by the shared resource classification implementation.</summary>
[Semantic(Concepts.IdentityDefinition)]
public class WellIdentity : IdentityDefinition
{
}
