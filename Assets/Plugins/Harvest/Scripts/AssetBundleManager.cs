using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Harvest
{
    public class AssetBundleManager : MonoBehaviour
    {
        public string assetBundleLocation = "/AssetBundles/";

        static AssetBundleManager sInstance = null;
        public static AssetBundleManager Instance
        {
            get
            {
                if (sInstance == null)
                {
                    GameObject manager = new GameObject("Harvest.AssetBundleManager");
                    sInstance = manager.AddComponent<AssetBundleManager>();
                }
                return sInstance;
            }
        }

        public enum AssetBundleManagerState
        {
            Uninitialized,
            LoadingManifest,
            Ready,
            Working
        }
        public AssetBundleManagerState state;

        class AssetBundleStatus
        {
            public AssetBundle bundle;
            public int refcount;
            public AssetBundleLoadType type;
        }

        AssetBundleManifest mManifest;

        Dictionary<string, AssetBundleStatus> mLoadedAssets = new Dictionary<string, AssetBundleStatus>();

        string mPrimaryBundle;
      

        public enum AssetBundleLoadType
        {
            Manifest,
            Primary,
            Dependency,
            Permanent
        }

        /// <summary>
        /// buffer entry for loading an asset bundle file.
        /// </summary>
        class AssetBundleLoadStatus
        {
            public AssetBundleLoadType type;
            public AssetBundleCreateRequest request;
        }
        List<AssetBundleLoadStatus> mPendingAssetBundleLoads = new List<AssetBundleLoadStatus>();

        /// <summary>
        /// buffer entry for a load operation out of an asset bundle
        /// </summary>

        public delegate void AssetLoadedCallback(UnityEngine.Object [] asset);

        class AssetBundleLoadRequest
        {
            public AssetBundleRequest request;
            public AssetLoadedCallback callback;
        }
        List<AssetBundleLoadRequest> mPendingAssetBundleRequests = new List<AssetBundleLoadRequest>();

        // storage for bundle loads that occur before asset bundle 
        struct DeferredAssetBundleLoad
        {
            public string bundle;
            public AssetBundleLoadType type;
        }
        List<DeferredAssetBundleLoad> mDeferredAssetBundleLoads = new List<DeferredAssetBundleLoad>();

        List<string> mPendingAssetBundleUnloads = new List<string>();

        bool Initialized()
        {
            return state != AssetBundleManagerState.Uninitialized &&
                state != AssetBundleManagerState.LoadingManifest;
        }

        void LoadManifest()
        {
            if(state != AssetBundleManagerState.Uninitialized)
            {
                Debug.LogError("AssetBundleManager:  Should only load manifest once");
            }
            string bundleName = RuntimePlatformToAssetBundlePath(Application.platform);
            LoadAssetBundle(bundleName, AssetBundleLoadType.Manifest);
            state = AssetBundleManagerState.LoadingManifest;
        }

        public AssetBundle GetLoadedBundle(string assetBundleName)
        {
            if(mLoadedAssets.ContainsKey(assetBundleName))
                return mLoadedAssets[assetBundleName].bundle;
            return null;
        }

        public AssetBundle GetCurrentPrimaryBundle()
        {
            if (mPrimaryBundle != null && mLoadedAssets.ContainsKey(mPrimaryBundle))
                return mLoadedAssets[mPrimaryBundle].bundle;
            return null;
        }

        public AssetBundle [] GetPreviousPrimaryBundles()
        {
            List<AssetBundle> otherBundles = new List<AssetBundle>();
            foreach(var bundle in mLoadedAssets)
            {
                if(!bundle.Key.Equals(mPrimaryBundle) &&
                    bundle.Value.type == AssetBundleLoadType.Primary)
                {
                    otherBundles.Add(bundle.Value.bundle);
                }
            }

            return otherBundles.ToArray();
        }

        public void UnloadOtherPrimaryBundles()
        {
            foreach (var bundle in mLoadedAssets)
            {
                if (!bundle.Key.Equals(mPrimaryBundle) &&
                    bundle.Value.type == AssetBundleLoadType.Primary)
                {
                    UnloadAssetBundle(bundle.Key);
                }
            }
        }

        public void LoadAssetBundle(string assetbundle, AssetBundleLoadType type)
        {
            string platform = RuntimePlatformToAssetBundlePath(Application.platform);
            string fullpath = Application.streamingAssetsPath + assetBundleLocation + platform + "/" + assetbundle;
            if (Initialized() && type != AssetBundleLoadType.Manifest)
            {
                if(type == AssetBundleLoadType.Primary 
                   || type == AssetBundleLoadType.Permanent)
                {
                    if(type == AssetBundleLoadType.Primary)
                        mPrimaryBundle = assetbundle;
                    // promotion of bundle previously loaded as dependency
                    if (mLoadedAssets.ContainsKey(assetbundle) 
                        && mLoadedAssets[assetbundle].type == AssetBundleLoadType.Dependency)
                    {
                        mLoadedAssets[assetbundle].type = type;
                    }
                    // don't load dependencies for a bundle that's already loaded
                    if (!mLoadedAssets.ContainsKey(assetbundle))
                    {
                        // get dependencies
                        string[] deps = mManifest.GetAllDependencies(assetbundle);
                        foreach (var dep in deps)
                        {
                            if (!mLoadedAssets.ContainsKey(dep))
                            {
                                LoadAssetBundle(dep, AssetBundleLoadType.Dependency);
                            }
                            else
                            {
                                mLoadedAssets[dep].refcount++;
                            }
                        }
                    }
                }
                // don't load already loaded bundles
                if (!mLoadedAssets.ContainsKey(assetbundle))
                {
                    AssetBundleLoadStatus status = new AssetBundleLoadStatus();
                    status.request = AssetBundle.LoadFromFileAsync(fullpath);
                    status.type = type;
                    mPendingAssetBundleLoads.Add(status);
                    state = AssetBundleManagerState.Working;
                }
            }
            else if(!Initialized())
            {
                if(type == AssetBundleLoadType.Manifest)
                {
                    AssetBundleLoadStatus status = new AssetBundleLoadStatus();
                    status.request = AssetBundle.LoadFromFileAsync(fullpath);
                    status.type = AssetBundleLoadType.Manifest;
                    mPendingAssetBundleLoads.Add(status);
                }
                else if(type == AssetBundleLoadType.Primary
                        || type == AssetBundleLoadType.Permanent)
                {
                    DeferredAssetBundleLoad load = new DeferredAssetBundleLoad();
                    load.bundle = assetbundle;
                    load.type = type;
                    mDeferredAssetBundleLoads.Add(load);
                }
            }
        }

        public void LoadFromAsset<T>(string bundle, string assetName, AssetLoadedCallback callback)
        {
            AssetBundleLoadRequest loadRequest = new AssetBundleLoadRequest();
            loadRequest.callback = callback;
            loadRequest.request = mLoadedAssets[bundle].bundle.LoadAssetAsync<T>(assetName);
            mPendingAssetBundleRequests.Add(loadRequest);
        }

        public void LoadAllFromAsset<T>(string bundle, AssetLoadedCallback callback)
        {
            AssetBundleLoadRequest loadRequest = new AssetBundleLoadRequest();
            loadRequest.callback = callback;
            loadRequest.request = mLoadedAssets[bundle].bundle.LoadAllAssetsAsync<T>();
            mPendingAssetBundleRequests.Add(loadRequest);
        }

        public void UnloadAssetBundle(string bundle)
        {
            if(mLoadedAssets[bundle].type == AssetBundleLoadType.Primary)
            {
                if(mLoadedAssets[bundle].refcount > 0)
                {
                    mLoadedAssets[bundle].type = AssetBundleLoadType.Dependency;
                }
                string[] deps = mManifest.GetAllDependencies(bundle);
                foreach(var dep in deps)
                {
                    if (mLoadedAssets[dep].type == AssetBundleLoadType.Dependency)
                    {
                        mLoadedAssets[dep].refcount--;
                        if (mLoadedAssets[dep].refcount <= 0 && !mPendingAssetBundleUnloads.Contains(mLoadedAssets[dep].bundle.name))
                        {
                            mLoadedAssets[dep].bundle.Unload(true);
                            mPendingAssetBundleUnloads.Add(dep);
                        }
                    }
                }
            }
            if (!mPendingAssetBundleUnloads.Contains(bundle))
            {
                mLoadedAssets[bundle].bundle.Unload(true);
                mPendingAssetBundleUnloads.Add(bundle);
            }
        }

        private void Awake()
        {
            if (sInstance == null)
                sInstance = this;
            DontDestroyOnLoad(gameObject);
            LoadManifest();
        }

        private void Update()
        {
            // clean out any unloaded bundles
            for(int i = mPendingAssetBundleUnloads.Count-1; i >= 0; i--)
            {
                mLoadedAssets.Remove(mPendingAssetBundleUnloads[i]);
                mPendingAssetBundleUnloads.RemoveAt(i);
            }

            // update pending asset bundle loads
            for(int i = mPendingAssetBundleLoads.Count-1; i >= 0; i--)
            {
                var pending = mPendingAssetBundleLoads[i];
                if(pending.request.isDone)
                {
                    AssetBundleStatus status = new AssetBundleStatus();
                    status.bundle = pending.request.assetBundle;
                    status.type = pending.type;
                    status.refcount = pending.type == AssetBundleLoadType.Dependency ? 1 : 0;
                    mLoadedAssets.Add(pending.request.assetBundle.name, status);
                    if (pending.type == AssetBundleLoadType.Manifest)
                    {
                        LoadAllFromAsset<AssetBundleManifest>(pending.request.assetBundle.name, delegate(Object[] o)
                        {
                            mManifest = o[0] as AssetBundleManifest;
                            state = AssetBundleManagerState.Ready;
                        });
                    }
                    mPendingAssetBundleLoads.RemoveAt(i);
                }                
            }

            if(Initialized() && mPendingAssetBundleLoads.Count == 0)
            {
                state = AssetBundleManagerState.Ready;
            }

            // update pending loads _from_ assetbundles
            for(int i = mPendingAssetBundleRequests.Count - 1; i >=0; i--)
            {
                var pending = mPendingAssetBundleRequests[i];
                if(pending.request.isDone)
                {
                    pending.callback(pending.request.allAssets);
                    mPendingAssetBundleRequests.RemoveAt(i);
                }
            }

            if(state == AssetBundleManagerState.Ready)
            {
                for(int i = mDeferredAssetBundleLoads.Count-1; i >=0; i--)
                {
                    LoadAssetBundle(mDeferredAssetBundleLoads[i].bundle, mDeferredAssetBundleLoads[i].type);
                    mDeferredAssetBundleLoads.RemoveAt(i);
                }
            }
        }
        
        string RuntimePlatformToAssetBundlePath(RuntimePlatform plat)
        {
            switch(plat)
            {
                case RuntimePlatform.PS4:
                    return "PS4";
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "StandaloneWindows64";
                default:
                    return null;
            }
        }

    }
}
