using System;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityStreamlabs
{
    /// <summary>
    /// Controls authorization procedures and provides token to access the APIs.
    /// Implementation based on Google OAuth 2.0 protocol: https://developers.google.com/identity/protocols/OAuth2.
    /// </summary>
    public static class AuthController
    {
        private class SocketTokenResponse { public string socket_token = default; }

        /// <summary>
        /// Invoked when <see cref="AccessToken"/> has been refreshed.
        /// Return false on authorization fail.
        /// </summary>
        public static event Action<bool> OnAccessTokenRefreshed;

        public static string AccessToken => settings.CachedAccessToken;
        public static string SocketToken;
        public static bool IsRefreshingAccessToken { get; private set; }

        private static StreamlabsSettings settings;
        private static IAccessTokenProvider accessTokenProvider;

        static AuthController ()
        {
            settings = StreamlabsSettings.LoadFromResources();

            #if UNITY_WEBGL && !UNITY_EDITOR // WebGL doesn't support loopback method; using redirection scheme instead.
            accessTokenProvider = new RedirectAccessTokenProvider(settings);
            #elif UNITY_ANDROID && !UNITY_EDITOR // On Android a native OpenID lib is used for better UX.
            accessTokenProvider = new AndroidAccessTokenProvider(settings);
            #elif UNITY_IOS && !UNITY_EDITOR // On iOS a native OpenID lib is used for better UX.
            accessTokenProvider = new IOSAccessTokenProvider(settings);
            #else // Loopback scheme is used on other platforms.
            accessTokenProvider = new LoopbackAccessTokenProvider(settings);
            #endif
        }

        public static void RefreshAccessToken ()
        {
            if (IsRefreshingAccessToken) return;
            IsRefreshingAccessToken = true;

            accessTokenProvider.OnDone += HandleAccessTokenProviderDone;
            accessTokenProvider.ProvideAccessToken();
        }

        public static void CancelAuth ()
        {
            if (IsRefreshingAccessToken)
                HandleAccessTokenProviderDone(accessTokenProvider);
        }

        private static void HandleAccessTokenProviderDone (IAccessTokenProvider provider)
        {
            accessTokenProvider.OnDone -= HandleAccessTokenProviderDone;

            var authFailed = !provider.IsDone || provider.IsError;

            if (authFailed)
            {
                Debug.LogError("UnityStreamlabs: Failed to execute authorization procedure. Check application settings and credentials.");
                IsRefreshingAccessToken = false;
                OnAccessTokenRefreshed?.Invoke(true);
                return;
            }


            var socketTokenRequest = UnityWebRequest.Get($"https://streamlabs.com/api/v1.0/socket/token?access_token={AccessToken}");
            socketTokenRequest.SendWebRequest().completed += HandleSocketTokenProviderDone;
        }

        private static void HandleSocketTokenProviderDone (AsyncOperation op)
        {
            var request = (op as UnityWebRequestAsyncOperation).webRequest;
            var authFailed = request.isHttpError || request.isNetworkError;

            if (authFailed)
                Debug.LogError("UnityStreamlabs: Failed to execute authorization procedure. Check application settings and credentials.");
            else SocketToken = JsonUtility.FromJson<SocketTokenResponse>(request.downloadHandler.text).socket_token;

            IsRefreshingAccessToken = false;
            OnAccessTokenRefreshed?.Invoke(!authFailed);
        }
    }
}
