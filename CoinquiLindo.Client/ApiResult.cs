using System.Diagnostics.CodeAnalysis;

namespace CoinquiLindo.Client
{
    public class ApiResult
    {
        protected ApiResult(ApiError? error) => Error = error;

        public ApiError? Error { get; }

        [MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccess => Error is null;

        public static ApiResult Success() => new(null);

        public static ApiResult Failure(ApiError error) => new(error);
    }

    public sealed class ApiResult<T> : ApiResult
    {
        private ApiResult(T? value, ApiError? error) : base(error) => Value = value;

        public T? Value { get; }

        public static ApiResult<T> Success(T value) => new(value, null);

        public static new ApiResult<T> Failure(ApiError error) => new(default, error);
    }
}
