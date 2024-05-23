using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Threading;

namespace VDFaceTracking
{
    public class VDFaceTracking : ResoniteMod
    {
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> EyeOpennessExponent =
          new("quest_pro_eye_open_exponent",
            "Exponent to apply to eye openness.  Can be updated at runtime.  Useful for applying different curves for how open your eyes are.",
            () => 1.0f);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> EyeWideMultiplier =
          new("quest_pro_eye_wide_multiplier",
            "Multiplier to apply to eye wideness.  Can be updated at runtime.  Useful for multiplying the amount your eyes can widen by.",
            () => 1.0f);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> EyeMovementMultiplier =
          new("quest_pro_eye_movement_multiplier",
            "Multiplier to adjust the movement range of the user's eyes.  Can be updated at runtime.", () => 1.0f);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> EyeExpressionMultiplier =
          new("quest_pro_eye_expression_multiplier",
            "Multiplier to adjust the range of the user's eye expressions.  Can be updated at runtime.", () => 1.0f);

        
        private static ModConfiguration _config;

        public override string Name => "VDFaceTracking";

        public override string Author => "Zeith & dfgHiatus & Geenz & Earthmark";

        public override string Version => "1.1.0";

        public static VDProxy proxy;

        public static float EyeOpenExponent = 1.0f;
        public static float EyeWideMult = 1.0f;
        public static float EyeMoveMult = 1.0f;
        public static float EyeExpressionMult = 1.0f;

        public override void OnEngineInit()
        {
            _config = GetConfiguration();
            _config.OnThisConfigurationChanged += OnConfigurationChanged;

            new Harmony("org.zeith.VDFaceTracking").PatchAll();
        }

        private void OnConfigurationChanged(ConfigurationChangedEvent @event)
        {
            if (@event.Key == EyeOpennessExponent)
            {
                if (@event.Config.TryGetValue(EyeOpennessExponent, out var openExp))
                {
                    EyeOpenExponent = openExp;
                }
            }

            if (@event.Key == EyeWideMultiplier)
            {
                if (@event.Config.TryGetValue(EyeWideMultiplier, out var wideMulti))
                {
                    EyeWideMult = wideMulti;
                }
            }

            if (@event.Key == EyeMovementMultiplier)
            {
                if (@event.Config.TryGetValue(EyeMovementMultiplier, out var moveMulti))
                {
                    EyeMoveMult = moveMulti;
                }
            }

            if (@event.Key == EyeExpressionMultiplier)
            {
                if (@event.Config.TryGetValue(EyeExpressionMultiplier, out var eyeExpressionMulti))
                {
                    EyeExpressionMult = eyeExpressionMulti;
                }
            }
        }

        [HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(Engine) })]
        public class InputInterfaceCtorPatch
        {
            public static void Postfix(InputInterface __instance)
            {
                proxy = new VDProxy();

                if (!proxy.Initialize()) return;

                __instance.RegisterInputDriver(proxy);

                Engine.Current.OnShutdown += () => proxy.Teardown();
            }
        }
    }
}
