﻿using BattleshipGame.Core;

namespace BattleshipGame.Network
{
    public static class RoomMessage
    {
        public const string Place = "place";
        public const string Rematch = "rematch";
        public const string Turn = "turn";
        public const string OpponentInfoRequest="opponentInfoRequest";
        public const string BasePosition="basepostion";
        public const string Direction="direction";
        public const string UseSkill = "useSkill";
        public const string SkillUsed = "skillUsed";
    }
}