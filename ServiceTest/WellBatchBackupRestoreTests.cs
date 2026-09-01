using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OSDC.DotnetLibraries.General.DataManagement;
using OSDC.Drilling.Well.Model;
using OSDC.Drilling.Well.Service;
using OSDC.Drilling.Well.Service.Managers;
using System.Reflection;
using System.Text.Json;
using WellModel = OSDC.Drilling.Well.Model.Well;

namespace OSDC.Drilling.Well.ServiceTest;

[TestFixture]
public class WellBatchBackupRestoreTests
{
    [SetUp]
    public void ResetManagers()
    {
        Reset(typeof(WellManager));
        Reset(typeof(WellIdentityManager));
        Reset(typeof(WellFeatureCategoryManager));
    }

    [Test]
    public void Export_SelectedWells_PreservesOrderAndIncludesOnlyReferencedCatalogs()
    {
        string path = TempDatabase();
        SqlConnectionManager connections = Manager(path);
        WellIdentityManager identities = WellIdentityManager.GetInstance(NullLogger<WellIdentityManager>.Instance, connections);
        WellFeatureCategoryManager categories = WellFeatureCategoryManager.GetInstance(NullLogger<WellFeatureCategoryManager>.Instance, connections);
        WellIdentity usedIdentity = Identity("UsedIdentity");
        WellIdentity unusedIdentity = Identity("UnusedIdentity");
        WellFeatureCategory category = Category("Purpose", "Producer");
        Assert.That(identities.AddWellIdentity(usedIdentity), Is.True);
        Assert.That(identities.AddWellIdentity(unusedIdentity), Is.True);
        Assert.That(categories.AddWellFeatureCategory(category), Is.True);
        WellManager wells = WellManager.GetInstance(NullLogger<WellManager>.Instance, connections);
        WellModel first = Well("First", usedIdentity, category);
        WellModel second = Well("Second", usedIdentity, category);
        Assert.That(wells.AddWell(first), Is.True);
        Assert.That(wells.AddWell(second), Is.True);

        WellBatchExportOutcome outcome = wells.ExportBatch(new WellBatchExportRequest
        {
            Scope = WellBatchExportScope.Selected,
            WellIDs = [second.MetaInfo!.ID, first.MetaInfo!.ID]
        });

        Assert.That(outcome.IsSuccess, Is.True);
        Assert.That(outcome.Document!.Wells.Select(value => value.MetaInfo!.ID),
            Is.EqualTo(new[] { second.MetaInfo!.ID, first.MetaInfo!.ID }));
        Assert.That(outcome.Document.CatalogDependencies.Identities.Select(value => value.Name), Is.EqualTo(new[] { "UsedIdentity" }));
        Assert.That(outcome.Document.CatalogDependencies.FeatureCategories, Has.Count.EqualTo(1));
        Assert.That(outcome.Document.CatalogDependencies.FeatureCategories[0].Options, Has.Count.EqualTo(1));
    }

    [Test]
    public void Restore_MapOrCreateMissing_RewritesCatalogReferencesAndCommitsAtomically()
    {
        string path = TempDatabase();
        SqlConnectionManager connections = Manager(path);
        WellIdentity sourceIdentity = Identity("PortableIdentity");
        WellFeatureCategory sourceCategory = Category("PortableCategory", "PortableOption");
        WellModel sourceWell = Well("PortableWell", sourceIdentity, sourceCategory);
        WellBatchExportDocument document = Document(sourceWell, sourceIdentity, sourceCategory);

        using SqliteConnection connection = connections.GetConnection()!;
        WellBatchRestoreOutcome outcome = WellBatchRestorer.Restore(connection, new WellBatchRestoreRequest
        {
            ConflictPolicy = WellBatchRestoreConflictPolicy.FailIfExists,
            CatalogPolicy = WellBatchCatalogRestorePolicy.MapOrCreateMissing,
            Document = document
        }, DateTimeOffset.UtcNow);

        Assert.That(outcome.IsSuccess, Is.True);
        Assert.That(outcome.Response!.CreatedCount, Is.EqualTo(1));
        Assert.That(outcome.Response.CreatedCatalogDefinitionCount, Is.EqualTo(2));
        Assert.That(outcome.Response.CreatedCatalogOptionCount, Is.EqualTo(1));
        Guid localIdentity = outcome.Response.CatalogMappings.Single(value => value.Catalog == "Identity").LocalID;
        Guid localCategory = outcome.Response.CatalogMappings.Single(value => value.Catalog == "FeatureCategory").LocalID;
        Guid localOption = outcome.Response.CatalogMappings.Single(value => value.Catalog == "FeatureOption").LocalID;
        Assert.That(localIdentity, Is.Not.EqualTo(sourceIdentity.MetaInfo!.ID));
        WellModel restored = ReadWell(path, sourceWell.MetaInfo!.ID);
        Assert.That(restored.WellIdentityAssignments![0].IdentityID, Is.EqualTo(localIdentity));
        Assert.That(restored.WellFeatureAssignments![0].FeatureCategoryID, Is.EqualTo(localCategory));
        Assert.That(restored.WellFeatureAssignments[0].FeatureOptionID, Is.EqualTo(localOption));
    }

    [Test]
    public void Restore_Collision_RollsBackPendingCatalogCreationAndPreservesExistingWell()
    {
        string path = TempDatabase();
        SqlConnectionManager connections = Manager(path);
        WellManager manager = WellManager.GetInstance(NullLogger<WellManager>.Instance, connections);
        WellModel existing = new() { MetaInfo = new MetaInfo { ID = Guid.NewGuid() }, Name = "Existing" };
        Assert.That(manager.AddWell(existing), Is.True);
        string before = ReadWellJson(path, existing.MetaInfo!.ID);
        WellIdentity sourceIdentity = Identity("WouldBeCreated");
        WellFeatureCategory sourceCategory = Category("WouldBeCreatedCategory", "Option");
        WellModel colliding = Well("Replacement", sourceIdentity, sourceCategory, existing.MetaInfo.ID);

        using SqliteConnection connection = connections.GetConnection()!;
        WellBatchRestoreOutcome outcome = WellBatchRestorer.Restore(connection, new WellBatchRestoreRequest
        {
            ConflictPolicy = WellBatchRestoreConflictPolicy.FailIfExists,
            CatalogPolicy = WellBatchCatalogRestorePolicy.MapOrCreateMissing,
            Document = Document(colliding, sourceIdentity, sourceCategory)
        }, DateTimeOffset.UtcNow);

        Assert.That(outcome.FailureKind, Is.EqualTo(WellBatchRestoreFailureKind.Conflict));
        Assert.That(ReadWellJson(path, existing.MetaInfo.ID), Is.EqualTo(before));
        Assert.That(Count(path, "WellIdentityTable"), Is.Zero);
        Assert.That(Count(path, "WellFeatureCategoryTable"), Is.Zero);
    }

    [Test]
    public void LegacyUpgradeThenRestore_PreservesUnrelatedRows()
    {
        string path = TempDatabase();
        Guid legacyId = Guid.NewGuid();
        CreateLegacyDatabase(path, legacyId);
        string legacyBefore = ReadWellJson(path, legacyId);
        SqlConnectionManager connections = Manager(path);
        WellModel restored = new() { MetaInfo = new MetaInfo { ID = Guid.NewGuid() }, Name = "Restored" };
        WellBatchExportDocument document = new() { ExportedAtUtc = DateTimeOffset.UtcNow, Wells = [restored] };

        using SqliteConnection connection = connections.GetConnection()!;
        WellBatchRestoreOutcome outcome = WellBatchRestorer.Restore(connection, new WellBatchRestoreRequest
        {
            ConflictPolicy = WellBatchRestoreConflictPolicy.FailIfExists,
            CatalogPolicy = WellBatchCatalogRestorePolicy.MapExisting,
            Document = document
        }, DateTimeOffset.UtcNow);

        Assert.That(outcome.IsSuccess, Is.True);
        Assert.That(ReadWellJson(path, legacyId), Is.EqualTo(legacyBefore));
        Assert.That(Count(path, "WellTable"), Is.EqualTo(2));
    }

    [Test]
    public void Restore_CorruptCatalogDocument_IsRejectedWithoutChangingData()
    {
        string path = TempDatabase();
        SqlConnectionManager connections = Manager(path);
        WellManager manager = WellManager.GetInstance(NullLogger<WellManager>.Instance, connections);
        WellModel existing = new() { MetaInfo = new MetaInfo { ID = Guid.NewGuid() }, Name = "Unchanged" };
        Assert.That(manager.AddWell(existing), Is.True);
        string before = ReadWellJson(path, existing.MetaInfo!.ID);
        WellIdentity first = Identity("First");
        WellIdentity duplicate = Identity("Second");
        duplicate.MetaInfo!.ID = first.MetaInfo!.ID;
        WellModel incoming = new()
        {
            MetaInfo = new MetaInfo { ID = Guid.NewGuid() },
            WellIdentityAssignments = [new WellIdentityAssignment { ID = Guid.NewGuid(), IdentityID = first.MetaInfo.ID }]
        };

        using SqliteConnection connection = connections.GetConnection()!;
        WellBatchRestoreOutcome outcome = WellBatchRestorer.Restore(connection, new WellBatchRestoreRequest
        {
            ConflictPolicy = WellBatchRestoreConflictPolicy.FailIfExists,
            CatalogPolicy = WellBatchCatalogRestorePolicy.MapExisting,
            Document = new WellBatchExportDocument
            {
                ExportedAtUtc = DateTimeOffset.UtcNow,
                CatalogDependencies = new WellBatchCatalogDependencies { Identities = [first, duplicate] },
                Wells = [incoming]
            }
        }, DateTimeOffset.UtcNow);

        Assert.That(outcome.FailureKind, Is.EqualTo(WellBatchRestoreFailureKind.InvalidRequest));
        Assert.That(ReadWellJson(path, existing.MetaInfo.ID), Is.EqualTo(before));
        Assert.That(Count(path, "WellTable"), Is.EqualTo(1));
    }

    private static WellIdentity Identity(string name) => new() { MetaInfo = new MetaInfo { ID = Guid.NewGuid() }, Name = name };
    private static WellFeatureCategory Category(string name, string option) => new()
    {
        MetaInfo = new MetaInfo { ID = Guid.NewGuid() }, Name = name, IsExclusive = true,
        HasValidityPeriod = true, Options = [new WellFeatureOption { ID = Guid.NewGuid(), Name = option }]
    };
    private static WellModel Well(string name, WellIdentity identity, WellFeatureCategory category, Guid? id = null) => new()
    {
        MetaInfo = new MetaInfo { ID = id ?? Guid.NewGuid() }, Name = name,
        WellIdentityAssignments = [new WellIdentityAssignment { ID = Guid.NewGuid(), IdentityID = identity.MetaInfo!.ID, Value = "value" }],
        WellFeatureAssignments = [new WellFeatureAssignment { ID = Guid.NewGuid(), FeatureCategoryID = category.MetaInfo!.ID, FeatureOptionID = category.Options![0].ID }]
    };
    private static WellBatchExportDocument Document(WellModel well, WellIdentity identity, WellFeatureCategory category) => new()
    {
        ExportedAtUtc = DateTimeOffset.UtcNow,
        CatalogDependencies = new WellBatchCatalogDependencies { Identities = [identity], FeatureCategories = [category] },
        Wells = [well]
    };
    private static string TempDatabase() => Path.Combine(TestContext.CurrentContext.WorkDirectory, $"WellBatch_{Guid.NewGuid():N}.db");
    private static SqlConnectionManager Manager(string path) => new($"Data Source={path}", NullLogger<SqlConnectionManager>.Instance);

    private static WellModel ReadWell(string path, Guid id) => JsonSerializer.Deserialize<WellModel>(ReadWellJson(path, id), JsonSettings.Options)!;
    private static string ReadWellJson(string path, Guid id)
    {
        using SqliteConnection connection = new($"Data Source={path}"); connection.Open();
        using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT Well FROM WellTable WHERE ID=$id";
        command.Parameters.AddWithValue("$id", id.ToString()); return (string)command.ExecuteScalar()!;
    }
    private static long Count(string path, string table)
    {
        using SqliteConnection connection = new($"Data Source={path}"); connection.Open();
        using SqliteCommand command = connection.CreateCommand(); command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt64(command.ExecuteScalar());
    }
    private static void CreateLegacyDatabase(string path, Guid id)
    {
        using SqliteConnection connection = new($"Data Source={path}"); connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE WellTable (ID text primary key,MetaInfo text,ClusterID text,SlotID text,Well text); CREATE UNIQUE INDEX WellTableIndex ON WellTable(ID); INSERT INTO WellTable VALUES ($id,$meta,'','','{" + "\"MetaInfo\":{\"ID\":\"" + id + "\"},\"Name\":\"Legacy\"}')";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$meta", JsonSerializer.Serialize(new MetaInfo { ID = id }, JsonSettings.Options));
        command.ExecuteNonQuery();
    }
    private static void Reset(Type type) => type.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
}
