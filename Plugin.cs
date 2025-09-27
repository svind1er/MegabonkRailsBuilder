using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Assets.Scripts.Actors.Player;
using Assets.Scripts.Utility;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace MegabonkRailsBuilder
{
    [BepInPlugin(GUID, MODNAME, VERSION)]
    public class Plugin : BasePlugin
    {
        public const string
            MODNAME = "MegaRailsBuilder",
            AUTHOR = "svindler",
            GUID = AUTHOR + "_" + MODNAME,
            VERSION = "1.0.0";

        public static ManualLogSource log;

        internal static bool freezeToggle = false;
        internal static float cachedRunTimer;
        internal static float cachedStageTimer;

        public override void Load()
        {
            log = Log;
            log.LogInfo($"Loading {MODNAME} v{VERSION} by {AUTHOR}");

            AddComponent<RailsBuilderBehaviour>();

            var harmony = new Harmony(GUID);
            harmony.PatchAll();
#if DEBUG
            log.LogInfo($"{MODNAME}: Harmony patches applied.");
#endif
        }
    }

    public class RailsBuilderBehaviour : MonoBehaviour
    {
        private GameObject ghostRail;
        private int currentRailIndex;
        private bool buildMode;
        private float rotationY;
        private bool flipped;
        private SpawnInteractables spawner;
        private static Material ghostMaterial;
        private static Texture2D bgTex;

        private void Update()
        {
            if (Input.GetKeyUp(KeyCode.F1))
            {
                buildMode = !buildMode;
                if (!buildMode && ghostRail != null)
                {
                    Destroy(ghostRail);
                    ghostRail = null;
                    rotationY = 0f;
                    flipped = false;
                }
#if DEBUG
                Plugin.log.LogInfo(buildMode ? "[!] Build Mode ON" : "[!] Build Mode OFF");
#endif
            }

            if (buildMode && Input.GetKeyDown(KeyCode.F2))
            {
                Plugin.freezeToggle = !Plugin.freezeToggle;
#if DEBUG
                Plugin.log.LogInfo(Plugin.freezeToggle
                    ? "[!] Freeze ENABLED (Game time paused)."
                    : "[!] Freeze DISABLED (Game time resumed).");
#endif
            }

            if (!buildMode) return;

            if (spawner == null) spawner = UnityEngine.Object.FindObjectOfType<SpawnInteractables>();
            if (spawner == null || spawner.rails == null || spawner.rails.Length == 0) return;

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                currentRailIndex = (currentRailIndex - 1 + spawner.rails.Length) % spawner.rails.Length;
                RefreshGhost(spawner.rails[currentRailIndex]);
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                currentRailIndex = (currentRailIndex + 1) % spawner.rails.Length;
                RefreshGhost(spawner.rails[currentRailIndex]);
            }

            if (Input.GetKey(KeyCode.Q)) rotationY -= 90f * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) rotationY += 90f * Time.deltaTime;
            if (Input.GetKeyUp(KeyCode.Z)) rotationY += 90f;
            if (Input.GetKeyUp(KeyCode.X)) rotationY -= 90f;
            if (Input.GetKeyUp(KeyCode.C)) rotationY = 0;
            if (Input.GetKeyUp(KeyCode.F)) flipped = !flipped;

            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            Camera cam = Camera.main;

            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                Vector3 origin = ray.origin;
                Vector3 dir = ray.direction;
                float dist = 0f;

                while (dist < 200f)
                {
                    if (Physics.Raycast(origin, dir, out RaycastHit hit, 200f - dist))
                    {
                        dist += hit.distance + 0.01f;
                        origin = hit.point + dir * 0.01f;

                        if (hit.collider != null && hit.collider.GetComponentInParent<MyPlayer>() != null)
                            continue;

                        pos = hit.point;
                        rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, rotationY, 0);
                        break;
                    }
                    else break;
                }
            }

            if (ghostRail == null)
                RefreshGhost(spawner.rails[currentRailIndex]);

            if (ghostRail != null)
            {
                ghostRail.transform.SetPositionAndRotation(pos, rot);

                Vector3 scale = ghostRail.transform.localScale;
                scale.x = flipped ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                ghostRail.transform.localScale = scale;

                if (Input.GetMouseButtonDown(0) && !Cursor.visible)
                {
                    GameObject placed = Instantiate(spawner.rails[currentRailIndex], pos, rot);

                    Vector3 pscale = placed.transform.localScale;
                    pscale.x = flipped ? -Mathf.Abs(pscale.x) : Mathf.Abs(pscale.x);
                    placed.transform.localScale = pscale;
#if DEBUG
                    Plugin.log.LogInfo($"[!] Placed rail {currentRailIndex} at {pos}");
#endif
                }
            }
        }

        private void OnGUI()
        {
            if (!buildMode) return;

            float width = 350f;
            float height = 230f;
            float x = 350f;
            float y = 500f;

            if (bgTex == null)
            {
                bgTex = new Texture2D(1, 1);
                bgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
                bgTex.Apply();
            }
            GUI.DrawTexture(new Rect(x, y, width, height), bgTex);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.cyan },
                fontStyle = FontStyle.Bold
            };

            GUIStyle railStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.yellow },
                fontStyle = FontStyle.Bold
            };

            GUIStyle instrStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.LowerCenter,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold
            };

            GUIStyle freezeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                richText = true
            };

            GUI.Label(new Rect(x, y + 5, width, 25), "BUILD MODE", titleStyle);

            string railName = (spawner != null && spawner.rails.Length > 0) ? spawner.rails[currentRailIndex].name : "N/A";
            GUI.Label(new Rect(x, y + 35, width, 24), $"Selected Rail: {railName}", railStyle);

            string freezeText = $"<color=white>Frozen:</color> " + (Plugin.freezeToggle ? "<color=green>ON</color>" : "<color=red>OFF</color>");
            Rect freezeRect = new Rect(x, y + 55, width, 28);
            GUI.Label(freezeRect, freezeText, freezeStyle);

            string[] lines =
            {
                "← / → : Switch Rails",
                "Q / E : Rotate Rail",
                "Z / X : Rotate by 90°",
                "C : Reset rotation",
                "F : Flip",
                "LMB : Place Rail",
                "F1 : Toggle Build Mode",
                "F2 : Toggle Freeze"
            };

            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(x + 10, y + 80 + (i * 18), width - 20, 20), lines[i], instrStyle);
        }

        private void RefreshGhost(GameObject prefab)
        {
            if (ghostRail != null) Destroy(ghostRail);
            if (prefab == null) return;

            ghostRail = Instantiate(prefab);
            ghostRail.name = "GhostRail";

            if (ghostMaterial == null)
            {
                ghostMaterial = new Material(Shader.Find("Standard"))
                {
                    color = new Color(1f, 1f, 1f, 0.3f),
                    renderQueue = 3000
                };
                ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghostMaterial.SetInt("_ZWrite", 0);
                ghostMaterial.EnableKeyword("_ALPHABLEND_ON");
            }

            ApplyGhost(ghostRail.transform);
#if DEBUG
            Plugin.log.LogInfo($"[!] Created ghost for prefab: {prefab.name}");
#endif
        }

        private void ApplyGhost(Transform t)
        {
            Collider col = t.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Renderer r = t.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = ghostMaterial;

            for (int i = 0; i < t.childCount; i++)
                ApplyGhost(t.GetChild(i));
        }
    }

    [HarmonyPatch]
    internal static class EnemySpawnBlockPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var enemyMgrType = typeof(Assets.Scripts.Managers.EnemyManager);
            return AccessTools.GetDeclaredMethods(enemyMgrType).Where(m => m.Name == "SpawnEnemy");
        }

        private static bool Prefix()
        {
            if (Plugin.freezeToggle)
            {
#if DEBUG
                Plugin.log.LogInfo("[!] Blocked an enemy spawn.");
#endif
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MyTime), "Update")]
    public static class FreezeTimerPatch
    {
        private static bool Prefix()
        {
            if (Plugin.freezeToggle)
            {
                MyTime.runTimer = Plugin.cachedRunTimer;
                MyTime.stageTimer = Plugin.cachedStageTimer;
                return false;
            }
            else
            {
                Plugin.cachedRunTimer = MyTime.runTimer;
                Plugin.cachedStageTimer = MyTime.stageTimer;
                return true;
            }
        }
    }
}