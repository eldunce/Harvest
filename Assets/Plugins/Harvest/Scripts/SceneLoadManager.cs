using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine.SceneManagement;

namespace Harvest
{
    public class SceneLoadManager : MonoBehaviour
    {
        static SceneLoadManager sInstance;

        public enum LoadingState
        {
            // no current operations
            Ready,
            // currently loading scenes
            Loading,
            // waiting for deferred activation scenes to get activated
            Loaded,
            // currently unloading scenes
            Unloading
        }

        public LoadingState currentState = LoadingState.Ready;

        public ThreadPriority loadingPriority = ThreadPriority.Low;
        
        public static SceneLoadManager Instance
        {
            get
            {
                if(sInstance == null)
                {
                    GameObject instance = new GameObject("Harvest.SceneLoadManager");
                    sInstance = instance.AddComponent<SceneLoadManager>();
                }
                return sInstance;
            }
        }

        List<AsyncOperation> mCurrentLoadOps = new List<AsyncOperation>();
        List<AsyncOperation> mPendingSceneStarts = new List<AsyncOperation>();
        List<AsyncOperation> mCurrentUnloadOps = new List<AsyncOperation>();

        private void Awake()
        {
            if (sInstance == null)
                sInstance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadScene(string scenepath, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Application.backgroundLoadingPriority = loadingPriority;
#if UNITY_EDITOR
            AsyncOperation op = EditorSceneManager.LoadSceneAsync(scenepath, mode);
#else
            AsyncOperation op = SceneManager.LoadSceneAsync(scenepath, mode);
#endif
            if (op != null)
            {
                op.allowSceneActivation = false;
                mCurrentLoadOps.Add(op);
                currentState = LoadingState.Loading;
            }
        }

        public void ActivateLoadedScenes()
        {
            for(int i = mPendingSceneStarts.Count-1; i >=0; i --)
            {
                mPendingSceneStarts[i].allowSceneActivation = true;
                mPendingSceneStarts.RemoveAt(i);
            }
        }

        public void UnloadScene(string scene)
        {
#if UNITY_EDITOR
            mCurrentUnloadOps.Add(EditorSceneManager.UnloadSceneAsync(scene));
#else
            mCurrentUnloadOps.Add(SceneManager.UnloadSceneAsync(scene));
#endif
        }

        public void UnloadOtherScenes(string [] ignoreScenes)
        {
#if UNITY_EDITOR
            int numScenes = EditorSceneManager.loadedSceneCount;
#else
            int numScenes = SceneManager.sceneCount;
#endif
            for(int i = 0; i < numScenes; i++)
            {
                Scene scene;
#if UNITY_EDITOR
                scene = EditorSceneManager.GetSceneAt(i);
#else
                scene = SceneManager.GetSceneAt(i);
#endif
                if (System.Array.Exists<string>(ignoreScenes, (x)=>x.Equals(scene.path)))
                {
                    continue;
                }
#if UNITY_EDITOR
                mCurrentUnloadOps.Add(EditorSceneManager.UnloadSceneAsync(scene));
#else
                mCurrentUnloadOps.Add(SceneManager.UnloadSceneAsync(scene));
#endif
            }

        }

        private void Update()
        {
            switch (currentState)
            {
                case LoadingState.Loading:
                    for (int i = mCurrentLoadOps.Count - 1; i >= 0; i--)
                    {
                        if (mCurrentLoadOps[i].isDone)
                        {                           
                            mCurrentLoadOps.RemoveAt(i);
                        }
                        else if(mCurrentLoadOps[i].allowSceneActivation == false &&
                            mCurrentLoadOps[i].progress >= .9f)
                        {
                            mPendingSceneStarts.Add(mCurrentLoadOps[i]);
                            mCurrentLoadOps.RemoveAt(i);
                        }
                    }
                    if (mCurrentLoadOps.Count == 0 && mPendingSceneStarts.Count > 0)
                    {
                        currentState = LoadingState.Loaded;
                    }
                    else if(mCurrentLoadOps.Count == 0)
                    {
                        currentState = LoadingState.Ready;
                    }
                    break;
                case LoadingState.Loaded:
                    if (mPendingSceneStarts.Count == 0)
                        currentState = LoadingState.Ready;
                    break;
                case LoadingState.Unloading:
                    for(int i = mCurrentUnloadOps.Count-1; i >=0; i--)
                    {
                        if(mCurrentUnloadOps[i] == null || mCurrentUnloadOps[i].isDone)
                        {
                            mCurrentUnloadOps.RemoveAt(i);
                        }
                    }
                    if(mCurrentUnloadOps.Count == 0)
                    {
                        currentState = LoadingState.Ready;
                    }
                    break;
                case LoadingState.Ready:
                    if(mCurrentUnloadOps.Count > 0)
                    {
                        currentState = LoadingState.Unloading;
                    }
                    else if(mCurrentLoadOps.Count > 0)
                    {
                        currentState = LoadingState.Loading;
                    }
                    else if(mPendingSceneStarts.Count > 0)
                    {
                        currentState = LoadingState.Loaded;
                    }
                    break;
            }
                        
        }   
        public Scene[] GetLoadedScenes()
        {
            Scene[] scenes;
#if UNITY_EDITOR
            scenes = new Scene[EditorSceneManager.sceneCount];
            for(int i = 0; i < scenes.Length; i++)
            {
                scenes[i] = EditorSceneManager.GetSceneAt(i);
            }
#else
            scenes = new Scene[SceneManager.sceneCount];
            for(int i = 0; i < scenes.Length; i++)
            {
                scenes[i] = SceneManager.GetSceneAt(i);
            }
#endif
            return scenes;
        }

    }


}
