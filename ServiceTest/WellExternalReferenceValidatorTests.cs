using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Configuration;
using OSDC.DotnetLibraries.General.DataManagement;
using OSDC.Drilling.Well.Model;
using OSDC.Drilling.Well.Service;
using WellModel = OSDC.Drilling.Well.Model.Well;

namespace OSDC.Drilling.Well.ServiceTest;

[TestFixture]
public sealed class WellExternalReferenceValidatorTests
{
    [Test]
    public async Task Existing_cluster_and_member_slot_are_valid_and_cluster_reads_are_cached()
    {
        Guid clusterId = Guid.NewGuid();
        Guid slotId = Guid.NewGuid();
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            $"{{\"MetaInfo\":{{\"ID\":\"{clusterId}\"}},\"Slots\":{{\"slot\":{{\"ID\":\"{slotId}\"}}}}}}"));
        WellExternalReferenceValidator validator = CreateValidator(handler);

        IReadOnlyList<WellExternalReferenceValidation> results = await validator.ValidateAsync(
            [Well(clusterId, slotId), Well(clusterId, slotId)], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(results.All(value => value.Status == WellExternalReferenceValidationStatus.Valid), Is.True);
            Assert.That(results.All(value => value.ClusterExists == true), Is.True);
            Assert.That(results.All(value => value.SlotBelongsToCluster == true), Is.True);
            Assert.That(handler.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Missing_cluster_and_wrong_slot_are_reported_as_invalid()
    {
        Guid foundCluster = Guid.NewGuid();
        Guid missingCluster = Guid.NewGuid();
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith(missingCluster.ToString(), StringComparison.OrdinalIgnoreCase)
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : Json(HttpStatusCode.OK, $"{{\"MetaInfo\":{{\"ID\":\"{foundCluster}\"}},\"Slots\":{{}}}}"));
        WellExternalReferenceValidator validator = CreateValidator(handler);

        IReadOnlyList<WellExternalReferenceValidation> results = await validator.ValidateAsync(
            [Well(missingCluster, Guid.NewGuid()), Well(foundCluster, Guid.NewGuid())], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].Status, Is.EqualTo(WellExternalReferenceValidationStatus.Invalid));
            Assert.That(results[0].Issues.Select(issue => issue.Code), Does.Contain("cluster_not_found"));
            Assert.That(results[1].Status, Is.EqualTo(WellExternalReferenceValidationStatus.Invalid));
            Assert.That(results[1].Issues.Select(issue => issue.Code), Does.Contain("slot_not_in_cluster"));
        });
    }

    [Test]
    public async Task External_failure_is_unavailable_not_invalid()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        WellExternalReferenceValidator validator = CreateValidator(handler);

        WellExternalReferenceValidation result = (await validator.ValidateAsync(
            [Well(Guid.NewGuid(), Guid.NewGuid())], CancellationToken.None)).Single();

        Assert.That(result.Status, Is.EqualTo(WellExternalReferenceValidationStatus.Unavailable));
        Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("cluster_service_error"));
    }

    [Test]
    public async Task Well_without_external_references_is_valid_without_http_call()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("HTTP must not be called."));
        WellExternalReferenceValidator validator = CreateValidator(handler);
        WellModel well = new() { MetaInfo = new MetaInfo { ID = Guid.NewGuid() } };

        WellExternalReferenceValidation result = (await validator.ValidateAsync([well], CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(WellExternalReferenceValidationStatus.Valid));
            Assert.That(handler.CallCount, Is.Zero);
        });
    }

    private static WellExternalReferenceValidator CreateValidator(HttpMessageHandler handler)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ClusterHostURL"] = "https://cluster.test/" })
            .Build();
        return new WellExternalReferenceValidator(new StubClientFactory(handler), configuration);
    }

    private static WellModel Well(Guid clusterId, Guid slotId) => new()
    {
        MetaInfo = new MetaInfo { ID = Guid.NewGuid() }, ClusterID = clusterId, SlotID = slotId
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(response(request));
        }
    }
}
