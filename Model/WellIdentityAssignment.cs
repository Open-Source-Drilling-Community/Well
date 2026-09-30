using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;
using OSDC.DotnetLibraries.General.ResourceClassification;

namespace OSDC.Drilling.Well.Model;

/// <summary>Well IdentityAssignment contract backed by the shared resource classification implementation.</summary>
[Semantic(Concepts.IdentityAssignment)]
public class WellIdentityAssignment : IdentityAssignment
{
}
