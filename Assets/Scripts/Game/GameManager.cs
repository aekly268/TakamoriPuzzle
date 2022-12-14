using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
public class GameManager : MonoBehaviour
{
    public SlidingPuzzle slidingPuzzle = null;
    public DialogBox dialogBox = null;
    private string language;
    Sequence tweener;
    
    // 生命週期 --------------------------------------------------------------------------------------------------------------

    // Start is called before the first frame update
    void Start()
    {
        language = DataManager.instance.getLanguageCode();
        initSlidingPuzzle(() => {
            initDialogBox();
            DataManager.instance.LanguageChanged += onLanguageChanged;
            if (DataManager.instance.skipPassPuzzle
            && DataManager.instance.isPassLevel(DataManager.instance.episodeId, DataManager.instance.levelId)) {
                slidingPuzzle.quickFinishPuzzle();
            }
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDestroy()
    {
        if (tweener != null) {
            tweener.Kill();
        }
        DataManager.instance.LanguageChanged -= onLanguageChanged;
    }
    
    // 內部呼叫 --------------------------------------------------------------------------------------------------------------

    /** 初始化謎題 */
    private void initSlidingPuzzle(System.Action callback) {
        int episodeId = DataManager.instance.episodeId;
        int levelId = DataManager.instance.levelId;
        int puzzleGridX = DataManager.instance.puzzleGridX;
        int puzzleGridY = DataManager.instance.puzzleGridY;
        string puzzleImagePath = ResManager.getPuzzleImagePath(episodeId, levelId);
        StartCoroutine(ResManager.asyncLoadSprite(puzzleImagePath, (sprite) => {
            slidingPuzzle.init(sprite, puzzleGridX, puzzleGridY);
            slidingPuzzle.startPuzzle();
            slidingPuzzle.setFinishPuzzleCallback(handleFinishPuzzle);
            if (callback != null) {
                callback();
            }
        }));
    }

    /** 初始化對話 */
    private void initDialogBox() {
        dialogBox.init();
        initDialogData(getLevelAllData());
    }

    /** 初始化對話資料 */
    private void initDialogData(Hashtable objectAllData) {
        if (objectAllData == null) {
            Debug.LogError("Error initDialogBox : episodeId & levelId can not find.");
            return;
        }
        setDialogTextData(objectAllData);
        if(setDialogSelectData(objectAllData)) {
            dialogBox.setSelectCallback((ID) => {
                handleDialogSelect(ID);
            });
        }
        else {
            object unlockLevel = objectAllData["unlockLevelId"];
            if ((string)unlockLevel != "") {
                int unlockID = int.Parse((string)unlockLevel);
                dialogBox.setFinishCallback(() => {
                    handleDialogFinish(unlockID);
                });
            }
        }
    }

    /** 取得關卡配置資料 */
    private Hashtable getLevelAllData() {
        int episodeId = DataManager.instance.episodeId;
        int levelId = DataManager.instance.levelId;
        List<Hashtable> objList = LoadExcel.instance.getObjectList("all", "episodeId", episodeId.ToString());
        foreach(Hashtable item in objList) {
            if (item["levelId"].ToString() == levelId.ToString()) {
                return item;
            }
        }
        return null;
    }

    /** 設定對話資料 */
    private void setDialogTextData(Hashtable objectAllData) {
        string contentId = (string)objectAllData["contentId"];
        Hashtable contentData = LoadExcel.instance.getObject("content", "id", contentId);
        string message = (string)contentData[language + "_story"];
        dialogBox.setMessageData(message);
    }

    /** 設定選項資料 */
    private bool setDialogSelectData(Hashtable objectAllData) {
        string contentId = (string)objectAllData["contentId"];
        Hashtable contentData = LoadExcel.instance.getObject("content", "id", contentId);
        int selectCount = 0;
        while(true) {
            int count = selectCount + 1;
            string chioceKey = "chioceId_" + count.ToString();
            if ((string)objectAllData[chioceKey] != "") {
                string contentKey = language + "_chioce_" + count.ToString();
                string selectText = (string)contentData[contentKey];
                int selectID = int.Parse((string)objectAllData[chioceKey]);
                dialogBox.addSelectData(selectText, selectID);
                selectCount++;
            }
            else {
                break;
            }
        }
        if (selectCount > 0) {
            return true;
        }
        return false;
    }

    /** 監聽語言更換 */
    private void onLanguageChanged(object sender, string languageCode) {
        if (language == languageCode) {
            return;
        }
        language = languageCode;
        DIALOG_BOX_STATE state = dialogBox.getState();
        switch(state) {
            case DIALOG_BOX_STATE.NONE: {
                Hashtable objectAllData = getLevelAllData();
                dialogBox.clearAllTextData();
                dialogBox.clearAllSelectData();
                setDialogTextData(objectAllData);
                setDialogSelectData(objectAllData);
            } break;
            case DIALOG_BOX_STATE.PLAYING: {
                Hashtable objectAllData = getLevelAllData();
                dialogBox.clearAllTextData();
                dialogBox.clearAllSelectData();
                setDialogTextData(objectAllData);
                setDialogSelectData(objectAllData);
                dialogBox.playMessage();
            } break;
            case DIALOG_BOX_STATE.SELECT: {
                Hashtable objectAllData = getLevelAllData();
                dialogBox.clearAllSelectData();
                setDialogSelectData(objectAllData);
                dialogBox.showSelect();
            } break;
            default: {
            } break;
        }
    }

    /** 處理完成謎題 */
    private void handleFinishPuzzle() {
        tweener = DOTween.Sequence();
        tweener.AppendInterval(0.1f);
        tweener.InsertCallback(0.5f, () => {
            dialogBox.playMessage();
        });
        tweener.Join(DOTween.To(
            () => { return slidingPuzzle.transform.parent.position; },
            (value) => { slidingPuzzle.transform.parent.position = value; },
            new Vector3(0, 0.74f, 0),
            1.0f
        ).SetEase(Ease.OutCubic));
        tweener.Play();
    }

    /** 處理選擇選項 */
    private void handleDialogSelect(int ID) {
        Debug.Log("選擇解鎖關卡:" + ID);
        handleUnlockLevel(ID);
        handleFinishLevel(ID);
    }
    
    /** 處理對話結束 */
    private void handleDialogFinish(int ID) {
        Debug.Log("直接解鎖關卡:" + ID);
        handleUnlockLevel(ID);
        handleFinishLevel(ID);
    }

    /** 處理解鎖關卡 */
    private void handleUnlockLevel(int ID) {
        int episode = DataManager.instance.episodeId;
        DataManager.instance.unlockLevel(episode, ID);
    }
    
    /** 處理完成關卡 */
    private void handleFinishLevel(int nextLevel) {
        int episode = DataManager.instance.episodeId;
        int levelId = DataManager.instance.levelId;
        List<Hashtable> levelList = LoadExcel.instance.getObjectList("all", "episodeId", episode.ToString());
        List<int> passList = DataManager.instance.getPassLevelList(episode);
        int awardID = levelList.Count + 1;
        int nowPass;

        DataManager.instance.passLevel(episode, levelId);
        DataManager.instance.levelId = nextLevel;
        passList = DataManager.instance.getPassLevelList(episode);
        nowPass = passList.Count;

        if (DataManager.instance.isPassLevel(awardID) == false && (nowPass >= levelList.Count)) {
            DataManager.instance.passLevel(episode, awardID);
            DataManager.instance.episodeClear = true;
            Debug.Log("已通過全部關卡");
            Debug.Log("解鎖第" + (levelList.Count+1) + "張cg");
        }

        if (nextLevel != 0) {
            MySceneManager.Instance.SetLoadSceneState(SceneState.Game);
            MySceneManager.Instance.LoadScene();
        }
        else {
            MySceneManager.Instance.SetLoadSceneState(SceneState.SelectLevel);
            MySceneManager.Instance.LoadScene();
        }
    }
}
