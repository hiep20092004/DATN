using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.SceneManagement;
using WaterFlow.Framework.Systems.UserData;
using TMPro;
using UnityEngine;

public class PopupSettingsBase : Panel
{
    public GameObject homeBtn;
    [SerializeField] protected TMP_Text txtVersion;
    [SerializeField] protected RectTransform panel;
    [SerializeField] protected int levelCanGoHome = 0;
    [SerializeField] protected GameObject[] gameplayOnlyObjs;
    protected readonly Service<SceneService> sceneService = new();
    protected bool clicked = false;
    [SerializeField] protected float sizeFull = 1385f;
    [SerializeField] protected float sizeSmall = 1024f;

    public override void OnSetup()
    {
        base.OnSetup();
        txtVersion.text = "v" + Application.version;
    }

    public override void Open(UIData uiData)
    {
        base.Open(uiData);

        if (uiData == null)
        {
            bool isHome = sceneService.Instance.GetCurrentGamePlacement() == GamePlacement.Home;
            homeBtn.SetActive(!isHome && GameSystem.GetService<UserDataService>().GetLevel() > levelCanGoHome);
            foreach (GameObject gameObj in gameplayOnlyObjs)
            {
                gameObj.SetActive(!isHome);
            }

            panel.sizeDelta = isHome ? new Vector2(panel.sizeDelta.x, sizeSmall) : new Vector2(panel.sizeDelta.x, sizeFull);

            return;
        }

        panel.sizeDelta = new Vector2(panel.sizeDelta.x, sizeFull);
        foreach (GameObject gameObj in gameplayOnlyObjs)
        {
            gameObj.SetActive(true);
        }

        clicked = false;
    }

    public virtual void PolicyClick()
    {
        // TODO: point at this project's privacy policy URL.
    }

    public virtual void HomeClick()
    {
        if (clicked) return;
        clicked = true;
        GoHome();
    }

    protected virtual void GoHome()
    {
        CloseImmediately();
        LoadHomeScene();
    }

    public virtual void ReplayClick()
    {
        if (clicked) return;
        clicked = true;
        Replay();
    }

    protected virtual void Replay()
    {
        Close();
        //Replay
    }

    protected virtual void LoadHomeScene()
    {
        //LoadHome
    }
}
