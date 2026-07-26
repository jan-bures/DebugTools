using ReduxLib.Configuration;
using ReduxLib.Input;
using UnityEngine;

namespace DebugTools.Utils
{
    public static class Configuration
    {
        public static ConfigValue<KeyboardShortcut> KeyboardShortcut;
        public static ConfigValue<bool> StructuralStressGauges;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            KeyboardShortcut = null;
            StructuralStressGauges = null;
            PhysicsSettingsManager.OnPhysicsSettingsLoaded -= ApplyStructuralStressGaugeSetting;
        }

        public static void Initialize(IConfigFile config)
        {
            KeyboardShortcut = new ConfigValue<KeyboardShortcut>(config.Bind("Keybinding", "Debug UI Keyboard shortcut",
                new KeyboardShortcut(KeyCode.F12, KeyCode.LeftAlt), "Keyboard shortcut to toggle the main debug UI"));
            StructuralStressGauges = new ConfigValue<bool>(config.Bind(
                "Joints",
                "Show structural stress gauges",
                false,
                "Show structural stress gauges over loaded vessel attachment points"));

            PhysicsSettingsManager.OnPhysicsSettingsLoaded -= ApplyStructuralStressGaugeSetting;
            PhysicsSettingsManager.OnPhysicsSettingsLoaded += ApplyStructuralStressGaugeSetting;
            ApplyStructuralStressGaugeSetting();
        }

        public static void ApplyStructuralStressGaugeSetting()
        {
            PhysicsSettings.STRUCTURAL_STRESS_GAUGES_ENABLED = StructuralStressGauges?.Value ?? false;
        }
    }
}