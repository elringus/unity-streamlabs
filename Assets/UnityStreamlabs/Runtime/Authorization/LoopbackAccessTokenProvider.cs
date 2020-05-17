using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace UnityStreamlabs
{
    /// <summary>
    /// Provides access token using local loopback method to read authorization response.
    /// Implementation based on: https://github.com/googlesamples/oauth-apps-for-windows.
    /// </summary>
    public class LoopbackAccessTokenProvider : IAccessTokenProvider
    {
        public event Action<IAccessTokenProvider> OnDone;

        public bool IsDone { get; private set; }
        public bool IsError { get; private set; }

        private SynchronizationContext unitySyncContext;
        private StreamlabsSettings settings;
        private AccessTokenRefresher accessTokenRefresher;
        private AuthCodeExchanger authCodeExchanger;
        private string codeVerifier;
        private string redirectUri;
        private string authorizationCode;

        public LoopbackAccessTokenProvider (StreamlabsSettings StreamlabsSettings)
        {
            settings = StreamlabsSettings;
            unitySyncContext = SynchronizationContext.Current;

            accessTokenRefresher = new AccessTokenRefresher(settings.GenericClientCredentials);
            accessTokenRefresher.OnDone += HandleAccessTokenRefreshed;

            authCodeExchanger = new AuthCodeExchanger(settings, settings.GenericClientCredentials);
            authCodeExchanger.OnDone += HandleAuthCodeExchanged;
        }

        public void ProvideAccessToken ()
        {
            if (!settings.GenericClientCredentials.ContainsSensitiveData())
            {
                Debug.LogError("Generic credentials are not valid.");
                HandleProvideAccessTokenComplete(true);
                return;
            }

            // Access token will never expire: https://dev.streamlabs.com/docs/oauth-2
            if (!string.IsNullOrEmpty(settings.CachedAccessToken))
            {
                HandleProvideAccessTokenComplete();
                return;
            }
            else ExecuteFullAuth();

            //// Refresh token isn't available; executing full auth procedure.
            //if (string.IsNullOrEmpty(settings.CachedRefreshToken)) ExecuteFullAuth();
            //// Using refresh token to issue a new access token.
            //else accessTokenRefresher.RefreshAccessToken(settings.CachedRefreshToken);
        }

        private void HandleProvideAccessTokenComplete (bool error = false)
        {
            IsError = error;
            IsDone = true;
            if (OnDone != null)
                OnDone.Invoke(this);
        }

        private void HandleAccessTokenRefreshed (AccessTokenRefresher refresher)
        {
            if (refresher.IsError)
            {
                if (Debug.isDebugBuild)
                {
                    var message = "UnityStreamlabs: Failed to refresh access token; executing full auth procedure.";
                    if (!string.IsNullOrEmpty(refresher.Error))
                        message += $"\nDetails: {refresher.Error}";
                    Debug.Log(message);
                }
                ExecuteFullAuth();
            }
            else
            {
                settings.CachedAccessToken = refresher.AccesToken;
                HandleProvideAccessTokenComplete();
            }
        }

        private void HandleAuthCodeExchanged (AuthCodeExchanger exchanger)
        {
            if (authCodeExchanger.IsError)
            {
                Debug.LogError("UnityStreamlabs: Failed to exchange authorization code.");
                HandleProvideAccessTokenComplete(true);
            }
            else
            {
                settings.CachedAccessToken = authCodeExchanger.AccesToken;
                settings.CachedRefreshToken = authCodeExchanger.RefreshToken;
                HandleProvideAccessTokenComplete();
            }
        }

        private void ExecuteFullAuth ()
        {
            // Generate state and PKCE values.
            codeVerifier = CryptoUtils.RandomDataBase64Uri(32);
            var codeVerifierHash = CryptoUtils.Sha256(codeVerifier);
            var codeChallenge = CryptoUtils.Base64UriEncodeNoPadding(codeVerifierHash);

            // Creates a redirect URI using an available port on the loopback address.
            redirectUri = $"{settings.LoopbackUri}";//:{GetRandomUnusedPort()}";

            // Listen for requests on the redirect URI.
            var httpListener = new HttpListener();
            httpListener.Prefixes.Add(redirectUri + '/');
            httpListener.Start();

            // Create the OAuth 2.0 authorization request.
            // https://developers.google.com/identity/protocols/OAuth2WebServer#creatingclient
            var authRequest = string.Format("{0}?client_id={1}&redirect_uri={2}&response_type=code&scope={3}",
                settings.GenericClientCredentials.AuthUri,
                settings.GenericClientCredentials.ClientId,
                redirectUri,
                settings.AccessScope);

            // Open request in the browser.
            Application.OpenURL(authRequest);

            // Wait for the authorization response.
            var asyncResult = httpListener.BeginGetContext(HandleHttpListenerCallback, httpListener);

            // Block the thread when backround mode is not supported to serve HTTP response while the application is not in focus.
            if (!Application.runInBackground)
                asyncResult.AsyncWaitHandle.WaitOne();
        }

        private void HandleHttpListenerCallback (IAsyncResult result)
        {
            // This method is called on a background thread; rerouting it to the Unity's thread.
            unitySyncContext.Send(HandleHttpListenerCallbackOnUnityThread, result);
        }

        private void HandleHttpListenerCallbackOnUnityThread (object state)
        {
            var result = (IAsyncResult)state;
            var httpListener = (HttpListener)result.AsyncState;
            var context = httpListener.EndGetContext(result);

            // Send an HTTP response to the browser to notify the user to close the browser.
            var response = context.Response;
            var responseString = settings.LoopbackResponseHtml;
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var responseOutput = response.OutputStream;
            responseOutput.Write(buffer, 0, buffer.Length);
            responseOutput.Close();
            httpListener.Close();

            // Check for errors.
            if (context.Request.QueryString.Get("error") != null)
            {
                Debug.LogError($"UnityStreamlabs: OAuth authorization error: {context.Request.QueryString.Get("error")}.");
                HandleProvideAccessTokenComplete(true);
                return;
            }
            if (context.Request.QueryString.Get("code") == null)
            {
                Debug.LogError($"UnityStreamlabs: Malformed authorization response. {context.Request.QueryString}");
                HandleProvideAccessTokenComplete(true);
                return;
            }

            // Extract the authorization code.
            authorizationCode = context.Request.QueryString.Get("code");

            // Exchange the authorization code for tokens.
            authCodeExchanger.ExchangeAuthCode(authorizationCode, codeVerifier, redirectUri);
        }

        private int GetRandomUnusedPort ()
        {
            // Based on: http://stackoverflow.com/a/3978040.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
