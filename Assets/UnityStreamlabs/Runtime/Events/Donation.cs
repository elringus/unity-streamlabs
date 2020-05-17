using System;
using System.Collections.Generic;

namespace UnityStreamlabs
{
    // https://dev.streamlabs.com/docs/socket-api

    [Serializable]
    public class Donation
    {
        public const string Type = "donation";

        [Serializable]
        public class Message
        {
            public string id;
            public string name;
            public float amount;
            public string formatted_amount;
            public string formattedAmount;
            public string message;
            public string currency;
            public string iconClassName;
            public string from;
            public string from_user_id;
            public string _id;
        }

        public string type;
        public List<Message> message;
        public string event_id;
    }
}