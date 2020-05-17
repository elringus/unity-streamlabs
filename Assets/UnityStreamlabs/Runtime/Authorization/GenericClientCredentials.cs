using System.Collections.Generic;
using UnityEngine;

namespace UnityStreamlabs
{
    [System.Serializable]
    public class GenericClientCredentials : IClientCredentials
    {
        public string ClientId => clientId;
        public string AuthUri => authUri;
        public string TokenUri => tokenUri;
        public string ClientSecret => clientSecret;
        public List<string> RedirectUris => redirectUris;

        [SerializeField] private string authUri = "https://streamlabs.com/api/v1.0/authorize";
        [SerializeField] private string tokenUri = "https://streamlabs.com/api/v1.0/token";
        [SerializeField] private string clientId = null;
        [SerializeField] private string clientSecret = null;
        [SerializeField] private List<string> redirectUris = new List<string> { "http://localhost" };

        public bool ContainsSensitiveData () => !string.IsNullOrEmpty(ClientId + ClientSecret);
    }
}
