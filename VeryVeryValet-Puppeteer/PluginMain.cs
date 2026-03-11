using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Cinemachine;
using TemplatePlugin;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VeryVeryValet_Puppeteer
{
    [BepInPlugin(TemplatePluginInfo.PLUGIN_GUID, TemplatePluginInfo.PLUGIN_NAME, Version)]
    public class PluginMain : BaseUnityPlugin
    {
        public const string GameName = TemplatePluginInfo.GAME_NAME;
        private const string Version = "1.0.0";

        private readonly Harmony _harmony = new Harmony(TemplatePluginInfo.PLUGIN_GUID);
        public static ManualLogSource? logger;
        public static Transform TargetPlayer;
        private static CinemachineVirtualCamera _vcam;
        
        void Awake()
        {
            _harmony.PatchAll();
            PlayerMgr.OnPlayerSpawned += (player) =>
            {
                if (FindObjectOfType<LevelBasics>() == null)
                    return;
                if (_vcam == null)
                {
                    TargetPlayer = player.transform;
                    SetupVcam();
                }
            };
        }

        [HarmonyPatch(typeof(ActiveSceneManager))]
        class ActiveSceneManager_Patch
        {
            [HarmonyPatch(nameof(ActiveSceneManager.GoToLevel))]
            [HarmonyPostfix]
            public static void GoToLevel(LevelData data, bool instant = false, bool isRetry = false)
            {
                if (_vcam != null) 
                    _vcam.gameObject.SetActive(true);
            }

            [HarmonyPatch(nameof(ActiveSceneManager.ExitGame))]
            [HarmonyPostfix]
            public static void ExitGame()
            {
                if (_vcam != null) 
                    _vcam.gameObject.SetActive(false);
            }
        }
        
        static void SetupVcam()
        {
            if (_vcam != null) return;

            var camObj = new GameObject("Puppeteer_VCam");
            _vcam = camObj.AddComponent<CinemachineVirtualCamera>();
            
            var body = _vcam.AddCinemachineComponent<CinemachineTransposer>();
            body.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
            body.m_FollowOffset = new Vector3(0f, 6f, -10f); 
            body.m_YawDamping = 2f;
            body.m_ZDamping = 1f;
            
            var aim = _vcam.AddCinemachineComponent<CinemachineComposer>();
            aim.m_TrackedObjectOffset = new Vector3(0, 1.5f, 0); 

            _vcam.Follow = TargetPlayer;
            _vcam.LookAt = TargetPlayer;
            _vcam.Priority = 999;
        }

        [HarmonyPatch(typeof(Toyful.CharacterState_RunJump), "UpdateState")]
        class CharacterState_RunJump_Patch
        {
            static void Postfix(Toyful.CharacterState_RunJump __instance)
            {
                var input = __instance.m_UnitGoal;
                if (input.sqrMagnitude < 0.01f) return;

                var camPos = Camera.main.transform.position;
                var playerPos = __instance.transform.position;

                var directionToPlayer = playerPos - camPos;
                directionToPlayer.y = 0;
                directionToPlayer.Normalize();
                var directionRight = Vector3.Cross(Vector3.up, directionToPlayer);
                var stableMoveDir = (directionToPlayer * input.z + directionRight * input.x).normalized;

                var currentVelocityFlat = __instance._setup.rider.rigidbody.velocity;
                currentVelocityFlat.y = 0;
                if (currentVelocityFlat.sqrMagnitude > 0.1f)
                {
                    var velDir = currentVelocityFlat.normalized;
                    var dot = Vector3.Dot(stableMoveDir, velDir);
                    if (dot < -0.4f) 
                    {
                        stableMoveDir = Vector3.Slerp(velDir, stableMoveDir, 0.5f).normalized;
                    }
                }

                __instance.m_UnitGoal = stableMoveDir * input.magnitude;
                __instance._setup.rider.uprightJointTargetRot = Quaternion.LookRotation(stableMoveDir);
            }
        }
    }
}