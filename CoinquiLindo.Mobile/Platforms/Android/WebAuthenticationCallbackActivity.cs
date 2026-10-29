using Android.App;
using Android.Content;
using Android.Content.PM;
using CoinquiLindo.Mobile.Services;

namespace CoinquiLindo.Mobile
{
    // Receives coinquilindo://auth#... from the browser and completes WebAuthenticator.AuthenticateAsync
    [Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
    [IntentFilter(
        [Intent.ActionView],
        Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
        DataScheme = AuthConstants.CallbackScheme)]
    public class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity
    {
    }
}
