using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ControlRoom
{
    public sealed class MissionWorld : MonoBehaviour
    {
        public const int ObstacleLayer = 8;
        public const int FieldAgentVisualLayer = 9;
        public const int SurveillanceLabelLayer = 10;
        public Dictionary<string, Vector3> Locations { get; } = new Dictionary<string, Vector3>();
        public Dictionary<string, WorldObject> Objects { get; } = new Dictionary<string, WorldObject>();
        public FieldAgent Agent { get; private set; }
        public GuardController Guard { get; private set; }
        public CctvManager Cctv { get; private set; }
        public bool NavigationReady { get; private set; }
        private readonly List<NavMeshBuildSource> navigationSources = new List<NavMeshBuildSource>();
        private readonly List<Material> materials = new List<Material>();
        private NavMeshData navigationData;
        private NavMeshDataInstance navigationInstance;
        private Material wall, metal, black, cyan, amber, white, agentBlue, guardOrange;
        private bool built;

        public void Build()
        {
            if (built) return;
            built = true;
            Locations.Add("ENTRANCE", new Vector3(0, 0, -12));
            Locations.Add("EXIT", Locations["ENTRANCE"]);
            Locations.Add("MAIN_HALL", Vector3.zero);
            Locations.Add("LABORATORY", new Vector3(14, 0, 0));
            Locations.Add("STORAGE", new Vector3(0, 0, 14));
            Locations.Add("SERVER_ROOM", new Vector3(14, 0, 14));
            wall = Mat("Concrete", new Color(.25f, .3f, .34f));
            metal = Mat("Brushed steel", new Color(.32f, .41f, .46f));
            black = Mat("Equipment graphite", new Color(.06f, .085f, .11f));
            cyan = Mat("Operator signal", new Color(.18f, .92f, .9f), true);
            amber = Mat("Security signal", new Color(1f, .56f, .16f), true);
            white = Mat("Laboratory ceramic", new Color(.65f, .71f, .72f));
            agentBlue = Mat("Field uniform", new Color(.12f, .6f, .83f));
            guardOrange = Mat("Security uniform", new Color(.87f, .34f, .13f));

            BuildFacility();
            BuildObjects();
            BuildNavigation();
            CreateLight();
            var agentRoot = new GameObject("FIELD_AGENT");
            agentRoot.transform.SetParent(transform, false);
            agentRoot.transform.position = Locations["ENTRANCE"];
            Agent = agentRoot.AddComponent<FieldAgent>();
            Agent.Visual = LowPolyVisual.Character(agentRoot.transform, false, agentBlue, black, cyan);
            foreach (Transform visualPart in Agent.Visual.GetComponentsInChildren<Transform>(true))
                visualPart.gameObject.layer = FieldAgentVisualLayer;
            Agent.Initialize(this);
            var guardRoot = new GameObject("GUARD");
            guardRoot.transform.SetParent(transform, false);
            guardRoot.transform.position = new Vector3(14, 0, 12.5f);
            Guard = guardRoot.AddComponent<GuardController>();
            Guard.Visual = LowPolyVisual.Character(guardRoot.transform, true, guardOrange, black, amber);
            Guard.Agent = Agent;
            Guard.Initialize(this);
            Cctv = new GameObject("CCTV network").AddComponent<CctvManager>();
            Cctv.transform.SetParent(transform, false);
            Cctv.Initialize(Locations);
        }

        private Material Mat(string name, Color color, bool glow = false)
        {
            Material material = LowPolyVisual.Material(name, color, glow);
            materials.Add(material);
            return material;
        }

        private GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool navigation = false)
        {
            GameObject item = LowPolyVisual.Cube(name, transform, position, size, material, navigation,
                name.StartsWith("Floor") ? 0 : ObstacleLayer);
            if (navigation)
            {
                navigationSources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = item.transform.localToWorldMatrix,
                    size = Vector3.one,
                    area = 0
                });
            }
            return item;
        }

        private void Floor(string room, Vector3 center, Vector2 size, Color color)
        {
            Box("Floor " + room, center + new Vector3(0, -.15f, 0), new Vector3(size.x, .3f, size.y), Mat(room + " floor", color), true);
            // The embedded stripe makes exits and corridor directions visible at CCTV resolution.
            Box(room + " floor stripe", center + new Vector3(0, .012f, 0), new Vector3(.12f, .014f, size.y - .8f), cyan);
            Label(room, center + new Vector3(-size.x * .32f, .025f, -size.y * .32f));
        }

        private void Wall(string name, Vector3 center, Vector3 size) => Box("Wall " + name, center, size, wall, true);
        private void VerticalWall(string name, float x, float z, float length) => Wall(name, new Vector3(x, 1.7f, z), new Vector3(.3f, 3.4f, length));
        private void HorizontalWall(string name, float x, float z, float length) => Wall(name, new Vector3(x, 1.7f, z), new Vector3(length, 3.4f, .3f));

        private void BuildFacility()
        {
            Floor("ENTRANCE", Locations["ENTRANCE"], new Vector2(9, 8), new Color(.13f, .2f, .24f));
            Floor("MAIN HALL", Locations["MAIN_HALL"], new Vector2(10, 10), new Color(.17f, .24f, .27f));
            Floor("LABORATORY", Locations["LABORATORY"], new Vector2(10, 10), new Color(.2f, .29f, .32f));
            Floor("STORAGE", Locations["STORAGE"], new Vector2(10, 10), new Color(.25f, .23f, .19f));
            Floor("SERVER ROOM", Locations["SERVER_ROOM"], new Vector2(10, 10), new Color(.14f, .2f, .28f));
            Floor("ENTRY LINK", new Vector3(0, 0, -6.5f), new Vector2(4, 3.5f), new Color(.16f, .22f, .24f));
            Floor("LAB LINK", new Vector3(7, 0, 0), new Vector2(4.4f, 4), new Color(.16f, .22f, .24f));
            Floor("STORAGE LINK", new Vector3(0, 0, 7), new Vector2(4, 4.4f), new Color(.16f, .22f, .24f));
            Floor("SERVER LINK", new Vector3(7, 0, 14), new Vector2(4.4f, 4), new Color(.16f, .22f, .24f));
            HorizontalWall("entrance south", 0, -16, 9);
            VerticalWall("entrance west", -4.5f, -12, 8);
            VerticalWall("entrance east", 4.5f, -12, 8);
            HorizontalWall("entrance north A", -3.25f, -8, 2.5f);
            HorizontalWall("entrance north B", 3.25f, -8, 2.5f);
            VerticalWall("entry link west", -2, -6.5f, 3);
            VerticalWall("entry link east", 2, -6.5f, 3);

            VerticalWall("hall west", -5, 0, 10);
            VerticalWall("hall east A", 5, -3.5f, 3);
            VerticalWall("hall east B", 5, 3.5f, 3);
            HorizontalWall("hall north A", -3.5f, 5, 3);
            HorizontalWall("hall north B", 3.5f, 5, 3);
            HorizontalWall("hall south A", -3.5f, -5, 3);
            HorizontalWall("hall south B", 3.5f, -5, 3);
            HorizontalWall("lab link south", 7, -2, 4);
            HorizontalWall("lab link north", 7, 2, 4);
            VerticalWall("storage link west", -2, 7, 4);
            VerticalWall("storage link east", 2, 7, 4);

            HorizontalWall("laboratory north", 14, 5, 10);
            HorizontalWall("laboratory south", 14, -5, 10);
            VerticalWall("laboratory east", 19, 0, 10);
            VerticalWall("laboratory west A", 9, -3.5f, 3);
            VerticalWall("laboratory west B", 9, 3.5f, 3);

            VerticalWall("storage west", -5, 14, 10);
            HorizontalWall("storage north", 0, 19, 10);
            HorizontalWall("storage south A", -3.5f, 9, 3);
            HorizontalWall("storage south B", 3.5f, 9, 3);
            VerticalWall("storage east A", 5, 10.5f, 3);
            VerticalWall("storage east B", 5, 17.5f, 3);
            HorizontalWall("server link north", 7, 16, 4);
            HorizontalWall("server link south", 7, 12, 4);
            HorizontalWall("server north", 14, 19, 10);
            HorizontalWall("server south", 14, 9, 10);
            VerticalWall("server east", 19, 14, 10);
            VerticalWall("server west A", 9, 10.5f, 3);
            VerticalWall("server west B", 9, 17.5f, 3);
            // Hazard band is decorative; no invisible gameplay barriers are introduced.
            Box("Storage hazard band", new Vector3(0, .02f, 9.8f), new Vector3(3.3f, .018f, .2f), amber);
            Box("Exit beacon", new Vector3(0, .05f, -14.3f), new Vector3(3.1f, .05f, .8f), cyan);
        }

        private WorldObject Register(string id, string label, string room, string kind, GameObject item, Vector3 standPoint)
        {
            var obj = new WorldObject(new ObjectRecord(id, label, room, kind), item.transform, standPoint);
            Objects.Add(id, obj);
            return obj;
        }

        private WorldObject Door(string id, string label, string room, Vector3 position, bool alongX)
        {
            GameObject leaf = Box(id, position + Vector3.up * 1.35f, alongX ? new Vector3(.22f, 2.7f, 3.4f) : new Vector3(3.4f, 2.7f, .22f), metal);
            // Door colliders participate in line of sight. The movement authority separately checks open state;
            // keeping them out of the baked mesh allows a runtime opening to be navigable immediately.
            leaf.AddComponent<BoxCollider>();
            LowPolyVisual.Cube("Door signal", leaf.transform, new Vector3(0, .35f, 0), new Vector3(.92f, .12f, 1.02f), amber);
            return Register(id, label, room, "DOOR", leaf, alongX ? position + Vector3.left * 1.5f : position + Vector3.back * 1.5f);
        }

        private void BuildObjects()
        {
            Door("LAB_DOOR", "연구실 문", "MAIN_HALL", new Vector3(8.6f, 0, 0), true);
            Door("STORAGE_DOOR", "창고 문", "MAIN_HALL", new Vector3(0, 0, 8.6f), false);
            WorldObject serverDoor = Door("SERVER_DOOR", "서버실 문", "STORAGE", new Vector3(8.6f, 0, 14), true);
            serverDoor.SetOpen();

            GameObject desk = Box("Laboratory desk", new Vector3(14.5f, .82f, -2.8f), new Vector3(3.2f, 1.6f, 1.35f), white, true);
            Register("LAB_DESK", "연구실 책상", "LABORATORY", "DESK", desk, new Vector3(14.5f, 0, -1.5f));
            LowPolyVisual.Cube("Desk display", desk.transform, new Vector3(-.2f, .61f, 0), new Vector3(.25f, .25f, .25f), black);
            LowPolyVisual.Cube("Desk display screen", desk.transform, new Vector3(-.2f, .63f, -.14f), new Vector3(.2f, .2f, .015f), cyan);
            GameObject cabinet = Box("Secure cabinet", new Vector3(16.5f, 1.18f, 3.35f), new Vector3(2.1f, 2.35f, 1.1f), black, true);
            cabinet.GetComponent<Renderer>().enabled = false;
            LowPolyVisual.Cube("Cabinet back", cabinet.transform, new Vector3(0, 0, .47f), new Vector3(1, 1, .08f), black);
            LowPolyVisual.Cube("Cabinet left frame", cabinet.transform, new Vector3(-.47f, 0, 0), new Vector3(.08f, 1, 1), metal);
            LowPolyVisual.Cube("Cabinet right frame", cabinet.transform, new Vector3(.47f, 0, 0), new Vector3(.08f, 1, 1), metal);
            LowPolyVisual.Cube("Cabinet top", cabinet.transform, new Vector3(0, .47f, 0), new Vector3(1, .06f, 1), metal);
            LowPolyVisual.Cube("Cabinet shelf", cabinet.transform, new Vector3(0, -.09f, 0), new Vector3(1, .035f, 1), metal);
            WorldObject labCabinet = Register("LAB_CABINET", "연구실 보안 캐비닛", "LABORATORY", "CABINET", cabinet, new Vector3(16.5f, 0, 1.75f));
            labCabinet.RequiredCode = "731";
            Transform cabinetDoor = LowPolyVisual.Cube("Cabinet hinged leaf", cabinet.transform, new Vector3(0, 0, -.52f), new Vector3(.95f, .93f, .055f), metal).transform;
            labCabinet.MovingPart = cabinetDoor;
            LowPolyVisual.Cube("Cabinet keypad", cabinetDoor, new Vector3(.23f, .07f, -.6f), new Vector3(.18f, .16f, .02f), cyan);
            GameObject usb = Box("Data USB", new Vector3(16.5f, 1.15f, 3.25f), new Vector3(.4f, .14f, .23f), cyan);
            LowPolyVisual.Cube("USB connector", usb.transform, new Vector3(.57f, 0, 0), new Vector3(.25f, .85f, .8f), metal);
            WorldObject usbObject = Register("USB", "기밀 데이터 USB", "LABORATORY", "USB", usb, labCabinet.StandPoint);
            usbObject.Record.IsKnown = false;
            usb.SetActive(false);
            labCabinet.Opened = RevealUsb;

            GameObject card = Box("Personnel keycard", new Vector3(14.8f, 1.68f, -2.6f), new Vector3(.32f, .035f, .48f), amber);
            Register("KEYCARD", "연구실 키카드", "LABORATORY", "KEYCARD", card, new Vector3(14.5f, 0, -1.5f));
            GameObject terminal = Box("Evidence archive terminal", new Vector3(17.5f, .72f, 11.3f), new Vector3(1.5f, 1.4f, 1.2f), metal, true);
            Register("TERMINAL", "증거 보관 터미널", "SERVER_ROOM", "TERMINAL", terminal, new Vector3(16.3f, 0, 11.3f));
            LowPolyVisual.Cube("Terminal screen", terminal.transform, new Vector3(-.53f, .57f, 0), new Vector3(.06f, .6f, .75f), amber);
            Label("PURGE / DANGER", new Vector3(16, .025f, 10));
            for (int i = 0; i < 3; i++)
            {
                GameObject rack = Box("Server rack " + i, new Vector3(11 + i * 2.35f, 1.4f, 17.6f), new Vector3(1.55f, 2.8f, 1.1f), black, true);
                for (int light = 0; light < 5; light++) LowPolyVisual.Cube("Status LED", rack.transform, new Vector3(.2f, -.35f + light * .15f, -.52f), new Vector3(.45f, .04f, .03f), cyan);
            }
            for (int i = 0; i < 4; i++) Box("Storage crate " + i, new Vector3(-3.4f, .6f, 11.5f + i * 1.65f), new Vector3(1.8f, 1.2f, 1.35f), metal, true);
            Box("Hall information kiosk", new Vector3(-3.65f, .65f, 1.8f), new Vector3(1.35f, 1.3f, 1.8f), black, true);
            Box("Hall display", new Vector3(-3.65f, 1.6f, 1.8f), new Vector3(1.45f, .5f, .12f), cyan);
        }

        public void RevealUsb()
        {
            if (!Objects.TryGetValue("USB", out WorldObject usb) || usb.IsCollected) return;
            usb.Record.IsKnown = true;
            usb.Transform.gameObject.SetActive(Objects["LAB_CABINET"].IsOpen);
        }

        private void BuildNavigation()
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .32f;
            settings.agentHeight = 1.9f;
            settings.agentClimb = .3f;
            settings.agentSlope = 45;
            settings.overrideVoxelSize = true;
            settings.voxelSize = .12f;
            navigationData = NavMeshBuilder.BuildNavMeshData(settings, navigationSources,
                new Bounds(new Vector3(6, 2, 2), new Vector3(40, 12, 44)), Vector3.zero, Quaternion.identity);
            if (navigationData == null)
            {
                Debug.LogError("Mission_01: runtime NavMesh build failed.");
                return;
            }
            navigationData.name = "Mission_01 runtime navigation";
            navigationInstance = NavMesh.AddNavMeshData(navigationData);
            NavigationReady = navigationInstance.valid;
        }

        private void CreateLight()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.43f, .52f, .6f);
            RenderSettings.fog = false;
            var directional = new GameObject("Facility overhead light").AddComponent<Light>();
            directional.transform.SetParent(transform, false);
            directional.transform.rotation = Quaternion.Euler(58, -35, 0);
            directional.type = LightType.Directional;
            directional.intensity = 1.35f;
            directional.color = new Color(.76f, .87f, 1f);
            directional.shadows = LightShadows.Soft;
            foreach (KeyValuePair<string, Vector3> room in Locations)
            {
                if (room.Key == "EXIT") continue;
                var lamp = new GameObject(room.Key + " ceiling lamp").AddComponent<Light>();
                lamp.transform.SetParent(transform, false);
                lamp.transform.position = room.Value + Vector3.up * 4f;
                lamp.type = LightType.Point;
                lamp.range = 11f;
                lamp.intensity = 3f;
                lamp.color = room.Key == "STORAGE" ? new Color(1f, .76f, .45f) : new Color(.45f, .78f, 1f);
                Box(room.Key + " luminous bar", room.Value + new Vector3(0, 3.5f, 0), new Vector3(2, .08f, .14f), cyan);
            }
        }

        private void Label(string text, Vector3 position)
        {
            var item = new GameObject(text + " floor sign");
            item.layer = SurveillanceLabelLayer;
            item.transform.SetParent(transform, false);
            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(90, 0, 0);
            TextMesh label = item.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            item.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.text = text;
            label.characterSize = .2f;
            label.fontSize = 42;
            label.color = new Color(.55f, .76f, .78f);
        }

        public WorldContext GetContext()
        {
            var context = new WorldContext { locationId = Agent != null ? Agent.CurrentLocationId : "ENTRANCE" };
            foreach (WorldObject obj in Objects.Values) context.objects.Add(obj.Record);
            return context;
        }

        public string LocationAt(Vector3 point)
        {
            if (point.z < -7.8f) return "ENTRANCE";
            if (point.z > 8.5f) return point.x > 8.5f ? "SERVER_ROOM" : "STORAGE";
            if (point.x > 8.5f) return "LABORATORY";
            return "MAIN_HALL";
        }

        /// <summary>Remove the old navigation before a synchronous scene restart builds the replacement world.</summary>
        public void ReleaseNavigation()
        {
            Agent?.Cancel();
            if (Agent != null && Agent.Navigation != null) Agent.Navigation.enabled = false;
            if (Guard != null && Guard.Navigation != null) Guard.Navigation.enabled = false;
            if (navigationInstance.valid) navigationInstance.Remove();
            NavigationReady = false;
        }

        private void OnDestroy()
        {
            ReleaseNavigation();
            if (navigationData != null) Destroy(navigationData);
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
