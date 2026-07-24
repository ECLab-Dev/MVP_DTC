using UnityEngine;
using UnityEditor;
using VRC.Udon;

/// <summary>
/// JSONファイルからVRChatの表示名リストを読み込み、
/// 指定した UdonBehaviour の string[] 配列変数に自動で代入するエディタ拡張ウィンドウです。
/// 
/// 使い方: 
/// 1. このスクリプトを Unity プロジェクトの Assets/Editor フォルダ配下に保存してください。
/// 2. Unityメニューの Window > VRChat > Udon Variable Importer からウィンドウを開きます。
/// </summary>
public class UdonVariableImporter : EditorWindow
{
    private UdonBehaviour targetUdon;
    private string variableName = "displayNames";
    private TextAsset jsonFile;

    [MenuItem("Window/VRChat/Udon Variable Importer")]
    public static void ShowWindow()
    {
        var window = GetWindow<UdonVariableImporter>("Udon Importer");
        window.minSize = new Vector2(400, 200);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("Udon Variable JSON Importer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetUdon = (UdonBehaviour)EditorGUILayout.ObjectField("対象のUdonBehaviour", targetUdon, typeof(UdonBehaviour), true);
        variableName = EditorGUILayout.TextField("代入先の変数名 (string[])", variableName);
        jsonFile = (TextAsset)EditorGUILayout.ObjectField("表示名JSONファイル", jsonFile, typeof(TextAsset), false);

        EditorGUILayout.Space();

        if (GUILayout.Button("JSONから配列データをインポート", GUILayout.Height(35)))
        {
            ImportJsonToUdon();
        }
    }

    private void ImportJsonToUdon()
    {
        if (targetUdon == null)
        {
            Debug.LogError("[UdonImporter] エラー: 対象のUdonBehaviourが指定されていません。ゲームオブジェクトをアタッチしてください。");
            EditorUtility.DisplayDialog("エラー", "対象のUdonBehaviourが指定されていません。", "OK");
            return;
        }

        if (jsonFile == null)
        {
            Debug.LogError("[UdonImporter] エラー: インポート元のJSONファイルが指定されていません。");
            EditorUtility.DisplayDialog("エラー", "JSONファイルが指定されていません。", "OK");
            return;
        }

        if (string.IsNullOrEmpty(variableName))
        {
            Debug.LogError("[UdonImporter] エラー: 変数名が空です。");
            EditorUtility.DisplayDialog("エラー", "変数名が空です。", "OK");
            return;
        }

        try
        {
            string jsonText = jsonFile.text.Trim();
            string[] names = ParseJsonArray(jsonText);

            if (names == null || names.Length == 0)
            {
                Debug.LogError("[UdonImporter] エラー: JSONの解析に失敗したか、データが空です。");
                EditorUtility.DisplayDialog("エラー", "JSONのパースに失敗したか、データが空です。フォーマットを確認してください。", "OK");
                return;
            }

            // UdonBehaviourの変数に値をセット
            if (targetUdon.publicVariables.TrySetVariableValue(variableName, names))
            {
                // Unityにアセットやシーンの変更があったことを通知し、シリアライズ（保存）を走らせる
                EditorUtility.SetDirty(targetUdon);

                // プレハブのインスタンス変更を記録し、保存時に変更が維持されるようにする
                PrefabUtility.RecordPrefabInstancePropertyModifications(targetUdon);

                Debug.Log($"[UdonImporter] 成功: '{variableName}' 配列に {names.Length} 件の名前データをインポートしました！");
                EditorUtility.DisplayDialog("成功", $"'{variableName}' 配列に {names.Length} 件のデータをインポートしました！", "OK");
            }
            else
            {
                string errorMsg = $"UdonBehaviour上に変数 '{variableName}' が見つからないか、型(string[])が一致しません。\n\n" +
                                  $"【確認事項】\n" +
                                  $"1. UdonGraph側で変数が「Public」にチェックされているか\n" +
                                  $"2. 変数の型が「System.String[] (string[])」になっているか\n" +
                                  $"3. UdonGraphのコンパイル（Compileボタンの押下）が完了しているか";
                Debug.LogError($"[UdonImporter] エラー:\n{errorMsg}");
                EditorUtility.DisplayDialog("インポート失敗", errorMsg, "OK");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UdonImporter] 例外が発生しました: {ex.Message}");
            EditorUtility.DisplayDialog("例外発生", $"処理中に例外が発生しました:\n{ex.Message}", "OK");
        }
    }

    /// <summary>
    /// JSON文字列をパースしてstring配列を返します。
    /// ["A", "B"] 形式と {"displayNames": ["A", "B"]} 形式の両方に対応します。
    /// </summary>
    private string[] ParseJsonArray(string json)
    {
        // 1. オブジェクト形式 { "displayNames": [...] } の場合
        if (json.StartsWith("{"))
        {
            var wrapper = JsonUtility.FromJson<JsonWrapper>(json);
            if (wrapper != null && wrapper.displayNames != null)
            {
                return wrapper.displayNames;
            }
        }

        // 2. ルートが配列形式 ["A", "B"] の場合
        // UnityのJsonUtilityはルート配列を直接扱えないため、一時的にオブジェクト形式にラップしてパースします
        if (json.StartsWith("["))
        {
            string wrappedJson = "{\"displayNames\":" + json + "}";
            var wrapper = JsonUtility.FromJson<JsonWrapper>(wrappedJson);
            if (wrapper != null && wrapper.displayNames != null)
            {
                return wrapper.displayNames;
            }
        }

        return null;
    }

    [System.Serializable]
    private class JsonWrapper
    {
        public string[] displayNames;
    }
}
