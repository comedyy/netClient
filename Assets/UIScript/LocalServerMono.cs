
using System;
using System.Collections.Generic;
using UnityEngine;

public class RoomMember
{
    public string name;
    public int id;
}

public class LocalServerMono : MonoBehaviour
{
    // List<RoomMember> _allMembers = new List<RoomMember>();
    NetProcessor _netProcessor;

    public static LocalServerMono Instance;
    public bool isStartBattle;
    void Awake()
    {
        Instance = this;
    }

    private void OnDestroy() {
        _netProcessor?.Destroy();
        Instance = null;
    }


    void Update()
    {
        _netProcessor?.OnUpdate(Time.deltaTime);
    }

    internal void StartServer()
    {
        if(_netProcessor != null) return;

        _netProcessor = new NetProcessor(new GameServerSocket(10, 5000), 0);
        isStartBattle = true;
    }
}