using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class PushDeviceTests
    {
        private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

        private readonly User _user = new("google-1", "salvo@gmail.com", "Salvo");

        [Fact]
        public void RegisterPushDevice_Should_AddDevice()
        {
            _user.RegisterPushDevice("  fcm-token-1  ", PushPlatform.Android, Now);

            var device = _user.PushDevices.Should().ContainSingle().Subject;
            device.Token.Should().Be("fcm-token-1");
            device.Platform.Should().Be(PushPlatform.Android);
            device.CreatedAt.Should().Be(Now);
            device.LastSeenAt.Should().Be(Now);
        }

        [Fact]
        public void RegisterPushDevice_Should_RefreshExistingToken_InsteadOfDuplicatingIt()
        {
            _user.RegisterPushDevice("fcm-token-1", PushPlatform.Android, Now);

            _user.RegisterPushDevice("fcm-token-1", PushPlatform.Ios, Now.AddDays(1));

            var device = _user.PushDevices.Should().ContainSingle().Subject;
            device.CreatedAt.Should().Be(Now);
            device.LastSeenAt.Should().Be(Now.AddDays(1));
            device.Platform.Should().Be(PushPlatform.Ios);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RegisterPushDevice_Should_RejectBlankToken(string? token)
        {
            var action = () => _user.RegisterPushDevice(token!, PushPlatform.Android, Now);

            action.Should().Throw<DomainValidationException>().Which.Code.Should().Be("PushDevice.TokenRequired");
        }

        [Fact]
        public void RegisterPushDevice_Should_RejectTooLongToken()
        {
            var action = () => _user.RegisterPushDevice(new string('x', PushDevice.MaxTokenLength + 1), PushPlatform.Android, Now);

            action.Should().Throw<DomainValidationException>().Which.Code.Should().Be("PushDevice.TokenInvalid");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void RegisterPushDevice_Should_RejectUndefinedPlatform(int platform)
        {
            var action = () => _user.RegisterPushDevice("fcm-token-1", (PushPlatform)platform, Now);

            action.Should().Throw<DomainValidationException>().Which.Code.Should().Be("PushDevice.InvalidPlatform");
        }

        [Fact]
        public void RegisterPushDevice_Should_EvictLeastRecentlySeenDevice_WhenLimitIsExceeded()
        {
            for (var i = 0; i < User.MaxPushDevices; i++)
            {
                _user.RegisterPushDevice($"token-{i}", PushPlatform.Android, Now.AddMinutes(i));
            }

            // token-0 is refreshed, so token-1 becomes the oldest
            _user.RegisterPushDevice("token-0", PushPlatform.Android, Now.AddHours(1));
            _user.RegisterPushDevice("token-new", PushPlatform.Android, Now.AddHours(2));

            _user.PushDevices.Should().HaveCount(User.MaxPushDevices);
            _user.PushDevices.Select(d => d.Token).Should().Contain(["token-0", "token-new"]).And.NotContain("token-1");
        }

        [Fact]
        public void RemovePushDevice_Should_RemoveOnlyThatToken()
        {
            _user.RegisterPushDevice("fcm-token-1", PushPlatform.Android, Now);
            _user.RegisterPushDevice("fcm-token-2", PushPlatform.Android, Now);

            _user.RemovePushDevice("fcm-token-1").Should().BeTrue();
            _user.RemovePushDevice("unknown").Should().BeFalse();

            _user.PushDevices.Should().ContainSingle().Which.Token.Should().Be("fcm-token-2");
        }
    }
}
