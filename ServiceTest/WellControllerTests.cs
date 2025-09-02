using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Reflection;
using NUnit.Framework;
using WellModel = NORCE.Drilling.Well.Model.Well;
using NORCE.Drilling.Well.Service.Controllers;
using NORCE.Drilling.Well.Service.Managers;
using OSDC.DotnetLibraries.General.DataManagement;

namespace NORCE.Drilling.Well.ServiceTest
{
    [TestFixture]
    public class WellControllerTests
    {
        private WellController _controller = null!;
        private SqlConnectionManager _connMgr = null!;
        private ILogger<WellManager> _logger = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Create shared logger factory
            var loggerFactory = LoggerFactory.Create(b => b.ClearProviders());
            _logger = loggerFactory.CreateLogger<WellManager>();
        }

        [SetUp]
        public void SetUp()
        {
            // Reset WellManager singleton to avoid cross-test pollution
            var instField = typeof(WellManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            instField?.SetValue(null, null);

            // unique DB file under test work dir per test
            var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"WellTests_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={dbPath}";

            var loggerFactory = LoggerFactory.Create(b => b.ClearProviders());
            _connMgr = new SqlConnectionManager(connectionString, loggerFactory.CreateLogger<SqlConnectionManager>());

            // ensure clean DB
            WellManager.GetInstance(_logger, _connMgr).Clear();

            _controller = new WellController(_logger, _connMgr);
        }

        private static WellModel NewWell(Guid? id = null, Guid? clusterId = null, Guid? slotId = null)
        {
            var meta = new MetaInfo { ID = id ?? Guid.NewGuid() };
            return new WellModel
            {
                MetaInfo = meta,
                Name = "Test",
                Description = "Test Well",
                CreationDate = DateTimeOffset.UtcNow,
                LastModificationDate = DateTimeOffset.UtcNow,
                ClusterID = clusterId ?? Guid.NewGuid(),
                SlotID = slotId ?? Guid.NewGuid(),
                IsSingleWell = false
            };
        }

        [Test]
        public void GetAllWellId_Empty_ReturnsOkEmptyList()
        {
            var result = _controller.GetAllWellId();
            Assert.That(result.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkObjectResult>());
            var ok = (Microsoft.AspNetCore.Mvc.OkObjectResult)result.Result!;
            Assert.That(ok.Value, Is.InstanceOf<IEnumerable<Guid>>());
            Assert.That((IEnumerable<Guid>)ok.Value!, Is.Empty);
        }

        [Test]
        public void PostWell_Valid_CreatesThenConflictsOnDuplicate()
        {
            var well = NewWell();

            var create = _controller.PostWell(well);
            Assert.That(create, Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            // duplicate with same ID should conflict
            var conflict = _controller.PostWell(well);
            Assert.That(conflict, Is.InstanceOf<Microsoft.AspNetCore.Mvc.StatusCodeResult>());
            var obj = (Microsoft.AspNetCore.Mvc.StatusCodeResult)conflict;
            Assert.That(obj.StatusCode, Is.EqualTo(409));
        }

        [Test]
        public void GetWellById_NotFound_Then_OkAfterCreate()
        {
            var id = Guid.NewGuid();

            var notFound = _controller.GetWellById(id);
            Assert.That(notFound.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.NotFoundResult>());

            var well = NewWell(id: id);
            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var ok = _controller.GetWellById(id);
            Assert.That(ok.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkObjectResult>());
            var val = ((Microsoft.AspNetCore.Mvc.OkObjectResult)ok.Result!).Value as WellModel;
            Assert.That(val, Is.Not.Null);
            Assert.That(val!.MetaInfo!.ID, Is.EqualTo(id));
        }

        [Test]
        public void PutWellById_Updates_WhenExists()
        {
            var well = NewWell();
            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            // change name and update
            well.Name = "Updated";
            var put = _controller.PutWellById(well.MetaInfo!.ID, well);
            Assert.That(put, Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var ok = _controller.GetWellById(well.MetaInfo!.ID);
            var updated = ((Microsoft.AspNetCore.Mvc.OkObjectResult)ok.Result!).Value as WellModel;
            Assert.That(updated!.Name, Is.EqualTo("Updated"));
        }

        [Test]
        public void DeleteWellById_Removes_WhenExists()
        {
            var well = NewWell();
            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var del = _controller.DeleteWellById(well.MetaInfo!.ID);
            Assert.That(del, Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var after = _controller.GetWellById(well.MetaInfo!.ID);
            Assert.That(after.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.NotFoundResult>());
        }

        [Test]
        public void GetAllUsedSlotMetaInfoByClusterId_ReturnsSlots()
        {
            var cluster = Guid.NewGuid();
            var w1 = NewWell(clusterId: cluster, slotId: Guid.NewGuid());
            var w2 = NewWell(clusterId: cluster, slotId: Guid.NewGuid());
            Assert.That(_controller.PostWell(w1), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            Assert.That(_controller.PostWell(w2), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var res = _controller.GetAllUsedSlotMetaInfoByClusterId(cluster);
            Assert.That(res.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkObjectResult>());
            var slots = (IEnumerable<Guid>)((Microsoft.AspNetCore.Mvc.OkObjectResult)res.Result!).Value!;
            Assert.That(slots, Does.Contain(w1.SlotID!.Value));
            Assert.That(slots, Does.Contain(w2.SlotID!.Value));
        }
    }
}
