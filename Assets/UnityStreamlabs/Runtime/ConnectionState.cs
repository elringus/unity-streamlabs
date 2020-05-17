
namespace UnityStreamlabs
{
    public enum ConnectionState
    {
        /// <summary>
        /// Not connected to server.
        /// </summary>
        NotConnected,
        /// <summary>
        /// Connection is in proccess.
        /// </summary>
        Connecting,
        /// <summary>
        /// Ready to send and receive events.
        /// </summary>
        Connected
    }
}