using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Boots the Home scene's UI stack, mirroring how <see cref="GameController"/> boots the gameplay scene:
    /// the scene controller owns <see cref="UIController"/> initialization, pages self-register as its children.
    /// </summary>
    public class HomeController : MonoBehaviour
    {
        [SerializeField] UIController uiController;

        private void Awake()
        {
            uiController.Init();
            uiController.InitPages();
        }

        private void Start()
        {
            UIController.ShowPage<UIHome>();
        }
    }
}
