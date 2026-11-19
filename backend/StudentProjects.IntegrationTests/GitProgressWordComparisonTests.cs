using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressWordComparisonTests
{
    [Fact]
    public async Task Word_work_paragraphs_and_table_rows_keep_their_claims_and_verified_citations()
    {
        using var word = new MemoryStream();
        using (var archive = new ZipArchive(word, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open()))
        {
            writer.Write("""
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                    <w:p><w:r><w:t>Thông tin chung</w:t></w:r></w:p>
                    <w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Công việc đã thực hiện</w:t></w:r></w:p>
                    <w:p><w:r><w:t>Thêm liên kết repository</w:t></w:r></w:p>
                    <w:tbl>
                      <w:tr><w:trPr><w:tblHeader/></w:trPr><w:tc><w:p><w:r><w:t>Công việc</w:t></w:r></w:p></w:tc></w:tr>
                      <w:tr><w:tc><w:p><w:r><w:t>Thêm ca kiểm thử và đã chạy đạt</w:t></w:r></w:p></w:tc></w:tr>
                      <w:tr><w:tc><w:p><w:r><w:t>Thêm xuất Excel</w:t></w:r></w:p></w:tc></w:tr>
                    </w:tbl>
                    <w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Kế hoạch</w:t></w:r></w:p>
                    <w:p><w:r><w:t>Bổ sung thống kê</w:t></w:r></w:p>
                  </w:body>
                </w:document>
                """);
        }
        word.Position = 0;
        var claims = GitProgressWordComparison.ReadClaims(word);
        Assert.Equal(3, claims.Count);
        var evidence = GitProgressWordComparison.ReadEvidence("""
            [{"title":"Liên kết repository","citations":[{"file":"backend/Endpoints/GitHub.cs","evidence":"+api.MapPost(\"/repositories\", Link);"},
              {"file":"backend/IntegrationTests/GitHubTests.cs","evidence":"+public async Task Student_can_link()"}]}]
            """);
        Assert.Equal(2, evidence.Count);
        using var http = new HttpClient(new ModelReply("""
            {"items":[{"id":1,"status":"full","evidenceIds":[1]},
                      {"id":2,"status":"full","evidenceIds":[2]},
                      {"id":3,"status":"missing","evidenceIds":[]}]}
            """)) { BaseAddress = new Uri("http://localhost:11434/") };
        var comparison = await new AiGitProgressClient(http, new ConfigurationBuilder().Build())
            .CompareWordAsync(claims, evidence, CancellationToken.None);
        Assert.Equal(50m, comparison.Score);
        Assert.Equal(1, comparison.Full);
        Assert.Equal(1, comparison.Partial);
        Assert.Equal(1, comparison.Missing);
        Assert.Equal("Thêm ca kiểm thử và đã chạy đạt", comparison.Items[1].Claim);
        Assert.Equal(evidence[1], Assert.Single(comparison.Items[1].Evidence));
    }

    [Fact]
    public void A_missing_or_fabricated_reference_cannot_produce_a_score()
    {
        var claims = new[] { new WordReportLine(1, "Thêm thao tác liên kết") };
        var evidence = new[] { new WordEvidence(1, "Liên kết", "backend/Endpoints/GitHub.cs", "+db.Repositories.Add(link);") };
        foreach (var response in new[] { """{"items":[]}""",
            """{"items":[{"id":1,"status":"full","evidenceIds":[999]}]}""",
            """{"items":[{"id":1,"status":"full","evidenceIds":[]}]}""" })
        {
            using var model = JsonDocument.Parse(response);
            var rejected = false;
            try { GitProgressWordComparison.ValidateBatch(model.RootElement, claims, evidence); }
            catch (InvalidDataException) { rejected = true; }
            Assert.Equal(true, rejected);
        }
    }

    private sealed class ModelReply(string answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { response = answer }))
            });
    }
}
