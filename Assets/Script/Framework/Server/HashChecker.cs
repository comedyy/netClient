using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

struct NotSameStruct
{
    public string name;
    public int index;
}

public class HashChecker
{
    Dictionary<int, FrameHash>[] _listHash;
    List<int> notSameIndexs = null;
    int _checkedFrame = 0;

    public bool NotSame => notSameIndexs != null && notSameIndexs.Any(m=>m != -1);

    public HashChecker(int maxCount)
    {
        _listHash = new Dictionary<int, FrameHash>[maxCount];
        for(int i = 0; i < _listHash.Length; i++)
        {
            _listHash[i] = new Dictionary<int, FrameHash>();
        }
    }

    public string[] AddHash(FrameHash hash)
    {
        if(NotSame) return null;
        if(_checkedFrame >= hash.frame) return null;

        var id = hash.id;
        var list = _listHash[id];
        list.Add(hash.frame, hash);

        return CheckHash(hash.frame);
    }

    private string[] CheckHash(int frame)
    {
        int count = _listHash.Count(m=>m.ContainsKey(frame));
        if(count == _listHash.Length)
        {
            return CheckIndex(frame);
        }

        return null;
    }

    private string[] CheckIndex(int v)
    {
        _checkedFrame = v;
        List<FrameHash> list = new List<FrameHash>();
        foreach(var x in _listHash)
        {
            list.Add(x[v]);
            FrameHash.Pool.Enqueue(x[v].allHashItems); // 回收
            x.Remove(v);
        }

        var first = list[0];
        if(first.allHashItems == null) return null;

        int hashCategoryCount = first.allHashItems.Length;
        if(notSameIndexs == null)
        {
            notSameIndexs = new List<int>();
            for(int i = 0; i < hashCategoryCount; i++) notSameIndexs.Add(-1);
        }

        for(int i = 0; i < hashCategoryCount; i++)
        {
            var isSame = list.All(m=>m.allHashItems[i] == first.allHashItems[i]);
            if(!isSame && notSameIndexs[i] < 0)
            {
                notSameIndexs[i] = v;
            }
        }

        if(NotSame)
        {
            var str = string.Join(",", notSameIndexs.Select((m, i)=>{
                return ((CheckSumType)first.allHashItems[i].hashType, m);
            }));
            LogError($"发现不一致的问题, {str}");

            if(list.Count > 1)
            {
                string[] lst = new string[list.Count];
                for(int i = 0; i < list.Count; i++)
                {
                    var hashInfos = WriteUnsyncToFile(list[i].allHashItems, 0, $"hashChecker_{i}.log", null);
                    lst[i] = hashInfos;
                }

                return lst;
            }
        }

        return null;
    }

    public static string WriteUnsyncToFile(FrameHashItem[] allHashDetails, float escaped, string logFile, List<string> symbol)
    {
        if(allHashDetails != null)
        {
            var list = new List<string>();
            var hashTypeCount = allHashDetails.Length;
            for(var hashTypeIndex = 0; hashTypeIndex < hashTypeCount; hashTypeIndex ++)
            {
                var hashItem = allHashDetails[hashTypeIndex];
                list.Add($"时间{escaped}【{hashItem.GetString(symbol)}】");
            }

            var text = string.Join("\n", list);
            SaveLogError(text, logFile /*"ReplayErrorLog.log"*/);

            return text;
        }

        return "";
    }

    static void SaveLogError(string v, string path)
    {
        #if UNITY_EDITOR
        File.WriteAllText("d://" + path, v + "\n");
        #elif UNITY_STANDALONE_WIN
        File.WriteAllText(path, v + "\n");
        #else
        File.WriteAllText(path, v + "\n");
        #endif
    }

    void LogError(string value)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.LogError(value);
        #if !UNITY_EDITOR
        File.AppendAllText(GetAppPath() + "ErrorLog.log", value + "\n");
        #endif
#endif
    }
}