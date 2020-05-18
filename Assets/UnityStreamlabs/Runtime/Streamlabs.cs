using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using WebSocketSharp;

namespace UnityStreamlabs
{
    public static class Streamlabs
    {
        #pragma warning disable CS0649
        [Serializable]
        private struct HandshakeResponse { public int pingTimeout; }
        #pragma warning restore CS0649

        /// <summary>
        /// Invoked when <see cref="ConnectionState"/> is changed.
        /// </summary>
        public static event Action<ConnectionState> OnConnectionStateChanged;
        /// <summary>
        /// Invoked when a donation is sent.
        /// </summary>
        public static event Action<Donation> OnDonation;

        /// <summary>
        /// Current connection state to the Streamlabs server.
        /// </summary>
        public static ConnectionState ConnectionState { get; private set; }

        private static SynchronizationContext unitySyncContext;
        private static UnityWebRequest donationRequest;
        private static WebSocket webSocket;
        private static CancellationTokenSource heartbeatTCS;
        private static StreamlabsSettings settings;

        /// <summary>
        /// Connect to the Streamlabs server to begin sending and receiving events.
        /// The connection process is async; listen for <see cref="OnConnectionStateChanged"/> to handle the result.
        /// </summary>
        public static void Connect ()
        {
            if (ConnectionState == ConnectionState.Connected || ConnectionState == ConnectionState.Connecting) return;

            settings = StreamlabsSettings.LoadFromResources();
            unitySyncContext = SynchronizationContext.Current;

            ChangeConnectionState(ConnectionState.Connecting);

            AuthController.OnAccessTokenRefreshed += HandleAccessTokenRefreshed;
            AuthController.RefreshAccessToken();

            void HandleAccessTokenRefreshed (bool success)
            {
                AuthController.OnAccessTokenRefreshed -= HandleAccessTokenRefreshed;
                InitializeWebSocket();
            }
        }

        /// <summary>
        /// Disconnect from the Streamlabs server and stop receiving events.
        /// </summary>
        public static void Disconnect ()
        {
            if (donationRequest != null)
            {
                donationRequest.Abort();
                donationRequest.Dispose();
                donationRequest = null;
            }

            if (webSocket != null)
                webSocket.Close();

            ChangeConnectionState(ConnectionState.NotConnected);
        }

        /// <summary>
        /// Sends a test donation event.
        /// </summary>
        /// <param name="name">The name of the donor. has to be between 2-25 chars and can only be alphanumeric + underscores.</param>
        /// <param name="message">The message from the donor. must be < 255 characters.</param>
        /// <param name="identifier">An identifier for this donor, which is used to group donations with the same donor. For example, if you create more than one donation with the same identifier, they will be grouped together as if they came from the same donor. Typically this is best suited as an email address, or a unique hash.</param>
        /// <param name="amount">The amount of this donation.</param>
        /// <param name="currency">The 3 letter currency code for this donation. Must be one of the supported currency codes.</param>
        public static UnityWebRequestAsyncOperation SendDonation (string name, string message, string identifier, double amount, string currency)
        {
            if (donationRequest != null)
            {
                Debug.LogError("Can't send donation event: send request already in progress.");
                return null;
            }

            if (ConnectionState != ConnectionState.Connected)
            {
                Debug.LogError("Can't send donation event: not connected to the Streamlabs server.");
                return null;
            }

            var refreshRequestForm = new WWWForm();
            refreshRequestForm.AddField("name", name);
            refreshRequestForm.AddField("message", message);
            refreshRequestForm.AddField("identifier", identifier);
            refreshRequestForm.AddField("amount", amount.ToString(CultureInfo.InvariantCulture));
            refreshRequestForm.AddField("currency", currency);
            refreshRequestForm.AddField("access_token", AuthController.AccessToken);

            donationRequest = UnityWebRequest.Post("https://streamlabs.com/api/v1.0/donations", refreshRequestForm);
            donationRequest.SetRequestHeader("Content-Type", StreamlabsSettings.RequestContentType);
            donationRequest.SetRequestHeader("Accept", "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            var sendOperation = donationRequest.SendWebRequest();
            sendOperation.completed += HandleResponse;

            return sendOperation;

            void HandleResponse (AsyncOperation op)
            {
                if (donationRequest.isHttpError || donationRequest.isNetworkError)
                    Debug.LogError($"UnityStreamlabs: Failed to send donation event: {donationRequest.error}");

                donationRequest.Dispose();
                donationRequest = null;
            }
        }

        private static void InitializeWebSocket ()
        {
            webSocket = new WebSocket($"wss://sockets.streamlabs.com/socket.io/?token={AuthController.SocketToken}&EIO=3&transport=websocket");
            webSocket.EmitOnPing = true;
            webSocket.OnOpen += HandleOpen;
            webSocket.OnClose += HandleClose;
            webSocket.OnError += HandleError;
            webSocket.OnMessage += HandleSocketMessage;
            webSocket.Connect();

            void HandleOpen (object sender, EventArgs evt)
            {
                if (settings.EmitDebugMessages)
                    Debug.Log("WebSocket: Open");
                ChangeConnectionState(ConnectionState.Connected);
            }

            void HandleClose (object sender, CloseEventArgs evt)
            {
                if (settings.EmitDebugMessages)
                    Debug.Log($"WebSocket: Close {evt.Code} {evt.Reason}");
                heartbeatTCS?.Cancel();
                ChangeConnectionState(ConnectionState.NotConnected);
            }

            void HandleError (object sender, ErrorEventArgs evt)
            {
                Debug.LogError($"Streamlabs web socket error: {evt.Exception} {evt.Message}");
            }
        }

        private static void HandleSocketMessage (object sender, MessageEventArgs evt)
        {
            if (settings.EmitDebugMessages)
                Debug.Log("Message: " + evt.Data);

            var code = evt.Data[0];
            var data = evt.Data.Substring(1);

            if (code == '0') // socket.io ping-pong heartbeat
            {
                heartbeatTCS?.Cancel();
                heartbeatTCS = new CancellationTokenSource();
                var interval = JsonUtility.FromJson<HandshakeResponse>(data).pingTimeout;
                WebSocketHeartbeatRoutine(interval, heartbeatTCS.Token);
                return;
            }

            if (data.StartsWith("2[\"event")) // event
            {
                var eventData = data.Substring(10, data.Length - 11);
                var donation = JsonUtility.FromJson<Donation>(eventData);
                if (donation.type == Donation.Type)
                    unitySyncContext.Send(SafeInokeDonation, donation);
            }

            void SafeInokeDonation (object donation) => OnDonation?.Invoke(donation as Donation);
        }

        private static void ChangeConnectionState (ConnectionState state)
        {
            if (ConnectionState == state) return;

            ConnectionState = state;

            // This method is called on a background thread; rerouting it to the Unity's thread.
            unitySyncContext.Send(SafeInoke, state);
            void SafeInoke (object obj) => OnConnectionStateChanged?.Invoke((ConnectionState)obj);
        }

        private static async void WebSocketHeartbeatRoutine (int interval, CancellationToken cancellationToken)
        {
            while (webSocket != null && webSocket.IsAlive && !cancellationToken.IsCancellationRequested)
            {
                webSocket.SendAsync("2", null);
                await Task.Delay(interval);
            }
        }
    }
}