using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressComparisonResponseTests
{
    private const string Valid = """{"items":[{"id":1,"status":"full","evidenceIds":[1,2]}]}""";
    private static readonly WordEvidence[] Evidence = [
        new(1, "Xem commit", "backend/Endpoints/GitHub.cs", "+ api.MapGet(\"/repositories/{id}/commits\", Commits);"),
        new(2, "Xem nhánh", "backend/Endpoints/GitHub.cs", "+ api.MapGet(\"/repositories/{id}/branches\", Branches);" )
    ];

    [Fact]
    public async Task Large_context_metadata_does_not_reject_a_small_valid_milestone_result()
    {
        var envelope = JsonSerializer.Serialize(new { response = Valid, done = true,
            done_reason = "stop", context = Enumerable.Repeat(123456, 16384).ToArray() });
        Assert.Equal(true, envelope.Length > 32000);
        using var http = new HttpClient(new Reply(envelope)) { BaseAddress = new Uri("http://localhost:11434/") };
        var result = await new AiGitProgressClient(http, new ConfigurationBuilder().Build())
            .CompareMilestoneAsync([new(1, "Xem commit và nhánh GitHub")], Evidence, CancellationToken.None);
        Assert.Equal("full", result.Items.Single().Status);
    }

    [Fact]
    public async Task Large_metadata_does_not_allow_invented_evidence_ids()
    {
        var answer = """{"items":[{"id":1,"status":"full","evidenceIds":[999]}]}""";
        var envelope = JsonSerializer.Serialize(new { response = answer, done = true, context = Enumerable.Repeat(123456, 16384) });
        using var http = new HttpClient(new Reply(envelope)) { BaseAddress = new Uri("http://localhost:11434/") };
        await ExpectInvalid(async () => { await new AiGitProgressClient(http, new ConfigurationBuilder().Build())
            .CompareMilestoneAsync([new(1, "Xem commit và nhánh GitHub")], Evidence, CancellationToken.None); });
    }

    [Fact]
    public async Task Generated_output_still_has_its_own_size_limit()
    {
        using var content = new StringContent(JsonSerializer.Serialize(new
            { response = new string('x', GitProgressComparisonResponse.MaxGeneratedCharacters + 1), done = true }));
        await ExpectInvalid(async () => { await GitProgressComparisonResponse.ReadGeneratedAsync(content, default); },
            GitProgressComparisonResponse.GeneratedTooLarge);
    }

    [Theory]
    [InlineData(false, "stop")]
    [InlineData(true, "length")]
    public async Task Incomplete_generation_is_never_treated_as_a_complete_result(bool done, string reason)
    {
        using var content = new StringContent(JsonSerializer.Serialize(new { response = Valid, done, done_reason = reason }));
        await ExpectInvalid(async () => { await GitProgressComparisonResponse.ReadGeneratedAsync(content, default); });
    }

    [Fact]
    public async Task An_oversized_transport_envelope_is_rejected_with_or_without_a_content_length_header()
    {
        var oversized = new string('x', GitProgressComparisonResponse.MaxEnvelopeBytes + 1);
        using var known = new StringContent(oversized);
        await ExpectInvalid(async () => { await GitProgressComparisonResponse.ReadGeneratedAsync(known, default); },
            GitProgressComparisonResponse.EnvelopeTooLarge);
        using var unknown = new UnknownLengthContent(Encoding.UTF8.GetBytes(oversized));
        await ExpectInvalid(async () => { await GitProgressComparisonResponse.ReadGeneratedAsync(unknown, default); },
            GitProgressComparisonResponse.EnvelopeTooLarge);
    }

    [Fact]
    public async Task The_request_timeout_also_covers_reading_the_body_after_headers_arrive()
    {
        using var http = new HttpClient(new SlowReply())
            { BaseAddress = new Uri("http://localhost:11434/"), Timeout = TimeSpan.FromMilliseconds(50) };
        var cancelled = false;
        try
        {
            await new AiGitProgressClient(http, new ConfigurationBuilder().Build())
                .CompareMilestoneAsync([new(1, "Xem commit và nhánh GitHub")], Evidence, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (OperationCanceledException) { cancelled = true; }
        Assert.Equal(true, cancelled);
    }

    private sealed class SlowReply : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new SlowContent() });
    }
    private sealed class SlowContent : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new SlowStream());
    }
    private sealed class SlowStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return 0; }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static async Task ExpectInvalid(Func<Task> operation, string? message = null)
    {
        var rejected = false;
        try { await operation(); }
        catch (InvalidDataException ex) { rejected = true; if (message is not null) Assert.Equal(message, ex.Message); }
        Assert.Equal(true, rejected);
    }
    private sealed class Reply(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes, false));
    }
}
