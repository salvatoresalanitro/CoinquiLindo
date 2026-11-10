using CoinquiLindo.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class FileSystemReportPhotoStorageTests : IDisposable
    {
        private const string PhotoKey = "0f8fad5bd9cb469fa165708167e1f3e1.png";

        private readonly string _rootPath = Path.Combine(
            Path.GetTempPath(), "CoinquiLindo.StorageTests", Guid.NewGuid().ToString("N"));

        [Fact]
        public async Task SaveAsync_Should_WriteFileUnderRoot_AndOpenReadAsync_Should_ReturnSameBytes()
        {
            var storage = CreateStorage();
            byte[] content = [1, 2, 3, 4];

            await storage.SaveAsync(PhotoKey, new MemoryStream(content), CancellationToken.None);

            Directory.GetFiles(_rootPath).Should().ContainSingle()
                .Which.Should().Be(Path.Combine(_rootPath, PhotoKey));
            await using var stream = await storage.OpenReadAsync(PhotoKey, CancellationToken.None);
            using var copy = new MemoryStream();
            await stream!.CopyToAsync(copy);
            copy.ToArray().Should().Equal(content);
        }

        [Fact]
        public async Task OpenReadAsync_Should_ReturnNull_When_PhotoDoesNotExist()
        {
            var stream = await CreateStorage().OpenReadAsync(PhotoKey, CancellationToken.None);

            stream.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_Should_RemoveFile_AndIgnoreMissingFile()
        {
            var storage = CreateStorage();
            await storage.SaveAsync(PhotoKey, new MemoryStream([1]), CancellationToken.None);

            await storage.DeleteAsync(PhotoKey, CancellationToken.None);
            await storage.DeleteAsync(PhotoKey, CancellationToken.None);

            Directory.GetFiles(_rootPath).Should().BeEmpty();
        }

        [Fact]
        public async Task SaveAsync_Should_NotOverwriteExistingPhoto()
        {
            var storage = CreateStorage();
            await storage.SaveAsync(PhotoKey, new MemoryStream([1]), CancellationToken.None);

            var action = () => storage.SaveAsync(PhotoKey, new MemoryStream([2]), CancellationToken.None);

            await action.Should().ThrowAsync<IOException>();
            (await File.ReadAllBytesAsync(Path.Combine(_rootPath, PhotoKey))).Should().Equal(1);
            Directory.GetFiles(_rootPath).Should().ContainSingle();
        }

        [Fact]
        public async Task SaveAsync_Should_LeaveNoFile_When_SourceStreamFails()
        {
            var storage = CreateStorage();

            var action = () => storage.SaveAsync(PhotoKey, new FailingStream(), CancellationToken.None);

            await action.Should().ThrowAsync<IOException>();
            (Directory.Exists(_rootPath) ? Directory.GetFiles(_rootPath) : []).Should().BeEmpty();
        }

        [Theory]
        [InlineData("../escape.png")]
        [InlineData("..\\escape.png")]
        [InlineData("sub/0f8fad5bd9cb469fa165708167e1f3e1.png")]
        [InlineData("0f8fad5bd9cb469fa165708167e1f3e1.exe")]
        [InlineData("")]
        public async Task Operations_Should_RejectKeysOutsideTheOpaqueFormat(string photoKey)
        {
            var storage = CreateStorage();

            await FluentActions.Awaiting(() => storage.SaveAsync(photoKey, new MemoryStream([1]), CancellationToken.None))
                .Should().ThrowAsync<ArgumentException>();
            await FluentActions.Awaiting(() => storage.OpenReadAsync(photoKey, CancellationToken.None))
                .Should().ThrowAsync<ArgumentException>();
            await FluentActions.Awaiting(() => storage.DeleteAsync(photoKey, CancellationToken.None))
                .Should().ThrowAsync<ArgumentException>();
        }

        private FileSystemReportPhotoStorage CreateStorage() =>
            new(Options.Create(new ReportPhotoStorageOptions { RootPath = _rootPath }));

        public void Dispose()
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }

        private sealed class FailingStream : MemoryStream
        {
            public FailingStream() : base([1, 2, 3]) { }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                ValueTask.FromException<int>(new IOException("Simulated read failure."));

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                Task.FromException<int>(new IOException("Simulated read failure."));
        }
    }
}
