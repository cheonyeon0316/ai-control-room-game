using UnityEngine;
using UnityEngine.SceneManagement;

namespace ControlRoom
{
    public sealed class SceneBoot : MonoBehaviour
    {
        private void Start() { SceneManager.LoadScene("Mission_01"); }
    }
}
