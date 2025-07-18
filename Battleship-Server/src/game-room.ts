import { Room, Client } from "colyseus";

import { State, Player } from './game-state';
const paodanCount: number = 1;
export class GameRoom extends Room<State> {
    rematchCount: any = {};
    maxClients: number = 2;
    password: string;
    name: string;
    gridSize: number = 10;
    startingFleetHealth: number = 25;
    placements: any={};
    playerHealth: any={};
    playersPlaced: number = 0;
    playerCount: number = 0;
    eDirections: any={};//敌军方向
    eBasePositions: any={};//敌军基座位置
    playerSkipNextTurn: {[key: string]: boolean} = {}; // 用于跟踪玩家是否需要跳过下一回合
    onCreate(options) {
        console.log(options);
        if (options.password) {
            this.password = options.password;
            this.name = options.name;
            // this.setPrivate();
        }
        this.reset();
        this.setMetadata({ name: options.name || this.roomId, requiresPassword: !!this.password });
        this.onMessage("place", (client, message) => this.playerPlace(client, message));
        this.onMessage("turn", (client, message) => this.playerTurn(client, message));
        this.onMessage("rematch", (client, message) => this.rematch(client, message));
        this.onMessage("direction", (client, message) => this.playerDirection(client, message));
        // 添加对opponentInfoRequest消息的处理
        this.onMessage("opponentInfoRequest", (client, message) => this.handleOpponentInfoRequest(client));
    }
    // 添加新方法处理对手信息请求
    handleOpponentInfoRequest(client: Client) {
        // 获取当前玩家
        const player = this.state.players[client.sessionId];

        // 获取对手
        const enemyId = Object.keys(this.state.players).find(id => id !== client.sessionId);
        if (!enemyId) return; // 如果没有对手，直接返回

        // 获取对手的方向和基点信息
        const enemyDirections = this.eDirections[enemyId] || [];
        const enemyBasePositions = this.eBasePositions[enemyId] || [];

        // 发送对手信息给请求的客户端
        client.send("opponentInfo", {
            directions: enemyDirections,
            basePositions: enemyBasePositions
        });
    }
    onJoin(client: Client) {
        let player: Player = new Player(client.sessionId, this.gridSize * this.gridSize, this.startingFleetHealth);
        this.state.players[client.sessionId] = player;
        this.playerCount++;

        if (this.playerCount == 2) {
            this.state.phase = 'place';
            this.lock();
        }
    }

    onAuth(client: Client, options: any) {
        if (!this.password) {
            return true
        };

        if (!options.password) {
            throw new Error("This room requires a password!")
        };

        if (this.password === options.password) {
            return true;
        }
        throw new Error("Invalid Password!");
    }

    onLeave(client: Client) {
        delete this.state.players[client.sessionId];
        delete this.playerHealth[client.sessionId];
        this.playerCount--;
        this.playersPlaced = 0;
        this.state.phase = 'waiting';
        this.placements = {};
        this.unlock();
    }

    playerPlace(client: Client, message: any) {
        let player: Player = this.state.players[client.sessionId];
        //console.log("Received message:", JSON.stringify(message));
            // 确保对象已初始化
        this.placements = this.placements || {};
        this.eDirections = this.eDirections || {};
        this.eBasePositions = this.eBasePositions || {};
        //cell,direction,basePositions
        this.placements[player.sessionId] = message.placement;
        this.eDirections[player.sessionId] = message.directions;
        this.eBasePositions[player.sessionId] = message.basePositions;
        
        this.playersPlaced++;

        if (this.playersPlaced == 2) {
            Object.keys(this.state.players).forEach(key => {
                this.playerHealth[this.state.players[key].sessionId] = this.startingFleetHealth;
            });
            this.state.playerTurn = this.getRandomUser();
            this.state.phase = 'battle';
            // 双方都布阵完成后，主动向双方发送对方的船只信息
            Object.keys(this.state.players).forEach(playerId => {
                const enemyId = Object.keys(this.state.players).find(id => id !== playerId);
                console.log("enemyId:", enemyId);
                if (enemyId) {
                    const client = this.clients.find(c => c.sessionId === playerId);
                    if (client) {
                        console.log("eDirections:", this.eDirections[enemyId]);
                        console.log("eBasePositions:", this.eBasePositions[enemyId]);
                        client.send("opponentInfo", {
                            directions: this.eDirections[enemyId] || [],
                            basePositions: this.eBasePositions[enemyId] || []
                        });
                    }
                }
            });
        }
    }

    getRandomUser() {
        const keys = Object.keys(this.state.players);
        return this.state.players[keys[keys.length * Math.random() << 0]].sessionId;
    }

    getNextUser() {
        return this.state.players[Object.keys(this.state.players).filter(key => key != this.state.playerTurn)[0]];
    }

    playerTurn(client: Client, message: any) {
        const player: Player = this.state.players[client.sessionId];

        if (this.state.playerTurn != player.sessionId) return;

        const [targetIndex] = message;        // 只取 1 个坐标

        if (targetIndex === undefined) return; // Ensure targetIndex is defined

        if (targetIndex < 0 || targetIndex >= this.gridSize * this.gridSize) return; // Basic bounds check

        const enemy = this.getNextUser();

        let shots = player.shots;
        let targetShips = enemy.ships;
        let targetedPlacement = this.placements[enemy.sessionId];

        let hit = false;                      // ← 记录是否命中
        let isXBomb = false;                  // ← 新增：记录是否命中X炸弹船

        if (shots[targetIndex] == -1) {
            shots[targetIndex] = this.state.currentTurn;
            if (targetedPlacement[targetIndex] >= 0) {
                hit = true;
                this.playerHealth[enemy.sessionId]--;
                switch (targetedPlacement[targetIndex]) {
                    case 0: // F0
                        this.updateShips(targetShips, 0, 6, this.state.currentTurn);
                        break;
                    case 1: // E0
                        this.updateShips(targetShips, 6, 11, this.state.currentTurn);
                        break;
                    case 2: // D0
                        this.updateShips(targetShips, 11, 15, this.state.currentTurn);
                        break;
                    case 3: // C0
                        this.updateShips(targetShips, 15, 18, this.state.currentTurn);
                        break;
                    case 4: // B0
                        this.updateShips(targetShips, 18, 20, this.state.currentTurn);
                        break;
                    case 5: // A0
                        this.updateShips(targetShips, 20, 21, this.state.currentTurn);
                        break;
                    case 6: // D1
                        this.updateShips(targetShips, 21, 25, this.state.currentTurn);
                        break;
                    case 7: // X，是个炸弹，踩到会卡对面一回合
                        isXBomb = true;  // 标记命中了X炸弹
                        this.playerSkipNextTurn[player.sessionId] = true; // 修改为标记当前玩家需要跳过下一回合
                        hit = false; // 踩到炸弹不算命中，需要结束回合
                        // 发送一个特殊消息通知客户端，玩家被炸弹影响
                        this.broadcast("xBombHit", {
                            player: player.sessionId,
                            victim: player.sessionId // 受害者是玩家自己
                        });
                        break;
                    case 8: // S
                        break;
                }
            }
        }

        if (this.playerHealth[enemy.sessionId] <= 0) {
            this.state.winningPlayer = player.sessionId;
            this.state.phase = 'result';
        } else {
            // 未命中或踩到炸弹：换对手并开始下一回合；命中普通船：保持当前玩家，不递增回合
            if (!hit) {
                // 正常切换回合
                this.state.playerTurn = enemy.sessionId;
                this.state.currentTurn++;
                
                // 检查下一回合的玩家是否需要跳过回合
                if (this.playerSkipNextTurn[enemy.sessionId]) {
                    // 对手需要跳过下一回合，直接切换回玩家
                    this.playerSkipNextTurn[enemy.sessionId] = false; // 重置跳过标记
                    this.state.playerTurn = player.sessionId; // 回合切回当前玩家
                    this.state.currentTurn++; // 回合数再次增加
                    // 发送通知消息
                    this.broadcast("skipTurn", {
                        player: enemy.sessionId
                    });
                }
            } else {
                // 命中普通船：保持当前玩家回合
                this.state.playerTurn = player.sessionId;
            }
        }
    }

    onDispose() { }

    rematch(client: Client, message: Boolean) {
        if (!message) {
            return this.state.phase = "leave";
        }

        this.state.players[client.sessionId].reset(this.gridSize * this.gridSize, this.startingFleetHealth);

        this.rematchCount[client.sessionId] = message;

        if (Object.keys(this.rematchCount).length == 2) {
            // this.reset(true);
            this.rematchCount = {};
            this.playerHealth = {};
            this.placements = {};
            this.state.playerTurn = "";
            this.state.winningPlayer = "";
            this.state.currentTurn = 1;
            this.playersPlaced = 0;
            this.state.phase = 'place';
        }
    }
    playerDirection(client: Client, message: any) {
        const player: Player = this.state.players[client.sessionId];
        this.eDirections[player.sessionId] = message;
    }
    reset() {
        this.rematchCount = {};
        this.playerHealth = {};
        this.placements = {};
        this.playersPlaced = 0;
        let state = new State();
        state.phase = 'waiting';
        state.playerTurn = "";
        state.winningPlayer = "";
        state.currentTurn = 1;
        this.setState(state);
        this.eDirections = {};
        this.eBasePositions = {};
    }

    updateShips(arr: number[], s: number, e: number, t: number) {
        for (let i = s; i < e; i++) {
            if (arr[i] == -1) {
                arr[i] = t;
                break;
            }
        }
    }
}