using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.SceneManagement;

namespace Reachoreo.EditorTools
{
    /// <summary>
    /// Builds the Stanford places as real-scale blockouts (simple shapes, our own; no photos copied):
    /// Hoover Tower plaza, Memorial Church on the Main Quad, McMurtry Building. Every size is an estimate
    /// from reference photos and is tagged with EstimatedDimension, to be corrected with your own photos,
    /// measurements or a phone scan. Menu: Reachoreo > Build places.
    /// Each scene gets the shared PlaceRig (you, coach, Reachys, mirror, menus) with a free-standing mirror.
    /// </summary>
    public static class PlaceBuilder
    {
        const string SceneDir = "Assets/Reachoreo/Scenes/";
        const string MatDir = "Assets/Reachoreo/Art/Materials/Places/";

        [MenuItem("Reachoreo/Build places/All three")]
        public static void BuildAll() { BuildHoover(); BuildMemChu(); BuildMcMurtry(); AddToBuild(); }

        [MenuItem("Reachoreo/Build places/Hoover Tower")] public static void BuildHoover() => Build("Place_HooverTower", HooverTower, Quaternion.Euler(42, 25, 0));
        [MenuItem("Reachoreo/Build places/Memorial Church")] public static void BuildMemChu() => Build("Place_MemorialChurch", MemorialChurch, Quaternion.Euler(50, -20, 0));
        [MenuItem("Reachoreo/Build places/McMurtry")] public static void BuildMcMurtry() => Build("Place_McMurtry", McMurtry, Quaternion.Euler(38, 30, 0));

        public static void AddToBuild()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var n in new[] { "Place_DanceStudio", "Place_HooverTower", "Place_MemorialChurch", "Place_McMurtry" })
                list.Add(new EditorBuildSettingsScene(SceneDir + n + ".unity", true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ================= Common =================

        static Transform env;

        static void Build(string sceneName, Action build, Quaternion sun)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            env = new GameObject("Environment").transform;
            build();

            // Lighting: clear California sky, one sun.
            var sky = Mat("Sky_Clear", Color.white, 0f, "Skybox/Procedural");
            sky.SetFloat("_SunSize", 0.035f); sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.95f)); sky.SetColor("_GroundColor", new Color(0.55f, 0.52f, 0.48f)); sky.SetFloat("_Exposure", 1.25f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox; RenderSettings.ambientIntensity = 1f;
            var sunGo = new GameObject("Sun"); var l = sunGo.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.35f; l.color = new Color(1f, 0.96f, 0.88f); l.shadows = LightShadows.Soft;
            sunGo.transform.rotation = sun;
            RenderSettings.sun = l;

            // You, the coach, the Reachys, the menus: the shared PlaceRig, with a free-standing mirror.
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reachoreo/Places/PlaceRig.prefab");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            var mirror = rig.transform.Find("Mirror");
            mirror.localPosition = new Vector3(0, 1.15f, 3.0f);
            mirror.Find("Mirror_Surface (placeholder)").localScale = new Vector3(2.4f, 1.8f, 1f);
            var metal = Mat("Mirror_Frame", new Color(0.16f, 0.16f, 0.18f), 0.4f);
            var stand = new GameObject("Mirror_Stand").transform; stand.SetParent(env, false);
            Box("Post_L", new Vector3(0.06f, 2.2f, 0.06f), new Vector3(-1.25f, 1.1f, 3.03f), metal, stand);
            Box("Post_R", new Vector3(0.06f, 2.2f, 0.06f), new Vector3(1.25f, 1.1f, 3.03f), metal, stand);
            Box("Top", new Vector3(2.56f, 0.06f, 0.06f), new Vector3(0, 2.08f, 3.03f), metal, stand);
            Box("Bottom", new Vector3(2.56f, 0.06f, 0.06f), new Vector3(0, 0.22f, 3.03f), metal, stand);
            Box("Foot_L", new Vector3(0.08f, 0.05f, 0.7f), new Vector3(-1.25f, 0.025f, 3.03f), metal, stand);
            Box("Foot_R", new Vector3(0.08f, 0.05f, 0.7f), new Vector3(1.25f, 0.025f, 3.03f), metal, stand);
            Box("Backing", new Vector3(2.44f, 1.84f, 0.02f), new Vector3(0, 1.15f, 3.045f), metal, stand);
            var stone = Mat("Plinth_Stone", new Color(0.62f, 0.60f, 0.56f), 0.1f);
            Box("Reachy_Plinth_A", new Vector3(0.4f, 0.5f, 0.4f), new Vector3(-1.2f, 0.25f, 0.3f), stone, env);
            Box("Reachy_Plinth_B", new Vector3(0.4f, 0.5f, 0.4f), new Vector3(1.2f, 0.25f, 0.3f), stone, env);
            rig.transform.Find("Reachy_A_Spot").localPosition = new Vector3(-1.2f, 0.5f, 0.3f);
            rig.transform.Find("Reachy_B_Spot").localPosition = new Vector3(1.2f, 0.5f, 0.3f);
            var mw = rig.GetComponent<MirrorWorld>();
            if (mw != null) { mw.staticSources.Clear(); mw.staticSources.Add(env); mw.lights.Clear(); }

            // Something to stand on everywhere (the player rig has gravity).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Ground_Collider";
            ground.transform.localScale = new Vector3(30, 1, 30); ground.transform.position = new Vector3(0, -0.005f, 0);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<MeshRenderer>());

            EditorSceneManager.SaveScene(scene, SceneDir + sceneName + ".unity");
            Debug.Log("Reachoreo: built " + sceneName);
        }

        static Material Mat(string name, Color c, float smooth = 0.1f, string shader = "Universal Render Pipeline/Lit")
        {
            System.IO.Directory.CreateDirectory(MatDir);
            string p = MatDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, p); }
            if (shader.Contains("Lit")) { m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Finish(ProBuilderMesh pb, string name, Vector3 pos, Quaternion rot, Material m, Transform parent, string estimate)
        {
            var go = pb.gameObject; go.name = name;
            go.transform.SetParent(parent ?? env, false);
            go.transform.localPosition = pos; go.transform.localRotation = rot;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            pb.ToMesh(); pb.Refresh();
            go.AddComponent<MeshCollider>();
            if (estimate != null) go.AddComponent<EstimatedDimension>().estimate = "ESTIMATE: " + estimate;
            return go;
        }

        static GameObject Box(string name, Vector3 size, Vector3 pos, Material m, Transform parent = null, string estimate = null, Vector3? euler = null) =>
            Finish(ShapeGenerator.GenerateCube(PivotLocation.Center, size), name, pos, Quaternion.Euler(euler ?? Vector3.zero), m, parent, estimate);

        static GameObject Cyl(string name, float radius, float height, Vector3 pos, Material m, Transform parent = null, string estimate = null, int sides = 24) =>
            Finish(ShapeGenerator.GenerateCylinder(PivotLocation.Center, sides, radius, height, 0), name, pos, Quaternion.identity, m, parent, estimate);

        /// <summary>A gable or roof: a triangular prism, ridge along X.</summary>
        static GameObject Prism(string name, Vector3 size, Vector3 pos, Material m, Transform parent = null, string estimate = null, Vector3? euler = null) =>
            Finish(ShapeGenerator.GeneratePrism(PivotLocation.Center, size), name, pos, Quaternion.Euler(euler ?? Vector3.zero), m, parent, estimate);

        static GameObject Ball(string name, Vector3 scale, Vector3 pos, Material m, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); g.name = name;
            g.transform.SetParent(parent ?? env, false); g.transform.localPosition = pos; g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static Transform Group(string name) { var t = new GameObject(name).transform; t.SetParent(env, false); return t; }

        static void Tree(Transform parent, Vector3 at, float height, Material trunk, Material leaves, bool cypress = false)
        {
            var t = Group("Tree"); t.SetParent(parent, false); t.localPosition = at;
            Cyl("Trunk", 0.22f, height * 0.45f, new Vector3(0, height * 0.225f, 0), trunk, t, null, 8);
            if (cypress) Ball("Crown", new Vector3(height * 0.22f, height * 0.75f, height * 0.22f), new Vector3(0, height * 0.6f, 0), leaves, t);
            else Ball("Crown", new Vector3(height * 0.6f, height * 0.5f, height * 0.6f), new Vector3(0, height * 0.7f, 0), leaves, t);
        }

        // ================= Hoover Tower =================

        static void HooverTower()
        {
            var pave = Mat("Paving_Light", new Color(0.80f, 0.77f, 0.70f), 0.15f);
            var grass = Mat("Grass", new Color(0.32f, 0.47f, 0.24f), 0.05f);
            var sand = Mat("Sandstone_Tower", new Color(0.86f, 0.80f, 0.66f), 0.1f);
            var shade = Mat("Sandstone_Recess", new Color(0.64f, 0.58f, 0.46f), 0.05f);
            var tile = Mat("Red_Tile", new Color(0.70f, 0.33f, 0.22f), 0.2f);
            var water = Mat("Water", new Color(0.38f, 0.58f, 0.70f), 0.92f);
            var spray = Mat("Fountain_Spray", new Color(0.92f, 0.95f, 0.98f), 0.6f);
            var trunk = Mat("Tree_Trunk", new Color(0.33f, 0.24f, 0.17f), 0.05f);
            var leaves = Mat("Tree_Leaves", new Color(0.20f, 0.34f, 0.18f), 0.05f);
            var hedge = Mat("Hedge", new Color(0.24f, 0.40f, 0.20f), 0.05f);

            var ground = Group("Ground");
            Box("Plaza", new Vector3(28, 0.1f, 60), new Vector3(0, -0.05f, 18), pave, ground, "plaza paving: about 28 m wide in front of the tower");
            Box("Lawn_L", new Vector3(40, 0.08f, 60), new Vector3(-34, -0.04f, 18), grass, ground);
            Box("Lawn_R", new Vector3(40, 0.08f, 60), new Vector3(34, -0.04f, 18), grass, ground);
            Box("Hedge_L", new Vector3(1.2f, 0.9f, 24), new Vector3(-14.6f, 0.45f, 12), hedge, ground);
            Box("Hedge_R", new Vector3(1.2f, 0.9f, 24), new Vector3(14.6f, 0.45f, 12), hedge, ground);

            var f = Group("Fountain"); f.localPosition = new Vector3(0, 0, 14);
            Cyl("Basin_Rim", 5.0f, 0.5f, new Vector3(0, 0.25f, 0), sand, f, "fountain basin about 10 m across, rim 0.5 m");
            Cyl("Water", 4.7f, 0.06f, new Vector3(0, 0.44f, 0), water, f);
            Cyl("Pedestal", 0.8f, 0.9f, new Vector3(0, 0.6f, 0), sand, f);
            Cyl("Jet_Main", 0.16f, 3.2f, new Vector3(0, 2.6f, 0), spray, f, null, 10);
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; Cyl("Jet_" + i, 0.07f, 1.4f, new Vector3(Mathf.Cos(a) * 2.2f, 1.1f, Mathf.Sin(a) * 2.2f), spray, f, null, 8); }

            var steps = Group("Steps");
            for (int i = 0; i < 4; i++) Box("Step_" + i, new Vector3(16, 0.16f, 0.9f), new Vector3(0, 0.08f + 0.16f * i, 24 + 0.9f * i), pave, steps, i == 0 ? "four steps up to the tower terrace" : null);
            Box("Terrace", new Vector3(30, 0.64f, 14), new Vector3(0, 0.32f, 34), pave, steps);

            // The tower: about 87 m tall. A square sandstone shaft with recessed vertical bays, an open belfry,
            // a cornice and a red tile dome with a lantern.
            var tw = Group("Hoover_Tower"); tw.localPosition = new Vector3(0, 0.64f, 38);
            Box("Base", new Vector3(14, 6, 14), new Vector3(0, 3, 0), sand, tw, "tower about 87 m tall; shaft about 11 m square (estimates)");
            Box("Shaft", new Vector3(11, 58, 11), new Vector3(0, 35, 0), sand, tw);
            for (int face = 0; face < 4; face++)
            {
                var rot = Quaternion.Euler(0, face * 90, 0);
                for (int k = -1; k <= 1; k++)
                {
                    var p = rot * new Vector3(k * 3.0f, 0, -5.52f);
                    Box($"Bay_{face}_{k}", new Vector3(1.3f, 46, 0.08f), new Vector3(p.x, 34, p.z), shade, tw, null, new Vector3(0, face * 90, 0));
                }
            }
            Box("Belfry", new Vector3(11.6f, 9, 11.6f), new Vector3(0, 68.5f, 0), sand, tw);
            for (int face = 0; face < 4; face++)
                for (int k = -1; k <= 1; k++)
                {
                    var p = Quaternion.Euler(0, face * 90, 0) * new Vector3(k * 3.3f, 0, -5.84f);
                    Box($"Opening_{face}_{k}", new Vector3(1.8f, 5.5f, 0.1f), new Vector3(p.x, 68, p.z), shade, tw, null, new Vector3(0, face * 90, 0));
                }
            Box("Cornice", new Vector3(12.6f, 1.0f, 12.6f), new Vector3(0, 73.5f, 0), sand, tw);
            Cyl("Drum", 5.0f, 2.0f, new Vector3(0, 75, 0), sand, tw, null, 32);
            Ball("Dome", new Vector3(10, 7, 10), new Vector3(0, 76, 0), tile, tw);
            Cyl("Lantern", 1.0f, 3.0f, new Vector3(0, 80.5f, 0), sand, tw, null, 12);
            Ball("Lantern_Cap", new Vector3(2.2f, 1.6f, 2.2f), new Vector3(0, 82.2f, 0), tile, tw);

            // Low sandstone wings with red tile roofs on either side.
            foreach (var sx in new[] { -1, 1 })
            {
                Box(sx < 0 ? "Wing_L" : "Wing_R", new Vector3(22, 10, 14), new Vector3(sx * 20, 5.6f, 40), sand, env, "Hoover wings about 10 m tall");
                Prism(sx < 0 ? "Roof_L" : "Roof_R", new Vector3(22.6f, 3, 14.6f), new Vector3(sx * 20, 12.1f, 40), tile, env);
            }

            var trees = Group("Trees");
            foreach (var p in new[] { new Vector3(-12, 0, 2), new Vector3(12, 0, 2), new Vector3(-18, 0, 22), new Vector3(18, 0, 22), new Vector3(-24, 0, 6), new Vector3(24, 0, 8), new Vector3(-9, 0, -10), new Vector3(10, 0, -12) })
                Tree(trees, p, 9f, trunk, leaves);
        }

        // ================= Memorial Church =================

        static void MemorialChurch()
        {
            var paver = Mat("Quad_Pavers", new Color(0.74f, 0.62f, 0.50f), 0.1f);
            var sand = Mat("Sandstone_Quad", new Color(0.80f, 0.68f, 0.50f), 0.1f);
            var shade = Mat("Arcade_Shadow", new Color(0.30f, 0.25f, 0.20f), 0.05f);
            var tile = Mat("Red_Tile", new Color(0.70f, 0.33f, 0.22f), 0.2f);
            var mosaic = Mat("Mosaic_Gold", new Color(0.80f, 0.67f, 0.38f), 0.45f);
            var glass = Mat("Stained_Glass", new Color(0.20f, 0.30f, 0.45f), 0.7f);
            var trunk = Mat("Tree_Trunk", new Color(0.33f, 0.24f, 0.17f), 0.05f);
            var leaves = Mat("Tree_Leaves", new Color(0.20f, 0.34f, 0.18f), 0.05f);

            Box("Quad_Paving", new Vector3(100, 0.1f, 80), new Vector3(0, -0.05f, 10), paver, env, "Main Quad paving; you stand about 30 m in front of the church");

            // The church: facade about 22 m wide; mosaic gable up to about 26 m (estimates).
            var ch = Group("Memorial_Church"); ch.localPosition = new Vector3(0, 0, 32);
            Box("Facade", new Vector3(22, 15, 4), new Vector3(0, 7.5f, 0), sand, ch, "church facade about 22 m wide, 15 m to the gable base");
            Prism("Gable", new Vector3(22, 11, 4), new Vector3(0, 20.5f, 0), sand, ch, null, new Vector3(0, 0, 0));
            Prism("Gable_Mosaic", new Vector3(19.5f, 9.6f, 0.2f), new Vector3(0, 20.2f, -2.05f), mosaic, ch);
            Box("Mosaic_Band", new Vector3(19.5f, 4.6f, 0.2f), new Vector3(0, 12.3f, -2.05f), mosaic, ch);
            Cyl("Rose_Window", 2.6f, 0.25f, new Vector3(0, 12.6f, -2.1f), glass, ch, null, 32).transform.localRotation = Quaternion.Euler(90, 0, 0);
            for (int k = -3; k <= 3; k++) if (k != 0) Box("Window_" + k, new Vector3(0.9f, 2.8f, 0.2f), new Vector3(k * 1.6f + Mathf.Sign(k) * 1.8f, 12.0f, -2.1f), glass, ch);
            Box("Lower_Mosaic", new Vector3(19.5f, 2.2f, 0.2f), new Vector3(0, 8.1f, -2.05f), mosaic, ch);
            for (int k = -1; k <= 1; k++) Box("Entrance_" + (k + 1), new Vector3(3.6f, 5.6f, 0.4f), new Vector3(k * 5.2f, 2.8f, -1.9f), shade, ch, k == 0 ? "three entrance arches" : null);
            Box("Cross_V", new Vector3(0.5f, 3.4f, 0.5f), new Vector3(0, 27.6f, 0), sand, ch);
            Box("Cross_H", new Vector3(1.8f, 0.45f, 0.5f), new Vector3(0, 28.3f, 0), sand, ch);
            Box("Nave", new Vector3(18, 18, 30), new Vector3(0, 9, 17), sand, ch);
            Prism("Nave_Roof", new Vector3(18.6f, 7, 30), new Vector3(0, 21.5f, 17), tile, ch, null, new Vector3(0, 90, 0));

            // Arcades along the quad on both sides of the church, with red tile roofs.
            foreach (var sx in new[] { -1, 1 })
            {
                var arc = Group(sx < 0 ? "Arcade_L" : "Arcade_R");
                float x0 = sx * 11f, len = 36f, cx = sx * (11f + len / 2f);
                Box("Back_Wall", new Vector3(len, 6.5f, 0.6f), new Vector3(cx, 3.25f, 35.2f), sand, arc, sx < 0 ? "arcades about 6.5 m tall, 4 m bays" : null);
                Box("Shadow", new Vector3(len, 5f, 0.1f), new Vector3(cx, 2.5f, 34.85f), shade, arc);
                for (int i = 0; i <= 9; i++) Box("Pillar_" + i, new Vector3(0.9f, 5.2f, 0.9f), new Vector3(x0 + sx * i * 4f, 2.6f, 30.8f), sand, arc);
                Box("Beam", new Vector3(len, 1.3f, 1.2f), new Vector3(cx, 5.85f, 30.8f), sand, arc);
                Box("Ceiling", new Vector3(len, 0.3f, 4.8f), new Vector3(cx, 6.35f, 33.0f), sand, arc);
                Prism("Roof", new Vector3(len, 1.8f, 5.6f), new Vector3(cx, 7.4f, 33.0f), tile, arc);
            }

            var trees = Group("Trees");
            foreach (var p in new[] { new Vector3(-36, 0, 44), new Vector3(-28, 0, 46), new Vector3(30, 0, 45), new Vector3(38, 0, 43), new Vector3(-16, 0, 52), new Vector3(17, 0, 52) })
                Tree(trees, p, 12f, trunk, leaves);
        }

        // ================= McMurtry Building =================

        static void McMurtry()
        {
            var concrete = Mat("Concrete_Plaza", new Color(0.68f, 0.68f, 0.66f), 0.15f);
            var wood = Mat("Wood_Cladding", new Color(0.66f, 0.48f, 0.33f), 0.25f);
            var deck = Mat("Wood_Deck", new Color(0.56f, 0.39f, 0.27f), 0.2f);
            var cork = Mat("Cork_Wall", new Color(0.66f, 0.52f, 0.38f), 0.05f);
            var glass = Mat("Glass_Dark", new Color(0.18f, 0.22f, 0.26f), 0.85f);
            var panel = Mat("Wall_Panel", new Color(0.86f, 0.84f, 0.78f), 0.1f);
            var gravel = Mat("Gravel", new Color(0.50f, 0.50f, 0.50f), 0.05f);
            var trunk = Mat("Tree_Trunk", new Color(0.33f, 0.24f, 0.17f), 0.05f);
            var leaves = Mat("Tree_Leaves_Light", new Color(0.36f, 0.50f, 0.22f), 0.05f);
            var grass = Mat("Grass", new Color(0.32f, 0.47f, 0.24f), 0.05f);

            Box("Plaza", new Vector3(44, 0.1f, 40), new Vector3(0, -0.05f, 6), concrete, env, "concrete plaza under the building");
            Box("Lawn_Back", new Vector3(30, 0.08f, 14), new Vector3(18, -0.04f, 30), grass, env);
            // Wood deck with the round inset, and wooden bleacher steps (left).
            var d = Group("Deck");
            Box("Deck", new Vector3(14, 0.12f, 12), new Vector3(-10, 0.06f, 4), deck, d, "wood deck about 14 x 12 m", new Vector3(0, 18, 0));
            Cyl("Deck_Circle", 2.0f, 0.04f, new Vector3(-11, 0.14f, 1.5f), Mat("Inset_Grey", new Color(0.55f, 0.58f, 0.60f), 0.4f), d, null, 32);
            for (int i = 0; i < 3; i++) Box("Bleacher_" + i, new Vector3(9, 0.45f, 1.1f), new Vector3(-16, 0.22f + 0.45f * i, 9 + 1.1f * i), deck, d, i == 0 ? "three wooden bleacher steps, 0.45 m each" : null, new Vector3(0, 18, 0));

            // The building behind: glass entrance, light wall panels, a door.
            var b = Group("Building");
            Box("Glass_Front", new Vector3(16, 7, 0.3f), new Vector3(-6, 3.5f, 16), glass, b, "glass entrance wall about 7 m tall");
            Box("Wall", new Vector3(12, 7, 0.4f), new Vector3(8, 3.5f, 17), panel, b);
            Box("Doors", new Vector3(3.2f, 2.8f, 0.1f), new Vector3(-3, 1.4f, 15.8f), Mat("Door_Frame", new Color(0.12f, 0.12f, 0.12f), 0.3f), b);
            Box("Upper_Glass", new Vector3(30, 5, 12), new Vector3(0, 12.5f, 22), glass, b);

            // The wooden canopy overhead, folded in facets, and the slanted beam that comes down to the ground.
            var c = Group("Canopy");
            Box("Soffit_Main", new Vector3(30, 0.8f, 14), new Vector3(0, 8.0f, 9), wood, c, "wooden canopy about 8 m overhead (estimate)", new Vector3(-4, 0, 0));
            Box("Soffit_Facet_L", new Vector3(12, 0.8f, 10), new Vector3(-14, 7.0f, 6), wood, c, null, new Vector3(-6, 20, 8));
            Box("Soffit_Facet_R", new Vector3(10, 0.8f, 9), new Vector3(13, 7.4f, 4), wood, c, null, new Vector3(-8, -15, -6));
            Box("Canopy_Edge", new Vector3(30, 1.6f, 0.8f), new Vector3(0, 7.6f, 2.0f), wood, c);
            Box("Slanted_Beam", new Vector3(18, 2.2f, 5.0f), new Vector3(7.5f, 3.6f, 11), cork, c, "slanted wall-beam from the canopy to the ground, about 30 degrees", new Vector3(0, 0, -27));
            Box("Beam_Block", new Vector3(3.5f, 4.5f, 4.5f), new Vector3(-1.2f, 5.2f, 11), cork, c);
            Box("Gravel_Bed", new Vector3(10, 0.15f, 3.5f), new Vector3(6, 0.07f, 9.2f), gravel, c);
            Box("Rail", new Vector3(8, 0.05f, 0.05f), new Vector3(4.5f, 1.0f, 7.5f), Mat("Steel", new Color(0.7f, 0.7f, 0.72f), 0.6f), c);

            var trees = Group("Trees");
            foreach (var p in new[] { new Vector3(14, 0, 26), new Vector3(18, 0, 22), new Vector3(22, 0, 28), new Vector3(26, 0, 24) })
                Tree(trees, p, 9f, trunk, leaves);
            foreach (var p in new[] { new Vector3(24, 0, 34), new Vector3(28, 0, 33) })
                Tree(trees, p, 14f, trunk, Mat("Cypress", new Color(0.18f, 0.30f, 0.16f), 0.05f), true);
        }
    }
}
