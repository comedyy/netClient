
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LiteNetLib.Utils;
using UnityEngine;

[Serializable]
struct PlaybackInfo
{
    public PlayBackInfoItem[] infos;
}

[Serializable]
struct PlayBackInfoItem
{
    public int frame;
    public List<MessageItem> item;
}

public abstract class LocalFrame
{
    public float preFrameSeconds;
    public float totalTime;
    protected float _tick;
    public int ReceivedServerFrame;

    public int _clientStageIndex;

    protected MessageItem? _messageItem;
    protected int _controllerId = 0;
    public int ControllerId => _controllerId;
    public bool IsInsideGame;
    public static LocalFrame Instance;
    public bool IsPaused {get; protected set;}
    public bool BattleEnd{get;private set;}
    public bool Win{get;private set;}
    public List<int> _listHelpAiUser = new List<int>(); // 哪些玩家需要帮忙

    public LocalFrame(){}

    public LocalFrame(float tick, int id)
    {
        Instance = this;
        _tick = tick;
        _controllerId = id;
    }

    public virtual void Update()
    {
    }

    protected Dictionary<int , List<MessageItem>> _allMessage = new Dictionary<int, List<MessageItem>>();

    public void GetFrameInput(int frame, List<MessageItem> listOut)
    {
        if(_allMessage.TryGetValue(frame, out var list))
        {
            listOut.AddRange(list);
            // Game.ListPool<MessageItem>.Release(list);
            _allMessage.Remove(frame);

            // foreach (var item in listOut)
            // {
            //     if((item.messageBit & MessageBit.Ping) > 0 && item.id == _controllerId)
            //     {
            //         _totalRoundTripTime =  (int)(Time.time * 1000) -  item.ping.msTime;
            //     }
            // }
        }
    }

    public virtual bool IsClientBattle => true;
    public virtual bool IsNetBattle => false;
    public virtual bool IsPlayback => false;
    public virtual bool IsSyncTest => false;
    
    public abstract bool IsClientNormalBattle{get;}
    public bool CanControl => !(IsPlayback || AiControl);
    public bool NeedPopupSkillWin => IsAutoPopupSkillWinSetting;
    public virtual bool IsInPreloadState => false;

    protected bool StageLogicEnd { get;  set; } = true;
    public virtual bool IsCanLoading => false;
    public virtual bool CanEnterGame => true;

    public int GameFrame { get; internal set; }

    public void Destroy()
    {
        Instance = null;
        OnBattleDestroy();
    }
    internal virtual void OnBattleDestroy(){}

    #region sendMsg
    
    int _lastSendLogicPingFrame = 0;
    MessagePosItem? _preMoveItem;
    public void SetPos(MessagePosItem messageItem)
    {
        if(_preMoveItem != null && _preMoveItem.Value == messageItem)
        {
            return;
        }

        MakeMessageHead();

        var x = _messageItem.Value;
        x.messageBit |= MessageBit.Pos;
        x.posItem = messageItem;
        _messageItem = x;

        _preMoveItem = messageItem;
    }

    #endregion

    #region 
    private void MakeMessageHead()
    {
        if(_messageItem == null)
        {
            var needAppendPingInfo = _messageItem == null &&  GameFrame - _lastSendLogicPingFrame > 20;

            _messageItem = new MessageItem()
            {
                id = (uint)_controllerId,
            };
        }
    }

    public bool NeedCalHash(int frameCount)
    {
        #if DEBUG_1 || DEBUG_2
        return true;
        #else
        return frameCount % 100 == 0;
        #endif
    }
    #endregion

    int _totalRoundTripTime = 0;
    public int SocketRoundTripLogicTime => _totalRoundTripTime;

    public bool IsAutoPopupSkillWinSetting { get; set; }
    public bool DisablePresentation {get;set;} = false;
    public virtual bool AiControl => _listHelpAiUser.Contains(_controllerId);
    public virtual bool IsPendingSkillChooseWhenRoundEnd{get; protected set;} = true;
    public int Speed { get; internal set; } = 16;
    public virtual int LastCanExecuteFrame => ReceivedServerFrame;

    public void SetHelpAi(IEnumerable<int> offListUsers)
    {
        HashSet<int> preUsers = new HashSet<int>(_listHelpAiUser);
        _listHelpAiUser.Clear();

        if(offListUsers != null)
        {
            _listHelpAiUser.AddRange(offListUsers);
        }

        ClientBattleRoomMgr.Instance().LogMessage("addSetHelpAi" + _listHelpAiUser.Count);

        // if(MainNet.IsNetGameAutoControl && !_listHelpAiUser.Contains(_controllerId))
        // {
        //     _listHelpAiUser.Add(_controllerId);
        // }

        // var autoSelectSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<AutoSelectSkillSystem>();
        // if(autoSelectSystem != null)
        // {
        //     foreach(var x in preUsers)
        //     {
        //         if(!_listHelpAiUser.Contains(x))
        //         {
        //             // false
        //             autoSelectSystem.SetUserNeedHelpAI(x, false);
        //         }
        //     }

        //     foreach(var x in _listHelpAiUser)
        //     {
        //         if(!preUsers.Contains(x))
        //         {
        //             // true
        //             autoSelectSystem.SetUserNeedHelpAI(x, true);
        //         }
        //     }
        // }
    }
}