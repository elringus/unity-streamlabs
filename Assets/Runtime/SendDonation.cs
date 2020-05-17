using UnityEngine;
using UnityStreamlabs;

public class SendDonation : MonoBehaviour
{
    public string Name = "TestName";
    public string Message = "Test message.";
    public string Identifier = "test_identifier_value";
    public float Amount = 25.98f;
    public string Currency = "USD";

    private void OnEnable ()
    {
        Streamlabs.Connect();
        Streamlabs.OnDonation += HandleDonation;
    }

    private void OnDisable ()
    {
        Streamlabs.Disconnect();
        Streamlabs.OnDonation -= HandleDonation;
    }

    private void HandleDonation (Donation donation)
    {
        Debug.Log($"Donation received: From: {donation.message[0].from} Message: {donation.message[0].message} Amount: {donation.message[0].formattedAmount}");
    }

    [ContextMenu("Send Donation")]
    private void Send ()
    {
        if (Streamlabs.ConnectionState == ConnectionState.Connected)
            Streamlabs.SendDonation(Name, Message, Identifier, Amount, Currency);
    }
}
