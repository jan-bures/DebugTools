using System;
using KSP.Game;
using UitkForKsp2.API;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace
namespace DebugTools.Utils
{
    public static class UITKHelper
    {
        private const float MinResizableWindowWidth = 240f;
        private const float MinResizableWindowHeight = 160f;

        public static void LoadUxml(string name, Action<VisualTreeAsset> callback)
        {
            GameManager.Instance.Assets.Load($"Assets/Modules/DebugTools/Assets/UI/{name}.uxml", callback);
        }

        public static PanelRenderer CreateWindowFromUxml(VisualTreeAsset uxml, string name)
        {
            // Create the window options object
            var windowOptions = new WindowOptions
            {
                WindowId = $"DebugTools_{name}",
                IsHidingEnabled = true,
                DisableGameInputForTextFields = true,
                MoveOptions = new MoveOptions
                {
                    IsMovingEnabled = true,
                    CheckScreenBounds = true
                },
                ResizeOptions = new ResizeOptions
                {
                    IsResizingEnabled = true,
                    CheckScreenBounds = true,
                    MinWidth = MinResizableWindowWidth,
                    MinHeight = MinResizableWindowHeight
                }
            };

            // Create the window
            Object.Instantiate(uxml);
            PanelRenderer window = Window.Create(windowOptions, uxml);
            window.panelSettings.sortingOrder = 9999;
            return window;
        }
    }
}