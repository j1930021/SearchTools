﻿using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

public class SearchTools : EditorWindow {

	/// <summary>
	/// メニュー化
	/// </summary>
	[MenuItem("Window/Search Tools")]
	public static void Menu() {
		var st = GetWindow<SearchTools>("Search Tools");
		st.Show();
	}

	/// <summary>
	/// 構築
	/// </summary>
	public void OnEnable() {
		mComponentTypes = getAllComponentTypes().ToArray();
	}

	/// <summary>
	/// 描画
	/// </summary>
	public void OnGUI() {
		OnGUIForToolbar();
		switch (mCurrentToolIndex) {
		case ToolIndex.Component:
			OnGUIForComponent();
			break;
		case ToolIndex.Resource:
			OnGUIForResource();
			break;
		case ToolIndex.Option:
			OnGUIForOption();
			break;
		}
	}

	/// <summary>
	/// 描画(ツールバー)
	/// </summary>
	public void OnGUIForToolbar() {
		EditorGUILayout.BeginHorizontal();
		{
			{ //Tool Part
				var labels = Enumerable.Range(0, (int)ToolIndex.Option)
											.Select(x=>((ToolIndex)x).ToString())
											.ToArray();
				var index = ((mCurrentToolIndex != ToolIndex.Option)? (int)mCurrentToolIndex: -1);
				EditorGUI.BeginChangeCheck();
				index = GUILayout.Toolbar(index, labels);
				if (EditorGUI.EndChangeCheck()) {
					mCurrentToolIndex = (ToolIndex)index;
				}
			}
			{ //Option Part
				var labels = new[]{ToolIndex.Option.ToString()};
				var index = ((mCurrentToolIndex == ToolIndex.Option)? (int)mCurrentToolIndex - (int)ToolIndex.Option: -1);
				EditorGUI.BeginChangeCheck();
				index = GUILayout.Toolbar(index, labels);
				if (EditorGUI.EndChangeCheck()) {
					mCurrentToolIndex = (ToolIndex)(index + ToolIndex.Option);
				}
			}
		}
		EditorGUILayout.EndHorizontal();
	}

	/// <summary>
	/// 描画(コンポーネント)
	/// </summary>
	public void OnGUIForComponent() {
		//パスフィルタ欄
		mComponentNamePassFilter = EditorGUILayout.TextField("Name Pass Filter", mComponentNamePassFilter);
		//検索場所
		mLookIn = (LookIn)EditorGUILayout.EnumMaskField("Look In", mLookIn);
		//候補コンポーネント取得
		var componentTypes = getComponentTypes(mComponentNamePassFilter).ToArray();
		//検索ボタン
		var old_gui_enabled = GUI.enabled;
		GUI.enabled = (componentTypes.Length == 1); //候補が1以外なら押せない
		var searchLabel = string.Format("Search ({0})", componentTypes.Length);
		GUI.SetNextControlName("SearchButton");
		if (GUILayout.Button(searchLabel)) {
			searchComponent(componentTypes[0]);
		}
		//サジェスト列挙
		if (!string.IsNullOrEmpty(mComponentNamePassFilter)) {
			GUI.enabled = (componentTypes.Length != 1); //候補が1なら押せない
			foreach (var type in componentTypes.Take(10)) {
				if (GUILayout.Button(type.FullName, EditorStyles.miniButton)) {
					mComponentNamePassFilter = type.FullName;
					GUI.FocusControl("SearchButton");
				}
			}
		}
		GUI.enabled = old_gui_enabled;
		//プレファブ結果
		if (mPrefabsPreservingTarget != null) {
			foreach (var prefab in mPrefabsPreservingTarget) {
				EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
			}
		}
		//シーン結果
		if (mScenesPreservingTarget != null) {
			foreach (var prefab in mScenesPreservingTarget) {
				EditorGUILayout.ObjectField(prefab, typeof(Object), false);
			}
		}
	}

	/// <summary>
	/// 描画(リソース)
	/// </summary>
	public void OnGUIForResource() {
		EditorGUILayout.HelpBox("Not Implemented", MessageType.Info);
	}

	/// <summary>
	/// 描画(オプション)
	/// </summary>
	public void OnGUIForOption() {
		EditorGUILayout.HelpBox("Not Implemented", MessageType.Info);
	}

	/// <summary>
	/// ツールインデックス
	/// </summary>
	private enum ToolIndex {
		Component,
		Resource,
		Option,
	}

	/// <summary>
	/// 検索場所
	/// </summary>
	[System.Flags]
	private enum LookIn {
		Prefab	= 1 << 0,
		Scene	= 1 << 1,
	}

	/// <summary>
	/// 選択中のツール
	/// </summary>
	private ToolIndex mCurrentToolIndex = 0;

	/// <summary>
	/// コンポーネント名パスフィルタ
	/// </summary>
	private string mComponentNamePassFilter = string.Empty;

	/// <summary>
	/// ターゲットを格納しているプレファブ群
	/// </summary>
	private GameObject[] mPrefabsPreservingTarget = null;

	/// <summary>
	/// ターゲットを格納しているシーン群
	/// </summary>
	private Object[] mScenesPreservingTarget = null;

	/// <summary>
	/// 検索場所
	/// </summary>
	private LookIn mLookIn = LookIn.Prefab;

	/// <summary>
	/// コンポーネント型
	/// </summary>
	private System.Type[] mComponentTypes = null;

	/// <summary>
	/// リセット
	/// </summary>
	private void reset() {
		mPrefabsPreservingTarget = null;
		mScenesPreservingTarget = null;
	}

	/// <summary>
	/// コンポーネント検索
	/// </summary>
	/// <param name="componentType">検索コンポーネント型</param>
	private void searchComponent(System.Type componentType) {
		reset();
		if ((mLookIn & LookIn.Prefab) != 0) {
			searchComponentInPrefabs(componentType);
		}
		if ((mLookIn & LookIn.Scene) != 0) {
			searchComponentInScenes(componentType);
		}
	}

	/// <summary>
	/// 空のシーン作成
	/// </summary>
	/// <param name="is_force">強行するか</param>
	/// <returns>true:成功、false:キャンセル</returns>
	private bool createEmptyScene(bool is_force) {
		var result = false;
		if (is_force || EditorApplication.SaveCurrentSceneIfUserWantsTo()) {
			EditorApplication.NewEmptyScene();
			result = true;
		}
		return result;
	}

	/// <summary>
	/// プレファブ内コンポーネント検索
	/// </summary>
	/// <param name="componentType">検索コンポーネント型</param>
	private void searchComponentInPrefabs(System.Type componentType) {
		if (createEmptyScene(false)) {
			mPrefabsPreservingTarget = getAllPrefabPaths().Where(x=>hasComponentInPrefabPath(x, componentType))
														.Select(x=>(GameObject)AssetDatabase.LoadAssetAtPath(x, typeof(GameObject)))
														.ToArray();
			createEmptyScene(true);
		}
	}

	/// <summary>
	/// プレファブ内のコンポーネント格納確認
	/// </summary>
	/// <param name="path">プレファブパス</param>
	/// <param name="componentType">検索コンポーネント型</param>
	/// <returns>true:格納している、false:格納していない</returns>
	/// <remarks>シーンを汚すので注意</remarks>
	private bool hasComponentInPrefabPath(string path, System.Type componentType) {
		var gc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)));
		var result = 0 < gc.GetComponentsInChildren(componentType, true).Length;
		DestroyImmediate(gc);
		return result;
	}

	/// <summary>
	/// シーン内コンポーネント検索
	/// </summary>
	/// <param name="componentType">検索コンポーネント型</param>
	private void searchComponentInScenes(System.Type componentType) {
		if (createEmptyScene(false)) {
			mScenesPreservingTarget = getAllScenePaths().Where(x=>hasComponentInScenePaths(x, componentType))
														.Select(x=>AssetDatabase.LoadAssetAtPath(x, typeof(Object)))
														.ToArray();
			createEmptyScene(true);
		}
	}

	/// <summary>
	/// シーン内のコンポーネント格納確認
	/// </summary>
	/// <param name="path">シーンパス</param>
	/// <param name="componentType">検索コンポーネント型</param>
	/// <returns>true:格納している、false:格納していない</returns>
	/// <remarks>シーンを破壊ので注意</remarks>
	private bool hasComponentInScenePaths(string path, System.Type componentType) {
		createEmptyScene(true);
		EditorApplication.OpenScene(path);
		return getTopGameObjectsInScene().Any(x=>0 < x.GetComponentsInChildren(componentType, true).Length);
	}

	/// <summary>
	/// 全ての型を取得する
	/// </summary>
	/// <returns>全ての型</returns>
	private static IEnumerable<System.Type> getAllTypes() {
		return System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(x=>x.GetTypes());
	}

	/// <summary>
	/// 全てのコンポーネント型を取得する
	/// </summary>
	/// <returns>全てのコンポーネント型</returns>
	private static IEnumerable<System.Type> getAllComponentTypes() {
		var componentType = typeof(MonoBehaviour);
		return getAllTypes().Where(x=>x.IsSubclassOf(componentType));
	}

	/// <summary>
	/// 指定されたコンポーネント型を取得する
	/// </summary>
	/// <param name="value">検索文字列</param>
	/// <returns>指定されたコンポーネント型</returns>
	private IEnumerable<System.Type> getComponentTypes(string indexOf) {
		return mComponentTypes.Where(x=>0 <= x.FullName.IndexOf(indexOf, System.StringComparison.CurrentCultureIgnoreCase));
	}

	/// <summary>
	/// 指定されたコンポーネント型を取得する
	/// </summary>
	/// <param name="pattern">パスフィルタ正規表現</param>
	/// <returns>指定されたコンポーネント型</returns>
	private IEnumerable<System.Type> getComponentTypes(Regex pattern) {
		return mComponentTypes.Where(x=>pattern.Match(x.FullName).Success);
	}

	/// <summary>
	/// 全アセットのパスを取得する
	/// </summary>
	/// <returns>全アセットのパス</returns>
	private static IEnumerable<string> getAllAssetPaths() {
		return AssetDatabase.GetAllAssetPaths();
	}

	/// <summary>
	/// 全プレファブのパスを取得する
	/// </summary>
	/// <returns>全プレファブのパス</returns>
	private static IEnumerable<string> getAllPrefabPaths() {
		return getAllAssetPaths().Where(x=>x.EndsWith(".prefab"));
	}

	/// <summary>
	/// 全シーンのパスを取得する
	/// </summary>
	/// <returns>全シーンのパス</returns>
	private static IEnumerable<string> getAllScenePaths() {
		return getAllAssetPaths().Where(x=>x.EndsWith(".unity"));
	}

	/// <summary>
	/// シーン内の全てのゲームオブジェクトを取得する
	/// </summary>
	/// <returns>シーン内の全てのゲームオブジェクト</returns>
	private static IEnumerable<GameObject> getAllGameObjectsInScene() {
		IEnumerable<GameObject> result;
		if (string.IsNullOrEmpty(EditorApplication.currentScene)) {
			//シーン名が無いなら
			var old_objects = Selection.objects;
			Selection.objects = Resources.FindObjectsOfTypeAll<GameObject>();
			var result_array = Selection.GetFiltered(typeof(GameObject), SelectionMode.ExcludePrefab)
										.Select(x=>(GameObject)x)
										.ToArray();
			result = result_array;
			Selection.objects = old_objects;
		} else {
			//シーン名が有るなら
			result = Resources.FindObjectsOfTypeAll<GameObject>()
								.Where(x=>AssetDatabase.GetAssetOrScenePath(x) == EditorApplication.currentScene);
		}
		return result;
	}

	/// <summary>
	/// シーン内のトップゲームオブジェクトを取得する
	/// </summary>
	/// <returns>シーン内のトップゲームオブジェクト</returns>
	private static IEnumerable<GameObject> getTopGameObjectsInScene() {
		return getAllGameObjectsInScene().Where(x=>x.transform.parent == null);
	}
}