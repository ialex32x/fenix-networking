namespace Fenix.Net
{
    public enum ConnectionError
    {
        ConnectionError = -1, // 未知错误
        None = 0,             // 正常
        Unreachable = 1,      // 找不到远端
        Refused = 2,          // 拒绝连接
        Aborted = 3,          // 远端关闭
        Lost = 4,             // 丢失
    }
}
