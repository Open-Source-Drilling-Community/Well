using System;
using NUnit.Framework;
using NORCE.Drilling.Well.Model;
using OSDC.DotnetLibraries.General.DataManagement;

namespace NORCE.Drilling.Well.ModelTest
{
    [TestFixture]
    public class WellTests
    {
        [Test]
        public void DefaultConstructor_InitializesWithDefaults()
        {
            var well = new NORCE.Drilling.Well.Model.Well();

            Assert.That(well.MetaInfo, Is.Null);
            Assert.That(well.Name, Is.Null);
            Assert.That(well.Description, Is.Null);
            Assert.That(well.CreationDate, Is.Null);
            Assert.That(well.LastModificationDate, Is.Null);
            Assert.That(well.SlotID, Is.Null);
            Assert.That(well.ClusterID, Is.Null);
            Assert.That(well.IsSingleWell, Is.False);
        }

        [Test]
        public void PropertySetters_Getters_WorkAsExpected()
        {
            var well = new NORCE.Drilling.Well.Model.Well();

            var meta = new MetaInfo();
            var name = "Test Well";
            var desc = "A description";
            var created = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
            var modified = new DateTimeOffset(2024, 6, 7, 8, 9, 10, TimeSpan.FromHours(2));
            var slotId = Guid.NewGuid();
            var clusterId = Guid.NewGuid();

            well.MetaInfo = meta;
            well.Name = name;
            well.Description = desc;
            well.CreationDate = created;
            well.LastModificationDate = modified;
            well.SlotID = slotId;
            well.ClusterID = clusterId;
            well.IsSingleWell = true;

            Assert.That(well.MetaInfo, Is.SameAs(meta));
            Assert.That(well.Name, Is.EqualTo(name));
            Assert.That(well.Description, Is.EqualTo(desc));
            Assert.That(well.CreationDate, Is.EqualTo(created));
            Assert.That(well.LastModificationDate, Is.EqualTo(modified));
            Assert.That(well.SlotID, Is.EqualTo(slotId));
            Assert.That(well.ClusterID, Is.EqualTo(clusterId));
            Assert.That(well.IsSingleWell, Is.True);
        }
    }
}
