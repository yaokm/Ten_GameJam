namespace BattleshipGame.Core
{
    public enum Direction
    {
        Right,  // 默认横向（初始方向）
        Up,     // 逆时针90°（向上）
        Left,   // 逆时针180°（向左）
        Down    // 逆时针270°（向下）
    }
    public enum Skill{
        xianjing,//0,让对面停止一回合
        suijiquyv,//1，随机选择一个2*3区域打开
        lianhuan,//2，选择四点中格子的上下左右格子选择进行一个同时发射
        getone,//3 探出一个点位
    }
}