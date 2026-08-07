using UnityEditor;
using UnityEngine;

namespace Eggverse.EditorTools
{
    /// <summary>Editor entry points for the design self-check and the save file.</summary>
    public static class EggverseMenu
    {
        [MenuItem("Eggverse/Run Self-Check %#e")]
        public static void RunSelfCheck()
        {
            var report = SelfCheck.Run();
            if (report.Ok) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());

            EditorUtility.DisplayDialog(
                report.Ok ? "Self-check passed" : "Self-check FAILED",
                report.ToString(),
                "OK");
        }

        [MenuItem("Eggverse/Delete Save File")]
        public static void DeleteSave()
        {
            if (!EditorUtility.DisplayDialog("Delete save?",
                    "This removes the Eggverse save file so the next run starts fresh.",
                    "Delete", "Cancel")) return;

            SaveSystem.Delete();
            Debug.Log("Eggverse: save file deleted.");
        }

        [MenuItem("Eggverse/Reveal Save File")]
        public static void RevealSave()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}
