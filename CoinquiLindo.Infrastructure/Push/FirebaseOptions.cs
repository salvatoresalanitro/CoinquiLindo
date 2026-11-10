namespace CoinquiLindo.Infrastructure.Push
{
    // Bound by the host (Api) from the "Firebase" section; the key file must live outside the repository
    public sealed class FirebaseOptions
    {
        public const string SectionName = "Firebase";

        public string CredentialsPath { get; set; } = string.Empty;
    }
}
