using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Harvest;

public class TestHarness : MonoBehaviour {

    public string[] bundles;
    public string[] scenes;

    public enum LoadState
    {
        None,
        Loading
    }

    LoadState mState = LoadState.None;

	// Use this for initialization
	void Start () {
        DontDestroyOnLoad(gameObject);		
	}
	
	// Update is called once per frame
	void Update () {        
	}

    private void OnGUI()
    {
        if (mState == LoadState.None)
        {
            Vector2 startPos = new Vector2(200f, 100f);
            for (int i = 0; i < scenes.Length; i++)
            {
                if (GUI.Button(new Rect(startPos, new Vector2(150f, 45f)), scenes[i]))
                {
                    StartCoroutine(LoadScene(i));
                }
                startPos.y += 50f;
            }
        }
        
    }

    IEnumerator LoadScene(int index)
    {
        mState = LoadState.Loading;
        AssetBundleManager.Instance.LoadAssetBundle(bundles[index], AssetBundleManager.AssetBundleLoadType.Primary);
        while(AssetBundleManager.Instance.state != AssetBundleManager.AssetBundleManagerState.Ready)
        {
            yield return null;
        }
        string scenepath = AssetBundleManager.Instance.GetCurrentPrimaryBundle().GetAllScenePaths()[0];
        SceneLoadManager.Instance.LoadScene(scenepath, UnityEngine.SceneManagement.LoadSceneMode.Single);

        while (SceneLoadManager.Instance.currentState != SceneLoadManager.LoadingState.Loaded)
        {
            yield return null;
        }

        SceneLoadManager.Instance.ActivateLoadedScenes();

        yield return null;

        AssetBundleManager.Instance.UnloadOtherPrimaryBundles();

        mState = LoadState.None;

    }
}
