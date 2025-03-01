using UnityEngine;

using UnityEditor;
using UnityEditor.SceneManagement;

namespace GrassAndFur.UEditor
{
    [InitializeOnLoad]
    public static class GrassAndFurMasterInvalidate
    {
        static GrassAndFurMasterInvalidate()
        {
            EditorSceneManager.sceneOpened += EditorSceneManager_sceneOpened;
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
        }

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            if(obj == PlayModeStateChange.EnteredEditMode)
                InitGnFs();
        }

        private static void EditorSceneManager_sceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            DelayedInit();
        }

        private static async void DelayedInit()
        {
            await System.Threading.Tasks.Task.Delay(64);
            InitGnFs();
        }

        private static void InitGnFs()
        {
            foreach (GrassAndFurMaster master in Object.FindObjectsOfType<GrassAndFurMaster>())
                master.MasterInitialize();
        }
    }
}