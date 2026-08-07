using UnityEngine;

namespace Eggverse
{
    /// <summary>
    /// Builds the whole game at play time so the project needs no scene setup:
    /// press Play on any scene and Eggverse starts.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<GameDirector>() != null) return;

            var go = new GameObject("Eggverse");
            go.AddComponent<GameDirector>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
