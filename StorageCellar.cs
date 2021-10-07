using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using BepInEx;
using ObjectBased.UIElements;
using UnityEngine.Events;
using ObjectBased.InteractiveItem;
using UnityEngine.Rendering;
using ObjectBased.UIElements.FloatingText;
using TMPAtlasGenerationSystem;
using Markers;
using SaveFileSystem;

namespace StorageCellar
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class StorageCellarMod : BaseUnityPlugin
    {
        // BepinEx
        public const string pluginGuid = "VIP.TommySoucy.StorageCellar";
        public const string pluginName = "StorageCellar";
        public const string pluginVersion = "1.0.0";

        // Config settings
        public static float spawnChancePerGrowingSpot = 0.05f;
        public static float mineralScale = 1;

        // Live data
        public static StorageCellarMod modInstance;
        public static GameObject storageCellar;
        public static List<MineralGrowingSpot> mineralGrowSpots;
        public static List<Ingredient> minerals;
        public static bool showInteractive;
        public static SavePool currentSavePool;
        public static bool savePoolSpecified;

        public void Awake()
        {
            modInstance = this;

            DoPatching();
        }

        public void Start()
        {
            FetchData();
            RegisterEvents();
            LoadConfig();
        }

        private void DoPatching()
        {
            var harmony = new HarmonyLib.Harmony("VIP.TommySoucy.StorageCellar");

            // LoadFromPoolPatch
            var loadFromPoolPatchOriginal = typeof(SaveLoadManager).GetMethod("LoadLastProgressFromPool");
            var loadFromPoolPatchPrefix = typeof(LoadFromPoolPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);

            harmony.Patch(loadFromPoolPatchOriginal, new HarmonyMethod(loadFromPoolPatchPrefix));

            // SaveToSlotPatch
            var saveToPoolPatchOriginal = typeof(SaveLoadManager).GetMethod("SaveProgressToPool");
            var saveToPoolPatchPrefix = typeof(SaveToPoolPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);

            harmony.Patch(saveToPoolPatchOriginal, new HarmonyMethod(saveToPoolPatchPrefix));
        }

        public void LogError(string error)
        {
            Logger.LogError(error);
        }

        public void LogWarning(string warning)
        {
            Logger.LogWarning(warning);
        }

        public void LogInfo(string info)
        {
            Logger.LogInfo(info);
        }

        private void RegisterEvents()
        {
            Managers.SaveLoad.onProgressSave.AddListener(new UnityAction(OnProgressStateSave));
            Managers.SaveLoad.onProgressLoad.AddListener(new UnityAction(OnProgressStateLoad));
            Managers.Day.onDayStart.AddListener(new UnityAction(OnDayStart));
        }

        private void LoadConfig()
        {
            try
            {
                string[] lines = System.IO.File.ReadAllLines("BepinEx/Plugins/Rooms/StorageCellar.txt");

                foreach (string line in lines)
                {
                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }

                    string trimmedLine = line.Trim();
                    string[] tokens = trimmedLine.Split('=');

                    if (tokens.Length == 0)
                    {
                        continue;
                    }

                    if (tokens[0].IndexOf("spawnChancePerGrowingSpot") == 0)
                    {
                        spawnChancePerGrowingSpot = float.Parse(tokens[1].Trim());
                    }
                    else if (tokens[0].IndexOf("mineralScale") == 0)
                    {
                        mineralScale = int.Parse(tokens[1].Trim());
                    } 
                }

                Logger.LogInfo("Configs loaded");
            }
            catch (FileNotFoundException ex) { Logger.LogInfo("Couldn't find StorageCellar.txt, using default settings instead. Error: " + ex.Message); }
            catch (Exception ex) { Logger.LogInfo("Couldn't read StorageCellar.txt, using default settings instead. Error: " + ex.Message); }
        }

        private void OnProgressStateSave()
        {
            if (savePoolSpecified)
            {
                savePoolSpecified = false;
                string[] lines = new string[mineralGrowSpots.Count];
                for (int i = 0; i < mineralGrowSpots.Count; ++i)
                {
                    lines[i] = mineralGrowSpots[i].spotMineral == null ? "null" : mineralGrowSpots[i].spotMineral.ingredient.name;
                }
                System.IO.File.WriteAllLines("BepinEx/Plugins/Rooms/StorageCellar."+ currentSavePool + ".sav", lines);
            }
        }

        private void OnProgressStateLoad()
        {
            if (savePoolSpecified)
            {
                foreach(MineralGrowingSpot growSpot in mineralGrowSpots)
                {
                    if(growSpot.spotMineral != null)
                    {
                        GameObject.Destroy(growSpot.spotMineral.gameObject);
                    }
                }
                try
                {
                    string[] lines = System.IO.File.ReadAllLines("BepinEx/Plugins/Rooms/StorageCellar." + currentSavePool + ".sav");
                    for(int i=0; i < lines.Length; ++i)
                    {
                        string line = lines[i].Trim();
                        if (!line.Equals("null")) 
                        {
                            mineralGrowSpots[i].SpawnSpecificMineral(Ingredient.GetByName(line));
                        }
                    }
                }
                catch (FileNotFoundException) { LogInfo("No save file found."); }
                catch (Exception ex) { LogError("Could not read save file: " + ex.Message); }
            }
        }

        private void OnDayStart()
        {
            // Grow minerals
            if (!Managers.Tutorial.TutorialIsActive())
            {
                foreach(MineralGrowingSpot spot in mineralGrowSpots)
                {
                    if(spot.spotMineral == null && UnityEngine.Random.value <= spawnChancePerGrowingSpot)
                    {
                        spot.SpawnRandomMineral();
                    }
                }
            }
        }

        private void FetchData()
        {
            // Fetch grow spots from game Object
            mineralGrowSpots = new List<MineralGrowingSpot>();
            storageCellar = GameObject.Find("StorageCellar");
            foreach (Transform transform in storageCellar.transform)
            {
                if (transform.name.Equals("MineralGrowSpots"))
                {
                    foreach (Transform growSpot in transform.transform)
                    {
                        mineralGrowSpots.Add(new MineralGrowingSpot(growSpot.gameObject));
                    }
                }
            }

            // Fetch minerals
            minerals = new List<Ingredient>();
            foreach (Ingredient ingredient in Managers.Ingredient.ingredients)
            {
                if (ingredient.isTeleportationIngredient)
                {
                    minerals.Add(ingredient);
                }
            }
        }
    }

    public class MineralGrowingSpot
    {
        public GameObject gameObject;

        public SpotMineral spotMineral;

        public MineralGrowingSpot(GameObject gameObject)
        {
            this.gameObject = gameObject;
            spotMineral = null;
        }

        public void SpawnRandomMineral()
        {
            SpawnSpecificMineral(StorageCellarMod.minerals[UnityEngine.Random.Range(0, StorageCellarMod.minerals.Count)]);
        }

        public void SpawnSpecificMineral(Ingredient ingredient)
        {
            GameObject spotMineralObject = new GameObject();
            spotMineralObject.transform.parent = gameObject.transform;
            spotMineralObject.transform.localPosition = Vector3.zero;
            spotMineralObject.transform.localRotation = Quaternion.identity;
            spotMineral = spotMineralObject.AddComponent<SpotMineral>();
            spotMineral.mineralGrowingSpot = this;
            spotMineral.ingredient = ingredient;
            spotMineralObject.name = spotMineral.ingredient.name + " SpotMineral";
        }
    }

    public class SpotMineral : InteractiveItem, IHoverable, IPrimaryCursorEventsHandler, ISecondaryCursorEventsHandler
    {
        public MineralGrowingSpot mineralGrowingSpot;
        public SpriteRenderer spriteRenderer;
        public int experienceOnHarvest = 50;
        public Ingredient ingredient;
        public Vector2Int ingredientAmount = Vector2Int.one;
        public Vector2 floatingTextCursorSpawnOffset = Vector2.zero;
        public int physicalParticlesOnHarvest = 10;
        public float physicalParticlesSpawnRadius = 0.5f;
        public float physicalParticlesVelocity = 1.5f;

        private void Start()
        {
            GameObject sprite = new GameObject();
            sprite.transform.parent = transform;
            sprite.transform.localPosition = Vector3.zero;
            sprite.transform.localRotation = Quaternion.identity;
            sprite.transform.localScale = Vector3.one * StorageCellarMod.mineralScale;
            sprite.name = "Sprite";
            spriteRenderer = sprite.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = ingredient.smallIcon;
            GameObject collider = new GameObject();
            collider.transform.parent = transform;
            collider.transform.localPosition = Vector3.zero;
            collider.layer = Layers.Equipment;
            collider.AddComponent<PolygonCollider2D>();
            collider.name = "Collider";
            sortingGroup = mineralGrowingSpot.gameObject.GetComponent<SortingGroup>();
        }

        private void GatherIngredients()
        {
            Managers.Player.experience.AddExperience(experienceOnHarvest);
            int amount = UnityEngine.Random.Range(ingredientAmount.x, ingredientAmount.y + 1);
            Managers.Player.inventory.AddItem(ingredient, amount, true, true);
            string ingredientsAtlasName = Managers.TmpAtlas.settings.IngredientsAtlasName;
            Vector2 v = new Vector2(Managers.Cursor.cursor.transform.position.x, Managers.Cursor.cursor.transform.position.y) + floatingTextCursorSpawnOffset;
            CollectedFloatingText.SpawnNewText(Managers.Ingredient.spotPlantSettings.floatingTextPrefab, v, new CollectedFloatingText.FloatingTextContent(string.Format("+{0}\u2009<sprite=\"{1}\" name=\"{2}\">", amount, ingredientsAtlasName, IngredientsAtlasGenerator.GetAtlasSpriteName(ingredient)), CollectedFloatingText.FloatingTextContent.Type.Numbers, 0.1f), Managers.Game.Cam.transform, false, false);
            SpawnVisualEffect();
            mineralGrowingSpot.spotMineral = null;
            GameObject.Destroy(gameObject);
        }

        private void SpawnVisualEffect()
        {
            for (int i = 0; i < physicalParticlesOnHarvest; i++)
            {
                Vector2 insideUnitCircle = UnityEngine.Random.insideUnitCircle;
                Vector2 actual = physicalParticlesSpawnRadius * insideUnitCircle;
                Vector3 actual3d = new Vector3(actual.x, actual.y, 0);
                Vector3 v = Managers.Cursor.cursor.transform.position + actual3d;
                Vector3 v2 = insideUnitCircle * physicalParticlesVelocity;
                PhysicalParticle.Spawn(v, v2, ingredient.physicalParticleType, PhysicalParticle.Behaviour.PlayZone, ingredient.grindedSubstanceColor);
            }
        }

        public void OnPrimaryCursorClick()
        {
            GatherIngredients();
        }

        public void OnPrimaryCursorRelease(){}

        public void OnSecondaryCursorClick()
        {
            GatherIngredients();
        }

        public void OnSecondaryCursorRelease(){}

        public void SetHovered(bool hovered)
        {
            Managers.Outline.OutlineOne(spriteRenderer, hovered, sortingGroup.sortingLayerName, sortingGroup.sortingOrder, SpriteMaskInteraction.None);
        }
    }

    class LoadFromPoolPatch
    {
        static void Prefix(SavePool pool)
        {
            StorageCellarMod.currentSavePool = pool;
            StorageCellarMod.savePoolSpecified = true;
        }
    }

    class SaveToPoolPatch
    {
        static void Prefix(SavePool pool)
        {
            StorageCellarMod.currentSavePool = pool;
            StorageCellarMod.savePoolSpecified = true;
        }
    }
}
