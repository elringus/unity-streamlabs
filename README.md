## Description

Unity Streamlabs is a client for [Streamlabs](https://streamlabs.com/) streaming platform, allowing to send and receive events (such as donation alerts) within [Unity game engine](https://unity.com/).

## Installation

Use [UPM](https://docs.unity3d.com/Manual/upm-ui.html) to install the package via the following git URL: `https://github.com/Elringus/UnityStreamlabs.git?path=Assets/UnityStreamlabs` or download and import [UnityStreamlabs.unitypackage](https://github.com/Elringus/UnityStreamlabs/raw/master/UnityStreamlabs.unitypackage) manually.

![](https://i.gyazo.com/b54e9daa9a483d9bf7f74f0e94b2d38a.gif)

## How to use

After installing the package, go to project settings (Edit -> Project Settings) and select Streamlabs category.

![](https://i.gyazo.com/f508fce5824710874d071ac628e7fa33.png) 

Click "Create Streamlabs API app" and register a new app. Enter required info; specify `http://localhost` for the redirect URI. Don't forget to whitelist users, that should be able to use the app.

After registering the app, enter client ID and secret to the project settings.

Now you can listen and send events in Unity, eg:

```csharp
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
private void SendDonation ()
{
    if (Streamlabs.ConnectionState == ConnectionState.Connected)
        Streamlabs.SendDonation(Name, Message, Identifier, Amount, Currency);
}
```

## Development
Currently, only donation event is implemented. Feel free to send PRs to extend the plugin for more events.

To add a new event, add a new cs script at `UnityStreamlabs/Runtime/Events` describing the data model (use Donation.cs for reference), then add corresponding event field to `UnityStreamlabs/Runtime/Streamlabs.cs` static class and handle the event in `HandleSocketMessage` method.

Consult Streamlabs [socket API reference](https://dev.streamlabs.com/docs/socket-api) for available events data models.
