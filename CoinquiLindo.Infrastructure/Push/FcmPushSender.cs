using CoinquiLindo.Application.Notifications;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoinquiLindo.Infrastructure.Push
{
    public sealed class FcmPushSender : IPushSender
    {
        // FCM accepts at most 500 tokens per multicast request
        private const int MaxTokensPerRequest = 500;

        private readonly FirebaseMessaging _messaging;
        private readonly ILogger<FcmPushSender> _logger;

        public FcmPushSender(IOptions<FirebaseOptions> options, ILogger<FcmPushSender> logger)
        {
            _logger = logger;
            var credential = CredentialFactory
                .FromFile<ServiceAccountCredential>(options.Value.CredentialsPath)
                .ToGoogleCredential();

            // Named app: never collides with a default FirebaseApp created elsewhere in the process
            var app = FirebaseApp.GetInstance(nameof(CoinquiLindo))
                ?? FirebaseApp.Create(new AppOptions { Credential = credential }, nameof(CoinquiLindo));
            _messaging = FirebaseMessaging.GetMessaging(app);
        }

        public async Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken)
        {
            var success = 0;
            var invalid = new List<string>();

            foreach (var batch in message.Tokens.Chunk(MaxTokensPerRequest))
            {
                var response = await _messaging.SendEachForMulticastAsync(
                    FcmMessageFactory.Create(message with { Tokens = batch }), cancellationToken);
                success += response.SuccessCount;

                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var result = response.Responses[i];
                    if (result.IsSuccess)
                    {
                        continue;
                    }

                    if (IsPermanentTokenError(result.Exception?.MessagingErrorCode))
                    {
                        invalid.Add(batch[i]);
                    }
                    else
                    {
                        _logger.LogWarning(result.Exception, "FCM delivery failed with {ErrorCode}", result.Exception?.MessagingErrorCode);
                    }
                }
            }

            return new PushSendResult(success, invalid);
        }

        // Only errors meaning "this token will never work again" remove the device; transient ones keep it
        public static bool IsPermanentTokenError(MessagingErrorCode? errorCode) => errorCode is
            MessagingErrorCode.Unregistered or
            MessagingErrorCode.InvalidArgument or
            MessagingErrorCode.SenderIdMismatch;
    }
}
