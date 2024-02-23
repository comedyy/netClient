using System;
using UnityEngine;
using System.Collections.Generic;

public class RoomGUI : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    RoomInfoMsg[] roomList;
    public RoomUser[] _userList => ClientBattleRoomMgr.Instance()._userList;
    bool _canQuery = true;


    public Func<int, (BattleStartMessage, ClientRoomShowInfo)> GetStartMessage;
    public Func<int, bool, (JoinMessage, ClientUserJoinShowInfo)> GetJoinMessage;
    public Func<IntPair2[]> GetStartRoomCondition;
    uint userId 
    {
        get
        {
            var y = PlayerPrefs.GetInt(Application.dataPath +"netPlayerId");
            if(y == 0)
            {
                y = UnityEngine.Random.Range(1000, 100000);
                PlayerPrefs.SetInt(Application.dataPath + "netPlayerId", y);
            }
            return (uint)y;
        }
    }

    uint robertUserCount = 0;
    public uint robertUserId
    {
        get{
            robertUserCount++;
            return 100000 + userId * 100 + robertUserCount;
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
            if(LocalFrame.Instance != null && LocalFrame.Instance._clientStageIndex < 1)
            {
                GUI.color = Color.red;
                for(int i = 0; i < _userList.Length; i++)
                {
                    int widthIndex = 0;
                    GUI.Label(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), _userList[i].name);

                    ClientBattleRoomMgr.Instance()._dicLoadingProcess.TryGetValue((int)_userList[i].userId, out var process);
                    GUI.Label(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), $"process:{process}");
                }
            }
            else if(LocalFrame.Instance != null && LocalFrame.Instance is LocalFrameNetGame netPing)
            {
                GUI.Label(new Rect(Screen.width - 300, 0, 100, 50), $"ping:{netPing.SocketRoundTripTime}");

                var logicPing = netPing.SocketRoundTripLogicTime;
                var processPerSec = netPing.FrameProcessPerSec;
                var receivePerSec = netPing.ReceiveFramePerSec;
                GUI.Label(new Rect(Screen.width - 200, 0, 200, 50), $"L:{logicPing} P:{processPerSec} R:{receivePerSec}");
                if(GUI.Button(new Rect(Screen.width - 400, 0, 100, 50), "disconnect"))
                {
                    ClientBattleRoomMgr.Instance().DEBUG_Disconnect();
                }
            }
        }
    }

    void DrawOutsideRoom()
    {
        // show all Rooms
        if(roomList != null)
        {
            GUI.color = Color.red;
            var roomCount = roomList.Length;
            if(GUI.Button(new Rect(500, 0, 150, 50), "创建机器人房间"))
            {
                CreateRobertRoomAsync();
            }

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
                    if(GUI.Button(new Rect(400, i * 50, 100, 50), "加入"))
                    {
                        JoinAsync(roomList[i].updateRoomMemberList);
                    }
                }
            }

            GUI.color = Color.white;
        }
        
        if(GUI.Button(new Rect(0, 150, 100, 50), "创建"))
        {
            (var startMessage, var roomShowInfo) = GetStartMessage(0);
            (var joinMessage, var joinShowInfo) = GetJoinMessage(0, true);
            var condition = GetStartRoomCondition();
            
            ClientBattleRoomMgr.Instance().CreateRoom(startMessage, joinMessage, joinShowInfo, roomShowInfo, condition);
        }

        port = int.Parse(GUI.TextField(new Rect(100, 100, 100, 50), port.ToString()));
        ip = GUI.TextField(new Rect(100, 150, 100, 50), ip);
        if(_canQuery && GUI.Button(new Rect(200, 150, 100, 50), "查询房间"))
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
                else if(GUI.Button(new Rect(400, 150, 100, 50), "Viet Nam"))
                {
                    ip = "101.36.102.203";
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
        PlayerPrefs.SetInt("NET_GAME_PORT", port);
        ClientBattleRoomMgr.Instance().ChangeIp(ip, port);
    }

    private void CreateRobertRoomAsync()
    {
        // (var startMessage, var roomShowInfo) = GetStartMessage(0);
        // (var joinMessage, var joinShowInfo) = GetJoinMessage(0, true);
        // joinMessage.userId = robertUserId;
        // var condition = GetStartRoomCondition();
        
        // ClientBattleRoomMgr.Instance().CreateRobertRoom(startMessage, joinMessage, joinShowInfo, roomShowInfo, condition);
    }

    private async void JoinAsync(UpdateRoomMemberList updateRoomMemberList)
    {
        var roomId = updateRoomMemberList.roomId;
        (var join, var showInfo) = GetJoinMessage(updateRoomMemberList.roomId, true);
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

            if(GUI.Button(new Rect(0, 350, 200, 50), "添加机器人"))
            {
                JoinRobertRoomAsync(ClientBattleRoomMgr.Instance().enterRoomId);
            }

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
                        if(iAmRoomMaster && GUI.Button(new Rect((widthIndex++) * 100, i * 50 + 400, 100, 50), "开始"))
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

    private async void JoinRobertRoomAsync(int enterRoomId)
    {
        // (var join, var showInfo) = GetJoinMessage(enterRoomId, true);
        // join.userId = robertUserId;
        // var ret = await ClientBattleRoomMgr.Instance().RobertJoinRoom(enterRoomId, join, showInfo);
        // if(ret != TeamRoomEnterFailedReason.OK)
        // {
        //     Debug.LogError("join failed" + ret);
        // }
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
    public bool AutoAddLevel = true;
    int _autoCreateFromLevel = 0;

    // 一秒一次
    float _lastTime = 0;
    float _inRoomTime = 0;
    List<int> ignoreRoomList = new List<int>();
    void Update()
    {
        var needAuto = AutoJoin || AutoCrate;
        if(!needAuto) return;
        
        if(Time.time - _lastTime < 1) return;
        _lastTime = Time.time;

        if(ClientBattleRoomMgr.Instance()._roomState == TeamRoomState.InSearchRoom)
        {
            _inRoomTime = 0;

            if(AutoJoin)
            {
                QueryAvailableRooms();
                if(roomList != null && roomList.Length > 0)
                {
                    foreach(var x in roomList)
                    {
                        if(ignoreRoomList.Contains(x.updateRoomMemberList.roomId)) continue;
                        JoinAsync(x.updateRoomMemberList);
                    }
                }
                return;
            }

            if(AutoCrate)
            {
                // var gui = FindObjectOfType<GameLogicGUI>();
                // gui.SetMain();
                // var level = (_autoCreateFromLevel++) % 99 + 1;
                // (var startMessage, var roomShowInfo) = GetStartMessage(level);
                // gui._minLevel = 200;//(uint)level;
                // gui._minStar = 10;//(uint)level / 10;

                // (var joinMessage, var joinShowInfo) = GetJoinMessage(0, true);
                // var condition = GetStartRoomCondition();
                
                // ClientBattleRoomMgr.Instance().CreateRoom(startMessage, joinMessage, joinShowInfo, roomShowInfo, condition);
                return;
            }
        }
        else if(ClientBattleRoomMgr.Instance()._roomState == TeamRoomState.InRoom)
        {
            if(_inRoomTime == 0) _inRoomTime = Time.time;

            var iAmRoomMaster = _userList[0].userId == userId;
            if(iAmRoomMaster)
            {
                if(AutoStartUserCount <= _userList.Length && Time.time - _inRoomTime > 10) // 在房间10秒之后才能开始战斗
                {
                    foreach(var x in ClientBattleRoomMgr.Instance()._updateRoomInfo.userList)
                    {
                        if(!x.isReady)
                        {
                            ClientBattleRoomMgr.Instance().KickUser((int)x.userId);
                        }
                    }

                    ClientBattleRoomMgr.Instance().StartRoom();
                }
            }
            else
            {
                // 如果有玩家一直不开始，退出。
                if(Time.time - _inRoomTime > 15)
                {
                    ignoreRoomList.Add(ClientBattleRoomMgr.Instance().enterRoomId);
                    ClientBattleRoomMgr.Instance().LeaveRoom();
                    return;
                }

                ClientBattleRoomMgr.Instance().ReadyRoom(true);
            }
        }
        else
        {
            _inRoomTime = 0;
            // 调整游戏速度。
            if(LocalFrame.Instance is LocalFrameNetGame netGame)
            {
                // netGame.DebugSetSererSpeed(5);
            }
        }
    }
}
