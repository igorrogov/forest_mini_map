using ModAPI.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TheForest.Utils;
using UnityEngine;

namespace MiniMap
{
    class MiniMap : MonoBehaviour
    {

        // radius of the visible area in the compass, using in-game coordinates
        private const float COMPASS_IN_GAME_RADIUS = 500;

        private const float COMPASS_SIZE_RELATIVE_TO_SCREEN_HEIGHT = 0.60f; // 15% of screen height

        private const float COMPASS_MARGIN_RELATIVE_TO_SCREEN_HEIGHT = 0.02f; // 2% of screen height

        private const float PLAYER_MARKER_SIZE_PERCENT = 0.05f; // 5% of compass size

        private const float ENEMY_MARKER_SIZE_PERCENT = 0.03f; // 3% of compass size

        private bool visible = false;

        private bool textureLoaded = false;

        private Texture2D playerMarkerTexture;

        private Texture2D enemyMarkerTexture;

        private Texture2D compassTexture;

        private readonly Dictionary<int, GameObject> enemies = new Dictionary<int, GameObject>();

        [ExecuteOnGameStart]
        private static void AddMeToScene()
        {
            new GameObject("__MiniMap__").AddComponent<MiniMap>();
        }

        void Awake()
        {
            ModAPI.Log.Write("mini map started: 10:38");
        }

        void Start()
        {
            try
            {
                string[] resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                ModAPI.Log.Write("Assembly resources: " + String.Join(", ", resourceNames));

                compassTexture = LoadTextureFromResource("MiniMap.Compass.png");
                playerMarkerTexture = LoadTextureFromResource("MiniMap.player_marker.png");
                enemyMarkerTexture = LoadTextureFromResource("MiniMap.enemy_marker.png");
                textureLoaded = true;
                ModAPI.Log.Write("Textures loaded.");
            }
            catch (Exception e)
            {
                ModAPI.Log.Write("Error starting: " + e);
            }
        }

        void Update()
        {
            if (ModAPI.Input.GetButtonDown("Show"))
            {
                visible = !visible;
            }

            if (visible) 
            {
                FindCannibals();
                // ModAPI.Log.Write("Found enemies: " + enemies.Count);
            }
        }

        private Texture2D LoadTextureFromResource(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, mipmap: false, linear: false);
                texture.LoadImage(ReadAllBytes(stream));
                return texture;
            }
        }

        private byte[] ReadAllBytes(Stream stream)
        {
            using (MemoryStream mem = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    mem.Write(buffer, 0, read);
                }
                return mem.ToArray();
            }
        }

        void OnGUI()
        {
            if (!visible || !textureLoaded)
            {
                return;
            }

            float margin = Screen.height * COMPASS_MARGIN_RELATIVE_TO_SCREEN_HEIGHT;
            float mapSize = Screen.height * COMPASS_SIZE_RELATIVE_TO_SCREEN_HEIGHT;

            float mapPosX = Screen.width - margin - mapSize;
            float mapPosY = margin;
            float mapCenterX = mapPosX + mapSize / 2;
            float mapCenterY = mapPosY + mapSize / 2;

            float rotationZ = LocalPlayer.Transform.rotation.eulerAngles.y;

            // opacity: 60%
            GUI.color = new Color(1.0f, 1.0f, 1.0f, 0.60f);

            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(180 - rotationZ, new Vector2(mapCenterX, mapCenterY));
            GUI.DrawTexture(new Rect(mapPosX, mapPosY, mapSize, mapSize), compassTexture, ScaleMode.ScaleToFit, true);
            GUI.matrix = matrix;

            DrawEnemyMarkers(mapSize, mapCenterX, mapCenterY, 180 - rotationZ);

            DrawPlayerMarker(mapSize, mapCenterX, mapCenterY);
        }

        private void DrawPlayerMarker(float mapSize, float mapCenterX, float mapCenterY)
        {
            float size = mapSize * PLAYER_MARKER_SIZE_PERCENT;

            float posX = mapCenterX - size / 2;
            float posY = mapCenterY - size / 2;

            // player marker (always facing north/up)
            GUI.DrawTexture(new Rect(posX, posY, size, size), playerMarkerTexture, ScaleMode.ScaleToFit, true);
        }

        private void DrawEnemyMarkers(float mapSize, float mapCenterX, float mapCenterY, float mapRotation)
        {
            foreach(GameObject go in enemies.Values)
            {
                DrawEnemyMarker(mapSize, mapCenterX, mapCenterY, mapRotation, go);
            }
        }

        private void DrawEnemyMarker(float mapSize, float mapCenterX, float mapCenterY, float mapRotation, GameObject go)
        {
            float mapRadius = mapSize / 2;
            float size = mapSize * ENEMY_MARKER_SIZE_PERCENT;

            float relX = -(go.transform.position.x - LocalPlayer.Transform.position.x);
            float relY = go.transform.position.z - LocalPlayer.Transform.position.z;
            Vector2 vec = new Vector2(relX, relY);
            float magnitude = vec.magnitude;
            if (magnitude > COMPASS_IN_GAME_RADIUS)
            {
                // not visible on the map
                return;
            }

            float relativeMagnitude = magnitude / COMPASS_IN_GAME_RADIUS;
            vec = vec.normalized * mapRadius * relativeMagnitude;

            // ModAPI.Log.Write("[VIS] GUI vec: " + vec);

            float centerX = mapCenterX + vec.x;
            float centerY = mapCenterY + vec.y;
            float posX = centerX - size / 2;
            float posY = centerY - size / 2;

            float rotation = go.GetComponentInChildren<Animator>().rootRotation.eulerAngles.y;

            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(rotation + 180, new Vector2(centerX, centerY));
            GUIUtility.RotateAroundPivot(mapRotation, new Vector2(mapCenterX, mapCenterY));
            GUI.DrawTexture(new Rect(posX, posY, size, size), enemyMarkerTexture, ScaleMode.ScaleToFit, true);
            GUI.matrix = matrix;
        }

        private void FindCannibals()
        {
            if (Scene.MutantControler == null || Scene.MutantControler.ActiveWorldCannibals == null || !visible)
            {
                return;
            }

            enemies.Clear();

            List<GameObject> activeCannibals = LocalPlayer.IsInCaves ? Scene.MutantControler.ActiveCaveCannibals : Scene.MutantControler.ActiveWorldCannibals;
            activeCannibals.ForEach(c => enemies[c.GetInstanceID()] = c);

            foreach (GameObject c in Scene.MutantControler.activeInstantSpawnedCannibals)
            {
                int key = c.GetInstanceID();
                if (!enemies.ContainsKey(key))
                {
                    enemies[key] = c;
                }
            }
        }

    }
}
