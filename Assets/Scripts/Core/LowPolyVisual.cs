using UnityEngine;

namespace ControlRoom
{
    /// <summary>Readable procedural facility art; every gameplay object retains its physical collider.</summary>
    public static class LowPolyVisual
    {
        public static Material Material(string name, Color color, bool luminous = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = name };
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.24f);
            if (luminous && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.6f);
            }
            return material;
        }

        public static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 localPosition,
            Vector3 scale, Material material, bool collider = false, int layer = 0)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = scale;
            item.layer = layer;
            item.GetComponent<Renderer>().sharedMaterial = material;
            Collider shapeCollider = item.GetComponent<Collider>();
            if (!collider && shapeCollider != null)
            {
                shapeCollider.enabled = false;
                Object.Destroy(shapeCollider);
            }
            return item;
        }

        public static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool collider = false, int layer = 0)
            => Shape(name, PrimitiveType.Cube, parent, position, scale, material, collider, layer);

        public static Transform Character(Transform parent, bool guard, Material uniform, Material dark, Material accent)
        {
            Transform body = new GameObject(guard ? "Guard silhouette" : "Agent silhouette").transform;
            body.SetParent(parent, false);
            Cube("Torso", body, new Vector3(0, 1.06f, 0), new Vector3(.62f, .72f, .34f), uniform);
            Cube("Vest", body, new Vector3(0, 1.08f, .2f), new Vector3(.48f, .5f, .07f), dark);
            Cube("Identification", body, new Vector3(0, 1.15f, .245f), new Vector3(.33f, .09f, .02f), accent);
            Shape("Helmet", PrimitiveType.Capsule, body, new Vector3(0, 1.65f, 0), new Vector3(.4f, .23f, .4f), uniform);
            Cube("Visor", body, new Vector3(0, 1.65f, .19f), new Vector3(.34f, .12f, .035f), accent);
            Cube("Left arm", body, new Vector3(-.4f, 1.04f, 0), new Vector3(.17f, .68f, .2f), uniform);
            Cube("Right arm", body, new Vector3(.4f, 1.04f, 0), new Vector3(.17f, .68f, .2f), uniform);
            Cube("Left leg", body, new Vector3(-.17f, .38f, 0), new Vector3(.22f, .75f, .25f), dark);
            Cube("Right leg", body, new Vector3(.17f, .38f, 0), new Vector3(.22f, .75f, .25f), dark);
            Cube("Backpack", body, new Vector3(0, 1.04f, -.26f), new Vector3(.44f, .52f, .22f), dark);
            if (guard)
            {
                Transform baton = Cube("Security baton", body, new Vector3(.45f, .83f, .18f), new Vector3(.08f, .48f, .08f), accent).transform;
                baton.SetParent(body.Find("Right arm"), true);
            }
            return body;
        }
    }
}
