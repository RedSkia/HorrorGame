using UnityEditor;
using UnityEditor.Build.Reporting;

// Command-line web build, used by build.cmd on a mirrored copy of the project (so the editor can stay open):
// Unity.exe -batchmode -buildTarget WebGL -executeMethod BuildWeb.Run
// -buildTarget WebGL makes Web the active platform first (otherwise URP strips the lit shaders and the build is black);
// the copy stays on WebGL, so the next build does not reimport.
public static class BuildWeb
{
    public static void Run()
    {
        BuildReport report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Game.unity" }, "Builds/Web", BuildTarget.WebGL, BuildOptions.None);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
