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
            foreach (Type managerType in new[] { typeof(WellManager), typeof(WellIdentityManager), typeof(WellFeatureCategoryManager) })
                managerType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);

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
            Assert.That(normalized!.CreationDate, Is.EqualTo(DateTimeOffset.UnixEpoch));
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
        public void SearchWells_FiltersAndPaginatesWithStableTotal()
        {
            Guid cluster = Guid.NewGuid();
            WellModel first = NewWell(Guid.Parse("00000000-0000-0000-0000-000000000001"), cluster, Guid.NewGuid());
            first.Name = "Alpha North";
            WellModel second = NewWell(Guid.Parse("00000000-0000-0000-0000-000000000002"), cluster, Guid.NewGuid());
            second.Name = "Alpha South";
            WellModel third = NewWell(Guid.Parse("00000000-0000-0000-0000-000000000003"));
            third.Name = "Beta";
            Assert.That(_controller.PostWell(first), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            Assert.That(_controller.PostWell(second), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            Assert.That(_controller.PostWell(third), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var response = _controller.SearchWells(offset: 1, limit: 1, name: "alpha", clusterId: cluster);
            var page = ((Microsoft.AspNetCore.Mvc.OkObjectResult)response.Result!).Value as WellSearchResult;
            Assert.Multiple(() =>
            {
                Assert.That(page!.Total, Is.EqualTo(2));
                Assert.That(page.Offset, Is.EqualTo(1));
                Assert.That(page.Limit, Is.EqualTo(1));
                Assert.That(page.Items.Select(value => value.MetaInfo!.ID), Is.EqualTo(new[] { second.MetaInfo!.ID }));
            });
        }

        [Test]
        public void IdentityAssignmentEndpoints_MutateOneAssignmentAndRejectStaleRevision()
        {
            WellIdentity identity = CreateIdentity("TestIdentity");
            WellModel well = NewWell();
            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            DateTimeOffset initialRevision = well.LastModificationDate!.Value;
            var assignment = new WellIdentityAssignment
            {
                ID = Guid.NewGuid(), IdentityID = identity.MetaInfo!.ID, Value = "External-1"
            };

            var add = _controller.PostWellIdentityAssignment(well.MetaInfo!.ID, initialRevision, assignment);
            WellModel afterAdd = (WellModel)((Microsoft.AspNetCore.Mvc.OkObjectResult)add).Value!;
            Assert.That(afterAdd.WellIdentityAssignments, Has.Count.EqualTo(1));
            var identitySearch = _controller.SearchWells(identityId: identity.MetaInfo.ID, identityValue: "external");
            var identityPage = (WellSearchResult)((Microsoft.AspNetCore.Mvc.OkObjectResult)identitySearch.Result!).Value!;
            Assert.That(identityPage.Items.Select(value => value.MetaInfo!.ID), Is.EqualTo(new[] { well.MetaInfo.ID }));

            var staleAdd = _controller.PostWellIdentityAssignment(well.MetaInfo.ID, initialRevision,
                new WellIdentityAssignment { ID = Guid.NewGuid(), IdentityID = identity.MetaInfo.ID, Value = "stale" });
            Assert.That(staleAdd, Is.InstanceOf<Microsoft.AspNetCore.Mvc.ConflictObjectResult>());

            assignment.Value = "External-2";
            var update = _controller.PutWellIdentityAssignment(well.MetaInfo.ID, assignment.ID,
                afterAdd.LastModificationDate!.Value, assignment);
            WellModel afterUpdate = (WellModel)((Microsoft.AspNetCore.Mvc.OkObjectResult)update).Value!;
            Assert.That(afterUpdate.WellIdentityAssignments!.Single().Value, Is.EqualTo("External-2"));

            var delete = _controller.DeleteWellIdentityAssignment(well.MetaInfo.ID, assignment.ID,
                afterUpdate.LastModificationDate!.Value);
            WellModel afterDelete = (WellModel)((Microsoft.AspNetCore.Mvc.OkObjectResult)delete).Value!;
            Assert.That(afterDelete.WellIdentityAssignments, Is.Empty);
        }

        [Test]
        public void FeatureAssignmentEndpoints_MutateOnlySelectedAssignment()
        {
            WellFeatureCategory category = CreateFeatureCategory("TestCategory");
            WellModel well = NewWell();
            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            var assignment = new WellFeatureAssignment
            {
                ID = Guid.NewGuid(), FeatureCategoryID = category.MetaInfo!.ID,
                FeatureOptionID = category.Options!.Single().ID,
                FromDate = DateTimeOffset.UtcNow.AddDays(-1), ToDate = DateTimeOffset.UtcNow.AddDays(1)
            };

            var invalid = new WellFeatureAssignment
            {
                ID = Guid.NewGuid(), FeatureCategoryID = category.MetaInfo.ID, FeatureOptionID = Guid.NewGuid()
            };
            Assert.That(_controller.PostWellFeatureAssignment(well.MetaInfo!.ID, well.LastModificationDate!.Value, invalid),
                Is.InstanceOf<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>());

            var add = _controller.PostWellFeatureAssignment(well.MetaInfo!.ID, well.LastModificationDate!.Value, assignment);
            WellModel afterAdd = (WellModel)((Microsoft.AspNetCore.Mvc.OkObjectResult)add).Value!;
            var featureSearch = _controller.SearchWells(featureCategoryId: category.MetaInfo.ID,
                featureOptionId: category.Options!.Single().ID);
            var featurePage = (WellSearchResult)((Microsoft.AspNetCore.Mvc.OkObjectResult)featureSearch.Result!).Value!;
            Assert.That(featurePage.Items.Select(value => value.MetaInfo!.ID), Is.EqualTo(new[] { well.MetaInfo.ID }));
            assignment.ToDate = assignment.ToDate!.Value.AddDays(1);
            var update = _controller.PutWellFeatureAssignment(well.MetaInfo.ID, assignment.ID,
                afterAdd.LastModificationDate!.Value, assignment);
            WellModel afterUpdate = (WellModel)((Microsoft.AspNetCore.Mvc.OkObjectResult)update).Value!;
            Assert.That(afterUpdate.WellFeatureAssignments!.Single().ToDate, Is.EqualTo(assignment.ToDate));

            var delete = _controller.DeleteWellFeatureAssignment(well.MetaInfo.ID, assignment.ID,
                afterUpdate.LastModificationDate!.Value);
            WellModel afterDelete = (WellModel)((Microsoft.AspNetCore.Mvc.OkObjectResult)delete).Value!;
            Assert.That(afterDelete.WellFeatureAssignments, Is.Empty);
        }

        private WellIdentity CreateIdentity(string name)
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.ClearProviders());
            var controller = new WellIdentityController(loggerFactory.CreateLogger<WellIdentityManager>(), _connMgr);
            var identity = new WellIdentity { MetaInfo = new MetaInfo { ID = Guid.NewGuid() }, Name = name };
            Assert.That(controller.PostWellIdentity(identity), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkObjectResult>());
            return identity;
        }

        private WellFeatureCategory CreateFeatureCategory(string name)
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.ClearProviders());
            var controller = new WellFeatureCategoryController(loggerFactory.CreateLogger<WellFeatureCategoryManager>(), _connMgr);
            var category = new WellFeatureCategory
            {
                MetaInfo = new MetaInfo { ID = Guid.NewGuid() }, Name = name,
                IsExclusive = false, HasValidityPeriod = true,
                Options = [new WellFeatureOption { ID = Guid.NewGuid(), Name = "Option" }]
            };
            Assert.That(controller.PostWellFeatureCategory(category), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkObjectResult>());
            return category;
        }

        [Test]
        public void CoreSubresources_UpdateOnlyDetailsOrLocation()
        {
            WellModel well = NewWell();
            Guid originalCluster = well.ClusterID!.Value;
            Guid originalSlot = well.SlotID!.Value;
            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var detailsResult = _controller.PutWellDetails(well.MetaInfo!.ID, well.LastModificationDate!.Value,
                new WellDetailsUpdate { Name = "Renamed", Description = "Revised description" });
            WellModel afterDetails = (WellModel)((Microsoft.AspNetCore.Mvc.OkObjectResult)detailsResult).Value!;
            Assert.Multiple(() =>
            {
                Assert.That(afterDetails.Name, Is.EqualTo("Renamed"));
                Assert.That(afterDetails.ClusterID, Is.EqualTo(originalCluster));
                Assert.That(afterDetails.SlotID, Is.EqualTo(originalSlot));
            });

            Guid newCluster = Guid.NewGuid();
            Guid newSlot = Guid.NewGuid();
            var locationResult = _controller.PutWellLocation(well.MetaInfo.ID, afterDetails.LastModificationDate!.Value,
                new WellLocationUpdate { ClusterID = newCluster, SlotID = newSlot, IsSingleWell = false });
            WellModel afterLocation = (WellModel)((Microsoft.AspNetCore.Mvc.OkObjectResult)locationResult).Value!;
            Assert.Multiple(() =>
            {
                Assert.That(afterLocation.Name, Is.EqualTo("Renamed"));
                Assert.That(afterLocation.ClusterID, Is.EqualTo(newCluster));
                Assert.That(afterLocation.SlotID, Is.EqualTo(newSlot));
            });
        }

        [Test]
        public void DeleteWellById_Removes_WhenExists()
        {
            var well = NewWell();
            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var del = _controller.DeleteWellById(well.MetaInfo!.ID, well.LastModificationDate!.Value);
            Assert.That(del, Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());

            var after = _controller.GetWellById(well.MetaInfo!.ID);
            Assert.That(after.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.NotFoundResult>());
        }

        [Test]
        public void DeleteWellById_RejectsStaleRevisionWithoutDeleting()
        {
            WellModel well = NewWell();
            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            DateTimeOffset staleRevision = well.LastModificationDate!.Value;
            var update = _controller.PutWellDetails(well.MetaInfo!.ID, staleRevision,
                new WellDetailsUpdate { Name = "Changed", Description = well.Description });
            WellModel updated = (WellModel)((Microsoft.AspNetCore.Mvc.OkObjectResult)update).Value!;

            Assert.That(_controller.DeleteWellById(well.MetaInfo.ID, staleRevision),
                Is.InstanceOf<Microsoft.AspNetCore.Mvc.ConflictObjectResult>());
            Assert.That(_controller.GetWellById(well.MetaInfo.ID).Result,
                Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkObjectResult>());
            Assert.That(_controller.DeleteWellById(well.MetaInfo.ID, updated.LastModificationDate!.Value),
                Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
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

        [Test]
        public async Task ExternalReferenceValidation_ReadsStoredWellWithoutChangingIt()
        {
            WellModel well = NewWell();
            Assert.That(_controller.PostWell(well), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            var validator = new RecordingExternalValidator(WellExternalReferenceValidationStatus.Valid);
            var controller = new WellController(_logger, _connMgr, validator);

            var response = await controller.ValidateWellExternalReferences(well.MetaInfo!.ID, CancellationToken.None);
            var validation = (WellExternalReferenceValidation)((Microsoft.AspNetCore.Mvc.OkObjectResult)response.Result!).Value!;

            Assert.Multiple(() =>
            {
                Assert.That(validation.WellID, Is.EqualTo(well.MetaInfo.ID));
                Assert.That(validator.LastBatch, Has.Count.EqualTo(1));
                Assert.That(_controller.GetWellById(well.MetaInfo.ID).Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkObjectResult>());
            });
        }

        [Test]
        public async Task ExternalReferenceAudit_PaginatesDeterministicallyAndCountsStatuses()
        {
            foreach (Guid id in new[]
                     {
                         Guid.Parse("00000000-0000-0000-0000-000000000003"),
                         Guid.Parse("00000000-0000-0000-0000-000000000001"),
                         Guid.Parse("00000000-0000-0000-0000-000000000002")
                     })
                Assert.That(_controller.PostWell(NewWell(id)), Is.InstanceOf<Microsoft.AspNetCore.Mvc.OkResult>());
            var validator = new RecordingExternalValidator(WellExternalReferenceValidationStatus.Invalid);
            var controller = new WellController(_logger, _connMgr, validator);

            var response = await controller.AuditWellExternalReferences(new WellExternalReferenceAuditRequest
            {
                Scope = WellExternalReferenceAuditScope.All, Offset = 1, Limit = 1
            }, CancellationToken.None);
            var audit = (WellExternalReferenceAuditResult)((Microsoft.AspNetCore.Mvc.OkObjectResult)response.Result!).Value!;

            Assert.Multiple(() =>
            {
                Assert.That(audit.Total, Is.EqualTo(3));
                Assert.That(audit.Items, Has.Count.EqualTo(1));
                Assert.That(audit.Items[0].WellID, Is.EqualTo(Guid.Parse("00000000-0000-0000-0000-000000000002")));
                Assert.That(audit.InvalidCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ExternalReferenceAudit_RejectsUndefinedScope()
        {
            var response = await _controller.AuditWellExternalReferences(new WellExternalReferenceAuditRequest
            {
                Scope = (WellExternalReferenceAuditScope)999
            }, CancellationToken.None);

            Assert.That(response.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>());
        }

        private sealed class RecordingExternalValidator(WellExternalReferenceValidationStatus status) : IWellExternalReferenceValidator
        {
            public IReadOnlyCollection<WellModel> LastBatch { get; private set; } = [];

            public Task<IReadOnlyList<WellExternalReferenceValidation>> ValidateAsync(
                IReadOnlyCollection<WellModel> wells, CancellationToken cancellationToken)
            {
                LastBatch = wells;
                DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
                IReadOnlyList<WellExternalReferenceValidation> results = wells.Select(well => new WellExternalReferenceValidation
                {
                    WellID = well.MetaInfo!.ID, ClusterID = well.ClusterID, SlotID = well.SlotID,
                    Status = status, CheckedAtUtc = checkedAt
                }).ToList();
                return Task.FromResult(results);
            }
        }
    }
}
