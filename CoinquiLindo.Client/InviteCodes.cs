namespace CoinquiLindo.Client
{
    public static class InviteCodes
    {
        // "ABCDEFGH" -> "ABCD-EFGH": easier to read aloud and to type
        public static string Format(string code)
        {
            ArgumentNullException.ThrowIfNull(code);

            var upper = code.ToUpperInvariant();
            return upper.Length == 8 ? $"{upper[..4]}-{upper[4..]}" : upper;
        }
    }
}
