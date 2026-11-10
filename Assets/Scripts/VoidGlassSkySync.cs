using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

// copies the settings of the skybox material (the Red Nebula) into shader globals every frame, so the Void Glass shader can draw the exact same sky
// nothing to put in the scene: it starts itself, in the editor and in play mode
public static class VoidGlassSkySync
{
    private static readonly int VoidColor = Shader.PropertyToID("_VoidSky_VoidColor");
    private static readonly int DeepColor = Shader.PropertyToID("_VoidSky_DeepColor");
    private static readonly int BrightColor = Shader.PropertyToID("_VoidSky_BrightColor");
    private static readonly int Scale = Shader.PropertyToID("_VoidSky_Scale");
    private static readonly int Coverage = Shader.PropertyToID("_VoidSky_Coverage");
    private static readonly int Softness = Shader.PropertyToID("_VoidSky_Softness");
    private static readonly int Brightness = Shader.PropertyToID("_VoidSky_Brightness");
    private static readonly int Drift = Shader.PropertyToID("_VoidSky_Drift");
    private static readonly int StarAmount = Shader.PropertyToID("_VoidSky_StarAmount");
    private static readonly int StarBrightness = Shader.PropertyToID("_VoidSky_StarBrightness");

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        // unsubscribe first, so entering play mode without a domain reload can't subscribe twice
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
    {
        Material sky = RenderSettings.skybox;

        // only the Red Nebula has these properties. any other sky just leaves the glass as it was
        if (sky == null || !sky.HasProperty("_BrightColor"))
            return;

        Shader.SetGlobalColor(VoidColor, sky.GetColor("_VoidColor"));
        Shader.SetGlobalColor(DeepColor, sky.GetColor("_DeepColor"));
        Shader.SetGlobalColor(BrightColor, sky.GetColor("_BrightColor"));
        Shader.SetGlobalFloat(Scale, sky.GetFloat("_Scale"));
        Shader.SetGlobalFloat(Coverage, sky.GetFloat("_Coverage"));
        Shader.SetGlobalFloat(Softness, sky.GetFloat("_Softness"));
        Shader.SetGlobalFloat(Brightness, sky.GetFloat("_Brightness"));
        Shader.SetGlobalFloat(Drift, sky.GetFloat("_Drift"));
        Shader.SetGlobalFloat(StarAmount, sky.GetFloat("_StarAmount"));
        Shader.SetGlobalFloat(StarBrightness, sky.GetFloat("_StarBrightness"));
    }
}
