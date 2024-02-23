using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class SyncCode
{
    [MenuItem("Tools/SyncCode")]
    public static void XXX()
    {
         var source = @"F:\dotnetServer\lockStepTest\Server";
        var dest = UnityEngine.Application.dataPath + @"\Script\Framework\Server";

        FileUtil.DeleteFileOrDirectory(dest);
        FileUtil.CopyFileOrDirectory(source, dest);

        // GameServerSocket
        source = @"F:\dotnetServer\NetWork\GameServerSocket.cs";
        dest = UnityEngine.Application.dataPath + @"\Script\Framework\Team\GameServerSocket.cs";

        File.Delete(dest);
        File.Copy(source, dest);

        source = @"D://ms-client/Assets/Scripts/Game/lockStepTest/Msg/IClientGameSocket.cs";
        dest = UnityEngine.Application.dataPath + @"\Script\Client\Team\IClientGameSocket.cs";

        File.Delete(dest);
        File.Copy(source, dest);

        // source = @"D://ms-client/Assets/Scripts/Game/lockStepTest/Team/ClientBattleRoomMgr.cs";
        // dest = UnityEngine.Application.dataPath + @"\Script\Client\Team\ClientBattleRoomMgr.cs";

        // File.Delete(dest);
        // File.Copy(source, dest);
        

        source = @"D://ms-client/Assets/Scripts/Game/lockStepTest/Team/GameClientSocket.cs";
        dest = UnityEngine.Application.dataPath + @"\Script\Client\Team\GameClientSocket.cs";

        File.Delete(dest);
        File.Copy(source, dest);


        source = @"D://ms-client/Assets/Scripts/LockStep/UIScript/RoomGUI.cs";
        dest = UnityEngine.Application.dataPath + @"/Script/Client/UIScript/RoomGUI.cs";

        File.Delete(dest);
        File.Copy(source, dest);
    }
}
