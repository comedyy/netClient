using System;
using UnityEngine;

public class RoomGUI : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    RoomInfoMsg[] roomList;
    public RoomUser[] _userList => ClientBattleRoomMgr.Instance()._userList;
    bool _canQuery = true;


    public Func<(BattleStartMessage, ClientRoomShowInfo)> GetStartMessage;
    public Func<int, (JoinMessage, ClientUserJoinShowInfo)> GetJoinMessage;
    public Func<IntPair2[]> GetStartRoomCondition;
    uint userId 
    {
        get
        {
            var y = PlayerPrefs.GetInt(Application.dataPath +"netPlayerId");
            if(y == 0)
            {
                y = UnityEngine.Random.Range(1000, 10000000);
                PlayerPrefs.SetInt(Application.dataPath + "netPlayerId", y);
            }
            return (uint)y;
        }
    }

    void Start()
    {
        ip = MainNet.LockstepServerUrl;
        port = MainNet.LockstepServerPort;

        var mono = gameObject.AddComponent<LocalServerMono>();
        if(AutoCreateLocalServer)
        {
            mono.StartServer();
            ip = "127.0.0.1";
        }

        gameObject.AddComponent<GameLogicGUI>().Init(this, userId);
        ClientBattleRoomMgr.Instance().SetUserId(userId);
    }


    public static string ip;
    public static int port;
    string _ipMac;
    void OnGUI()
    {
        if(ClientBattleRoomMgr.Instance()._roomState == TeamRoomState.InSearchRoom)
        {
            DrawOutsideRoom();
        }
        else if(ClientBattleRoomMgr.Instance()._roomState == TeamRoomState.InRoom)
        {
            DrawInsideRoom();
        }
        else if(ClientBattleRoomMgr.Instance()._roomState == TeamRoomState.InBattle)
        {
            // if(LocalFrame.Instance != null && LocalFrame.Instance._clientStageIndex < 1)
            // {
            //     GUI.color = Color.red;
            //     for(int i = 0; i < _userList.Length; i++)
            //     {
            //         int widthIndex = 0;
            //         GUI.Label(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), _userList[i].name);
            //         GUI.Label(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), $"{_userList[i].HeroId};{_userList[i].heroLevel};{_userList[i].heroStar}");

            //         ClientBattleRoomMgr.Instance()._dicLoadingProcess.TryGetValue((int)_userList[i].userId, out var process);
            //         GUI.Label(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), $"process:{process}");
            //     }
            // }
            // else if(LocalFrame.Instance != null && LocalFrame.Instance is LocalFrameNetGame netPing)
            // {
            //     GUI.Label(new Rect(Screen.width - 300, 0, 100, 50), $"ping:{netPing.SocketRoundTripTime}");

            //     var logicPing = netPing.SocketRoundTripLogicTime;
            //     var processPerSec = netPing.FrameProcessPerSec;
            //     var receivePerSec = netPing.ReceiveFramePerSec;
            //     GUI.Label(new Rect(Screen.width - 200, 0, 200, 50), $"L:{logicPing} P:{processPerSec} R:{receivePerSec}");
            //     if(GUI.Button(new Rect(Screen.width - 300, 0, 100, 50), "disconnect"))
            //     {
            //         ClientBattleRoomMgr.Instance().DEBUG_Disconnect();
            //     }
            // }
        }
    }

    void DrawOutsideRoom()
    {
        // show all Rooms
        if(roomList != null)
        {
            GUI.color = Color.red;
            var roomCount = roomList.Length;
            if(roomCount == 0)
            {
                GUI.Label(new Rect(0, 50, 300, 50), $"暂无房间");
            }
            else
            {
                for(int i = 0; i < roomCount; i++)
                {
                    var minStar = ClientBattleRoomMgr.GetMinCondition(roomList[i].updateRoomMemberList.conditions, RoomConditionType.Star);
                    var minLevel = ClientBattleRoomMgr.GetMinCondition(roomList[i].updateRoomMemberList.conditions, RoomConditionType.Level);
                    GUI.Label(new Rect(0, i * 50, 300, 50), $"ID: {roomList[i].updateRoomMemberList.roomId} version: {roomList[i].updateRoomMemberList.version} roomType: {roomList[i].updateRoomMemberList.roomType} roomLeve: {roomList[i].updateRoomMemberList.roomLevel} userCount: {roomList[i].updateRoomMemberList.userList.Length} condition {minLevel} {minStar}");
                    if(GUI.Button(new Rect(400, i * 50, 100, 50), "加入") || AutoJoin)
                    {
                        (var joinMessage, var joinShowInfo) = GetJoinMessage(roomList[i].updateRoomMemberList.roomId);
                        JoinAsync(roomList[i].updateRoomMemberList.roomId, joinMessage, joinShowInfo);
                    }
                }
            }

            GUI.color = Color.white;
        }
        
        if(GUI.Button(new Rect(0, 150, 100, 50), "创建") || AutoCrate)
        {
            (var startMessage, var roomShowInfo) = GetStartMessage();
            (var joinMessage, var joinShowInfo) = GetJoinMessage(0);
            var condition = GetStartRoomCondition();
            
            ClientBattleRoomMgr.Instance().CreateRoom(startMessage, joinMessage, joinShowInfo, roomShowInfo, condition);
        }

        ip = GUI.TextField(new Rect(100, 150, 100, 50), ip);
        if(_canQuery && (GUI.Button(new Rect(200, 150, 100, 50), "查询房间") || AutoJoin))
        {
            QueryAvailableRooms();
        }

        if(LocalServerMono.Instance != null)
        {
            if( !LocalServerMono.Instance.isStartBattle)
            {
                if(GUI.Button(new Rect(300, 150, 100, 50), "启用本地服务器"))
                {
                    LocalServerMono.Instance.StartServer();
                    ip = "127.0.0.1";
                }
                else if(GUI.Button(new Rect(400, 150, 100, 50), "MAC服务器"))
                {
                    ip = _ipMac;
                }

                else if(GUI.Button(new Rect(500, 150, 100, 50), "外网服务器"))
                {
                    ip = "106.75.214.130";
                    
                }
                else if(ClientBattleRoomMgr.Instance().ServerUserState != GetUserStateMsg.UserState.None && GUI.Button(new Rect(600, 150, 100, 50), "同步房间数据")) 
                {
                    // OnClickReconnect();
                    ClientBattleRoomMgr.Instance().ReconnectToServer(TeamConnectParam.SyncInfo);
                }
                else if(GUI.Button(new Rect(800, 150, 100, 50), "检测状态")) 
                {
                    ClientBattleRoomMgr.Instance().CheckRoomState();
                }
            }
            else
            {
                GUI.Label(new Rect(300 , 150, 100, 50), "本地服务器已经开启");
            }
        }

        PlayerPrefs.SetString("NET_GAME_IP", ip);
        ClientBattleRoomMgr.Instance().ChangeIp(ip, port);
    }

    private async void JoinAsync(int roomId, JoinMessage join, ClientUserJoinShowInfo showInfo)
    {
        var ret = await ClientBattleRoomMgr.Instance().JoinRoom(roomId, join, showInfo);
        if(ret != TeamRoomEnterFailedReason.OK)
        {
            Debug.LogError("join failed" + ret);
        }
    }

    private void DrawInsideRoom()
    {
        if(_userList != null)
        {
            GUI.color = Color.red;
            var iAmRoomMaster = _userList[0].userId == userId;

            for(int i = 0; i < _userList.Length; i++)
            {
                int widthIndex = 0;
                GUI.Label(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), _userList[i].name);
                GUI.Label(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), _userList[i].userId.ToString());
                GUI.Label(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), $"在线：{_userList[i].isOnLine}");
                GUI.Label(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), $"准备：{_userList[i].isReady}");

                var isSelf = _userList[i].userId == userId;
                var currentIsRoomMaster = i == 0;
                if(isSelf)  // 自己的操作。
                {
                    if(GUI.Button(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), "退出"))
                    {
                        ClientBattleRoomMgr.Instance().LeaveRoom();
                    }

                    if(GUI.Button(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), "断线"))
                    {
                        ClientBattleRoomMgr.Instance().DEBUG_Disconnect();
                    }

                    if(currentIsRoomMaster)
                    {
                        if(iAmRoomMaster && (GUI.Button(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), "开始") || AutoStartUserCount >= _userList.Length))
                        {
                            ClientBattleRoomMgr.Instance().StartRoom();
                        }
                    }
                    else
                    {
                        if(GUI.Button(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), $"ready：{_userList[i].isReady}"))
                        {
                            ClientBattleRoomMgr.Instance().ReadyRoom(!_userList[i].isReady);
                        }
                    }
                }
                else if(iAmRoomMaster) // 群主对别人的操作。
                {
                    if(GUI.Button(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), "踢出"))
                    {
                        ClientBattleRoomMgr.Instance().KickUser((int)_userList[i].userId);
                    }

                    for(int k = 1; k < _userList.Length; k++)
                    {
                        if(k != i)
                        {
                            if(iAmRoomMaster && GUI.Button(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), $"=>{k}"))
                            {
                                ClientBattleRoomMgr.Instance().ChangeUserPos(i, k);
                            }
                        }
                    }
                }
            }

            GUI.color = Color.white;
        }
    }

    private async void QueryAvailableRooms()
    {
        _canQuery = false;
        roomList = await ClientBattleRoomMgr.Instance().QueryRoomListAsync();
        _canQuery = true;
    }

    public bool AutoJoin;
    public bool AutoCrate;
    public int AutoStartUserCount;
    public bool AutoCreateLocalServer;
}
