using UnityEngine;

[ExecuteAlways]
public class GlobalShaderTime : MonoBehaviour
{
    void Update()
    {
        Shader.SetGlobalFloat("_UnscaledTime", Time.unscaledTime);
    }
}