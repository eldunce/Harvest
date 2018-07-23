using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class SimpleBundleBuilder : Editor {

	[MenuItem("Harvest/Test/Build Bundles")]
    static void BuildBundles()
    {
        BuildPipeline.BuildAssetBundles("Assets/StreamingAssets/AssetBundles/StandaloneWindows64", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
    }
}
