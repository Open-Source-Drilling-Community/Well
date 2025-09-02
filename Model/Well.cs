using OSDC.DotnetLibraries.General.DataManagement;
using System;

namespace NORCE.Drilling.Well.Model
{
    public class Well
    {
        /// <summary>
        /// a MetaInfo for the Well
        /// </summary>
        public MetaInfo? MetaInfo { get; set; }

        /// <summary>
        /// name of the data
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// a description of the data
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// the date when the data was created
        /// </summary>
        public DateTimeOffset? CreationDate { get; set; }

        /// <summary>
        /// the date when the data was last modified
        /// </summary>
        public DateTimeOffset? LastModificationDate { get; set; }

        /// <summary>
        ///  the ID of the slot to which this well belongs to
        /// </summary>
        public Guid? SlotID { get; set; }

        /// <summary>
        ///  the ID of the cluster to which this well belongs to
        /// </summary>
        public Guid? ClusterID { get; set; }
        /// <summary>
        /// true if this well does not really belong to a cluster, 
        /// but is a single well for which the cluster is just a proxy
        /// </summary>
        public bool IsSingleWell { get; set; } = false;

        /// <summary>
        /// default constructor required for JSON serialization
        /// </summary>
        public Well() : base()
        {
        }
    }
}
