using OSDC.DotnetLibraries.General.DataManagement;
using System;

namespace OSDC.Drilling.Well.Model
{
    public class WellIdentityAssignment : IIdentityAssignment
    {
        /// <summary>
        /// unique ID of the assignment
        /// </summary>
        public Guid ID { get; set; }

        /// <summary>
        /// reference to the selected WellIdentity
        /// </summary>
        public Guid? IdentityID { get; set; }

        /// <summary>
        /// well-specific identity value
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// default constructor required for JSON serialization
        /// </summary>
        public WellIdentityAssignment() : base()
        {
        }
    }
}
