using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;
using OSDC.DotnetLibraries.General.DataManagement;
using System;
using System.Collections.Generic;

namespace OSDC.Drilling.Well.Model
{
    [Semantic(Concepts.Well)]
    public class Well
    {
        /// <summary>
        /// a MetaInfo for the Well
        /// </summary>
        [Semantic(Concepts.ResourceMetadata)]
        public MetaInfo? MetaInfo { get; set; }

        /// <summary>
        /// name of the data
        /// </summary>
        [Semantic(Concepts.ResourceName)]
        public string? Name { get; set; }

        /// <summary>
        /// a description of the data
        /// </summary>
        [Semantic(Concepts.ResourceDescription)]
        public string? Description { get; set; }

        /// <summary>
        /// the date when the data was created
        /// </summary>
        [Semantic(Concepts.Instant, Role = Concepts.CreationTime, Reference = Concepts.Utc)]
        public DateTimeOffset? CreationDate { get; set; }

        /// <summary>
        /// the date when the data was last modified
        /// </summary>
        [Semantic(Concepts.Instant, Role = Concepts.LastModificationTime, Reference = Concepts.Utc)]
        public DateTimeOffset? LastModificationDate { get; set; }

        /// <summary>
        ///  the ID of the slot to which this well belongs to
        /// </summary>
        [Semantic(Concepts.ResourceIdentifier)]
        public Guid? SlotID { get; set; }

        /// <summary>
        ///  the ID of the cluster to which this well belongs to
        /// </summary>
        [Semantic(Concepts.ResourceIdentifier)]
        public Guid? ClusterID { get; set; }
        /// <summary>
        /// true if this well does not really belong to a cluster, 
        /// but is a single well for which the cluster is just a proxy
        /// </summary>
        [Semantic(Concepts.SingleWellClusterFlag)]
        public bool IsSingleWell { get; set; } = false;

        /// <summary>
        /// identities assigned to this well
        /// </summary>
        public List<WellIdentityAssignment>? WellIdentityAssignments { get; set; }

        /// <summary>
        /// features assigned to this well
        /// </summary>
        public List<WellFeatureAssignment>? WellFeatureAssignments { get; set; }

        /// <summary>
        /// default constructor required for JSON serialization
        /// </summary>
        public Well() : base()
        {
        }
    }
}
