using UnityEngine;
using Steamworks;

public class AchiManager : MonoBehaviour
{
    public static AchiManager Instance; // 어디서든 접근 가능하게 추가

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [System.Serializable]
    public struct achiID
    {
        public string steamID;
        public string androidID;
    }
    [SerializeField] achiID[] achiIDs;

    public int platform;
    bool isAchiUnlocked;

    public void UnlockAchi(int _index)
    {
        isAchiUnlocked = false;
        switch(platform)
        {
            case 0:
                TestSteamAchi(achiIDs[_index].steamID);
                Debug.Log($"Achi with ID : {achiIDs[_index].steamID} unlocked = {isAchiUnlocked}");
                if(!isAchiUnlocked)
                {
                    SteamUserStats.SetAchievement(achiIDs[_index].steamID);
                    SteamUserStats.StoreStats();
                }
                break;

            default:
                break;
        }
    }

    void TestSteamAchi(string _id)
    {
        SteamUserStats.GetAchievement(_id, out isAchiUnlocked);
    }

    public void RelockAchi(int _index)
    {
        TestSteamAchi(achiIDs[_index].steamID);
        if(isAchiUnlocked)
        {
            SteamUserStats.ClearAchievement(achiIDs[_index].steamID);
            SteamUserStats.StoreStats();
        }
    }
} 
