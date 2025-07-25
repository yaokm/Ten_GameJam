import { Schema, type, MapSchema, ArraySchema, } from "@colyseus/schema";

export class Player extends Schema {
    @type('string')
    sessionId: string;

    @type(['int8'])
    shots: ArraySchema<number>;

    @type(['int8'])
    ships: ArraySchema<number>;

    @type(['int8'])
    usedSkills: ArraySchema<number>;

    constructor(sessionId: string, shotsSize, shipsSize) {
        super();
        this.sessionId = sessionId;
        this.reset(shotsSize, shipsSize);
        this.usedSkills = new ArraySchema<number>(0, 0, 0, 0); // 4种技能
    }

    reset(shotsSize, shipsSize) {
        this.shots = new ArraySchema<number>(...new Array(shotsSize).fill(-1));
        this.ships = new ArraySchema<number>(...new Array(shipsSize).fill(-1));
        this.usedSkills = new ArraySchema<number>(0, 0, 0, 0); // 重置技能
    }
}

export class State extends Schema {
    @type({ map: Player })
    players: MapSchema<Player> = new MapSchema<Player>();

    @type('string')
    phase: string = 'waiting';

    @type('string')
    playerTurn: string;//当前回合玩家

    @type('string')
    winningPlayer: string;

    @type('int8')
    currentTurn: number = 1;//当前回合数
}