using System.Collections.Generic;
using UnityEngine;

namespace ControlRoom
{
    public sealed class CctvManager : MonoBehaviour
    {
        public List<Camera> Cameras { get; } = new List<Camera>();
        public List<RenderTexture> Textures { get; } = new List<RenderTexture>();
        public List<string> Names { get; } = new List<string>();
        public List<string> Ids { get; } = new List<string>();
        public int SelectedIndex { get; private set; } = -1;
        public RenderTexture SelectedTexture => SelectedIndex >= 0 && SelectedIndex < Textures.Count ? Textures[SelectedIndex] : null;

        public void Initialize(IReadOnlyDictionary<string, Vector3> locations)
        {
            Dispose();
            string[] rooms = { "MAIN_HALL", "LABORATORY", "STORAGE", "SERVER_ROOM" };
            string[] names = { "메인 복도", "연구실", "창고", "서버실" };
            for (int i = 0; i < rooms.Length; i++)
            {
                string id = "CAM-0" + (i + 1);
                var rig = new GameObject(id + " " + rooms[i]);
                rig.transform.SetParent(transform, false);
                Vector3 center = locations[rooms[i]];
                rig.transform.position = center + new Vector3(.5f, 15.5f, -8.5f);
                rig.transform.LookAt(center + Vector3.up * .5f);
                var camera = rig.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 7.35f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 65f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.015f, .025f, .035f);
                camera.depth = -10 + i;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var texture = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32)
                {
                    name = id + " Live feed", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                texture.Create();
                camera.targetTexture = texture;
                Cameras.Add(camera);
                Textures.Add(texture);
                Names.Add(names[i]);
                Ids.Add(id);
            }
        }

        public void Select(int index)
        {
            SelectedIndex = index >= 0 && index < Cameras.Count ? index : -1;
        }

        public void Dispose()
        {
            foreach (Camera camera in Cameras)
            {
                if (camera == null) continue;
                camera.targetTexture = null;
                Destroy(camera.gameObject);
            }
            foreach (RenderTexture texture in Textures)
            {
                if (texture == null) continue;
                texture.Release();
                Destroy(texture);
            }
            Cameras.Clear(); Textures.Clear(); Names.Clear(); Ids.Clear();
            SelectedIndex = -1;
        }

        private void OnDestroy() => Dispose();
    }
}
