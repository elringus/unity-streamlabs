using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityStreamlabs
{
    [CustomEditor(typeof(StreamlabsSettings))]
    public class StreamlabsSettingsEditor : Editor
    {
        protected StreamlabsSettings TargetSettings => target as StreamlabsSettings;

        private SerializedProperty genericClientCredentials;
        private SerializedProperty accessScopes;
        private SerializedProperty loopbackUri;
        private SerializedProperty loopbackResponseHtml;
        private SerializedProperty accessTokenPrefsKey;
        private SerializedProperty refreshTokenPrefsKey;
        private SerializedProperty emitDebugMessages;

        private readonly static GUIContent genericClientCredentialsContent = new GUIContent("Credentials", "Streamlabs API application credentials used to authorize requests via loopback and redirect schemes.");
        private readonly static GUIContent accessScopesContent = new GUIContent("Access Scopes", "Scopes of access to the user's Streamlabs the app will request.");
        private readonly static GUIContent loopbackUriContent = new GUIContent("Loopback URI", "A web address for the loopback authentication requests. Defult is 'localhost'.");
        private readonly static GUIContent loopbackResponseHtmlContent = new GUIContent("Loopback Response HTML", "HTML page shown to the user when loopback response is received.");
        private readonly static GUIContent accessTokenPrefsKeyContent = new GUIContent("Access Token Key", "PlayerPrefs key used to store access token.");
        private readonly static GUIContent refreshTokenPrefsKeyContent = new GUIContent("Refresh Token Key", "PlayerPrefs key used to store refresh token.");
        private readonly static GUIContent deleteCachedTokensContent = new GUIContent("Delete cached tokens", "Removes cached access and refresh tokens forcing user to login on the next request.");

        private static StreamlabsSettings GetOrCreateSettings ()
        {
            var settings = StreamlabsSettings.LoadFromResources(true);
            if (!settings)
            {
                settings = CreateInstance<StreamlabsSettings>();
                Directory.CreateDirectory(Application.dataPath + "/UnityStreamlabs/Resources");
                const string path = "Assets/UnityStreamlabs/Resources/StreamlabsSettings.asset";
                AssetDatabase.CreateAsset(settings, path);
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                Debug.Log($"UnityStreamlabs: Settings file didn't exist and was created at: {path}.\n" +
                    "You're free to move it, just make sure it stays in the root of a 'Resources' folder.");
            }
            return settings;
        }

        [SettingsProvider]
        internal static SettingsProvider CreateProjectSettingsProvider ()
        {
            var assetPath = AssetDatabase.GetAssetPath(GetOrCreateSettings());
            var keywords = SettingsProvider.GetSearchKeywordsFromPath(assetPath);
            return AssetSettingsProvider.CreateProviderFromAssetPath("Project/Streamlabs", assetPath, keywords);
        }

        private void OnEnable ()
        {
            if (!TargetSettings) return;
            genericClientCredentials = serializedObject.FindProperty("genericClientCredentials");
            accessScopes = serializedObject.FindProperty("accessScopes");
            loopbackUri = serializedObject.FindProperty("loopbackUri");
            loopbackResponseHtml = serializedObject.FindProperty("loopbackResponseHtml");
            accessTokenPrefsKey = serializedObject.FindProperty("accessTokenPrefsKey");
            refreshTokenPrefsKey = serializedObject.FindProperty("refreshTokenPrefsKey");
            emitDebugMessages = serializedObject.FindProperty("emitDebugMessages");
        }

        public override void OnInspectorGUI ()
        {
            if (TargetSettings.GenericClientCredentials.ContainsSensitiveData())
                EditorGUILayout.HelpBox("The asset contains sensitive data about your Streamlabs API app. " +
                    "Consider excluding it from the version control systems.", MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(genericClientCredentials, genericClientCredentialsContent, true);
            EditorGUILayout.PropertyField(accessScopes, accessScopesContent, true);
            EditorGUILayout.PropertyField(loopbackUri, loopbackUriContent);
            EditorGUILayout.PropertyField(loopbackResponseHtml, loopbackResponseHtmlContent);
            EditorGUILayout.PropertyField(accessTokenPrefsKey, accessTokenPrefsKeyContent);
            EditorGUILayout.PropertyField(refreshTokenPrefsKey, refreshTokenPrefsKeyContent);
            EditorGUILayout.PropertyField(refreshTokenPrefsKey, refreshTokenPrefsKeyContent);
            EditorGUILayout.PropertyField(emitDebugMessages);

            EditorGUILayout.Space();

            if (GUILayout.Button("Create Streamlabs API app"))
                Application.OpenURL(@"https://streamlabs.com/dashboard/#/settings/api-settings");

            using (new EditorGUI.DisabledScope(!TargetSettings.IsAnyAuthTokenCached()))
                if (GUILayout.Button(deleteCachedTokensContent))
                    TargetSettings.DeleteCachedAuthTokens();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
