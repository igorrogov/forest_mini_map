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
        private const float MAP_IN_GAME_SIZE = 500;

        private const float MAP_SIZE_RELATIVE_TO_SCREEN_HEIGHT = 0.50f; // 50% of screen height

        private const float MAP_MARGIN_RELATIVE_TO_SCREEN_HEIGHT = 0.05f; // 5% of screen height

        private const float PLAYER_MARKER_SIZE_PERCENT = 0.05f; // 5% of map size

        private const float ENEMY_MARKER_SIZE_PERCENT = 0.03f; // 3% of map size

        private float mapSizeRelativeToScreenHeight = MAP_SIZE_RELATIVE_TO_SCREEN_HEIGHT;

        private float mapOpacity = 0.75f; // 75%

        private float mapInGameSize = MAP_IN_GAME_SIZE;

        // true if the map is visible
        private bool visible = false;

        // true if we are in the Settings mode (can use mouse to change settings)
        private bool inSettings = false;

        private bool textureLoaded = false;

        private Texture2D playerMarkerTexture;

        private Texture2D enemyMarkerTexture;

        private Texture2D entranceTexture;

        private Texture2D overworldTexture;

        private Texture2D cavesTexture;

        private readonly Dictionary<int, GameObject> enemies = new Dictionary<int, GameObject>();

        [ExecuteOnGameStart]
        private static void AddMeToScene()
        {
            new GameObject("__MiniMap__").AddComponent<MiniMap>();
        }

        void Awake()
        {
            ModAPI.Log.Write("mini map started: 11:08");
        }

        void Start()
        {
            try
            {
                // string[] resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                // ModAPI.Log.Write("Assembly resources: " + String.Join(", ", resourceNames));

                playerMarkerTexture = LoadTextureFromResource("MiniMap.player_marker.png");
                enemyMarkerTexture = LoadTextureFromResource("MiniMap.enemy_marker.png");
                entranceTexture = LoadTextureFromResource("MiniMap.entrance.png");
                overworldTexture = LoadTextureFromResource("MiniMap.overworld.jpg");
                cavesTexture = LoadTextureFromResource("MiniMap.caves.jpg");
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
                if (ModAPI.Input.GetButtonDown("Settings"))
                {
                    inSettings = !inSettings;
                    if (inSettings)
                    {
                        LocalPlayer.FpCharacter.LockView();
                    }
                    else
                    {
                        LocalPlayer.FpCharacter.UnLockView();
                    }
                }

                FindCannibals();
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

            float margin = Screen.height * MAP_MARGIN_RELATIVE_TO_SCREEN_HEIGHT;
            float mapSize = Screen.height * mapSizeRelativeToScreenHeight;
            float mapScale = mapSize / mapInGameSize;

            float mapPosX = Screen.width - margin - mapSize;
            float mapPosY = margin;
            float mapCenterX = mapPosX + mapSize / 2;
            float mapCenterY = mapPosY + mapSize / 2;

            // opacity: 75% by default
            GUI.color = new Color(1.0f, 1.0f, 1.0f, mapOpacity);

            Texture2D mapTexture = LocalPlayer.IsInCaves ? cavesTexture : overworldTexture;
            GUI.DrawTextureWithTexCoords(new Rect(mapPosX, mapPosY, mapSize, mapSize), mapTexture, GetPlayerLocationTextCoordinates(), true);

            //Matrix4x4 matrix = GUI.matrix;
            //GUIUtility.RotateAroundPivot(180 - rotationZ, new Vector2(mapCenterX, mapCenterY));
            //GUI.DrawTexture(new Rect(mapPosX, mapPosY, mapSize, mapSize), compassTexture, ScaleMode.ScaleToFit, true);
            //GUI.matrix = matrix;

            DrawEnemyMarkers(mapSize, mapCenterX, mapCenterY, mapScale);
            DrawCaveEntrances(mapSize, mapCenterX, mapCenterY, mapScale);
            DrawPlayerMarker(mapSize, mapCenterX, mapCenterY);

            if (inSettings)
            {
                DrawControls(margin);
            }
        }

        private void DrawControls(float areaSize)
        {
            float posX = Screen.width - areaSize;
            float posY = areaSize;
            float buttonSize = areaSize * 0.8f;
            float margin = areaSize * 0.1f;

            if (GUI.Button(new Rect(posX + margin, posY, buttonSize, buttonSize), "-"))
            {
                mapSizeRelativeToScreenHeight -= 0.05f; // -5%
            }

            posY += margin + buttonSize;
            if (GUI.Button(new Rect(posX + margin, posY, buttonSize, buttonSize), "+"))
            {
                mapSizeRelativeToScreenHeight += 0.05f; // +5%
            }

            posY += margin * 3 + buttonSize;
            if (GUI.Button(new Rect(posX + margin, posY, buttonSize, buttonSize), "-"))
            {
                mapOpacity -= 0.05f; // -5%
            }

            posY += margin + buttonSize;
            if (GUI.Button(new Rect(posX + margin, posY, buttonSize, buttonSize), "+"))
            {
                mapOpacity += 0.05f; // +5%
            }

            posY += margin * 3 + buttonSize;
            if (GUI.Button(new Rect(posX + margin, posY, buttonSize, buttonSize), "-"))
            {
                mapInGameSize += 100;
            }

            posY += margin + buttonSize;
            if (GUI.Button(new Rect(posX + margin, posY, buttonSize, buttonSize), "+"))
            {
                mapInGameSize -= 100;
            }
        }


        // how much the map image is bigger than the actual in game world (1750x2 -> 4096)
        private const float WORLD_TO_MAP_IMAGE_SCALE_X = 1.17269076305f;
        private const float WORLD_TO_MAP_IMAGE_SCALE_Y = 1.168f;

        // dimensions of the map image PNG files: 4096 x 4096
        private const float MAP_IMAGE_SIZE = 4096;
        private const float MAP_IMAGE_OFFSET_X = 2.5f;
        private const float MAP_IMAGE_OFFSET_Y = 0.0f;


        private Rect GetPlayerLocationTextCoordinates()
        {
            Vector2 m = ToMapCoordinates(LocalPlayer.Transform.position);
            float posX = m.x - mapInGameSize / 2;
            float posY = m.y - mapInGameSize / 2;
            return new Rect(posX / MAP_IMAGE_SIZE, posY / MAP_IMAGE_SIZE, mapInGameSize / MAP_IMAGE_SIZE, mapInGameSize / MAP_IMAGE_SIZE);
        }

        private Vector2 ToMapCoordinates(Vector3 pos)
        {
            // convert to map image coordinates (0,0 -> 4096,4096)
            float mx = -(pos.x * WORLD_TO_MAP_IMAGE_SCALE_X) + MAP_IMAGE_SIZE / 2 - MAP_IMAGE_OFFSET_X;
            float my = (pos.z * WORLD_TO_MAP_IMAGE_SCALE_Y) + MAP_IMAGE_SIZE / 2 + MAP_IMAGE_OFFSET_Y;
            my = MAP_IMAGE_SIZE - my;

            return new Vector2(mx, my);
        }

        private void DrawPlayerMarker(float mapSize, float mapCenterX, float mapCenterY)
        {
            float size = mapSize * PLAYER_MARKER_SIZE_PERCENT;

            float posX = mapCenterX - size / 2;
            float posY = mapCenterY - size / 2;
            float rotationZ = LocalPlayer.Transform.rotation.eulerAngles.y;

            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(rotationZ + 180, new Vector2(mapCenterX, mapCenterY));
            GUI.DrawTexture(new Rect(posX, posY, size, size), playerMarkerTexture, ScaleMode.ScaleToFit, true);
            GUI.matrix = matrix;
        }

        private void DrawEnemyMarkers(float mapSize, float mapCenterX, float mapCenterY, float mapScale)
        {
            foreach(GameObject go in enemies.Values)
            {
                DrawMarker(mapSize, mapCenterX, mapCenterY, go, mapScale, ENEMY_MARKER_SIZE_PERCENT, true, enemyMarkerTexture);
            }
        }

        private void DrawCaveEntrances(float mapSize, float mapCenterX, float mapCenterY, float mapScale)
        {
            foreach (GameObject go in FindCaveEntrances())
            {
                DrawMarker(mapSize, mapCenterX, mapCenterY, go, mapScale, PLAYER_MARKER_SIZE_PERCENT, false, entranceTexture);
            }
        }

        private void DrawMarker(float mapSize, float mapCenterX, float mapCenterY, GameObject go, float mapScale, float markerSizePercent, bool drawRotation, Texture2D texture)
        {
            float size = mapSize * markerSizePercent;

            float relX = -(go.transform.position.x - LocalPlayer.Transform.position.x);
            float relY = go.transform.position.z - LocalPlayer.Transform.position.z;
            Vector2 vec = new Vector2(relX, relY);

            if (Math.Abs(relX) > mapInGameSize / 2 || Math.Abs(relY) > mapInGameSize / 2)
            {
                // not visible on the map
                return;
            }

            vec *= mapScale;

            float centerX = mapCenterX + vec.x;
            float centerY = mapCenterY + vec.y;
            float posX = centerX - size / 2;
            float posY = centerY - size / 2;

            if (drawRotation)
            {
                float rotation = go.GetComponentInChildren<Animator>().rootRotation.eulerAngles.y;
                Matrix4x4 matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(rotation + 180, new Vector2(centerX, centerY));
                GUI.DrawTexture(new Rect(posX, posY, size, size), texture, ScaleMode.ScaleToFit, true);
                GUI.matrix = matrix;
            }
            else
            {
                GUI.DrawTexture(new Rect(posX, posY, size, size), texture, ScaleMode.ScaleToFit, true);
            }
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

        private List<GameObject> FindCaveEntrances()
        {
            List<GameObject> list = new List<GameObject>();

            if (Scene.SceneTracker == null)
            {
                return list;
            }

            foreach (var ce in Scene.SceneTracker.caveEntrances)
            {
                GameObject go = ce.blackBackingGo;
                if (go != null)
                {
                    list.Add(go);
                    continue;
                }
                go = ce.fadeToDarkGo;
                if (go != null)
                {
                    list.Add(go);
                    continue;
                }
                go = ce.blackBackingFadeGo;
                if (go != null)
                {
                    list.Add(go);
                    continue;
                }
            }

            return list;
        }

    }
}
