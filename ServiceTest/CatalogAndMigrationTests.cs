using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OSDC.Drilling.Well.Model;
using OSDC.Drilling.Well.Service.Controllers;
using OSDC.Drilling.Well.Service.Managers;
using OSDC.DotnetLibraries.General.DataManagement;
using System.Reflection;

namespace OSDC.Drilling.Well.ServiceTest;

[TestFixture]
public class CatalogAndMigrationTests
{
    private ILoggerFactory loggerFactory = null!;

    [SetUp]
    public void SetUp()
    {
        loggerFactory = LoggerFactory.Create(builder => builder.ClearProviders());
        ResetSingleton(typeof(WellIdentityManager));
        ResetSingleton(typeof(WellFeatureCategoryManager));
        ResetSingleton(typeof(WellManager));
    }

    [TearDown]
    public void TearDown() => loggerFactory.Dispose();

    [Test]
    public void EmptyCatalogs_AreSeededWithTheSpecifiedDefaults()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"WellCatalog_{Guid.NewGuid()}.db");
        SqlConnectionManager connections = CreateManager(path);
        WellIdentityController identities = new(loggerFactory.CreateLogger<WellIdentityManager>(), connections);
        WellFeatureCategoryController features = new(loggerFactory.CreateLogger<WellFeatureCategoryManager>(), connections);

        List<WellIdentity> identityValues = OkValues<WellIdentity>(identities.GetAllWellIdentity());
        Assert.That(identityValues.Select(value => value.Name), Is.EquivalentTo(new[]
        {
            "OfficialAuthorityName", "OperatorName", "CompanyInternalName", "PlanningName", "DataManagementName",
            "HistoricalName", "ShortName", "DisplayName", "ReportingName", "LegacyName", "ImportedName"
        }));

        List<WellFeatureCategory> categoryValues = OkValues<WellFeatureCategory>(features.GetAllWellFeatureCategory());
        Assert.That(categoryValues, Has.Count.EqualTo(12));
        Assert.That(categoryValues.Single(value => value.Name == "WellPurpose").Options!.Select(value => value.Name),
            Is.EquivalentTo(new[] { "Producer", "Injector", "Observer", "Monitoring", "Disposal", "Relief", "Storage", "GeothermalProducer", "GeothermalInjector" }));
        Assert.That(categoryValues.Single(value => value.Name == "RegulatoryWellClass").IsExclusive, Is.True);
        Assert.That(categoryValues.Single(value => value.Name == "RegulatoryWellClass").HasValidityPeriod, Is.True);
    }

    [Test]
    public void LegacyDatabaseMigration_PreservesEveryWellColumnExactly()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"WellLegacy_{Guid.NewGuid()}.db");
        Guid id = Guid.NewGuid();
        string meta = $"{{\"ID\":\"{id}\"}}";
        string document = $"{{\"MetaInfo\":{{\"ID\":\"{id}\"}},\"Name\":\"legacy O'Brien\"}}";
        using (SqliteConnection connection = new($"Data Source={path}"))
        {
            connection.Open();
            using SqliteCommand create = connection.CreateCommand();
            create.CommandText = "CREATE TABLE WellTable (ID text primary key, MetaInfo text, ClusterID text, SlotID text, Well text); CREATE UNIQUE INDEX WellTableIndex ON WellTable(ID);";
            create.ExecuteNonQuery();
            using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO WellTable VALUES ($id,$meta,$cluster,$slot,$well)";
            insert.Parameters.AddWithValue("$id", id.ToString());
            insert.Parameters.AddWithValue("$meta", meta);
            insert.Parameters.AddWithValue("$cluster", "cluster-original");
            insert.Parameters.AddWithValue("$slot", "slot-original");
            insert.Parameters.AddWithValue("$well", document);
            insert.ExecuteNonQuery();
        }

        string[] before = ReadWellRow(path, id);
        _ = CreateManager(path);
        string[] after = ReadWellRow(path, id);

        Assert.That(after, Is.EqualTo(before));
        using SqliteConnection migrated = new($"Data Source={path}");
        migrated.Open();
        Assert.That(Scalar<long>(migrated, "PRAGMA user_version"), Is.EqualTo(SqlConnectionManager.CURRENT_SCHEMA_VERSION));
        Assert.That(Scalar<long>(migrated, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('WellIdentityTable','WellFeatureCategoryTable')"), Is.EqualTo(2));
    }

    [Test]
    public void ReferencedIdentity_CannotBeDeleted()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"WellReferences_{Guid.NewGuid()}.db");
        SqlConnectionManager connections = CreateManager(path);
        WellIdentityController identities = new(loggerFactory.CreateLogger<WellIdentityManager>(), connections);
        WellIdentity identity = OkValues<WellIdentity>(identities.GetAllWellIdentity()).First();
        WellController wells = new(loggerFactory.CreateLogger<WellManager>(), connections);
        OSDC.Drilling.Well.Model.Well well = new()
        {
            MetaInfo = new MetaInfo { ID = Guid.NewGuid() },
            Name = "Referenced catalog test",
            WellIdentityAssignments = [new WellIdentityAssignment { ID = Guid.NewGuid(), IdentityID = identity.MetaInfo!.ID, Value = "A-1" }]
        };
        Assert.That(wells.PostWell(well), Is.InstanceOf<OkResult>());

        Assert.That(identities.DeleteWellIdentityById(identity.MetaInfo!.ID), Is.InstanceOf<ConflictObjectResult>());
    }

    [Test]
    public void KubernetesBackupCopies_MigrateWithoutChangingWellRows()
    {
        string backupRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "deployment", "backups"));
        string[] backups = Directory.Exists(backupRoot) ? Directory.GetFiles(backupRoot, "Well.db", SearchOption.AllDirectories) : [];
        if (backups.Length == 0) Assert.Ignore("No local Kubernetes backup snapshots are available.");
        Assert.That(backups, Has.Length.EqualTo(3), "Expected one backup from each Kubernetes site.");

        foreach (string backup in backups)
        {
            string copy = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Path.GetFileName(Path.GetDirectoryName(backup))}_{Guid.NewGuid()}.db");
            File.Copy(backup, copy);
            List<string[]> before = ReadAllWellRows(copy);
            _ = CreateManager(copy);
            List<string[]> after = ReadAllWellRows(copy);
            Assert.That(after, Is.EqualTo(before), $"WellTable changed while migrating {backup}");
        }
    }

    private SqlConnectionManager CreateManager(string path) =>
        new($"Data Source={path}", loggerFactory.CreateLogger<SqlConnectionManager>());

    private static List<T> OkValues<T>(ActionResult<IEnumerable<T?>> result) where T : class =>
        ((IEnumerable<T?>)((OkObjectResult)result.Result!).Value!).Where(value => value != null).Cast<T>().ToList();

    private static string[] ReadWellRow(string path, Guid id)
    {
        using SqliteConnection connection = new($"Data Source={path}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT ID,MetaInfo,ClusterID,SlotID,Well FROM WellTable WHERE ID=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        using SqliteDataReader reader = command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        return Enumerable.Range(0, 5).Select(reader.GetString).ToArray();
    }

    private static List<string[]> ReadAllWellRows(string path)
    {
        using SqliteConnection connection = new($"Data Source={path}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT ID,MetaInfo,ClusterID,SlotID,Well FROM WellTable ORDER BY ID";
        using SqliteDataReader reader = command.ExecuteReader();
        List<string[]> rows = [];
        while (reader.Read()) rows.Add(Enumerable.Range(0, 5).Select(index => reader.IsDBNull(index) ? "<NULL>" : reader.GetString(index)).ToArray());
        return rows;
    }

    private static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }

    private static void ResetSingleton(Type type) =>
        type.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
}
