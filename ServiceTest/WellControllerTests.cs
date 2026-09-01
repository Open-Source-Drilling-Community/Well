using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using WellModel = OSDC.Drilling.Well.Model.Well;
using OSDC.Drilling.Well.Service.Controllers;
using OSDC.Drilling.Well.Service.Managers;
using OSDC.Drilling.Well.Service;
using OSDC.Drilling.Well.Model;
using OSDC.DotnetLibraries.General.DataManagement;

namespace OSDC.Drilling.Well.ServiceTest
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
            Assert.That(conflict, Is.InstanceOf<Microsoft.AspNetCore.Mvc.ConflictObjectResult>());
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
            var put = _controller.PutWellById(well.MetaInfo!.ID, well.LastModificationDate!.Value, well);
            Assert.That(put, Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var ok = _controller.GetWellById(well.MetaInfo!.ID);
            var updated = ((Microsoft.AspNetCore.Mvc.OkObjectResult)ok.Result!).Value as WellModel;
            Assert.That(updated!.Name, Is.EqualTo("Updated"));
        }

        [Test]
        public void PutWellById_RejectsStaleRevisionWithoutOverwritingFirstUpdate()
        {
            var well = NewWell();
            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            DateTimeOffset originalRevision = well.LastModificationDate!.Value;

            well.Name = "First writer";
            Assert.That(_controller.PutWellById(well.MetaInfo!.ID, originalRevision, well),
                Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var stale = NewWell(id: well.MetaInfo.ID, clusterId: well.ClusterID, slotId: well.SlotID);
            stale.Name = "Stale writer";
            var conflict = _controller.PutWellById(stale.MetaInfo!.ID, originalRevision, stale);
            Assert.That(conflict, Is.InstanceOf<Microsoft.AspNetCore.Mvc.ConflictObjectResult>());

            var read = _controller.GetWellById(well.MetaInfo.ID);
            var stored = ((Microsoft.AspNetCore.Mvc.OkObjectResult)read.Result!).Value as WellModel;
            Assert.That(stored!.Name, Is.EqualTo("First writer"));
            Assert.That(stored.LastModificationDate, Is.GreaterThan(originalRevision));
        }

        [Test]
        public void PostWell_ParameterizesJsonContainingSqlCharacters()
        {
            var well = NewWell();
            well.Name = "O'Brien'; DROP TABLE WellTable; --";

            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            var read = _controller.GetWellById(well.MetaInfo!.ID);
            var stored = ((Microsoft.AspNetCore.Mvc.OkObjectResult)read.Result!).Value as WellModel;
            Assert.That(stored!.Name, Is.EqualTo(well.Name));
            Assert.That(_controller.GetAllWellId().Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkObjectResult>());
        }

        [Test]
        public void PostWell_RejectsSlotWithoutClusterAsStructuredBadRequest()
        {
            var well = NewWell();
            well.ClusterID = null;

            var result = _controller.PostWell(well);
            Assert.That(result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>());
            var envelope = ((Microsoft.AspNetCore.Mvc.BadRequestObjectResult)result).Value as WellMutationErrorEnvelope;
            Assert.That(envelope?.Errors, Has.Some.Matches<WellMutationError>(error => error.Code == "cluster_required"));
        }

        [Test]
        public void LegacyWellWithoutTimestamps_GetsStableRevisionAndUpdatesWithoutMigration()
        {
            var legacy = NewWell();
            legacy.CreationDate = null;
            legacy.LastModificationDate = null;
            using (var connection = _connMgr.GetConnection())
            using (var command = connection!.CreateCommand())
            {
                command.CommandText = "INSERT INTO WellTable (ID,MetaInfo,ClusterID,SlotID,Well) VALUES ($id,$meta,$cluster,$slot,$well)";
                command.Parameters.AddWithValue("$id", legacy.MetaInfo!.ID.ToString());
                command.Parameters.AddWithValue("$meta", JsonSerializer.Serialize(legacy.MetaInfo, JsonSettings.Options));
                command.Parameters.AddWithValue("$cluster", legacy.ClusterID!.Value.ToString());
                command.Parameters.AddWithValue("$slot", legacy.SlotID!.Value.ToString());
                command.Parameters.AddWithValue("$well", JsonSerializer.Serialize(legacy, JsonSettings.Options));
                Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
            }

            var read = _controller.GetWellById(legacy.MetaInfo.ID);
            var normalized = ((Microsoft.AspNetCore.Mvc.OkObjectResult)read.Result!).Value as WellModel;
            Assert.That(normalized!.LastModificationDate, Is.EqualTo(DateTimeOffset.UnixEpoch));

            normalized.Name = "Updated legacy Well";
            Assert.That(_controller.PutWellById(legacy.MetaInfo.ID, DateTimeOffset.UnixEpoch, normalized),
                Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            var updatedRead = _controller.GetWellById(legacy.MetaInfo.ID);
            var updated = ((Microsoft.AspNetCore.Mvc.OkObjectResult)updatedRead.Result!).Value as WellModel;
            Assert.That(updated!.Name, Is.EqualTo("Updated legacy Well"));
            Assert.That(updated.LastModificationDate, Is.GreaterThan(DateTimeOffset.UnixEpoch));
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
