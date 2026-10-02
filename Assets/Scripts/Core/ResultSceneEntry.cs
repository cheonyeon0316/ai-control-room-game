using UnityEngine;
using UnityEngine.SceneManagement;

namespace ControlRoom
{
    public sealed class ResultSceneEntry : MonoBehaviour
    {
        private void Start() { if (GameManager.Instance == null) SceneManager.LoadScene("Mission_01"); }
    }
}
